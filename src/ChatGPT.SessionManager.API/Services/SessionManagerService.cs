using System.Text.Json;
using ChatGPT.SessionManager.API.Models;

namespace ChatGPT.SessionManager.API.Services;

public class SessionManagerService : ISessionManagerService
{
    private readonly string _filePath = "UserEntities.json";
    private readonly ILogger<SessionManagerService> _logger;

    public event EventHandler<(UserEntity, UserChangedAction)> UserChanged;
    public event EventHandler<bool> LockStatusChanged;

    public SessionManagerService(ILogger<SessionManagerService> logger)
    {
        _logger = logger;

        var filePath = Path.Combine("/app/data", _filePath);
        var fileExists = File.Exists(filePath);
        
        if (!fileExists)
        {
            File.Create(filePath).Dispose();
        }
    }

    public async Task<IEnumerable<UserEntity>> GetAllUsers()
    {
        _logger.LogInformation("Getting all users");
        return await GetEntitiesFromFile();
    }

    public async Task<UserEntity> GetUserById(string id)
    {
        _logger.LogInformation("Getting user by id");
        
        var entities = await GetEntitiesFromFile();
        return entities.FirstOrDefault(u => u.Id == id);

    }

    public async Task<UserEntity> GetUserByName(string name)
    {
        _logger.LogInformation("Getting user {name}", name);
        
        var entities = await GetEntitiesFromFile();
        return entities.FirstOrDefault(u => u.Name == name);
    }

    public async Task<UserEntity> AddUser(UserEntity newUser)
    {
        _logger.LogInformation("Adding user session {id}", newUser.Id);
        
        var entities = await GetEntitiesFromFile();
        
        entities.Add(newUser);
        await SaveToFileAsync(entities);
        UserChanged?.Invoke(this, (newUser, UserChangedAction.Added));
        return newUser;
    }

    public async Task<bool> UpdateUser(UserEntity updatedUser)
    {
        _logger.LogInformation("Updating user {id}", updatedUser.Id);
        
        var entities = await GetEntitiesFromFile();

        var user = entities.FirstOrDefault(u => u.Id == updatedUser.Id);
        if (user == null)
        {
            return false;
        }

        user.Name = updatedUser.Name;
        await SaveToFileAsync(entities);
        UserChanged?.Invoke(this, (updatedUser, UserChangedAction.Updated));
        return true;
    }

    public async Task<bool> DeleteUser(string id)
    {
        _logger.LogInformation("Deleting user {id}", id);
        
        var entities = await GetEntitiesFromFile();

        var user = entities.FirstOrDefault(u => u.Id == id);
        if (user == null)
        {
            return false;
        }

        entities.Remove(user);
        await SaveToFileAsync(entities);
        UserChanged?.Invoke(this, (user, UserChangedAction.Removed));
        return true;
    }

    public async Task<bool> LockUser(string id)
    {
        _logger.LogInformation("Locking user {id}", id);
        if (string.IsNullOrEmpty(id))
            return false;
        
        var entities = await GetEntitiesFromFile();

        var user = entities.FirstOrDefault(u => u.Id == id);
        
        if (user is null)
            return false;
            
        if (entities.FirstOrDefault(e => e.IsLocked is true) != null)
            return false;
        
        user.IsLocked = true;

        // if something goes wrong, a task should unlock the user after 30 seconds
        _ = Task.Delay(45000).ContinueWith(async _ =>
        {
            var entitiesUpd = await GetEntitiesFromFile();
            var userUpd = entitiesUpd.FirstOrDefault(u => u.Id == id);

            if (userUpd?.IsLocked == true)
            {
                _logger.LogWarning("User was locked for too long, unlocking {id}", id);
                await UnlockUser(id);
            }
        });
        
        await SaveToFileAsync(entities);
        LockStatusChanged?.Invoke(this, true);
        UserChanged?.Invoke(this, (user, UserChangedAction.Updated));
        return true;
    }

    public async Task<bool> UnlockUser(string id)
    {
        _logger.LogInformation("Unlocking user {id}", id);
        
        var entities = await GetEntitiesFromFile();

        UserEntity user = entities.FirstOrDefault(u => u.Id == id);
        
        if (user is null)
            return false;
        
        user.IsLocked = false;

        await SaveToFileAsync(entities);
        LockStatusChanged?.Invoke(this, false);
        UserChanged?.Invoke(this, (user, UserChangedAction.Updated));
        return true;
    }

    public async Task<bool> IsLocked()
    {
        var entities = await GetEntitiesFromFile();

        return entities.FirstOrDefault(e => e.IsLocked is true) != null;
    }
    
    private async Task SaveToFileAsync(List<UserEntity> entities)
    {
        var json = JsonSerializer.Serialize(entities, new JsonSerializerOptions() { WriteIndented = true });
        var fullFilePath = Path.Combine("/app/data", _filePath);
        await File.WriteAllTextAsync(fullFilePath, json);
    }

    private async Task<List<UserEntity>> GetEntitiesFromFile()
    {
        var fullFilePath = Path.Combine("/app/data", _filePath);
        return JsonSerializer.Deserialize<List<UserEntity>>(await File.ReadAllBytesAsync(fullFilePath));
    }
}
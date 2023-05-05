using System.Text.Json;
using ChatGPT.SessionManager.API.Models;

namespace ChatGPT.SessionManager.API.Services;

public class SessionManagerService : ISessionManagerService
{
    private readonly string _filePath = "UserEntities.json";
    private List<UserEntity> entities = new();
    private readonly ILogger<SessionManagerService> _logger;

    public event EventHandler<(UserEntity, UserChangedAction)> UserChanged;
    public event EventHandler<bool> LockStatusChanged;

    public SessionManagerService(ILogger<SessionManagerService> logger)
    {
        _logger = logger;
        
        if (File.Exists(_filePath))
        {
            var json = File.ReadAllText(_filePath);
            entities = JsonSerializer.Deserialize<List<UserEntity>>(json);
        }
        else
        {
            entities = new List<UserEntity>();
        }
    }

    public Task<IEnumerable<UserEntity>> GetAllUsers()
    {
        _logger.LogInformation("Getting all users");
        return Task.FromResult(entities.AsEnumerable());
    }

    public Task<UserEntity> GetUserById(string id)
    {
        _logger.LogInformation("Getting user by id");
        return Task.FromResult(entities.FirstOrDefault(u => u.Id == id));
    }

    public Task<UserEntity> GetUserByName(string name)
    {
        _logger.LogInformation("Getting user {name}", name);
        return Task.FromResult(entities.FirstOrDefault(u => u.Name == name));
    }

    public async Task<UserEntity> AddUser(UserEntity newUser)
    {
        _logger.LogInformation("Adding user session {id}", newUser.Id);
        entities.Add(newUser);
        await SaveToFileAsync();
        UserChanged?.Invoke(this, (newUser, UserChangedAction.Added));
        return newUser;
    }

    public async Task<bool> UpdateUser(UserEntity updatedUser)
    {
        _logger.LogInformation("Updating user {id}", updatedUser.Id);
        var user = entities.FirstOrDefault(u => u.Id == updatedUser.Id);
        if (user == null)
        {
            return false;
        }

        user.Name = updatedUser.Name;
        await SaveToFileAsync();
        UserChanged?.Invoke(this, (updatedUser, UserChangedAction.Updated));
        return true;
    }

    public async Task<bool> DeleteUser(string id)
    {
        _logger.LogInformation("Deleting user {id}", id);
        var user = entities.FirstOrDefault(u => u.Id == id);
        if (user == null)
        {
            return false;
        }

        entities.Remove(user);
        await SaveToFileAsync();
        UserChanged?.Invoke(this, (user, UserChangedAction.Removed));
        return true;
    }

    public async Task<bool> LockUser(string id)
    {
        _logger.LogInformation("Locking user {id}", id);
        if (string.IsNullOrEmpty(id))
            return false;
        
        UserEntity? user = entities.FirstOrDefault(u => u.Id == id);
        
        if (user is null)
            return false;
            
        if (entities.FirstOrDefault(e => e.IsLocked is true) != null)
            return false;
        
        user.IsLocked = true;

        // if something goes wrong, a task should unlock the user after 30 seconds
        _ = Task.Delay(25000).ContinueWith(async _ =>
        {
            if (user.IsLocked == true)
            {
                _logger.LogWarning("User was locked for too long, unlocking {id}", id);
                await UnlockUser(id);
            }
        });
        await SaveToFileAsync();
        LockStatusChanged?.Invoke(this, true);
        UserChanged?.Invoke(this, (user, UserChangedAction.Updated));
        return true;
    }

    public async Task<bool> UnlockUser(string id)
    {
        _logger.LogInformation("Unlocking user {id}", id);
        UserEntity? user = entities.FirstOrDefault(u => u.Id == id);
        
        if (user is null)
            return false;
        
        user.IsLocked = false;

        await SaveToFileAsync();
        LockStatusChanged?.Invoke(this, false);
        UserChanged?.Invoke(this, (user, UserChangedAction.Updated));
        return true;
    }

    public Task<bool> IsLocked()
    {
        return Task.FromResult(entities.FirstOrDefault(e => e.IsLocked is true) != null);
    }
    
    private async Task SaveToFileAsync()
    {
        var json = JsonSerializer.Serialize(entities, new JsonSerializerOptions() { WriteIndented = true });
        await File.WriteAllTextAsync(_filePath, json);
    }
}
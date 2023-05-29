using System.Text.Json;
using ChatGPT.SessionManager.API.Models;

namespace ChatGPT.SessionManager.API.Services;

public class SessionManagerService : ISessionManagerService
{
    private readonly string _directoryPath = "/app/data/users";
    private readonly ILogger<SessionManagerService> _logger;

    public event EventHandler<(UserEntity, UserChangedAction)> UserChanged;
    public event EventHandler<bool> LockStatusChanged;

    public SessionManagerService(ILogger<SessionManagerService> logger)
    {
        _logger = logger;

        if (!Directory.Exists(_directoryPath))
        {
            Directory.CreateDirectory(_directoryPath);
        }
    }

    public async Task<IEnumerable<UserEntity>> GetAllUsers()
    {
        _logger.LogInformation("Getting all users");
        var users = new List<UserEntity>();
        foreach (var file in Directory.EnumerateFiles(_directoryPath, "*.json"))
        {
            var user = JsonSerializer.Deserialize<UserEntity>(await File.ReadAllBytesAsync(file));
            if (user != null)
            {
                users.Add(user);
            }
        }
        
        return users;
    }

    public async Task<UserEntity> GetUserById(string id)
    {
        var filePath = GetFilePathForId(id);

        if (!File.Exists(filePath))
        {
            return null;
        }

        return JsonSerializer.Deserialize<UserEntity>(await File.ReadAllBytesAsync(filePath));
    }

    public async Task<UserEntity> AddUser(UserEntity newUser)
    {
        var filePath = GetFilePathForId(newUser.Id);

        if (File.Exists(filePath))
        {
            throw new Exception($"User with id {newUser.Id} already exists.");
        }

        newUser.UserCreatedDate = DateTime.UtcNow;

        await SaveToFileAsync(filePath, newUser);

        return newUser;
    }
    
    public async Task<bool> UpdateUser(UserEntity updatedUser)
    {
        _logger.LogInformation("Updating user {id}", updatedUser.Id);

        var filePath = GetFilePathForId(updatedUser.Id);

        if (!File.Exists(filePath))
        {
            return false;
        }

        updatedUser.UserCreatedDate = (await GetUserById(updatedUser.Id)).UserCreatedDate; // retain original created date

        await SaveToFileAsync(filePath, updatedUser);

        UserChanged?.Invoke(this, (updatedUser, UserChangedAction.Updated));

        return true;
    }
    
    public async Task<bool> DeleteUser(string id)
    {
        _logger.LogInformation("Deleting user {id}", id);

        var filePath = GetFilePathForId(id);

        if (!File.Exists(filePath))
        {
            return false;
        }

        File.Delete(filePath);

        UserChanged?.Invoke(this, (await GetUserById(id), UserChangedAction.Removed)); // We might want to get user before deleting for the event

        return true;
    }
    
    public async Task<bool> LockUser(string id)
    {
        _logger.LogInformation("Locking user {id}", id);

        var filePath = GetFilePathForId(id);

        if (!File.Exists(filePath))
        {
            return false;
        }
        
        if (IsAnyUserLocked())
        {
            return false;
        }
        
        var lockedFilePath = GetLockedFilePathForId(id);
        var user = await GetUserById(id);
        user.IsLocked = true;

        await SaveToFileAsync(filePath, user);
        File.Move(filePath, lockedFilePath);
        
        UserChanged?.Invoke(this, (user, UserChangedAction.Updated));
        
        // if something goes wrong, a task should unlock the user after 30 seconds
        _ = Task.Delay(45000).ContinueWith(async _ =>
        {
            var lockedFilePathUpd = GetLockedFilePathForId(id);

            if (File.Exists(lockedFilePathUpd))
            {
                _logger.LogWarning("User was locked for too long, unlocking {id}", id);
                await UnlockUser(id);
            }
        });
        
        return true;
    }
    
    public async Task<bool> UnlockUser(string id)
    {
        _logger.LogInformation("Unlocking user {id}", id);

        var lockedFilePath = GetLockedFilePathForId(id);
        if (!File.Exists(lockedFilePath))
        {
            return false;
        }
        
        var filePath = GetFilePathForId(id);
        
        File.Move(lockedFilePath, filePath);
        
        var user = await GetUserFromFile(filePath);
        user.IsLocked = false;
        await SaveToFileAsync(filePath, user);

        UserChanged?.Invoke(this, (user, UserChangedAction.Updated));
        LockStatusChanged?.Invoke(this, false);

        return true;
    }

    public Task<bool> IsLocked()
    {
        return Task.FromResult(IsAnyUserLocked());
    }
    
    private async Task<UserEntity> GetUserFromFile(string filePath)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<UserEntity>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error reading user file: {filePath}");
            return null;
        }
    }

    private async Task SaveToFileAsync(string filePath, UserEntity user)
    {
        var json = JsonSerializer.Serialize(user, new JsonSerializerOptions { WriteIndented = true });

        await File.WriteAllTextAsync(filePath, json);
    }
    
    private bool IsAnyUserLocked() => Directory.EnumerateFiles(_directoryPath, "*_locked.json").Any();

    private string GetFilePathForId(string id)
    {
        return Path.Combine(_directoryPath, $"{id}.json");
    }
    
    private string GetLockedFilePathForId(string id)
    {
        return Path.Combine(_directoryPath, $"{id}_locked.json");
    }
}
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

        newUser.LastInteractionDate = DateTime.UtcNow;

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

        updatedUser.LastInteractionDate = DateTime.UtcNow;

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
        user.LockDate = DateTime.UtcNow;
        user.LastInteractionDate = DateTime.UtcNow;

        await SaveToFileAsync(filePath, user);
        File.Move(filePath, lockedFilePath);
        
        UserChanged?.Invoke(this, (user, UserChangedAction.Updated));
        LockStatusChanged?.Invoke(this, true);

        // if something goes wrong, a task should unlock the user after 30 seconds
        _ = Task.Delay(45_000).ContinueWith(async _ =>
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
        user.LastInteractionDate = DateTime.UtcNow;
        user.LockDate = null;
        
        await SaveToFileAsync(filePath, user);

        UserChanged?.Invoke(this, (user, UserChangedAction.Updated));
        LockStatusChanged?.Invoke(this, false);

        return true;
    }

    public Task<bool> IsLocked()
    {
        return Task.FromResult(IsAnyUserLocked());
    }

    public async Task Cleanup()
    {
        _logger.LogInformation("Cleaning up users");
        // Cleanup users which have a LastInteractionDate older than 12 hours
        var files = Directory.EnumerateFiles(_directoryPath, "*.json").ToList();

        var usersDeleted = 0;
        
        foreach (var file in files)
        {
            var user = await GetUserFromFile(file);
            if (user.LastInteractionDate.AddHours(12) > DateTime.UtcNow)
                continue;
            
            UserChanged?.Invoke(this, (user, UserChangedAction.Removed));
            File.Delete(file);
            usersDeleted++;
        }
        
        _logger.LogInformation("Cleaned up {count} users", usersDeleted);
        
        // Cleanup locked users which are older than 45 seconds
        await BlockingCleanup();
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
            _logger.LogError(ex, $"Error reading  user file: {filePath}");
            return null;
        }
    }

    private async Task SaveToFileAsync(string filePath, UserEntity user)
    {
        var json = JsonSerializer.Serialize(user, new JsonSerializerOptions { WriteIndented = true });

        await File.WriteAllTextAsync(filePath, json);
    }
    
    public async Task SaveLastUserInteractionDateAndCheckBlocking(string id)
    {
        var filePath = GetFilePathForId(id);

        if (!File.Exists(filePath))
        {
            return;
        }

        var user = await GetUserById(id);
        user.LastInteractionDate = DateTime.UtcNow;
        
        await SaveToFileAsync(filePath, user);
        await BlockingCleanup();
    }
    
    private bool IsAnyUserLocked() => Directory.EnumerateFiles(_directoryPath, "*_locked.json").Any();

    // Check if any user is locked and if it is blocking longer than 45 seconds => remove lock
    private async Task BlockingCleanup()
    {
        if (!IsAnyUserLocked())
            return;

        var lockedFilePaths = Directory.EnumerateFiles(_directoryPath, "*_locked.json");

        foreach (var lockedFilePath in lockedFilePaths)
        {
            var user = await GetUserFromFile(lockedFilePath);
        
            if (user.LockDate?.AddSeconds(45) > DateTime.UtcNow)
                return;
        
            _logger.LogWarning("User was locked for too long, unlocking {id}", user.Id);
            await UnlockUser(user.Id);
        }
    }
    
    private string GetFilePathForId(string id)
    {
        return Path.Combine(_directoryPath, $"{id}.json");
    }
    
    private string GetLockedFilePathForId(string id)
    {
        return Path.Combine(_directoryPath, $"{id}_locked.json");
    }
}
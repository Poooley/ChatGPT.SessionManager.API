using System.Text.Json;
using ChatGPT.SessionManager.API.Models;

namespace ChatGPT.SessionManager.API.Services;

public class SessionManagerService : ISessionManagerService
{
    private readonly string _filePath = "UserEntitys.json";
    private List<UserEntity> entities = new();

    public SessionManagerService()
    {
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
        return Task.FromResult(entities.AsEnumerable());
    }

    public Task<UserEntity> GetUserById(string id)
    {
        return Task.FromResult(entities.FirstOrDefault(u => u.Id == id));
    }

    public Task<UserEntity> GetUserByName(string name)
    {
        return Task.FromResult(entities.FirstOrDefault(u => u.Name == name));
    }

    public async Task<UserEntity> AddUser(UserEntity newUser)
    {
        entities.Add(newUser);
        await SaveToFileAsync();
        return newUser;
    }

    public async Task<bool> UpdateUser(UserEntity updatedUser)
    {
        var user = entities.FirstOrDefault(u => u.Id == updatedUser.Id);
        if (user == null)
        {
            return false;
        }

        user.Name = updatedUser.Name;
        await SaveToFileAsync();
        return true;
    }

    public async Task<bool> DeleteUser(string id)
    {
        var user = entities.FirstOrDefault(u => u.Id == id);
        if (user == null)
        {
            return false;
        }

        entities.Remove(user);
        await SaveToFileAsync();
        return true;
    }

    public async Task<bool> LockUser(string id)
    {
        if (string.IsNullOrEmpty(id))
            return false;
        
        UserEntity? user = entities.FirstOrDefault(u => u.Id == id);
        
        if (user is null)
            return false;
            
        if (entities.FirstOrDefault(e => e.IsLocked == true) is not null)
            return false;
        
        user.IsLocked = true;
        
        await SaveToFileAsync();
        return true;
    }

    public async Task<bool> UnlockUser(string id)
    {
        UserEntity? user = entities.FirstOrDefault(u => u.Id == id);
        
        if (user is null)
            return false;
        
        user.IsLocked = false;

        await SaveToFileAsync();
        return true;
    }

    private async Task SaveToFileAsync()
    {
        var json = JsonSerializer.Serialize(entities, new JsonSerializerOptions() { WriteIndented = true });
        await File.WriteAllTextAsync(_filePath, json);
    }
}
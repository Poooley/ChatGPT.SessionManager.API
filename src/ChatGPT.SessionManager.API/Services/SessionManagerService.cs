using System.Text.Json;
using ChatGPT.SessionManager.API.Models;

namespace ChatGPT.SessionManager.API.Services;

public class SessionManagerService : ISessionManagerService
{
    private readonly string _filePath = "UserRegistrations.json";
    private List<UserRegistration> _userRegistrations = new();

    public SessionManagerService()
    {
        if (File.Exists(_filePath))
        {
            var json = File.ReadAllText(_filePath);
            _userRegistrations = JsonSerializer.Deserialize<List<UserRegistration>>(json);
        }
        else
        {
            _userRegistrations = new List<UserRegistration>();
        }
    }

    public Task<IEnumerable<UserRegistration>> GetAllUsers()
    {
        return Task.FromResult(_userRegistrations.AsEnumerable());
    }

    public Task<UserRegistration> GetUserById(int id)
    {
        return Task.FromResult(_userRegistrations.FirstOrDefault(u => u.Id == id));
    }

    public Task<UserRegistration> GetUserByName(string name)
    {
        return Task.FromResult(_userRegistrations.FirstOrDefault(u => u.Name == name));
    }

    public async Task<UserRegistration> AddUser(UserRegistration newUser)
    {
        _userRegistrations.Add(newUser);
        await SaveToFileAsync();
        return newUser;
    }

    public async Task<bool> UpdateUser(UserRegistration updatedUser)
    {
        var user = _userRegistrations.FirstOrDefault(u => u.Id == updatedUser.Id);
        if (user == null)
        {
            return false;
        }

        user.Name = updatedUser.Name;
        await SaveToFileAsync();
        return true;
    }

    public async Task<bool> DeleteUser(int id)
    {
        var user = _userRegistrations.FirstOrDefault(u => u.Id == id);
        if (user == null)
        {
            return false;
        }

        _userRegistrations.Remove(user);
        await SaveToFileAsync();
        return true;
    }

    private async Task SaveToFileAsync()
    {
        var json = JsonSerializer.Serialize(_userRegistrations, new JsonSerializerOptions() { WriteIndented = true });
        await File.WriteAllTextAsync(_filePath, json);
    }
}
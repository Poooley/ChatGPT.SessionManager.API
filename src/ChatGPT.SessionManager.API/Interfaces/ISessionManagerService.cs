using ChatGPT.SessionManager.API.Models;

namespace ChatGPT.SessionManager.API;

public interface ISessionManagerService
{
    Task<IEnumerable<UserRegistration>> GetAllUsers();
    Task<UserRegistration> GetUserById(int id);
    Task<UserRegistration> GetUserByName(string name);
    Task<UserRegistration> AddUser(UserRegistration newUser);
    Task<bool> UpdateUser(UserRegistration updatedUser);
    Task<bool> DeleteUser(int id);
}
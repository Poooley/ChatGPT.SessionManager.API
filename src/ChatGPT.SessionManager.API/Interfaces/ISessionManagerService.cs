using ChatGPT.SessionManager.API.Models;

namespace ChatGPT.SessionManager.API;

public interface ISessionManagerService
{
    Task<IEnumerable<UserEntities>> GetAllUsers();
    Task<UserEntities> GetUserById(string id);
    Task<UserEntities> GetUserByName(string name);
    Task<UserEntities> AddUser(UserEntities newUser);
    Task<bool> UpdateUser(UserEntities updatedUser);
    Task<bool> DeleteUser(string id);
    Task<bool> LockUser(string id);
    Task<bool> UnlockUser(string id);
}
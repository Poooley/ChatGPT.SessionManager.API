using ChatGPT.SessionManager.API.Models;

namespace ChatGPT.SessionManager.API;

public interface ISessionManagerService
{
    public event EventHandler<(UserEntity, UserChangedAction)> UserChanged;
    public event EventHandler<bool> LockStatusChanged;
    
    Task<IEnumerable<UserEntity>> GetAllUsers();
    Task<UserEntity> GetUserById(string id);
    Task<UserEntity> GetUserByName(string name);
    Task<UserEntity> AddUser(UserEntity newUser);
    Task<bool> UpdateUser(UserEntity updatedUser);
    Task<bool> DeleteUser(string id);
    Task<bool> LockUser(string id);
    Task<bool> UnlockUser(string id);
    Task<bool> IsLocked();
}
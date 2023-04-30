namespace ChatGPT.SessionManager.API.Models;

public class UserEntity
{
    public string Id { get; set; }
    public string Name { get; set; }
    public bool? IsLocked { get; set; } = null;
}
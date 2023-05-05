using ChatGPT.SessionManager.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChatGPT.SessionManager.API.Controllers;

[Authorize]
[ApiController]
[Route("api/session-manager")]
public class SessionManagerController : ControllerBase
{
    private readonly ILogger<SessionManagerController> _logger;
    private readonly ISessionManagerService _sessionManagerService;

    public SessionManagerController(ILogger<SessionManagerController> logger, ISessionManagerService sessionManagerService)
    {
        _logger = logger;
        _sessionManagerService = sessionManagerService;
    }

    [HttpPost("users")]
    public async Task<IActionResult> AddUser([FromBody] UserEntity newUser)
    {
        var createdUser = await _sessionManagerService.AddUser(newUser);
        return CreatedAtAction(nameof(AddUser), new { id = createdUser.Id }, createdUser);
    }

    [HttpGet("users")]
    public async Task<IEnumerable<UserEntity>> GetAllUsers()
    {
        return await _sessionManagerService.GetAllUsers();
    }

    [HttpGet("users/{id:int}")]
    public async Task<IActionResult> GetUserById(string id)
    {
        var user = await _sessionManagerService.GetUserById(id);
        if (user == null)
        {
            return NotFound();
        }
        return Ok(user);
    }

    [HttpGet("users/{name}")]
    public async Task<IActionResult> GetUserByName(string name)
    {
        var user = await _sessionManagerService.GetUserByName(name);
        if (user == null)
        {
            return NotFound();
        }
        return Ok(user);
    }

    [HttpPut("users/")]
    public async Task<IActionResult> UpdateUser([FromBody] UserEntity updatedUser)
    {
        var result = await _sessionManagerService.UpdateUser(updatedUser);
        if (!result)
        {
            return NotFound();
        }
        return NoContent();
    }

    [HttpDelete("users/{id}")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var result = await _sessionManagerService.DeleteUser(id);
        if (!result)
        {
            return NotFound();
        }
        return NoContent();
    }
    
    [HttpPut("users/{session}/lock")]
    public async Task<IActionResult> LockUser(string session)
    {
        bool result = await _sessionManagerService.LockUser(session);
        if (!result)
        {
            return Conflict("Already locked or user not found");
        }
        
        return NoContent();
    }
    
    [HttpPut("users/{session}/unlock")]
    public async Task<IActionResult> UnlockUser(string session)
    {
        bool result = await _sessionManagerService.UnlockUser(session);
        if (!result)
        {
            return Conflict("User not found");
        }
        
        return NoContent();
    }
    
    [HttpGet("users/isLocked")]
    public async Task<IActionResult> IsLocked()
    {
        var result = await _sessionManagerService.IsLocked();
        
        return Ok(result);
    }
}
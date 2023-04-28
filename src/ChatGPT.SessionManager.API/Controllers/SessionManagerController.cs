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

    public SessionManagerController(ILogger<SessionManagerController> logger)
    {
        _logger = logger;
    }

    [HttpGet(Name = "users")]
    public IEnumerable<UserRegistration> Get()
    {
        return Enumerable.Repeat(1, 10).Select((x) => new UserRegistration()  { Name = x.ToString() });
    }
}

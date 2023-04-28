using Microsoft.AspNetCore.Authentication;

namespace ChatGPT.SessionManager.API.Controllers;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "APIKey";
}
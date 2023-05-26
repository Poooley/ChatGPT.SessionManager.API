using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ChatGPT.SessionManager.API.Controllers;

public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private const string ApiKeyHeaderName = "X-Api-Key";
    private readonly IConfiguration _config;
    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration config,
        ISystemClock clock) : base(options, logger, encoder, clock)
    {
        _config = config;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKeyHeaderValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var apiKey = apiKeyHeaderValues.ToString();

        // Validate the API key here. You should replace this with your actual validation logic.
        if (!IsValidApiKey(apiKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }

        // If the API key is valid, create a ClaimsIdEntities for the user.
        var idEntities = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, apiKey) }, ApiKeyAuthenticationOptions.DefaultScheme);
        var principal = new ClaimsPrincipal(idEntities);
        var ticket = new AuthenticationTicket(principal, ApiKeyAuthenticationOptions.DefaultScheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private bool IsValidApiKey(string apiKey)
    {
        return apiKey == _config["SESSION_MANAGER_SECRET"];
    }
}
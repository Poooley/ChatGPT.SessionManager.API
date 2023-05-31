using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using ChatGPT.SessionManager.API.Models;
using Microsoft.Extensions.Caching.Memory;

namespace ChatGPT.SessionManager.API.Services;

public class WebSocketMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ISessionManagerService _sessionManagerService;
    private readonly ILogger<WebSocketMiddleware> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private static readonly ConcurrentDictionary<Guid, WebSocket> _sockets = new();
    private readonly IMemoryCache _cache;

    public WebSocketMiddleware(RequestDelegate next, 
        ISessionManagerService sessionManagerService,
        ILogger<WebSocketMiddleware> logger,
        IMemoryCache cache)
    {
        _next = next;
        _sessionManagerService = sessionManagerService;
        _logger = logger;
        _cache = cache;
        _jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = null };
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.WebSockets.IsWebSocketRequest)
        {
            _logger.LogInformation("WebSocket connection established");
            if (!IsValidToken(context.Request.Query))
            {
                _logger.LogWarning("Invalid token");
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
            
            _logger.LogInformation("Token is valid");
            using WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
            await ProcessWebSocketRequests(context, webSocket);
        }
        else
        {
            _logger.LogDebug("Request is not a WebSocket request");
            await _next(context);
        }
    }

    private async Task ProcessWebSocketRequests(HttpContext context, WebSocket webSocket)
    {
        var socketId = Guid.NewGuid();
        _sockets.TryAdd(socketId, webSocket);
        
        _logger.LogDebug("WebSocket connection added to dictionary");
        // Subscribe to events in the SessionManagerService
        _sessionManagerService.UserChanged += SendUserChangedNotification;
        _sessionManagerService.LockStatusChanged += SendLockStatusChangedNotification;

        WebSocketReceiveResult result;
        do
        {
            var buffer = new ArraySegment<byte>(new byte[1024]);
            result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);

            // You can process incoming WebSocket messages here if needed

        } while (!result.CloseStatus.HasValue);
        
        // Clean up when the WebSocket is closed
        _logger.LogDebug("WebSocket connection closed");
        _sockets.TryRemove(socketId, out _);
        _sessionManagerService.UserChanged -= SendUserChangedNotification;
        _sessionManagerService.LockStatusChanged -= SendLockStatusChangedNotification;
        await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
    }
    
    private bool IsValidToken(IQueryCollection query)
    {
        if (!query.TryGetValue("token", out var tokenValues))
        {
            return false;
        }

        var token = tokenValues.ToString();
        if (_cache.TryGetValue(token, out _))
        {
            _cache.Remove(token);
            return true;
        }

        return false;
    }
    
    private async void SendUserChangedNotification(object sender, (UserEntity user, UserChangedAction action) e)
    {
        var message = JsonSerializer.Serialize(new { type = "userChanged", e.user, e.action}, _jsonOptions);
        await SendWebSocketMessageAsync(message);
    }
    
    private async void SendLockStatusChangedNotification(object sender, bool lockStatus)
    {
        var message = JsonSerializer.Serialize(new { type = "lockStatusChanged", lockStatus }, _jsonOptions);
        await SendWebSocketMessageAsync(message);
    }
    
    private async Task SendWebSocketMessageAsync(string message)
    {
        var messageBytes = Encoding.UTF8.GetBytes(message);
        var messageSegment = new ArraySegment<byte>(messageBytes);

        foreach (var socket in _sockets.Values)
        {
            if (socket.State == WebSocketState.Open)
            {
                await socket.SendAsync(messageSegment, WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
    }
}
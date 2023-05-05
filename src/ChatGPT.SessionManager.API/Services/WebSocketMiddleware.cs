using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using ChatGPT.SessionManager.API.Models;

namespace ChatGPT.SessionManager.API.Services;

public class WebSocketMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ISessionManagerService _sessionManagerService;
    private readonly JsonSerializerOptions _jsonOptions;
    private static readonly ConcurrentDictionary<Guid, WebSocket> _sockets = new();

    public WebSocketMiddleware(RequestDelegate next, ISessionManagerService sessionManagerService)
    {
        _next = next;
        _sessionManagerService = sessionManagerService;
        _jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = null };
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api/session-manager/ws"))
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                using WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                await ProcessWebSocketRequests(context, webSocket);
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }
        else
        {
            await _next(context);
        }
    }

    private async Task ProcessWebSocketRequests(HttpContext context, WebSocket webSocket)
    {
        var socketId = Guid.NewGuid();
        _sockets.TryAdd(socketId, webSocket);

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
        _sockets.TryRemove(socketId, out _);
        _sessionManagerService.UserChanged -= SendUserChangedNotification;
        _sessionManagerService.LockStatusChanged -= SendLockStatusChangedNotification;
        await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
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
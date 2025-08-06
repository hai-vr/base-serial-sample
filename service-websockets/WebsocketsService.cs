using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using Hai.PositionSystemToExternalProgram.Core;

namespace Hai.PositionSystemToExternalProgram.Services.Websockets;

public class WebsocketsService
{
    private readonly IWebsocketActions _websocketActions;
    private readonly ConcurrentDictionary<string, WebSocket> _connections = new();
    
    private WebsocketsStarter _callback;

    public void DefineCallback(WebsocketsStarter starter)
    {
        _callback = starter;
    }

    public async Task HandleWebSocketAsync(WebSocket webSocket, string connectionId)
    {
        _connections.TryAdd(connectionId, webSocket);

        try
        {
            var buffer = new byte[4096];
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer), 
                    CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    
                    // Process the received string message
                    await ProcessMessageAsync(connectionId, message);
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Connection closed by client",
                        CancellationToken.None);
                    break;
                }
            }
        }
        catch (WebSocketException _)
        {
            // Ignore
        }
        finally
        {
            _connections.TryRemove(connectionId, out _);
        }
    }

    private async Task ProcessMessageAsync(string connectionId, string message)
    {
        if (!message.StartsWith("PositionSystemInterpreted ")) return;
        var split = message.Split(" ");
        
        if (split.Length is not (7 or 10)) return;
        if (!float.TryParse(split[1], out var positionX)) return;
        if (!float.TryParse(split[2], out var positionY)) return;
        if (!float.TryParse(split[3], out var positionZ)) return;
        if (!float.TryParse(split[4], out var normalX)) return;
        if (!float.TryParse(split[5], out var normalY)) return;
        if (!float.TryParse(split[6], out var normalZ)) return;
        if (split.Length == 7)
        {
            _callback.Submit(positionX, positionY, positionZ, normalX, normalY, normalZ);
        }
        else // == 10
        {
            if (!float.TryParse(split[4], out var tangentX)) return;
            if (!float.TryParse(split[5], out var tangentY)) return;
            if (!float.TryParse(split[6], out var tangentZ)) return;
            _callback.Submit(positionX, positionY, positionZ, normalX, normalY, normalZ, tangentX, tangentY, tangentZ);
        }
        
        await Task.CompletedTask;
    }
}

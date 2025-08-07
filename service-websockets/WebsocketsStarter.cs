using Hai.PositionSystemToExternalProgram.Core;
using System.Net;

namespace Hai.PositionSystemToExternalProgram.Services.Websockets;

public class WebsocketsStarter
{
    private readonly IWebsocketActions _websocketActions;
    private readonly WebsocketsService _websocketService;

    private bool _started;
    private HttpListener _httpListener;
    private CancellationTokenSource _cancellationTokenSource;

    public WebsocketsStarter(IWebsocketActions websocketActions)
    {
        _websocketActions = websocketActions;
            
        _websocketService = new WebsocketsService();
        _websocketService.DefineCallback(this);
    }

    public void Start(ushort port)
    {
        if (_started) return;
        _started = true;
        
        Console.WriteLine("Trying to start websockets...");
        
        _httpListener = new HttpListener();
        _httpListener.Prefixes.Add($"http://localhost:{port}/");
        _cancellationTokenSource = new CancellationTokenSource();
        
        _httpListener.Start();
        
        _ = Task.Run(async () => await ListenForRequests(_cancellationTokenSource.Token));
        
        Console.WriteLine("Started websockets.");
    }

    private async Task ListenForRequests(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _httpListener.IsListening)
        {
            try
            {
                var context = await _httpListener.GetContextAsync();
                
                if (context.Request.Url.AbsolutePath == "/ws" && context.Request.IsWebSocketRequest)
                {
                    _ = Task.Run(async () => await HandleWebSocketRequest(context));
                }
                else
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                }
            }
            catch (Exception ex) when (cancellationToken.IsCancellationRequested)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling request: {ex.Message}");
            }
        }
    }

    private async Task HandleWebSocketRequest(HttpListenerContext context)
    {
        try
        {
            var webSocketContext = await context.AcceptWebSocketAsync(null);
            var webSocket = webSocketContext.WebSocket;
            var connectionId = Guid.NewGuid().ToString();

            await _websocketService.HandleWebSocketAsync(webSocket, connectionId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling WebSocket: {ex.Message}");
            context.Response.StatusCode = 500;
            context.Response.Close();
        }
    }

    public void Stop()
    {
        if (!_started) return;
        _started = false;

        Console.WriteLine("Trying to stop websockets...");
        
        _cancellationTokenSource?.Cancel();
        _httpListener?.Stop();
        _httpListener?.Close();
        
        Console.WriteLine("Stopped websockets.");
    }

    public void Submit(float positionX, float positionY, float positionZ, float normalX, float normalY, float normalZ)
    {
        _websocketActions.Submit(positionX, positionY, positionZ, normalX, normalY, normalZ);
    }

    public void Submit(float positionX, float positionY, float positionZ, float normalX, float normalY, float normalZ, float tangentX, float tangentY, float tangentZ)
    {
        _websocketActions.Submit(positionX, positionY, positionZ, normalX, normalY, normalZ, tangentX, tangentY, tangentZ);
    }
}
using Hai.PositionSystemToExternalProgram.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Hai.PositionSystemToExternalProgram.Services.Websockets;

public class WebsocketsStarter
{
    private readonly IWebsocketActions _websocketActions;
    
    private bool _started;
    private WebApplication _app;

    public WebsocketsStarter(IWebsocketActions websocketActions)
    {
        _websocketActions = websocketActions;
    }

    public void Start(ushort port)
    {
        if (_started) return;
        _started = true;
        
        Console.WriteLine("Trying to start websockets...");
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<WebsocketsService>();

        _app = builder.Build();
        _app.UseWebSockets();

        _app.Urls.Add($"http://localhost:{port}");

        _app.Map("/ws", async context =>
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                var connectionId = Guid.NewGuid().ToString();
                var websocketService = context.RequestServices.GetRequiredService<WebsocketsService>();
                websocketService.DefineCallback(this);

                await websocketService.HandleWebSocketAsync(webSocket, connectionId);
            }
            else
            {
                context.Response.StatusCode = 400;
            }
        });

        _app.RunAsync();
        Console.WriteLine("Started websockets.");
    }

    public void Stop()
    {
        if (!_started) return;
        _started = false;

        Console.WriteLine("Trying to stop websockets...");
        _app.StopAsync().Wait();
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
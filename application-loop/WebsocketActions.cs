using Hai.PositionSystemToExternalProgram.ApplicationLoop;
using Hai.PositionSystemToExternalProgram.Core;

namespace Hai.PositionSystemToExternalProgram.Program;

public class WebsocketActions : IWebsocketActions
{
    private readonly Routine _routine;

    public WebsocketActions(Routine routine)
    {
        _routine = routine;
    }

    public void Submit(float positionX, float positionY, float positionZ, float normalX, float normalY, float normalZ)
    {
        _routine.Enqueue(() =>
        {
            _routine.ReceiveDirectControl(positionX, positionY, positionZ, normalX, normalY, normalZ);
        });
    }

    public void Submit(float positionX, float positionY, float positionZ, float normalX, float normalY, float normalZ, float tangentX, float tangentY, float tangentZ)
    {
        _routine.Enqueue(() =>
        {
            _routine.ReceiveDirectControl(positionX, positionY, positionZ, normalX, normalY, normalZ, tangentX, tangentY, tangentZ);
        });
    }
}
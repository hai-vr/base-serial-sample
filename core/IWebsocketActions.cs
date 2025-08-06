namespace Hai.PositionSystemToExternalProgram.Core;

public interface IWebsocketActions
{
    public const ushort WebsocketDefaultPort = 56247;
    
    void Submit(float positionX, float positionY, float positionZ, float normalX, float normalY, float normalZ);
    void Submit(float positionX, float positionY, float positionZ, float normalX, float normalY, float normalZ, float tangentX, float tangentY, float tangentZ);
}
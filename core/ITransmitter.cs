namespace Hai.PositionSystemToExternalProgram.Core;

public interface ITransmitter
{
    void ProvideNewTarget(RoboticsCoordinates roboticsCoordinates);
    void Update(float deltaTimeMs);
    
    bool IsOpen();
    void Open();
    void Close();
}
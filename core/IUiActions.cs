namespace Hai.PositionSystemToExternalProgram.Core;

public interface IUiActions
{
    void ConnectSerial(string portName);
    void DisconnectSerial();
    bool IsSerialOpen();
    TcodeData ExposeRawData();
    bool IsOpenVrRunning();
    bool IsUsingVrExtractor();
    ExtractionResult ExtractedData();
    bool[] Bits();
    ExtractionCoordinates VrCoordinates();
    ExtractionCoordinates WindowCoordinates();
    DecodedData Data();
    InterpretedLightData InterpretedData();
    float VirtualScale();
    string[] FetchPortNames();
    void ConfigCoordinatesUpdated();
    void ConfigRoboticsUpdated();
    void ConfigWebsocketsUpdated();
}
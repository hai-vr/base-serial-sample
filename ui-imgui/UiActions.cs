using Hai.PositionSystemToExternalProgram.ApplicationLoop;
using Hai.PositionSystemToExternalProgram.Core;

namespace Hai.PositionSystemToExternalProgram.ImGuiProgram;

public class UiActions
{
    private readonly Routine _routine;
    private bool _configCoordinatesUpdated;
    private bool _configRoboticsUpdated;
    private bool _configWebsocketsUpdated;

    public UiActions(Routine routine)
    {
        _routine = routine;
    }

    public void ConnectSerial(string portName)
    {
        _routine.Enqueue(() => _routine.TryConnectSerial(portName));
    }

    public void DisconnectSerial()
    {
        _routine.Enqueue(() => _routine.TryDisconnectSerial());
    }

    public bool IsSerialOpen()
    {
        return _routine.IsSerialOpen();
    }

    public TcodeData ExposeRawData() => _routine.RawSerialData;
    public bool IsOpenVrRunning() => _routine.IsOpenVrRunning;
    public bool IsUsingVrExtractor() => _routine.IsUsingVrExtractor();
    public ExtractionResult ExtractedData() => _routine.ExtractedData;
    public bool[] Bits() => _routine.Bits;
    public ExtractionCoordinates VrCoordinates() => _routine.VrCoordinates;
    public ExtractionCoordinates WindowCoordinates() => _routine.WindowCoordinates;
    public DecodedData Data() => _routine.Data;
    public InterpretedLightData InterpretedData() => _routine.InterpretedData;
    public float VirtualScale() => _routine.VirtualScale;

    public string[] FetchPortNames()
    {
        return _routine.FetchPortNames();
    }

    public void Submit()
    {
        _routine.Enqueue(() => _routine.Submit());
    }

    public void ConfigCoordinatesUpdated()
    {
        _configCoordinatesUpdated = true;
        _routine.Enqueue(() =>
        {
            if (!_configCoordinatesUpdated) return;

            _routine.RefreshExtractionConfiguration();
            _configCoordinatesUpdated = false;
        });
    }

    public void ConfigRoboticsUpdated()
    {
        _configRoboticsUpdated = true;
        _routine.Enqueue(() =>
        {
            if (!_configRoboticsUpdated) return;

            _routine.RefreshRoboticsConfiguration();
            _configRoboticsUpdated = false;
        });
    }

    public void ConfigWebsocketsUpdated()
    {
        _configWebsocketsUpdated = true;
        _routine.Enqueue(() =>
        {
            if (!_configWebsocketsUpdated) return;

            _routine.RefreshWebsocketsConfiguration();
            _configWebsocketsUpdated = false;
        });
    }
}
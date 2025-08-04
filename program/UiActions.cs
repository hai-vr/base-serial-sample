using Hai.PositionSystemToExternalProgram.Core;

namespace Hai.PositionSystemToExternalProgram.Program;

public class UiActions
{
    private readonly Routine _routine;
    private bool _configCoordinatesUpdated;

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
    public ExtractionResult ExtractedData() => _routine.ExtractedData;
    public bool[] Bits() => _routine.Bits;
    public ExtractionCoordinates VrCoordinates() => _routine.VrCoordinates;
    public ExtractionCoordinates DesktopCoordinates() => _routine.DesktopCoordinates;
    public DecodedData Data() => _routine.Data;

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

            _routine.RefreshConfiguration();
        });
    }
}
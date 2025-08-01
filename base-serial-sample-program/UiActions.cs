namespace Hai.BaseSerial.SampleProgram;

public class UiActions
{
    private readonly Routine _routine;

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

    public string[] FetchPortNames()
    {
        return _routine.FetchPortNames();
    }

    public void Submit()
    {
        _routine.Enqueue(() => _routine.Submit());
    }
}
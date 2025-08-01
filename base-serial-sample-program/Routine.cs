using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using Hai.PositionSystemToExternalProgram.ExampleApp.Serial;
using Hai.PositionSystemToExternalProgram.Extractor.OVR;

namespace Hai.BaseSerial.SampleProgram;

public class Routine
{
    private readonly TcodeSerial _serial;
    private readonly OpenVrStarter _ovrStarter;

    public bool IsOpenVrRunning { get; private set; }
    public TcodeData RawSerialData { get; }

    private readonly ConcurrentQueue<Action> _queuedForMain = new ConcurrentQueue<Action>();
    private readonly Stopwatch _stopwatch;
    
    private bool _exitRequested;
    private double _nextStartOpenVrTime;

    public Routine(TcodeSerial serial, OpenVrStarter ovrStarter)
    {
        _serial = serial;
        _ovrStarter = ovrStarter;
        RawSerialData = new TcodeData();

        _ovrStarter.OnExited += () => Enqueue(() =>
        {
            IsOpenVrRunning = false;
        });
        _stopwatch = Stopwatch.StartNew();
    }

    public void Enqueue(Action action)
    {
        _queuedForMain.Enqueue(action);
    }

    public void MainLoop()
    {
        while (!_exitRequested)
        {
            Update();
        }
    }

    public string[] FetchPortNames()
    {
        return _serial.FetchPortNames();
    }

    public void TryConnectSerial(string portName)
    {
        if (_serial.IsOpen) return;
        
        _serial.OpenSerial(portName);
    }

    public void TryDisconnectSerial()
    {
        if (!_serial.IsOpen) return;

        _serial.CloseSerial();
    }

    private void Update()
    {
        while (_queuedForMain.TryDequeue(out var action))
        {
            action.Invoke();
        }

        if (!IsOpenVrRunning)
        {
            if (_stopwatch.Elapsed.TotalSeconds > _nextStartOpenVrTime)
            {
                _nextStartOpenVrTime = _stopwatch.Elapsed.TotalSeconds + 5;
                IsOpenVrRunning = _ovrStarter.TryStart();
            }
        }
        else
        {
            _ovrStarter.PollVrEvents();
        }
        
        if (_serial.IsOpen && RawSerialData.autoUpdate)
        {
            Submit();
        }
    }

    public void Submit()
    {
        _serial.TrySendCoords(
            new Vector3(RawSerialData.L0, RawSerialData.L1, RawSerialData.L2),
            new Vector3(RawSerialData.R0, RawSerialData.R1, RawSerialData.R2)
        );
    }

    public void Finish()
    {
        _exitRequested = true;
    }

    public bool IsSerialOpen()
    {
        return _serial.IsOpen;
    }
}

public class TcodeData
{
    public int L0 = 5000;
    public int L1 = 5000;
    public int L2 = 5000;
    public int R0 = 5000;
    public int R1 = 5000;
    public int R2 = 5000;
    public bool autoUpdate = true;
}
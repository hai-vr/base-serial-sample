using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using Hai.HView.Data;
using Hai.PositionSystemToExternalProgram.Extractors.OVR;
using Hai.PositionSystemToExternalProgram.Core;
using Hai.PositionSystemToExternalProgram.ExampleApp.Serial;
using Hai.PositionSystemToExternalProgram.Extractor.OVR;
using Hai.PositionSystemToExternalProgram.Extractors.GDI;
using Hai.PositionSystemToExternalProgram.Processors;

namespace Hai.BaseSerial.SampleProgram;

public class Routine
{
    private readonly TcodeSerial _serial;
    private readonly OpenVrStarter _ovrStarter;
    private readonly OpenVrExtractor _ovrExtractor;
    private readonly WindowGdiExtractor _windowGdiExtractor;
    private readonly SavedData _config;
    private readonly OversizedToBitsTransformer _toBits;
    private readonly ExtractedDataDecoder _decoder;

    public bool IsOpenVrRunning { get; private set; }
    public TcodeData RawSerialData { get; }
    public bool[] Bits { get; private set; }
    public DecodedLightBundle LightBundle { get; }
    public ExtractionResult ExtractedData { get; private set; }
    public ExtractLocation Location { get; private set; } = new();

    private readonly ConcurrentQueue<Action> _queuedForMain = new ConcurrentQueue<Action>();
    private readonly Stopwatch _stopwatch;
    
    private bool _exitRequested;
    private double _nextStartOpenVrTime;
    private int _lastExtractionIteration;

    public Routine(TcodeSerial serial, OpenVrStarter ovrStarter, OpenVrExtractor ovrExtractor, WindowGdiExtractor windowGdiExtractor, SavedData config, OversizedToBitsTransformer toBits, ExtractedDataDecoder decoder)
    {
        _serial = serial;
        _ovrStarter = ovrStarter;
        _ovrExtractor = ovrExtractor;
        _windowGdiExtractor = windowGdiExtractor;
        _config = config;
        _toBits = toBits;
        _decoder = decoder;
        
        RawSerialData = new TcodeData();
        LightBundle = new DecodedLightBundle();

        _ovrStarter.OnExited += () => Enqueue(() =>
        {
            IsOpenVrRunning = false;
        });
        _stopwatch = Stopwatch.StartNew();
        
        ExtractedData = new ExtractionResult
        {
            Success = false
        };
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
        var sw = Stopwatch.StartNew();
        while (_queuedForMain.TryDequeue(out var action))
        {
            action.Invoke();
        }
        
        // TODO: Only update this when the config updates
        _windowGdiExtractor.desiredWindowName = _config.windowName;

        if (IsOpenVrRunning)
        {
            // We need to poll events, because we need to detect when SteamVR is shutting down.
            _ovrStarter.PollVrEvents();
        }
        else
        {
            if (_stopwatch.Elapsed.TotalSeconds > _nextStartOpenVrTime)
            {
                _nextStartOpenVrTime = _stopwatch.Elapsed.TotalSeconds + 5;
                IsOpenVrRunning = _ovrStarter.TryStart();
            }
        }

        // We do the check again, as PollVrEvents may have shut OpenVR down.
        if (IsOpenVrRunning)
        {
            var eye = Location.useRightEye ? ExtractionSource.RightEye : ExtractionSource.LeftEye;
            var result = _ovrExtractor.Extract(eye, Location.coordinates);
            if (result.Success)
            {
                ExtractedData = result;
            }
        }
        else
        {
            var result = _windowGdiExtractor.Extract(ExtractionSource.Generic, Location.coordinates);
            if (result.Success)
            {
                ExtractedData = result;
            }
            else
            {
                Console.WriteLine("Failed to extract");
            }
        }

        if (ExtractedData.Success && _lastExtractionIteration != ExtractedData.Iteration)
        {
            _lastExtractionIteration = ExtractedData.Iteration;
            Bits = _toBits.ExtractBitsFromSubregion(ExtractedData.MonochromaticData, Location.coordinates.requestedWidth, Location.coordinates.requestedHeight);
            _decoder.DecodeInto(LightBundle, Bits);
        }
        
        if (_serial.IsOpen && RawSerialData.autoUpdate)
        {
            Submit();
        }

        // Limit logic to 100 fps, we don't want to extract images too fast
        var elapsedTime = sw.ElapsedMilliseconds;
        if (elapsedTime < 10)
        {
            Thread.Sleep((int)(10 - elapsedTime));
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

public class ExtractLocation
{
    // public int X = 0;
    // public int Y = 0;
    // public int W = 512;
    // public int H = 512;
    public ExtractionCoordinates coordinates = new()
    {
        x = 0,
        y = 0,
        requestedWidth = 128,
        requestedHeight = 128,
        anchorX = 0f,
        anchorY = 0f
    };
    public bool useRightEye = false;
}
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using Hai.PositionSystemToExternalProgram.Configuration;
using Hai.PositionSystemToExternalProgram.Extractors.OVR;
using Hai.PositionSystemToExternalProgram.Core;
using Hai.PositionSystemToExternalProgram.Tcode;
using Hai.PositionSystemToExternalProgram.Extractors.GDI;
using Hai.PositionSystemToExternalProgram.Processors;

namespace Hai.PositionSystemToExternalProgram.Program;

public class Routine
{
    private const int ViveProEyeVerticalBase = 3360;
    
    private readonly TcodeSerial _serial;
    private readonly OpenVrStarter _ovrStarter;
    private readonly OpenVrExtractor _ovrExtractor;
    private readonly WindowGdiExtractor _windowGdiExtractor;
    private readonly SavedData _config;
    private readonly OversizedToBitsTransformer _toBits;
    private readonly ExtractedDataDecoder _decoder;
    private readonly PositionSystemDataLayout _layout;
    private readonly DpsLightInterpreter _interpreter;

    public bool IsOpenVrRunning { get; private set; }
    public TcodeData RawSerialData { get; }
    public ExtractionCoordinates DesktopCoordinates { get; private set; } = new();
    public ExtractionCoordinates VrCoordinates { get; private set; } = new();
    public ExtractionResult ExtractedData { get; private set; }
    public bool[] Bits { get; private set; }
    public DecodedData Data { get; }
    public InterpretedLightData InterpretedData { get; private set; }
    
    public void RefreshConfiguration()
    {
        CopyCoordinates(_config.desktopCoordinates, DesktopCoordinates);
        CopyCoordinates(_config.vrCoordinates, VrCoordinates);
        
        _windowGdiExtractor.desiredWindowName = _config.windowName;
        VrCoordinates.source = _config.vrUseRightEye ? ExtractionSource.RightEye : ExtractionSource.LeftEye;
    }

    private void CopyCoordinates(ConfigCoord from, ExtractionCoordinates to)
    {
        to.x = from.x;
        to.y = from.y;
        to.anchorX = from.anchorX;
        to.anchorY = from.anchorY;
    }

    private readonly ConcurrentQueue<Action> _queuedForMain = new ConcurrentQueue<Action>();
    private readonly Stopwatch _stopwatch;
    
    private bool _exitRequested;
    private double _nextStartOpenVrTime;
    private int _lastExtractionIteration;

    public Routine(TcodeSerial serial,
        OpenVrStarter ovrStarter,
        OpenVrExtractor ovrExtractor,
        WindowGdiExtractor windowGdiExtractor,
        SavedData config,
        OversizedToBitsTransformer toBits,
        ExtractedDataDecoder decoder,
        PositionSystemDataLayout layout,
        DpsLightInterpreter interpreter)
    {
        _serial = serial;
        _ovrStarter = ovrStarter;
        _ovrExtractor = ovrExtractor;
        _windowGdiExtractor = windowGdiExtractor;
        _config = config;
        _toBits = toBits;
        _decoder = decoder;
        _layout = layout;
        _interpreter = interpreter;

        RawSerialData = new TcodeData();
        Data = new DecodedData();

        _ovrStarter.OnExited += () => Enqueue(() =>
        {
            IsOpenVrRunning = false;
        });
        _stopwatch = Stopwatch.StartNew();
        
        ExtractedData = new ExtractionResult
        {
            Success = false
        };
        
        RefreshConfiguration();
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
        var isUsingVrExtractor = IsUsingVrExtractor();
        var coordinates = isUsingVrExtractor ? VrCoordinates : DesktopCoordinates;
        if (isUsingVrExtractor)
        {
            var scale = (1 / 0.6f) * (_ovrExtractor.VerticalResolution(coordinates.source) / (float)ViveProEyeVerticalBase);
            // var scale = 1600 / 1000f;
            
            // FIXME: Move margin to data layout
            var MARGIN = 1;
            coordinates.requestedWidth = (int)((_layout.numberOfColumns + MARGIN * 2) * _layout.EncodedSquareSize * scale);
            coordinates.requestedHeight = (int)((_layout.numberOfDataLines + MARGIN * 2) * _layout.EncodedSquareSize * scale);
            var result = _ovrExtractor.Extract(VrCoordinates);
            if (result.Success)
            {
                ExtractedData = result;
            }
        }
        else
        {
            // FIXME: Move margin to data layout
            var MARGIN = 1;
            coordinates.requestedWidth = (int)((_layout.numberOfColumns + MARGIN * 2) * _layout.EncodedSquareSize);
            coordinates.requestedHeight = (int)((_layout.numberOfDataLines + MARGIN * 2) * _layout.EncodedSquareSize);
            var result = _windowGdiExtractor.Extract(DesktopCoordinates);
            if (result.Success)
            {
                ExtractedData = result;
            }
        }

        if (ExtractedData.Success && _lastExtractionIteration != ExtractedData.Iteration)
        {
            _lastExtractionIteration = ExtractedData.Iteration;
            Bits = _toBits.ExtractBitsFromSubregion(ExtractedData.MonochromaticData, coordinates.requestedWidth, coordinates.requestedHeight);
            _decoder.DecodeInto(Data, Bits);

            if (Data.validity == DataValidity.Ok)
            {
                InterpretedData = _interpreter.Interpret(Data);
            }
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

    public bool IsUsingVrExtractor()
    {
        return _config.extractorPreference == ExtractorConfig.PrioritizeVR && IsOpenVrRunning;
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

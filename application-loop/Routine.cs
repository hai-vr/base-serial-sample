using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using Hai.PositionSystemToExternalProgram.Configuration;
using Hai.PositionSystemToExternalProgram.Extractors.OVR;
using Hai.PositionSystemToExternalProgram.Core;
using Hai.PositionSystemToExternalProgram.Tcode;
using Hai.PositionSystemToExternalProgram.Extractors.GDI;
using Hai.PositionSystemToExternalProgram.Decoder;
using Hai.PositionSystemToExternalProgram.Robotics;

namespace Hai.PositionSystemToExternalProgram.ApplicationLoop;

public class Routine
{
    private const int ViveProEyeVerticalBase = 3360;
    
    private readonly TcodeSerial _serial;
    private readonly OpenVrStarter _ovrStarter;
    private readonly OpenVrExtractor _ovrExtractor;
    private readonly WindowGdiExtractor _windowGdiExtractor;
    private readonly SavedData _config;
    private readonly BitsTransformer _toBits;
    private readonly ExtractedDataDecoder _decoder;
    private readonly PositionSystemDataLayout _layout;
    private readonly DpsLightInterpreter _interpreter;
    private readonly RoboticsDriver _roboticsDriver;

    public bool IsOpenVrRunning { get; private set; }
    public TcodeData RawSerialData { get; }
    public ExtractionCoordinates WindowCoordinates { get; private set; } = new();
    public ExtractionCoordinates VrCoordinates { get; private set; } = new();
    public ExtractionResult ExtractedData { get; private set; }
    public bool[] Bits { get; private set; }
    public DecodedData Data { get; }
    public InterpretedLightData InterpretedData { get; private set; }
    
    //
    
    private readonly ConcurrentQueue<Action> _queuedForMain = new ConcurrentQueue<Action>();
    private readonly Stopwatch _globalStopwatch;
    
    private bool _exitRequested;
    private double _nextStartOpenVrTime;
    private int _lastExtractionIteration;
    private long _lastRoboticsUpdate;
    private bool _websocketStarted;

    private bool _hasReceivedDirectLightData;
    private InterpretedLightData _directLightData;
    private int _directExtraction;

    public event WebsocketStartRequested OnWebsocketStartRequested;
    public delegate void WebsocketStartRequested(ushort port);
    public event WebsocketStopRequested OnWebsocketStopRequested;
    public delegate void WebsocketStopRequested();
    
    public void RefreshExtractionConfiguration()
    {
        CopyCoordinates(_config.windowCoordinates, WindowCoordinates);
        CopyCoordinates(_config.vrCoordinates, VrCoordinates);
        
        _windowGdiExtractor.desiredWindowName = _config.windowName;
        VrCoordinates.source = _config.vrUseRightEye ? ExtractionSource.RightEye : ExtractionSource.LeftEye;
    }
    
    public void RefreshRoboticsConfiguration()
    {
        _roboticsDriver.UpdateConfiguration(
            configRoboticsVirtualScale: _config.roboticsVirtualScale,
            configRoboticsSafetyUsePolarMode: _config.roboticsSafetyUsePolarMode,
            configRoboticsUsePidRoot: _config.roboticsUsePidRoot,
            configRoboticsUsePidTarget: _config.roboticsUsePidTarget
        );
    }

    public void RefreshWebsocketsConfiguration()
    {
        if (_config.useResoniteWebsockets && !_websocketStarted)
        {
            _websocketStarted = true;
            OnWebsocketStartRequested?.Invoke(IWebsocketActions.WebsocketDefaultPort);
        }

        if (!_config.useResoniteWebsockets && _websocketStarted)
        {
            _websocketStarted = false;
            OnWebsocketStopRequested?.Invoke();
        }
    }

    private void CopyCoordinates(ConfigCoord from, ExtractionCoordinates to)
    {
        to.x = from.x;
        to.y = from.y;
        to.anchorX = from.anchorX;
        to.anchorY = from.anchorY;
    }

    public Routine(SavedData config,
        PositionSystemDataLayout layout,
        OpenVrStarter ovrStarter,
        OpenVrExtractor ovrExtractor,
        WindowGdiExtractor windowGdiExtractor,
        BitsTransformer toBits,
        ExtractedDataDecoder decoder,
        DpsLightInterpreter interpreter,
        RoboticsDriver roboticsDriver,
        TcodeSerial serial)
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
        _roboticsDriver = roboticsDriver;

        RawSerialData = new TcodeData();
        Data = new DecodedData
        {
            validity = DataValidity.NotInitialized
        };

        _ovrStarter.OnExited += () => Enqueue(() =>
        {
            IsOpenVrRunning = false;
        });
        _globalStopwatch = Stopwatch.StartNew();
        
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
        // Don't do this in the constructor, as some of these configurations (the websocket one especially)
        // needs to have events hooked beforehand. 
        RefreshExtractionConfiguration();
        RefreshRoboticsConfiguration();
        RefreshWebsocketsConfiguration();
        
        while (!_exitRequested)
        {
            Update();
        }

        if (_websocketStarted)
        {
            OnWebsocketStopRequested?.Invoke();
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
        
        if (IsOpenVrRunning)
        {
            // We need to poll events, because we need to detect when SteamVR is shutting down.
            _ovrStarter.PollVrEvents();
        }
        else
        {
            if (_globalStopwatch.Elapsed.TotalSeconds > _nextStartOpenVrTime)
            {
                _nextStartOpenVrTime = _globalStopwatch.Elapsed.TotalSeconds + 5;
                IsOpenVrRunning = _ovrStarter.TryStart();
            }
        }

        if (!_hasReceivedDirectLightData)
        {
            // We do the check again, as PollVrEvents may have shut OpenVR down.
            var isUsingVrExtractor = IsUsingVrExtractor();
            var coordinates = isUsingVrExtractor ? VrCoordinates : WindowCoordinates;
            if (isUsingVrExtractor)
            {
                // FIXME: Where does this magic constant even come from? Both ViveProEyeVerticalBase and 1 / 0.5945f
                var scale = (1 / 0.5945f) * (_ovrExtractor.VerticalResolution(coordinates.source) / (float)ViveProEyeVerticalBase);
                // var scale = 1600 / 1000f;
            
                coordinates.requestedWidth = (int)((_layout.NumberOfColumns + _layout.MarginPerSide * 2) * _layout.EncodedSquareSize * scale);
                coordinates.requestedHeight = (int)((_layout.NumberOfDataLines + _layout.MarginPerSide * 2) * _layout.EncodedSquareSize * scale);
                var result = _ovrExtractor.Extract(VrCoordinates);
                if (result.Success)
                {
                    ExtractedData = result;
                }
            }
            else
            {
                coordinates.requestedWidth = (int)((_layout.NumberOfColumns + _layout.MarginPerSide * 2) * _layout.EncodedSquareSize);
                coordinates.requestedHeight = (int)((_layout.NumberOfDataLines + _layout.MarginPerSide * 2) * _layout.EncodedSquareSize);
                var result = _windowGdiExtractor.Extract(WindowCoordinates);
                if (result.Success)
                {
                    ExtractedData = result;
                }
            }

            if (ExtractedData.Success && _lastExtractionIteration != ExtractedData.Iteration)
            {
                _lastExtractionIteration = ExtractedData.Iteration;
                Bits = _toBits.ReadBitsFromExtractedImage(ExtractedData.MonochromaticData, coordinates.requestedWidth, coordinates.requestedHeight);
                _decoder.DecodeInto(Data, Bits);

                if (Data.validity == DataValidity.Ok)
                {
                    InterpretedData = _interpreter.Interpret(Data);
                    _roboticsDriver.ProvideTargets(InterpretedData);
                }
                else
                {
                    _roboticsDriver.MarkDataFailure();
                }
            }
        }
        else
        {
            if (_lastExtractionIteration != _directExtraction)
            {
                _lastExtractionIteration = _directExtraction;
                
                InterpretedData = _directLightData;
                _roboticsDriver.ProvideTargets(InterpretedData);
            }
        }

        // TODO: Split image extraction logic update rate from robotics logic update rate.
        var roboticsCoordinates = _roboticsDriver.UpdateAndGetCoordinates(_lastRoboticsUpdate == 0 ? 10L : _globalStopwatch.ElapsedMilliseconds - _lastRoboticsUpdate);
        _lastRoboticsUpdate = _globalStopwatch.ElapsedMilliseconds;

        RawSerialData.L0 = RemapTarget(roboticsCoordinates.JoystickTargetL0);
        RawSerialData.L1 = RemapTarget(roboticsCoordinates.JoystickTargetL1);
        RawSerialData.L2 = RemapTarget(roboticsCoordinates.JoystickTargetL2);
        RawSerialData.R0 = RemapTarget(roboticsCoordinates.AngleDegR0 / 35f);
        RawSerialData.R1 = RemapTarget(roboticsCoordinates.AngleDegR1 / 35f);
        RawSerialData.R2 = RemapTarget(roboticsCoordinates.AngleDegR2 / 35f);
        
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

    private int RemapTarget(float joystick)
    {
        return (int)(5000 + joystick * 5000);
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

    public void ReceiveDirectControl(float positionX, float positionY, float positionZ, float normalX, float normalY, float normalZ)
    {
        InternalDirectLightData(positionX, positionY, positionZ, normalX, normalY, normalZ);
    }

    public void ReceiveDirectControl(float positionX, float positionY, float positionZ, float normalX, float normalY, float normalZ, float tangentX, float tangentY, float tangentZ)
    {
        InternalDirectLightData(positionX, positionY, positionZ, normalX, normalY, normalZ);
    }

    private void InternalDirectLightData(float positionX, float positionY, float positionZ, float normalX, float normalY, float normalZ)
    {
        _hasReceivedDirectLightData = true;
        _directLightData = new InterpretedLightData
        {
            position = new Vector3(positionX, positionY, positionZ),
            normal = new Vector3(normalX, normalY, normalZ),
            hasTarget = true,
            hasNormal = true
        };
        _directExtraction++;
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

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
    private readonly ITransmitter _transmitter;
    private readonly OpenVrStarter _ovrStarter;
    private readonly OpenVrExtractor _ovrExtractor;
    private readonly WindowGdiExtractor _windowGdiExtractor;
    private readonly SavedData _config;
    private readonly BitsTransformer _toBits;
    private readonly ExtractedDataDecoder _decoder;
    private readonly PositionSystemDataLayout _layout;
    private readonly DpsLightInterpreter _interpreter;
    private readonly RoboticsDriver _roboticsDriver;
    private readonly ScaleEvaluator _scaleEvaluator;

    public bool IsOpenVrRunning { get; private set; }
    public TcodeData RawSerialData { get; }
    public ExtractionCoordinates WindowCoordinates { get; private set; } = new();
    public ExtractionCoordinates VrCoordinates { get; private set; } = new();
    public ExtractionResult ExtractedData { get; private set; }
    public bool[] Bits { get; private set; }
    public DecodedData Data { get; }
    public InterpretedLightData InterpretedData { get; private set; }
    public float VirtualScale { get; private set; } = 1f;
    
    //
    
    private readonly ConcurrentQueue<Action> _queuedForMain = new ConcurrentQueue<Action>();
    private readonly Stopwatch _globalStopwatch;
    
    private bool _exitRequested;
    private double _nextStartOpenVrTime;
    private int _lastExtractionIteration;
    private long _lastRoboticsUpdate;
    private long _lastTransmissionUpdate;
    private bool _websocketStarted;

    private bool _hasReceivedDirectLightData;
    private InterpretedLightData _directLightData;
    private int _directExtraction;
    private readonly Stopwatch _tickWatch = new();

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

        void CopyCoordinates(ConfigCoord from, ExtractionCoordinates to)
        {
            to.x = from.x;
            to.y = from.y;
            to.anchorX = from.anchorX;
            to.anchorY = from.anchorY;
        }
    }
    
    public void RefreshRoboticsConfiguration()
    {
        _roboticsDriver.UpdateConfiguration(
            configRoboticsVirtualScale: _config.roboticsVirtualScale,
            configRoboticsSafetyUsePolarMode: _config.roboticsSafetyUsePolarMode,
            configRoboticsUsePidRoot: _config.roboticsUsePidRoot,
            configRoboticsUsePidTarget: _config.roboticsUsePidTarget,
            configTopmostHardLimit: _config.roboticsTopmostHardLimit,
            configOffsetAngleDegR2: _config.roboticsOffsetAngleDegR2,
            configRotateSystemAngleDegPitch: _config.roboticsRotateSystemAngleDegPitch,
            configCompensateVirtualScaleHardLimit: _config.roboticsCompensateVirtualScaleHardLimit);
    }

    public void RefreshWebsocketsConfiguration()
    {
        if (_config.useWebsockets && !_websocketStarted)
        {
            _websocketStarted = true;
            OnWebsocketStartRequested?.Invoke(IWebsocketActions.WebsocketDefaultPort);
        }

        if (!_config.useWebsockets && _websocketStarted)
        {
            _websocketStarted = false;
            OnWebsocketStopRequested?.Invoke();
        }
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

        _scaleEvaluator = new ScaleEvaluator();
        _transmitter = serial;
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
        if (_serial.IsOpen()) return;

        _serial.PortName = portName;
        _serial.Open();
    }

    public void TryDisconnectSerial()
    {
        if (!_serial.IsOpen()) return;

        _serial.Close();
    }

    private void Update()
    {
        _tickWatch.Restart();
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

        if (IsOpenVrRunning && Data.validity == DataValidity.Ok && Data.Version >= 1_001_000)
        {
            _ovrExtractor.Additions.UpdatePoses();
            var hmdPosition = _ovrExtractor.Additions.GetHmdPositionAsUnityVector();
            _scaleEvaluator.Evaluate(hmdPosition, Data.CameraPosition);
            VirtualScale = _scaleEvaluator.VirtualScale;
        }

        // TODO: Split image extraction logic update rate from robotics logic update rate.
        var roboticsCoordinates = _roboticsDriver.UpdateAndGetCoordinates(_lastRoboticsUpdate == 0 ? 10L : _globalStopwatch.ElapsedMilliseconds - _lastRoboticsUpdate);
        _lastRoboticsUpdate = _globalStopwatch.ElapsedMilliseconds;
        
        Submit(roboticsCoordinates);
        
        // Limit logic to 100 fps, we don't want to extract images too fast
        var elapsedTime = _tickWatch.ElapsedMilliseconds;
        if (elapsedTime < 10)
        {
            Thread.Sleep((int)(10 - elapsedTime));
        }
    }

    public bool IsUsingVrExtractor()
    {
        return _config.extractorPreference == ExtractorConfig.PrioritizeVR && IsOpenVrRunning;
    }

    private void Submit(RoboticsCoordinates roboticsCoordinates)
    {
        if (!_transmitter.IsOpen()) return;
        
        if (RawSerialData.autoUpdate)
        {
            RawSerialData.L0 = RemapTarget(roboticsCoordinates.JoystickTargetL0);
            RawSerialData.L1 = RemapTarget(roboticsCoordinates.JoystickTargetL1);
            RawSerialData.L2 = RemapTarget(roboticsCoordinates.JoystickTargetL2);
            RawSerialData.R0 = RemapTarget(roboticsCoordinates.AngleDegR0 / 35f);
            RawSerialData.R1 = RemapTarget(roboticsCoordinates.AngleDegR1 / 35f);
            RawSerialData.R2 = RemapTarget(roboticsCoordinates.AngleDegR2 / 35f);
        
            _transmitter.ProvideNewTarget(roboticsCoordinates);
        }

        var duration = _globalStopwatch.ElapsedMilliseconds - _lastTransmissionUpdate;
        _transmitter.Update(_lastTransmissionUpdate == 0 ? 10L : duration > 1000 ? 1000 : duration);
        _lastTransmissionUpdate = _globalStopwatch.ElapsedMilliseconds;
    }

    public void Finish()
    {
        _exitRequested = true;
    }

    public bool IsSerialOpen()
    {
        return _serial.IsOpen();
    }

    public void ReceiveDirectControl(float positionX, float positionY, float positionZ, float normalX, float normalY, float normalZ)
    {
        InternalDirectLightData(new Vector3(positionX, positionY, positionZ), new Vector3(normalX, normalY, normalZ), null);
    }

    public void ReceiveDirectControl(float positionX, float positionY, float positionZ, float normalX, float normalY, float normalZ, float tangentX, float tangentY, float tangentZ)
    {
        InternalDirectLightData(new Vector3(positionX, positionY, positionZ), new Vector3(normalX, normalY, normalZ), new Vector3(tangentX, tangentY, tangentZ));;
    }

    private void InternalDirectLightData(Vector3 position, Vector3 normalUntrusted, Vector3? tangentUntrustedNullable)
    {
        _hasReceivedDirectLightData = true;
        _directLightData = new InterpretedLightData
        {
            position = position,
            normal = Vector3.Normalize(normalUntrusted),
            tangent = tangentUntrustedNullable != null ? Vector3.Normalize(tangentUntrustedNullable.Value) : Vector3.Zero,
            hasTarget = true,
            hasNormal = true,
            hasTangent = tangentUntrustedNullable != null
        };
        _directExtraction++;
    }

    private int RemapTarget(float joystick)
    {
        return (int)(5000 + joystick * 5000);
    }
}
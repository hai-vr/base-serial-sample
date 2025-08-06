using System.Numerics;
using Hai.PositionSystemToExternalProgram.Configuration;
using Hai.PositionSystemToExternalProgram.Core;
using Hai.PositionSystemToExternalProgram.Decoder;
using ImGuiNET;
using Veldrid;
using Veldrid.Sdl2;

namespace Hai.PositionSystemToExternalProgram.Program;

public class UiMainApplication
{
    private const ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoResize;
    private const ImGuiWindowFlags WindowFlagsNoCollapse = WindowFlags | ImGuiWindowFlags.NoCollapse;

    private const string AutoUpdateLabel = "Auto-update";
    private const string CloseSerialLabel = "Close serial";
    private const string DataLabel = "Data";
    private const string DebugLabel = "Debug";
    private const string ExtractorLabel = "Extractor";
    private const string ExtractorPreferenceLabel = "Extractor Preference";
    private const string HardwareLabel = "Hardware";
    private const string InterpretedDataLabel = "Interpreted data";
    private const string LightsLabel = "Lights";
    private const string ModeLabel = "Mode";
    private const string OpenVrLabel = "OpenVR";
    private const string RefreshLabel = "Refresh";
    private const string ResetToDefaultsExceptWindowNameLabel = "Reset to defaults (except Window name)";
    private const string ResetToDefaultsLabel = "Reset to defaults";
    private const string SpoutLabel = "Spout";
    private const string SubmitLabel = "Submit";
    private const string UseRightEyeLabel = "Use right eye";
    private const string WindowLabel = "Window";
    private const string WindowNameLabel = "Window name";
    
    private const string MsgChecksumInvalid = "Checksum is failing";
    private const string MsgChecksumOk = "Data is OK";
    private const string MsgChecksumUnexpectedMajorVersion = "Unexpected major version";
    private const string MsgChecksumUnexpectedVendor = "Unexpected vendor";
    private const string MsgConnectToDeviceOnSerialPort = "Connect to device on serial port {0}";
    private const string MsgOpenVrUnavailable = "OpenVR is not running.";
    private const string MsgSpoutUnavailable = "Spout is not yet available in this version of the software.";

    private readonly UiActions _uiActions;
    private readonly SavedData _config;
    private readonly UiScrollManager _scrollManager = new UiScrollManager();
    
    private string[] _portNames;
    private int _selectedPortIndex;
    private string _selectedPortName = "";
    
    private int _lastExtractedDataIteration;
    private Texture _cachedTexture;
    private int _lastWidth;
    private int _lastHeight;
    private IntPtr _textureId;
    private readonly string[] _extractorNames;

    public UiMainApplication(UiActions uiActions, SavedData config)
    {
        _uiActions = uiActions;
        _config = config;
        _extractorNames = Enum.GetNames<ExtractorConfig>();
    }

    public void Initialize()
    {
        UpdatePortNames();
        _selectedPortName = _portNames.Length > 0 ? _portNames[0] : "";
    }

    public void SubmitUi(CustomImGuiController controller, Sdl2Window window)
    {
        ImGui.SetNextWindowPos(new Vector2(0, 0), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(window.Width, window.Height), ImGuiCond.Always);
        ImGui.Begin("###main", WindowFlagsNoCollapse | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollbar);

        DrawMain(controller, window);
        
        ImGui.End();
        
        _scrollManager.StoreIfAnyItemHovered();
    }

    private void DrawMain(CustomImGuiController controller, Sdl2Window window)
    {
        var rawData = _uiActions.ExposeRawData();
        var isSerialOpen = _uiActions.IsSerialOpen();
        var isOpenVrRunning = _uiActions.IsOpenVrRunning();
        
        ImGui.BeginDisabled(isSerialOpen);
        if (ImGui.BeginCombo("##PortCombo", _selectedPortName))
        {
            for (int i = 0; i < _portNames.Length; i++)
            {
                bool isSelected = (_selectedPortIndex == i);
                if (ImGui.Selectable(_portNames[i], isSelected))
                {
                    _selectedPortIndex = i;
                    _selectedPortName = _portNames[i];
                }
                    
                if (isSelected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }
            ImGui.EndCombo();
        }
        ImGui.SameLine();
        if (ImGui.Button(RefreshLabel))
        {
            UpdatePortNames();
        }
        ImGui.EndDisabled();

        if (isSerialOpen)
        {
            ImGui.SameLine();
            if (ImGui.Button(CloseSerialLabel))
            {
                _uiActions.DisconnectSerial();
            }
        }
        else
        {
            ImGui.BeginDisabled(_selectedPortName == "");
            var port = (_selectedPortName == "" ? "UNKNOWN" : _selectedPortName);
            if (ImGui.Button(string.Format(MsgConnectToDeviceOnSerialPort, port), new Vector2(ImGui.GetContentRegionAvail().X, 60)))
            {
                _uiActions.ConnectSerial(_selectedPortName);
            }
            ImGui.EndDisabled();
        }
        
        var anyChanged = false;
        ImGui.BeginTabBar("##tabs");
        _scrollManager.MakeTab(ExtractorLabel, () =>
        {
            ImGui.SeparatorText(ExtractorPreferenceLabel);
            var currentExtractor = (int)_config.extractorPreference;
            if (ImGui.Combo(ModeLabel, ref currentExtractor, _extractorNames, _extractorNames.Length))
            {
                _config.extractorPreference = (ExtractorConfig)currentExtractor;
                anyChanged = true;
            }

            if (_config.extractorPreference is ExtractorConfig.PrioritizeSpout or ExtractorConfig.UseSpoutIfVRRunning)
            {
                ImGui.SeparatorText(SpoutLabel);
                ImGui.Text(MsgSpoutUnavailable);
            }
            
            var anyCoordinateChanged = false;
            if (!_uiActions.IsUsingVrExtractor())
            {
                if (_config.extractorPreference == ExtractorConfig.PrioritizeVR && !isOpenVrRunning)
                {
                    ImGui.SeparatorText(OpenVrLabel);
                    ImGui.Text(MsgOpenVrUnavailable);
                }
                ImGui.SeparatorText(WindowLabel);
                anyCoordinateChanged |= SmallAdjustmentSlider("Window Offset X", ref _config.windowCoordinates.x);
                anyCoordinateChanged |= SmallAdjustmentSlider("Window Offset Y", ref _config.windowCoordinates.y);
                anyCoordinateChanged |= ImGui.SliderFloat("Window Anchor X", ref _config.windowCoordinates.anchorX, 0f, 1f);
                anyCoordinateChanged |= ImGui.SliderFloat("Window Anchor Y", ref _config.windowCoordinates.anchorY, 0f, 1f);
                anyCoordinateChanged |= ImGui.InputText(WindowNameLabel, ref _config.windowName, 500);
                if (ImGui.Button(ResetToDefaultsExceptWindowNameLabel))
                {
                    anyCoordinateChanged = true;
                    _config.SetWindowCoordinatesToDefault();
                }
            }
            else
            {
                ImGui.SeparatorText(OpenVrLabel);
                anyCoordinateChanged |= SmallAdjustmentSlider("VR Offset X", ref _config.vrCoordinates.x);
                anyCoordinateChanged |= SmallAdjustmentSlider("VR Offset Y", ref _config.vrCoordinates.y);
                anyCoordinateChanged |= ImGui.SliderFloat("VR Anchor X", ref _config.vrCoordinates.anchorX, 0f, 1f);
                anyCoordinateChanged |= ImGui.SliderFloat("VR Anchor Y", ref _config.vrCoordinates.anchorY, 0f, 1f);
                anyCoordinateChanged |= ImGui.Checkbox(UseRightEyeLabel, ref _config.vrUseRightEye);
                if (ImGui.Button(ResetToDefaultsLabel))
                {
                    anyCoordinateChanged = true;
                    _config.SetVrCoordinatesToDefault();
                }
            }

            if (anyCoordinateChanged)
            {
                _uiActions.ConfigCoordinatesUpdated();
            }

            anyChanged |= anyCoordinateChanged;
            
            var extractedData = _uiActions.ExtractedData();
            if (extractedData.IsValid())
            {
                var interpreted = _uiActions.InterpretedData();
                ImGui.SeparatorText(DebugLabel);
                ImGui.Columns(2);
                
                ImGui.SeparatorText(InterpretedDataLabel);
                InterpretedDebug(interpreted);
                
                ImGui.SeparatorText(DataLabel);
                DrawSieve(16, 101);
                ImGui.Text("");
                DrawSieve(16);
                ImGui.NextColumn();

                var textureId = TurnDataIntoTexture(controller, extractedData);

                ImGui.Text($"{extractedData.Iteration}");
                ImGui.Image(textureId, new Vector2(_lastWidth, _lastHeight));
                var coordinates = _uiActions.IsUsingVrExtractor() ? _uiActions.VrCoordinates() : _uiActions.WindowCoordinates();
                ImGui.Text($"{coordinates.requestedWidth} x {coordinates.requestedHeight}");
            }
            
            ImGui.Columns(1);
        });
        _scrollManager.MakeTab(DebugLabel, () =>
        {
            ImGui.BeginTabBar("##tabs_debug");
            _scrollManager.MakeTab(LightsLabel, () =>
            {
                var data = _uiActions.Data();
                var interpreted = _uiActions.InterpretedData();
                var valid = data.validity == DataValidity.Ok;
        
                ShowDataWarningIfApplicable(data);
                
                if (!valid) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 0, 0, 1));
                for (var index = 0; index < data.Lights.Length; index++)
                {
                    var decodedLight = data.Lights[index];
                    ImGui.SeparatorText($"Light #{index + 1}");
                    ImGui.BeginDisabled(!decodedLight.enabled);
                    ImGui.Text($"Enabled: {BoolToString(decodedLight.enabled)}");
                    ImGui.Text($"Range: {decodedLight.range}");
                    ImGui.Text($"Color: {decodedLight.color.X} {decodedLight.color.Y} {decodedLight.color.Z}");
                    ImGui.Text($"Intensity: {decodedLight.intensity}");
                    ImGui.Text($"Position: {decodedLight.position.X} {decodedLight.position.Y} {decodedLight.position.Z}");
                    ImGui.EndDisabled();
                }
                if (!valid) ImGui.PopStyleColor();
                
                ImGui.SeparatorText(InterpretedDataLabel);
                InterpretedDebug(interpreted);
            });
            _scrollManager.MakeTab(DataLabel, () =>
            {
                var extractedData = _uiActions.ExtractedData();
                if (extractedData.IsValid())
                {
                    DrawSieve(32);
                }
            });
            ImGui.EndTabBar();
        });
        _scrollManager.MakeTab(HardwareLabel, () =>
        {
            ImGui.SliderInt("L0", ref rawData.L0, 0, 9999);
            ImGui.SliderInt("L1", ref rawData.L1, 0, 9999);
            ImGui.SliderInt("L2", ref rawData.L2, 0, 9999);
            ImGui.SliderInt("R0", ref rawData.R0, 0, 9999);
            ImGui.SliderInt("R1", ref rawData.R1, 0, 9999);
            ImGui.SliderInt("R2", ref rawData.R2, 0, 9999);
            ImGui.Checkbox(AutoUpdateLabel, ref rawData.autoUpdate);
            
            ImGui.BeginDisabled(!isSerialOpen || rawData.autoUpdate);
            if (ImGui.Button(SubmitLabel))
            {
                _uiActions.Submit();
            }
            ImGui.EndDisabled();
        });
        _scrollManager.MakeTab(VERSION.miniVersion, () =>
        {
            ImGui.Text($"Version: {VERSION.version}");
        });

        ImGui.EndTabBar();
        
        if (anyChanged)
        {
            _config.SaveConfig();
        }
    }

    private bool SmallAdjustmentSlider(string label, ref int coord)
    {
        var anyChanged = false;
        if (ImGui.Button($"-##minus__{label}"))
        {
            coord--;
            anyChanged = true;
        }
        ImGui.SameLine();
        anyChanged |= ImGui.SliderInt($"##slider__{label}", ref coord, -100, 100);
        ImGui.SameLine();
        if (ImGui.Button($"+##plus__{label}"))
        {
            coord++;
            anyChanged = true;
        }
        ImGui.SameLine();
        if (ImGui.Button($"0##plus__{label}"))
        {
            coord = 0;
            anyChanged = true;
        }
        ImGui.SameLine();
        ImGui.Text(label);

        return anyChanged;
    }

    private static void InterpretedDebug(InterpretedLightData interpreted)
    {
        ImGui.Text($"HasTarget: {BoolToString(interpreted.hasTarget)}");
        ImGui.Text($"HasNormal: {BoolToString(interpreted.hasNormal)}");
        var interpretedType = interpreted.isHole ? "hole" : interpreted.isRing ? "ring" : "undefined";
        ImGui.Text($"Type: {interpretedType}");
        ImGui.Text($"Position: {interpreted.position.X} {interpreted.position.Y} {interpreted.position.Z}");
        ImGui.Text($"Normal: {interpreted.normal.X} {interpreted.normal.Y} {interpreted.normal.Z}");
    }

    private static string BoolToString(bool b)
    {
        return b ? "true" : "false";
    }

    private void DrawSieve(int numberOfColumns, int startAtRow = 0)
    {
        var bits = _uiActions.Bits();
        var data = _uiActions.Data();
        var valid = data.validity == DataValidity.Ok;
        
        ShowDataWarningIfApplicable(data);

        if (!valid) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.5f, 1));
        
        var numberOfLines = PositionSystemDataLayout.CalculateNumberOfLines(numberOfColumns);
        var digitCount = (int)Math.Floor(Math.Log10(numberOfLines)) + 1;
        var format = new string('0', digitCount);
        
        for (var row = startAtRow; row < numberOfLines; row++)
        {
            ImGui.Text($"#{row.ToString(format)}  ");
            ImGui.SameLine();
            for (var index = 0; index < numberOfColumns; index++)
            {
                var inx = row * numberOfColumns + index;
                var b = inx < bits.Length ? bits[inx] : false;
                var xx = b;
                ImGui.Text(xx ? "X" : ".");
                if (index != numberOfColumns - 1)
                {
                    ImGui.SameLine();
                }
            }
            if (numberOfLines == ExtractedDataDecoder.GroupLength)
            {
                ImGui.SameLine();
                ImGui.Text("  ->   " + Enum.GetName(typeof(ShaderV1_0_0), (ShaderV1_0_0)row));
            }
        }
        if (!valid) ImGui.PopStyleColor();
    }

    private static void ShowDataWarningIfApplicable(DecodedData data)
    {
        var valid = data.validity == DataValidity.Ok;
        if (!valid) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 0, 0, 1));
        switch (data.validity)
        {
            case DataValidity.Ok:
                ImGui.Text(MsgChecksumOk);
                break;
            case DataValidity.InvalidChecksum:
                ImGui.Text(MsgChecksumInvalid);
                break;
            case DataValidity.UnexpectedVendor:
                ImGui.Text(MsgChecksumUnexpectedVendor);
                break;
            case DataValidity.UnexpectedMajorVersion:
                ImGui.Text(MsgChecksumUnexpectedMajorVersion);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        if (!valid) ImGui.PopStyleColor();
    }

    private IntPtr TurnDataIntoTexture(CustomImGuiController controller, ExtractionResult extractedData)
    {
        if (extractedData.Iteration == _lastExtractedDataIteration) return _textureId;
        
        _lastExtractedDataIteration = extractedData.Iteration;
        if (_cachedTexture == null || _lastWidth != extractedData.Width || _lastHeight != extractedData.Height)
        {
            _cachedTexture?.Dispose();
            _cachedTexture = controller.Graphics.ResourceFactory.CreateTexture(new TextureDescription(
                (uint)extractedData.Width,
                (uint)extractedData.Height,
                1, // depth
                1, // mipLevels
                1, // arrayLayers
                PixelFormat.R8_G8_B8_A8_UNorm,
                TextureUsage.Sampled,
                TextureType.Texture2D
            ));
            _lastWidth = extractedData.Width;
            _lastHeight = extractedData.Height;
        }
            
        controller.Graphics.UpdateTexture(
            _cachedTexture,
            extractedData.ColorData,
            0, 0, 0, // x, y, z offsets
            (uint)extractedData.Width,
            (uint)extractedData.Height,
            1, // depth
            0, // mipLevel
            0  // arrayLayer
        );
            
        _textureId = controller.GetOrCreateImGuiBinding(controller.Graphics.ResourceFactory, _cachedTexture);

        return _textureId;
    }

    private void UpdatePortNames()
    {
        _portNames = _uiActions.FetchPortNames();
    }
}
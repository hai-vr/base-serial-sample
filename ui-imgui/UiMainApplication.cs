using System.Numerics;
using Hai.PositionSystemToExternalProgram.Configuration;
using Hai.PositionSystemToExternalProgram.Core;
using ImGuiNET;
using Veldrid;
using Veldrid.Sdl2;

namespace Hai.PositionSystemToExternalProgram.ImGuiProgram;

public class UiMainApplication
{
    private const ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoResize;
    private const ImGuiWindowFlags WindowFlagsNoCollapse = WindowFlags | ImGuiWindowFlags.NoCollapse;

    private readonly IUiActions _uiActions;
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

    private readonly UiRoboticsTab _roboticsTab;

    public UiMainApplication(IUiActions uiActions, SavedData config)
    {
        _uiActions = uiActions;
        _config = config;
        _extractorNames = Enum.GetNames<ExtractorConfig>();

        _roboticsTab = new UiRoboticsTab(_uiActions, config);
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
        var data = _uiActions.Data();
        
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
        if (ImGui.Button(LocalizationPhrase.MainLocalizationPhrase.RefreshLabel))
        {
            UpdatePortNames();
        }
        ImGui.EndDisabled();

        if (isSerialOpen)
        {
            ImGui.SameLine();
            if (ImGui.Button(LocalizationPhrase.MainLocalizationPhrase.CloseSerialLabel))
            {
                _uiActions.DisconnectSerial();
            }
        }
        else
        {
            ImGui.BeginDisabled(_selectedPortName == "");
            var port = (_selectedPortName == "" ? "UNKNOWN" : _selectedPortName);
            if (ImGui.Button(string.Format(LocalizationPhrase.MainLocalizationPhrase.MsgConnectToDeviceOnSerialPort, port), new Vector2(ImGui.GetContentRegionAvail().X, 60)))
            {
                _uiActions.ConnectSerial(_selectedPortName);
            }
            ImGui.EndDisabled();
        }
        
        ShowDataWarningIfApplicable(data);
        if (data.validity == DataValidity.Ok)
        {
            var interpreted = _uiActions.InterpretedData();
            if (interpreted.hasTarget)
            {
                ImGui.SameLine();
                ImGui.Text("- Light found");
            }
            else
            {
                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.5f, 1));
                ImGui.Text("- Light not found");
                ImGui.PopStyleColor();
            }
        }
        
        var anyChanged = false;
        ImGui.BeginTabBar("##tabs");
        _scrollManager.MakeTab(LocalizationPhrase.MainLocalizationPhrase.RoboticsLabel, () => { anyChanged |= _roboticsTab.RoboticsTab(); });
        _scrollManager.MakeTab(LocalizationPhrase.MainLocalizationPhrase.RoboticsAdvancedLabel, () =>
        {
            var anyRoboticsConfigChanged = _roboticsTab.RoboticsAdvancedTab();
            anyChanged |= anyRoboticsConfigChanged;
            
            if (anyRoboticsConfigChanged)
            {
                _uiActions.ConfigRoboticsUpdated();
            }
        });
        _scrollManager.MakeTab(LocalizationPhrase.MainLocalizationPhrase.DataCalibrationLabel, () =>
        {
            ImGui.SeparatorText(LocalizationPhrase.MainLocalizationPhrase.ExtractorPreferenceLabel);
            var currentExtractor = (int)_config.extractorPreference;
            if (ImGui.Combo(LocalizationPhrase.MainLocalizationPhrase.ModeLabel, ref currentExtractor, _extractorNames, _extractorNames.Length))
            {
                _config.extractorPreference = (ExtractorConfig)currentExtractor;
                anyChanged = true;
            }

            if (_config.extractorPreference is ExtractorConfig.PrioritizeSpout or ExtractorConfig.UseSpoutIfVRRunning)
            {
                ImGui.SeparatorText(LocalizationPhrase.MainLocalizationPhrase.SpoutLabel);
                ImGui.Text(LocalizationPhrase.MainLocalizationPhrase.MsgSpoutUnavailable);
            }
            
            var anyCoordinateChanged = false;
            if (!_uiActions.IsUsingVrExtractor())
            {
                if (_config.extractorPreference == ExtractorConfig.PrioritizeVR && !isOpenVrRunning)
                {
                    ImGui.SeparatorText(LocalizationPhrase.MainLocalizationPhrase.OpenVrLabel);
                    ImGui.Text(LocalizationPhrase.MainLocalizationPhrase.MsgOpenVrUnavailable);
                }
                ImGui.SeparatorText(LocalizationPhrase.MainLocalizationPhrase.WindowLabel);
                anyCoordinateChanged |= SmallAdjustmentSlider($"{LocalizationPhrase.MainLocalizationPhrase.WindowOffsetLabel} X", ref _config.windowCoordinates.x);
                anyCoordinateChanged |= SmallAdjustmentSlider($"{LocalizationPhrase.MainLocalizationPhrase.WindowOffsetLabel} Y", ref _config.windowCoordinates.y);
                anyCoordinateChanged |= ImGui.SliderFloat($"{LocalizationPhrase.MainLocalizationPhrase.WindowAnchorLabel} X", ref _config.windowCoordinates.anchorX, 0f, 1f);
                anyCoordinateChanged |= ImGui.SliderFloat($"{LocalizationPhrase.MainLocalizationPhrase.WindowAnchorLabel} Y", ref _config.windowCoordinates.anchorY, 0f, 1f);
                anyCoordinateChanged |= ImGui.InputText(LocalizationPhrase.MainLocalizationPhrase.WindowNameLabel, ref _config.windowName, 500);
                if (ImGui.Button(LocalizationPhrase.MainLocalizationPhrase.ResetToDefaultsExceptWindowNameLabel))
                {
                    anyCoordinateChanged = true;
                    _config.SetWindowCoordinatesToDefault();
                }
            }
            else
            {
                ImGui.SeparatorText(LocalizationPhrase.MainLocalizationPhrase.OpenVrLabel);
                anyCoordinateChanged |= SmallAdjustmentSlider($"{LocalizationPhrase.MainLocalizationPhrase.VrOffsetLabel} X", ref _config.vrCoordinates.x);
                anyCoordinateChanged |= SmallAdjustmentSlider($"{LocalizationPhrase.MainLocalizationPhrase.VrOffsetLabel} Y", ref _config.vrCoordinates.y);
                anyCoordinateChanged |= ImGui.SliderFloat($"{LocalizationPhrase.MainLocalizationPhrase.VrAnchorLabel} X", ref _config.vrCoordinates.anchorX, 0f, 1f);
                anyCoordinateChanged |= ImGui.SliderFloat($"{LocalizationPhrase.MainLocalizationPhrase.VrAnchorLabel} Y", ref _config.vrCoordinates.anchorY, 0f, 1f);
                anyCoordinateChanged |= ImGui.Checkbox(LocalizationPhrase.MainLocalizationPhrase.UseRightEyeLabel, ref _config.vrUseRightEye);
                if (ImGui.Button(LocalizationPhrase.MainLocalizationPhrase.ResetToDefaultsLabel))
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
                ImGui.SeparatorText(LocalizationPhrase.MainLocalizationPhrase.DebugLabel);
                ImGui.Columns(2);
                
                ImGui.SeparatorText(LocalizationPhrase.MainLocalizationPhrase.InterpretedDataLabel);
                InterpretedDebug(interpreted);
                
                ImGui.SeparatorText(LocalizationPhrase.MainLocalizationPhrase.DataLabel);
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
            
            ImGui.SeparatorText(LocalizationPhrase.MainLocalizationPhrase.WebsocketsSupportLabel);
            var websocketChanged = ImGui.Checkbox(string.Format(LocalizationPhrase.MainLocalizationPhrase.ExposeWebsocketsOnPortLabel, IWebsocketActions.WebsocketDefaultPort), ref _config.useWebsockets);
            if (websocketChanged)
            {
                _uiActions.ConfigWebsocketsUpdated();
            }
            anyChanged |= websocketChanged;
        });
        _scrollManager.MakeTab(LocalizationPhrase.MainLocalizationPhrase.DebugLabel, () =>
        {
            ImGui.BeginTabBar("##tabs_debug");
            _scrollManager.MakeTab(LocalizationPhrase.MainLocalizationPhrase.LightsLabel, () =>
            {
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
                
                ImGui.SeparatorText(LocalizationPhrase.MainLocalizationPhrase.InterpretedDataLabel);
                InterpretedDebug(interpreted);
            });
            _scrollManager.MakeTab(LocalizationPhrase.MainLocalizationPhrase.CameraLabel, () =>
            {
                var extractedData = _uiActions.ExtractedData();
                if (extractedData.IsValid())
                {
                    var decodedData = _uiActions.Data();
                    if (decodedData.Version < 1_001_000)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 0, 1, 1));
                        ImGui.Text(string.Format(LocalizationPhrase.MainLocalizationPhrase.MsgShaderDoesNotSupportCameraPosition, decodedData.AsSemverString(), "1.1.0"));
                        ImGui.PopStyleColor();
                        ImGui.NewLine();
                    }

                    ImGui.Text($"{LocalizationPhrase.MainLocalizationPhrase.CameraPositionLabel} X: {decodedData.CameraPosition.X}");
                    ImGui.Text($"{LocalizationPhrase.MainLocalizationPhrase.CameraPositionLabel} Y: {decodedData.CameraPosition.Y}");
                    ImGui.Text($"{LocalizationPhrase.MainLocalizationPhrase.CameraPositionLabel} Z: {decodedData.CameraPosition.Z}");
                    ImGui.NewLine();
                    ImGui.Text($"{LocalizationPhrase.MainLocalizationPhrase.CameraRotationLabel} X: {decodedData.CameraRotation.X}");
                    ImGui.Text($"{LocalizationPhrase.MainLocalizationPhrase.CameraRotationLabel} Y: {decodedData.CameraRotation.Y}");
                    ImGui.Text($"{LocalizationPhrase.MainLocalizationPhrase.CameraRotationLabel} Z: {decodedData.CameraRotation.Z}");
                    ImGui.NewLine();
                    ImGui.SeparatorText(LocalizationPhrase.MainLocalizationPhrase.SteamVrPlayspaceLabel);
                    ImGui.Text($"{LocalizationPhrase.MainLocalizationPhrase.EstimatedScaleLabel}: {_uiActions.VirtualScale()}");
                }
            });
            _scrollManager.MakeTab(LocalizationPhrase.MainLocalizationPhrase.DataLabel, () =>
            {
                var extractedData = _uiActions.ExtractedData();
                if (extractedData.IsValid())
                {
                    var decodedData = _uiActions.Data();
                    ImGui.Text($"{LocalizationPhrase.MainLocalizationPhrase.ShaderVersionLabel}: {decodedData.AsSemverString()}");
                    DrawSieve(32);
                    ImGui.NewLine();
                }
            });
            ImGui.EndTabBar();
        });

        _scrollManager.MakeTab(VERSION.miniVersion, () =>
        {
            ImGui.Text($"{LocalizationPhrase.MainLocalizationPhrase.SoftwareVersionLabel}: {VERSION.version}");
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
            if (numberOfLines == (int)ShaderV1_1_0.NumberOfLines)
            {
                ImGui.SameLine();
                ImGui.Text("  ->   " + Enum.GetName(typeof(ShaderV1_1_0), (ShaderV1_1_0)row));
            }
        }
        if (!valid) ImGui.PopStyleColor();
    }

    private static void ShowDataWarningIfApplicable(DecodedData data)
    {
        var valid = data.validity == DataValidity.Ok;
        ImGui.PushStyleColor(ImGuiCol.Text, !valid ? new Vector4(1, 0, 0, 1) : new Vector4(0, 1, 1, 1));
        switch (data.validity)
        {
            case DataValidity.NotInitialized:
                ImGui.Text(LocalizationPhrase.MainLocalizationPhrase.MsgDataNotInitialized);
                break;
            case DataValidity.Ok:
                ImGui.Text(LocalizationPhrase.MainLocalizationPhrase.MsgChecksumOk);
                break;
            case DataValidity.InvalidChecksum:
                ImGui.Text(LocalizationPhrase.MainLocalizationPhrase.MsgChecksumInvalid);
                break;
            case DataValidity.UnexpectedVendor:
                ImGui.Text(LocalizationPhrase.MainLocalizationPhrase.MsgChecksumUnexpectedVendor);
                break;
            case DataValidity.UnexpectedMajorVersion:
                ImGui.Text(LocalizationPhrase.MainLocalizationPhrase.MsgChecksumUnexpectedMajorVersion);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        ImGui.PopStyleColor();
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
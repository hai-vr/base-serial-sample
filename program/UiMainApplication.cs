using System.Numerics;
using Hai.PositionSystemToExternalProgram.Configuration;
using Hai.PositionSystemToExternalProgram.Core;
using Hai.PositionSystemToExternalProgram.Processors;
using ImGuiNET;
using Veldrid;
using Veldrid.Sdl2;

namespace Hai.PositionSystemToExternalProgram.Program;

public class UiMainApplication
{
    private const ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoResize;
    private const ImGuiWindowFlags WindowFlagsNoCollapse = WindowFlags | ImGuiWindowFlags.NoCollapse;
    
    private const string OpenSerialLabel = "Open serial";
    private const string CloseSerialLabel = "Close serial";
    private const string AutoUpdateLabel = "Auto-update";
    private const string SubmitLabel = "Submit";

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

    public UiMainApplication(UiActions uiActions, SavedData config)
    {
        _uiActions = uiActions;
        _config = config;
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
        if (ImGui.Button("Refresh"))
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
            if (ImGui.Button($"Connect to device on serial port {(_selectedPortName == "" ? "UNKNOWN" : _selectedPortName)}", new Vector2(ImGui.GetContentRegionAvail().X, 60)))
            {
                _uiActions.ConnectSerial(_selectedPortName);
            }
            ImGui.EndDisabled();
        }
        
        var anyChanged = false;
        ImGui.BeginTabBar("##tabs");
        _scrollManager.MakeTab("Extractor", () =>
        {
            ImGui.SeparatorText("OpenVR");
            var anyCoordinateChanged = false;
            if (!isOpenVrRunning)
            {
                ImGui.Text("OpenVR is not running.");
                ImGui.SeparatorText("Desktop");
                anyCoordinateChanged |= ImGui.SliderInt("Desktop Offset X", ref _config.desktopCoordinates.x, 0, 100);
                anyCoordinateChanged |= ImGui.SliderInt("Desktop Offset Y", ref _config.desktopCoordinates.y, 0, 1000);
                anyCoordinateChanged |= ImGui.SliderFloat("Desktop Anchor X", ref _config.desktopCoordinates.anchorX, 0f, 1f);
                anyCoordinateChanged |= ImGui.SliderFloat("Desktop Anchor Y", ref _config.desktopCoordinates.anchorY, 0f, 1f);
                anyCoordinateChanged |= ImGui.InputText("Window name", ref _config.windowName, 500);
                if (ImGui.Button("Reset to defaults (except Window name)"))
                {
                    anyCoordinateChanged = true;
                    _config.SetDesktopCoordinatesToDefault();
                }
            }
            else
            {
                anyCoordinateChanged |= ImGui.SliderInt("VR Offset X", ref _config.vrCoordinates.x, 0, 100);
                anyCoordinateChanged |= ImGui.SliderInt("VR Offset Y", ref _config.vrCoordinates.y, 0, 1000);
                anyCoordinateChanged |= ImGui.SliderFloat("VR Anchor X", ref _config.vrCoordinates.anchorX, 0f, 1f);
                anyCoordinateChanged |= ImGui.SliderFloat("VR Anchor Y", ref _config.vrCoordinates.anchorY, 0f, 1f);
                anyCoordinateChanged |= ImGui.Checkbox("Use right eye", ref _config.vrUseRightEye);
                if (ImGui.Button("Reset to defaults"))
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
                var bits = _uiActions.Bits();
                var data = _uiActions.Data();
                var valid = data.validity == DataValidity.Ok;

                ImGui.SeparatorText("Debug");
                ImGui.Columns(2);
                if (!valid) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 0, 0, 1));
                switch (data.validity)
                {
                    case DataValidity.Ok:
                        ImGui.Text("Data is OK");
                        break;
                    case DataValidity.InvalidChecksum:
                        ImGui.Text("Checksum is failing");
                        break;
                    case DataValidity.UnexpectedVendor:
                        ImGui.Text("Unexpected vendor");
                        break;
                    case DataValidity.UnexpectedMajorVersion:
                        ImGui.Text("Unexpected major version");
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                if (!valid) ImGui.PopStyleColor();
                
                if (!valid) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.5f, 1));
                var numberOfColumns = 16;
                for (var row = 0; row < ExtractedDataDecoder.GroupLength; row++)
                {
                    ImGui.Text($"#{row:00}  ");
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
                }
                if (!valid) ImGui.PopStyleColor();
                ImGui.NextColumn();

                var textureId = TurnDataIntoTexture(controller, extractedData);

                ImGui.Text($"{extractedData.Iteration}");
                ImGui.Image(textureId, new Vector2(_lastWidth, _lastHeight));
                var coordinates = _uiActions.IsOpenVrRunning() ? _uiActions.VrCoordinates() : _uiActions.DesktopCoordinates();
                
                // FIXME: Not thread safe, these requests need to be enqueued
                ImGui.SliderInt("W", ref coordinates.requestedWidth, 1, 1024);
                ImGui.SliderInt("H", ref coordinates.requestedHeight, 1, 1024);
                if (ImGui.Button("Reset to defaults##size"))
                {
                    coordinates.requestedWidth = 32 * 4;
                    coordinates.requestedHeight = 64 * 4;
                }
            }
            
            ImGui.Columns(1);
        });
        _scrollManager.MakeTab("Hardware", () =>
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

        ImGui.EndTabBar();
        
        if (anyChanged)
        {
            _config.SaveConfig();
        }
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
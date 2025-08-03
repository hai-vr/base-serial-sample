using System.Numerics;
using Hai.HView.Data;
using Hai.PositionSystemToExternalProgram.Core;
using ImGuiNET;
using Veldrid;
using Veldrid.Sdl2;

namespace Hai.BaseSerial.SampleProgram;

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
            if (!isOpenVrRunning)
            {
                ImGui.Text("OpenVR is not running.");
                ImGui.SeparatorText("Desktop");
                anyChanged |= ImGui.SliderInt("Window Offset X", ref _config.offsetX, 0, 100);
                anyChanged |= ImGui.SliderInt("Window Offset Y", ref _config.offsetY, 0, 1000);
                anyChanged |= ImGui.InputText("Window name", ref _config.windowName, 500);
            }
            else
            {
                anyChanged |= ImGui.SliderInt("Image Offset X", ref _config.vrOffsetX, 0, 100);
                anyChanged |= ImGui.SliderInt("Image Offset Y", ref _config.vrOffsetY, 0, 1000);
                anyChanged |= ImGui.Checkbox("Use right eye", ref _config.vrUseRightEye);
            }
            
            var extractedData = _uiActions.ExtractedData();
            var bits = _uiActions.Bits();

            ImGui.SeparatorText("Debug");
            ImGui.Columns(2);
            for (var row = 0; row < 32; row++)
            {
                var numberOfColumns = 32;
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
            ImGui.NextColumn();

            if (extractedData.IsValid())
            {
                var textureId = TurnDataIntoTexture(controller, extractedData);

                ImGui.Text($"{extractedData.Iteration}");
                ImGui.Image(textureId, new Vector2(_lastWidth, _lastHeight));
                var location = _uiActions.Location();
                ImGui.SliderInt("X", ref location.coordinates.x, 0, 2048);
                ImGui.SliderInt("Y", ref location.coordinates.y, 0, 2048);
                ImGui.SliderInt("W", ref location.coordinates.requestedWidth, 1, 1024);
                ImGui.SliderInt("H", ref location.coordinates.requestedHeight, 1, 1024);
                ImGui.SliderFloat("aX", ref location.coordinates.anchorX, 0, 1f);
                ImGui.SliderFloat("aY", ref location.coordinates.anchorY, 0, 1f);
                ImGui.Checkbox("Use right eye", ref location.useRightEye);
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
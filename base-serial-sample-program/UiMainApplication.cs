using System.Numerics;
using extractor_openvr;
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
    private string[] _portNames;
    private int _selectedPortIndex;
    private string _selectedPortName = "";
    
    private int _lastExtractedDataIteration;
    private Texture _cachedTexture;
    private int _lastWidth;
    private int _lastHeight;
    private IntPtr _textureId;

    public UiMainApplication(UiActions uiActions)
    {
        _uiActions = uiActions;
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
    }

    private void DrawMain(CustomImGuiController controller, Sdl2Window window)
    {
        var rawData = _uiActions.ExposeRawData();
        var isSerialOpen = _uiActions.IsSerialOpen();
        var isOpenVrRunning = _uiActions.IsOpenVrRunning();

        if (!isOpenVrRunning)
        {
            ImGui.Text("OpenVR is not running.");
        }
        
        ImGui.BeginDisabled(isSerialOpen || _selectedPortName == "");
        if (ImGui.Button(OpenSerialLabel))
        {
            _uiActions.ConnectSerial(_selectedPortName);
        }
        ImGui.EndDisabled();
        
        ImGui.SameLine();
        
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
        ImGui.EndDisabled();
        
        ImGui.BeginDisabled(!isSerialOpen);
        if (ImGui.Button(CloseSerialLabel))
        {
            _uiActions.DisconnectSerial();
        }
        ImGui.EndDisabled();
        
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
        
        var extractedData = _uiActions.ExtractedData();
        if (extractedData.IsValid())
        {
            var textureId = TurnDataIntoTexture(controller, extractedData);

            ImGui.Text($"{extractedData.Iteration}");
            ImGui.Image(textureId, new Vector2(_lastHeight, _lastHeight));
            var location = _uiActions.Location();
            ImGui.SliderInt("X", ref location.X, 0, 2048);
            ImGui.SliderInt("Y", ref location.Y, 0, 2048);
            ImGui.SliderInt("W", ref location.W, 0, 1024);
            ImGui.SliderInt("H", ref location.H, 0, 1024);
            ImGui.Checkbox("Use right eye", ref location.useRightEye);
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
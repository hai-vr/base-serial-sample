using System.Numerics;
using ImGuiNET;
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

        DrawMain();
        
        ImGui.End();
    }

    private void DrawMain()
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
    }

    private void UpdatePortNames()
    {
        _portNames = _uiActions.FetchPortNames();
    }
}
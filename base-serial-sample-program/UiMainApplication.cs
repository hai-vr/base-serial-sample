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
    
    public UiMainApplication(UiActions uiActions)
    {
        _uiActions = uiActions;
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
        
        ImGui.BeginDisabled(isSerialOpen);
        if (ImGui.Button(OpenSerialLabel))
        {
            _uiActions.ConnectSerial();
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
}
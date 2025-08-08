using System.Numerics;
using Hai.PositionSystemToExternalProgram.Configuration;
using ImGuiNET;

namespace Hai.PositionSystemToExternalProgram.Program;

public class UiRoboticsTab
{
    private const string AutoUpdateLabel = "Auto-update";
    private const string CommandLabel = "Command";
    private const string HardLimits = "Hard limits";
    private const string LimitLateralMovementAtTheBottom = "Limit movement at the bottom";
    private const string OffsetPitchAngleLabel = "Offset pitch angle";
    private const string OffsetsLabel = "Offsets";
    private const string ResetLabel = "Reset";
    private const string RoboticsConfigurationLabel = "Robotics configuration";
    private const string SafetySettingsLabel = "Safety settings";
    private const string SubmitLabel = "Submit";
    private const string VirtualScaleLabel = "Virtual scale";
    private const string RotateMachineLabel = "Rotate machine";
    
    private const string MsgHardLimitsHelper = "Hard limits are applied after PID controllers. PID controllers will remain unaware that a limit has been applied.";
    private const string MsgNotDefaultWarning = "This value is not the default. If you think something is strange with the machine behaviour, press the Reset button.";
    private const string MsgNotLimitedWarning = "The movement of the machine is not limited. If you are using a machine that is capable of moving laterally to the main axis, this can pose a risk.";
    private const string MsgRotateMachineHelper = "This will rotate the entire machine, so that the movement in the virtual space in one direction results in a different direction in the physical space.";
    private const string MsgVirtualScaleHelper = "A value greater than 1 means it takes more travel in the virtual space to move the same distance in the physical space.";
    private const string ResetVirtualScaleLabel = "Reset virtual scale";

    private readonly UiActions _uiActions;
    private readonly SavedData _config;

    public UiRoboticsTab(UiActions uiActions, SavedData config)
    {
        _uiActions = uiActions;
        _config = config;
    }
    
    public bool RoboticsTab()
    {
        var rawData = _uiActions.ExposeRawData();
        var isSerialOpen = _uiActions.IsSerialOpen();
        
        var anyRoboticsConfigurationChanged = false;
            
        ImGui.SeparatorText(VirtualScaleLabel);
        anyRoboticsConfigurationChanged |= ImGui.SliderFloat($"{VirtualScaleLabel} (0 to 1)", ref _config.roboticsVirtualScale, 0.01f, 1f);
        anyRoboticsConfigurationChanged |= ImGui.SliderFloat($"{VirtualScaleLabel} (1 to 2)", ref _config.roboticsVirtualScale, 1f, 2f);
        anyRoboticsConfigurationChanged |= ImGui.SliderFloat($"{VirtualScaleLabel} (0 to 5)", ref _config.roboticsVirtualScale, 0.01f, 5f);
        if (ImGui.Button(ResetVirtualScaleLabel))
        {
            _config.roboticsVirtualScale = 1f;
            anyRoboticsConfigurationChanged = true;
        }
        ImGui.TextWrapped(MsgVirtualScaleHelper);
        if (_config.roboticsVirtualScale != 1f) ResetButtonWarning(MsgNotDefaultWarning);
            
        ImGui.NewLine();
        ImGui.SeparatorText(HardLimits);
        anyRoboticsConfigurationChanged |= ImGui.SliderFloat("Limit maximum height (0 to 1)", ref _config.roboticsTopmostHardLimit, 0.01f, 1f);
        if (ImGui.Button($"{ResetLabel}##reset_roboticsTopmostLimit"))
        {
            _config.roboticsTopmostHardLimit = 1f;
            anyRoboticsConfigurationChanged = true;
        }
        ImGui.TextWrapped(MsgHardLimitsHelper);
            
        ImGui.NewLine();
        ImGui.SeparatorText(RoboticsConfigurationLabel);
        ImGui.BeginDisabled(); // TEMP
        anyRoboticsConfigurationChanged |= ImGui.Checkbox("Auto-adjust root (Root PID controller)", ref _config.roboticsUsePidRoot);
        ImGui.EndDisabled(); // TEMP
        anyRoboticsConfigurationChanged |= ImGui.Checkbox("Dampen target (Target PID controller)", ref _config.roboticsUsePidTarget);

        {
            ImGui.NewLine();
            ImGui.SeparatorText(RotateMachineLabel);
            anyRoboticsConfigurationChanged |= ImGui.SliderFloat("Rotation pitch", ref _config.roboticsRotateSystemAngleDegPitch, -180, 180);
            if (ImGui.Button($"{ResetLabel}##reset_roboticsRotateSystemAngleDegPitch"))
            {
                _config.roboticsRotateSystemAngleDegPitch = 0f;
                anyRoboticsConfigurationChanged = true;
            }
            ImGui.SameLine();
            if (ImGui.Button("90##90_roboticsRotateSystemAngleDegPitch"))
            {
                _config.roboticsRotateSystemAngleDegPitch = 90f;
                anyRoboticsConfigurationChanged = true;
            }
            ImGui.SameLine();
            if (ImGui.Button("-90##neg90_roboticsRotateSystemAngleDegPitch"))
            {
                _config.roboticsRotateSystemAngleDegPitch = -90f;
                anyRoboticsConfigurationChanged = true;
            }
            ImGui.SameLine();
            if (ImGui.Button("180##neg180_roboticsRotateSystemAngleDegPitch"))
            {
                _config.roboticsRotateSystemAngleDegPitch = 180;
                anyRoboticsConfigurationChanged = true;
            }
            ImGui.TextWrapped(MsgRotateMachineHelper);
            if (_config.roboticsRotateSystemAngleDegPitch != 0f) ResetButtonWarning(MsgNotDefaultWarning);
        }
            
        ImGui.NewLine();
        ImGui.SeparatorText(OffsetsLabel);
        anyRoboticsConfigurationChanged |= ImGui.SliderFloat(OffsetPitchAngleLabel, ref _config.roboticsOffsetAngleDegR2, -45, 45);
        if (ImGui.Button($"{ResetLabel}##reset_roboticsOffsetAngleDegR2"))
        {
            _config.roboticsOffsetAngleDegR2 = 0f;
            anyRoboticsConfigurationChanged = true;
        }
        if (_config.roboticsOffsetAngleDegR2 != 0f) ResetButtonWarning(MsgNotDefaultWarning);
            
        ImGui.NewLine();
        ImGui.SeparatorText(SafetySettingsLabel);
        anyRoboticsConfigurationChanged |= ImGui.Checkbox(LimitLateralMovementAtTheBottom, ref _config.roboticsSafetyUsePolarMode);
        if (!_config.roboticsSafetyUsePolarMode) ResetButtonWarning(MsgNotLimitedWarning);

        if (anyRoboticsConfigurationChanged)
        {
            _uiActions.ConfigRoboticsUpdated();
        }
            
        ImGui.NewLine();
        ImGui.SeparatorText(CommandLabel);
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
            
        return anyRoboticsConfigurationChanged;
    }

    private static void ResetButtonWarning(string message)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 1, 0, 1));
        ImGui.TextWrapped(message);
        ImGui.PopStyleColor();
    }
}
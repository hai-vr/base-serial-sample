using System.Numerics;
using Hai.PositionSystemToExternalProgram.Configuration;
using ImGuiNET;

namespace Hai.PositionSystemToExternalProgram.ImGuiProgram;

public class UiRoboticsTab
{
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
            
        ImGui.SeparatorText(LocalizationPhrase.RoboticsLocalizationPhrase.VirtualScaleLabel);
        anyRoboticsConfigurationChanged |= ImGui.SliderFloat($"{LocalizationPhrase.RoboticsLocalizationPhrase.VirtualScaleLabel} (0 to 1)", ref _config.roboticsVirtualScale, 0.01f, 1f);
        anyRoboticsConfigurationChanged |= ImGui.SliderFloat($"{LocalizationPhrase.RoboticsLocalizationPhrase.VirtualScaleLabel} (1 to 2)", ref _config.roboticsVirtualScale, 1f, 2f);
        anyRoboticsConfigurationChanged |= ImGui.SliderFloat($"{LocalizationPhrase.RoboticsLocalizationPhrase.VirtualScaleLabel} (0 to 5)", ref _config.roboticsVirtualScale, 0.01f, 5f);
        if (ImGui.Button(LocalizationPhrase.RoboticsLocalizationPhrase.ResetVirtualScaleLabel))
        {
            _config.roboticsVirtualScale = 1f;
            anyRoboticsConfigurationChanged = true;
        }
        ImGui.TextWrapped(LocalizationPhrase.RoboticsLocalizationPhrase.MsgVirtualScaleHelper);
        if (_config.roboticsVirtualScale != 1f) ResetButtonWarning(LocalizationPhrase.RoboticsLocalizationPhrase.MsgNotDefaultWarning);
            
        ImGui.NewLine();
        ImGui.SeparatorText(LocalizationPhrase.RoboticsLocalizationPhrase.HardLimits);
        anyRoboticsConfigurationChanged |= ImGui.SliderFloat($"{LocalizationPhrase.RoboticsLocalizationPhrase.LimitMaximumHeightLabel} (0 to 1)", ref _config.roboticsTopmostHardLimit, 0.01f, 1f);
        anyRoboticsConfigurationChanged |= ImGui.Checkbox(LocalizationPhrase.RoboticsLocalizationPhrase.CompensateVirtualScaleLabel, ref _config.roboticsCompensateVirtualScaleHardLimit);
        if (ImGui.Button($"{LocalizationPhrase.RoboticsLocalizationPhrase.ResetLabel}##reset_roboticsTopmostLimit"))
        {
            _config.roboticsTopmostHardLimit = 1f;
            _config.roboticsCompensateVirtualScaleHardLimit = true;
            anyRoboticsConfigurationChanged = true;
        }
        ImGui.TextWrapped(LocalizationPhrase.RoboticsLocalizationPhrase.MsgHardLimitsHelper);
        if (_config.roboticsTopmostHardLimit != 1f || !_config.roboticsCompensateVirtualScaleHardLimit) ResetButtonWarning(LocalizationPhrase.RoboticsLocalizationPhrase.MsgNotDefaultWarning);
            
        ImGui.NewLine();
        ImGui.SeparatorText(LocalizationPhrase.RoboticsLocalizationPhrase.RoboticsConfigurationLabel);
        ImGui.BeginDisabled(); // TEMP
        anyRoboticsConfigurationChanged |= ImGui.Checkbox(LocalizationPhrase.RoboticsLocalizationPhrase.AutoAdjustRootLabel, ref _config.roboticsUsePidRoot);
        ImGui.EndDisabled(); // TEMP
        anyRoboticsConfigurationChanged |= ImGui.Checkbox(LocalizationPhrase.RoboticsLocalizationPhrase.DampenTargetLabel, ref _config.roboticsUsePidTarget);

        {
        }
            
        ImGui.NewLine();
        ImGui.SeparatorText(LocalizationPhrase.RoboticsLocalizationPhrase.OffsetsLabel);
        anyRoboticsConfigurationChanged |= ImGui.SliderFloat(LocalizationPhrase.RoboticsLocalizationPhrase.OffsetPitchAngleLabel, ref _config.roboticsOffsetAngleDegR2, -45, 45);
        if (ImGui.Button($"{LocalizationPhrase.RoboticsLocalizationPhrase.ResetLabel}##reset_roboticsOffsetAngleDegR2"))
        {
            _config.roboticsOffsetAngleDegR2 = 0f;
            anyRoboticsConfigurationChanged = true;
        }
        if (_config.roboticsOffsetAngleDegR2 != 0f) ResetButtonWarning(LocalizationPhrase.RoboticsLocalizationPhrase.MsgNotDefaultWarning);
            
        ImGui.NewLine();
        ImGui.SeparatorText(LocalizationPhrase.RoboticsLocalizationPhrase.SafetySettingsLabel);
        anyRoboticsConfigurationChanged |= ImGui.Checkbox(LocalizationPhrase.RoboticsLocalizationPhrase.LimitLateralMovementAtTheBottom, ref _config.roboticsSafetyUsePolarMode);
        if (!_config.roboticsSafetyUsePolarMode) ResetButtonWarning(LocalizationPhrase.RoboticsLocalizationPhrase.MsgNotLimitedWarning);

        if (_config.roboticsRotateSystemAngleDegPitch != 0)
        {
            ImGui.NewLine();
            anyRoboticsConfigurationChanged |= RoboticsAdvancedTab();
        }

        if (anyRoboticsConfigurationChanged)
        {
            _uiActions.ConfigRoboticsUpdated();
        }
            
        ImGui.NewLine();
        ImGui.SeparatorText(LocalizationPhrase.RoboticsLocalizationPhrase.CommandLabel);
        ImGui.SliderInt("L0", ref rawData.L0, 0, 9999);
        ImGui.SliderInt("L1", ref rawData.L1, 0, 9999);
        ImGui.SliderInt("L2", ref rawData.L2, 0, 9999);
        ImGui.SliderInt("R0", ref rawData.R0, 0, 9999);
        ImGui.SliderInt("R1", ref rawData.R1, 0, 9999);
        ImGui.SliderInt("R2", ref rawData.R2, 0, 9999);
        ImGui.Checkbox(LocalizationPhrase.RoboticsLocalizationPhrase.AutoUpdateLabel, ref rawData.autoUpdate);
            
        ImGui.BeginDisabled(!isSerialOpen || rawData.autoUpdate);
        if (ImGui.Button(LocalizationPhrase.RoboticsLocalizationPhrase.SubmitLabel))
        {
            _uiActions.Submit();
        }
        ImGui.EndDisabled();
            
        return anyRoboticsConfigurationChanged;
    }

    public bool RoboticsAdvancedTab()
    {
        var anyRoboticsConfigurationChanged = false;
        
        ImGui.SeparatorText(LocalizationPhrase.RoboticsLocalizationPhrase.RotateMachineLabel);
        anyRoboticsConfigurationChanged |= ImGui.SliderFloat($"{LocalizationPhrase.RoboticsLocalizationPhrase.RotationPitchLabel} (0 to 90)", ref _config.roboticsRotateSystemAngleDegPitch, 0, 90);
        anyRoboticsConfigurationChanged |= ImGui.SliderFloat($"{LocalizationPhrase.RoboticsLocalizationPhrase.RotationPitchLabel} (-90 to 90)", ref _config.roboticsRotateSystemAngleDegPitch, -90, 90);
        anyRoboticsConfigurationChanged |= ImGui.SliderFloat($"{LocalizationPhrase.RoboticsLocalizationPhrase.RotationPitchLabel} (-180 to 180)", ref _config.roboticsRotateSystemAngleDegPitch, -180, 180);
        if (ImGui.Button($"{LocalizationPhrase.RoboticsLocalizationPhrase.ResetLabel}##reset_roboticsRotateSystemAngleDegPitch"))
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
        ImGui.TextWrapped(LocalizationPhrase.RoboticsLocalizationPhrase.MsgRotateMachineHelper);
        if (_config.roboticsRotateSystemAngleDegPitch != 0f) ResetButtonWarning(LocalizationPhrase.RoboticsLocalizationPhrase.MsgNotDefaultWarning);

        return anyRoboticsConfigurationChanged;
    }

    private static void ResetButtonWarning(string message)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 1, 0, 1));
        ImGui.TextWrapped(message);
        ImGui.PopStyleColor();
    }
}
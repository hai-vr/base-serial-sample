namespace Hai.PositionSystemToExternalProgram.ImGuiProgram;

public class LocalizationPhrase
{
    public static string LocalizeOrElse(Type localizationGroup, string localizationKey, string englishPhrase)
    {
        return Localization.LocalizeOrElse(localizationGroup, localizationKey, englishPhrase);
    }
    
    public class MainLocalizationPhrase
    {
        private static string LocalizeOrElse(string localizationKey, string englishPhrase) => LocalizationPhrase.LocalizeOrElse(typeof(MainLocalizationPhrase), localizationKey, englishPhrase);

        public static string Separator => "-----------------------------";
        public static string CameraLabel => LocalizeOrElse(nameof(CameraLabel), "Camera");
        public static string CameraPositionLabel => LocalizeOrElse(nameof(CameraPositionLabel), "Camera Position");
        public static string CameraRotationLabel => LocalizeOrElse(nameof(CameraRotationLabel), "Camera Rotation");
        public static string CloseSerialLabel => LocalizeOrElse(nameof(CloseSerialLabel), "Close serial");
        public static string DataCalibrationLabel => LocalizeOrElse(nameof(DataCalibrationLabel), "Data calibration");
        public static string DataLabel => LocalizeOrElse(nameof(DataLabel), "Data");
        public static string DebugLabel => LocalizeOrElse(nameof(DebugLabel), "Debug");
        public static string EstimatedScaleLabel => LocalizeOrElse(nameof(EstimatedScaleLabel), "Estimated scale");
        public static string ExposeWebsocketsOnPortLabel => LocalizeOrElse(nameof(ExposeWebsocketsOnPortLabel), "Expose WebSockets on port {0}");
        public static string ExtractorPreferenceLabel => LocalizeOrElse(nameof(ExtractorPreferenceLabel), "Extractor Preference");
        public static string InterpretedDataLabel => LocalizeOrElse(nameof(InterpretedDataLabel), "Interpreted data");
        public static string LightsLabel => LocalizeOrElse(nameof(LightsLabel), "Lights");
        public static string ModeLabel => LocalizeOrElse(nameof(ModeLabel), "Mode");
        public static string OpenVrLabel => LocalizeOrElse(nameof(OpenVrLabel), "OpenVR");
        public static string RefreshLabel => LocalizeOrElse(nameof(RefreshLabel), "Refresh");
        public static string ResetToDefaultsExceptWindowNameLabel => LocalizeOrElse(nameof(ResetToDefaultsExceptWindowNameLabel), "Reset to defaults (except Window name)");
        public static string ResetToDefaultsLabel => LocalizeOrElse(nameof(ResetToDefaultsLabel), "Reset to defaults");
        public static string RoboticsAdvancedLabel => LocalizeOrElse(nameof(RoboticsAdvancedLabel), "Robotics (Advanced)");
        public static string RoboticsLabel => LocalizeOrElse(nameof(RoboticsLabel), "Robotics");
        public static string ShaderVersionLabel => LocalizeOrElse(nameof(ShaderVersionLabel), "Shader version");
        public static string SoftwareVersionLabel => LocalizeOrElse(nameof(SoftwareVersionLabel), "Software version");
        public static string SpoutLabel => LocalizeOrElse(nameof(SpoutLabel), "Spout");
        public static string SteamVrPlayspaceLabel => LocalizeOrElse(nameof(SteamVrPlayspaceLabel), "SteamVR playspace");
        public static string UseRightEyeLabel => LocalizeOrElse(nameof(UseRightEyeLabel), "Use right eye");
        public static string VrAnchorLabel => LocalizeOrElse(nameof(VrAnchorLabel), "VR Anchor");
        public static string VrOffsetLabel => LocalizeOrElse(nameof(VrOffsetLabel), "VR Offset");
        public static string WebsocketsSupportLabel => LocalizeOrElse(nameof(WebsocketsSupportLabel), "WebSockets support");
        public static string WindowAnchorLabel => LocalizeOrElse(nameof(WindowAnchorLabel), "Window Anchor");
        public static string WindowLabel => LocalizeOrElse(nameof(WindowLabel), "Window");
        public static string WindowNameLabel => LocalizeOrElse(nameof(WindowNameLabel), "Window name");
        public static string WindowOffsetLabel => LocalizeOrElse(nameof(WindowOffsetLabel), "Window Offset");
        
        public static string MsgChecksumInvalid => LocalizeOrElse(nameof(MsgChecksumInvalid), "Checksum is failing");
        public static string MsgChecksumOk => LocalizeOrElse(nameof(MsgChecksumOk), "Data is OK");
        public static string MsgChecksumUnexpectedMajorVersion => LocalizeOrElse(nameof(MsgChecksumUnexpectedMajorVersion), "Unexpected major version");
        public static string MsgChecksumUnexpectedVendor => LocalizeOrElse(nameof(MsgChecksumUnexpectedVendor), "Unexpected vendor");
        public static string MsgConnectToDeviceOnSerialPort => LocalizeOrElse(nameof(MsgConnectToDeviceOnSerialPort), "Connect to device on serial port {0}");
        public static string MsgDataNotInitialized => LocalizeOrElse(nameof(MsgDataNotInitialized), "Data not initialized");
        public static string MsgOpenVrUnavailable => LocalizeOrElse(nameof(MsgOpenVrUnavailable), "OpenVR is not running.");
        public static string MsgShaderDoesNotSupportCameraPosition => LocalizeOrElse(nameof(MsgShaderDoesNotSupportCameraPosition), "Detected shader version is {0}, which does not support camera position (minimum required: {1})");
        public static string MsgSpoutUnavailable => LocalizeOrElse(nameof(MsgSpoutUnavailable), "Spout is not yet available in this version of the software.");
    }

    public class RoboticsLocalizationPhrase
    {
        private static string LocalizeOrElse(string localizationKey, string englishPhrase) => LocalizationPhrase.LocalizeOrElse(typeof(RoboticsLocalizationPhrase), localizationKey, englishPhrase);
        
        public static string Separator => "-----------------------------";
        public static string AutoAdjustRootLabel => LocalizeOrElse(nameof(AutoAdjustRootLabel), "Auto-adjust root (Root PID controller)");
        public static string AutoUpdateLabel => LocalizeOrElse(nameof(AutoUpdateLabel), "Auto-update");
        public static string CommandLabel => LocalizeOrElse(nameof(CommandLabel), "Command");
        public static string CompensateVirtualScaleLabel => LocalizeOrElse(nameof(CompensateVirtualScaleLabel), "Compensate virtual scale");
        public static string DampenTargetLabel => LocalizeOrElse(nameof(DampenTargetLabel), "Dampen target (Target PID controller)");
        public static string HardLimits => LocalizeOrElse(nameof(HardLimits), "Hard limits");
        public static string LimitLateralMovementAtTheBottom => LocalizeOrElse(nameof(LimitLateralMovementAtTheBottom), "Limit movement at the bottom");
        public static string LimitMaximumHeightLabel => LocalizeOrElse(nameof(LimitMaximumHeightLabel), "Limit maximum height");
        public static string OffsetPitchAngleLabel => LocalizeOrElse(nameof(OffsetPitchAngleLabel), "Offset pitch angle");
        public static string OffsetsLabel => LocalizeOrElse(nameof(OffsetsLabel), "Offsets");
        public static string ResetLabel => LocalizeOrElse(nameof(ResetLabel), "Reset");
        public static string ResetVirtualScaleLabel => LocalizeOrElse(nameof(ResetVirtualScaleLabel), "Reset virtual scale");
        public static string RoboticsConfigurationLabel => LocalizeOrElse(nameof(RoboticsConfigurationLabel), "Robotics configuration");
        public static string RotateMachineLabel => LocalizeOrElse(nameof(RotateMachineLabel), "Rotate machine");
        public static string RotationPitchLabel => LocalizeOrElse(nameof(RotationPitchLabel), "Rotation pitch");
        public static string SafetySettingsLabel => LocalizeOrElse(nameof(SafetySettingsLabel), "Safety settings");
        public static string SubmitLabel => LocalizeOrElse(nameof(SubmitLabel), "Submit");
        public static string VirtualScaleLabel => LocalizeOrElse(nameof(VirtualScaleLabel), "Virtual scale");
        
        public static string MsgHardLimitsHelper => LocalizeOrElse(nameof(MsgHardLimitsHelper), "Hard limits are applied after PID controllers. PID controllers will remain unaware that a limit has been applied.");
        public static string MsgNotDefaultWarning => LocalizeOrElse(nameof(MsgNotDefaultWarning), "This value is not the default. If you think something is strange with the machine behaviour, press the Reset button.");
        public static string MsgNotLimitedWarning => LocalizeOrElse(nameof(MsgNotLimitedWarning), "The movement of the machine is not limited. If you are using a machine that is capable of moving laterally to the main axis, this can pose a risk.");
        public static string MsgRotateMachineHelper => LocalizeOrElse(nameof(MsgRotateMachineHelper), "This will rotate the entire machine, so that the movement in the virtual space in one direction results in a different direction in the physical space.");
        public static string MsgVirtualScaleHelper => LocalizeOrElse(nameof(MsgVirtualScaleHelper), "A value greater than 1 means it takes more travel in the virtual space to move the same distance in the physical space.");
    }
}
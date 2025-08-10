namespace Hai.PositionSystemToExternalProgram.ImGuiProgram;

public class LocalizationPhrase
{
    public static string LocalizeOrElse(string phrase)
    {
        return phrase;
    }
    
    public class MainLocalizationPhrase
    {
        public const string CameraLabel = "Camera";
        public const string CameraPositionLabel = "Camera Position";
        public const string CameraRotationLabel = "Camera Rotation";
        public const string CloseSerialLabel = "Close serial";
        public const string DataCalibrationLabel = "Data calibration";
        public const string DataLabel = "Data";
        public const string DebugLabel = "Debug";
        public const string EstimatedScaleLabel = "Estimated scale";
        public const string ExposeWebsocketsOnPortLabel = "Expose WebSockets on port {0}";
        public const string ExtractorPreferenceLabel = "Extractor Preference";
        public const string InterpretedDataLabel = "Interpreted data";
        public const string LightsLabel = "Lights";
        public const string ModeLabel = "Mode";
        public const string OpenVrLabel = "OpenVR";
        public const string RefreshLabel = "Refresh";
        public const string ResetToDefaultsExceptWindowNameLabel = "Reset to defaults (except Window name)";
        public const string ResetToDefaultsLabel = "Reset to defaults";
        public const string RoboticsAdvancedLabel = "Robotics (Advanced)";
        public const string RoboticsLabel = "Robotics";
        public const string ShaderVersionLabel = "Shader version";
        public const string SoftwareVersionLabel = "Software version";
        public const string SpoutLabel = "Spout";
        public const string SteamVrPlayspaceLabel = "SteamVR playspace";
        public const string UseRightEyeLabel = "Use right eye";
        public const string VrAnchorLabel = "VR Anchor";
        public const string VrOffsetLabel = "VR Offset";
        public const string WebsocketsSupportLabel = "WebSockets support";
        public const string WindowAnchorLabel = "Window Anchor";
        public const string WindowLabel = "Window";
        public const string WindowNameLabel = "Window name";
        public const string WindowOffsetLabel = "Window Offset";
        
        public const string MsgChecksumInvalid = "Checksum is failing";
        public const string MsgChecksumOk = "Data is OK";
        public const string MsgChecksumUnexpectedMajorVersion = "Unexpected major version";
        public const string MsgChecksumUnexpectedVendor = "Unexpected vendor";
        public const string MsgConnectToDeviceOnSerialPort = "Connect to device on serial port {0}";
        public const string MsgDataNotInitialized = "Data not initialized";
        public const string MsgOpenVrUnavailable = "OpenVR is not running.";
        public const string MsgShaderDoesNotSupportCameraPosition = "Detected shader version is {0}, which does not support camera position (minimum required: {1})";
        public const string MsgSpoutUnavailable = "Spout is not yet available in this version of the software.";
    }

    public class RoboticsLocalizationPhrase
    {
        public const string AutoAdjustRootLabel = "Auto-adjust root (Root PID controller)";
        public const string AutoUpdateLabel = "Auto-update";
        public const string CommandLabel = "Command";
        public const string CompensateVirtualScaleLabel = "Compensate virtual scale";
        public const string DampenTargetLabel = "Dampen target (Target PID controller)";
        public const string HardLimits = "Hard limits";
        public const string LimitLateralMovementAtTheBottom = "Limit movement at the bottom";
        public const string LimitMaximumHeightLabel = "Limit maximum height";
        public const string OffsetPitchAngleLabel = "Offset pitch angle";
        public const string OffsetsLabel = "Offsets";
        public const string ResetLabel = "Reset";
        public const string ResetVirtualScaleLabel = "Reset virtual scale";
        public const string RoboticsConfigurationLabel = "Robotics configuration";
        public const string RotateMachineLabel = "Rotate machine";
        public const string RotationPitchLabel = "Rotation pitch";
        public const string SafetySettingsLabel = "Safety settings";
        public const string SubmitLabel = "Submit";
        public const string VirtualScaleLabel = "Virtual scale";
        
        public const string MsgHardLimitsHelper = "Hard limits are applied after PID controllers. PID controllers will remain unaware that a limit has been applied.";
        public const string MsgNotDefaultWarning = "This value is not the default. If you think something is strange with the machine behaviour, press the Reset button.";
        public const string MsgNotLimitedWarning = "The movement of the machine is not limited. If you are using a machine that is capable of moving laterally to the main axis, this can pose a risk.";
        public const string MsgRotateMachineHelper = "This will rotate the entire machine, so that the movement in the virtual space in one direction results in a different direction in the physical space.";
        public const string MsgVirtualScaleHelper = "A value greater than 1 means it takes more travel in the virtual space to move the same distance in the physical space.";
    }
}
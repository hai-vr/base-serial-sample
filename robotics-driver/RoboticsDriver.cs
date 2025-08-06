
using Hai.PositionSystemToExternalProgram.Core;

namespace Hai.PositionSystemToExternalProgram.Robotics;

public class RoboticsDriver
{
    private float _virtualScaleChange = 1f; // = 1.5f;
    
    private float _unsafeJoystickTargetL0;
    private float _unsafeJoystickTargetL1;
    private float _unsafeJoystickTargetL2;
    private float _unsafeAngleDegR0 = 0; 
    private float _unsafeAngleDegR1 = 0; 
    private float _unsafeAngleDegR2 = 0;
    
    private float _offsetJoystickTargetL0;
    private float _offsetJoystickTargetL1;
    private float _offsetJoystickTargetL2;
    private float _offsetAngleDegR0; 
    private float _offsetAngleDegR1; 
    private float _offsetAngleDegR2; // = 30; 
    
    private float _safeJoystickTargetL0;
    private float _safeJoystickTargetL1;
    private float _safeJoystickTargetL2;
    private float _safeAngleDegR0 = 0; 
    private float _safeAngleDegR1 = 0; 
    private float _safeAngleDegR2 = 0; 

    public void ProvideTargets(InterpretedLightData interpretedData)
    {
        if (!interpretedData.hasTarget) return;

        _unsafeJoystickTargetL0 = RemapAndClamp(interpretedData.position.Y * _virtualScaleChange, 0f, 1f, -1f, 1f);
        _unsafeJoystickTargetL1 = RemapAndClamp(-interpretedData.position.Z * _virtualScaleChange, -0.5f, 0.5f, -1f, 1f);
        _unsafeJoystickTargetL2 = RemapAndClamp(interpretedData.position.X * _virtualScaleChange, -0.5f, 0.5f, -1f, 1f);
        _safeJoystickTargetL0 = Clamp(_unsafeJoystickTargetL0 + _offsetJoystickTargetL0, -1f, 1f);
        _safeJoystickTargetL1 = Clamp(_unsafeJoystickTargetL1 + _offsetJoystickTargetL1, -1f, 1f);
        _safeJoystickTargetL2 = Clamp(_unsafeJoystickTargetL2 + _offsetJoystickTargetL2, -1f, 1f);

        if (interpretedData.hasNormal)
        {
            // Normals have no twist, so we cannot set this.
            // _unsafeAngleDegL0 = 0;
            _unsafeAngleDegR1 = NormalToDegrees(-interpretedData.normal.X);
            _unsafeAngleDegR2 = NormalToDegrees(-interpretedData.normal.Z);
            _safeAngleDegR0 = Clamp(_unsafeAngleDegR0 + _offsetAngleDegR0, -360f, 360f);
            _safeAngleDegR1 = Clamp(_unsafeAngleDegR1 + _offsetAngleDegR1, -65f, 65f);
            _safeAngleDegR2 = Clamp(_unsafeAngleDegR2 + _offsetAngleDegR2, -65f, 65f);
        }
    }

    private static float NormalToDegrees(float normal)
    {
        var angleRad = (float)Math.Asin(normal);
        var angleDegrees = angleRad * 180f / (float)Math.PI;
        return angleDegrees;
    }

    public RoboticsCoordinates UpdateAndGetCoordinates(long deltaTimeMs)
    {
        return new RoboticsCoordinates
        {
            JoystickTargetL0 = _safeJoystickTargetL0,
            JoystickTargetL1 = _safeJoystickTargetL1,
            JoystickTargetL2 = _safeJoystickTargetL2,
            AngleDegR0 = _safeAngleDegR0,
            AngleDegR1 = _safeAngleDegR1,
            AngleDegR2 = _safeAngleDegR2,
        };
    }

    public static float Clamp(float value, float toMin, float toMax)
    {
        if (value < toMin) return toMin;
        if (value > toMax) return toMax;
        return value;
    }

    public static float RemapAndClamp(float value, float fromMin, float fromMax, float toMin, float toMax)
    {
        var vv = Math.Max(fromMin, Math.Min(fromMax, value));
        var normalizedValue = (vv - fromMin) / (fromMax - fromMin);
        var result = toMin + normalizedValue * (toMax - toMin);
        if (result < toMin) return toMin;
        if (result > toMax) return toMax;
        return result;
    }

}
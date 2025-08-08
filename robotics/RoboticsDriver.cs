
using System.Numerics;
using Hai.PositionSystemToExternalProgram.Core;

namespace Hai.PositionSystemToExternalProgram.Robotics;

public class RoboticsDriver
{
    private float _configVirtualScale = 1f;
    
    private bool _configUsePidRoot = false;
    private bool _configUsePidTarget = false;
    
    private float _configSafetyDistanceBeyondWhichInputsAreIgnored = 3f;
    
    private bool _configSafetyUsePolarMode = true;
    private float _configSafetyPolarModeUppermostRadius = 1f;
    private float _configSafetyPolarModeBottommostRadius = 0.4f;
    
    private float _configTopmostHardLimit = 1f;
    
    //

    private float _unsafeJoystickTargetL0;
    private float _unsafeJoystickTargetL1;
    private float _unsafeJoystickTargetL2;
    private float _unsafeAngleDegR0 = 0; 
    private float _unsafeAngleDegR1 = 0; 
    private float _unsafeAngleDegR2 = 0;
    private float _unsafeVerticality;
    
    private Vector3 _transitionalCoordinate;
    private readonly PidControllerVector3 _postTransitionalPid;
    private Vector3 _pidCoordinateCurrent;
    
    private readonly PidControllerVector3 _rootPositionPid;
    private Vector3 _pidRootCurrent;
    private Vector3 _pidRootTarget;

    private float _offsetJoystickTargetL0;
    private float _offsetJoystickTargetL1;
    private float _offsetJoystickTargetL2;
    private float _offsetAngleDegR0; 
    private float _offsetAngleDegR1; 
    private float _offsetAngleDegR2; 
    
    private float _safeJoystickTargetL0;
    private float _safeJoystickTargetL1;
    private float _safeJoystickTargetL2;
    private float _safeAngleDegTargetR0 = 0; 
    private float _safeAngleDegTargetR1 = 0; 
    private float _safeAngleDegTargetR2 = 0;

    public RoboticsDriver()
    {
        // TODO: We need to those PID controllers.
        _rootPositionPid = new PidControllerVector3
        {
            proportionalGain = 0.003f,
            integralGain = 0.003f,
            derivativeGain = 0.01f,
            integralMaximumMagnitude = 0.1f
        };
        _postTransitionalPid = new PidControllerVector3
        {
            proportionalGain = 0.05f,
            integralGain = 1f,
            derivativeGain = 0f,
            integralMaximumMagnitude = 0.1f
        };
    }

    public void ProvideTargets(InterpretedLightData interpretedData)
    {
        if (!interpretedData.hasTarget)
        {
            // TODO: If there is no target, we need to remember that, so that when a target appears,
            // we don't immediately slam the robotic arm because the data has changed too much.
            return;
        }

        // ## Acquire Inputs
        {
            // TODO: Handle what happens when the target is way, way off the system.
            // For instance we may have to consider using a PID controller in order to relativize the position,
            // and only consider (0, 0, 0) as being a preferred position.

            // Confine the input light position to a centered box and make it match the robotics coordinate system.
            var unclampedL0 = Remap(interpretedData.position.Y / _configVirtualScale, 0f, 1f, -1f, 1f);
            var unclampedL1 = Remap(-interpretedData.position.Z / _configVirtualScale, -0.5f, 0.5f, -1f, 1f);
            var unclampedL2 = Remap(interpretedData.position.X / _configVirtualScale, -0.5f, 0.5f, -1f, 1f);
            var unclampedVectorUntouched = new Vector3(unclampedL0, unclampedL1, unclampedL2);
            
            // Optionally, use a PID controller to stabilize the root.
            Vector3 unclampedVector;
            if (_configUsePidRoot)
            {
                _pidRootTarget = unclampedVectorUntouched;
                unclampedVector = unclampedVectorUntouched - _pidRootCurrent;
            }
            else
            {
                unclampedVector = unclampedVectorUntouched;
            }
            
            // If we use the root PID controller, the length does not matter because it will readjust anyway.
            if (_configUsePidRoot || unclampedVector.Length() <= _configSafetyDistanceBeyondWhichInputsAreIgnored)
            {
                _unsafeJoystickTargetL0 = Clamp(unclampedVector.X, -1f, 1f);
                _unsafeJoystickTargetL1 = Clamp(unclampedVector.Y, -1f, 1f);
                _unsafeJoystickTargetL2 = Clamp(unclampedVector.Z, -1f, 1f);
                _unsafeVerticality = (_unsafeJoystickTargetL0 + 1) / 2f;

                if (interpretedData.hasNormal)
                {
                    // Perform a normal to degree conversion. This limits the range from -90 to +90.
                    _unsafeAngleDegR0 = 0; // Normals have no twist, so we cannot set this.
                    _unsafeAngleDegR1 = NormalToDegrees(-interpretedData.normal.X);
                    _unsafeAngleDegR2 = NormalToDegrees(-interpretedData.normal.Z);
                }
            }
        }

        // ## From there on, we use the robotic arm coordinate space, where X is up (!!!)

        if (_configSafetyUsePolarMode)
        {
            // When the Safety Polar mode is enabled, we clamp the Y and Z axis to be within a disc.
            // If the length on (Y, Z) is greater than the allowed radius, we clamp it to that radius. 
            
            var allowedRadius = RemapAndClamp(_unsafeVerticality, 0f, 1f, _configSafetyPolarModeBottommostRadius, _configSafetyPolarModeUppermostRadius);
            var radial = new Vector3(0, _unsafeJoystickTargetL1, _unsafeJoystickTargetL2);
            if (radial.Length() > allowedRadius) radial = Vector3.Normalize(radial) * allowedRadius;
            
            _transitionalCoordinate = new Vector3(_unsafeJoystickTargetL0, radial.Y, radial.Z);
        }
        else
        {
            // Otherwise, we use unclamped coordinates. This can be dangerous.
            
            _transitionalCoordinate = new Vector3(_unsafeJoystickTargetL0, _unsafeJoystickTargetL1, _unsafeJoystickTargetL2);
        }

        if (!_configUsePidTarget)
        {
            CalculateOutputs(_transitionalCoordinate);
        }
    }

    private void CalculateOutputs(Vector3 whichVector)
    {
        // Apply offsets to the physical device. Note that doing this will reduce the motion range of the device
        // because the input was already clamped.
        // Using offsets instead of reducing the motion space has the advantage that the motion in virtual space
        // is still consistent in scale in comparison to the other axis.
        var workX = whichVector.X + _offsetJoystickTargetL0;
        if (_configTopmostHardLimit < 1f)
        {
            var joystickLimit = _configTopmostHardLimit * 2 - 1;
            Console.WriteLine(joystickLimit);
            if (workX > joystickLimit)
            {
                workX = joystickLimit;
            }
        }
        _safeJoystickTargetL0 = Clamp(workX, -1f, 1f);
        _safeJoystickTargetL1 = Clamp(whichVector.Y + _offsetJoystickTargetL1, -1f, 1f);
        _safeJoystickTargetL2 = Clamp(whichVector.Z + _offsetJoystickTargetL2, -1f, 1f);

        // Apply offsets to the physical device and clamp it. Since the input was not clamped,
        // this will not reduce the motion range of the device.
        _safeAngleDegTargetR0 = Clamp(_unsafeAngleDegR0 + _offsetAngleDegR0, -360f, 360f);
        _safeAngleDegTargetR1 = Clamp(_unsafeAngleDegR1 + _offsetAngleDegR1, -65f, 65f);
        _safeAngleDegTargetR2 = Clamp(_unsafeAngleDegR2 + _offsetAngleDegR2, -65f, 65f);
    }

    public void MarkDataFailure()
    {
        // Placeholder; then there may be a procedure to ensure that when data is recovered,
        // we don't immediately slam the robotic arm because the data has changed too much.
    }

    public RoboticsCoordinates UpdateAndGetCoordinates(long deltaTimeMs)
    {
        var deltaTime = deltaTimeMs / 1000f;
        if (_configUsePidRoot)
        {
            _pidRootCurrent += _rootPositionPid.Update(deltaTime, _pidRootCurrent, _pidRootTarget);
        }
        
        if (_configUsePidTarget)
        {
            _pidCoordinateCurrent += _postTransitionalPid.Update(deltaTime, _pidCoordinateCurrent, _transitionalCoordinate);
            
            CalculateOutputs(_pidCoordinateCurrent);
        }
        
        return new RoboticsCoordinates
        {
            JoystickTargetL0 = _safeJoystickTargetL0,
            JoystickTargetL1 = _safeJoystickTargetL1,
            JoystickTargetL2 = _safeJoystickTargetL2,
            AngleDegR0 = _safeAngleDegTargetR0,
            AngleDegR1 = _safeAngleDegTargetR1,
            AngleDegR2 = _safeAngleDegTargetR2,
        };
    }

    private static float Clamp(float value, float toMin, float toMax)
    {
        if (value < toMin) return toMin;
        if (value > toMax) return toMax;
        return value;
    }

    private static float RemapAndClamp(float value, float fromMin, float fromMax, float toMin, float toMax)
    {
        var result = Remap(value, fromMin, fromMax, toMin, toMax);
        if (result < toMin) return toMin;
        if (result > toMax) return toMax;
        return result;
    }

    private static float Remap(float value, float fromMin, float fromMax, float toMin, float toMax)
    {
        var normalizedValue = (value - fromMin) / (fromMax - fromMin);
        var result = toMin + normalizedValue * (toMax - toMin);
        return result;
    }

    private static float NormalToDegrees(float normal)
    {
        var angleRad = (float)Math.Asin(normal);
        var angleDegrees = angleRad * 180f / (float)Math.PI;
        return angleDegrees;
    }

    public void UpdateConfiguration(float configRoboticsVirtualScale,
        bool configRoboticsSafetyUsePolarMode,
        bool configRoboticsUsePidRoot,
        bool configRoboticsUsePidTarget,
        float configTopmostHardLimit,
        float configOffsetAngleDegR2)
    {
        _configVirtualScale = configRoboticsVirtualScale;
        _configSafetyUsePolarMode = configRoboticsSafetyUsePolarMode;
        _configUsePidRoot = configRoboticsUsePidRoot;
        _configUsePidTarget = configRoboticsUsePidTarget;
        _configTopmostHardLimit = configTopmostHardLimit;
        _offsetAngleDegR2 = configOffsetAngleDegR2;
    }
}
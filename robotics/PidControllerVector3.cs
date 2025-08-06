using System.Numerics;

namespace Hai.PositionSystemToExternalProgram.Robotics;

public class PidControllerVector3
{
    public float proportionalGain;
    public float integralGain;
    public float derivativeGain;

    public float integralMaximumMagnitude = 1f;

    private Vector3 _previous;
    private Vector3 _integration;

    // Whenever the object spawns or teleports, we need skip the first derivative in order to prevent a sudden jump.
    private bool _skipFirstDerivative = true;

    public void ResetAsFirstFrame()
    {
        _skipFirstDerivative = true;
        _previous = Vector3.Zero;
        _integration = Vector3.Zero;
    }

    public Vector3 Update(float fixedDeltaTime, Vector3 current, Vector3 target)
    {
        var error = target - current;
        var proportionalValue = proportionalGain * error;

        var change = (current - _previous) / fixedDeltaTime;
        _previous = current;

        Vector3 derivativeValue;
        if (_skipFirstDerivative)
        {
            derivativeValue = Vector3.Zero;
            _skipFirstDerivative = false;
        }
        else
        {
            derivativeValue = derivativeGain * -change;
        }

        _integration += error * fixedDeltaTime;
        if (_integration.Length() > integralMaximumMagnitude)
        {
            _integration = Vector3.Normalize(_integration) * integralMaximumMagnitude;
        }
        var integralValue = integralGain * _integration;

        return proportionalValue + derivativeValue + integralValue;
    }
}
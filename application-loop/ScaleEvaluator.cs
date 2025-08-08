using System.Diagnostics;
using System.Numerics;

namespace Hai.PositionSystemToExternalProgram.ApplicationLoop;

public class ScaleEvaluator
{
    private readonly Stopwatch _gw;

    public float VirtualScale { get; private set; } = 1f;
    
    private Vector3 _sampledHmdPosition;
    private Vector3 _sampledCameraPosition;
    private Vector3 _lastHmdPosition;
    private Vector3 _lastCameraPosition;
    private long _nextEvaluationMs;

    public ScaleEvaluator()
    {
        _gw = Stopwatch.StartNew();
    }

    public void Evaluate(Vector3 hmdPosition, Vector3 cameraPosition)
    {
        if (_gw.ElapsedMilliseconds < _nextEvaluationMs) return;
        _nextEvaluationMs = _gw.ElapsedMilliseconds + 100;

        var anyPositionRemainedTheSame = hmdPosition == _sampledHmdPosition || cameraPosition == _sampledCameraPosition;
        
        // We can only evaluate if both have changed.
        if (anyPositionRemainedTheSame)
        {
            // We redefine them anyway if only one of them has changed.
            _lastHmdPosition = hmdPosition;
            _lastCameraPosition = cameraPosition;
            return;
        }

        var virtualChange = (cameraPosition - _sampledCameraPosition).Length();
        var physicalChange = (hmdPosition - _sampledHmdPosition).Length();
        
        if (physicalChange > 0.25f) // We have moved a lot since the previous sample.
        {
            var fastphysicalChange = (hmdPosition - _lastHmdPosition).Length();
            
            if (fastphysicalChange < 0.005f) // We haven't moved much since last time.
            {
                var scaleInVirtual = virtualChange / physicalChange;
                VirtualScale = scaleInVirtual; 
                _sampledHmdPosition = hmdPosition;
                _sampledCameraPosition = cameraPosition;
            }
        }
        
        _lastHmdPosition = hmdPosition;
        _lastCameraPosition = cameraPosition;
    }
}
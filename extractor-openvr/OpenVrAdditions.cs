using System.Numerics;
using Valve.VR;

namespace Hai.PositionSystemToExternalProgram.Extractors.OVR;

public class OpenVrAdditions
{
    private readonly TrackedDevicePose_t[] _poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
    
    public void UpdatePoses()
    {
        // TODO: We could get poses in the past, and use a WaitGetPoses() timing.
        OpenVR.System.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseRawAndUncalibrated, 0f, _poses);
    }
    
    public Vector3 GetHmdPositionAsUnityVector()
    {
        HmdMatrix34_t ovrMatrix = _poses[0].mDeviceToAbsoluteTracking;
        return OvrMatrixToUnityVector(ovrMatrix);
    }

    private static Vector3 OvrMatrixToUnityVector(HmdMatrix34_t ovrMatrix)
    {
        return new Vector3(ovrMatrix.m3, ovrMatrix.m7, -ovrMatrix.m11);
    }
}
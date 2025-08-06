using System.Numerics;

namespace Hai.PositionSystemToExternalProgram.Core;

public class DecodedData
{
    /// If the last checksum failed, the data will still contain the last known valid decoded data.
    public DataValidity validity;
    
    public float Time = -1f;
    public DecodedLight Light0 { get; } = new();
    public DecodedLight Light1 { get; } = new();
    public DecodedLight Light2 { get; } = new();
    public DecodedLight Light3 { get; } = new();
    public DecodedLight[] Lights { get; }
    public Vector3 HmdPosition;
    public Vector4 HmdRotation;

    public DecodedData()
    {
        Lights = new [] { Light0, Light1, Light2, Light3 };
    }
}

public enum DataValidity
{
    NotInitialized,
    Ok,
    InvalidChecksum,
    UnexpectedVendor,
    UnexpectedMajorVersion,
}
    
public class DecodedLight
{
    public bool colorAvailable;
    public bool positionAvailable;
    public bool rangeAvailable;
    
    public Vector3 color;
    public bool enabled;
    public float intensity;
        
    public Vector3 position;
    public float range;

    public DecodedLight()
    {
        color = Vector3.Zero;
        position = Vector3.Zero;
    }
}
using System.Numerics;

namespace Hai.PositionSystemToExternalProgram.Core;

public class DecodedData
{
    /// If the last checksum failed, the data will still contain the last known valid decoded data.
    public DataValidity validity;

    public uint Version = 0;
    public float Time = -1f;
    public DecodedLight Light0 { get; } = new();
    public DecodedLight Light1 { get; } = new();
    public DecodedLight Light2 { get; } = new();
    public DecodedLight Light3 { get; } = new();
    public DecodedLight[] Lights { get; }
    public Vector3 CameraPosition;
    public Vector3 CameraRotation;

    public string AsSemverString()
    {
        var major = Version / 1_000_000;
        var minor = (Version / 1_000) % 1000;
        var patch = Version % 1000;
        return $"{major}.{minor}.{patch}";
    }

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
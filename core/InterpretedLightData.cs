using System.Numerics;

namespace Hai.PositionSystemToExternalProgram.Core;

public struct InterpretedLightData
{
    public bool hasTarget;
    public Vector3 position;
        
    public bool hasNormal;
    public Vector3 normal;
    public bool isHole;
    public bool isRing;
}
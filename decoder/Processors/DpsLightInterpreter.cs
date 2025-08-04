using System.Numerics;
using Hai.PositionSystemToExternalProgram.Core;

namespace Hai.PositionSystemToExternalProgram.Processors;

/// Using light information coming from ExtractedDataDecoder, interpret DPS-like light data.
public class DpsLightInterpreter
{
    private const float LightRangeForHole = 0.41f;
    private const float LightRangeForRing = 0.42f;
    private const float LightRangeForDirectionNormal = 0.45f;
        
    private const float SuspiciousNormalDistanceLimit = 0.3f;

    public InterpretedLightData Interpret(DecodedData decoded)
    {
        var lights = decoded.Lights.Where(IsBlackLight).ToList();

        var holes = lights.Where(light => EncodesRange(light, LightRangeForHole)).OrderBy(LocalPosSqrMagnitude).ToList();
        var rings = lights.Where(light => EncodesRange(light, LightRangeForRing)).OrderBy(LocalPosSqrMagnitude).ToList();
        var directionIndicators = lights.Where(light => EncodesRange(light, LightRangeForDirectionNormal)).ToList();

        if (holes.Count > 0 || rings.Count > 0)
        {
            var holeOrRingElts = holes.Concat(rings)
                .OrderBy(LocalPosSqrMagnitude)
                .ToList();

            var our = holeOrRingElts.First();
            var position = our.position;
            if (directionIndicators.Count > 0)
            {
                var closestDirectionIndicators = directionIndicators
                    .OrderBy(directionIndicator => Vector3.Distance(position, directionIndicator.position))
                    .First();
                if (Vector3.Distance(position, closestDirectionIndicators.position) < SuspiciousNormalDistanceLimit)
                {
                    var normal = Vector3.Normalize(position - closestDirectionIndicators.position);
                    
                    return new InterpretedLightData
                    {
                        hasTarget = true,
                        position = position,
                        hasNormal = true,
                        normal = normal,
                        isHole = EncodesRange(our, LightRangeForHole),
                        isRing = EncodesRange(our, LightRangeForRing),
                    };
                }
            }

            return new InterpretedLightData
            {
                hasTarget = true,
                position = position,
                isHole = EncodesRange(our, LightRangeForHole),
                isRing = EncodesRange(our, LightRangeForRing),
            };
        }

        return new InterpretedLightData
        {
            hasTarget = false
        };
    }

    private static bool IsBlackLight(DecodedLight light) => light.color.X == 0f && light.color.Y == 0f && light.color.Z == 0f;
    private static bool EncodesRange(DecodedLight light, float encodedAmount) => light.enabled && MathF.Abs(light.range - encodedAmount) < 0.005f;
    private static float LocalPosSqrMagnitude(DecodedLight light) => Vector3.DistanceSquared(Vector3.Zero, light.position);
}
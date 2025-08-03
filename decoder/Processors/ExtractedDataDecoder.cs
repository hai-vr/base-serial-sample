using System.Numerics;
using Hai.PositionSystemToExternalProgram.Core;

namespace Hai.PositionSystemToExternalProgram.Processors;

/// Using data lines coming from TextureDataExtractor, decode into light information.
public class ExtractedDataDecoder
{
    private const uint OurVendor = 1366692562;
    private const uint OurMajorVersionNumber = 1;
    
    private const int NumberOfBytesInAFloat = 4;
    private const int NumberOfBitsInAFloat = NumberOfBytesInAFloat * 8;
    private const int NumberOfComponentsInAColor = 4;
    private const int NumberOfComponentsInAVector = 3;
        
    private const int Checksum = 0;
    private const int Time = 1;
    private const int VendorCheck = 2;
    private const int VersionSemver = 3;
    private const int LightPositionStart = 4;
    private const int LightColorStart = LightPositionStart + 4 * NumberOfComponentsInAVector;
    private const int LightAttenuationStart = LightColorStart + 4 * NumberOfComponentsInAColor;
    private const int HmdPositionStart = 36;
    private const int HmdRotationStart = 40;

    private bool[] _data;

    public bool IsDataValid(bool[] data, int crc32Line)
    {
        var crc32StartPos = crc32Line * NumberOfBitsInAFloat;
        var bulkPart = data.AsSpan(0, crc32StartPos);
        var crc32Part = data.AsSpan(crc32StartPos, 32);

        // var calculatedCrc32 = InCRC32.CalculateCrc32(bulkPart);
        // var storedCrc32 = InCRC32.ExtractCrc32FromBits(crc32Part);
        // return calculatedCrc32 == storedCrc32;
        return true;
    }

    public void DecodeInto(DecodedData decodedMutated, bool[] dataLines)
    {
        _data = dataLines;
        
        var checksum = SampleUInt32(Checksum);
        // TODO: Calculate checksum
        if (false)
        {
            decodedMutated.validity = DataValidity.InvalidChecksum;
            return;
        }

        var time = SampleFloat(Time);
        var itIsTheSameTime = Math.Abs(time - decodedMutated.Time) < 0.0001f;
        if (itIsTheSameTime)
        {
            // Skip decoding. Data can only change when the time also changes.
            return;
        }
        decodedMutated.Time = time;
        
        var versionCheckValue = SampleUInt32(VendorCheck);
        if (versionCheckValue != OurVendor)
        {
            decodedMutated.validity = DataValidity.UnexpectedVendor;
            return;
        }
        
        var versionSemverValue = SampleUInt32(VersionSemver);
        var major = versionSemverValue / 1_000_000;
        if (major != OurMajorVersionNumber)
        {
            // TODO: Expose the version to the decoded data.
            decodedMutated.validity = DataValidity.UnexpectedMajorVersion;
            return;
        }
        
        for (var index = 0; index < 4; index++)
        {
            var light = decodedMutated.Lights[index];
            DecodeLight(index, light);
        }
        
        decodedMutated.validity = DataValidity.Ok;
    }

    private void DecodeLight(int index, DecodedLight light)
    {
        if (ReadVector3StartingFromLine(LightPositionStart + index * NumberOfComponentsInAVector, out var pos))
        {
            light.position = pos;
            light.positionAvailable = true;
        }
        else
        {
            light.positionAvailable = false;
        }

        if (ReadVector4StartingFromLine(LightColorStart + index * NumberOfComponentsInAColor, out var color))
        {
            light.color = V3(color);
            light.intensity = color.W;
            light.enabled = color.W > 0f;
            light.colorAvailable = true;
        }
        else
        {
            light.colorAvailable = false;
        }

        if (ReadFloatStartingFromLine(LightAttenuationStart + index, out var attenuation))
        {
            light.range = ConvertAttenuationToRangeOrOne(attenuation);
            light.rangeAvailable = true;
        }
        else
        {
            light.rangeAvailable = false;
        }
    }

    private static float ConvertAttenuationToRangeOrOne(float attenuation)
    {
        var result = (float)((0.005f * Math.Sqrt(1000000f - attenuation)) / Math.Sqrt(attenuation));
        if (float.IsNaN(result) || float.IsInfinity(result))
        {
            return 1f;
        }
        return result;
    }

    private Vector3 V3(Vector4 result)
    {
        return new Vector3(result.X, result.Y, result.Z);
    }

    private bool ReadFloatStartingFromLine(int lineNumber, out float result)
    {
        var x = SampleFloat(lineNumber);
        if (float.IsNaN(x))
        {
            result = 0f;
            return false;
        }
        result = x;
        return true;
    }

    private bool ReadVector3StartingFromLine(int lineNumber, out Vector3 result)
    {
        var x = SampleFloat(lineNumber);
        var y = SampleFloat(lineNumber + 1);
        var z = SampleFloat(lineNumber + 2);
        if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z))
        {
            result = Vector3.Zero;
            return false;
        }
        result = new Vector3(x, y, z);
        return true;
    }

    private bool ReadVector4StartingFromLine(int lineNumber, out Vector4 result)
    {
        var x = SampleFloat(lineNumber);
        var y = SampleFloat(lineNumber + 1);
        var z = SampleFloat(lineNumber + 2);
        var w = SampleFloat(lineNumber + 3);
        if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z) || float.IsNaN(w))
        {
            result = Vector4.Zero;
            return false;
        }
        result = new Vector4(x, y, z, w);
        return true;
    }

    private float SampleFloat(int line)
    {
        var floatRepresentation = Decode32Bit(line);
        return BitConverter.ToSingle(BitConverter.GetBytes(floatRepresentation), 0);
    }

    private uint SampleUInt32(int line)
    {
        var floatRepresentation = Decode32Bit(line);
        return BitConverter.ToUInt32(BitConverter.GetBytes(floatRepresentation), 0);
    }

    private uint Decode32Bit(int line)
    {
        var startPos = line * NumberOfBitsInAFloat;
        if (startPos < 0 || _data.Length < startPos + NumberOfBitsInAFloat) throw new ArgumentOutOfRangeException(nameof(line));
        // var dataLine = _data.AsSpan(startPos);
            
        uint floatRepresentation = 0;
        for (var bit = 0; bit < NumberOfBitsInAFloat; bit++)
        {
            var truthiness = _data[startPos + bit];
            var value = (uint)(truthiness ? 1 : 0);
            floatRepresentation |= value << bit;
        }

        return floatRepresentation;
    }
}
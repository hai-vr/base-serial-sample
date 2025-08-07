using System.Numerics;
using Hai.PositionSystemToExternalProgram.Core;

namespace Hai.PositionSystemToExternalProgram.Decoder;

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
    private const int CameraPositionStart = 36;
    private const int CameraRotationStart = 39;
    public const int GroupLength = 52;
    
    private const uint Crc32Polynomial = 0xEDB88320u;

    private bool[] _data;
    private DataValidity _lastChesksumPassingValidity;

    public void DecodeInto(DecodedData decodedMutated, bool[] dataLines)
    {
        _data = dataLines;
        
        var receivedChecksum = SampleUInt32(Checksum);
        var calculatedChecksum = CalculateChecksum();

        var checksumPasses = receivedChecksum == calculatedChecksum;
        if (!checksumPasses)
        {
            decodedMutated.validity = DataValidity.InvalidChecksum;
            return;
        }

        var time = SampleFloat(Time);
        var itIsTheSameTime = Math.Abs(time - decodedMutated.Time) < 0.0001f;
        if (itIsTheSameTime)
        {
            // We reapply the last validity that is not a checksum, because if we had a valid data, followed by invalid data,
            // followed by valid data that was the same as previously, we want it to be marked as valid.
            decodedMutated.validity = _lastChesksumPassingValidity;
            // Skip decoding. Data can only change when the time also changes.
            return;
        }
        decodedMutated.Time = time;
        
        var versionCheckValue = SampleUInt32(VendorCheck);
        if (versionCheckValue != OurVendor)
        {
            decodedMutated.validity = DataValidity.UnexpectedVendor;
            _lastChesksumPassingValidity = DataValidity.UnexpectedVendor;
            return;
        }
        
        var versionSemverValue = SampleUInt32(VersionSemver);
        decodedMutated.Version = versionSemverValue;
        var major = versionSemverValue / 1_000_000;
        if (major != OurMajorVersionNumber)
        {
            decodedMutated.validity = DataValidity.UnexpectedMajorVersion;
            _lastChesksumPassingValidity = DataValidity.UnexpectedMajorVersion;
            return;
        }
        
        for (var index = 0; index < 4; index++)
        {
            var light = decodedMutated.Lights[index];
            DecodeLight(index, light);
        }

        if (versionSemverValue >= 1_001_000)
        {
            decodedMutated.CameraPosition = ReadVector3StartingFromLine(CameraPositionStart, out var pos) ? pos : Vector3.Zero;
            decodedMutated.CameraRotation = ReadVector3StartingFromLine(CameraRotationStart, out var rot) ? rot : Vector3.Zero;
        }
        else
        {
            decodedMutated.CameraPosition = Vector3.Zero;
            decodedMutated.CameraRotation = Vector3.Zero;
        }
        
        decodedMutated.validity = DataValidity.Ok;
        _lastChesksumPassingValidity = DataValidity.Ok;
    }

    private uint CalculateChecksum()
    {
        uint crc = 0xFFFFFFFFu;
        for (var line = Time; line < GroupLength; line++)
        {
            crc = CRC32UpdateUint(crc, SampleUInt32(line));
        }
        crc = crc ^ 0xFFFFFFFFu;
        return crc;
    }

    private uint CRC32UpdateUint(uint crc, uint value)
    {
        crc = CRC32UpdateByte(crc, value & 0xFF);
        crc = CRC32UpdateByte(crc, (value >> 8) & 0xFF);
        crc = CRC32UpdateByte(crc, (value >> 16) & 0xFF);
        crc = CRC32UpdateByte(crc, (value >> 24) & 0xFF);
        return crc;
    }

    private uint CRC32UpdateByte(uint crc, uint byte_val)
    {
        uint temp = crc ^ byte_val;
        for (int i = 0; i < 8; i++)
        {
            if ((temp & 1) > 0)
                temp = (temp >> 1) ^ Crc32Polynomial;
            else
                temp = temp >> 1;
        }
        return temp;
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
        return Decode32Bit(line);
    }

    private uint Decode32Bit(int line)
    {
        var startPos = line * NumberOfBitsInAFloat;
        if (startPos < 0 || _data.Length < startPos + NumberOfBitsInAFloat) throw new ArgumentOutOfRangeException(nameof(line));
            
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
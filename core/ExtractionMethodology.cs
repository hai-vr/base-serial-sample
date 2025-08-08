namespace Hai.PositionSystemToExternalProgram.Core;

public static class ExtractionMethodology
{
    // To combat bloom, the shader outputs black and red.
    // Bloom may affect all color channels, so we try to remove the green value from the red value to compensate for
    // black pixels getting elevated red values due to the bloom.
    public static byte CombineRedGreen(byte red, byte green)
    {
        // We no longer use this. The shader now outputs gray, not red and 0% green like it used to.
        if (false)
        {
            // return (byte)Math.Clamp(red - green, 0, 255);
        }
        return red;
    }
}
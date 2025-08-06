using Hai.PositionSystemToExternalProgram.Core;

namespace Hai.PositionSystemToExternalProgram.Decoder;

/// Given known shader settings, extract data contained within a larger texture (e.g. OpenVR mirror texture).
public class BitsTransformer
{
    // All brightness comparisons in the decoder should expect values that vary from what was set
    // in the shader, as there is still a possibility that transparency, post-processing, bloom, or other
    // shader effects will write over our pixels.
    // We can't use a too low value, because tonemapping can occur and might change the blackness,
    // even in the presence of bloom.
    private const int ColorValueThresholdForTruthness = 110;

    private readonly PositionSystemDataLayout _dataLayout;
    private readonly bool[] _data;

    public BitsTransformer(PositionSystemDataLayout dataLayout)
    {
        _dataLayout = dataLayout;

        _data = new bool[ExtractedDataDecoder.GroupLength * 32];
    }

    public bool[] ReadBitsFromExtractedImage(byte[] monochromaticBytes, int width, int height)
    {
        // The width and height are from the extracted image. This can depend on the vertical resolution of the HMD,
        // so the width is NOT necessarily equal to EncodedSquareSize * (NumberOfColumns + MarginPerSide * 2)
        
        var squareSize = _dataLayout.EncodedSquareSize;
        var actualInterSquareDistanceW = width / ((float)_dataLayout.numberOfColumns + _dataLayout.MarginPerSide * 2);
        var actualInterSquareDistanceH = height / ((float)_dataLayout.numberOfDataLines + _dataLayout.MarginPerSide * 2);
        var interPixelDistanceW = actualInterSquareDistanceW / squareSize;
        var interPixelDistanceH = actualInterSquareDistanceH / squareSize;
        
        for (var i = 0; i < _data.Length; i++)
        {
            var column = i % _dataLayout.numberOfColumns;
            var line = i / _dataLayout.numberOfColumns;
            var x = (int)((_dataLayout.MarginPerSide + column + 0.5) * actualInterSquareDistanceW);
            var y = (int)((_dataLayout.MarginPerSide + line + 0.5) * actualInterSquareDistanceH);

            // Calculate the average of the pixels.
            // Sampling from just one pixel makes it too sensitive and hard to align.
            var sum = 0;
            var count = 0;
            for (var p = 0; p < squareSize; p++)
            {
                for (var q = 0; q < squareSize; q++)
                {
                    var xx = x + (int)((p - (float)squareSize / 2) * interPixelDistanceW);
                    var yy = y + (int)((q - (float)squareSize / 2) * interPixelDistanceH);
                    if (TryGetMonochromaticIndex(xx, yy, out var monochromaticIndex))
                    {
                        sum += monochromaticBytes[monochromaticIndex];
                        count++;
                    }
                }
            }
            
            var value = (float)sum / count;
            var truthness = value > ColorValueThresholdForTruthness;
            _data[i] = truthness;

            bool TryGetMonochromaticIndex(int xx, int yy, out int result)
            {
                result = yy * width + xx;
                return result >= 0 && result < monochromaticBytes.Length;
            }
        }

        return _data;
    }
}
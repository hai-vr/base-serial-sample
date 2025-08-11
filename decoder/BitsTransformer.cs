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
    private const bool UseOtsuThreshold = true;
    private const int ColorValueThresholdForTruthness = 110;

    private readonly PositionSystemDataLayout _dataLayout;
    private readonly bool[] _data;
    private readonly int[] _dataInt;
    private readonly int[] _histogram = new int[256];
    private readonly float[] _probabilities = new float[256];

    public BitsTransformer(PositionSystemDataLayout dataLayout)
    {
        _dataLayout = dataLayout;

        _data = new bool[ExtractedDataDecoder.GroupLength * 32];
        _dataInt = new int[ExtractedDataDecoder.GroupLength * 32];
    }

    public bool[] ReadBitsFromExtractedImage(byte[] monochromaticBytes, int width, int height)
    {
        // The width and height are from the extracted image. This can depend on the vertical resolution of the HMD,
        // so the width is NOT necessarily equal to EncodedSquareSize * (NumberOfColumns + MarginPerSide * 2)
        
        var squareSize = _dataLayout.EncodedSquareSize;
        var actualInterSquareDistanceW = width / ((float)_dataLayout.NumberOfColumns + _dataLayout.MarginPerSide * 2);
        var actualInterSquareDistanceH = height / ((float)_dataLayout.NumberOfDataLines + _dataLayout.MarginPerSide * 2);
        var interPixelDistanceW = actualInterSquareDistanceW / squareSize;
        var interPixelDistanceH = actualInterSquareDistanceH / squareSize;
        
        for (var i = 0; i < _data.Length; i++)
        {
            var column = i % _dataLayout.NumberOfColumns;
            var line = i / _dataLayout.NumberOfColumns;
            var x = (int)((_dataLayout.MarginPerSide + column + 0.5) * actualInterSquareDistanceW);
            var y = (int)((_dataLayout.MarginPerSide + line + 0.5) * actualInterSquareDistanceH);

            var value = CalculateAverage(x, y);
            if (UseOtsuThreshold)
            {
                _dataInt[i] = (int)value;
            }
            else
            {
                var truthness = value > ColorValueThresholdForTruthness;
                _data[i] = truthness;
            }
        }

        if (UseOtsuThreshold)
        {
            var threshold = CalculateOtsuThreshold();
            for (var i = 0; i < _dataInt.Length; i++)
            {
                var value = _dataInt[i];
                _data[i] = value > threshold;
            }
        }

        return _data;

        float CalculateAverage(int x, int y)
        {
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
            return value;
        }

        bool TryGetMonochromaticIndex(int xx, int yy, out int result)
        {
            result = yy * width + xx;
            return result >= 0 && result < monochromaticBytes.Length;
        }
    }
    
    private int CalculateOtsuThreshold()
    {
        for (var i = 0; i < 256; i++)
        {
            _histogram[i] = 0;
        }

        var totalPixels = 0;
        foreach (var value in _dataInt)
        {
            if (value is >= 0 and <= 255)
            {
                _histogram[value]++;
                totalPixels++;
            }
        }

        // if (totalPixels == 0) return 128;

        float totalMean = 0;
        for (var i = 0; i < 256; i++)
        {
            _probabilities[i] = (float)_histogram[i] / totalPixels;
            totalMean += i * _probabilities[i];
        }

        var maxVariance = 0f;
        int optimalThreshold = 0;
        
        var weightFalsy = 0f;
        var sumFalsy = 0f;

        for (var t = 0; t < 256; t++)
        {
            weightFalsy += _probabilities[t];
            if (weightFalsy == 0) continue;

            var wTruthy = 1 - weightFalsy;
            if (wTruthy == 0) break;

            sumFalsy += t * _probabilities[t];
            var meanFalsy = sumFalsy / weightFalsy;
            var meanTruthy = (totalMean - sumFalsy) / wTruthy;

            var betweenVariance = weightFalsy * wTruthy * (meanFalsy - meanTruthy) * (meanFalsy - meanTruthy);
            if (betweenVariance > maxVariance)
            {
                maxVariance = betweenVariance;
                optimalThreshold = t;
            }
        }

        return optimalThreshold;
    }
}
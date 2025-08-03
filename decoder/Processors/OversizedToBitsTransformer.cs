using Hai.PositionSystemToExternalProgram.Core;

namespace Hai.PositionSystemToExternalProgram.Processors
{
    /// Given known shader settings, extract data contained within a larger texture (e.g. OpenVR mirror texture).
    public class OversizedToBitsTransformer
    {
        // All brightness comparisons in the decoder should expect values that vary from what was set
        // in the shader, as there is still a possibility that transparency, post-processing, bloom, or other
        // shader effects will write over our pixels.
        private const int ColorValueThresholdForTruthness = 110;

        private readonly PositionSystemDataLayout _dataLayout;
        private readonly int _shiftX;
        private readonly int _shiftY;
        private readonly bool[] _data;

        public OversizedToBitsTransformer(PositionSystemDataLayout dataLayout)
        {
            _dataLayout = dataLayout;
            _shiftX = dataLayout.EncodedSquareSize / 2;
            _shiftY = dataLayout.EncodedSquareSize / 2;

            _data = new bool[_dataLayout.numberOfDataLines * _dataLayout.numberOfColumns];
        }

        public bool[] ExtractBitsFromSubregion(byte[] monochromaticBytes, int width, int height)
        {
            var xxStart = _shiftX;
            var yyStart = _shiftY;

            for (var i = 0; i < _data.Length; i++)
            {
                var column = i % _dataLayout.numberOfColumns;
                var line = i / _dataLayout.numberOfColumns;

                var x = xxStart + column * _dataLayout.EncodedSquareSize;
                var y = yyStart + line * _dataLayout.EncodedSquareSize;

                var monochromaticIndex = y * width + x;
                int value = monochromaticBytes[monochromaticIndex];
                
                var truthness = value > ColorValueThresholdForTruthness;
                _data[i] = truthness;
            }

            return _data;
        }
    }
}
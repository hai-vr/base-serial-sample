namespace Hai.PositionSystemToExternalProgram.Core;

public class PositionSystemDataLayout
{
    private const int SERIALIZE_NumberOfColumns = 16;
    private const int GroupLength = 52;
    
    public readonly int EncodedSquareSize = 4;
    public readonly int numberOfColumns = SERIALIZE_NumberOfColumns;
    public readonly int numberOfDataLines = (int)Math.Ceiling((GroupLength * 32.0) / SERIALIZE_NumberOfColumns);

    public int PowerOfTwoAcquisitionTextureHeight => ContainWithinPowerOfTwo(numberOfDataLines);
    public int PowerOfTwoAcquisitionTextureWidth => ContainWithinPowerOfTwo(numberOfColumns);

    private static int ContainWithinPowerOfTwo(int n)
    {
        if (n < 1) return 1;

        var power = (int)Math.Ceiling(Math.Log(n, 2));
        return (int)Math.Pow(2, power);
    }
}
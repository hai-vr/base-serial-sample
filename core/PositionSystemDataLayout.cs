namespace Hai.PositionSystemToExternalProgram.Core;

public class PositionSystemDataLayout
{
    private const int SERIALIZE_NumberOfColumns = 16;
    private const int GroupLength = 52;
    
    public readonly int MarginPerSide = 1;
    public readonly int EncodedSquareSize = 4;
    public readonly int numberOfColumns = SERIALIZE_NumberOfColumns;
    public readonly int numberOfDataLines = CalculateNumberOfLines(SERIALIZE_NumberOfColumns);

    public static int CalculateNumberOfLines(int numberOfColumns)
    {
        return (int)Math.Ceiling((GroupLength * 32.0) / numberOfColumns);
    }

    public int PowerOfTwoAcquisitionTextureHeight => ContainWithinPowerOfTwo(numberOfDataLines);
    public int PowerOfTwoAcquisitionTextureWidth => ContainWithinPowerOfTwo(numberOfColumns);

    private static int ContainWithinPowerOfTwo(int n)
    {
        if (n < 1) return 1;

        var power = (int)Math.Ceiling(Math.Log(n, 2));
        return (int)Math.Pow(2, power);
    }
}
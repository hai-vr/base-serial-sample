namespace Hai.PositionSystemToExternalProgram.Processors;

public class PositionSystemDataLayout
{
    public readonly int EncodedSquareSize = 4;
    public readonly int numberOfDataLines = 32;
    public readonly int numberOfColumns = 32;
        
    public int PowerOfTwoAcquisitionTextureHeight => ContainWithinPowerOfTwo(numberOfDataLines);
    public int PowerOfTwoAcquisitionTextureWidth => ContainWithinPowerOfTwo(numberOfColumns);

    private static int ContainWithinPowerOfTwo(int n)
    {
        if (n < 1) return 1;

        var power = (int)Math.Ceiling(Math.Log(n, 2));
        return (int)Math.Pow(2, power);
    }
}
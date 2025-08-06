namespace Hai.PositionSystemToExternalProgram.Core;

public class PositionSystemDataLayout
{
    private const int SERIALIZE_NumberOfColumns = 16;
    private const int GroupLength = 52;
    
    public readonly int MarginPerSide = 1;
    public readonly int EncodedSquareSize = 4;
    public readonly int NumberOfColumns = SERIALIZE_NumberOfColumns;
    public readonly int NumberOfDataLines = CalculateNumberOfLines(SERIALIZE_NumberOfColumns);

    public static int CalculateNumberOfLines(int numberOfColumns)
    {
        return (int)Math.Ceiling((GroupLength * 32.0) / numberOfColumns);
    }
}
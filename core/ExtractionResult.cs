namespace Hai.PositionSystemToExternalProgram.Core;

public struct ExtractionResult
{
    public bool Success;
    
    public byte[] MonochromaticData;
    public byte[] ColorData;
    public int Width;
    public int Height;
    public int Iteration;

    public bool IsValid()
    {
        return Width > 0 && Height > 0;
    }
}
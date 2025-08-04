using System.Drawing;

namespace Hai.PositionSystemToExternalProgram.Core;

/// This uses a coordinate system where the top-left is (0, 0)
[Serializable]
public class ExtractionCoordinates
{
    public ExtractionSource source;
    public int x;
    public int y;
    public int requestedWidth = 256;
    public int requestedHeight = 256;
    public float anchorX;
    public float anchorY;

    public Rectangle ToRectangle(int canvasWidth, int canvasHeight)
    {
        var xx = (int)(anchorX * (canvasWidth - requestedWidth)) + x;
        var yy = (int)(anchorY * (canvasHeight - requestedHeight)) + y;
        
        return new Rectangle(xx, yy, requestedWidth, requestedHeight);
    }
}
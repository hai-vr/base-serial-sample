using System.IO.Ports;
using System.Numerics;

namespace Hai.PositionSystemToExternalProgram.Tcode;

public class TcodeSerial
{
    private SerialPort _port;
    private bool _ready;

    public bool IsOpen => _port != null && _ready;

    public string[] FetchPortNames()
    {
        return SerialPort.GetPortNames();
    }

    public void OpenSerial(string portName)
    {
        if (_port != null)
        {
            _ready = false;
            _port.Close();
            _port = null;
        }

        try
        {
            _port = new SerialPort(portName, 115200, Parity.None, 8, StopBits.One);
            _port.Open();
            _ready = true;
        }
        catch (Exception e)
        {
            _port = null;
            _ready = false;
        }
    }

    public void CloseSerial()
    {
        if (_port == null) return;
        
        _ready = false;
        var port = _port;
        _port = null;
        port.Close();
    }
    
    /// T-code uses coordinates from 0 to 9999, where 5000 is the middle.<br/>
    /// L0 goes up. L1 goes away. L2 goes left.<br/>
    /// R0 twists counter-clockwise from the user looking down.<br/>
    /// R1 rolls clockwise from the user looking forward.<br/>
    /// R2 leans away from the user.
    public bool TrySendCoords(Vector3 pos010000, Vector3 rot010000)
    {
        try
        {
            SanitizedWrite('L', '0', (int)pos010000.X);
            SanitizedWrite('L', '1', (int)pos010000.Y);
            SanitizedWrite('L', '2', (int)pos010000.Z);
            SanitizedWrite('R', '0', (int)rot010000.X);
            SanitizedWrite('R', '1', (int)rot010000.Y);
            SanitizedWrite('R', '2', (int)rot010000.Z);
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to send coords: {e.Message}");
            return false;
        }
    }
    
    private void SanitizedWrite(char system, char channel, float linearValue)
    {
        if (_port == null) return;
        
        var sanitized01 = Clamp01(linearValue / 10000f);
        var i = (int)MathF.Floor(sanitized01 * 9999);
        try
        {
            _port.WriteLine($"{system}{channel}{i:0000}");
        }
        catch (Exception e)
        {
            _ready = false;
            Console.WriteLine($"Exception returned, will close and abandon port: {e.Message}");
            var port = _port;
            _port = null;
            port.Close();
        }
    }

    private float Clamp01(float value)
    {
        if (value < 0) return 0;
        if (value > 1) return 1;
        return value;
    }
}
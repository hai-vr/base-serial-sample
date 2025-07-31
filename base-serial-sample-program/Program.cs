using System.Globalization;
using System.Threading;
using Hai.PositionSystemToExternalProgram.ExampleApp;
using Hai.PositionSystemToExternalProgram.ExampleApp.Serial;

namespace Hai.BaseSerial.SampleProgram;

internal class MainApp
{
    private const string AppTitle = "BaseSerialSampleProgram";
    
    private readonly Routine _routine;
    private readonly UiMainApplication _uiMain;
    private readonly HVWindowRendering _windowRendering;
    private readonly Thread _uiThread;

    public static void Main()
    {
        new MainApp().Run();
    }

    private MainApp()
    {
        var serial = new TcodeSerial();
        _routine = new Routine(serial);
        
        _uiMain = new UiMainApplication(new UiActions(_routine));
        
        _windowRendering = new HVWindowRendering(false, 256, 256, AppTitle);
        _windowRendering.OnSubmitUi += _uiMain.SubmitUi;
        
        _uiThread = new Thread(o =>
        {
            _windowRendering.UiLoop();
            WhenWindowClosed();
        })
        {
            CurrentCulture = CultureInfo.InvariantCulture, // We don't want locale-specific numbers
            CurrentUICulture = CultureInfo.InvariantCulture,
            Name = "UI-Thread"
        };
    }

    private void Run()
    {
        _uiThread.Start();
        _routine.MainLoop();
    }

    private void WhenWindowClosed()
    {
        _routine.Finish();
    }
}
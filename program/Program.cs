using System.Globalization;
using Hai.PositionSystemToExternalProgram.Configuration;
using Hai.PositionSystemToExternalProgram.Core;
using Hai.PositionSystemToExternalProgram.Extractors.OVR;
using Hai.PositionSystemToExternalProgram.Tcode;
using Hai.PositionSystemToExternalProgram.Extractors.GDI;
using Hai.PositionSystemToExternalProgram.Processors;
using Hai.PositionSystemToExternalProgram.ApplicationLoop;

namespace Hai.PositionSystemToExternalProgram.Program;

internal class MainApp
{
    private const string AppTitle = "Position System to External Program";
    
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
        var config = SavedData.OpenConfig();
        
        var serial = new TcodeSerial();
        var ovrStarter = new OpenVrStarter();
        var ovrExtractor = new OpenVrExtractor(ovrStarter);
        var windowGdiExtractor = new WindowGdiExtractor();
        var layout = new PositionSystemDataLayout();
        var toBits = new OversizedToBitsTransformer(layout);
        var decoder = new ExtractedDataDecoder();
        var interpreter = new DpsLightInterpreter();
        
        // Core
        _routine = new Routine(serial, ovrStarter, ovrExtractor, windowGdiExtractor, config, toBits, decoder, layout, interpreter);
        
        // UI
        _uiMain = new UiMainApplication(new UiActions(_routine), config);
        
        _windowRendering = new HVWindowRendering(false, 1024, 1024, AppTitle);
        _windowRendering.OnSubmitUi += _uiMain.SubmitUi;
        
        _uiThread = new Thread(_ =>
        {
            _uiMain.Initialize();
            _windowRendering.UiLoop(); // Blocking call. Exits when window closes.
            
            // When window closes, The following is called:
            _routine.Finish();
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
        _routine.MainLoop(); // Blocking call. Exits due to routine.Finish() being called when the UI thread is about to terminate, see above.
    }
}
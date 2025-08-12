using System.Globalization;
using Hai.PositionSystemToExternalProgram.Actions;
using Hai.PositionSystemToExternalProgram.Configuration;
using Hai.PositionSystemToExternalProgram.Core;
using Hai.PositionSystemToExternalProgram.Extractors.OVR;
using Hai.PositionSystemToExternalProgram.Tcode;
using Hai.PositionSystemToExternalProgram.Extractors.GDI;
using Hai.PositionSystemToExternalProgram.Decoder;
using Hai.PositionSystemToExternalProgram.ApplicationLoop;
using Hai.PositionSystemToExternalProgram.ImGuiProgram;
using Hai.PositionSystemToExternalProgram.Robotics;
using Hai.PositionSystemToExternalProgram.Services.Websockets;
using Hai.PositionSystemToExternalProgram.ThirdPartyLicenses;

namespace Hai.PositionSystemToExternalProgram.Program;

internal class MainApp
{
    private const string AppTitle = "Position System";
    
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
        new ThirdParty();
        
        var config = SavedData.OpenConfig();
        
        Localization.InitializeAndProvideFor(config.locale);
        
        var serial = new TcodeSerial();
        var ovrStarter = new OpenVrStarter();
        var ovrExtractor = new OpenVrExtractor(ovrStarter);
        var windowGdiExtractor = new WindowGdiExtractor();
        var layout = new PositionSystemDataLayout();
        var toBits = new BitsTransformer(layout);
        var decoder = new ExtractedDataDecoder();
        
        var interpreter = new DpsLightInterpreter();
        var roboticsDriver = new RoboticsDriver();
        
        // Core
        _routine = new Routine(config, layout, ovrStarter, ovrExtractor, windowGdiExtractor, toBits, decoder, interpreter, roboticsDriver, serial);
        
        // Misc
        var websockets = new WebsocketsStarter(new WebsocketActions(_routine));
        _routine.OnWebsocketStartRequested += websockets.Start;
        _routine.OnWebsocketStopRequested += websockets.Stop;
        
        // UI
        _uiMain = new UiMainApplication(new UiActions(_routine), config);
        
        _windowRendering = new HVWindowRendering(false, 1024, 1024, AppTitle);
        _windowRendering.OnSubmitUi += _uiMain.SubmitUi;
        
        _uiThread = new Thread(_ =>
        {
            _uiMain.Initialize();
            _windowRendering.UiLoop(); // Blocking call. Exits when the window closes.
            
            // When the window closes, we ask the application to exit.
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
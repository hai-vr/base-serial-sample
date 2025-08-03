using System.Globalization;
using Hai.HView.Data;
using Hai.PositionSystemToExternalProgram.Core;
using Hai.PositionSystemToExternalProgram.Extractors.OVR;
using Hai.PositionSystemToExternalProgram.ExampleApp;
using Hai.PositionSystemToExternalProgram.ExampleApp.Serial;
using Hai.PositionSystemToExternalProgram.Extractor.OVR;
using Hai.PositionSystemToExternalProgram.Extractors.GDI;
using Hai.PositionSystemToExternalProgram.Processors;

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
        var config = SavedData.OpenConfig();
        
        var serial = new TcodeSerial();
        var ovrStarter = new OpenVrStarter();
        var ovrExtractor = new OpenVrExtractor(ovrStarter);
        var windowGdiExtractor = new WindowGdiExtractor();
        var toBits = new OversizedToBitsTransformer(new PositionSystemDataLayout());
        var decoder = new ExtractedDataDecoder();
        _routine = new Routine(serial, ovrStarter, ovrExtractor, windowGdiExtractor, config, toBits, decoder);
        
        _uiMain = new UiMainApplication(new UiActions(_routine), config);
        
        _windowRendering = new HVWindowRendering(false, 1024, 1024, AppTitle);
        _windowRendering.OnSubmitUi += _uiMain.SubmitUi;
        
        _uiThread = new Thread(o =>
        {
            _uiMain.Initialize();
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
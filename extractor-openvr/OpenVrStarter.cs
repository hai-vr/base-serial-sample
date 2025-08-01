using System.Runtime.InteropServices;
using Valve.VR;

namespace Hai.PositionSystemToExternalProgram.Extractor.OVR;

public class OpenVrStarter
{
    private static readonly uint SizeOfVrEvent = (uint)Marshal.SizeOf(typeof(VREvent_t));
    
    private bool _ready;
    public event StartedEvent OnStarted;
    public delegate void StartedEvent();
    
    public event ExitedEvent OnExited;
    public delegate void ExitedEvent();

    /// Try to start OpenVR. If it's already started, this returns true.<br/>
    /// Otherwise, this tries to initialize OpenVR. If it succeeds, this returns true. Otherwise, this returns false.
    public bool TryStart()
    {
        if (_ready) return true;
        
        // We start as a Background app, so that it doesn't try to start SteamVR if it's not running.
        EVRInitError err = EVRInitError.None;
        OpenVR.Init(ref err, EVRApplicationType.VRApplication_Background);
        var isStarted = err == EVRInitError.None;
        if (isStarted)
        {
            _ready = true;
            OnStarted?.Invoke();
            return true;
        }

        return false;
    }

    public void PollVrEvents()
    {
        if (!_ready) return;
        
        VREvent_t evt = default;
        while (OpenVR.System.PollNextEvent(ref evt, SizeOfVrEvent))
        {
            var type = (EVREventType)evt.eventType;
            
            // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
            switch (type)
            {
                case EVREventType.VREvent_Quit:
                {
                    ExitRequested();
                    return; // Don't bother processing more events.
                }
            }
        }
    }

    private void ExitRequested()
    {
        OpenVR.Shutdown();
        _ready = false;
        
        OnExited?.Invoke();
    }
}
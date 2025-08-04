using System.Diagnostics;
using ImGuiNET;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

namespace Hai.PositionSystemToExternalProgram.Program;

public class HVWindowRendering
{
    public event SubmitUi OnSubmitUi;
    public delegate void SubmitUi(CustomImGuiController controller, Sdl2Window window);
    
    private const int RefreshFramesPerSecondWhenUnfocused = 60;
    private const int RefreshEventPollPerSecondWhenMinimized = 15;
    
    private Sdl2Window _window;
    private GraphicsDevice _gd;
    private CommandList _cl;

    private CustomImGuiController _controller;
    
    private readonly RgbaFloat _transparentClearColor = new(0f, 0f, 0f, 0f);
    private readonly RgbaFloat _debugRedClearColor = new(1f, 0f, 0f, 1f);
    
    private readonly int _windowWidth;
    private readonly int _windowHeight;
    private readonly bool _isWindowlessStyle;
    private readonly string _appTitle;

    public HVWindowRendering(bool isWindowlessStyle, int windowWidth, int windowHeight, string appTitle)
    {
        _isWindowlessStyle = isWindowlessStyle;
        _windowWidth = windowWidth;
        _windowHeight = windowHeight;
        _appTitle = appTitle;
    }
    
    public void UiLoop()
    {
        // Create window, GraphicsDevice, and all resources necessary for the demo.
        var width = _windowWidth;
        var height = _windowHeight;
        VeldridStartup.CreateWindowAndGraphicsDevice(
            new WindowCreateInfo(50, 50, width, height, WindowState.Normal, $"{_appTitle}"),
            new GraphicsDeviceOptions(true, null, true, ResourceBindingModel.Improved, true, true),
            out _window,
            out _gd);
        if (_isWindowlessStyle)
        {
            _window.Resizable = false;
        }
        _window.Resized += () =>
        {
            _gd.MainSwapchain.Resize((uint)_window.Width, (uint)_window.Height);
            _controller.WindowResized(_window.Width, _window.Height);
        };
        _cl = _gd.ResourceFactory.CreateCommandList();
        _controller = new CustomImGuiController(_gd, _gd.MainSwapchain.Framebuffer.OutputDescription, _window.Width, _window.Height);
        
        var timer = Stopwatch.StartNew();
        timer.Start();
        var stopwatch = Stopwatch.StartNew();
        var deltaTime = 0f;
        // Main application loop
        while (_window.Exists)
        {
            if (!_window.Focused)
            {
                Thread.Sleep(1000 / RefreshFramesPerSecondWhenUnfocused);
            }
            // else: Do not limit framerate.
            
            while (_window.WindowState == WindowState.Minimized)
            {
                Thread.Sleep(1000 / RefreshEventPollPerSecondWhenMinimized);
                
                // TODO: We need to know when the window is no longer minimized.
                // How to properly poll events while minimized?
                _window.PumpEvents();
            }
            
            deltaTime = stopwatch.ElapsedTicks / (float)Stopwatch.Frequency;
            stopwatch.Restart();
            var snapshot = _window.PumpEvents();
            if (!_window.Exists) break;
            _controller.Update(deltaTime,
                snapshot); // Feed the input events to our ImGui controller, which passes them through to ImGui.

            SubmitUI();

            _cl.Begin();
            _cl.SetFramebuffer(_gd.MainSwapchain.Framebuffer);
            DoClearColor();
            _controller.Render(_gd, _cl);
            _cl.End();
            _gd.SubmitCommands(_cl);
            _gd.SwapBuffers(_gd.MainSwapchain);
        }

        // Clean up Veldrid resources
        _gd.WaitForIdle();
        _controller.Dispose();
        _cl.Dispose();
        _gd.Dispose();
    }

    private void SubmitUI()
    {
        OnSubmitUi?.Invoke(_controller, _window);
    }

    private void DoClearColor()
    {
        _cl.ClearColorTarget(0, _transparentClearColor);
    }
}
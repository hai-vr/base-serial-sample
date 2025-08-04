using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Hai.PositionSystemToExternalProgram.Core;

namespace Hai.PositionSystemToExternalProgram.Extractors.GDI;
 
// cnlohr: https://github.com/cnlohr/swadge-vrchat-bridge/blob/db33f403d3dcfe81524320bbf736a78e9c1a169d/bridgeapp/bridgeapp.c
// https://youtu.be/VnZac6lA1_k?t=828
/// Extracts the entire window screen of a process, possibly including title bar and borders of that screen.
public class WindowGdiExtractor
{
    private const bool IsMinimalMode = false; // TODO: Make this true only if the UI is displaying the picture
    
    private readonly bool _ignoreRedAndBlueColors;
    private const int NumberOfColorComponents = 4;
    
    private readonly Stopwatch _time;

    public string desiredWindowName = "";
    
    // Unused debug outputs
    private int _bigWindowWidth;
    private int _bigWindowHeight;
    private byte[] _bigWindowBytes;
    // private int shiftX;
    // private int shiftY;
    // private Vector4 windowRectDebug;
    // private Vector4 clientRectDebug;
    
    private IntPtr _widnowHandle_hwnd;
    private IntPtr _screen;
    private IntPtr _target_hdc;
    private IntPtr _bmp_hgdiobj;
    private GDIBiz.GdiBiz_BITMAPINFO _bitmapInfo;
    // public int getDIBitsReturnValue;
    private bool _hasInitializedAtLeastOnce;
    
    private readonly StringBuilder _buffer = new StringBuilder(2048);
    private long _timeMsUntilNextTryFindWindows;
    private bool _isWindowSizeValid;
    private int _offsetX;
    private int _offsetY;
    private int _width;
    private int _height;
    private int _extractionIteration;
    private byte[] _monochromaticData;
    private byte[] _monochromaticDataB;
    private byte[] _marshalData;
    private byte[] _marshalDataB;

    public WindowGdiExtractor()
    {
        _time = Stopwatch.StartNew();
    }

    public void Start()
    {
        TryFindWindow();
    }

    private void TryFindWindow()
    {
        if (_time.ElapsedMilliseconds < _timeMsUntilNextTryFindWindows) return;
        _timeMsUntilNextTryFindWindows = _time.ElapsedMilliseconds + 5000;
        
        var sw = Stopwatch.StartNew();
        // This can take a long time, like 350ms
        var wnds = WindowNameBiz.FindWindowsWithText(windowName =>
        {
            return windowName.ToLowerInvariant().StartsWith(desiredWindowName.ToLowerInvariant());
        }).ToList();
        Console.WriteLine($"Took {sw.ElapsedMilliseconds}ms to enumerate windows");
        
        if (wnds.Count == 0) return;
        Console.WriteLine($"Found {wnds.Count} windows");
        
        _widnowHandle_hwnd = wnds[0];
        if (_widnowHandle_hwnd != (IntPtr)0)
        {
            GDIBiz.EnumChildWindows(_widnowHandle_hwnd, (hwnd, lparam) =>
            {
                if (hwnd == 0) return true;

                if (GDIBiz.GetClassName(hwnd, _buffer, _buffer.Capacity) != 0)
                {
                    _widnowHandle_hwnd = hwnd;
                    return false;
                }

                return true;
            }, IntPtr.Zero);
            
            _screen = GDIBiz.GetDC(_widnowHandle_hwnd);
            
            if (_target_hdc != IntPtr.Zero) GDIBiz.DeleteDC(_target_hdc);
            _target_hdc = GDIBiz.CreateCompatibleDC(_screen);
        }
    }

    /// Extract the given coordinates from the image.
    /// If the width or height changes, this will result in the instantiation of new arrays, so you should not invoke
    /// this function on the same instance if you need different output sizes. 
    public ExtractionResult Extract(ExtractionCoordinates coordinates)
    {
        if (CheckWindowValidAndUpdateIt())
        {
            // By this point, we have the window size
            var rectangle = coordinates.ToRectangle(_bigWindowWidth, _bigWindowHeight);
            _offsetX = rectangle.X;
            _offsetY = rectangle.Y;
            _width = rectangle.Width;
            _height = rectangle.Height;
            
            var monochromaticDataSize = _height * _width;
            var directXDataSize = _height * _width * 4;
            if (_monochromaticData == null || _monochromaticData.Length != monochromaticDataSize)
            {
                Console.WriteLine("Creating new arrays...");
                _monochromaticData = new byte[monochromaticDataSize];
                _monochromaticDataB = new byte[monochromaticDataSize];
                _marshalData = new byte[directXDataSize];
                _marshalDataB = new byte[directXDataSize];
                for (var i = 0; i < _marshalData.Length; i++)
                {
                    _marshalData[i] = 255;
                    _marshalDataB[i] = 255;
                }
            }
            
            Capture();
            return CopyFromGdiToDirectX();
        }
        else
        {
            TryFindWindow();
            return new ExtractionResult
            {
                Success = false
            };
        }
    }

    private bool CheckWindowValidAndUpdateIt()
    {
        GDIBiz.GetWindowRect(_widnowHandle_hwnd, out GDIBiz.GdiBiz_RECT rect);
        // windowRectDebug = new Vector4(
            // rect.Left,
            // rect.Right,
            // rect.Top,
            // rect.Bottom
        // );

        if (false)
        {
            // This sucks massively, doesn't work on some windows, and also on windows like Notepad you need to get one of the child windows.
            // TODO: Possible handle title bar which height changes while the app is still running (borderless style, etc.)
            GDIBiz.GetClientRect(_widnowHandle_hwnd, out GDIBiz.GdiBiz_RECT clientRect);
            // clientRectDebug = new Vector4(
            // clientRect.Left,
            // clientRect.Right,
            // clientRect.Top,
            // clientRect.Bottom
            // );
        }
            
        var theoricalWidth = rect.Right - rect.Left;
        var theoricalHeight = rect.Bottom - rect.Top;

        // shiftX = theoricalWidth - clientRect.Right;
        // shiftY = theoricalHeight - clientRect.Bottom;

        var width = theoricalWidth;
        var height = theoricalHeight;

        var isUpdateNeeded = _bigWindowWidth != width || _bigWindowHeight != height;
        if (!isUpdateNeeded) return _isWindowSizeValid;
            
        if (_hasInitializedAtLeastOnce)
        {
            GDIBiz.DeleteObject(_bmp_hgdiobj);
            _hasInitializedAtLeastOnce = false;
        }
        
        _bigWindowWidth = width;
        _bigWindowHeight = height;
        _bigWindowBytes = new byte[_bigWindowWidth * _bigWindowHeight * NumberOfColorComponents];

        _isWindowSizeValid = width != 0 && height != 0;
        if (!_isWindowSizeValid) return false;
            
        _bmp_hgdiobj = GDIBiz.CreateCompatibleBitmap(_screen, _bigWindowWidth, _bigWindowHeight);

        if (!_hasInitializedAtLeastOnce)
        {
            _bitmapInfo = default;
            _bitmapInfo.bmiHeader.biBitCount = 32;
            _bitmapInfo.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(GDIBiz.GdiBiz_BITMAPINFOHEADER));
            _bitmapInfo.bmiHeader.biCompression = GDIBiz.BI_RGB;
            _bitmapInfo.bmiHeader.biPlanes = 1;
            _bitmapInfo.bmiHeader.biXPelsPerMeter = 0;
            _bitmapInfo.bmiHeader.biYPelsPerMeter = 0;
            _bitmapInfo.bmiHeader.biClrUsed = 0;
            _bitmapInfo.bmiHeader.biClrImportant = 0;
        }
        _bitmapInfo.bmiHeader.biWidth = _bigWindowWidth;
        _bitmapInfo.bmiHeader.biHeight = _bigWindowHeight;
        _bitmapInfo.bmiHeader.biSizeImage = (uint)(_bigWindowWidth * _bigWindowHeight * NumberOfColorComponents); // must be DWORD aligned
                
        _hasInitializedAtLeastOnce = true;

        return true;
    }

    private void Capture()
    {
        GDIBiz.SelectObject(_target_hdc, _bmp_hgdiobj);
        GDIBiz.PrintWindow(_widnowHandle_hwnd, _target_hdc, 2);
        // TODO Crop, so that we start at kshiftY (3rd arg would be kshiftY), and end at kshiftY + desiredHeight (4th arg would be desiredHeight)
        _ = GDIBiz.GetDIBits(_target_hdc, _bmp_hgdiobj, 0, (uint)_bigWindowHeight, _bigWindowBytes, ref _bitmapInfo, GDIBiz.DIB_RGB_COLORS);
    }

    private ExtractionResult CopyFromGdiToDirectX()
    {
        // We write in B, because A was returned in the last iteration, and the UI thread might be currently reading it.
        // This should give enough time for the UI thread to finish reading the data in A;
        // it's unlikely we're doing two iterations while the UI thread is still reading it.
        for (var scratchY = 0; scratchY < _height; scratchY++)
        {
            for (var scratchX = 0; scratchX < _width; scratchX++)
            {
                var monochromaticScratchIndex = ((int)_height - scratchY - 1) * _width + scratchX;
                var directxScratchIndex = (((int)_height - scratchY - 1) * (int)_width + scratchX) * NumberOfColorComponents;

                var sampleX = scratchX + _offsetX;
                var sampleY = scratchY - _offsetY + _bigWindowHeight - _height; // FIXME: I don't understand
                if (sampleX >= 0 && sampleX < _bigWindowWidth && sampleY >= 0 && sampleY < _bigWindowHeight)
                {
                    var bigWindowBytesSampleIndex = (sampleY * _bigWindowWidth + sampleX) * NumberOfColorComponents;
                    if (monochromaticScratchIndex > _monochromaticDataB.Length)
                    {
                        Console.WriteLine($"Outside bounds ({monochromaticScratchIndex} of {_monochromaticDataB.Length})");
                    }
                    
                    _monochromaticDataB[monochromaticScratchIndex] = _bigWindowBytes[bigWindowBytesSampleIndex + 1]; // Sample from green
                    if (IsMinimalMode)
                    {
                        _marshalDataB[directxScratchIndex + 1] = _bigWindowBytes[bigWindowBytesSampleIndex + 1];
                    }
                    else
                    {
                        // Doing this is not strictly necessary, as it's only for the benefit of the UI being able to display it for debug/alignment purposes.
                        _marshalDataB[directxScratchIndex] = _bigWindowBytes[bigWindowBytesSampleIndex + 2];
                        _marshalDataB[directxScratchIndex + 1] = _bigWindowBytes[bigWindowBytesSampleIndex + 1];
                        _marshalDataB[directxScratchIndex + 2] = _bigWindowBytes[bigWindowBytesSampleIndex];
                    }
                }
                else
                {
                    if (IsMinimalMode)
                    {
                        _marshalDataB[directxScratchIndex + 1] = 128;
                    }
                    else
                    {
                        _marshalDataB[directxScratchIndex] = 128;
                        _marshalDataB[directxScratchIndex + 1] = 128;
                        _marshalDataB[directxScratchIndex + 2] = 128;
                    }
                }
            }
        }

        (_monochromaticData, _monochromaticDataB) = (_monochromaticDataB, _monochromaticData);
        (_marshalData, _marshalDataB) = (_marshalDataB, _marshalData);
        _extractionIteration++;
        
        return new ExtractionResult
        {
            Success = true,
            MonochromaticData = _monochromaticData,
            ColorData = _marshalData,
            Width = _width,
            Height = _height,
            Iteration = _extractionIteration
        };
    }

    private static void Reset(int dstIndex, bool minimalMode, byte[] dataScratchBytes)
    {
        if (minimalMode)
        {
            dataScratchBytes[dstIndex + 1] = 128;
        }
        else
        {
            dataScratchBytes[dstIndex] = 128;
            dataScratchBytes[dstIndex + 1] = 128;
            dataScratchBytes[dstIndex + 2] = 128;
            dataScratchBytes[dstIndex + 3] = 255;
        }
    }
}
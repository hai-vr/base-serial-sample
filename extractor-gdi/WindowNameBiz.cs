using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Hai.PositionSystemToExternalProgram.Extractors.GDI;

public static class WindowNameBiz
{
    private const int WM_GETTEXT = 0x000D;
    private const int WM_GETTEXTLENGTH = 0x000E;
    private const uint SMTO_ABORTIFHUNG = 0x0002;
    private const uint TimeoutMs = 1000;

    // There's a function called "GetWindowText" in user32.dll, but as far as I understand it,
    // that function internally calls SendMessage if the window belongs to another process. So, just call SendMessage directly.
    [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, StringBuilder lParam);
    [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SendMessageTimeout(IntPtr hWnd, int msg, int wParam, StringBuilder lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);
    [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SendMessageTimeout(IntPtr hWnd, int msg, int wParam, int lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

    /// Return true to continue searching, false to halt enumeration (e.g. when found first result).
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    public static IEnumerable<IntPtr> FindWindowsWithText(Predicate<string> predicateFn)
    {
        return EnumerateWindows((wnd, _) => 
        {
            var sw = Stopwatch.StartNew();
            var windowText = GetWindowText(wnd);
            if (sw.ElapsedMilliseconds > 3)
            {
                Console.WriteLine($"Took {sw.ElapsedMilliseconds}ms to get window text of {windowText}");
            }
            
            return predicateFn(windowText);
        });
    }

    private static IEnumerable<IntPtr> EnumerateWindows(EnumWindowsProc passProc)
    {
        var results = new List<IntPtr>();
        EnumWindows((wnd, param) =>
        {
            if (passProc(wnd, param))
            {
                results.Add(wnd);
            }

            return true; // true to continue searching
        }, IntPtr.Zero);

        return results;
    }

    public static string GetWindowText(IntPtr hWnd)
    {
        SendMessageTimeout(hWnd, WM_GETTEXTLENGTH, 0, 0, SMTO_ABORTIFHUNG, TimeoutMs, out var sizeOut);
        var size = (int)sizeOut;
        if (size <= 0) return "";
        
        var sb = new StringBuilder(size + 1);
        SendMessageTimeout(hWnd, WM_GETTEXT, sb.Capacity, sb, SMTO_ABORTIFHUNG, TimeoutMs, out _);
            
        return sb.ToString();
    }
}
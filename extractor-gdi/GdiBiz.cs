using System.Runtime.InteropServices;
using System.Text;

namespace Hai.PositionSystemToExternalProgram.Extractors.GDI;

internal class GDIBiz
{
    internal const int BI_RGB = 0;
    internal const int DIB_RGB_COLORS = 0;

    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool GetWindowRect(IntPtr hWnd, out GdiBiz_RECT lpRect);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool GetClientRect(IntPtr hWnd, out GdiBiz_RECT lpRect);
    // [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool ClientToScreen(IntPtr hWnd, out GdiBiz_POINT lpPoint);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);
    [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr hdc); // Release with gdi32_DeleteDC
    [DllImport("gdi32.dll")] public static extern IntPtr DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] public static extern int GetDIBits(IntPtr hdc, IntPtr hbm, uint start, uint cLines, [Out] byte[] lpvBits, ref GdiBiz_BITMAPINFO bmi, uint usage);
    [DllImport("gdi32.dll", SetLastError = true)] internal static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight); // Release with gdi32_DeleteObject
    [DllImport("gdi32.dll")] public static extern IntPtr DeleteObject(IntPtr hgdiobj);
    [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool EnumChildWindows(IntPtr hwnd, WindowEnumProc func, IntPtr lParam);
    internal delegate bool WindowEnumProc(IntPtr hwnd, IntPtr lparam);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)] public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [StructLayout(LayoutKind.Sequential)]
    public struct GdiBiz_RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    // [StructLayout(LayoutKind.Sequential)]
    // public struct GdiBiz_POINT
    // {
    //     public int X;
    //     public int Y;
    // }

    [StructLayout(LayoutKind.Sequential)]
    public struct GdiBiz_BITMAPINFO
    {
        public GdiBiz_BITMAPINFOHEADER bmiHeader;
        public GdiBiz_RGBQUAD bmiColors;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GdiBiz_BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GdiBiz_RGBQUAD
    {
        public byte rgbBlue;
        public byte rgbGreen;
        public byte rgbRed;
        public byte rgbReserved;
    }
}
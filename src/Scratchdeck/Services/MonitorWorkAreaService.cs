using System.Runtime.InteropServices;
using System.Windows;

namespace Scratchdeck.Services;

internal static class MonitorWorkAreaService
{
    private const uint MonitorDefaultToNearest = 2;

    public static Rect GetNearestWorkingArea(double left, double top, double width, double height)
    {
        var centre = double.IsNaN(left) || double.IsNaN(top)
            ? new NativePoint(0, 0)
            : new NativePoint(
                (int)Math.Round(left + width / 2),
                (int)Math.Round(top + height / 2));

        var monitor = MonitorFromPoint(centre, MonitorDefaultToNearest);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
        {
            return SystemParameters.WorkArea;
        }

        var scaleX = 1d;
        var scaleY = 1d;
        try
        {
            if (GetDpiForMonitor(monitor, 0, out var dpiX, out var dpiY) == 0)
            {
                scaleX = Math.Max(1, dpiX / 96d);
                scaleY = Math.Max(1, dpiY / 96d);
            }
        }
        catch (DllNotFoundException)
        {
            // Windows versions supported by .NET 10 normally expose shcore.dll.
        }

        return new Rect(
            info.WorkArea.Left / scaleX,
            info.WorkArea.Top / scaleY,
            info.WorkArea.Right / scaleX - info.WorkArea.Left / scaleX,
            info.WorkArea.Bottom / scaleY - info.WorkArea.Top / scaleY);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct NativePoint(int X, int Y);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect MonitorArea;
        public NativeRect WorkArea;
        public uint Flags;
    }
}

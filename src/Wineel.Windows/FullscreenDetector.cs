namespace Wineel;

public sealed class FullscreenDetector
{
    public bool IsExclusiveLikeFullscreen()
    {
        var foreground = Native.GetForegroundWindow();
        if (foreground == 0 || !Native.GetWindowRect(foreground, out var rect)) return false;
        var center = new Native.Point { X = rect.Left + rect.Width / 2, Y = rect.Top + rect.Height / 2 };
        var monitor = Native.MonitorFromPoint(center, Native.MonitorDefaulttonearest);
        var info = new Native.MonitorInfo { Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Native.MonitorInfo>() };
        if (!Native.GetMonitorInfo(monitor, ref info)) return false;
        return Math.Abs(rect.Left - info.Monitor.Left) <= 1 && Math.Abs(rect.Top - info.Monitor.Top) <= 1
               && Math.Abs(rect.Right - info.Monitor.Right) <= 1 && Math.Abs(rect.Bottom - info.Monitor.Bottom) <= 1;
    }
}

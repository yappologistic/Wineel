namespace Wineel;

public readonly record struct MonitorSnapshot(
    nint Handle,
    int Left,
    int Top,
    int Width,
    int Height,
    int WorkLeft,
    int WorkTop,
    int WorkWidth,
    int WorkHeight,
    double DpiX,
    double DpiY);

public interface IWindowSnapshotProvider
{
    IReadOnlyList<WindowCandidate> Enumerate(AppSettings settings);
}

public interface IWindowActivator
{
    bool Activate(nint hwnd);
}

public sealed class PointerMonitorService
{
    public MonitorSnapshot GetCurrent()
    {
        if (!Windows.Win32.PInvoke.GetCursorPos(out var cursor)) throw new InvalidOperationException("GetCursorPos failed.");
        var nativePoint = new Native.Point { X = cursor.X, Y = cursor.Y };
        var monitor = Native.MonitorFromPoint(nativePoint, Native.MonitorDefaulttonearest);
        var info = new Native.MonitorInfo { Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Native.MonitorInfo>() };
        if (monitor == 0 || !Native.GetMonitorInfo(monitor, ref info)) throw new InvalidOperationException("Unable to resolve the pointer monitor.");
        var dpiX = 96u;
        var dpiY = 96u;
        _ = Native.GetDpiForMonitor(monitor, Native.MdteffectiveDpi, out dpiX, out dpiY);
        return new MonitorSnapshot(monitor, info.Monitor.Left, info.Monitor.Top, info.Monitor.Width, info.Monitor.Height,
            info.Work.Left, info.Work.Top, info.Work.Width, info.Work.Height, dpiX, dpiY);
    }

    public LogicalPoint GetCursorOnMonitor(MonitorSnapshot monitor)
    {
        if (!Windows.Win32.PInvoke.GetCursorPos(out var cursor)) return new LogicalPoint(monitor.Width / 2d, monitor.Height / 2d);
        return new LogicalPoint(
            DpiMath.PixelsToDips(cursor.X - monitor.Left, monitor.DpiX),
            DpiMath.PixelsToDips(cursor.Y - monitor.Top, monitor.DpiY));
    }
}

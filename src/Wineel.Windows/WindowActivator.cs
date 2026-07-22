namespace Wineel;

public sealed class WindowActivator : IWindowActivator
{
    public bool Activate(nint hwnd)
    {
        if (hwnd == 0 || !Native.IsWindow(hwnd)) return false;
        if (Native.IsIconic(hwnd)) _ = Native.ShowWindowAsync(hwnd, Native.SwRestore);
        if (Native.SetForegroundWindow(hwnd)) return true;

        var foreground = Native.GetForegroundWindow();
        var foregroundThread = foreground == 0 ? 0 : Native.GetWindowThreadProcessId(foreground, out _);
        var targetThread = Native.GetWindowThreadProcessId(hwnd, out _);
        var currentThread = Native.GetCurrentThreadId();
        var attachedForeground = false;
        var attachedTarget = false;
        try
        {
            if (foregroundThread != 0 && foregroundThread != currentThread)
                attachedForeground = Native.AttachThreadInput(currentThread, foregroundThread, true);
            if (targetThread != 0 && targetThread != currentThread)
                attachedTarget = Native.AttachThreadInput(currentThread, targetThread, true);
            _ = Native.BringWindowToTop(hwnd);
            return Native.SetForegroundWindow(hwnd);
        }
        finally
        {
            if (attachedTarget) _ = Native.AttachThreadInput(currentThread, targetThread, false);
            if (attachedForeground) _ = Native.AttachThreadInput(currentThread, foregroundThread, false);
        }
    }
}

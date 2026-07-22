using System.Text;

namespace Wineel;

public static class ForegroundApplicationInfo
{
    private static readonly HashSet<string> ShellSurfaceClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "NotifyIconOverflowWindow",
        "DV2ControlHost", "Windows.UI.Core.CoreWindow",
    };

    public static string? GetExecutablePath() => GetExecutablePath(Native.GetForegroundWindow());

    public static string? GetExecutablePath(nint hwnd)
    {
        if (hwnd == 0) return null;
        _ = Native.GetWindowThreadProcessId(hwnd, out var processId);
        var process = Native.OpenProcess(Native.ProcessQueryLimitedInformation, false, processId);
        if (process == 0) return null;
        try
        {
            var capacity = 32768u;
            var path = new StringBuilder((int)capacity);
            return Native.QueryFullProcessImageName(process, 0, path, ref capacity) ? path.ToString() : null;
        }
        finally { _ = Native.CloseHandle(process); }
    }

    public static bool IsShellSurface(nint hwnd)
    {
        if (hwnd == 0) return false;
        var className = new StringBuilder(256);
        _ = Native.GetClassName(hwnd, className, className.Capacity);
        return ShellSurfaceClasses.Contains(className.ToString());
    }
}

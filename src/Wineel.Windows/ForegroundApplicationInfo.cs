using System.Text;

namespace Wineel;

public static class ForegroundApplicationInfo
{
    public static string? GetExecutablePath()
    {
        var hwnd = Native.GetForegroundWindow();
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
}

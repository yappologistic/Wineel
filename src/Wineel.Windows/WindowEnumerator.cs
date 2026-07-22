using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Wineel;

public sealed class WindowEnumerator : IWindowSnapshotProvider
{
    private static readonly HashSet<string> ShellClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd",
        "NotifyIconOverflowWindow", "DV2ControlHost", "MultitaskingViewFrame",
        "XamlExplorerHostIslandWindow", "Windows.UI.Core.CoreWindow",
    };

    private readonly VirtualDesktopService _virtualDesktops;
    private readonly int _ownProcessId = Environment.ProcessId;

    public WindowEnumerator(VirtualDesktopService virtualDesktops) => _virtualDesktops = virtualDesktops;

    public IReadOnlyList<WindowCandidate> Enumerate(AppSettings settings)
    {
        var windows = new List<WindowCandidate>();
        var exclusions = settings.Exclusions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        Native.EnumWindows((hwnd, _) =>
        {
            try
            {
                var candidate = TryCreateCandidate(hwnd, settings.CurrentVirtualDesktopOnly);
                if (candidate is not null && !exclusions.Contains(candidate.ExecutablePath) && !exclusions.Contains(candidate.Identity))
                    windows.Add(candidate);
            }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception)
            {
                RollingFileLogger.Instance.Warning($"Skipped window 0x{hwnd:X}: {exception.GetType().Name}");
            }
            return true;
        }, 0);
        return windows;
    }

    internal WindowCandidate? TryCreateCandidate(nint hwnd, bool currentDesktopOnly)
    {
        _ = Native.GetWindowThreadProcessId(hwnd, out var processId);
        var isOwn = processId == _ownProcessId;
        var className = GetClassName(hwnd);
        var isShell = hwnd == Native.GetShellWindow() || ShellClasses.Contains(className);
        var visible = Native.IsWindowVisible(hwnd);
        var minimized = Native.IsIconic(hwnd);
        var validBounds = Native.GetWindowRect(hwnd, out var bounds) && bounds.Width > 0 && bounds.Height > 0;
        _ = Native.DwmGetWindowAttribute(hwnd, Native.DwmwaCloaked, out var cloaked, sizeof(int));
        var exStyle = Native.GetWindowLongPtr(hwnd, Native.GwlExstyle).ToInt64();
        var tool = (exStyle & Native.WsExToolwindow) != 0;
        var appWindow = (exStyle & Native.WsExAppwindow) != 0;
        var popup = ResolveLastActivePopup(hwnd);

        var metadata = new WindowMetadata(visible, minimized, cloaked != 0, tool, appWindow, isOwn, validBounds, isShell, popup == hwnd);
        if (!WindowEligibility.IsEligible(metadata)) return null;
        if (currentDesktopOnly && !_virtualDesktops.IsOnCurrentDesktop(hwnd)) return null;

        var path = GetProcessPath(processId);
        if (string.IsNullOrWhiteSpace(path)) return null;
        var identity = ResolveIdentity(processId, path);
        var title = GetWindowTitle(hwnd);
        var displayName = ResolveDisplayName(path, title);
        return new WindowCandidate(hwnd, (int)processId, identity, path, displayName, title, minimized, DateTimeOffset.UtcNow);
    }

    private static nint ResolveLastActivePopup(nint hwnd)
    {
        var popup = Native.GetAncestor(hwnd, Native.GaRootowner);
        if (popup == 0) popup = hwnd;
        for (var guard = 0; guard < 16; guard++)
        {
            var next = Native.GetLastActivePopup(popup);
            if (next == 0 || next == popup) break;
            if (Native.IsWindowVisible(next)) return next;
            popup = next;
        }
        return popup;
    }

    private static string GetClassName(nint hwnd)
    {
        var text = new StringBuilder(256);
        _ = Native.GetClassName(hwnd, text, text.Capacity);
        return text.ToString();
    }

    private static string GetWindowTitle(nint hwnd)
    {
        var length = Math.Clamp(Native.GetWindowTextLength(hwnd), 0, 4096);
        var text = new StringBuilder(length + 1);
        _ = Native.GetWindowText(hwnd, text, text.Capacity);
        return text.ToString();
    }

    private static string GetProcessPath(uint processId)
    {
        var process = Native.OpenProcess(Native.ProcessQueryLimitedInformation, false, processId);
        if (process == 0) return string.Empty;
        try
        {
            var size = 32768u;
            var path = new StringBuilder((int)size);
            return Native.QueryFullProcessImageName(process, 0, path, ref size) ? path.ToString() : string.Empty;
        }
        finally { _ = Native.CloseHandle(process); }
    }

    private static string ResolveIdentity(uint processId, string path)
    {
        var process = Native.OpenProcess(Native.ProcessQueryLimitedInformation, false, processId);
        if (process != 0)
        {
            try
            {
                var length = 0u;
                var result = Native.GetPackageFullName(process, ref length, null);
                if (result == 122 && length > 0)
                {
                    var fullName = new StringBuilder((int)length);
                    if (Native.GetPackageFullName(process, ref length, fullName) == 0)
                    {
                        var familyLength = 0u;
                        _ = Native.PackageFamilyNameFromFullName(fullName.ToString(), ref familyLength, null);
                        if (familyLength > 0)
                        {
                            var family = new StringBuilder((int)familyLength);
                            if (Native.PackageFamilyNameFromFullName(fullName.ToString(), ref familyLength, family) == 0)
                                return $"package:{family}";
                        }
                    }
                }
            }
            finally { _ = Native.CloseHandle(process); }
        }

        try { return $"exe:{Path.GetFullPath(path).Trim().ToUpperInvariant()}"; }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException) { return $"exe:{path.ToUpperInvariant()}"; }
    }

    private static string ResolveDisplayName(string path, string title)
    {
        try
        {
            var version = FileVersionInfo.GetVersionInfo(path);
            if (!string.IsNullOrWhiteSpace(version.ProductName)) return version.ProductName.Trim();
            if (!string.IsNullOrWhiteSpace(version.FileDescription)) return version.FileDescription.Trim();
        }
        catch (Exception exception) when (exception is FileNotFoundException or System.ComponentModel.Win32Exception or ArgumentException) { }
        var filename = Path.GetFileNameWithoutExtension(path);
        return !string.IsNullOrWhiteSpace(filename) ? filename : (!string.IsNullOrWhiteSpace(title) ? title : "Application");
    }
}

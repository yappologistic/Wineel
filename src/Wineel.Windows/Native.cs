using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace Wineel;

internal static class Native
{
    internal const int WhKeyboardLl = 13;
    internal const int WhMouseLl = 14;
    internal const int HcAction = 0;
    internal const int WmKeyDown = 0x0100;
    internal const int WmKeyUp = 0x0101;
    internal const int WmSysKeyDown = 0x0104;
    internal const int WmSysKeyUp = 0x0105;
    internal const int WmMouseWheel = 0x020A;
    internal const int WmMouseHWheel = 0x020E;
    internal const int WmHotKey = 0x0312;
    internal const int WmGetIcon = 0x007F;
    internal const int IconSmall2 = 2;
    internal const int IconBig = 1;
    internal const int VkTab = 0x09;
    internal const int VkReturn = 0x0D;
    internal const int VkEscape = 0x1B;
    internal const int VkSpace = 0x20;
    internal const int VkLeft = 0x25;
    internal const int VkUp = 0x26;
    internal const int VkRight = 0x27;
    internal const int VkDown = 0x28;
    internal const int VkShift = 0x10;
    internal const int VkLShift = 0xA0;
    internal const int VkRShift = 0xA1;
    internal const int VkMenu = 0x12;
    internal const int VkLMenu = 0xA4;
    internal const int VkRMenu = 0xA5;
    internal const int VkControl = 0x11;
    internal const int LlkhfAltdown = 0x20;
    internal const uint EventSystemForeground = 0x0003;
    internal const uint WineventOutofcontext = 0x0000;
    internal const uint WineventSkipownprocess = 0x0002;
    internal const int GwlExstyle = -20;
    internal const long WsExToolwindow = 0x00000080L;
    internal const long WsExAppwindow = 0x00040000L;
    internal const long WsExNoactivate = 0x08000000L;
    internal const long WsExLayered = 0x00080000L;
    internal const int GaRootowner = 3;
    internal const int DwmwaCloaked = 14;
    internal const int GclpHicon = -14;
    internal const int GclpHiconsm = -34;
    internal const uint SmtoAbortifhung = 0x0002;
    internal const int SwRestore = 9;
    internal const int SwShownoactivate = 4;
    internal const uint SwpNoactivate = 0x0010;
    internal const uint SwpShowwindow = 0x0040;
    internal static readonly nint HwndTopmost = new(-1);
    internal const uint MonitorDefaulttonearest = 2;
    internal const int MdteffectiveDpi = 0;
    internal const uint ShgfiIcon = 0x000000100;
    internal const uint ShgfiLargeicon = 0x000000000;
    internal const uint ProcessQueryLimitedInformation = 0x1000;
    internal const uint ModAlt = 0x0001;
    internal const uint ModControl = 0x0002;
    internal const uint ModShift = 0x0004;
    internal const uint ModNorepeat = 0x4000;

    internal delegate nint HookProc(int code, nint wParam, nint lParam);
    internal delegate bool EnumWindowsProc(nint hwnd, nint lParam);
    internal delegate void WinEventProc(nint hook, uint eventType, nint hwnd, int objectId, int childId, uint eventThread, uint eventTime);

    [StructLayout(LayoutKind.Sequential)]
    internal struct KbdLlHookStruct { public uint VkCode; public uint ScanCode; public uint Flags; public uint Time; public nuint ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Point { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct MsLlHookStruct { public Point Pt; public uint MouseData; public uint Flags; public uint Time; public nuint ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect { public int Left; public int Top; public int Right; public int Bottom; public int Width => Right - Left; public int Height => Bottom - Top; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct MonitorInfo { public uint Size; public Rect Monitor; public Rect Work; public uint Flags; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct ShFileInfo { public nint Icon; public int IconIndex; public uint Attributes; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName; }

    [DllImport("user32.dll", SetLastError = true)] internal static extern nint SetWindowsHookEx(int idHook, HookProc callback, nint module, uint threadId);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] internal static extern nint CallNextHookEx(nint hook, int code, nint wParam, nint lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] internal static extern nint GetModuleHandle(string? moduleName);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int virtualKey);
    [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumWindowsProc callback, nint lParam);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsIconic(nint hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] internal static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll")] internal static extern nint GetClassLongPtr(nint hwnd, int index);
    [DllImport("user32.dll")] internal static extern nint GetAncestor(nint hwnd, int flags);
    [DllImport("user32.dll")] internal static extern nint GetLastActivePopup(nint hwnd);
    [DllImport("user32.dll")] internal static extern nint GetShellWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetClassName(nint hwnd, StringBuilder className, int maxCount);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetWindowText(nint hwnd, StringBuilder text, int maxCount);
    [DllImport("user32.dll")] internal static extern int GetWindowTextLength(nint hwnd);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint hwnd, out uint processId);
    [DllImport("dwmapi.dll")] internal static extern int DwmGetWindowAttribute(nint hwnd, int attribute, out int value, int size);
    [DllImport("kernel32.dll", SetLastError = true)] internal static extern nint OpenProcess(uint access, bool inherit, uint processId);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern bool QueryFullProcessImageName(nint process, uint flags, StringBuilder name, ref uint size);
    [DllImport("kernel32.dll")] internal static extern bool CloseHandle(nint handle);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] internal static extern int GetPackageFullName(nint process, ref uint length, StringBuilder? fullName);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] internal static extern int PackageFamilyNameFromFullName(string packageFullName, ref uint length, StringBuilder? familyName);
    [DllImport("user32.dll", SetLastError = true)] internal static extern nint SetWinEventHook(uint eventMin, uint eventMax, nint module, WinEventProc callback, uint processId, uint threadId, uint flags);
    [DllImport("user32.dll")] internal static extern bool UnhookWinEvent(nint hook);
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern bool ShowWindowAsync(nint hwnd, int command);
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool BringWindowToTop(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool AttachThreadInput(uint attach, uint attachTo, bool attachValue);
    [DllImport("kernel32.dll")] internal static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] internal static extern nint MonitorFromPoint(Point point, uint flags);
    [DllImport("user32.dll")] internal static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("shcore.dll")] internal static extern int GetDpiForMonitor(nint monitor, int type, out uint dpiX, out uint dpiY);
    [DllImport("user32.dll")] internal static extern nint SendMessageTimeout(nint hwnd, uint message, nuint wParam, nint lParam, uint flags, uint timeout, out nuint result);
    [DllImport("user32.dll")] internal static extern nint CopyIcon(nint icon);
    [DllImport("user32.dll")] internal static extern bool DestroyIcon(nint icon);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern uint PrivateExtractIcons(string fileName, int iconIndex, int iconWidth, int iconHeight, [Out] nint[] icons, [Out] uint[] iconIds, uint iconCount, uint flags);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] internal static extern nint SHGetFileInfo(string path, uint attributes, ref ShFileInfo info, uint size, uint flags);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint virtualKey);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool UnregisterHotKey(nint hwnd, int id);

    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "Windows-only assembly")]
    internal static bool IsKeyDown(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;
}

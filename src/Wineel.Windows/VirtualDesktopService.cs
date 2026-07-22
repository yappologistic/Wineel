using System.Runtime.InteropServices;

namespace Wineel;

public sealed class VirtualDesktopService
{
    private readonly IVirtualDesktopManager? _manager;

    public VirtualDesktopService()
    {
        try
        {
            var type = Type.GetTypeFromCLSID(new Guid("AA509086-5CA9-4C25-8F95-589D3C07B48A"), throwOnError: true)!;
            _manager = (IVirtualDesktopManager)Activator.CreateInstance(type)!;
        }
        catch (COMException) { }
    }

    public bool IsOnCurrentDesktop(nint hwnd)
    {
        if (_manager is null) return true;
        try { return _manager.IsWindowOnCurrentVirtualDesktop(hwnd); }
        catch (COMException) { return true; }
    }

    [ComImport, Guid("A5CD92FF-29BE-454C-8D04-D82879FB3F1B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IVirtualDesktopManager
    {
        bool IsWindowOnCurrentVirtualDesktop(nint topLevelWindow);
        Guid GetWindowDesktopId(nint topLevelWindow);
        void MoveWindowToDesktop(nint topLevelWindow, [In] ref Guid desktopId);
    }
}

using System.Runtime.InteropServices;

namespace Wineel;

public sealed class MouseHookService : IDisposable
{
    private readonly Native.HookProc _callback;
    private readonly Action<int> _postWheel;
    private nint _hook;

    public MouseHookService(Action<int> postWheel)
    {
        _postWheel = postWheel;
        _callback = HookCallback;
    }

    public bool Install()
    {
        if (_hook != 0) return true;
        _hook = Native.SetWindowsHookEx(Native.WhMouseLl, _callback, Native.GetModuleHandle(null), 0);
        return _hook != 0;
    }

    private nint HookCallback(int code, nint wParam, nint lParam)
    {
        if (code == Native.HcAction && wParam.ToInt32() is Native.WmMouseWheel or Native.WmMouseHWheel)
        {
            var data = Marshal.PtrToStructure<Native.MsLlHookStruct>(lParam);
            var delta = unchecked((short)(data.MouseData >> 16));
            _postWheel(delta);
            return 1;
        }
        return Native.CallNextHookEx(_hook, code, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hook != 0) _ = Native.UnhookWindowsHookEx(_hook);
        _hook = 0;
        GC.KeepAlive(_callback);
    }
}

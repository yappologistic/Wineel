using System.Runtime.InteropServices;

namespace Wineel;

public enum KeyboardHookCommand { BeginAlt, Next, Previous, Commit, Cancel, AltReleased, Select0, Select1, Select2, Select3, Select4, Select5, Select6, Select7, Select8, Select9 }

public sealed class KeyboardHookService : IDisposable
{
    private readonly Native.HookProc _callback;
    private readonly Func<bool> _canStart;
    private readonly Action<KeyboardHookCommand> _post;
    private nint _hook;
    private volatile bool _sessionActive;
    private volatile bool _commitOnAltRelease;
    private bool _leftAlt;
    private bool _rightAlt;

    public KeyboardHookService(Func<bool> canStart, Action<KeyboardHookCommand> post)
    {
        _canStart = canStart;
        _post = post;
        _callback = HookCallback;
    }

    public bool Install()
    {
        if (_hook != 0) return true;
        _hook = Native.SetWindowsHookEx(Native.WhKeyboardLl, _callback, Native.GetModuleHandle(null), 0);
        return _hook != 0;
    }

    public void SetSessionActive(bool active, bool commitOnAltRelease = false)
    {
        _sessionActive = active;
        _commitOnAltRelease = active && commitOnAltRelease;
    }

    private nint HookCallback(int code, nint wParam, nint lParam)
    {
        if (code != Native.HcAction) return Native.CallNextHookEx(_hook, code, wParam, lParam);
        var message = wParam.ToInt32();
        var keyDown = message is Native.WmKeyDown or Native.WmSysKeyDown;
        var keyUp = message is Native.WmKeyUp or Native.WmSysKeyUp;
        if (!keyDown && !keyUp) return Native.CallNextHookEx(_hook, code, wParam, lParam);
        var data = Marshal.PtrToStructure<Native.KbdLlHookStruct>(lParam);
        var key = (int)data.VkCode;

        if (key is Native.VkLMenu or Native.VkMenu) _leftAlt = keyDown;
        if (key == Native.VkRMenu) _rightAlt = keyDown;

        if (keyUp && key is Native.VkLMenu or Native.VkRMenu or Native.VkMenu && !_leftAlt && !_rightAlt && _sessionActive && _commitOnAltRelease)
        {
            Post(KeyboardHookCommand.AltReleased);
            return 1;
        }

        if (!keyDown) return Native.CallNextHookEx(_hook, code, wParam, lParam);
        var altDown = _leftAlt || _rightAlt || (data.Flags & Native.LlkhfAltdown) != 0 || Native.IsKeyDown(Native.VkMenu);
        var shiftDown = Native.IsKeyDown(Native.VkShift) || Native.IsKeyDown(Native.VkLShift) || Native.IsKeyDown(Native.VkRShift);

        if (!_sessionActive && key == Native.VkTab && altDown && _canStart())
        {
            _sessionActive = true;
            Post(KeyboardHookCommand.BeginAlt);
            return 1;
        }

        if (!_sessionActive) return Native.CallNextHookEx(_hook, code, wParam, lParam);
        var command = key switch
        {
            Native.VkTab => shiftDown ? KeyboardHookCommand.Previous : KeyboardHookCommand.Next,
            Native.VkLeft or Native.VkUp => KeyboardHookCommand.Previous,
            Native.VkRight or Native.VkDown => KeyboardHookCommand.Next,
            Native.VkReturn => KeyboardHookCommand.Commit,
            Native.VkEscape => KeyboardHookCommand.Cancel,
            >= 0x30 and <= 0x39 => (KeyboardHookCommand)((int)KeyboardHookCommand.Select0 + (key - 0x30)),
            _ => (KeyboardHookCommand)(-1),
        };
        if ((int)command < 0) return Native.CallNextHookEx(_hook, code, wParam, lParam);
        Post(command);
        return 1;
    }

    private void Post(KeyboardHookCommand command) => _post(command);

    public void Dispose()
    {
        if (_hook != 0) _ = Native.UnhookWindowsHookEx(_hook);
        _hook = 0;
        _sessionActive = false;
        _commitOnAltRelease = false;
        GC.KeepAlive(_callback);
    }
}

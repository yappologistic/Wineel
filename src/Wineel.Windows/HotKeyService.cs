using System.Windows.Interop;

namespace Wineel;

public sealed class HotKeyService : IDisposable
{
    private const int Id = 0x574E;
    private HwndSource? _source;
    private uint _registeredModifiers;
    private uint _registeredKey;
    private bool _isRegistered;
    public event Action? Pressed;

    public bool Register(string shortcut)
    {
        EnsureWindow();
        if (!TryParse(shortcut, out var modifiers, out var key)) return false;
        if (_isRegistered && modifiers == _registeredModifiers && key == _registeredKey) return true;

        var previousModifiers = _registeredModifiers;
        var previousKey = _registeredKey;
        var hadPrevious = _isRegistered;
        if (hadPrevious) _ = Native.UnregisterHotKey(_source!.Handle, Id);
        _isRegistered = false;

        if (Native.RegisterHotKey(_source!.Handle, Id, modifiers | Native.ModNorepeat, key))
        {
            _registeredModifiers = modifiers;
            _registeredKey = key;
            _isRegistered = true;
            return true;
        }

        if (hadPrevious && Native.RegisterHotKey(_source.Handle, Id, previousModifiers | Native.ModNorepeat, previousKey))
        {
            _registeredModifiers = previousModifiers;
            _registeredKey = previousKey;
            _isRegistered = true;
        }
        return false;
    }

    private void EnsureWindow()
    {
        if (_source is not null) return;
        var parameters = new HwndSourceParameters("Wineel.HotKey") { Width = 0, Height = 0, WindowStyle = unchecked((int)0x80000000) };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    private nint WndProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == Native.WmHotKey && wParam.ToInt32() == Id)
        {
            handled = true;
            Pressed?.Invoke();
        }
        return 0;
    }

    public static bool TryParse(string text, out uint modifiers, out uint key)
    {
        modifiers = 0;
        key = 0;
        foreach (var rawPart in text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var part = rawPart.ToUpperInvariant();
            if (part is "CTRL" or "CONTROL") modifiers |= Native.ModControl;
            else if (part == "ALT") modifiers |= Native.ModAlt;
            else if (part == "SHIFT") modifiers |= Native.ModShift;
            else if (part == "SPACE")
            {
                if (key != 0) return false;
                key = Native.VkSpace;
            }
            else if (part.Length == 1 && char.IsLetterOrDigit(part[0]))
            {
                if (key != 0) return false;
                key = part[0];
            }
            else return false;
        }
        return key != 0 && modifiers != 0;
    }

    public void Dispose()
    {
        if (_source is not null && _isRegistered) _ = Native.UnregisterHotKey(_source.Handle, Id);
        _source?.Dispose();
        _source = null;
        _isRegistered = false;
        _registeredModifiers = 0;
        _registeredKey = 0;
    }
}

using System.Windows.Interop;

namespace Wineel;

public sealed class HotKeyService : IDisposable
{
    private const int Id = 0x574E;
    private HwndSource? _source;
    public event Action? Pressed;

    public bool Register(string shortcut)
    {
        EnsureWindow();
        _ = Native.UnregisterHotKey(_source!.Handle, Id);
        if (!TryParse(shortcut, out var modifiers, out var key)) return false;
        return Native.RegisterHotKey(_source.Handle, Id, modifiers | Native.ModNorepeat, key);
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
            else if (part == "SPACE") key = Native.VkSpace;
            else if (part.Length == 1 && char.IsLetterOrDigit(part[0])) key = part[0];
            else return false;
        }
        return key != 0 && modifiers != 0;
    }

    public void Dispose()
    {
        if (_source is not null) _ = Native.UnregisterHotKey(_source.Handle, Id);
        _source?.Dispose();
        _source = null;
    }
}

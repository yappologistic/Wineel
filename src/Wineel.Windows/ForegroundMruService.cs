namespace Wineel;

public sealed class ForegroundMruService : IDisposable
{
    private readonly Native.WinEventProc _callback;
    private nint _hook;
    public MruList Mru { get; } = new();
    public event Action<nint>? ForegroundChanged;

    public ForegroundMruService()
    {
        _callback = OnWinEvent;
        var current = Native.GetForegroundWindow();
        if (current != 0) Mru.Observe(current);
    }

    public bool Start()
    {
        if (_hook != 0) return true;
        _hook = Native.SetWinEventHook(Native.EventSystemForeground, Native.EventSystemForeground, 0, _callback, 0, 0,
            Native.WineventOutofcontext | Native.WineventSkipownprocess);
        return _hook != 0;
    }

    private void OnWinEvent(nint hook, uint eventType, nint hwnd, int objectId, int childId, uint eventThread, uint eventTime)
    {
        if (hwnd == 0 || !Native.IsWindow(hwnd)) return;
        Mru.Observe(hwnd);
        ForegroundChanged?.Invoke(hwnd);
    }

    public void Dispose()
    {
        if (_hook != 0) _ = Native.UnhookWinEvent(_hook);
        _hook = 0;
        GC.KeepAlive(_callback);
    }
}

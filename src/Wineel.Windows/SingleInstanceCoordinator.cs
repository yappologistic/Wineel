namespace Wineel;

public sealed class SingleInstanceCoordinator : IDisposable
{
    private const string MutexName = @"Local\Wineel.SingleInstance";
    private const string EventName = @"Local\Wineel.ShowSettings";
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _signal;
    private readonly CancellationTokenSource _cancellation = new();
    private Task? _listener;

    public SingleInstanceCoordinator()
    {
        _mutex = new Mutex(true, MutexName, out var created);
        IsPrimary = created;
        _signal = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
    }

    public bool IsPrimary { get; }

    public void SignalPrimary() => _signal.Set();

    public void Listen(Action callback)
    {
        if (!IsPrimary || _listener is not null) return;
        _listener = Task.Run(() =>
        {
            while (!_cancellation.IsCancellationRequested)
            {
                if (_signal.WaitOne(500) && !_cancellation.IsCancellationRequested) callback();
            }
        });
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        _signal.Set();
        try { _listener?.Wait(1000); } catch (AggregateException) { }
        _signal.Dispose();
        if (IsPrimary) _mutex.ReleaseMutex();
        _mutex.Dispose();
        _cancellation.Dispose();
    }
}

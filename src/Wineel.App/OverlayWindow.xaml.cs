using System.Windows;
using System.Windows.Interop;

namespace Wineel;

public partial class OverlayWindow : Window
{
    private nint _handle;
    public event Action<int>? ItemClicked;
    public event Action<int>? ItemSelected;
    public event Action<int>? ItemContextRequested;
    public event Action? OutsideClicked;

    public OverlayWindow()
    {
        InitializeComponent();
        Renderer.ItemClicked += index => ItemClicked?.Invoke(index);
        Renderer.ItemSelected += index => ItemSelected?.Invoke(index);
        Renderer.ItemContextRequested += index => ItemContextRequested?.Invoke(index);
        Renderer.OutsideClicked += () => OutsideClicked?.Invoke();
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs eventArgs)
    {
        _handle = new WindowInteropHelper(this).Handle;
        var style = Native.GetWindowLongPtr(_handle, Native.GwlExstyle).ToInt64();
        _ = SetWindowLongPtr(_handle, Native.GwlExstyle, new nint(style | Native.WsExToolwindow | Native.WsExNoactivate | Native.WsExLayered));
    }

    public void ShowSession(MonitorSnapshot monitor, IReadOnlyList<VisualSwitcherItem> items, int selectedIndex, LogicalPoint cursor, AppSettings settings, string status = "")
    {
        if (!IsVisible) Show();
        _handle = _handle == 0 ? new WindowInteropHelper(this).Handle : _handle;
        var widthDip = DpiMath.PixelsToDips(monitor.Width, monitor.DpiX);
        var heightDip = DpiMath.PixelsToDips(monitor.Height, monitor.DpiY);
        Width = widthDip;
        Height = heightDip;
        var radius = settings.WheelSize / 2;
        var work = new LogicalRect(
            DpiMath.PixelsToDips(monitor.WorkLeft - monitor.Left, monitor.DpiX),
            DpiMath.PixelsToDips(monitor.WorkTop - monitor.Top, monitor.DpiY),
            DpiMath.PixelsToDips(monitor.WorkWidth, monitor.DpiX),
            DpiMath.PixelsToDips(monitor.WorkHeight, monitor.DpiY));
        var desiredCenter = settings.WheelAnchor == WheelAnchorMode.Pointer
            ? cursor
            : new LogicalPoint(work.X + work.Width / 2, work.Y + work.Height / 2);
        var center = MonitorPlacement.ClampWheelCenter(desiredCenter, work, radius, 28);
        Renderer.SetSession(items, selectedIndex, center, settings, status);
        _ = Native.SetWindowPos(_handle, Native.HwndTopmost, monitor.Left, monitor.Top, monitor.Width, monitor.Height,
            Native.SwpNoactivate | Native.SwpShowwindow);
        _ = ShowWindow(_handle, Native.SwShownoactivate);
    }

    public void SetSelection(int selectedIndex) => Renderer.SetSelection(selectedIndex);
    public void UpdateSession(IReadOnlyList<VisualSwitcherItem> items, int selectedIndex, string status) => Renderer.UpdateSession(items, selectedIndex, status);

    public void CloseSession()
    {
        Renderer.ClearSession();
        Hide();
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool ShowWindow(nint hwnd, int command);
}

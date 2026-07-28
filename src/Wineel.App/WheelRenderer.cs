using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Automation.Peers;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Brushes = System.Windows.Media.Brushes;
using FontFamily = System.Windows.Media.FontFamily;
using WpfSystemColors = System.Windows.SystemColors;
using WpfFlowDirection = System.Windows.FlowDirection;

namespace Wineel;

public sealed class WheelRenderer : FrameworkElement
{
    private readonly Stopwatch _animation = new();
    private readonly Dictionary<int, Rect> _hitRects = new();
    private IReadOnlyList<VisualSwitcherItem> _items = Array.Empty<VisualSwitcherItem>();
    private AppSettings _settings = new();
    private int _selectedIndex;
    private int _previousSelectedIndex = -1;
    private LogicalPoint _center;
    private double _openProgress;
    private bool _renderSubscribed;
    private bool _sessionVisible;
    private string _status = string.Empty;

    public event Action<int>? ItemClicked;
    public event Action<int>? ItemSelected;
    public event Action<int>? ItemContextRequested;
    public event Action? OutsideClicked;

    public WheelRenderer() => RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);

    public void SetSession(IReadOnlyList<VisualSwitcherItem> items, int selectedIndex, LogicalPoint center, AppSettings settings, string status = "")
    {
        _sessionVisible = true;
        _items = items;
        _selectedIndex = selectedIndex;
        _previousSelectedIndex = -1;
        _center = center;
        _settings = settings;
        _status = status;
        _openProgress = IsReducedMotion ? 1 : 0;
        BeginAnimation();
        InvalidateVisual();
    }

    public void UpdateSession(IReadOnlyList<VisualSwitcherItem> items, int selectedIndex, string status)
    {
        _items = items;
        _selectedIndex = selectedIndex;
        _previousSelectedIndex = -1;
        _status = status;
        RaiseAutomationSelectionChanged();
        InvalidateVisual();
    }

    public void SetSelection(int selectedIndex)
    {
        if (selectedIndex == _selectedIndex) return;
        _previousSelectedIndex = _selectedIndex;
        _selectedIndex = selectedIndex;
        RaiseAutomationSelectionChanged();
        BeginAnimation();
    }

    public void ClearSession()
    {
        _items = Array.Empty<VisualSwitcherItem>();
        _sessionVisible = false;
        _status = string.Empty;
        _hitRects.Clear();
        StopAnimation();
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawing)
    {
        base.OnRender(drawing);
        if (!_sessionVisible) return;
        _hitRects.Clear();
        var wheel = _settings.WheelSize;
        var plateRadius = wheel / 2;
        var ringRadius = Math.Min(plateRadius - _settings.IconSize * 0.65, wheel * 0.405);
        var center = new Point(_center.X, _center.Y);
        var highContrast = SystemParameters.HighContrast;
        var visualPolicy = WheelVisualPolicy.Resolve(_settings, highContrast, SystemParameters.ClientAreaAnimation);
        var darkTheme = highContrast
            ? WpfSystemColors.WindowColor.R + WpfSystemColors.WindowColor.G + WpfSystemColors.WindowColor.B < 384
            : ThemeService.IsDark(_settings.ThemeMode);
        var foreground = highContrast ? WpfSystemColors.WindowTextColor : darkTheme ? Color.FromRgb(242, 242, 244) : Color.FromRgb(30, 30, 32);
        var openScale = 0.94 + 0.06 * EaseOut(_openProgress);
        drawing.PushOpacity(Math.Clamp(_openProgress, 0.01, 1));
        drawing.PushTransform(new ScaleTransform(openScale, openScale, center.X, center.Y));

        var visibleCapacity = WheelCapacity.Calculate(wheel, _settings.IconSize, _settings.MaximumVisibleIcons);
        var slots = RadialLayout.Calculate(_items.Count, _selectedIndex, visibleCapacity, _center, ringRadius);
        var selectedSlot = slots.FirstOrDefault(slot => slot.ItemIndex == _selectedIndex);
        System.Windows.Media.Brush plateFill = highContrast
            ? new SolidColorBrush(WpfSystemColors.WindowColor)
            : CreateNeutralAcrylicBrush(visualPolicy.PlateOpacity, darkTheme);
        plateFill.Freeze();
        if (!highContrast)
            drawing.DrawEllipse(new SolidColorBrush(Color.FromArgb(42, 0, 0, 0)), null, new Point(center.X, center.Y + 3), plateRadius + 5, plateRadius + 5);
        var rim = highContrast ? WpfSystemColors.WindowTextColor : darkTheme ? Color.FromArgb(118, 192, 195, 200) : Color.FromArgb(130, 96, 96, 100);
        drawing.DrawEllipse(plateFill, new Pen(new SolidColorBrush(rim), 1.25), center, plateRadius, plateRadius);
        drawing.DrawEllipse(new SolidColorBrush(foreground), null, center, 5.25, 5.25);

        var animationProgress = IsReducedMotion ? 1 : EaseOut(Math.Min(1, _animation.Elapsed.TotalMilliseconds / SelectionDuration));
        foreach (var slot in slots)
        {
            var visual = _items[slot.ItemIndex];
            var isSelected = slot.ItemIndex == _selectedIndex;
            var wasSelected = slot.ItemIndex == _previousSelectedIndex;
            var selectedScale = isSelected ? Lerp(1, 1.12, animationProgress) : wasSelected ? Lerp(1.12, 1, animationProgress) : 1;
            var size = _settings.IconSize * selectedScale;
            var iconRect = new Rect(slot.Center.X - size / 2, slot.Center.Y - size / 2, size, size);
            if (isSelected)
            {
                var accent = highContrast ? WpfSystemColors.HighlightColor : Color.FromRgb(241, 185, 74);
                var glowAlpha = (byte)Math.Clamp(visualPolicy.SelectionGlow * 180, 0, 100);
                drawing.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(glowAlpha, accent.R, accent.G, accent.B)), null, Inflate(iconRect, 14), 18, 18);
                drawing.DrawRoundedRectangle(new SolidColorBrush(darkTheme ? Color.FromArgb(220, 24, 26, 29) : Color.FromArgb(235, 255, 255, 255)), new Pen(new SolidColorBrush(accent), 3), Inflate(iconRect, 8), 15, 15);
            }
            drawing.DrawImage(visual.Icon, iconRect);
            _hitRects[slot.ItemIndex] = Inflate(iconRect, 16);
            if (_settings.ShowNumberBadges && ShortcutBadges.ForViewportIndex(slot.ViewportIndex) is { } badge)
                DrawBadge(drawing, badge, iconRect.Right + 2, iconRect.Bottom - 10);
            if (visual.Item.WindowCount > 1)
                DrawCount(drawing, visual.Item.WindowCount, iconRect.Left - 3, iconRect.Bottom - 9);
            if (visual.IsPinned)
                DrawPin(drawing, iconRect.Left + 2, iconRect.Top + 3);
        }

        if (_items.Count > 0 && _selectedIndex >= 0 && _selectedIndex < _items.Count)
            DrawCenterInfo(drawing, _items[_selectedIndex].Item.DisplayName, $"{_selectedIndex + 1} of {_items.Count}", _status, center.X, center.Y, darkTheme, foreground, _settings.ShowLabels);
        else if (!string.IsNullOrWhiteSpace(_status))
            DrawStatus(drawing, _status, center.X, center.Y - 10, darkTheme, foreground);

        drawing.Pop();
        drawing.Pop();
    }

    private static SolidColorBrush CreateNeutralAcrylicBrush(double opacity, bool darkTheme)
    {
        var baseColor = darkTheme ? Color.FromRgb(27, 30, 34) : Color.FromRgb(246, 246, 248);
        return new SolidColorBrush(Color.FromArgb((byte)(Math.Clamp(opacity, 0.55, 0.98) * 255), baseColor.R, baseColor.G, baseColor.B));
    }

    private static void DrawCenterInfo(DrawingContext drawing, string label, string position, string status, double x, double y, bool darkTheme, Color foreground, bool showLabel)
    {
        var title = showLabel ? (label.Length > 28 ? string.Concat(label.AsSpan(0, 25), "…") : label) : "Wineel";
        var titleText = Text(title, 16, FontWeights.SemiBold, foreground);
        var positionText = Text(position, 12, FontWeights.Normal, Color.FromArgb(205, foreground.R, foreground.G, foreground.B));
        var statusText = Text(status, 11, FontWeights.Normal, Color.FromArgb(190, foreground.R, foreground.G, foreground.B));
        var width = Math.Max(titleText.Width, Math.Max(positionText.Width, statusText.Width)) + 30;
        var height = titleText.Height + positionText.Height + statusText.Height + 20;
        var rect = new Rect(x - width / 2, y - height / 2, width, height);
        var fill = darkTheme ? Color.FromArgb(218, 18, 20, 23) : Color.FromArgb(235, 255, 255, 255);
        var border = darkTheme ? Color.FromArgb(65, 255, 255, 255) : Color.FromArgb(60, 0, 0, 0);
        drawing.DrawRoundedRectangle(new SolidColorBrush(fill), new Pen(new SolidColorBrush(border), 1), rect, 14, 14);
        drawing.DrawText(titleText, new Point(x - titleText.Width / 2, rect.Top + 8));
        drawing.DrawText(positionText, new Point(x - positionText.Width / 2, rect.Top + 8 + titleText.Height));
        drawing.DrawText(statusText, new Point(x - statusText.Width / 2, rect.Top + 10 + titleText.Height + positionText.Height));
    }

    private static void DrawBadge(DrawingContext drawing, string text, double x, double y)
    {
        drawing.DrawEllipse(new SolidColorBrush(Color.FromArgb(230, 12, 12, 14)), new Pen(new SolidColorBrush(Color.FromArgb(140, 255, 255, 255)), 1), new Point(x, y), 11, 11);
        var formatted = Text(text, 12, FontWeights.SemiBold);
        drawing.DrawText(formatted, new Point(x - formatted.Width / 2, y - formatted.Height / 2));
    }

    private static void DrawCount(DrawingContext drawing, int count, double x, double y)
    {
        var value = count > 99 ? "99+" : count.ToString(CultureInfo.InvariantCulture);
        var formatted = Text(value, 10, FontWeights.Bold);
        var rect = new Rect(x - formatted.Width / 2 - 5, y - 9, formatted.Width + 10, 18);
        drawing.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(235, 38, 38, 42)), new Pen(new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)), 1), rect, 8, 8);
        drawing.DrawText(formatted, new Point(rect.X + 5, rect.Y + 2));
    }

    private static void DrawPin(DrawingContext drawing, double x, double y)
    {
        drawing.DrawEllipse(new SolidColorBrush(Color.FromArgb(235, 232, 184, 72)), new Pen(new SolidColorBrush(Color.FromArgb(180, 255, 245, 205)), 1), new Point(x, y), 8, 8);
        var formatted = Text("P", 8, FontWeights.Bold, Color.FromRgb(26, 24, 18));
        drawing.DrawText(formatted, new Point(x - formatted.Width / 2, y - formatted.Height / 2));
    }

    private static void DrawStatus(DrawingContext drawing, string status, double x, double y, bool darkTheme, Color foreground)
    {
        var display = status.Length > 46 ? string.Concat(status.AsSpan(0, 43), "…") : status;
        var formatted = Text(display, 12, FontWeights.SemiBold, foreground);
        var rect = new Rect(x - formatted.Width / 2 - 10, y, formatted.Width + 20, formatted.Height + 7);
        var fill = darkTheme ? Color.FromArgb(190, 18, 18, 20) : Color.FromArgb(205, 255, 255, 255);
        var border = darkTheme ? Color.FromArgb(48, 255, 255, 255) : Color.FromArgb(52, 0, 0, 0);
        drawing.DrawRoundedRectangle(new SolidColorBrush(fill), new Pen(new SolidColorBrush(border), 1), rect, 9, 9);
        drawing.DrawText(formatted, new Point(rect.X + 10, rect.Y + 3.5));
    }

    private static void DrawLabel(DrawingContext drawing, string label, double x, double y, bool darkTheme, Color foreground)
    {
        var display = label.Length > 38 ? string.Concat(label.AsSpan(0, 35), "…") : label;
        var formatted = Text(display, 15, FontWeights.SemiBold, foreground);
        var rect = new Rect(x - formatted.Width / 2 - 13, y, formatted.Width + 26, formatted.Height + 10);
        var fill = darkTheme ? Color.FromArgb(225, 16, 16, 18) : Color.FromArgb(235, 255, 255, 255);
        var border = darkTheme ? Color.FromArgb(65, 255, 255, 255) : Color.FromArgb(60, 0, 0, 0);
        drawing.DrawRoundedRectangle(new SolidColorBrush(fill), new Pen(new SolidColorBrush(border), 1), rect, 10, 10);
        drawing.DrawText(formatted, new Point(rect.X + 13, rect.Y + 5));
    }

    private static void DrawPosition(DrawingContext drawing, string value, double x, double y, Color foreground)
    {
        var formatted = Text(value, 11, FontWeights.Normal, Color.FromArgb(185, foreground.R, foreground.G, foreground.B));
        drawing.DrawText(formatted, new Point(x - formatted.Width / 2, y - formatted.Height / 2));
    }

    private static FormattedText Text(string value, double size, FontWeight weight, Color? color = null) =>
        new(value, CultureInfo.CurrentUICulture, WpfFlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, weight, FontStretches.Normal),
            size, new SolidColorBrush(color ?? Colors.White), 1.0);

    private bool IsReducedMotion => !WheelVisualPolicy.Resolve(_settings, SystemParameters.HighContrast, SystemParameters.ClientAreaAnimation).Animate;
    private double SelectionDuration => Math.Clamp(105 / Math.Max(0.25, _settings.AnimationSpeed), 40, 260);

    private void BeginAnimation()
    {
        _animation.Restart();
        if (_renderSubscribed) return;
        CompositionTarget.Rendering += OnRendering;
        _renderSubscribed = true;
    }

    private void OnRendering(object? sender, EventArgs eventArgs)
    {
        _openProgress = IsReducedMotion ? 1 : Math.Min(1, _openProgress + 0.12 * Math.Max(0.25, _settings.AnimationSpeed));
        InvalidateVisual();
        if (_openProgress >= 1 && _animation.Elapsed.TotalMilliseconds >= SelectionDuration) StopAnimation();
    }

    private void StopAnimation()
    {
        if (_renderSubscribed) CompositionTarget.Rendering -= OnRendering;
        _renderSubscribed = false;
        _animation.Stop();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        var point = e.GetPosition(this);
        foreach (var pair in _hitRects)
        {
            if (!pair.Value.Contains(point)) continue;
            ItemClicked?.Invoke(pair.Key);
            e.Handled = true;
            return;
        }
        OutsideClicked?.Invoke();
        e.Handled = true;
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        var point = e.GetPosition(this);
        foreach (var pair in _hitRects)
        {
            if (!pair.Value.Contains(point)) continue;
            ItemContextRequested?.Invoke(pair.Key);
            e.Handled = true;
            return;
        }
        e.Handled = true;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new WheelRendererAutomationPeer(this);

    internal IReadOnlyList<VisualSwitcherItem> AutomationItems => _items;
    internal int AutomationSelectedIndex => _selectedIndex;
    internal string AutomationStatus => _status;
    internal void AutomationInvoke(int index) => ItemClicked?.Invoke(index);
    internal void AutomationSelect(int index) => ItemSelected?.Invoke(index);
    internal Rect AutomationItemBounds(int index)
    {
        if (!_hitRects.TryGetValue(index, out var bounds) || !IsVisible) return Rect.Empty;
        var topLeft = PointToScreen(bounds.TopLeft);
        var bottomRight = PointToScreen(bounds.BottomRight);
        return new Rect(topLeft, bottomRight);
    }

    private void RaiseAutomationSelectionChanged()
    {
        if (UIElementAutomationPeer.FromElement(this) is WheelRendererAutomationPeer peer)
            peer.RaiseSelectionChanged();
    }

    private static Rect Inflate(Rect rect, double amount) => new(rect.X - amount, rect.Y - amount, rect.Width + amount * 2, rect.Height + amount * 2);
    private static Color ToColor(RgbColor value) => Color.FromRgb(value.R, value.G, value.B);
    private static double Lerp(double from, double to, double progress) => from + (to - from) * progress;
    private static double EaseOut(double value) => 1 - Math.Pow(1 - Math.Clamp(value, 0, 1), 3);
}

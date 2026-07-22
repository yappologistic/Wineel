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
        var openScale = 0.94 + 0.06 * EaseOut(_openProgress);
        drawing.PushOpacity(Math.Clamp(_openProgress, 0.01, 1));
        drawing.PushTransform(new ScaleTransform(openScale, openScale, center.X, center.Y));

        var slots = RadialLayout.Calculate(_items.Count, _selectedIndex, _settings.MaximumVisibleIcons, _center, ringRadius);
        var selectedSlot = slots.FirstOrDefault(slot => slot.ItemIndex == _selectedIndex);
        DrawBeam(drawing, center, selectedSlot, plateRadius);

        System.Windows.Media.Brush plateFill = highContrast
            ? new SolidColorBrush(WpfSystemColors.WindowColor)
            : CreateNeutralAcrylicBrush(_settings.PlateOpacity);
        plateFill.Freeze();
        if (!highContrast)
            drawing.DrawEllipse(new SolidColorBrush(Color.FromArgb(42, 0, 0, 0)), null, new Point(center.X, center.Y + 3), plateRadius + 5, plateRadius + 5);
        drawing.DrawEllipse(plateFill, new Pen(new SolidColorBrush(Color.FromArgb(118, 192, 195, 200)), 1.25), center, plateRadius, plateRadius);
        drawing.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(42, 255, 255, 255)), 1), center, plateRadius - 12, plateRadius - 12);
        drawing.DrawEllipse(new SolidColorBrush(highContrast ? WpfSystemColors.WindowTextColor : Color.FromRgb(242, 242, 244)), null, center, 5.25, 5.25);

        var animationProgress = IsReducedMotion ? 1 : EaseOut(Math.Min(1, _animation.Elapsed.TotalMilliseconds / SelectionDuration));
        foreach (var slot in slots)
        {
            var visual = _items[slot.ItemIndex];
            var isSelected = slot.ItemIndex == _selectedIndex;
            var wasSelected = slot.ItemIndex == _previousSelectedIndex;
            var selectedScale = isSelected ? Lerp(1, 1.34, animationProgress) : wasSelected ? Lerp(1.34, 1, animationProgress) : 1;
            var size = _settings.IconSize * selectedScale;
            var iconRect = new Rect(slot.Center.X - size / 2, slot.Center.Y - size / 2, size, size);
            if (isSelected)
            {
                drawing.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(38, 255, 255, 255)), null, Inflate(iconRect, 22), 22, 22);
                drawing.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(150, 20, 20, 22)), new Pen(Brushes.White, 3), Inflate(iconRect, 10), 18, 18);
                drawing.DrawRoundedRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(170, 255, 255, 255)), 1.2), Inflate(iconRect, 4), 13, 13);
            }
            drawing.DrawImage(visual.Icon, iconRect);
            _hitRects[slot.ItemIndex] = Inflate(iconRect, 16);
            if (_settings.ShowNumberBadges && ShortcutBadges.ForViewportIndex(slot.ViewportIndex) is { } badge)
                DrawBadge(drawing, badge, iconRect.Right + 2, iconRect.Bottom - 10);
            if (visual.Item.WindowCount > 1)
                DrawCount(drawing, visual.Item.WindowCount, iconRect.Left - 3, iconRect.Bottom - 9);
            if (visual.IsPinned)
                DrawPin(drawing, iconRect.Left + 2, iconRect.Top + 3);
            if (isSelected && _settings.ShowLabels) DrawLabel(drawing, visual.Item.DisplayName, slot.Center.X, iconRect.Bottom + 15);
        }

        if (_items.Count > _settings.MaximumVisibleIcons)
            DrawPosition(drawing, $"{_selectedIndex + 1} / {_items.Count}", center.X, center.Y + plateRadius - 29);
        if (!string.IsNullOrWhiteSpace(_status)) DrawStatus(drawing, _status, center.X, center.Y + 17);

        drawing.Pop();
        drawing.Pop();
    }

    private void DrawBeam(DrawingContext drawing, Point center, RadialSlot? selectedSlot, double plateRadius)
    {
        if (selectedSlot is null || _settings.BeamIntensity <= 0) return;
        var accent = ToColor(_items[selectedSlot.ItemIndex].Accent);
        var far = Math.Sqrt(ActualWidth * ActualWidth + ActualHeight * ActualHeight) + plateRadius;
        for (var layer = 4; layer >= 0; layer--)
        {
            var halfAngle = (8 + layer * 3.5) * Math.PI / 180;
            var alpha = (byte)Math.Clamp(_settings.BeamIntensity * 255 / (layer + 2.1), 0, 90);
            var geometry = Wedge(center, selectedSlot.AngleRadians, halfAngle, far);
            drawing.DrawGeometry(new SolidColorBrush(Color.FromArgb(alpha, accent.R, accent.G, accent.B)), null, geometry);
        }
    }

    private static StreamGeometry Wedge(Point center, double angle, double halfAngle, double distance)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(center, true, true);
        context.LineTo(new Point(center.X + Math.Cos(angle - halfAngle) * distance, center.Y + Math.Sin(angle - halfAngle) * distance), true, false);
        context.LineTo(new Point(center.X + Math.Cos(angle + halfAngle) * distance, center.Y + Math.Sin(angle + halfAngle) * distance), true, false);
        geometry.Freeze();
        return geometry;
    }

    private static RadialGradientBrush CreateNeutralAcrylicBrush(double opacity)
    {
        var brush = new RadialGradientBrush
        {
            MappingMode = BrushMappingMode.RelativeToBoundingBox,
            Center = new Point(0.34, 0.25),
            GradientOrigin = new Point(0.24, 0.16),
            RadiusX = 0.92,
            RadiusY = 0.92,
            Opacity = Math.Clamp(opacity, 0.35, 0.95),
        };
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(78, 79, 82), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(42, 43, 46), 0.48));
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(17, 18, 20), 1));
        return brush;
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

    private static void DrawStatus(DrawingContext drawing, string status, double x, double y)
    {
        var display = status.Length > 46 ? string.Concat(status.AsSpan(0, 43), "…") : status;
        var formatted = Text(display, 12, FontWeights.SemiBold, Color.FromArgb(220, 242, 242, 244));
        var rect = new Rect(x - formatted.Width / 2 - 10, y, formatted.Width + 20, formatted.Height + 7);
        drawing.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(180, 18, 18, 20)), new Pen(new SolidColorBrush(Color.FromArgb(48, 255, 255, 255)), 1), rect, 9, 9);
        drawing.DrawText(formatted, new Point(rect.X + 10, rect.Y + 3.5));
    }

    private static void DrawLabel(DrawingContext drawing, string label, double x, double y)
    {
        var display = label.Length > 38 ? string.Concat(label.AsSpan(0, 35), "…") : label;
        var formatted = Text(display, 15, FontWeights.SemiBold);
        var rect = new Rect(x - formatted.Width / 2 - 13, y, formatted.Width + 26, formatted.Height + 10);
        drawing.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(225, 16, 16, 18)), new Pen(new SolidColorBrush(Color.FromArgb(65, 255, 255, 255)), 1), rect, 10, 10);
        drawing.DrawText(formatted, new Point(rect.X + 13, rect.Y + 5));
    }

    private static void DrawPosition(DrawingContext drawing, string value, double x, double y)
    {
        var formatted = Text(value, 11, FontWeights.Normal, Color.FromArgb(185, 238, 238, 240));
        drawing.DrawText(formatted, new Point(x - formatted.Width / 2, y - formatted.Height / 2));
    }

    private static FormattedText Text(string value, double size, FontWeight weight, Color? color = null) =>
        new(value, CultureInfo.CurrentUICulture, WpfFlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, weight, FontStretches.Normal),
            size, new SolidColorBrush(color ?? Colors.White), 1.0);

    private bool IsReducedMotion => _settings.MotionPreference == MotionPreference.Reduced || (_settings.MotionPreference == MotionPreference.FollowWindows && !SystemParameters.ClientAreaAnimation);
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

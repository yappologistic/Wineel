namespace Wineel;

public static class RadialLayout
{
    public static IReadOnlyList<RadialSlot> Calculate(
        int itemCount,
        int selectedIndex,
        int maximumVisible,
        LogicalPoint center,
        double radius)
    {
        if (itemCount <= 0) return Array.Empty<RadialSlot>();
        if (selectedIndex < 0 || selectedIndex >= itemCount) throw new ArgumentOutOfRangeException(nameof(selectedIndex));
        maximumVisible = Math.Clamp(maximumVisible, 1, 12);
        var visibleCount = Math.Min(itemCount, maximumVisible);
        var firstIndex = itemCount <= visibleCount ? 0 : Mod(selectedIndex - (visibleCount / 2), itemCount);
        var result = new List<RadialSlot>(visibleCount);

        for (var slot = 0; slot < visibleCount; slot++)
        {
            var itemIndex = Mod(firstIndex + slot, itemCount);
            var angle = (-Math.PI / 2) + ((Math.PI * 2 * slot) / visibleCount);
            result.Add(new RadialSlot(
                itemIndex,
                slot,
                angle,
                new LogicalPoint(center.X + Math.Cos(angle) * radius, center.Y + Math.Sin(angle) * radius)));
        }

        return result;
    }

    public static int Mod(int value, int modulus) => ((value % modulus) + modulus) % modulus;
}

public static class MonitorPlacement
{
    public static LogicalPoint ClampWheelCenter(LogicalPoint desired, LogicalRect workArea, double ringRadius, double padding)
    {
        var margin = Math.Max(0, ringRadius + padding);
        var minX = workArea.Left + margin;
        var maxX = workArea.Right - margin;
        var minY = workArea.Top + margin;
        var maxY = workArea.Bottom - margin;

        var x = minX > maxX ? workArea.Left + workArea.Width / 2 : Math.Clamp(desired.X, minX, maxX);
        var y = minY > maxY ? workArea.Top + workArea.Height / 2 : Math.Clamp(desired.Y, minY, maxY);
        return new LogicalPoint(x, y);
    }
}

public static class DpiMath
{
    public const double DefaultDpi = 96;
    public static double PixelsToDips(double pixels, double dpi) => pixels * DefaultDpi / Normalize(dpi);
    public static double DipsToPixels(double dips, double dpi) => dips * Normalize(dpi) / DefaultDpi;
    public static double Scale(double dpi) => Normalize(dpi) / DefaultDpi;
    private static double Normalize(double dpi) => dpi > 0 ? dpi : DefaultDpi;
}

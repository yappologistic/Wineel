using Xunit;

namespace Wineel.Tests;

public sealed class RadialMathTests
{
    [Fact]
    public void FirstSlotStartsAtTwelveOClock()
    {
        var slots = RadialLayout.Calculate(4, 0, 12, new LogicalPoint(100, 100), 50);
        Assert.Equal(100, slots[0].Center.X, 6);
        Assert.Equal(50, slots[0].Center.Y, 6);
        Assert.Equal(-Math.PI / 2, slots[0].AngleRadians, 6);
    }

    [Fact]
    public void SlotsProceedClockwise()
    {
        var slots = RadialLayout.Calculate(4, 0, 12, new LogicalPoint(0, 0), 10);
        Assert.True(slots[1].Center.X > 9.9);
        Assert.True(Math.Abs(slots[1].Center.Y) < 0.01);
    }

    [Fact]
    public void LongViewportContainsSelectionAndTwelveItems()
    {
        var slots = RadialLayout.Calculate(23, 19, 12, new LogicalPoint(0, 0), 10);
        Assert.Equal(12, slots.Count);
        Assert.Contains(slots, slot => slot.ItemIndex == 19);
        Assert.Equal(12, slots.Select(slot => slot.ItemIndex).Distinct().Count());
    }

    [Theory]
    [InlineData(-1, 5, 4)]
    [InlineData(5, 5, 0)]
    [InlineData(7, 5, 2)]
    public void ModWrapsBothDirections(int value, int modulus, int expected) => Assert.Equal(expected, RadialLayout.Mod(value, modulus));

    [Fact]
    public void MonitorClampKeepsRingInsideWorkArea()
    {
        var result = MonitorPlacement.ClampWheelCenter(new LogicalPoint(-100, 900), new LogicalRect(0, 0, 1000, 800), 250, 28);
        Assert.Equal(278, result.X);
        Assert.Equal(522, result.Y);
    }

    [Fact]
    public void MonitorClampCentersWhenWorkAreaIsTooSmall()
    {
        var result = MonitorPlacement.ClampWheelCenter(new LogicalPoint(0, 0), new LogicalRect(-300, 50, 400, 300), 250, 28);
        Assert.Equal(-100, result.X);
        Assert.Equal(200, result.Y);
    }

    [Theory]
    [InlineData(96, 150, 150)]
    [InlineData(144, 150, 225)]
    [InlineData(192, 64, 128)]
    public void DpiConversionsAreStable(double dpi, double dips, double pixels)
    {
        Assert.Equal(pixels, DpiMath.DipsToPixels(dips, dpi), 6);
        Assert.Equal(dips, DpiMath.PixelsToDips(pixels, dpi), 6);
    }
}

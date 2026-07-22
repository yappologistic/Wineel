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
        Assert.Equal(19, slots[0].ItemIndex);
        Assert.Equal(-Math.PI / 2, slots[0].AngleRadians, 6);
        Assert.Equal(12, slots.Select(slot => slot.ItemIndex).Distinct().Count());
    }

    [Fact]
    public void SelectionRemainsAtTwelveOClockForShortFilteredLists()
    {
        var slots = RadialLayout.Calculate(5, 3, 12, new LogicalPoint(0, 0), 10);
        Assert.Equal(3, slots[0].ItemIndex);
        Assert.Equal(-Math.PI / 2, slots[0].AngleRadians, 6);
    }

    [Theory]
    [InlineData(400, 64, 12, 8)]
    [InlineData(340, 92, 12, 4)]
    [InlineData(650, 48, 12, 12)]
    [InlineData(400, 64, 6, 6)]
    public void WheelCapacityAvoidsCrowdedLayouts(double wheelSize, double iconSize, int maximum, int expected)
    {
        Assert.Equal(expected, WheelCapacity.Calculate(wheelSize, iconSize, maximum));
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
    [InlineData(120, 160, 200)]
    [InlineData(144, 150, 225)]
    [InlineData(168, 192, 336)]
    [InlineData(192, 64, 128)]
    public void DpiConversionsAreStable(double dpi, double dips, double pixels)
    {
        Assert.Equal(pixels, DpiMath.DipsToPixels(dips, dpi), 6);
        Assert.Equal(dips, DpiMath.PixelsToDips(pixels, dpi), 6);
    }

    [Theory]
    [InlineData(96)]
    [InlineData(120)]
    [InlineData(144)]
    [InlineData(168)]
    [InlineData(192)]
    public void DpiRoundTripSupportsCommonMixedMonitorScales(double dpi)
    {
        const double coordinate = -731.25;
        var pixels = DpiMath.DipsToPixels(coordinate, dpi);
        Assert.Equal(coordinate, DpiMath.PixelsToDips(pixels, dpi), 6);
    }

    [Theory]
    [InlineData(-1920, 0, 1920, 1040, -2200, 900, -1670, 790)]
    [InlineData(0, -1440, 2560, 1400, 2450, -1500, 2310, -1190)]
    [InlineData(-1280, -1024, 1280, 984, -2000, -2000, -1030, -774)]
    public void ClampHandlesNegativeOriginsAndTaskbarWorkAreas(
        double x, double y, double width, double height,
        double desiredX, double desiredY, double expectedX, double expectedY)
    {
        var result = MonitorPlacement.ClampWheelCenter(
            new LogicalPoint(desiredX, desiredY),
            new LogicalRect(x, y, width, height),
            220,
            30);
        Assert.Equal(expectedX, result.X, 6);
        Assert.Equal(expectedY, result.Y, 6);
    }

    [Fact]
    public void ZeroDpiFallsBackToNinetySix()
    {
        Assert.Equal(125, DpiMath.DipsToPixels(125, 0), 6);
        Assert.Equal(125, DpiMath.PixelsToDips(125, -1), 6);
    }
}

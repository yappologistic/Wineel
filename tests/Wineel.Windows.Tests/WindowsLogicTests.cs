using Xunit;

namespace Wineel.Tests;

public sealed class WindowsLogicTests
{
    [Theory]
    [InlineData("Ctrl+Alt+Space", 0x0003u, 0x20u)]
    [InlineData("Ctrl+Shift+W", 0x0006u, 0x57u)]
    public void HotKeyParserAcceptsSupportedShortcuts(string text, uint expectedModifiers, uint expectedKey)
    {
        Assert.True(HotKeyService.TryParse(text, out var modifiers, out var key));
        Assert.Equal(expectedModifiers, modifiers);
        Assert.Equal(expectedKey, key);
    }

    [Theory]
    [InlineData("Ctrl+Space+A")]
    [InlineData("Ctrl+Alt")]
    [InlineData("Ctrl+F12")]
    public void HotKeyParserRejectsAmbiguousOrUnsupportedShortcuts(string text)
    {
        Assert.False(HotKeyService.TryParse(text, out _, out _));
    }

    [Theory]
    [InlineData(120, 1)]
    [InlineData(-120, -1)]
    public void NativeWheelSignMatchesCoreAccumulator(int delta, int expected)
    {
        var accumulator = new WheelDeltaAccumulator();
        Assert.Equal(expected, accumulator.Add(delta));
    }
}

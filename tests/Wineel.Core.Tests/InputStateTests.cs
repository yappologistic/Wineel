using Xunit;

namespace Wineel.Tests;

public sealed class InputStateTests
{
    [Fact]
    public void HighResolutionWheelAccumulatesOneDetent()
    {
        var accumulator = new WheelDeltaAccumulator();
        Assert.Equal(0, accumulator.Add(30));
        Assert.Equal(0, accumulator.Add(30));
        Assert.Equal(0, accumulator.Add(30));
        Assert.Equal(1, accumulator.Add(30));
        Assert.Equal(0, accumulator.Remainder);
    }

    [Fact]
    public void WheelPreservesSignedRemainderAndReverses()
    {
        var accumulator = new WheelDeltaAccumulator();
        Assert.Equal(-1, accumulator.Add(-150));
        Assert.Equal(-30, accumulator.Remainder);
        accumulator.Reset();
        Assert.Equal(1, accumulator.Add(-120, reverse: true));
    }

    [Fact]
    public void StateMachineInitiallySelectsPreviousMruWindow()
    {
        var state = new SwitcherStateMachine();
        var items = Items(3);
        Assert.True(state.TryStart(items, items[0].TargetWindow, SwitcherMode.AltHeld));
        Assert.Equal(1, state.SelectedIndex);
    }

    [Fact]
    public void StateMachineWrapsForwardAndBackward()
    {
        var state = new SwitcherStateMachine();
        var items = Items(3);
        state.TryStart(items, 999, SwitcherMode.Latched);
        state.Handle(SwitcherCommand.Previous);
        Assert.Equal(2, state.SelectedIndex);
        state.Handle(SwitcherCommand.Next);
        Assert.Equal(0, state.SelectedIndex);
    }

    [Fact]
    public void CancelReturnsOriginalWithoutCommit()
    {
        var state = new SwitcherStateMachine();
        state.TryStart(Items(3), 44, SwitcherMode.Latched);
        var result = state.Handle(SwitcherCommand.Cancel);
        Assert.True(result.Closed);
        Assert.False(result.Committed);
        Assert.Equal((nint)44, result.TargetWindow);
    }

    [Fact]
    public void CommitReturnsSelectedTarget()
    {
        var state = new SwitcherStateMachine();
        var items = Items(3);
        state.TryStart(items, items[0].TargetWindow, SwitcherMode.AltHeld);
        var result = state.Handle(SwitcherCommand.Commit);
        Assert.True(result.Committed);
        Assert.Equal(items[1].TargetWindow, result.TargetWindow);
    }

    [Fact]
    public void SingleItemCannotStart() => Assert.False(new SwitcherStateMachine().TryStart(Items(1), 0, SwitcherMode.Latched));

    private static IReadOnlyList<SwitcherItem> Items(int count) => Enumerable.Range(1, count)
        .Select(index => new SwitcherItem($"app-{index}", $"App {index}", new nint[] { index }, index, $"app-{index}", RgbColor.AccentFallback)).ToArray();
}

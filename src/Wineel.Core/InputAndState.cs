namespace Wineel;

public sealed class WheelDeltaAccumulator
{
    public const int StandardDetent = 120;
    private int _remainder;

    public int Add(int delta, bool reverse = false)
    {
        if (reverse) delta = -delta;
        _remainder += delta;
        var steps = _remainder / StandardDetent;
        _remainder -= steps * StandardDetent;
        return steps;
    }

    public void Reset() => _remainder = 0;
    public int Remainder => _remainder;
}

public enum SwitcherCommand
{
    Next,
    Previous,
    Commit,
    Cancel,
}

public sealed record SwitcherResult(bool Consumed, bool SelectionChanged, bool Closed, bool Committed, nint TargetWindow);

public sealed class SwitcherStateMachine
{
    private IReadOnlyList<SwitcherItem> _items = Array.Empty<SwitcherItem>();
    public bool IsActive { get; private set; }
    public SwitcherMode Mode { get; private set; }
    public int SelectedIndex { get; private set; } = -1;
    public nint OriginalForeground { get; private set; }
    public IReadOnlyList<SwitcherItem> Items => _items;

    public bool TryStart(IReadOnlyList<SwitcherItem> items, nint currentForeground, SwitcherMode mode)
    {
        if (IsActive || items.Count < 2) return false;
        _items = items.ToArray();
        OriginalForeground = currentForeground;
        Mode = mode;
        var currentIndex = _items.ToList().FindIndex(item => item.WindowHandles.Contains(currentForeground));
        SelectedIndex = currentIndex >= 0 ? NextValidIndex(currentIndex, 1) : 0;
        IsActive = SelectedIndex >= 0;
        return IsActive;
    }

    public SwitcherResult Handle(SwitcherCommand command, bool wrap = true)
    {
        if (!IsActive) return new(false, false, false, false, 0);

        return command switch
        {
            SwitcherCommand.Next => Move(1, wrap),
            SwitcherCommand.Previous => Move(-1, wrap),
            SwitcherCommand.Commit => Close(true),
            SwitcherCommand.Cancel => Close(false),
            _ => new(false, false, false, false, 0),
        };
    }

    public SwitcherResult SelectVisible(int itemIndex, bool commit)
    {
        if (!IsActive || itemIndex < 0 || itemIndex >= _items.Count) return new(false, false, false, false, 0);
        var changed = SelectedIndex != itemIndex;
        SelectedIndex = itemIndex;
        return commit ? Close(true) : new(true, changed, false, false, 0);
    }

    public SwitcherResult SelectNumber(int visibleNumber, IReadOnlyList<RadialSlot> slots)
    {
        if (!IsActive || visibleNumber < 0 || visibleNumber >= Math.Min(10, slots.Count))
            return new(false, false, false, false, 0);
        var index = slots[visibleNumber].ItemIndex;
        var changed = SelectedIndex != index;
        SelectedIndex = index;
        return new(true, changed, false, false, 0);
    }

    private SwitcherResult Move(int direction, bool wrap)
    {
        var proposed = SelectedIndex + direction;
        if (wrap) proposed = RadialLayout.Mod(proposed, _items.Count);
        else proposed = Math.Clamp(proposed, 0, _items.Count - 1);
        var changed = proposed != SelectedIndex;
        SelectedIndex = proposed;
        return new(true, changed, false, false, 0);
    }

    private int NextValidIndex(int from, int direction) => RadialLayout.Mod(from + direction, _items.Count);

    private SwitcherResult Close(bool commit)
    {
        var target = commit && SelectedIndex >= 0 ? _items[SelectedIndex].TargetWindow : OriginalForeground;
        IsActive = false;
        return new(true, false, true, commit, target);
    }
}

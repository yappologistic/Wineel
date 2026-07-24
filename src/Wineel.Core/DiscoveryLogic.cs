namespace Wineel;

public sealed class MruList
{
    private readonly LinkedList<nint> _order = new();
    private readonly Dictionary<nint, LinkedListNode<nint>> _nodes = new();
    private readonly object _gate = new();

    public void Observe(nint window)
    {
        if (window == 0) return;
        lock (_gate)
        {
            if (_nodes.Remove(window, out var existing)) _order.Remove(existing);
            _nodes[window] = _order.AddFirst(window);
        }
    }

    public IReadOnlyList<nint> Snapshot()
    {
        lock (_gate) return _order.ToArray();
    }

    public void Remove(nint window)
    {
        lock (_gate)
        {
            if (_nodes.Remove(window, out var node)) _order.Remove(node);
        }
    }

    public void Prune(Func<nint, bool> keep)
    {
        ArgumentNullException.ThrowIfNull(keep);
        lock (_gate)
        {
            var node = _order.First;
            while (node is not null)
            {
                var next = node.Next;
                if (!keep(node.Value))
                {
                    _order.Remove(node);
                    _nodes.Remove(node.Value);
                }
                node = next;
            }
        }
    }
}

public static class ApplicationGrouper
{
    public static IReadOnlyList<SwitcherItem> Group(
        IReadOnlyList<WindowCandidate> windows,
        IReadOnlyList<nint> mru,
        GroupingMode mode)
    {
        var rank = mru.Select((handle, index) => (handle, index)).ToDictionary(x => x.handle, x => x.index);
        var ordered = windows.OrderBy(w => rank.GetValueOrDefault(w.Handle, int.MaxValue)).ThenByDescending(w => w.LastActivated).ToArray();

        if (mode == GroupingMode.IndividualWindows)
        {
            return ordered.Select(w => new SwitcherItem(
                $"{w.Identity}#{w.Handle}",
                string.IsNullOrWhiteSpace(w.Title) ? w.DisplayName : w.Title,
                new[] { w.Handle },
                w.Handle,
                w.Identity,
                RgbColor.AccentFallback)).ToArray();
        }

        return ordered
            .GroupBy(w => w.Identity, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var members = group.ToArray();
                var target = members[0];
                return new SwitcherItem(
                    group.Key,
                    target.DisplayName,
                    members.Select(w => w.Handle).ToArray(),
                    target.Handle,
                    group.Key,
                    RgbColor.AccentFallback);
            }).ToArray();
    }
}

public sealed record WindowMetadata(
    bool IsVisible,
    bool IsMinimized,
    bool IsCloaked,
    bool IsToolWindow,
    bool IsAppWindow,
    bool IsOwnProcess,
    bool HasNonZeroBounds,
    bool IsShellWindow,
    bool IsRootOwnerLastActivePopup);

public static class WindowEligibility
{
    public static bool IsEligible(WindowMetadata metadata)
    {
        if (metadata.IsOwnProcess || metadata.IsShellWindow || metadata.IsCloaked || !metadata.HasNonZeroBounds) return false;
        if (!metadata.IsVisible && !metadata.IsMinimized) return false;
        if (metadata.IsToolWindow && !metadata.IsAppWindow) return false;
        if (!metadata.IsAppWindow && !metadata.IsRootOwnerLastActivePopup) return false;
        return true;
    }
}

public static class ShortcutBadges
{
    public static string? ForViewportIndex(int index) => index switch
    {
        >= 0 and <= 8 => (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
        9 => "0",
        _ => null,
    };
}

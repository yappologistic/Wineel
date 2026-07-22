namespace Wineel;

public static class SwitcherViews
{
    public static IReadOnlyList<SwitcherItem> OrderPinned(
        IReadOnlyList<SwitcherItem> items,
        IReadOnlyList<string> pinnedIdentities)
    {
        if (pinnedIdentities.Count == 0) return items.ToArray();
        var rank = pinnedIdentities
            .Select((identity, index) => (identity, index))
            .GroupBy(pair => pair.identity, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().index, StringComparer.OrdinalIgnoreCase);
        return items
            .Select((item, index) => (item, index))
            .OrderBy(pair => rank.TryGetValue(pair.item.Identity, out var pinnedRank) ? 0 : 1)
            .ThenBy(pair => rank.GetValueOrDefault(pair.item.Identity, int.MaxValue))
            .ThenBy(pair => pair.index)
            .Select(pair => pair.item)
            .ToArray();
    }

    public static IReadOnlyList<SwitcherItem> Filter(IReadOnlyList<SwitcherItem> items, string query)
    {
        query = query.Trim();
        if (query.Length == 0) return items.ToArray();
        return items
            .Select((item, index) => (item, index, score: MatchScore(item.DisplayName, query)))
            .Where(match => match.score < int.MaxValue)
            .OrderBy(match => match.score)
            .ThenBy(match => match.index)
            .Select(match => match.item)
            .ToArray();
    }

    public static IReadOnlyList<SwitcherItem> CreateWindowItems(
        SwitcherItem parent,
        IReadOnlyList<WindowCandidate> candidates)
    {
        var byHandle = candidates.ToDictionary(candidate => candidate.Handle);
        return parent.WindowHandles
            .Where(byHandle.ContainsKey)
            .Select(handle => byHandle[handle])
            .Select(window => new SwitcherItem(
                $"{parent.Identity}#{window.Handle}",
                string.IsNullOrWhiteSpace(window.Title) ? window.DisplayName : window.Title,
                new[] { window.Handle },
                window.Handle,
                parent.IconCacheKey,
                parent.AccentColor))
            .ToArray();
    }

    private static int MatchScore(string value, string query)
    {
        if (value.StartsWith(query, StringComparison.CurrentCultureIgnoreCase)) return 0;
        var contains = value.IndexOf(query, StringComparison.CurrentCultureIgnoreCase);
        if (contains >= 0) return 100 + contains;

        var valueIndex = 0;
        var distance = 0;
        foreach (var character in query)
        {
            var found = value.IndexOf(character.ToString(), valueIndex, StringComparison.CurrentCultureIgnoreCase);
            if (found < 0) return int.MaxValue;
            distance += found - valueIndex;
            valueIndex = found + 1;
        }
        return 1000 + distance;
    }
}

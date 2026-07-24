using Xunit;

namespace Wineel.Tests;

public sealed class DiscoveryTests
{
    [Fact]
    public void PinnedApplicationsLeadWithoutDisturbingRemainingMruOrder()
    {
        var items = new[] { Item("a"), Item("b"), Item("c") };
        var ordered = SwitcherViews.OrderPinned(items, new[] { "c" });
        Assert.Equal(new[] { "c", "a", "b" }, ordered.Select(item => item.Identity));
    }

    [Fact]
    public void SearchRanksPrefixContainsAndFuzzyMatches()
    {
        var items = new[] { Item("terminal", "Windows Terminal"), Item("telegram", "Telegram"), Item("teams", "Microsoft Teams") };
        Assert.Equal(new[] { "telegram", "terminal" }, SwitcherViews.Filter(items, "tel").Select(item => item.Identity));
        Assert.Equal("teams", Assert.Single(SwitcherViews.Filter(items, "msteams")).Identity);
    }

    private static SwitcherItem Item(string identity, string? name = null) =>
        new(identity, name ?? identity, new nint[] { 1 }, 1, identity, RgbColor.AccentFallback);

    [Fact]
    public void MruPromotesRepeatedWindow()
    {
        var mru = new MruList();
        mru.Observe(1); mru.Observe(2); mru.Observe(3); mru.Observe(1);
        Assert.Equal(new nint[] { 1, 3, 2 }, mru.Snapshot());
    }

    [Fact]
    public void MruPruneRemovesStaleWindowsWithoutChangingRemainingOrder()
    {
        var mru = new MruList();
        mru.Observe(1); mru.Observe(2); mru.Observe(3); mru.Observe(4);

        mru.Prune(window => window is 2 or 4);

        Assert.Equal(new nint[] { 4, 2 }, mru.Snapshot());
    }

    [Fact]
    public void ApplicationGroupingCombinesIdentityAndPreservesMruTarget()
    {
        var windows = new[]
        {
            Window(10, "editor", "Editor A"), Window(11, "editor", "Editor B"), Window(20, "browser", "Browser")
        };
        var grouped = ApplicationGrouper.Group(windows, new nint[] { 11, 20, 10 }, GroupingMode.Applications);
        Assert.Equal(2, grouped.Count);
        Assert.Equal((nint)11, grouped[0].TargetWindow);
        Assert.Equal(2, grouped[0].WindowCount);
    }

    [Fact]
    public void IndividualModeUsesWindowTitles()
    {
        var grouped = ApplicationGrouper.Group(new[] { Window(10, "editor", "Document A"), Window(11, "editor", "Document B") }, new nint[] { 11, 10 }, GroupingMode.IndividualWindows);
        Assert.Equal(2, grouped.Count);
        Assert.Equal("Document B", grouped[0].DisplayName);
    }

    [Theory]
    [MemberData(nameof(FilterCases))]
    public void WindowFilterMatchesAltTabPredicates(WindowMetadata metadata, bool expected) => Assert.Equal(expected, WindowEligibility.IsEligible(metadata));

    public static IEnumerable<object[]> FilterCases()
    {
        var valid = new WindowMetadata(true, false, false, false, false, false, true, false, true);
        yield return new object[] { valid, true };
        yield return new object[] { valid with { IsOwnProcess = true }, false };
        yield return new object[] { valid with { IsCloaked = true }, false };
        yield return new object[] { valid with { IsToolWindow = true }, false };
        yield return new object[] { valid with { IsToolWindow = true, IsAppWindow = true }, true };
        yield return new object[] { valid with { IsVisible = false, IsMinimized = false }, false };
        yield return new object[] { valid with { IsVisible = false, IsMinimized = true }, true };
    }

    [Theory]
    [InlineData(0, "1")]
    [InlineData(8, "9")]
    [InlineData(9, "0")]
    [InlineData(10, null)]
    public void ShortcutMappingMatchesVisibleSlots(int index, string? expected) => Assert.Equal(expected, ShortcutBadges.ForViewportIndex(index));

    private static WindowCandidate Window(int handle, string identity, string title) => new(handle, handle, identity, $"C:\\{identity}.exe", identity, title, false, DateTimeOffset.UtcNow);
}

using Xunit;

namespace Wineel.Tests;

public sealed class UiPolicyTests
{
    [Fact]
    public void ReducedMotionAlwaysDisablesAnimation()
    {
        var settings = new AppSettings { MotionPreference = MotionPreference.Reduced };
        Assert.False(WheelVisualPolicy.Resolve(settings, false, true).Animate);
    }

    [Fact]
    public void FullMotionOverridesWindowsAnimationPreference()
    {
        var settings = new AppSettings { MotionPreference = MotionPreference.Full };
        Assert.True(WheelVisualPolicy.Resolve(settings, false, false).Animate);
    }

    [Fact]
    public void FollowWindowsHonorsAnimationPreference()
    {
        var settings = new AppSettings { MotionPreference = MotionPreference.FollowWindows };
        Assert.False(WheelVisualPolicy.Resolve(settings, false, false).Animate);
        Assert.True(WheelVisualPolicy.Resolve(settings, false, true).Animate);
    }

    [Fact]
    public void HighContrastUsesOpaqueSystemSurfaceAndNoDecorativeGlow()
    {
        var policy = WheelVisualPolicy.Resolve(new AppSettings(), true, true);
        Assert.True(policy.UseSystemColors);
        Assert.Equal(1, policy.PlateOpacity);
        Assert.Equal(0, policy.SelectionGlow);
    }

    [Fact]
    public void CollectionAddRejectsDuplicatesIgnoringCase()
    {
        var result = SettingsCollection.AddUnique(new[] { "C:\\Apps\\Editor.exe" }, "c:\\apps\\editor.exe");
        Assert.False(result.Changed);
        Assert.Single(result.Items);
    }

    [Fact]
    public void CollectionAddAndRemoveCoverHappyAndMissingPaths()
    {
        var added = SettingsCollection.AddUnique(Array.Empty<string>(), " C:\\Apps\\Editor.exe ");
        Assert.True(added.Changed);
        Assert.Equal("C:\\Apps\\Editor.exe", Assert.Single(added.Items));

        var missing = SettingsCollection.Remove(added.Items, "missing.exe");
        Assert.False(missing.Changed);

        var removed = SettingsCollection.Remove(added.Items, "c:\\apps\\editor.exe");
        Assert.True(removed.Changed);
        Assert.Empty(removed.Items);
    }
}

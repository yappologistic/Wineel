using System.Text.Json.Nodes;
using Xunit;

namespace Wineel.Tests;

public sealed class SettingsAndColorTests
{
    [Fact]
    public void NewSettingsUseCenteredCompactWheel()
    {
        var settings = new AppSettings();
        Assert.Equal(WheelAnchorMode.MonitorCenter, settings.WheelAnchor);
        Assert.Equal(400, settings.WheelSize);
        Assert.Equal(0.68, settings.PlateOpacity);
    }

    [Fact]
    public void SettingsRoundTrip()
    {
        var directory = Directory.CreateTempSubdirectory("wineel-settings-");
        try
        {
            var path = Path.Combine(directory.FullName, "settings.json");
            var store = new SettingsStore(path);
            var expected = new AppSettings { ReplaceAltTab = true, WheelSize = 578, GroupingMode = GroupingMode.IndividualWindows, PinnedIdentities = new[] { "app-id" }, Exclusions = new[] { "C:\\App.exe" } };
            store.Save(expected);
            var actual = store.Load();
            Assert.True(actual.ReplaceAltTab);
            Assert.Equal(578, actual.WheelSize);
            Assert.Equal(GroupingMode.IndividualWindows, actual.GroupingMode);
            Assert.Equal("app-id", Assert.Single(actual.PinnedIdentities));
            Assert.Equal("C:\\App.exe", Assert.Single(actual.Exclusions));
        }
        finally { directory.Delete(true); }
    }

    [Fact]
    public void CorruptSettingsAreBackedUpAndDefaultsRestored()
    {
        var directory = Directory.CreateTempSubdirectory("wineel-corrupt-");
        try
        {
            var path = Path.Combine(directory.FullName, "settings.json");
            File.WriteAllText(path, "{not-json");
            var store = new SettingsStore(path);
            var settings = store.Load();
            Assert.False(settings.ReplaceAltTab);
            Assert.NotNull(store.LastRecoveryBackup);
            Assert.True(File.Exists(store.LastRecoveryBackup));
            Assert.True(File.Exists(path));
        }
        finally { directory.Delete(true); }
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{\"Version\":\"not-a-number\"}")]
    public void StructurallyInvalidSettingsAreBackedUpAndDefaultsRestored(string json)
    {
        var directory = Directory.CreateTempSubdirectory("wineel-invalid-settings-");
        try
        {
            var path = Path.Combine(directory.FullName, "settings.json");
            File.WriteAllText(path, json);
            var store = new SettingsStore(path);

            var settings = store.Load();

            Assert.Equal(new AppSettings(), settings);
            Assert.NotNull(store.LastRecoveryBackup);
            Assert.True(File.Exists(store.LastRecoveryBackup));
            Assert.True(File.Exists(path));
        }
        finally { directory.Delete(true); }
    }

    [Fact]
    public void VersionOneEnableAltTabMigrates()
    {
        var root = JsonNode.Parse("{\"Version\":1,\"EnableAltTab\":true}")!.AsObject();
        var migrated = SettingsStore.Migrate(root);
        Assert.Equal(AppSettings.CurrentVersion, migrated["Version"]!.GetValue<int>());
        Assert.True(migrated["ReplaceAltTab"]!.GetValue<bool>());
        Assert.Null(migrated["EnableAltTab"]);
        Assert.Equal(400, migrated["WheelSize"]!.GetValue<int>());
        Assert.Equal(0.68, migrated["PlateOpacity"]!.GetValue<double>());
    }

    [Fact]
    public void VersionThreeDefaultsMigrateWithoutOverwritingCustomization()
    {
        var defaults = SettingsStore.Migrate(JsonNode.Parse("{\"Version\":3,\"WheelSize\":440,\"PlateOpacity\":0.72}")!.AsObject());
        Assert.Equal(400, defaults["WheelSize"]!.GetValue<int>());
        Assert.Equal(0.68, defaults["PlateOpacity"]!.GetValue<double>());

        var customized = SettingsStore.Migrate(JsonNode.Parse("{\"Version\":3,\"WheelSize\":470,\"PlateOpacity\":0.8}")!.AsObject());
        Assert.Equal(470, customized["WheelSize"]!.GetValue<int>());
        Assert.Equal(0.8, customized["PlateOpacity"]!.GetValue<double>());
    }

    [Fact]
    public void VersionFourAddsEmptyFavorites()
    {
        var migrated = SettingsStore.Migrate(JsonNode.Parse("{\"Version\":4}")!.AsObject());
        Assert.Equal(AppSettings.CurrentVersion, migrated["Version"]!.GetValue<int>());
        Assert.Empty(migrated["PinnedIdentities"]!.AsArray());
    }

    [Fact]
    public void DominantColorWeightsSaturatedMidtones()
    {
        var pixels = Enumerable.Repeat(new PixelSample(240, 240, 240), 100)
            .Concat(Enumerable.Repeat(new PixelSample(20, 120, 230), 30));
        var color = DominantColorExtractor.Extract(pixels, new RgbColor(1, 2, 3));
        Assert.True(color.B > color.R);
        Assert.True(color.B > color.G);
    }

    [Fact]
    public void DominantColorFallsBackForTransparentOrNeutralPixels()
    {
        var fallback = new RgbColor(9, 8, 7);
        var color = DominantColorExtractor.Extract(new[] { new PixelSample(0, 0, 0), new PixelSample(255, 255, 255), new PixelSample(255, 0, 0, 0) }, fallback);
        Assert.Equal(fallback, color);
    }
}

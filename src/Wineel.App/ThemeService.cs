using Microsoft.Win32;
using System.Windows;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using WpfApplication = System.Windows.Application;

namespace Wineel;

internal static class ThemeService
{
    public static bool Apply(ThemeMode mode)
    {
        var dark = IsDark(mode);
        var colors = dark
            ? new Dictionary<string, Color>
            {
                ["BackgroundBrush"] = Color.FromRgb(20, 20, 20),
                ["SurfaceBrush"] = Color.FromRgb(29, 29, 29),
                ["SurfaceRaisedBrush"] = Color.FromRgb(41, 41, 41),
                ["InputBrush"] = Color.FromRgb(32, 32, 32),
                ["TextPrimaryBrush"] = Color.FromRgb(244, 244, 244),
                ["TextSecondaryBrush"] = Color.FromRgb(185, 185, 185),
                ["BorderBrush"] = Color.FromRgb(69, 69, 69),
                ["HoverBrush"] = Color.FromRgb(52, 52, 52),
                ["AccentBrush"] = Color.FromRgb(138, 180, 248),
            }
            : new Dictionary<string, Color>
            {
                ["BackgroundBrush"] = Color.FromRgb(246, 246, 246),
                ["SurfaceBrush"] = Color.FromRgb(255, 255, 255),
                ["SurfaceRaisedBrush"] = Color.FromRgb(235, 235, 235),
                ["InputBrush"] = Color.FromRgb(255, 255, 255),
                ["TextPrimaryBrush"] = Color.FromRgb(31, 31, 31),
                ["TextSecondaryBrush"] = Color.FromRgb(92, 92, 92),
                ["BorderBrush"] = Color.FromRgb(190, 190, 190),
                ["HoverBrush"] = Color.FromRgb(222, 222, 222),
                ["AccentBrush"] = Color.FromRgb(47, 103, 178),
            };

        foreach (var pair in colors)
            WpfApplication.Current.Resources[pair.Key] = new SolidColorBrush(pair.Value);
        return dark;
    }

    public static bool IsDark(ThemeMode mode)
    {
        if (mode == ThemeMode.Dark) return true;
        if (mode == ThemeMode.Light) return false;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException)
        {
            return true;
        }
    }
}

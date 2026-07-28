using Microsoft.Win32;
using System.Windows;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using WpfApplication = System.Windows.Application;
using WpfSystemColors = System.Windows.SystemColors;

namespace Wineel;

internal static class ThemeService
{
    public static bool Apply(ThemeMode mode)
    {
        var dark = IsDark(mode);
        var colors = SystemParameters.HighContrast
            ? new Dictionary<string, Color>
            {
                ["BackgroundBrush"] = WpfSystemColors.WindowColor,
                ["NavigationBrush"] = WpfSystemColors.WindowColor,
                ["SurfaceBrush"] = WpfSystemColors.WindowColor,
                ["SurfaceRaisedBrush"] = WpfSystemColors.ControlColor,
                ["InputBrush"] = WpfSystemColors.WindowColor,
                ["TextPrimaryBrush"] = WpfSystemColors.WindowTextColor,
                ["TextSecondaryBrush"] = WpfSystemColors.WindowTextColor,
                ["BorderBrush"] = WpfSystemColors.WindowTextColor,
                ["HoverBrush"] = WpfSystemColors.HighlightColor,
                ["AccentBrush"] = WpfSystemColors.HighlightColor,
                ["AccentMutedBrush"] = WpfSystemColors.ControlColor,
                ["SuccessBrush"] = WpfSystemColors.WindowTextColor,
                ["ErrorBrush"] = WpfSystemColors.WindowTextColor,
                ["WarningBrush"] = WpfSystemColors.WindowTextColor,
            }
            : dark
            ? new Dictionary<string, Color>
            {
                ["BackgroundBrush"] = Color.FromRgb(16, 18, 20),
                ["NavigationBrush"] = Color.FromRgb(21, 24, 27),
                ["SurfaceBrush"] = Color.FromRgb(26, 29, 32),
                ["SurfaceRaisedBrush"] = Color.FromRgb(35, 39, 43),
                ["InputBrush"] = Color.FromRgb(32, 36, 40),
                ["TextPrimaryBrush"] = Color.FromRgb(245, 245, 245),
                ["TextSecondaryBrush"] = Color.FromRgb(184, 187, 192),
                ["BorderBrush"] = Color.FromRgb(58, 63, 69),
                ["HoverBrush"] = Color.FromRgb(44, 49, 54),
                ["AccentBrush"] = Color.FromRgb(241, 185, 74),
                ["AccentMutedBrush"] = Color.FromRgb(59, 48, 32),
                ["SuccessBrush"] = Color.FromRgb(114, 196, 114),
                ["ErrorBrush"] = Color.FromRgb(255, 139, 134),
                ["WarningBrush"] = Color.FromRgb(241, 185, 74),
            }
            : new Dictionary<string, Color>
            {
                ["BackgroundBrush"] = Color.FromRgb(246, 246, 246),
                ["NavigationBrush"] = Color.FromRgb(239, 239, 239),
                ["SurfaceBrush"] = Color.FromRgb(255, 255, 255),
                ["SurfaceRaisedBrush"] = Color.FromRgb(235, 235, 235),
                ["InputBrush"] = Color.FromRgb(255, 255, 255),
                ["TextPrimaryBrush"] = Color.FromRgb(31, 31, 31),
                ["TextSecondaryBrush"] = Color.FromRgb(92, 92, 92),
                ["BorderBrush"] = Color.FromRgb(190, 190, 190),
                ["HoverBrush"] = Color.FromRgb(222, 222, 222),
                ["AccentBrush"] = Color.FromRgb(154, 101, 0),
                ["AccentMutedBrush"] = Color.FromRgb(255, 244, 219),
                ["SuccessBrush"] = Color.FromRgb(24, 120, 48),
                ["ErrorBrush"] = Color.FromRgb(178, 37, 34),
                ["WarningBrush"] = Color.FromRgb(154, 101, 0),
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

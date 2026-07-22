namespace Wineel;

public enum GroupingMode { Applications, IndividualWindows }
public enum SwitcherMode { AltHeld, Latched }
public enum ThemeMode { FollowWindows, Dark, Light }
public enum MotionPreference { FollowWindows, Reduced, Full }
public enum WheelAnchorMode { MonitorCenter, Pointer }

public readonly record struct RgbColor(byte R, byte G, byte B)
{
    public static RgbColor AccentFallback => new(96, 165, 250);
}

public sealed record WindowCandidate(
    nint Handle,
    int ProcessId,
    string Identity,
    string ExecutablePath,
    string DisplayName,
    string Title,
    bool IsMinimized,
    DateTimeOffset LastActivated);

public sealed record SwitcherItem(
    string Identity,
    string DisplayName,
    IReadOnlyList<nint> WindowHandles,
    nint TargetWindow,
    string IconCacheKey,
    RgbColor AccentColor)
{
    public int WindowCount => WindowHandles.Count;
}

public readonly record struct LogicalPoint(double X, double Y);
public readonly record struct LogicalRect(double X, double Y, double Width, double Height)
{
    public double Left => X;
    public double Top => Y;
    public double Right => X + Width;
    public double Bottom => Y + Height;
}

public sealed record RadialSlot(int ItemIndex, int ViewportIndex, double AngleRadians, LogicalPoint Center);

public sealed record AppSettings
{
    public const int CurrentVersion = 4;
    public int Version { get; init; } = CurrentVersion;
    public bool ReplaceAltTab { get; init; }
    public string FallbackShortcut { get; init; } = "Ctrl+Alt+Space";
    public bool StartWithWindows { get; init; }
    public bool LaunchMinimized { get; init; } = true;
    public bool IsPaused { get; init; }
    public GroupingMode GroupingMode { get; init; } = GroupingMode.Applications;
    public bool CurrentVirtualDesktopOnly { get; init; } = true;
    public bool DisableInExclusiveFullscreen { get; init; } = true;
    public double WheelSize { get; init; } = 400;
    public double IconSize { get; init; } = 64;
    public int MaximumVisibleIcons { get; init; } = 12;
    public double PlateOpacity { get; init; } = 0.68;
    public double BeamIntensity { get; init; } = 0.32;
    public double AnimationSpeed { get; init; } = 1.0;
    public bool ShowLabels { get; init; } = true;
    public bool ShowNumberBadges { get; init; } = true;
    public ThemeMode ThemeMode { get; init; } = ThemeMode.FollowWindows;
    public MotionPreference MotionPreference { get; init; } = MotionPreference.FollowWindows;
    public WheelAnchorMode WheelAnchor { get; init; } = WheelAnchorMode.MonitorCenter;
    public bool ReverseWheel { get; init; }
    public bool RepeatTabEnabled { get; init; } = true;
    public bool MouseClickSelection { get; init; } = true;
    public bool WrapSelection { get; init; } = true;
    public IReadOnlyList<string> Exclusions { get; init; } = Array.Empty<string>();
    public bool OnboardingCompleted { get; init; }
}

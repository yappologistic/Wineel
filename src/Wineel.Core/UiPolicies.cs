namespace Wineel;

public readonly record struct WheelVisualPolicy(
    bool UseSystemColors,
    bool Animate,
    double PlateOpacity,
    double SelectionGlow)
{
    public static WheelVisualPolicy Resolve(AppSettings settings, bool highContrast, bool clientAreaAnimation)
    {
        var animate = settings.MotionPreference switch
        {
            MotionPreference.Reduced => false,
            MotionPreference.Full => true,
            _ => clientAreaAnimation,
        };
        return new WheelVisualPolicy(
            highContrast,
            animate,
            highContrast ? 1 : Math.Clamp(settings.PlateOpacity, 0.55, 0.98),
            highContrast ? 0 : Math.Clamp(settings.BeamIntensity, 0, 0.55));
    }
}

public readonly record struct CollectionChange(IReadOnlyList<string> Items, bool Changed);

public static class SettingsCollection
{
    public static CollectionChange AddUnique(IReadOnlyList<string> items, string value)
    {
        value = value.Trim();
        if (value.Length == 0 || items.Contains(value, StringComparer.OrdinalIgnoreCase))
            return new CollectionChange(items.ToArray(), false);
        return new CollectionChange(items.Append(value).ToArray(), true);
    }

    public static CollectionChange Remove(IReadOnlyList<string> items, string value)
    {
        var result = items.Where(item => !string.Equals(item, value, StringComparison.OrdinalIgnoreCase)).ToArray();
        return new CollectionChange(result, result.Length != items.Count);
    }
}

namespace Wineel;

public sealed record SettingsApplyResult(AppSettings AppliedSettings, bool Saved, string? ErrorMessage = null)
{
    public static SettingsApplyResult Success(AppSettings settings) => new(settings, true);
    public static SettingsApplyResult Failure(AppSettings settings, string message) => new(settings, false, message);
}

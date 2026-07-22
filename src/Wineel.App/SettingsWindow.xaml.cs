using System.Windows;
using System.Windows.Controls;

namespace Wineel;

public partial class SettingsWindow : Window
{
    private bool _loading;
    private AppSettings _settings = new();
    public event Action<AppSettings>? SettingsChanged;
    public event Action? TryRequested;

    public SettingsWindow()
    {
        InitializeComponent();
        Closed += (_, _) => Hide();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    public void LoadSettings(AppSettings settings)
    {
        _loading = true;
        _settings = settings;
        ReplaceAltTab.IsChecked = settings.ReplaceAltTab;
        FallbackShortcut.Text = settings.FallbackShortcut;
        StartWithWindows.IsChecked = settings.StartWithWindows;
        LaunchMinimized.IsChecked = settings.LaunchMinimized;
        CurrentDesktopOnly.IsChecked = settings.CurrentVirtualDesktopOnly;
        DisableFullscreen.IsChecked = settings.DisableInExclusiveFullscreen;
        GroupingMode.SelectedIndex = (int)settings.GroupingMode;
        WheelAnchor.SelectedIndex = (int)settings.WheelAnchor;
        WheelSize.Value = settings.WheelSize;
        IconSize.Value = settings.IconSize;
        MaximumVisible.Value = settings.MaximumVisibleIcons;
        PlateOpacity.Value = settings.PlateOpacity;
        BeamIntensity.Value = settings.BeamIntensity;
        AnimationSpeed.Value = settings.AnimationSpeed;
        ShowLabels.IsChecked = settings.ShowLabels;
        ShowBadges.IsChecked = settings.ShowNumberBadges;
        ThemeModePicker.SelectedIndex = (int)settings.ThemeMode;
        MotionMode.SelectedIndex = (int)settings.MotionPreference;
        ReverseWheel.IsChecked = settings.ReverseWheel;
        RepeatTab.IsChecked = settings.RepeatTabEnabled;
        MouseClickSelection.IsChecked = settings.MouseClickSelection;
        WrapSelection.IsChecked = settings.WrapSelection;
        PinnedIdentities.ItemsSource = settings.PinnedIdentities.ToArray();
        Exclusions.ItemsSource = settings.Exclusions.ToArray();
        WelcomePanel.Visibility = settings.OnboardingCompleted ? Visibility.Collapsed : Visibility.Visible;
        PauseButton.Content = settings.IsPaused ? "Resume Wineel" : "Pause Wineel";
        _loading = false;
    }

    private void AnySettingChanged(object sender, RoutedEventArgs e)
    {
        if (_loading || !IsInitialized) return;
        Publish(ReadSettings());
    }

    private AppSettings ReadSettings() => _settings with
    {
        ReplaceAltTab = ReplaceAltTab.IsChecked == true,
        FallbackShortcut = string.IsNullOrWhiteSpace(FallbackShortcut.Text) ? "Ctrl+Alt+Space" : FallbackShortcut.Text.Trim(),
        StartWithWindows = StartWithWindows.IsChecked == true,
        LaunchMinimized = LaunchMinimized.IsChecked == true,
        CurrentVirtualDesktopOnly = CurrentDesktopOnly.IsChecked == true,
        DisableInExclusiveFullscreen = DisableFullscreen.IsChecked == true,
        GroupingMode = (Wineel.GroupingMode)Math.Max(0, GroupingMode.SelectedIndex),
        WheelAnchor = (WheelAnchorMode)Math.Max(0, WheelAnchor.SelectedIndex),
        WheelSize = WheelSize.Value,
        IconSize = IconSize.Value,
        MaximumVisibleIcons = (int)MaximumVisible.Value,
        PlateOpacity = PlateOpacity.Value,
        BeamIntensity = BeamIntensity.Value,
        AnimationSpeed = AnimationSpeed.Value,
        ShowLabels = ShowLabels.IsChecked == true,
        ShowNumberBadges = ShowBadges.IsChecked == true,
        ThemeMode = (Wineel.ThemeMode)Math.Max(0, ThemeModePicker.SelectedIndex),
        MotionPreference = (MotionPreference)Math.Max(0, MotionMode.SelectedIndex),
        ReverseWheel = ReverseWheel.IsChecked == true,
        RepeatTabEnabled = RepeatTab.IsChecked == true,
        MouseClickSelection = MouseClickSelection.IsChecked == true,
        WrapSelection = WrapSelection.IsChecked == true,
        PinnedIdentities = PinnedIdentities.Items.Cast<string>().ToArray(),
        Exclusions = Exclusions.Items.Cast<string>().ToArray(),
    };

    private void Publish(AppSettings settings)
    {
        _settings = settings;
        PauseButton.Content = settings.IsPaused ? "Resume Wineel" : "Pause Wineel";
        SettingsChanged?.Invoke(settings);
    }

    private void TryWineel_Click(object sender, RoutedEventArgs e) => TryRequested?.Invoke();
    private void FinishSetup_Click(object sender, RoutedEventArgs e) { WelcomePanel.Visibility = Visibility.Collapsed; Publish(ReadSettings() with { OnboardingCompleted = true }); }
    private void Pause_Click(object sender, RoutedEventArgs e) => Publish(ReadSettings() with { IsPaused = !_settings.IsPaused });
    private void RestoreShortcuts_Click(object sender, RoutedEventArgs e) { FallbackShortcut.Text = "Ctrl+Alt+Space"; Publish(ReadSettings()); }
    private void ResetInput_Click(object sender, RoutedEventArgs e)
    {
        ReverseWheel.IsChecked = false; RepeatTab.IsChecked = true; MouseClickSelection.IsChecked = true; WrapSelection.IsChecked = true; Publish(ReadSettings());
    }
    private void AddCurrent_Click(object sender, RoutedEventArgs e)
    {
        var path = ForegroundApplicationInfo.GetExecutablePath();
        if (string.IsNullOrWhiteSpace(path) || _settings.Exclusions.Contains(path, StringComparer.OrdinalIgnoreCase)) return;
        Publish(ReadSettings() with { Exclusions = _settings.Exclusions.Append(path).ToArray() });
        LoadSettings(_settings);
    }
    private void RemoveExclusion_Click(object sender, RoutedEventArgs e)
    {
        if (Exclusions.SelectedItem is not string selected) return;
        Publish(ReadSettings() with { Exclusions = _settings.Exclusions.Where(item => !string.Equals(item, selected, StringComparison.OrdinalIgnoreCase)).ToArray() });
        LoadSettings(_settings);
    }

    private void RemoveFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (PinnedIdentities.SelectedItem is not string selected) return;
        Publish(ReadSettings() with { PinnedIdentities = _settings.PinnedIdentities.Where(item => !string.Equals(item, selected, StringComparison.OrdinalIgnoreCase)).ToArray() });
        LoadSettings(_settings);
    }
}

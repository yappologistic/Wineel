using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using WpfBrush = System.Windows.Media.Brush;
using WpfRadioButton = System.Windows.Controls.RadioButton;
using Win32OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace Wineel;

public partial class SettingsWindow : Window
{
    private bool _loading;
    private bool _publishPending;
    private string? _exclusionCandidatePath;
    private AppSettings _settings = new();
    private readonly DispatcherTimer _publishTimer;

    public event Func<AppSettings, SettingsApplyResult>? SettingsChanged;
    public event Action? TryRequested;

    public SettingsWindow()
    {
        InitializeComponent();
        _publishTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _publishTimer.Tick += (_, _) => FlushPendingSettings();
        Loaded += (_, _) => ConstrainToWorkingArea();
    }

    private void ConstrainToWorkingArea()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var workArea = System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
        var dpi = VisualTreeHelper.GetDpi(this);
        var availableWidth = workArea.Width / dpi.DpiScaleX;
        var availableHeight = workArea.Height / dpi.DpiScaleY;
        MaxWidth = Math.Max(MinWidth, availableWidth);
        MaxHeight = Math.Max(MinHeight, availableHeight);
        Width = Math.Min(Width, MaxWidth);
        Height = Math.Min(Height, MaxHeight);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        FlushPendingSettings();
        e.Cancel = true;
        Hide();
    }

    public void SetExclusionCandidate(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var ownPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(ownPath) && string.Equals(path, ownPath, StringComparison.OrdinalIgnoreCase)) return;
        _exclusionCandidatePath = path;
        UpdateListState();
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
        ApplyTheme(settings.ThemeMode);
        UpdateValueLabels();
        UpdateShortcutValidation(false);
        UpdateListState();
        _loading = false;
    }

    private void Navigation_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized || sender is not WpfRadioButton { Tag: string tag } || !int.TryParse(tag, out var index)) return;
        var pages = new FrameworkElement[] { GeneralPage, AppearancePage, InputPage, FavoritesPage, ExclusionsPage };
        for (var i = 0; i < pages.Length; i++) pages[i].Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;
        var titles = new[] { "General", "Appearance", "Input", "Favorites", "Exclusions" };
        var subtitles = new[]
        {
            "Core behavior for how Wineel launches, shows, and works on your system.",
            "Tune the wheel layout, separation, labels, theme, and motion.",
            "Choose how keyboard and mouse input move through the wheel.",
            "Keep your most-used applications at the front of the wheel.",
            "Choose applications that Wineel should never show."
        };
        PageTitle.Text = titles[index];
        PageSubtitle.Text = subtitles[index];
    }

    private void AnySettingChanged(object sender, RoutedEventArgs e)
    {
        if (_loading || !IsInitialized) return;
        UpdateValueLabels();
        if (sender is Slider)
        {
            SetSaveState(SaveVisualState.Saving, "Saving changes…");
            _publishPending = true;
            _publishTimer.Stop();
            _publishTimer.Start();
            return;
        }
        Publish(ReadSettings());
    }

    private void FallbackShortcut_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading || !IsInitialized) return;
        UpdateShortcutValidation(false);
        SetSaveState(SaveVisualState.Pending, "Shortcut not saved yet");
    }

    private void FallbackShortcut_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_loading || !IsInitialized) return;
        if (!UpdateShortcutValidation(true))
        {
            SetSaveState(SaveVisualState.Error, "Fix the shortcut before saving");
            return;
        }
        Publish(ReadSettings());
    }

    private bool UpdateShortcutValidation(bool announce)
    {
        var value = FallbackShortcut.Text.Trim();
        var valid = HotKeyService.TryParse(value, out _, out _);
        ShortcutValidationText.Text = valid
            ? "Shortcut format is valid"
            : "Use at least one modifier and one supported key, such as Ctrl+Alt+Space.";
        ShortcutValidationText.Foreground = (WpfBrush)FindResource(valid ? "SuccessBrush" : "ErrorBrush");
        if (announce) ShortcutValidationText.Focusable = true;
        return valid;
    }

    private void FlushPendingSettings()
    {
        if (!_publishPending) return;
        _publishTimer.Stop();
        _publishPending = false;
        Publish(ReadSettings());
    }

    private void UpdateValueLabels()
    {
        if (!IsInitialized) return;
        WheelSizeValue.Text = $"{WheelSize.Value:0} px";
        IconSizeValue.Text = $"{IconSize.Value:0} px";
        MaximumVisibleValue.Text = $"{MaximumVisible.Value:0}";
        PlateOpacityValue.Text = $"{PlateOpacity.Value:P0}";
        BeamIntensityValue.Text = $"{BeamIntensity.Value:P0}";
        AnimationSpeedValue.Text = $"{AnimationSpeed.Value:0.0}×";
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
        _publishTimer.Stop();
        _publishPending = false;
        _settings = settings;
        SetSaveState(SaveVisualState.Saving, "Saving changes…");
        var result = SettingsChanged?.Invoke(settings) ?? SettingsApplyResult.Success(settings);
        _settings = result.AppliedSettings;
        PauseButton.Content = _settings.IsPaused ? "Resume Wineel" : "Pause Wineel";
        ApplyTheme(_settings.ThemeMode);
        if (!string.Equals(FallbackShortcut.Text.Trim(), _settings.FallbackShortcut, StringComparison.OrdinalIgnoreCase))
        {
            _loading = true;
            FallbackShortcut.Text = _settings.FallbackShortcut;
            _loading = false;
        }
        UpdateShortcutValidation(false);
        UpdateListState();
        SetSaveState(result.Saved ? SaveVisualState.Saved : SaveVisualState.Error,
            result.Saved ? "All changes saved" : result.ErrorMessage ?? "Could not save changes");
    }

    private void SetSaveState(SaveVisualState state, string message)
    {
        if (!IsInitialized) return;
        SaveStatusText.Text = message;
        SaveStatusText.ToolTip = state == SaveVisualState.Error ? message : null;
        var resource = state switch
        {
            SaveVisualState.Saved => "SuccessBrush",
            SaveVisualState.Error => "ErrorBrush",
            _ => "WarningBrush",
        };
        SaveStatusText.Foreground = (WpfBrush)FindResource(resource);
    }

    private void TryWineel_Click(object sender, RoutedEventArgs e) { FlushPendingSettings(); TryRequested?.Invoke(); }
    private void FinishSetup_Click(object sender, RoutedEventArgs e) { WelcomePanel.Visibility = Visibility.Collapsed; Publish(ReadSettings() with { OnboardingCompleted = true }); }
    private void Pause_Click(object sender, RoutedEventArgs e) => Publish(ReadSettings() with { IsPaused = !_settings.IsPaused });
    private void RestoreShortcuts_Click(object sender, RoutedEventArgs e) { FallbackShortcut.Text = "Ctrl+Alt+Space"; Publish(ReadSettings()); }

    private void ResetInput_Click(object sender, RoutedEventArgs e)
    {
        ReverseWheel.IsChecked = false;
        RepeatTab.IsChecked = true;
        MouseClickSelection.IsChecked = true;
        WrapSelection.IsChecked = true;
        Publish(ReadSettings());
    }

    private void AddFavorite_Click(object sender, RoutedEventArgs e)
    {
        var path = PickExecutable("Add a favorite application");
        if (path is not null) AddFavorite(path);
    }

    private void AddFavoriteCandidate_Click(object sender, RoutedEventArgs e)
    {
        if (_exclusionCandidatePath is not null) AddFavorite(_exclusionCandidatePath);
    }

    private void AddFavorite(string path)
    {
        var change = SettingsCollection.AddUnique(_settings.PinnedIdentities, path);
        if (!change.Changed)
        {
            SetSaveState(SaveVisualState.Error, $"{DescribeApplication(path)} is already a favorite");
            return;
        }
        Publish(ReadSettings() with { PinnedIdentities = change.Items });
        LoadSettings(_settings);
    }

    private void BrowseExclusion_Click(object sender, RoutedEventArgs e)
    {
        var path = PickExecutable("Exclude an application from Wineel");
        if (path is not null) AddExclusion(path);
    }

    private void AddCurrent_Click(object sender, RoutedEventArgs e)
    {
        if (_exclusionCandidatePath is not null) AddExclusion(_exclusionCandidatePath);
    }

    private void AddExclusion(string path)
    {
        var change = SettingsCollection.AddUnique(_settings.Exclusions, path);
        if (!change.Changed)
        {
            SetSaveState(SaveVisualState.Error, $"{DescribeApplication(path)} is already excluded");
            return;
        }
        Publish(ReadSettings() with { Exclusions = change.Items });
        LoadSettings(_settings);
    }

    private static string? PickExecutable(string title)
    {
        var dialog = new Win32OpenFileDialog
        {
            Title = title,
            Filter = "Applications (*.exe)|*.exe",
            CheckFileExists = true,
            Multiselect = false,
        };
        return dialog.ShowDialog() == true ? Path.GetFullPath(dialog.FileName) : null;
    }

    private void RemoveExclusion_Click(object sender, RoutedEventArgs e)
    {
        if (Exclusions.SelectedItem is not string selected) return;
        Publish(ReadSettings() with { Exclusions = SettingsCollection.Remove(_settings.Exclusions, selected).Items });
        LoadSettings(_settings);
    }

    private void RemoveFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (PinnedIdentities.SelectedItem is not string selected) return;
        Publish(ReadSettings() with { PinnedIdentities = SettingsCollection.Remove(_settings.PinnedIdentities, selected).Items });
        LoadSettings(_settings);
    }

    private void FavoriteSelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateListState();
    private void ExclusionSelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateListState();

    private void UpdateListState()
    {
        if (!IsInitialized) return;
        FavoritesEmpty.Visibility = PinnedIdentities.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ExclusionsEmpty.Visibility = Exclusions.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RemoveFavoriteButton.IsEnabled = PinnedIdentities.SelectedItem is not null;
        RemoveExclusionButton.IsEnabled = Exclusions.SelectedItem is not null;

        var candidateAvailable = !string.IsNullOrWhiteSpace(_exclusionCandidatePath);
        var alreadyFavorite = candidateAvailable && _settings.PinnedIdentities.Contains(_exclusionCandidatePath!, StringComparer.OrdinalIgnoreCase);
        AddFavoriteCandidateButton.IsEnabled = candidateAvailable && !alreadyFavorite;
        AddFavoriteCandidateButton.Content = !candidateAvailable
            ? "Reopen Settings after focusing an app"
            : alreadyFavorite ? $"{DescribeApplication(_exclusionCandidatePath!)} is a favorite" : $"Add {DescribeApplication(_exclusionCandidatePath!)}";

        var alreadyExcluded = candidateAvailable && _settings.Exclusions.Contains(_exclusionCandidatePath!, StringComparer.OrdinalIgnoreCase);
        AddExclusionButton.IsEnabled = candidateAvailable && !alreadyExcluded;
        AddExclusionButton.Content = !candidateAvailable
            ? "Reopen Settings after focusing an app"
            : alreadyExcluded ? $"{DescribeApplication(_exclusionCandidatePath!)} is excluded" : $"Exclude {DescribeApplication(_exclusionCandidatePath!)}";
    }

    private static string DescribeApplication(string path)
    {
        var filename = Path.GetFileNameWithoutExtension(path);
        if (string.Equals(filename, "explorer", StringComparison.OrdinalIgnoreCase)) return "File Explorer";
        try
        {
            var description = FileVersionInfo.GetVersionInfo(path).FileDescription;
            if (!string.IsNullOrWhiteSpace(description)) return description.Trim();
        }
        catch (Exception exception) when (exception is FileNotFoundException or System.ComponentModel.Win32Exception or ArgumentException) { }
        return string.IsNullOrWhiteSpace(filename) ? "previous application" : filename;
    }

    private void ApplyTheme(Wineel.ThemeMode mode)
    {
        var dark = ThemeService.Apply(mode);
#pragma warning disable WPF0001
        ThemeMode = dark ? System.Windows.ThemeMode.Dark : System.Windows.ThemeMode.Light;
#pragma warning restore WPF0001
    }

    private enum SaveVisualState { Pending, Saving, Saved, Error }
}

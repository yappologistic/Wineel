using System.Windows;
using System.Windows.Media.Imaging;
using System.IO;

namespace Wineel;

public partial class App : System.Windows.Application
{
    private SingleInstanceCoordinator? _singleInstance;
    private SettingsStore? _settingsStore;
    private SwitcherController? _controller;
    private SettingsWindow? _settingsWindow;
    private TrayService? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _singleInstance = new SingleInstanceCoordinator();
        if (!_singleInstance.IsPrimary)
        {
            _singleInstance.SignalPrimary();
            Shutdown();
            return;
        }

        var dataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Wineel");
        RollingFileLogger.Instance.Configure(Path.Combine(dataRoot, "Logs"));
        _settingsStore = new SettingsStore(Path.Combine(dataRoot, "settings.json"));
        var settings = _settingsStore.Load();
        if (_settingsStore.LastRecoveryBackup is not null)
            RollingFileLogger.Instance.Warning("Corrupt settings were backed up and defaults restored.");

        var icon = new BitmapImage();
        icon.BeginInit();
        icon.UriSource = new Uri("pack://application:,,,/Assets/wineel-icon.png", UriKind.Absolute);
        icon.CacheOption = BitmapCacheOption.OnLoad;
        icon.EndInit();
        icon.Freeze();

        var overlay = new OverlayWindow();
        _controller = new SwitcherController(_settingsStore, settings, icon, overlay);
        _settingsWindow = new SettingsWindow();
        _settingsWindow.LoadSettings(settings);
        _settingsWindow.SettingsChanged += _controller.UpdateSettings;
        _settingsWindow.TryRequested += _controller.TryWineel;
        _controller.SettingsApplied += applied => { _settingsWindow.LoadSettings(applied); _tray?.Apply(applied); };
        _controller.Notification += message => _tray?.Notify(message);

        _tray = new TrayService();
        _tray.Apply(settings);
        _tray.OpenSettings += ShowSettings;
        _tray.TryRequested += _controller.TryWineel;
        _tray.PauseRequested += _controller.TogglePause;
        _tray.ReplacementRequested += _controller.ToggleReplacement;
        _tray.StartupRequested += _controller.ToggleStartup;
        _tray.ExitRequested += Shutdown;

        DispatcherUnhandledException += (_, args) =>
        {
            RollingFileLogger.Instance.Error("Unhandled UI exception.", args.Exception);
            _tray.Notify("Wineel recovered from an unexpected error. Native Alt+Tab remains available.");
            args.Handled = true;
        };

        _singleInstance.Listen(() => Dispatcher.BeginInvoke(ShowSettings));
        _controller.Start();
        if (!settings.OnboardingCompleted || (!settings.LaunchMinimized && !e.Args.Contains("--tray", StringComparer.OrdinalIgnoreCase))) ShowSettings();
    }

    private void ShowSettings()
    {
        if (_settingsWindow is null) return;
        _settingsWindow.LoadSettings(_controller?.Settings ?? new AppSettings());
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        _tray?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}

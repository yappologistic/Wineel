using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Wineel;

public sealed class SwitcherController : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly SettingsStore _settingsStore;
    private readonly WindowEnumerator _enumerator;
    private readonly ForegroundMruService _mru = new();
    private readonly WindowActivator _activator = new();
    private readonly PointerMonitorService _pointer = new();
    private readonly FullscreenDetector _fullscreen = new();
    private readonly IconResolver _icons;
    private readonly OverlayWindow _overlay;
    private readonly KeyboardHookService _keyboard;
    private readonly HotKeyService _hotKey = new();
    private readonly StartupRegistration _startup = new();
    private readonly SwitcherStateMachine _state = new();
    private readonly WheelDeltaAccumulator _wheel = new();
    private MouseHookService? _mouse;
    private IReadOnlyList<VisualSwitcherItem> _cached = Array.Empty<VisualSwitcherItem>();
    private IReadOnlyList<WindowCandidate> _latestWindows = Array.Empty<WindowCandidate>();
    private IReadOnlyList<VisualSwitcherItem> _sessionRootItems = Array.Empty<VisualSwitcherItem>();
    private IReadOnlyList<VisualSwitcherItem> _sessionViewItems = Array.Empty<VisualSwitcherItem>();
    private IReadOnlyList<VisualSwitcherItem> _drillItems = Array.Empty<VisualSwitcherItem>();
    private VisualSwitcherItem? _drillParent;
    private string _searchQuery = string.Empty;
    private string _transientStatus = string.Empty;
    private int _refreshGeneration;
    private nint _pendingForegroundHandle;
    private int _foregroundRefreshQueued;
    private volatile bool _disposed;

    public SwitcherController(SettingsStore settingsStore, AppSettings settings, ImageSource fallbackIcon, OverlayWindow overlay)
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        _settingsStore = settingsStore;
        Settings = settings;
        _overlay = overlay;
        _enumerator = new WindowEnumerator(new VirtualDesktopService());
        _icons = new IconResolver(fallbackIcon);
        _keyboard = new KeyboardHookService(CanStartAltSession, input => _dispatcher.BeginInvoke(() => HandleKeyboard(input), DispatcherPriority.Input));
        _hotKey.Pressed += () => BeginSession(SwitcherMode.Latched);
        _mru.ForegroundChanged += QueueForegroundRefresh;
        _overlay.ItemClicked += OnItemClicked;
        _overlay.ItemSelected += OnItemSelected;
        _overlay.ItemContextRequested += OnItemContextRequested;
        _overlay.OutsideClicked += () => { if (_state.IsActive && _state.Mode == SwitcherMode.Latched) Cancel(); };
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        CaptureExclusionCandidate(Native.GetForegroundWindow());
    }

    public AppSettings Settings { get; private set; }
    public string? ExclusionCandidatePath { get; private set; }
    public event Action<AppSettings>? SettingsApplied;
    public event Action<string>? Notification;

    public void Start()
    {
        if (!_mru.Start()) Notification?.Invoke("Wineel could not start MRU tracking. Window order may be less accurate.");
        if (!_keyboard.Install()) Notification?.Invoke("Wineel could not install its keyboard hook. Native Alt+Tab remains unchanged.");
        RegisterInitialShortcut();
        _ = TryApplyStartup(Settings.StartWithWindows);
        RefreshCache();
    }

    private void RegisterInitialShortcut()
    {
        if (_hotKey.Register(Settings.FallbackShortcut)) return;
        if (HotKeyService.TryParse(Settings.FallbackShortcut, out _, out _))
        {
            Notification?.Invoke($"The shortcut {Settings.FallbackShortcut} is already in use.");
            return;
        }

        const string fallback = "Ctrl+Alt+Space";
        var invalidShortcut = Settings.FallbackShortcut;
        if (!_hotKey.Register(fallback))
        {
            Notification?.Invoke($"The saved shortcut {invalidShortcut} is invalid, and {fallback} is already in use.");
            return;
        }

        Settings = Settings with { FallbackShortcut = fallback };
        _ = SaveSettings(Settings);
        SettingsApplied?.Invoke(Settings);
        Notification?.Invoke($"The invalid shortcut {invalidShortcut} was reset to {fallback}.");
    }

    public SettingsApplyResult UpdateSettings(AppSettings settings)
    {
        var previous = Settings;
        var applied = settings;
        string? errorMessage = null;

        if (!string.Equals(previous.FallbackShortcut, settings.FallbackShortcut, StringComparison.OrdinalIgnoreCase))
        {
            if (!HotKeyService.TryParse(settings.FallbackShortcut, out _, out _))
            {
                applied = applied with { FallbackShortcut = previous.FallbackShortcut };
                errorMessage = $"The shortcut format is not supported. {previous.FallbackShortcut} remains active.";
            }
            else if (!_hotKey.Register(settings.FallbackShortcut))
            {
                applied = applied with { FallbackShortcut = previous.FallbackShortcut };
                errorMessage = $"{settings.FallbackShortcut} is unavailable. {previous.FallbackShortcut} remains active.";
            }
        }

        if (previous.StartWithWindows != applied.StartWithWindows && !TryApplyStartup(applied.StartWithWindows))
        {
            applied = applied with { StartWithWindows = previous.StartWithWindows };
            errorMessage ??= "Start with Windows could not be updated. The previous setting remains active.";
        }

        Settings = applied;
        var saved = SaveSettings(applied);
        if (!saved) errorMessage ??= "Wineel could not save changes. Check the Logs folder for details.";
        if (applied.IsPaused && _state.IsActive) Cancel();
        if (RequiresCacheRefresh(previous, applied)) RefreshCache();
        SettingsApplied?.Invoke(applied);
        if (errorMessage is not null) Notification?.Invoke(errorMessage);
        return errorMessage is null ? SettingsApplyResult.Success(applied) : SettingsApplyResult.Failure(applied, errorMessage);
    }

    private bool SaveSettings(AppSettings settings)
    {
        try
        {
            _settingsStore.Save(settings);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            RollingFileLogger.Instance.Error("Unable to save settings.", exception);
            Notification?.Invoke("Wineel could not save settings. Check the Logs folder for details.");
            return false;
        }
    }

    private static bool RequiresCacheRefresh(AppSettings previous, AppSettings current) =>
        previous.GroupingMode != current.GroupingMode
        || previous.CurrentVirtualDesktopOnly != current.CurrentVirtualDesktopOnly
        || !previous.Exclusions.SequenceEqual(current.Exclusions, StringComparer.OrdinalIgnoreCase)
        || !previous.PinnedIdentities.SequenceEqual(current.PinnedIdentities, StringComparer.OrdinalIgnoreCase);

    public void TogglePause() => UpdateSettings(Settings with { IsPaused = !Settings.IsPaused });
    public void ToggleReplacement() => UpdateSettings(Settings with { ReplaceAltTab = !Settings.ReplaceAltTab });
    public void ToggleStartup() => UpdateSettings(Settings with { StartWithWindows = !Settings.StartWithWindows });
    public void TryWineel() => BeginSession(SwitcherMode.Latched);

    private void CaptureExclusionCandidate(nint handle)
    {
        if (handle == 0 || ForegroundApplicationInfo.IsShellSurface(handle)) return;
        var path = ForegroundApplicationInfo.GetExecutablePath(handle);
        if (string.IsNullOrWhiteSpace(path) || string.Equals(path, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase)) return;
        ExclusionCandidatePath = path;
    }

    private bool CanStartAltSession()
    {
        if (_disposed || Settings.IsPaused || !Settings.ReplaceAltTab || _cached.Count < 2) return false;
        return !Settings.DisableInExclusiveFullscreen || !_fullscreen.IsExclusiveLikeFullscreen();
    }

    private void BeginSession(SwitcherMode mode)
    {
        if (_disposed || _state.IsActive || Settings.IsPaused || _cached.Count < 2) return;
        if (Settings.DisableInExclusiveFullscreen && _fullscreen.IsExclusiveLikeFullscreen()) return;
        var foreground = Native.GetForegroundWindow();
        var items = _cached.Select(item => item.Item).ToArray();
        if (!_state.TryStart(items, foreground, mode))
        {
            _keyboard.SetSessionActive(false);
            return;
        }
        _keyboard.SetSessionActive(true, mode == SwitcherMode.AltHeld);
        _wheel.Reset();
        _sessionRootItems = _cached;
        _sessionViewItems = _cached;
        _drillItems = Array.Empty<VisualSwitcherItem>();
        _drillParent = null;
        _searchQuery = string.Empty;
        _transientStatus = string.Empty;
        var monitor = _pointer.GetCurrent();
        var cursor = _pointer.GetCursorOnMonitor(monitor);
        _overlay.ShowSession(monitor, _sessionViewItems, _state.SelectedIndex, cursor, Settings, BuildStatus());
        _mouse = new MouseHookService(delta => _dispatcher.BeginInvoke(() => HandleWheel(delta), DispatcherPriority.Input));
        if (!_mouse.Install())
        {
            Notification?.Invoke("Wineel could not capture the mouse wheel. Keyboard controls remain available.");
            _mouse.Dispose();
            _mouse = null;
        }
    }

    private void HandleKeyboard(KeyboardHookInput input)
    {
        var command = input.Command;
        if (command == KeyboardHookCommand.BeginAlt) { BeginSession(SwitcherMode.AltHeld); return; }
        if (!_state.IsActive) { _keyboard.SetSessionActive(false); return; }
        switch (command)
        {
            case KeyboardHookCommand.Next when Settings.RepeatTabEnabled: Move(SwitcherCommand.Next); break;
            case KeyboardHookCommand.Previous when Settings.RepeatTabEnabled: Move(SwitcherCommand.Previous); break;
            case KeyboardHookCommand.Commit: Commit(); break;
            case KeyboardHookCommand.Cancel: Cancel(); break;
            case KeyboardHookCommand.DrillDown: EnterDrillDownOrCommit(); break;
            case KeyboardHookCommand.SearchBackspace: HandleBackspace(); break;
            case KeyboardHookCommand.SearchCharacter: AddSearchCharacter(input.Character); break;
            case KeyboardHookCommand.TogglePin: TogglePin(); break;
            case KeyboardHookCommand.AltReleased when _state.Mode == SwitcherMode.AltHeld: Commit(); break;
            case >= KeyboardHookCommand.Select0 and <= KeyboardHookCommand.Select9:
                var digit = (int)command - (int)KeyboardHookCommand.Select0;
                var viewportIndex = digit == 0 ? 9 : digit - 1;
                var capacity = WheelCapacity.Calculate(Settings.WheelSize, Settings.IconSize, Settings.MaximumVisibleIcons);
                var slots = RadialLayout.Calculate(_state.Items.Count, _state.SelectedIndex, capacity, new LogicalPoint(0, 0), 1);
                var result = _state.SelectNumber(viewportIndex, slots);
                if (result.SelectionChanged) _overlay.SetSelection(_state.SelectedIndex);
                break;
        }
    }

    private void HandleWheel(int delta)
    {
        if (!_state.IsActive) return;
        var steps = _wheel.Add(delta, Settings.ReverseWheel);
        var command = steps > 0 ? SwitcherCommand.Previous : SwitcherCommand.Next;
        for (var i = 0; i < Math.Abs(steps); i++) Move(command);
    }

    private void Move(SwitcherCommand command)
    {
        var result = _state.Handle(command, Settings.WrapSelection);
        if (result.SelectionChanged)
        {
            _transientStatus = string.Empty;
            _overlay.SetSelection(_state.SelectedIndex);
        }
    }

    private void OnItemClicked(int index)
    {
        if (!Settings.MouseClickSelection || !_state.IsActive || index < 0 || index >= _state.Items.Count) return;
        var result = _state.SelectVisible(index, false);
        if (result.SelectionChanged) _overlay.SetSelection(_state.SelectedIndex);
        if (_drillParent is null && _state.Items[index].WindowCount > 1) EnterDrillDownOrCommit();
        else Commit();
    }

    private void OnItemContextRequested(int index)
    {
        if (!_state.IsActive || index < 0 || index >= _state.Items.Count) return;
        _ = _state.SelectVisible(index, false);
        _overlay.SetSelection(_state.SelectedIndex);
        TogglePin();
    }

    private void OnItemSelected(int index)
    {
        if (!_state.IsActive || index < 0 || index >= _state.Items.Count) return;
        var result = _state.SelectVisible(index, false);
        if (result.SelectionChanged) _overlay.SetSelection(_state.SelectedIndex);
    }

    private void Commit()
    {
        if (!_state.IsActive || _state.SelectedIndex < 0 || _state.SelectedIndex >= _state.Items.Count) return;
        var attempts = _state.Items.Count;
        while (attempts-- > 0 && !Native.IsWindow(_state.Items[_state.SelectedIndex].TargetWindow)) Move(SwitcherCommand.Next);
        var result = _state.Handle(SwitcherCommand.Commit, Settings.WrapSelection);
        if (!result.Closed) return;
        CloseVisuals();
        if (result.Committed && Native.IsWindow(result.TargetWindow)) ActivateLater(result.TargetWindow);
    }

    private void Cancel()
    {
        if (!_state.IsActive) return;
        _ = _state.Handle(SwitcherCommand.Cancel, Settings.WrapSelection);
        CloseVisuals();
    }

    private void CloseVisuals()
    {
        _overlay.CloseSession();
        _mouse?.Dispose();
        _mouse = null;
        _keyboard.SetSessionActive(false);
        _wheel.Reset();
        _sessionRootItems = Array.Empty<VisualSwitcherItem>();
        _sessionViewItems = Array.Empty<VisualSwitcherItem>();
        _drillItems = Array.Empty<VisualSwitcherItem>();
        _drillParent = null;
        _searchQuery = string.Empty;
        _transientStatus = string.Empty;
    }

    private void ActivateLater(nint target) => _dispatcher.BeginInvoke(() =>
    {
        if (!_activator.Activate(target)) RollingFileLogger.Instance.Warning($"Foreground activation failed for 0x{target:X}.");
        QueueForegroundRefresh(target);
    }, DispatcherPriority.Input);

    private void QueueForegroundRefresh(nint handle)
    {
        if (_disposed) return;
        Interlocked.Exchange(ref _pendingForegroundHandle, handle);
        if (Interlocked.Exchange(ref _foregroundRefreshQueued, 1) != 0) return;
        _dispatcher.BeginInvoke(() =>
        {
            Interlocked.Exchange(ref _foregroundRefreshQueued, 0);
            var latestHandle = Interlocked.Exchange(ref _pendingForegroundHandle, 0);
            if (_disposed) return;
            CaptureExclusionCandidate(latestHandle);
            RefreshCache();
        }, DispatcherPriority.Background);
    }

    private void RefreshCache()
    {
        if (_disposed) return;
        var generation = ++_refreshGeneration;
        IReadOnlyList<WindowCandidate> windows;
        try { windows = _enumerator.Enumerate(Settings); }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            RollingFileLogger.Instance.Error("Window enumeration failed.", exception);
            return;
        }
        if (generation != _refreshGeneration) return;
        _mru.Mru.Prune(Native.IsWindow);
        _latestWindows = windows;
        var grouped = SwitcherViews.OrderPinned(
            ApplicationGrouper.Group(windows, _mru.Mru.Snapshot(), Settings.GroupingMode),
            Settings.PinnedIdentities);
        var pathByHandle = windows.ToDictionary(window => window.Handle, window => window.ExecutablePath);
        _cached = grouped.Select(item =>
        {
            var resolved = _icons.Resolve(item, pathByHandle.GetValueOrDefault(item.TargetWindow));
            var pinned = Settings.PinnedIdentities.Contains(item.Identity, StringComparer.OrdinalIgnoreCase);
            return new VisualSwitcherItem(item with { AccentColor = resolved.Accent }, resolved.Image, resolved.Accent, pinned);
        }).ToArray();
    }

    private void AddSearchCharacter(char character)
    {
        if (!char.IsLetterOrDigit(character) || _searchQuery.Length >= 32) return;
        _searchQuery += char.ToLowerInvariant(character);
        _transientStatus = string.Empty;
        ApplySessionView();
    }

    private void HandleBackspace()
    {
        if (_searchQuery.Length > 0)
        {
            _searchQuery = _searchQuery[..^1];
            _transientStatus = string.Empty;
            ApplySessionView();
            return;
        }
        if (_drillParent is not null) ExitDrillDown();
    }

    private void EnterDrillDownOrCommit()
    {
        if (!_state.IsActive || _state.SelectedIndex < 0 || _state.SelectedIndex >= _sessionViewItems.Count) return;
        if (_drillParent is not null) { Commit(); return; }
        var parent = _sessionViewItems[_state.SelectedIndex];
        if (parent.Item.WindowCount <= 1) { Commit(); return; }

        var children = SwitcherViews.CreateWindowItems(parent.Item, _latestWindows);
        if (children.Count <= 1) { Commit(); return; }
        var pathByHandle = _latestWindows.ToDictionary(window => window.Handle, window => window.ExecutablePath);
        _drillParent = parent;
        _drillItems = children.Select(item =>
        {
            var resolved = _icons.Resolve(item, pathByHandle.GetValueOrDefault(item.TargetWindow));
            return new VisualSwitcherItem(item with { AccentColor = resolved.Accent }, resolved.Image, resolved.Accent);
        }).ToArray();
        _searchQuery = string.Empty;
        var preferred = _drillItems.FirstOrDefault(item => item.Item.TargetWindow == parent.Item.TargetWindow)?.Item.Identity;
        _state.ReplaceItems(_drillItems.Select(item => item.Item).ToArray(), preferred);
        _sessionViewItems = _drillItems;
        _overlay.UpdateSession(_sessionViewItems, _state.SelectedIndex, BuildStatus());
    }

    private void ExitDrillDown()
    {
        var parentIdentity = _drillParent?.Item.Identity;
        _drillParent = null;
        _drillItems = Array.Empty<VisualSwitcherItem>();
        _searchQuery = string.Empty;
        _state.ReplaceItems(_sessionRootItems.Select(item => item.Item).ToArray(), parentIdentity);
        _sessionViewItems = _sessionRootItems;
        _overlay.UpdateSession(_sessionViewItems, _state.SelectedIndex, BuildStatus());
    }

    private void TogglePin()
    {
        if (!_state.IsActive || _state.SelectedIndex < 0 || _state.SelectedIndex >= _sessionViewItems.Count) return;
        var rootIdentity = _drillParent?.Item.Identity ?? _sessionViewItems[_state.SelectedIndex].Item.Identity;
        var wasPinned = Settings.PinnedIdentities.Contains(rootIdentity, StringComparer.OrdinalIgnoreCase);
        var pins = wasPinned
            ? Settings.PinnedIdentities.Where(identity => !string.Equals(identity, rootIdentity, StringComparison.OrdinalIgnoreCase)).ToArray()
            : Settings.PinnedIdentities.Append(rootIdentity).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        UpdateSettings(Settings with { PinnedIdentities = pins });
        _sessionRootItems = _cached;
        if (_drillParent is not null)
        {
            _drillParent = _sessionRootItems.FirstOrDefault(item => string.Equals(item.Item.Identity, rootIdentity, StringComparison.OrdinalIgnoreCase)) ?? _drillParent;
            _drillItems = SwitcherViews.CreateWindowItems(_drillParent.Item, _latestWindows)
                .Select(item => new VisualSwitcherItem(item, _drillParent.Icon, _drillParent.Accent))
                .ToArray();
        }
        _transientStatus = wasPinned ? $"Unpinned {_drillParent?.Item.DisplayName ?? rootIdentity}" : $"Pinned {_drillParent?.Item.DisplayName ?? rootIdentity}";
        ApplySessionView(rootIdentity);
    }

    private void ApplySessionView(string? preferredIdentity = null)
    {
        var source = _drillParent is null ? _sessionRootItems : _drillItems;
        var filtered = SwitcherViews.Filter(source.Select(item => item.Item).ToArray(), _searchQuery);
        var byIdentity = source.ToDictionary(item => item.Item.Identity, StringComparer.OrdinalIgnoreCase);
        _sessionViewItems = filtered.Select(item => byIdentity[item.Identity]).ToArray();
        _state.ReplaceItems(filtered, preferredIdentity);
        _overlay.UpdateSession(_sessionViewItems, _state.SelectedIndex, BuildStatus());
    }

    private string BuildStatus()
    {
        if (!string.IsNullOrWhiteSpace(_transientStatus)) return _transientStatus;
        if (_searchQuery.Length > 0) return _sessionViewItems.Count == 0 ? $"No matches for “{_searchQuery}” · Backspace to edit" : $"Search · {_searchQuery}";
        if (_drillParent is not null) return $"{_drillParent.Item.DisplayName} windows · Backspace to return";
        return "Type to search · Space: windows";
    }

    private bool TryApplyStartup(bool enabled)
    {
        try
        {
            _startup.SetEnabled(enabled, Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location);
            return true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            RollingFileLogger.Instance.Error("Unable to update the per-user startup entry.", exception);
            Notification?.Invoke("Wineel could not update Start with Windows.");
            return false;
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs eventArgs) => _dispatcher.BeginInvoke(() => { if (_state.IsActive) Cancel(); });

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        if (_state.IsActive) Cancel();
        _mouse?.Dispose();
        _keyboard.Dispose();
        _hotKey.Dispose();
        _mru.Dispose();
        _overlay.Close();
    }
}

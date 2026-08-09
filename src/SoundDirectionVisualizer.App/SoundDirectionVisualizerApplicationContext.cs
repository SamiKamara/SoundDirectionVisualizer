using Microsoft.Win32;
using SoundDirectionVisualizer.App.Native;
using SoundDirectionVisualizer.App.Services;
using SoundDirectionVisualizer.App.UI;
using SoundDirectionVisualizer.Core.Direction;
using System.Drawing;

namespace SoundDirectionVisualizer.App;

public sealed class SoundDirectionVisualizerApplicationContext : ApplicationContext
{
    private readonly object _frameGate = new();
    private readonly SettingsStore _settingsStore = new();
    private readonly AudioCaptureService _audioCapture = new();
    private readonly SteamLibraryService _steamLibraryService = new();
    private readonly GlobalHotkeyManager _hotkeyManager = new();
    private readonly DirectionOverlayForm _overlayForm = new();
    private readonly Control _uiDispatcher = new();
    private readonly EventWaitHandle _openSettingsSignal;
    private readonly RegisteredWaitHandle _openSettingsRegistration;
    private readonly Icon _trayIcon;
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _trayMenu = new();
    private readonly ToolStripMenuItem _toggleOverlayMenuItem;
    private readonly ToolStripMenuItem _autoTargetMenuItem;
    private readonly ToolStripMenuItem _monitorsMenuItem;
    private readonly ToolStripMenuItem _audioStatusMenuItem;
    private readonly Dictionary<string, ToolStripMenuItem> _monitorMenuItems = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Windows.Forms.Timer _renderTimer = new() { Interval = 33 };
    private readonly System.Windows.Forms.Timer _targetRefreshTimer = new() { Interval = 2000 };
    private readonly System.Windows.Forms.Timer? _startupSettingsTimer;
    private readonly GameWindowMonitor _gameWindowMonitor;
    private readonly GameAudioProcessResolver _gameAudioProcessResolver = new();
    private readonly NativeMethods.WinEventProc _foregroundWindowEventProc;
    private AppSettings _settings;
    private Screen _currentScreen;
    private DetectedGameTarget? _detectedGame;
    private GameAudioProcessTarget? _detectedGameAudio;
    private DirectionFrame? _latestFrame;
    private SettingsForm? _activeSettingsForm;
    private bool _autoTargetRefreshInProgress;
    private bool _pendingAutoTargetRefresh;
    private bool _pendingAutoTargetForceRefresh;
    private bool _isExiting;
    private bool _captureFailureShown;
    private string? _captureFailure;
    private int _audioCaptureGeneration;
    private int? _processCaptureFallbackNoticeProcessId;
    private IntPtr _foregroundWindowHook;

    public SoundDirectionVisualizerApplicationContext(
        EventWaitHandle openSettingsSignal,
        bool openSettingsOnStartup)
    {
        _openSettingsSignal = openSettingsSignal;
        _settings = _settingsStore.Load();
        _settings.Normalize();
        _gameWindowMonitor = new GameWindowMonitor(_steamLibraryService);
        _currentScreen = DisplayInfoFormatter.ResolveScreen(_settings.SelectedMonitorDeviceName);
        _trayIcon = LoadTrayIcon();
        _foregroundWindowEventProc = HandleForegroundWindowChanged;
        _ = _uiDispatcher.Handle;
        _openSettingsRegistration = ThreadPool.RegisterWaitForSingleObject(
            _openSettingsSignal,
            static (state, _) => ((SoundDirectionVisualizerApplicationContext)state!).RequestOpenSettings(),
            this,
            Timeout.Infinite,
            false);

        _toggleOverlayMenuItem = new ToolStripMenuItem(
            "Visualizer Enabled",
            null,
            (_, _) => ToggleOverlay());
        _autoTargetMenuItem = new ToolStripMenuItem(
            "Auto Target Steam Game Display",
            null,
            (_, _) => SetAutoTarget(!_settings.AutoDetectSteamGameMonitor));
        _monitorsMenuItem = new ToolStripMenuItem("Manual Display");
        _audioStatusMenuItem = new ToolStripMenuItem("Audio: starting...") { Enabled = false };
        var settingsMenuItem = new ToolStripMenuItem("Settings...", null, (_, _) => OpenSettings());
        var exitMenuItem = new ToolStripMenuItem("Exit", null, (_, _) => ExitApplication());

        _trayMenu.Items.AddRange(
            _toggleOverlayMenuItem,
            _autoTargetMenuItem,
            _monitorsMenuItem,
            new ToolStripSeparator(),
            _audioStatusMenuItem,
            new ToolStripSeparator(),
            settingsMenuItem,
            exitMenuItem);

        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = _trayMenu,
            Icon = _trayIcon,
            Text = "Sound Direction Visualizer",
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => OpenSettings();
        _notifyIcon.MouseClick += HandleNotifyIconMouseClick;

        _hotkeyManager.HotkeyPressed += HandleHotkeyPressed;
        _audioCapture.FrameAvailable += HandleDirectionFrame;
        _audioCapture.CaptureFailed += HandleCaptureFailed;
        _renderTimer.Tick += HandleRenderTick;
        _targetRefreshTimer.Tick += (_, _) => RefreshTargetScreen();
        SystemEvents.DisplaySettingsChanged += HandleDisplaySettingsChanged;

        _foregroundWindowHook = NativeMethods.SetWinEventHook(
            NativeMethods.EventSystemForeground,
            NativeMethods.EventSystemForeground,
            IntPtr.Zero,
            _foregroundWindowEventProc,
            0,
            0,
            NativeMethods.WinEventOutOfContext | NativeMethods.WinEventSkipOwnProcess);

        RebuildMonitorMenu();
        RegisterHotkeys();
        _overlayForm.ApplySettings(_settings);
        _overlayForm.SetTargetScreen(_currentScreen);
        ApplyOverlayState();
        StartAudioCapture();
        RefreshTargetScreen(force: true);
        _renderTimer.Start();
        _targetRefreshTimer.Start();

        if (openSettingsOnStartup)
        {
            _startupSettingsTimer = new System.Windows.Forms.Timer { Interval = 50 };
            _startupSettingsTimer.Tick += HandleStartupSettingsTimerTick;
            _startupSettingsTimer.Start();
        }
        else
        {
            ShowStartupHint();
        }
    }

    private void HandleDirectionFrame(object? sender, DirectionFrame frame)
    {
        lock (_frameGate)
        {
            _latestFrame = frame;
        }
    }

    private void HandleCaptureFailed(object? sender, string message)
    {
        lock (_frameGate)
        {
            _captureFailure = message;
        }
    }

    private void HandleRenderTick(object? sender, EventArgs eventArgs)
    {
        DirectionFrame? frame;
        string? failure;

        lock (_frameGate)
        {
            frame = _latestFrame;
            failure = _captureFailure;
        }

        _overlayForm.UpdateFrame(frame, DateTimeOffset.UtcNow);

        if (failure is not null && !_captureFailureShown)
        {
            _captureFailureShown = true;
            _audioStatusMenuItem.Text = "Audio: capture error";
            _notifyIcon.ShowBalloonTip(
                5000,
                "Audio capture error",
                failure,
                ToolTipIcon.Error);
        }
    }

    private async void StartAudioCapture()
    {
        var generation = ++_audioCaptureGeneration;
        var settings = _settings.Clone();
        var preferredGame = settings.PreferDetectedGameAudio ? _detectedGameAudio : null;

        lock (_frameGate)
        {
            _latestFrame = null;
            _captureFailure = null;
        }

        _captureFailureShown = false;

        try
        {
            await _audioCapture.StartAsync(
                settings,
                preferredGame?.ProcessId,
                preferredGame?.ProcessName);

            if (_isExiting || generation != _audioCaptureGeneration)
            {
                return;
            }

            _audioStatusMenuItem.Text = $"Audio: {_audioCapture.ActiveDeviceName}";

            if (_audioCapture.ProcessCaptureFallbackReason is not null
                && preferredGame is not null
                && _processCaptureFallbackNoticeProcessId != preferredGame.ProcessId)
            {
                _processCaptureFallbackNoticeProcessId = preferredGame.ProcessId;
                _notifyIcon.ShowBalloonTip(
                    6000,
                    "Game audio capture fallback",
                    $"Direct capture from {preferredGame.ProcessName} was unavailable. " +
                    "The selected output device is being analyzed instead.",
                    ToolTipIcon.Warning);
            }
            else if (_audioCapture.IsProcessCapture)
            {
                _processCaptureFallbackNoticeProcessId = null;
            }
        }
        catch (Exception exception)
        {
            if (_isExiting || generation != _audioCaptureGeneration)
            {
                return;
            }

            _audioStatusMenuItem.Text = "Audio: unavailable";
            _captureFailureShown = true;
            _notifyIcon.ShowBalloonTip(
                6000,
                "Sound Direction Visualizer",
                exception.Message,
                ToolTipIcon.Error);
        }
    }

    private void HandleHotkeyPressed(object? sender, HotkeyAction action)
    {
        switch (action)
        {
            case HotkeyAction.ToggleOverlay:
                ToggleOverlay();
                break;
            case HotkeyAction.CycleMonitor:
                CycleMonitor();
                break;
            case HotkeyAction.OpenSettings:
                OpenSettings();
                break;
        }
    }

    private void ToggleOverlay()
    {
        _settings.OverlayEnabled = !_settings.OverlayEnabled;
        PersistSettings();
        ApplyOverlayState();
        UpdateMenuState();
    }

    private void SetAutoTarget(bool enabled)
    {
        _settings.AutoDetectSteamGameMonitor = enabled;
        PersistSettings();
        RefreshTargetScreen(force: true);
    }

    private void SelectManualMonitor(string deviceName)
    {
        _settings.AutoDetectSteamGameMonitor = false;
        _settings.SelectedMonitorDeviceName = deviceName;
        PersistSettings();
        RefreshTargetScreen(force: true);
    }

    private void CycleMonitor()
    {
        var screens = Screen.AllScreens;
        if (screens.Length == 0)
        {
            return;
        }

        var currentIndex = Array.FindIndex(screens, screen =>
            string.Equals(screen.DeviceName, _currentScreen.DeviceName, StringComparison.OrdinalIgnoreCase));
        var next = screens[(Math.Max(0, currentIndex) + 1) % screens.Length];
        SelectManualMonitor(next.DeviceName);
    }

    private void OpenSettings()
    {
        if (_activeSettingsForm is not null && !_activeSettingsForm.IsDisposed)
        {
            if (_activeSettingsForm.WindowState == FormWindowState.Minimized)
            {
                _activeSettingsForm.WindowState = FormWindowState.Normal;
            }

            _activeSettingsForm.Activate();
            return;
        }

        _hotkeyManager.ClearBindings();
        _activeSettingsForm = new SettingsForm(_settings);
        _activeSettingsForm.OverlayPreviewChanged += HandleOverlayPreviewChanged;
        var settingsSaved = false;

        try
        {
            if (_activeSettingsForm.ShowDialog() == DialogResult.OK)
            {
                _settings = _activeSettingsForm.ResultSettings.Clone();
                settingsSaved = true;
                PersistSettings();
                _overlayForm.ApplySettings(_settings);
                RebuildMonitorMenu();
                StartAudioCapture();
                RefreshTargetScreen(force: true);
                ApplyOverlayState();
            }
        }
        finally
        {
            _activeSettingsForm.OverlayPreviewChanged -= HandleOverlayPreviewChanged;

            if (!settingsSaved)
            {
                _overlayForm.ApplySettings(_settings);
                ApplyOverlayState();
            }

            _activeSettingsForm.Dispose();
            _activeSettingsForm = null;
            RegisterHotkeys();
        }
    }

    private void HandleOverlayPreviewChanged(AppSettings previewSettings)
    {
        _overlayForm.ApplySettings(previewSettings);

        if (previewSettings.OverlayEnabled)
        {
            if (!_overlayForm.Visible)
            {
                _overlayForm.Show();
            }
        }
        else if (_overlayForm.Visible)
        {
            _overlayForm.Hide();
        }
    }

    private void RequestOpenSettings()
    {
        try
        {
            if (!_uiDispatcher.IsDisposed)
            {
                _uiDispatcher.BeginInvoke(new MethodInvoker(OpenSettings));
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void RegisterHotkeys()
    {
        var bindings = new Dictionary<HotkeyAction, HotkeyDefinition>
        {
            [HotkeyAction.ToggleOverlay] = _settings.ToggleHotkey,
            [HotkeyAction.CycleMonitor] = _settings.CycleMonitorHotkey,
            [HotkeyAction.OpenSettings] = _settings.OpenSettingsHotkey
        };
        _hotkeyManager.ReplaceBindings(bindings, out var failures);

        if (failures.Count > 0)
        {
            _notifyIcon.ShowBalloonTip(
                4000,
                "Hotkey warning",
                "One or more hotkeys are already in use by another application.",
                ToolTipIcon.Warning);
        }
    }

    private void RefreshTargetScreen(bool force = false)
    {
        if (_isExiting)
        {
            return;
        }

        if (!_settings.AutoDetectSteamGameMonitor && !_settings.PreferDetectedGameAudio)
        {
            var previousProcessId = _detectedGame?.ProcessId;
            _detectedGame = null;
            _detectedGameAudio = null;
            if (previousProcessId.HasValue || _audioCapture.IsProcessCapture)
            {
                StartAudioCapture();
            }

            ApplyResolvedScreen(DisplayInfoFormatter.ResolveScreen(_settings.SelectedMonitorDeviceName), force);
            return;
        }

        QueueAutoTargetRefresh(force);
    }

    private void QueueAutoTargetRefresh(bool force)
    {
        _pendingAutoTargetRefresh = true;
        _pendingAutoTargetForceRefresh |= force;

        if (!_autoTargetRefreshInProgress)
        {
            _ = RefreshAutoTargetAsync();
        }
    }

    private async Task RefreshAutoTargetAsync()
    {
        if (_autoTargetRefreshInProgress)
        {
            return;
        }

        _autoTargetRefreshInProgress = true;

        try
        {
            while (_pendingAutoTargetRefresh)
            {
                var force = _pendingAutoTargetForceRefresh;
                _pendingAutoTargetRefresh = false;
                _pendingAutoTargetForceRefresh = false;

                var preferDetectedGameAudio = _settings.PreferDetectedGameAudio;
                var detection = await Task.Run(() =>
                {
                    try
                    {
                        var game = _gameWindowMonitor.Detect();
                        var audio = preferDetectedGameAudio && game is not null
                            ? _gameAudioProcessResolver.Resolve(game)
                            : null;
                        return (Game: game, Audio: audio);
                    }
                    catch
                    {
                        return (Game: (DetectedGameTarget?)null, Audio: (GameAudioProcessTarget?)null);
                    }
                });

                if (_isExiting
                    || (!_settings.AutoDetectSteamGameMonitor && !_settings.PreferDetectedGameAudio))
                {
                    return;
                }

                var previousPreferredProcessId = _settings.PreferDetectedGameAudio
                    ? _detectedGameAudio?.ProcessId
                    : null;
                _detectedGame = detection.Game;
                _detectedGameAudio = detection.Audio;
                var nextPreferredProcessId = _settings.PreferDetectedGameAudio
                    ? detection.Audio?.ProcessId
                    : null;
                if (previousPreferredProcessId != nextPreferredProcessId)
                {
                    StartAudioCapture();
                }

                var resolvedScreen = _settings.AutoDetectSteamGameMonitor
                    ? detection.Game?.Screen ?? _currentScreen
                    : DisplayInfoFormatter.ResolveScreen(_settings.SelectedMonitorDeviceName);
                ApplyResolvedScreen(resolvedScreen, force);
            }
        }
        finally
        {
            _autoTargetRefreshInProgress = false;

            if (!_isExiting && _pendingAutoTargetRefresh)
            {
                _ = RefreshAutoTargetAsync();
            }
        }
    }

    private void ApplyResolvedScreen(Screen screen, bool force)
    {
        var changed = force || !string.Equals(
            screen.DeviceName,
            _currentScreen.DeviceName,
            StringComparison.OrdinalIgnoreCase);
        _currentScreen = screen;

        if (!_settings.AutoDetectSteamGameMonitor)
        {
            _settings.SelectedMonitorDeviceName = screen.DeviceName;
        }

        if (changed)
        {
            _overlayForm.SetTargetScreen(screen);
        }

        ApplyOverlayState();
        UpdateMenuState();
    }

    private void ApplyOverlayState()
    {
        if (_settings.OverlayEnabled)
        {
            if (!_overlayForm.Visible)
            {
                _overlayForm.Show();
            }
            else
            {
                _overlayForm.Invalidate();
            }
        }
        else if (_overlayForm.Visible)
        {
            _overlayForm.Hide();
        }
    }

    private void RebuildMonitorMenu()
    {
        _monitorMenuItems.Clear();
        _monitorsMenuItem.DropDownItems.Clear();

        foreach (var screen in Screen.AllScreens)
        {
            var item = new ToolStripMenuItem(
                DisplayInfoFormatter.ToDisplayLabel(screen),
                null,
                (_, _) => SelectManualMonitor(screen.DeviceName));
            _monitorMenuItems[screen.DeviceName] = item;
            _monitorsMenuItem.DropDownItems.Add(item);
        }

        UpdateMenuState();
    }

    private void UpdateMenuState()
    {
        _toggleOverlayMenuItem.Checked = _settings.OverlayEnabled;
        _autoTargetMenuItem.Checked = _settings.AutoDetectSteamGameMonitor;

        foreach (var item in _monitorMenuItems)
        {
            item.Value.Checked = !_settings.AutoDetectSteamGameMonitor
                && string.Equals(item.Key, _currentScreen.DeviceName, StringComparison.OrdinalIgnoreCase);
        }

        var mode = _settings.AutoDetectSteamGameMonitor && _detectedGame is not null
            ? $"Auto: {_detectedGame.ProcessName}"
            : DisplayInfoFormatter.ToDisplayLabel(_currentScreen);
        var text = $"Sound Direction Visualizer - {mode}";
        _notifyIcon.Text = text.Length > 63 ? text[..63] : text;
    }

    private void HandleForegroundWindowChanged(
        IntPtr hook,
        uint eventType,
        IntPtr windowHandle,
        int objectId,
        int childId,
        uint eventThread,
        uint eventTime)
    {
        if (_isExiting
            || (!_settings.AutoDetectSteamGameMonitor && !_settings.PreferDetectedGameAudio)
            || _uiDispatcher.IsDisposed)
        {
            return;
        }

        try
        {
            _uiDispatcher.BeginInvoke(new MethodInvoker(() => RefreshTargetScreen(force: true)));
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void HandleDisplaySettingsChanged(object? sender, EventArgs eventArgs)
    {
        RebuildMonitorMenu();
        RefreshTargetScreen(force: true);
    }

    private void HandleNotifyIconMouseClick(object? sender, MouseEventArgs eventArgs)
    {
        if (eventArgs.Button == MouseButtons.Left)
        {
            OpenSettings();
        }
    }

    private void HandleStartupSettingsTimerTick(object? sender, EventArgs eventArgs)
    {
        if (_startupSettingsTimer is null)
        {
            return;
        }

        _startupSettingsTimer.Stop();
        _startupSettingsTimer.Tick -= HandleStartupSettingsTimerTick;
        OpenSettings();
    }

    private void ShowStartupHint()
    {
        _notifyIcon.ShowBalloonTip(
            4500,
            "Sound Direction Visualizer is running",
            $"Toggle: {_settings.ToggleHotkey.ToDisplayString()} | Settings: {_settings.OpenSettingsHotkey.ToDisplayString()}",
            ToolTipIcon.Info);
    }

    private void PersistSettings()
    {
        _settings.Normalize();
        _settingsStore.Save(_settings);
    }

    private void ExitApplication()
    {
        _isExiting = true;
        _notifyIcon.Visible = false;
        ExitThread();
    }

    protected override void ExitThreadCore()
    {
        _isExiting = true;
        _audioCaptureGeneration++;
        _openSettingsRegistration.Unregister(null);
        _startupSettingsTimer?.Stop();
        if (_startupSettingsTimer is not null)
        {
            _startupSettingsTimer.Tick -= HandleStartupSettingsTimerTick;
            _startupSettingsTimer.Dispose();
        }

        _renderTimer.Stop();
        _targetRefreshTimer.Stop();
        _renderTimer.Dispose();
        _targetRefreshTimer.Dispose();

        if (_foregroundWindowHook != IntPtr.Zero)
        {
            _ = NativeMethods.UnhookWinEvent(_foregroundWindowHook);
            _foregroundWindowHook = IntPtr.Zero;
        }

        _audioCapture.FrameAvailable -= HandleDirectionFrame;
        _audioCapture.CaptureFailed -= HandleCaptureFailed;
        _audioCapture.Dispose();
        _hotkeyManager.Dispose();
        _overlayForm.Dispose();
        _uiDispatcher.Dispose();
        _notifyIcon.Dispose();
        _trayIcon.Dispose();
        _trayMenu.Dispose();
        SystemEvents.DisplaySettingsChanged -= HandleDisplaySettingsChanged;
        base.ExitThreadCore();
    }

    private static Icon LoadTrayIcon()
    {
        try
        {
            var extracted = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (extracted is not null)
            {
                return (Icon)extracted.Clone();
            }
        }
        catch
        {
        }

        return (Icon)SystemIcons.Application.Clone();
    }
}

using SoundDirectionVisualizer.App.Native;
using SoundDirectionVisualizer.App.Services;

namespace SoundDirectionVisualizer.App.UI;

public sealed class SettingsForm : Form
{
    private readonly CheckBox _overlayEnabled = DarkUiTheme.CreateCheckBox("Enable overlay");
    private readonly ComboBox _audioDevice = CreateComboBox();
    private readonly CheckBox _useDetectedGameProcessAudio = DarkUiTheme.CreateCheckBox(
        "Capture only the detected Steam game's process audio (optional)");
    private readonly CheckBox _useBestAvailableMultichannelAudio = DarkUiTheme.CreateCheckBox(
        "Automatically use verified multichannel game audio when available");
    private readonly CheckBox _debugForceMultichannelSource = DarkUiTheme.CreateCheckBox(
        "Debug: force multichannel source when available");
    private readonly CheckBox _automaticallyFallbackToGameProcessAudio = DarkUiTheme.CreateCheckBox(
        "Automatically try game-process audio when a running game's output stays centered");
    private readonly CheckBox _automaticAudioCalibration = DarkUiTheme.CreateCheckBox(
        "Automatically adapt to output level and stereo width (recommended)");
    private readonly NumericUpDown _silenceThreshold = CreateDecimalNumeric(0.00001m, 0.1m, 5, 0.00010m);
    private readonly NumericUpDown _smoothing = CreateDecimalNumeric(0.01m, 1m, 2, 0.01m);
    private readonly NumericUpDown _modelBalance = CreateDecimalNumeric(0.05m, 1m, 2, 0.05m);
    private readonly CheckBox _loudSoundEmphasis = DarkUiTheme.CreateCheckBox("Emphasize loud sounds separately");
    private readonly NumericUpDown _loudSoundThreshold = CreateDecimalNumeric(1.1m, 10m, 1, 0.1m);
    private readonly CheckBox _autoDetect = DarkUiTheme.CreateCheckBox(
        "Automatically target a running Steam game's display");
    private readonly ComboBox _monitor = CreateComboBox();
    private readonly NumericUpDown _scale = CreateIntegerNumeric(10, 200, 5);
    private readonly NumericUpDown _thickness = CreateIntegerNumeric(1, 12);
    private readonly NumericUpDown _markerSize = CreateIntegerNumeric(4, 32);
    private readonly NumericUpDown _ambientMarkerSize = CreateIntegerNumeric(25, 300, 5);
    private readonly DarkSlider _ambientMarkerOpacity = CreatePercentageSlider(10);
    private readonly NumericUpDown _loudMarkerSize = CreateIntegerNumeric(25, 300, 5);
    private readonly DarkSlider _loudMarkerOpacity = CreatePercentageSlider(10);
    private readonly CheckBox _loudMarkerOutline = DarkUiTheme.CreateCheckBox("Outline loud markers");
    private readonly NumericUpDown _loudMarkerOutlineThickness = CreateDecimalNumeric(0.1m, 8m, 1, 0.1m);
    private readonly DarkSlider _opacitySlider = CreatePercentageSlider(0);
    private readonly NumericUpDown _horizontalOffset = CreateIntegerNumeric(-4000, 4000);
    private readonly NumericUpDown _verticalOffset = CreateIntegerNumeric(-4000, 4000);
    private readonly NumericUpDown _trailDuration = CreateDecimalNumeric(0.5m, 15m, 1, 0.5m);
    private readonly CheckBox _showRing = DarkUiTheme.CreateCheckBox("Show compass ring");
    private readonly CheckBox _showTicks = DarkUiTheme.CreateCheckBox("Show cardinal tick marks");
    private readonly CheckBox _showCurrentRays = DarkUiTheme.CreateCheckBox("Show current direction rays");
    private readonly CheckBox _showCurrentMarkers = DarkUiTheme.CreateCheckBox("Show current direction markers");
    private readonly CheckBox _showListenerDot = DarkUiTheme.CreateCheckBox("Show center listener dot");
    private readonly CheckBox _showTrail = DarkUiTheme.CreateCheckBox("Show fading direction trail");
    private readonly CheckBox _showLabels = DarkUiTheme.CreateCheckBox("Show F / B / L / R labels");
    private readonly Button _colorButton = DarkUiTheme.CreateButton("Change", primary: false, 92);
    private readonly Panel _colorPreview = CreateColorPreview();
    private readonly Button _ambientMarkerColorButton = DarkUiTheme.CreateButton("Change", primary: false, 92);
    private readonly Panel _ambientMarkerColorPreview = CreateColorPreview();
    private readonly Button _loudMarkerColorButton = DarkUiTheme.CreateButton("Change", primary: false, 92);
    private readonly Panel _loudMarkerColorPreview = CreateColorPreview();
    private readonly Button _loudOutlineColorButton = DarkUiTheme.CreateButton("Change", primary: false, 92);
    private readonly Panel _loudOutlineColorPreview = CreateColorPreview();
    private readonly HotkeyTextBox _toggleHotkey = CreateHotkeyTextBox();
    private readonly HotkeyTextBox _cycleHotkey = CreateHotkeyTextBox();
    private readonly HotkeyTextBox _openSettingsHotkey = CreateHotkeyTextBox();
    private readonly Label _statusSource = CreateStatusValueLabel();
    private readonly Label _statusSourcePolicy = CreateStatusValueLabel();
    private readonly Label _statusMethod = CreateStatusValueLabel();
    private readonly Label _statusEstimator = CreateStatusValueLabel();
    private readonly Label _statusFormat = CreateStatusValueLabel();
    private readonly Label _statusRequestedLayout = CreateStatusValueLabel();
    private readonly Label _statusObservedLayout = CreateStatusValueLabel();
    private readonly Label _statusValidation = CreateStatusValueLabel();
    private readonly Label _statusFallbackReason = CreateStatusValueLabel();
    private readonly Label _statusNextRetry = CreateStatusValueLabel();
    private readonly ChannelLevelMeter _channelLevelMeter = new();
    private readonly TextBox _statusEventLog = CreateStatusEventLog();
    private readonly Panel _tabsHost = new();
    private readonly List<TableLayoutPanel> _contentStacks = [];
    private readonly List<Label> _wrappingLabels = [];
    private Control? _channelVisualizationCard;
    private readonly Icon _windowIcon = LoadWindowIcon();
    private string _selectedColorHex = "#FFFFFF";
    private string _selectedAmbientMarkerColorHex = "#FFFFFF";
    private string _selectedLoudMarkerColorHex = "#FFFFFF";
    private string _selectedLoudOutlineColorHex = "#000000";
    private bool _isLoading = true;

    public SettingsForm(AppSettings settings)
        : this(settings, AudioStatusSnapshot.Empty)
    {
    }

    internal SettingsForm(AppSettings settings, AudioStatusSnapshot statusSnapshot)
    {
        Text = "Sound Direction Visualizer";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(720, 620);
        ClientSize = new Size(920, 780);
        ShowInTaskbar = true;
        MaximizeBox = false;
        MinimizeBox = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = DarkUiTheme.WindowBackground;
        ForeColor = DarkUiTheme.PrimaryText;
        Font = new Font("Segoe UI", 9.25F, FontStyle.Regular, GraphicsUnit.Point);
        Icon = _windowIcon;
        ShowIcon = true;

        ResultSettings = settings.Clone();
        BuildLayout();
        DarkUiTheme.ApplyTo(this);
        LoadSettings(settings);
        UpdateStatus(statusSnapshot);
        _isLoading = false;

        Shown += (_, _) => FitToWorkingArea();
        Resize += (_, _) => UpdateWrappingWidths();
    }

    public AppSettings ResultSettings { get; private set; }

    public event Action<AppSettings>? OverlayPreviewChanged;

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            BackColor = DarkUiTheme.WindowBackground,
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));

        var tabs = new DarkTabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildAudioTab());
        tabs.TabPages.Add(BuildOverlayTab());
        tabs.TabPages.Add(BuildTargetingTab());
        tabs.TabPages.Add(BuildStatusTab());
        tabs.TabPages.Add(BuildHotkeysTab());

        _tabsHost.BackColor = DarkUiTheme.WindowBackground;
        _tabsHost.Dock = DockStyle.Fill;
        _tabsHost.Margin = new Padding(18, 0, 18, 0);
        _tabsHost.Controls.Add(tabs);
        _tabsHost.ClientSizeChanged += (_, _) => UpdateWrappingWidths();
        tabs.SelectedIndexChanged += (_, _) => UpdateWrappingWidths();

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(_tabsHost, 0, 1);
        root.Controls.Add(BuildFooter(), 0, 2);
        Controls.Add(root);

        _autoDetect.CheckedChanged += (_, _) => _monitor.Enabled = !_autoDetect.Checked;
        _automaticAudioCalibration.CheckedChanged += (_, _) => UpdateAudioCalibrationControls();
        _loudSoundEmphasis.CheckedChanged += (_, _) =>
        {
            UpdateLoudSoundControls();
            NotifyOverlayPreviewChanged();
        };
        _showTrail.CheckedChanged += (_, _) => _trailDuration.Enabled = _showTrail.Checked;
        _loudMarkerOutline.CheckedChanged += (_, _) =>
        {
            UpdateLoudMarkerOutlineControls();
            NotifyOverlayPreviewChanged();
        };
        _colorButton.Click += (_, _) => ChooseColor();
        _ambientMarkerColorButton.Click += (_, _) => ChooseAmbientMarkerColor();
        _loudMarkerColorButton.Click += (_, _) => ChooseLoudMarkerColor();
        _loudOutlineColorButton.Click += (_, _) => ChooseLoudOutlineColor();

        foreach (var numeric in new[]
                 {
                     _scale,
                     _thickness,
                     _markerSize,
                     _ambientMarkerSize,
                     _loudMarkerSize,
                     _loudMarkerOutlineThickness,
                     _horizontalOffset,
                     _verticalOffset,
                     _trailDuration
                 })
        {
            numeric.ValueChanged += (_, _) => NotifyOverlayPreviewChanged();
        }

        BindPercentageSlider(_ambientMarkerOpacity);
        BindPercentageSlider(_loudMarkerOpacity);

        _overlayEnabled.CheckedChanged += (_, _) => NotifyOverlayPreviewChanged();
        foreach (var toggle in new[]
                 {
                     _showRing,
                     _showTicks,
                     _showCurrentRays,
                     _showCurrentMarkers,
                     _showListenerDot,
                     _showTrail,
                     _showLabels
                 })
        {
            toggle.CheckedChanged += (_, _) => NotifyOverlayPreviewChanged();
        }

        _opacitySlider.ValueChanged += (_, _) =>
        {
            NotifyOverlayPreviewChanged();
        };
    }

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel
        {
            AutoSize = true,
            BackColor = DarkUiTheme.WindowBackground,
            ColumnCount = 1,
            Dock = DockStyle.Top,
            Margin = Padding.Empty,
            Padding = new Padding(22, 18, 22, 14)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var title = new Label
        {
            AutoSize = true,
            Font = new Font(Font.FontFamily, 18F, FontStyle.Bold),
            ForeColor = DarkUiTheme.PrimaryText,
            Margin = Padding.Empty,
            Text = "Sound Direction Visualizer"
        };
        var subtitle = CreateWrappingLabel(
            "Tune audio analysis, overlay appearance, display targeting, and global shortcuts, or inspect the live capture status.",
            DarkUiTheme.SecondaryText);
        subtitle.Margin = new Padding(0, 4, 0, 0);

        header.Controls.Add(title, 0, 0);
        header.Controls.Add(subtitle, 0, 1);
        return header;
    }

    private TabPage BuildAudioTab()
    {
        var page = CreateTab("Audio");
        var content = CreateContentStack();

        var sourceCard = CreateCard(
            "Audio source",
            "Normally keep the selected stereo endpoint as the baseline and promote richer game-process audio only after validation. The debug override below can force an available multichannel source. The default endpoint follows the Windows multimedia output.");
        AddRow(sourceCard, "Output device", _audioDevice);
        AddWideRow(sourceCard, _useBestAvailableMultichannelAudio);
        AddWideRow(sourceCard, _debugForceMultichannelSource);
        AddWideRow(sourceCard, _useDetectedGameProcessAudio);
        AddWideRow(sourceCard, _automaticallyFallbackToGameProcessAudio);
        AddWideRow(sourceCard, CreateNote(
            "The automatic best-available path checks standard 7.1/5.1 process audio while endpoint stereo remains active, and switches only after independent side or rear content is verified. " +
            "The debug force option switches to an available 7.1/5.1 process source before content validation; uninformative channels still use an honest stereo fold-down. " +
            "Manual game-process capture can preserve stereo direction when a headset or spatial-audio driver exposes only dual mono at the physical output. " +
            "The automatic fallback tries it after eight seconds of audible centered output while a Steam game is running."));
        AddContent(content, sourceCard);

        var analysisCard = CreateCard(
            "Direction analysis",
            "Automatic calibration is the recommended path. Manual controls remain available for known, stable audio pipelines.");
        AddWideRow(analysisCard, _automaticAudioCalibration);
        AddRow(analysisCard, "Silence threshold (RMS)", _silenceThreshold);
        AddRow(analysisCard, "Smoothing factor", _smoothing);
        AddRow(analysisCard, "Manual hard-pan balance", _modelBalance);
        AddWideRow(analysisCard, _loudSoundEmphasis);
        AddRow(analysisCard, "Loud sound threshold (× ambience)", _loudSoundThreshold);
        AddWideRow(analysisCard, CreateNote(
            "Automatic calibration adapts the silence gate and usual stereo width. Loud-sound emphasis compares each frame with the recent ambience median; " +
            "a larger multiplier marks fewer sounds as loud."));
        AddContent(content, analysisCard);

        AddContent(content, CreateInfoCard(
            "Stereo uncertainty",
            "Stereo identifies left/right balance but normally cannot distinguish front from back. The overlay intentionally shows both mathematically valid candidates."));

        page.Controls.Add(content);
        return page;
    }

    private TabPage BuildOverlayTab()
    {
        var page = CreateTab("Overlay");
        var content = CreateContentStack();

        var appearanceCard = CreateCard(
            "Global appearance",
            "These settings apply to the complete visualizer and update the overlay while you edit.");
        AddWideRow(appearanceCard, _overlayEnabled);
        AddRow(appearanceCard, "Compass, ray, and label color", CreateColorPanel(_colorPreview, _colorButton));
        AddRow(appearanceCard, "Opacity", _opacitySlider);
        AddRow(appearanceCard, "Size (% of display height)", _scale);
        AddRow(appearanceCard, "Line thickness (px)", _thickness);
        AddRow(appearanceCard, "Base marker size (px)", _markerSize);
        AddRow(appearanceCard, "Horizontal offset (px)", _horizontalOffset);
        AddRow(appearanceCard, "Vertical offset (px)", _verticalOffset);
        AddWideRow(appearanceCard, CreateNote(
            "Size scales the ring, lines, markers, labels, and padding together. Offsets are relative to the target display center; positive Y moves the overlay down."));
        AddContent(content, appearanceCard);

        AddContent(content, BuildAmbientMarkerGroup());
        AddContent(content, BuildLoudMarkerGroup());
        AddContent(content, CreateInfoCard(
            "Marker layers",
            "Size is relative to the base marker size. Current and delayed markers share the same type-specific styling, while trail age still shrinks and fades them. " +
            "Marker opacity is relative to the overlay's global opacity."));

        var elementsCard = CreateCard(
            "Visible elements",
            "Build the visualizer from independent layers. Hidden layers retain their settings.");
        AddWideRow(elementsCard, BuildElementToggleGrid());
        AddRow(elementsCard, "Trail duration (seconds)", _trailDuration);
        AddContent(content, elementsCard);

        page.Controls.Add(content);
        return page;
    }

    private TabPage BuildTargetingTab()
    {
        var page = CreateTab("Target display");
        var content = CreateContentStack();

        var targetCard = CreateCard(
            "Display targeting",
            "Follow a detected Steam game automatically or pin the overlay to a specific display.");
        AddWideRow(targetCard, _autoDetect);
        AddRow(targetCard, "Manual display", _monitor);
        AddWideRow(targetCard, CreateNote(
            "Automatic targeting verifies that the foreground executable is inside a Steam library and follows its display. " +
            "When no valid game is detected, the current display remains selected."));
        AddContent(content, targetCard);

        AddContent(content, CreateInfoCard(
            "Game display mode",
            "The overlay works best with borderless-windowed games. Exclusive fullscreen and some anti-cheat or protected presentation paths can prevent third-party topmost windows from appearing."));

        page.Controls.Add(content);
        return page;
    }

    private GroupBox BuildAmbientMarkerGroup()
    {
        var group = CreateSettingsGroup("Ambient markers");
        var layout = CreateTwoColumnTable(DarkUiTheme.CardBackground);
        layout.Font = Font;
        AddRow(layout, "Size (% of base)", _ambientMarkerSize);
        AddRow(layout, "Opacity", _ambientMarkerOpacity);
        AddRow(layout, "Fill color", CreateColorPanel(_ambientMarkerColorPreview, _ambientMarkerColorButton));
        group.Controls.Add(layout);
        return group;
    }

    private GroupBox BuildLoudMarkerGroup()
    {
        var group = CreateSettingsGroup("Loud markers");
        var layout = CreateTwoColumnTable(DarkUiTheme.CardBackground);
        layout.Font = Font;
        AddRow(layout, "Size (% of base)", _loudMarkerSize);
        AddRow(layout, "Opacity", _loudMarkerOpacity);
        AddRow(layout, "Fill color", CreateColorPanel(_loudMarkerColorPreview, _loudMarkerColorButton));
        AddWideRow(layout, _loudMarkerOutline);
        AddRow(layout, "Outline color", CreateColorPanel(_loudOutlineColorPreview, _loudOutlineColorButton));
        AddRow(layout, "Outline thickness (px)", _loudMarkerOutlineThickness);
        group.Controls.Add(layout);
        return group;
    }

    private TabPage BuildHotkeysTab()
    {
        var page = CreateTab("Hotkeys");
        var content = CreateContentStack();

        var hotkeysCard = CreateCard(
            "Global shortcuts",
            "Shortcuts work while another application or game has focus.");
        AddRow(hotkeysCard, "Toggle overlay", _toggleHotkey);
        AddRow(hotkeysCard, "Cycle displays", _cycleHotkey);
        AddRow(hotkeysCard, "Open settings", _openSettingsHotkey);
        AddWideRow(hotkeysCard, CreateNote(
            "Focus a shortcut field and press a key combination. Press Delete to clear an optional binding. The overlay toggle remains required."));
        AddContent(content, hotkeysCard);

        page.Controls.Add(content);
        return page;
    }

    private TabPage BuildStatusTab()
    {
        var page = CreateTab("Status");
        var content = CreateContentStack();

        var currentCard = CreateCard(
            "Current audio path",
            "This is the capture and direction-estimation path currently feeding the overlay. Values update while this window is open.");
        AddRow(currentCard, "Source", _statusSource);
        AddRow(currentCard, "Source policy", _statusSourcePolicy);
        AddRow(currentCard, "Capture method", _statusMethod);
        AddRow(currentCard, "Direction estimator", _statusEstimator);
        AddRow(currentCard, "Audio format", _statusFormat);
        AddRow(currentCard, "Requested layout", _statusRequestedLayout);
        AddRow(currentCard, "Observed layout", _statusObservedLayout);
        AddRow(currentCard, "Validation state", _statusValidation);
        AddRow(currentCard, "Fallback reason", _statusFallbackReason);
        AddRow(currentCard, "Next multichannel retry", _statusNextRetry);
        AddContent(content, currentCard);

        var channelCard = CreateCard(
            "Live monitored channels",
            "Debug view of every channel currently received from the active capture source. Levels use a −60…0 dBFS scale; LFE is shown for diagnostics but is not used for direction estimation.");
        AddWideRow(channelCard, _channelLevelMeter, new Padding(0, 3, 0, 0));
        _channelVisualizationCard = channelCard;
        AddContent(content, channelCard);

        var eventCard = CreateCard(
            "Session event log",
            "Recent in-memory events explain why the capture method changed or why a fallback was retained. The newest event is shown first; audio is never written to this log.");
        AddWideRow(eventCard, _statusEventLog, new Padding(0, 3, 0, 0));
        AddContent(content, eventCard);

        page.Controls.Add(content);
        return page;
    }

    internal void UpdateStatus(AudioStatusSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(new MethodInvoker(() => UpdateStatus(snapshot)));
            return;
        }

        var status = snapshot.CurrentStatus;
        _statusSource.Text = status?.SourceName ?? "Starting audio capture...";
        _statusSourcePolicy.Text = snapshot.DebugForceMultichannelSourceEnabled
            ? "Debug force enabled: prefer an available 7.1/5.1 game-process source"
            : "Debug force disabled; normal capture settings apply";
        _statusMethod.Text = FormatCaptureMethod(status);
        _statusEstimator.Text = status?.EstimatorMode switch
        {
            AudioEstimatorMode.Multichannel => "Verified multichannel direction",
            AudioEstimatorMode.Stereo => "Stereo left/right (front/back remains ambiguous)",
            _ => "Not available yet"
        };
        _statusFormat.Text = status?.FormatDescription ?? "—";
        _statusRequestedLayout.Text = status?.RequestedLayout ?? "—";
        _statusObservedLayout.Text = status?.ObservedLayout ?? "—";
        _statusValidation.Text = (status?.MultichannelState, status?.IsMultichannelSourceForced) switch
        {
            (MultichannelCaptureState.Probing, true) => "Forced source active; checking whether multichannel direction is trustworthy",
            (MultichannelCaptureState.Verified, true) => "Forced source active; multichannel direction verified",
            (MultichannelCaptureState.Uninformative, true) => "Forced source active; using stereo fold-down because content is uninformative",
            (MultichannelCaptureState.Unavailable, true) => "Forced source unavailable; endpoint fallback active",
            (MultichannelCaptureState.NotAttempted, _) => "Stereo baseline; no multichannel validation active",
            (MultichannelCaptureState.Probing, _) => "Checking for independent side/rear content",
            (MultichannelCaptureState.Verified, _) => "Verified",
            (MultichannelCaptureState.Uninformative, _) => "Rejected as uninformative",
            (MultichannelCaptureState.Unavailable, _) => "Activation unavailable",
            _ => "Starting..."
        };
        _statusFallbackReason.Text = string.IsNullOrWhiteSpace(status?.FallbackReason)
            ? "—"
            : status.FallbackReason;
        _statusNextRetry.Text = snapshot.NextMultichannelRetryAt is null
            ? "—"
            : snapshot.NextMultichannelRetryAt.Value.ToLocalTime().ToString("HH:mm:ss");
        _channelLevelMeter.SetVisualizationEnabled(snapshot.DebugForceMultichannelSourceEnabled);
        if (_channelVisualizationCard is not null)
        {
            _channelVisualizationCard.Visible = snapshot.DebugForceMultichannelSourceEnabled;
        }

        _statusEventLog.Text = FormatEventLog(snapshot.Events);
        _statusEventLog.SelectionStart = 0;
        _statusEventLog.SelectionLength = 0;
    }

    internal void UpdateChannelVisualization(AudioChannelMeterFrame? frame)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(new MethodInvoker(() => UpdateChannelVisualization(frame)));
            return;
        }

        _channelLevelMeter.UpdateFrame(frame);
    }

    private Control BuildElementToggleGrid()
    {
        var toggles = new TableLayoutPanel
        {
            AutoSize = true,
            BackColor = DarkUiTheme.CardBackground,
            ColumnCount = 2,
            Dock = DockStyle.Top,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        toggles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        toggles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        var controls = new[]
        {
            _showRing,
            _showTicks,
            _showCurrentRays,
            _showCurrentMarkers,
            _showListenerDot,
            _showTrail,
            _showLabels
        };
        for (var index = 0; index < controls.Length; index++)
        {
            var row = index / 2;
            while (toggles.RowCount <= row)
            {
                toggles.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                toggles.RowCount++;
            }

            controls[index].Margin = new Padding(0, 5, 18, 5);
            toggles.Controls.Add(controls[index], index % 2, row);
        }

        return toggles;
    }

    private Control BuildFooter()
    {
        var footer = new Panel
        {
            BackColor = DarkUiTheme.CardBackground,
            Dock = DockStyle.Fill,
            Padding = new Padding(18, 13, 18, 12)
        };
        var previewNote = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Left,
            ForeColor = DarkUiTheme.SecondaryText,
            Padding = new Padding(2, 8, 0, 0),
            Text = "Overlay appearance is previewed live. Save keeps the changes."
        };
        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            BackColor = DarkUiTheme.CardBackground,
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = Padding.Empty,
            WrapContents = false
        };

        var save = DarkUiTheme.CreateButton("Save", primary: true, 104);
        var cancel = DarkUiTheme.CreateButton("Cancel", primary: false, 104);
        save.Click += (_, _) => SaveAndClose();
        cancel.Click += (_, _) => Close();
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        footer.Controls.Add(buttons);
        footer.Controls.Add(previewNote);

        AcceptButton = save;
        CancelButton = cancel;
        cancel.DialogResult = DialogResult.Cancel;
        return footer;
    }

    private void LoadSettings(AppSettings source)
    {
        var settings = source.Clone();
        settings.Normalize();

        IReadOnlyList<Services.AudioEndpointInfo> endpoints;
        try
        {
            endpoints = Services.AudioEndpointService.GetRenderEndpoints();
        }
        catch
        {
            endpoints = new[] { new Services.AudioEndpointInfo(null, "Default Windows output device") };
        }

        _audioDevice.Items.AddRange(endpoints.Cast<object>().ToArray());
        _audioDevice.SelectedItem = endpoints.FirstOrDefault(item => item.Id == settings.AudioDeviceId) ?? endpoints[0];

        foreach (var screen in Screen.AllScreens)
        {
            _monitor.Items.Add(new DisplayOption(screen.DeviceName, DisplayInfoFormatter.ToDisplayLabel(screen)));
        }

        _monitor.SelectedItem = _monitor.Items.Cast<DisplayOption>().FirstOrDefault(item =>
            string.Equals(item.DeviceName, settings.SelectedMonitorDeviceName, StringComparison.OrdinalIgnoreCase))
            ?? _monitor.Items.Cast<DisplayOption>().FirstOrDefault();

        _overlayEnabled.Checked = settings.OverlayEnabled;
        _useDetectedGameProcessAudio.Checked = settings.UseDetectedGameProcessAudio;
        _useBestAvailableMultichannelAudio.Checked = settings.UseBestAvailableMultichannelAudio;
        _debugForceMultichannelSource.Checked = settings.DebugForceMultichannelSource;
        _automaticallyFallbackToGameProcessAudio.Checked = settings.AutomaticallyFallbackToGameProcessAudio;
        _automaticAudioCalibration.Checked = settings.AutomaticAudioCalibration;
        _silenceThreshold.Value = (decimal)settings.SilenceRmsThreshold;
        _smoothing.Value = (decimal)settings.SmoothingFactor;
        _modelBalance.Value = (decimal)settings.ModelMaximumBalance;
        _loudSoundEmphasis.Checked = settings.LoudSoundEmphasisEnabled;
        _loudSoundThreshold.Value = (decimal)settings.LoudSoundThresholdMultiplier;
        _autoDetect.Checked = settings.AutoDetectSteamGameMonitor;
        _monitor.Enabled = !_autoDetect.Checked;
        _selectedColorHex = settings.OverlayColorHex;
        _colorPreview.BackColor = settings.GetOverlayColor();
        _opacitySlider.Value = settings.OverlayOpacityPercent;
        _scale.Value = settings.OverlayHeightPercent;
        _thickness.Value = settings.RingThickness;
        _markerSize.Value = settings.MarkerSize;
        _ambientMarkerSize.Value = settings.AmbientMarkerSizePercent;
        _ambientMarkerOpacity.Value = settings.AmbientMarkerOpacityPercent;
        _selectedAmbientMarkerColorHex = settings.AmbientMarkerColorHex;
        _ambientMarkerColorPreview.BackColor = settings.GetAmbientMarkerColor();
        _loudMarkerSize.Value = settings.LoudMarkerSizePercent;
        _loudMarkerOpacity.Value = settings.LoudMarkerOpacityPercent;
        _selectedLoudMarkerColorHex = settings.LoudMarkerColorHex;
        _loudMarkerColorPreview.BackColor = settings.GetLoudMarkerColor();
        _loudMarkerOutline.Checked = settings.LoudMarkerOutlineEnabled;
        _selectedLoudOutlineColorHex = settings.LoudMarkerOutlineColorHex;
        _loudOutlineColorPreview.BackColor = settings.GetLoudMarkerOutlineColor();
        _loudMarkerOutlineThickness.Value = (decimal)settings.LoudMarkerOutlineThickness;
        UpdateLoudSoundControls();
        _horizontalOffset.Value = settings.HorizontalOffset;
        _verticalOffset.Value = settings.VerticalOffset;
        _showRing.Checked = settings.ShowCompassRing;
        _showTicks.Checked = settings.ShowCardinalTicks;
        _showCurrentRays.Checked = settings.ShowCurrentDirectionRays;
        _showCurrentMarkers.Checked = settings.ShowCurrentDirectionMarkers;
        _showListenerDot.Checked = settings.ShowListenerDot;
        _showTrail.Checked = settings.ShowDirectionTrail;
        _trailDuration.Value = (decimal)settings.TrailDurationSeconds;
        _trailDuration.Enabled = _showTrail.Checked;
        _showLabels.Checked = settings.ShowCompassLabels;
        _toggleHotkey.Hotkey = settings.ToggleHotkey;
        _cycleHotkey.Hotkey = settings.CycleMonitorHotkey;
        _openSettingsHotkey.Hotkey = settings.OpenSettingsHotkey;
        UpdateAudioCalibrationControls();
    }

    private void ChooseColor()
    {
        using var dialog = new ColorDialog
        {
            Color = ColorTranslator.FromHtml(_selectedColorHex),
            FullOpen = true
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _selectedColorHex = ToColorHex(dialog.Color);
        _colorPreview.BackColor = dialog.Color;
        NotifyOverlayPreviewChanged();
    }

    private void ChooseLoudOutlineColor()
    {
        using var dialog = new ColorDialog
        {
            Color = ColorTranslator.FromHtml(_selectedLoudOutlineColorHex),
            FullOpen = true
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _selectedLoudOutlineColorHex = ToColorHex(dialog.Color);
        _loudOutlineColorPreview.BackColor = dialog.Color;
        NotifyOverlayPreviewChanged();
    }

    private void ChooseAmbientMarkerColor()
    {
        using var dialog = new ColorDialog
        {
            Color = ColorTranslator.FromHtml(_selectedAmbientMarkerColorHex),
            FullOpen = true
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _selectedAmbientMarkerColorHex = ToColorHex(dialog.Color);
        _ambientMarkerColorPreview.BackColor = dialog.Color;
        NotifyOverlayPreviewChanged();
    }

    private void ChooseLoudMarkerColor()
    {
        using var dialog = new ColorDialog
        {
            Color = ColorTranslator.FromHtml(_selectedLoudMarkerColorHex),
            FullOpen = true
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _selectedLoudMarkerColorHex = ToColorHex(dialog.Color);
        _loudMarkerColorPreview.BackColor = dialog.Color;
        NotifyOverlayPreviewChanged();
    }

    private void SaveAndClose()
    {
        ResultSettings = ReadSettingsFromControls();
        DialogResult = DialogResult.OK;
        Close();
    }

    private AppSettings ReadSettingsFromControls()
    {
        var selectedEndpoint = _audioDevice.SelectedItem as Services.AudioEndpointInfo;
        var selectedDisplay = _monitor.SelectedItem as DisplayOption;

        var settings = new AppSettings
        {
            OverlayEnabled = _overlayEnabled.Checked,
            AudioDeviceId = selectedEndpoint?.Id,
            UseDetectedGameProcessAudio = _useDetectedGameProcessAudio.Checked,
            UseBestAvailableMultichannelAudio = _useBestAvailableMultichannelAudio.Checked,
            DebugForceMultichannelSource = _debugForceMultichannelSource.Checked,
            AutomaticallyFallbackToGameProcessAudio = _automaticallyFallbackToGameProcessAudio.Checked,
            AutomaticAudioCalibration = _automaticAudioCalibration.Checked,
            SilenceRmsThreshold = (double)_silenceThreshold.Value,
            SmoothingFactor = (double)_smoothing.Value,
            ModelMaximumBalance = (double)_modelBalance.Value,
            LoudSoundEmphasisEnabled = _loudSoundEmphasis.Checked,
            LoudSoundThresholdMultiplier = (double)_loudSoundThreshold.Value,
            AutoDetectSteamGameMonitor = _autoDetect.Checked,
            SelectedMonitorDeviceName = selectedDisplay?.DeviceName,
            OverlayColorHex = _selectedColorHex,
            OverlayOpacityPercent = _opacitySlider.Value,
            OverlayHeightPercent = (int)_scale.Value,
            RingThickness = (int)_thickness.Value,
            MarkerSize = (int)_markerSize.Value,
            AmbientMarkerSizePercent = (int)_ambientMarkerSize.Value,
            AmbientMarkerOpacityPercent = _ambientMarkerOpacity.Value,
            AmbientMarkerColorHex = _selectedAmbientMarkerColorHex,
            LoudMarkerSizePercent = (int)_loudMarkerSize.Value,
            LoudMarkerOpacityPercent = _loudMarkerOpacity.Value,
            LoudMarkerColorHex = _selectedLoudMarkerColorHex,
            LoudMarkerOutlineEnabled = _loudMarkerOutline.Checked,
            LoudMarkerOutlineColorHex = _selectedLoudOutlineColorHex,
            LoudMarkerOutlineThickness = (double)_loudMarkerOutlineThickness.Value,
            HorizontalOffset = (int)_horizontalOffset.Value,
            VerticalOffset = (int)_verticalOffset.Value,
            ShowCompassRing = _showRing.Checked,
            ShowCardinalTicks = _showTicks.Checked,
            ShowCurrentDirectionRays = _showCurrentRays.Checked,
            ShowCurrentDirectionMarkers = _showCurrentMarkers.Checked,
            ShowListenerDot = _showListenerDot.Checked,
            ShowDirectionTrail = _showTrail.Checked,
            TrailDurationSeconds = (double)_trailDuration.Value,
            ShowCompassLabels = _showLabels.Checked,
            ToggleHotkey = _toggleHotkey.Hotkey,
            CycleMonitorHotkey = _cycleHotkey.Hotkey,
            OpenSettingsHotkey = _openSettingsHotkey.Hotkey
        };
        settings.Normalize();
        return settings;
    }

    private void NotifyOverlayPreviewChanged()
    {
        if (_isLoading)
        {
            return;
        }

        OverlayPreviewChanged?.Invoke(ReadSettingsFromControls());
    }

    private void UpdateAudioCalibrationControls()
    {
        var manualCalibration = !_automaticAudioCalibration.Checked;
        _silenceThreshold.Enabled = manualCalibration;
        _modelBalance.Enabled = manualCalibration;
    }

    private void UpdateLoudMarkerOutlineControls()
    {
        var enabled = _loudSoundEmphasis.Checked && _loudMarkerOutline.Checked;
        _loudOutlineColorButton.Enabled = enabled;
        _loudOutlineColorPreview.Enabled = enabled;
        _loudMarkerOutlineThickness.Enabled = enabled;
    }

    private void UpdateLoudSoundControls()
    {
        var enabled = _loudSoundEmphasis.Checked;
        _loudSoundThreshold.Enabled = enabled;
        _loudMarkerSize.Enabled = enabled;
        _loudMarkerOpacity.Enabled = enabled;
        _loudMarkerColorButton.Enabled = enabled;
        _loudMarkerColorPreview.Enabled = enabled;
        _loudMarkerOutline.Enabled = enabled;
        UpdateLoudMarkerOutlineControls();
    }

    private void FitToWorkingArea()
    {
        var workingArea = Screen.FromControl(this).WorkingArea;
        var targetWidth = Math.Min(980, Math.Max(MinimumSize.Width, workingArea.Width - 48));
        var targetHeight = Math.Min(920, Math.Max(MinimumSize.Height, workingArea.Height - 48));
        Size = new Size(targetWidth, targetHeight);
        CenterToScreen();
        UpdateWrappingWidths();
    }

    private void UpdateWrappingWidths()
    {
        var maximumLabelWidth = Math.Max(300, _tabsHost.ClientSize.Width - 150);
        foreach (var label in _wrappingLabels)
        {
            label.MaximumSize = new Size(maximumLabelWidth, 0);
        }

        foreach (var content in _contentStacks)
        {
            if (content.Parent is not TabPage page)
            {
                continue;
            }

            var contentWidth = Math.Max(
                320,
                page.ClientSize.Width - page.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 3);
            content.MinimumSize = new Size(contentWidth, 0);
            content.MaximumSize = new Size(contentWidth, 0);

            foreach (Control contentItem in content.Controls)
            {
                var itemWidth = Math.Max(280, contentWidth - content.Padding.Horizontal);
                contentItem.MinimumSize = new Size(itemWidth, 0);
                contentItem.MaximumSize = new Size(itemWidth, 0);
            }
        }
    }

    protected override void OnHandleCreated(EventArgs eventArgs)
    {
        base.OnHandleCreated(eventArgs);
        var darkModeEnabled = 1;
        const int useImmersiveDarkMode = 20;
        const int useImmersiveDarkModeBefore20H1 = 19;
        if (NativeMethods.DwmSetWindowAttribute(
                Handle,
                useImmersiveDarkMode,
                ref darkModeEnabled,
                sizeof(int)) != 0)
        {
            NativeMethods.DwmSetWindowAttribute(
                Handle,
                useImmersiveDarkModeBefore20H1,
                ref darkModeEnabled,
                sizeof(int));
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _windowIcon.Dispose();
        }

        base.Dispose(disposing);
    }

    private static TabPage CreateTab(string text) => new(text)
    {
        AutoScroll = true,
        BackColor = DarkUiTheme.WindowBackground,
        ForeColor = DarkUiTheme.PrimaryText,
        Padding = new Padding(16),
        UseVisualStyleBackColor = false
    };

    private TableLayoutPanel CreateContentStack()
    {
        var content = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = DarkUiTheme.WindowBackground,
            ColumnCount = 1,
            Dock = DockStyle.Top,
            Margin = Padding.Empty,
            Padding = new Padding(0, 2, 0, 8)
        };
        _contentStacks.Add(content);
        return content;
    }

    private TableLayoutPanel CreateCard(string title, string description)
    {
        var card = CreateTwoColumnTable(DarkUiTheme.CardBackground);
        card.Margin = new Padding(0, 0, 0, 12);
        card.Padding = new Padding(16, 14, 16, 16);

        var titleLabel = new Label
        {
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            ForeColor = DarkUiTheme.PrimaryText,
            Margin = Padding.Empty,
            Text = title
        };
        AddWideRow(card, titleLabel, new Padding(0, 0, 0, 2));

        var descriptionLabel = CreateWrappingLabel(description, DarkUiTheme.SecondaryText);
        AddWideRow(card, descriptionLabel, new Padding(0, 0, 0, 10));
        return card;
    }

    private Control CreateInfoCard(string title, string text)
    {
        var card = new TableLayoutPanel
        {
            AutoSize = true,
            BackColor = DarkUiTheme.RaisedBackground,
            ColumnCount = 2,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 12),
            Padding = new Padding(0)
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 4));
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var accent = new Panel
        {
            BackColor = DarkUiTheme.Accent,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty
        };
        var body = new TableLayoutPanel
        {
            AutoSize = true,
            BackColor = DarkUiTheme.RaisedBackground,
            ColumnCount = 1,
            Dock = DockStyle.Top,
            Margin = Padding.Empty,
            Padding = new Padding(14, 12, 14, 13)
        };
        body.Controls.Add(new Label
        {
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            ForeColor = DarkUiTheme.Accent,
            Margin = new Padding(0, 0, 0, 3),
            Text = title
        });
        body.Controls.Add(CreateWrappingLabel(text, DarkUiTheme.SecondaryText));
        card.Controls.Add(accent, 0, 0);
        card.Controls.Add(body, 1, 0);
        return card;
    }

    private static TableLayoutPanel CreateTwoColumnTable(Color background)
    {
        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = background,
            ColumnCount = 2,
            Dock = DockStyle.Top,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 39));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 61));
        return layout;
    }

    private static void AddContent(TableLayoutPanel layout, Control control)
    {
        var row = layout.RowCount++;
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(control, 0, row);
    }

    private static void AddRow(TableLayoutPanel layout, string labelText, Control control)
    {
        var row = layout.RowCount++;
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var label = new Label
        {
            Text = labelText,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            ForeColor = DarkUiTheme.PrimaryText,
            Margin = new Padding(0, 9, 14, 9)
        };
        control.Margin = new Padding(0, 5, 0, 5);
        layout.Controls.Add(label, 0, row);
        layout.Controls.Add(control, 1, row);
    }

    private static void AddWideRow(TableLayoutPanel layout, Control control) =>
        AddWideRow(layout, control, new Padding(0, 7, 0, 7));

    private static void AddWideRow(TableLayoutPanel layout, Control control, Padding margin)
    {
        var row = layout.RowCount++;
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        control.Margin = margin;
        layout.Controls.Add(control, 0, row);
        layout.SetColumnSpan(control, 2);
    }

    private Label CreateNote(string text)
    {
        var label = CreateWrappingLabel(text, DarkUiTheme.SecondaryText);
        label.Padding = new Padding(0, 2, 0, 0);
        return label;
    }

    private static Label CreateStatusValueLabel() => new()
    {
        Anchor = AnchorStyles.Left,
        AutoSize = true,
        ForeColor = DarkUiTheme.PrimaryText,
        MaximumSize = new Size(520, 0),
        Text = "—"
    };

    private static TextBox CreateStatusEventLog() => new()
    {
        AccessibleName = "Session event log",
        BackColor = DarkUiTheme.InputBackground,
        BorderStyle = BorderStyle.FixedSingle,
        Dock = DockStyle.Top,
        Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point),
        ForeColor = DarkUiTheme.PrimaryText,
        Height = 260,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        TabStop = true,
        WordWrap = true
    };

    private static string FormatEventLog(IReadOnlyList<CaptureSessionEvent> events)
    {
        if (events.Count == 0)
        {
            return "No capture events have been recorded yet.";
        }

        return string.Join(
            Environment.NewLine + Environment.NewLine,
            events.Reverse().Select(item =>
                $"{item.Timestamp.ToLocalTime():HH:mm:ss}  {item.Event}" + Environment.NewLine +
                $"          Reason: {item.Reason}"));
    }

    private static string FormatCaptureMethod(AudioCaptureStatus? status)
    {
        if (status is null)
        {
            return "Not available yet";
        }

        if (status.IsMultichannelSourceForced)
        {
            return status.IsProcessCapture
                ? "Debug-forced multichannel process loopback"
                : "WASAPI endpoint fallback (forced multichannel source unavailable)";
        }

        if (status.MultichannelState == MultichannelCaptureState.Probing && !status.IsProcessCapture)
        {
            return "WASAPI endpoint loopback + process-loopback validation";
        }

        return status.IsProcessCapture
            ? "Windows process loopback"
            : "WASAPI endpoint loopback";
    }

    private Label CreateWrappingLabel(string text, Color color)
    {
        var label = new Label
        {
            AutoSize = true,
            ForeColor = color,
            MaximumSize = new Size(760, 0),
            Text = text
        };
        _wrappingLabels.Add(label);
        return label;
    }

    private static GroupBox CreateSettingsGroup(string text) => new DarkGroupBox
    {
        Text = text,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Dock = DockStyle.Top,
        Font = new Font("Segoe UI", 9.25F, FontStyle.Bold, GraphicsUnit.Point),
        Margin = new Padding(0, 0, 0, 12),
        Padding = new Padding(16, 28, 16, 16)
    };

    private static FlowLayoutPanel CreateColorPanel(Panel preview, Button button)
    {
        var panel = new FlowLayoutPanel
        {
            AutoSize = true,
            BackColor = DarkUiTheme.CardBackground,
            Margin = Padding.Empty,
            WrapContents = false
        };
        preview.Margin = new Padding(0, 3, 10, 3);
        button.Margin = Padding.Empty;
        panel.Controls.Add(preview);
        panel.Controls.Add(button);
        return panel;
    }

    private void BindPercentageSlider(DarkSlider slider)
    {
        slider.ValueChanged += (_, _) =>
        {
            NotifyOverlayPreviewChanged();
        };
    }

    private static ComboBox CreateComboBox() => new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Dock = DockStyle.Fill,
        Height = 31
    };

    private static DarkSlider CreatePercentageSlider(int minimum) => new()
    {
        Dock = DockStyle.Fill,
        Minimum = minimum,
        Maximum = 100,
        SmallChange = 1,
        LargeChange = 10
    };

    private static Panel CreateColorPreview() => new()
    {
        BackColor = Color.White,
        BorderStyle = BorderStyle.FixedSingle,
        Height = 30,
        Width = 54
    };

    private static HotkeyTextBox CreateHotkeyTextBox() => new()
    {
        Dock = DockStyle.Fill,
        Height = 31
    };

    private static string ToColorHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    private static Icon LoadWindowIcon()
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

    private static NumericUpDown CreateIntegerNumeric(
        int minimum,
        int maximum,
        int increment = 1) => new()
        {
            Minimum = minimum,
            Maximum = maximum,
            Increment = increment,
            Dock = DockStyle.Left,
            Width = 132
        };

    private static NumericUpDown CreateDecimalNumeric(
        decimal minimum,
        decimal maximum,
        int decimalPlaces,
        decimal increment) => new()
        {
            Minimum = minimum,
            Maximum = maximum,
            DecimalPlaces = decimalPlaces,
            Increment = increment,
            Dock = DockStyle.Left,
            Width = 132
        };

    private sealed record DisplayOption(string DeviceName, string Label)
    {
        public override string ToString() => Label;
    }
}

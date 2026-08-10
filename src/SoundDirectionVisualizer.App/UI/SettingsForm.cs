namespace SoundDirectionVisualizer.App.UI;

public sealed class SettingsForm : Form
{
    private readonly CheckBox _overlayEnabled = new() { Text = "Enable overlay", AutoSize = true };
    private readonly ComboBox _audioDevice = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly CheckBox _useDetectedGameProcessAudio = new()
    {
        Text = "Capture only the detected Steam game's process audio (optional)",
        AutoSize = true
    };
    private readonly CheckBox _automaticallyFallbackToGameProcessAudio = new()
    {
        Text = "Automatically try game-process audio when a running game's output stays centered",
        AutoSize = true
    };
    private readonly CheckBox _automaticAudioCalibration = new()
    {
        Text = "Automatically adapt to output level and stereo width (recommended)",
        AutoSize = true
    };
    private readonly NumericUpDown _silenceThreshold = CreateDecimalNumeric(0.00001m, 0.1m, 5, 0.00010m);
    private readonly NumericUpDown _smoothing = CreateDecimalNumeric(0.01m, 1m, 2, 0.01m);
    private readonly NumericUpDown _modelBalance = CreateDecimalNumeric(0.05m, 1m, 2, 0.05m);
    private readonly CheckBox _loudSoundEmphasis = new() { Text = "Emphasize loud sounds separately", AutoSize = true };
    private readonly NumericUpDown _loudSoundThreshold = CreateDecimalNumeric(1.1m, 10m, 1, 0.1m);
    private readonly CheckBox _autoDetect = new() { Text = "Automatically target a running Steam game's display", AutoSize = true };
    private readonly ComboBox _monitor = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly NumericUpDown _scale = CreateIntegerNumeric(10, 200, 5);
    private readonly NumericUpDown _thickness = CreateIntegerNumeric(1, 12);
    private readonly NumericUpDown _markerSize = CreateIntegerNumeric(4, 32);
    private readonly NumericUpDown _ambientMarkerSize = CreateIntegerNumeric(25, 300, 5);
    private readonly TrackBar _ambientMarkerOpacity = CreatePercentageSlider(10);
    private readonly Label _ambientMarkerOpacityValue = new() { Text = "40%", AutoSize = true };
    private readonly NumericUpDown _loudMarkerSize = CreateIntegerNumeric(25, 300, 5);
    private readonly TrackBar _loudMarkerOpacity = CreatePercentageSlider(10);
    private readonly Label _loudMarkerOpacityValue = new() { Text = "100%", AutoSize = true };
    private readonly CheckBox _loudMarkerOutline = new() { Text = "Outline loud markers", AutoSize = true };
    private readonly NumericUpDown _loudMarkerOutlineThickness = CreateDecimalNumeric(0.1m, 8m, 1, 0.1m);
    private readonly TrackBar _opacitySlider = new()
    {
        Minimum = 0,
        Maximum = 100,
        TickFrequency = 10,
        SmallChange = 1,
        LargeChange = 10,
        AutoSize = false,
        Width = 230,
        Height = 34
    };
    private readonly Label _opacityValueLabel = new() { Text = "40%", AutoSize = true };
    private readonly NumericUpDown _horizontalOffset = CreateIntegerNumeric(-4000, 4000);
    private readonly NumericUpDown _verticalOffset = CreateIntegerNumeric(-4000, 4000);
    private readonly NumericUpDown _trailDuration = CreateDecimalNumeric(0.5m, 15m, 1, 0.5m);
    private readonly CheckBox _showRing = new() { Text = "Show compass ring", AutoSize = true };
    private readonly CheckBox _showTicks = new() { Text = "Show cardinal tick marks", AutoSize = true };
    private readonly CheckBox _showCurrentRays = new() { Text = "Show current direction rays", AutoSize = true };
    private readonly CheckBox _showCurrentMarkers = new() { Text = "Show current direction markers", AutoSize = true };
    private readonly CheckBox _showListenerDot = new() { Text = "Show center listener dot", AutoSize = true };
    private readonly CheckBox _showTrail = new() { Text = "Show fading direction trail", AutoSize = true };
    private readonly CheckBox _showLabels = new() { Text = "Show F / B / L / R labels", AutoSize = true };
    private readonly Button _colorButton = new() { Text = "Choose...", AutoSize = true };
    private readonly Panel _colorPreview = new() { Width = 48, Height = 22, BorderStyle = BorderStyle.FixedSingle };
    private readonly Button _ambientMarkerColorButton = new() { Text = "Choose...", AutoSize = true };
    private readonly Panel _ambientMarkerColorPreview = new() { Width = 48, Height = 22, BorderStyle = BorderStyle.FixedSingle };
    private readonly Button _loudMarkerColorButton = new() { Text = "Choose...", AutoSize = true };
    private readonly Panel _loudMarkerColorPreview = new() { Width = 48, Height = 22, BorderStyle = BorderStyle.FixedSingle };
    private readonly Button _loudOutlineColorButton = new() { Text = "Choose...", AutoSize = true };
    private readonly Panel _loudOutlineColorPreview = new() { Width = 48, Height = 22, BorderStyle = BorderStyle.FixedSingle };
    private readonly HotkeyTextBox _toggleHotkey = new() { Dock = DockStyle.Fill };
    private readonly HotkeyTextBox _cycleHotkey = new() { Dock = DockStyle.Fill };
    private readonly HotkeyTextBox _openSettingsHotkey = new() { Dock = DockStyle.Fill };
    private string _selectedColorHex = "#FFFFFF";
    private string _selectedAmbientMarkerColorHex = "#FFFFFF";
    private string _selectedLoudMarkerColorHex = "#FFFFFF";
    private string _selectedLoudOutlineColorHex = "#000000";
    private bool _isLoading = true;

    public SettingsForm(AppSettings settings)
    {
        Text = "Sound Direction Visualizer Settings";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(660, 600);
        ClientSize = new Size(700, 680);
        ShowInTaskbar = true;
        MaximizeBox = false;
        AutoScaleMode = AutoScaleMode.Dpi;

        ResultSettings = settings.Clone();
        BuildLayout();
        LoadSettings(settings);
        _isLoading = false;
    }

    public AppSettings ResultSettings { get; private set; }

    public event Action<AppSettings>? OverlayPreviewChanged;

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(10)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildAudioTab());
        tabs.TabPages.Add(BuildOverlayTab());
        tabs.TabPages.Add(BuildTargetingTab());
        tabs.TabPages.Add(BuildHotkeysTab());
        root.Controls.Add(tabs, 0, 0);
        root.Controls.Add(BuildButtons(), 0, 1);
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

        BindPercentageSlider(_ambientMarkerOpacity, _ambientMarkerOpacityValue);
        BindPercentageSlider(_loudMarkerOpacity, _loudMarkerOpacityValue);

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
            _opacityValueLabel.Text = $"{_opacitySlider.Value}%";
            NotifyOverlayPreviewChanged();
        };
    }

    private TabPage BuildAudioTab()
    {
        var page = CreateTab("Audio");
        var layout = CreateTwoColumnTable();
        AddWideRow(layout, _useDetectedGameProcessAudio);
        AddWideRow(layout, _automaticallyFallbackToGameProcessAudio);
        AddRow(layout, "Output device", _audioDevice);
        AddWideRow(layout, _automaticAudioCalibration);
        AddRow(layout, "Silence threshold (RMS)", _silenceThreshold);
        AddRow(layout, "Smoothing factor", _smoothing);
        AddRow(layout, "Manual hard-pan balance", _modelBalance);
        AddWideRow(layout, _loudSoundEmphasis);
        AddRow(layout, "Loud sound threshold (× ambience)", _loudSoundThreshold);
        AddWideRow(layout, CreateNote(
            "By default, audio is captured from the selected Windows output device. If the default device stays silent, the app occasionally " +
            "checks other active stereo output devices and temporarily follows one carrying audio. Optional game-process capture can preserve " +
            "stereo direction when a headset or spatial-audio driver exposes only dual mono at its physical output loopback. When the automatic fallback is enabled, " +
            "the app also tries game-process capture if audible output remains centered for eight seconds while a Steam game is running. Both modes require exactly two channels."));
        AddWideRow(layout, CreateNote(
            "Automatic calibration scales the silence gate, normalizes each source's usual stereo width toward a consistent lateral angle, and adds immediate headroom for wider transient sounds. " +
            "Disable it only when using the manual silence threshold and hard-pan balance values."));
        AddWideRow(layout, CreateNote(
            "Loud-sound detection compares the current combined level with the median recent ambience. " +
            "A larger multiplier marks fewer sounds as loud."));
        AddWideRow(layout, CreateNote(
            "Stereo identifies left/right balance, but cannot distinguish front from back. The overlay therefore shows both valid candidates."));
        page.Controls.Add(layout);
        return page;
    }

    private TabPage BuildOverlayTab()
    {
        var page = CreateTab("Overlay");
        var layout = CreateTwoColumnTable();
        AddWideRow(layout, _overlayEnabled);

        var colorPanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        colorPanel.Controls.Add(_colorPreview);
        colorPanel.Controls.Add(_colorButton);
        AddRow(layout, "Compass / ray / label color", colorPanel);

        var opacityPanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        opacityPanel.Controls.Add(_opacitySlider);
        opacityPanel.Controls.Add(_opacityValueLabel);
        AddRow(layout, "Opacity", opacityPanel);
        AddRow(layout, "Size (% of display height)", _scale);
        AddRow(layout, "Line thickness (px)", _thickness);
        AddRow(layout, "Base marker size (px)", _markerSize);
        AddRow(layout, "Horizontal offset (px)", _horizontalOffset);
        AddRow(layout, "Vertical offset (px)", _verticalOffset);
        AddWideRow(layout, CreateNote(
            "The default size is 110% of the target display height. " +
            "Size changes the ring, lines, markers, and labels together. " +
            "Offsets are relative to the target display center; positive Y moves the visualizer down. Changes are previewed live."));
        AddWideRow(layout, BuildAmbientMarkerGroup());
        AddWideRow(layout, BuildLoudMarkerGroup());
        AddWideRow(layout, CreateNote(
            "Size is relative to the base marker size. The same type-specific appearance is used by current and trail markers; " +
            "trail age still shrinks and fades both types. Marker opacity is relative to the overlay's global opacity."));
        AddWideRow(layout, new Label
        {
            Text = "Visible elements",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold)
        });
        AddWideRow(layout, _showRing);
        AddWideRow(layout, _showTicks);
        AddWideRow(layout, _showCurrentRays);
        AddWideRow(layout, _showCurrentMarkers);
        AddWideRow(layout, _showListenerDot);
        AddWideRow(layout, _showTrail);
        AddRow(layout, "Trail duration (seconds)", _trailDuration);
        AddWideRow(layout, _showLabels);
        page.Controls.Add(layout);
        return page;
    }

    private TabPage BuildTargetingTab()
    {
        var page = CreateTab("Target display");
        var layout = CreateTwoColumnTable();
        AddWideRow(layout, _autoDetect);
        AddRow(layout, "Manual display", _monitor);
        AddWideRow(layout, CreateNote(
            "Auto targeting first checks the foreground window, verifies that its executable is inside a Steam library, " +
            "and uses that window's display. A recently detected valid game window is retained; otherwise the selected display remains in use."));
        AddWideRow(layout, CreateNote(
            "The overlay is intended for borderless-windowed games. Exclusive fullscreen and some anti-cheat/protected overlays can block it."));
        page.Controls.Add(layout);
        return page;
    }

    private GroupBox BuildAmbientMarkerGroup()
    {
        var group = CreateSettingsGroup("Ambient markers");
        var layout = CreateTwoColumnTable();
        AddRow(layout, "Size (% of base)", _ambientMarkerSize);
        AddRow(
            layout,
            "Opacity",
            CreatePercentageSliderPanel(_ambientMarkerOpacity, _ambientMarkerOpacityValue));
        AddRow(
            layout,
            "Fill color",
            CreateColorPanel(_ambientMarkerColorPreview, _ambientMarkerColorButton));
        group.Controls.Add(layout);
        return group;
    }

    private GroupBox BuildLoudMarkerGroup()
    {
        var group = CreateSettingsGroup("Loud markers");
        var layout = CreateTwoColumnTable();
        AddRow(layout, "Size (% of base)", _loudMarkerSize);
        AddRow(
            layout,
            "Opacity",
            CreatePercentageSliderPanel(_loudMarkerOpacity, _loudMarkerOpacityValue));
        AddRow(
            layout,
            "Fill color",
            CreateColorPanel(_loudMarkerColorPreview, _loudMarkerColorButton));
        AddWideRow(layout, _loudMarkerOutline);
        AddRow(
            layout,
            "Outline color",
            CreateColorPanel(_loudOutlineColorPreview, _loudOutlineColorButton));
        AddRow(layout, "Outline thickness (px)", _loudMarkerOutlineThickness);
        group.Controls.Add(layout);
        return group;
    }

    private TabPage BuildHotkeysTab()
    {
        var page = CreateTab("Hotkeys");
        var layout = CreateTwoColumnTable();
        AddRow(layout, "Toggle overlay", _toggleHotkey);
        AddRow(layout, "Cycle displays", _cycleHotkey);
        AddRow(layout, "Open settings", _openSettingsHotkey);
        AddWideRow(layout, CreateNote("Focus a field and press a key combination. Press Delete to clear an optional binding."));
        page.Controls.Add(layout);
        return page;
    }

    private FlowLayoutPanel BuildButtons()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 10, 0, 0)
        };
        var save = new Button { Text = "Save", DialogResult = DialogResult.None, AutoSize = true };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        save.Click += (_, _) => SaveAndClose();
        panel.Controls.Add(save);
        panel.Controls.Add(cancel);
        AcceptButton = save;
        CancelButton = cancel;
        return panel;
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
        _opacityValueLabel.Text = $"{settings.OverlayOpacityPercent}%";
        _scale.Value = settings.OverlayHeightPercent;
        _thickness.Value = settings.RingThickness;
        _markerSize.Value = settings.MarkerSize;
        _ambientMarkerSize.Value = settings.AmbientMarkerSizePercent;
        _ambientMarkerOpacity.Value = settings.AmbientMarkerOpacityPercent;
        _ambientMarkerOpacityValue.Text = $"{settings.AmbientMarkerOpacityPercent}%";
        _selectedAmbientMarkerColorHex = settings.AmbientMarkerColorHex;
        _ambientMarkerColorPreview.BackColor = settings.GetAmbientMarkerColor();
        _loudMarkerSize.Value = settings.LoudMarkerSizePercent;
        _loudMarkerOpacity.Value = settings.LoudMarkerOpacityPercent;
        _loudMarkerOpacityValue.Text = $"{settings.LoudMarkerOpacityPercent}%";
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

        _selectedColorHex = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
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

        _selectedLoudOutlineColorHex = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
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
        _loudMarkerOpacityValue.Enabled = enabled;
        _loudMarkerColorButton.Enabled = enabled;
        _loudMarkerColorPreview.Enabled = enabled;
        _loudMarkerOutline.Enabled = enabled;
        UpdateLoudMarkerOutlineControls();
    }

    private static TabPage CreateTab(string text) => new(text)
    {
        Padding = new Padding(14),
        AutoScroll = true
    };

    private static TableLayoutPanel CreateTwoColumnTable()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(4)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        return layout;
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
            Margin = new Padding(3, 9, 12, 9)
        };
        control.Margin = new Padding(3, 5, 3, 5);
        layout.Controls.Add(label, 0, row);
        layout.Controls.Add(control, 1, row);
    }

    private static void AddWideRow(TableLayoutPanel layout, Control control)
    {
        var row = layout.RowCount++;
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        control.Margin = new Padding(3, 8, 3, 8);
        layout.Controls.Add(control, 0, row);
        layout.SetColumnSpan(control, 2);
    }

    private static Label CreateNote(string text) => new()
    {
        Text = text,
        AutoSize = true,
        MaximumSize = new Size(590, 0),
        ForeColor = SystemColors.GrayText
    };

    private static GroupBox CreateSettingsGroup(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Dock = DockStyle.Top,
        Padding = new Padding(8)
    };

    private static FlowLayoutPanel CreateColorPanel(Panel preview, Button button)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        panel.Controls.Add(preview);
        panel.Controls.Add(button);
        return panel;
    }

    private static FlowLayoutPanel CreatePercentageSliderPanel(TrackBar slider, Label valueLabel)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        valueLabel.Margin = new Padding(3, 8, 3, 3);
        panel.Controls.Add(slider);
        panel.Controls.Add(valueLabel);
        return panel;
    }

    private void BindPercentageSlider(TrackBar slider, Label valueLabel)
    {
        slider.ValueChanged += (_, _) =>
        {
            valueLabel.Text = $"{slider.Value}%";
            NotifyOverlayPreviewChanged();
        };
    }

    private static TrackBar CreatePercentageSlider(int minimum) => new()
    {
        Minimum = minimum,
        Maximum = 100,
        TickFrequency = 10,
        SmallChange = 1,
        LargeChange = 10,
        AutoSize = false,
        Width = 230,
        Height = 34
    };

    private static string ToColorHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    private static NumericUpDown CreateIntegerNumeric(
        int minimum,
        int maximum,
        int increment = 1) => new()
        {
            Minimum = minimum,
            Maximum = maximum,
            Increment = increment,
            Dock = DockStyle.Left,
            Width = 120
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
            Width = 120
        };

    private sealed record DisplayOption(string DeviceName, string Label)
    {
        public override string ToString() => Label;
    }
}

using SoundDirectionVisualizer.App.Services;

namespace SoundDirectionVisualizer.App.UI;

internal sealed class ChannelLevelMeter : Control
{
    private const int HeaderHeight = 32;
    private const int RowHeight = 30;
    private const int MinimumRows = 2;
    private const int HorizontalPadding = 10;
    private const int LabelWidth = 164;
    private const int ValueWidth = 76;
    private AudioChannelMeterFrame? _frame;
    private bool _visualizationEnabled;

    public ChannelLevelMeter()
    {
        AccessibleName = "Live monitored audio channels";
        AccessibleDescription = "Waiting for live channel levels.";
        BackColor = DarkUiTheme.InputBackground;
        DoubleBuffered = true;
        Dock = DockStyle.Top;
        ForeColor = DarkUiTheme.PrimaryText;
        Height = CalculateHeight(null);
        Margin = Padding.Empty;
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    internal bool VisualizationEnabled => _visualizationEnabled;

    internal int DisplayedChannelCount => _frame?.Channels.Count ?? 0;

    internal AudioChannelMeterFrame? CurrentFrame => _frame;

    internal void SetVisualizationEnabled(bool enabled)
    {
        if (_visualizationEnabled == enabled)
        {
            return;
        }

        _visualizationEnabled = enabled;
        if (!enabled)
        {
            _frame = null;
            AccessibleDescription = "Live channel visualization is disabled.";
        }
        else
        {
            AccessibleDescription = "Waiting for live channel levels.";
        }

        Height = CalculateHeight(_frame);
        Invalidate();
    }

    internal void UpdateFrame(AudioChannelMeterFrame? frame)
    {
        if (!_visualizationEnabled || ReferenceEquals(_frame, frame))
        {
            return;
        }

        _frame = frame;
        Height = CalculateHeight(frame);
        AccessibleDescription = frame is null
            ? "Waiting for live channel levels."
            : BuildAccessibleDescription(frame);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);
        var graphics = eventArgs.Graphics;
        graphics.Clear(BackColor);

        if (_frame is null)
        {
            TextRenderer.DrawText(
                graphics,
                _visualizationEnabled
                    ? "Waiting for live audio from the active capture source..."
                    : "Enable the debug force option to show live channel levels.",
                Font,
                new Rectangle(HorizontalPadding, 0, Math.Max(0, ClientSize.Width - (2 * HorizontalPadding)), ClientSize.Height),
                DarkUiTheme.SecondaryText,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            return;
        }

        TextRenderer.DrawText(
            graphics,
            $"{_frame.SourceName}  ·  {_frame.LayoutName}",
            Font,
            new Rectangle(HorizontalPadding, 0, Math.Max(0, ClientSize.Width - (2 * HorizontalPadding)), HeaderHeight),
            DarkUiTheme.SecondaryText,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        using var trackBrush = new SolidBrush(DarkUiTheme.RaisedBackground);
        using var levelBrush = new SolidBrush(DarkUiTheme.Accent);
        using var clippingBrush = new SolidBrush(DarkUiTheme.AccentPressed);
        using var borderPen = new Pen(DarkUiTheme.Border);

        for (var index = 0; index < _frame.Channels.Count; index++)
        {
            var channel = _frame.Channels[index];
            var rowTop = HeaderHeight + (index * RowHeight);
            TextRenderer.DrawText(
                graphics,
                $"{channel.ShortLabel}  {channel.DisplayName}",
                Font,
                new Rectangle(HorizontalPadding, rowTop, LabelWidth - HorizontalPadding, RowHeight),
                DarkUiTheme.PrimaryText,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            var trackLeft = LabelWidth;
            var trackWidth = Math.Max(0, ClientSize.Width - trackLeft - ValueWidth - HorizontalPadding);
            var trackBounds = new Rectangle(trackLeft, rowTop + 7, trackWidth, 16);
            if (trackBounds.Width > 0)
            {
                graphics.FillRectangle(trackBrush, trackBounds);
                graphics.DrawRectangle(borderPen, trackBounds);
                var normalizedWidth = AudioChannelMeterScale.ToNormalizedWidth(channel.RmsLevel);
                var fillWidth = (int)Math.Round(Math.Max(0, trackBounds.Width - 1) * normalizedWidth);
                if (fillWidth > 0)
                {
                    var fillBounds = new Rectangle(trackBounds.X + 1, trackBounds.Y + 1, fillWidth, Math.Max(1, trackBounds.Height - 1));
                    graphics.FillRectangle(normalizedWidth >= 0.98 ? clippingBrush : levelBrush, fillBounds);
                }
            }

            var decibels = AudioChannelMeterScale.ToDecibels(channel.RmsLevel);
            var valueText = double.IsNegativeInfinity(decibels)
                ? "−∞ dBFS"
                : $"{Math.Max(AudioChannelMeterScale.MinimumDecibels, decibels),5:0.0} dBFS";
            TextRenderer.DrawText(
                graphics,
                valueText,
                Font,
                new Rectangle(ClientSize.Width - ValueWidth, rowTop, ValueWidth - HorizontalPadding, RowHeight),
                DarkUiTheme.SecondaryText,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        }
    }

    private static int CalculateHeight(AudioChannelMeterFrame? frame) =>
        HeaderHeight + (Math.Max(frame?.Channels.Count ?? 0, MinimumRows) * RowHeight) + 6;

    private static string BuildAccessibleDescription(AudioChannelMeterFrame frame) =>
        $"{frame.SourceName}, {frame.LayoutName}. " +
        string.Join(
            ", ",
            frame.Channels.Select(channel =>
            {
                var decibels = AudioChannelMeterScale.ToDecibels(channel.RmsLevel);
                var value = double.IsNegativeInfinity(decibels)
                    ? "silent"
                    : $"{Math.Max(AudioChannelMeterScale.MinimumDecibels, decibels):0.0} dBFS";
                return $"{channel.ShortLabel} {channel.DisplayName}: {value}";
            }));
}

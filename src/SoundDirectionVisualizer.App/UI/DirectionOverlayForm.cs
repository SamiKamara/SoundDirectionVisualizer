using SoundDirectionVisualizer.App.Native;
using SoundDirectionVisualizer.Core.Audio;
using SoundDirectionVisualizer.Core.Direction;
using SoundDirectionVisualizer.Core.Visualization;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace SoundDirectionVisualizer.App.UI;

public sealed class DirectionOverlayForm : Form
{
    private readonly DirectionTrail _trail = new();
    private AppSettings _settings = new();
    private Color _overlayBaseColor = Color.FromArgb(70, 230, 255);
    private Color _ambientMarkerBaseColor = Color.FromArgb(70, 230, 255);
    private Color _loudMarkerBaseColor = Color.FromArgb(70, 230, 255);
    private Color _loudMarkerOutlineBaseColor = Color.Black;
    private Screen _targetScreen = Screen.PrimaryScreen ?? Screen.AllScreens.First();
    private DirectionFrame? _currentFrame;
    private DateTimeOffset _lastTrailTimestamp = DateTimeOffset.MinValue;

    public DirectionOverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        DoubleBuffered = true;
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;

        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint,
            true);

        UpdateOverlayBounds();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int wsExLayered = 0x00080000;
            const int wsExTransparent = 0x00000020;
            const int wsExToolWindow = 0x00000080;
            const int wsExNoActivate = 0x08000000;

            var parameters = base.CreateParams;
            parameters.ExStyle |= wsExLayered | wsExTransparent | wsExToolWindow | wsExNoActivate;
            return parameters;
        }
    }

    public void ApplySettings(AppSettings settings)
    {
        _settings = settings.Clone();
        _settings.Normalize();
        _overlayBaseColor = _settings.GetOverlayColor();
        _ambientMarkerBaseColor = _settings.GetAmbientMarkerColor();
        _loudMarkerBaseColor = _settings.GetLoudMarkerColor();
        _loudMarkerOutlineBaseColor = _settings.GetLoudMarkerOutlineColor();
        Opacity = _settings.OverlayOpacityPercent / 100d;
        _trail.Clear();
        _lastTrailTimestamp = DateTimeOffset.MinValue;
        UpdateOverlayBounds();
        Invalidate();
    }

    public void SetTargetScreen(Screen screen)
    {
        _targetScreen = screen;
        UpdateOverlayBounds();
        Invalidate();
    }

    public void UpdateFrame(DirectionFrame? frame, DateTimeOffset now)
    {
        _currentFrame = frame;

        if (frame is not null
            && frame.Timestamp != _lastTrailTimestamp
            && _settings.ShowDirectionTrail)
        {
            _trail.Add(frame.Estimate, frame.Timestamp, frame.Loudness);
            _lastTrailTimestamp = frame.Timestamp;
        }

        _trail.Prune(now, TimeSpan.FromSeconds(_settings.TrailDurationSeconds));
        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs eventArgs)
    {
        eventArgs.Graphics.Clear(TransparencyKey);
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);

        var graphics = eventArgs.Graphics;
        // TransparencyKey cannot preserve per-pixel alpha. Opaque, non-antialiased
        // drawing keeps the selected color from blending with the magenta key.
        graphics.SmoothingMode = SmoothingMode.None;
        graphics.PixelOffsetMode = PixelOffsetMode.None;
        graphics.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;

        var center = new PointF(ClientSize.Width / 2f, ClientSize.Height / 2f);
        var metrics = GetMetrics();
        var radius = metrics.Radius;
        var ringRectangle = new RectangleF(
            center.X - radius,
            center.Y - radius,
            radius * 2,
            radius * 2);

        if (_settings.ShowCompassRing || _settings.ShowCardinalTicks)
        {
            using var ringPen = new Pen(OverlayColor(), metrics.LineThickness);

            if (_settings.ShowCompassRing)
            {
                graphics.DrawEllipse(ringPen, ringRectangle);
            }

            if (_settings.ShowCardinalTicks)
            {
                DrawCardinalTicks(graphics, ringPen, center, metrics);
            }
        }

        var current = _currentFrame is { Estimate.IsQuiet: false } activeFrame
            ? activeFrame
            : null;

        if (_settings.ShowDirectionTrail)
        {
            DrawTrailLayer(
                graphics,
                center,
                metrics,
                DateTimeOffset.UtcNow,
                SoundLoudness.Ambient);
        }

        if (current is not null && _settings.ShowCurrentDirectionRays)
        {
            DrawCurrentDirectionRays(graphics, center, metrics, current.Estimate.CandidateAzimuths);
        }

        if (current is not null
            && _settings.ShowCurrentDirectionMarkers
            && GetEffectiveLoudness(current.Loudness) == SoundLoudness.Ambient)
        {
            DrawCurrentDirectionMarkers(
                graphics,
                center,
                metrics,
                current.Estimate.CandidateAzimuths,
                current.Loudness);
        }

        // Loud markers are a separate top layer so neither a newer ambient trail point
        // nor an ambient current marker can obscure a loud marker at the same position.
        if (_settings.ShowDirectionTrail)
        {
            DrawTrailLayer(
                graphics,
                center,
                metrics,
                DateTimeOffset.UtcNow,
                SoundLoudness.Loud);
        }

        if (current is not null
            && _settings.ShowCurrentDirectionMarkers
            && GetEffectiveLoudness(current.Loudness) == SoundLoudness.Loud)
        {
            DrawCurrentDirectionMarkers(
                graphics,
                center,
                metrics,
                current.Estimate.CandidateAzimuths,
                current.Loudness);
        }

        if (_settings.ShowListenerDot)
        {
            using var listenerBrush = new SolidBrush(OverlayColor());
            var listenerSize = metrics.ListenerSize;
            graphics.FillEllipse(
                listenerBrush,
                center.X - listenerSize / 2,
                center.Y - listenerSize / 2,
                listenerSize,
                listenerSize);
        }

        if (_settings.ShowCompassLabels)
        {
            DrawLabels(graphics, center, metrics);
        }
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == NativeMethods.WmMouseActivate)
        {
            message.Result = (IntPtr)NativeMethods.MaNoActivate;
            return;
        }

        base.WndProc(ref message);
    }

    private void DrawTrailLayer(
        Graphics graphics,
        PointF center,
        OverlayMetrics metrics,
        DateTimeOffset now,
        SoundLoudness layer)
    {
        var maximumAge = Math.Max(0.1, _settings.TrailDurationSeconds);

        foreach (var point in _trail.Points)
        {
            if (GetEffectiveLoudness(point.Loudness) != layer)
            {
                continue;
            }

            var age = (now - point.Timestamp).TotalSeconds;
            var freshness = Math.Clamp(1 - age / maximumAge, 0, 1);
            if (freshness < 0.04)
            {
                continue;
            }

            var position = ToPoint(center, metrics.Radius, point.Azimuth);
            var visual = GetMarkerVisual(metrics, freshness, point.Loudness);
            DrawDirectionMarker(graphics, position, metrics, visual);
        }
    }

    private void DrawCurrentDirectionRays(
        Graphics graphics,
        PointF center,
        OverlayMetrics metrics,
        IReadOnlyList<double> candidateAzimuths)
    {
        using var rayPen = new Pen(OverlayColor(), metrics.LineThickness)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        foreach (var azimuth in candidateAzimuths)
        {
            var position = ToPoint(center, metrics.Radius, azimuth);
            graphics.DrawLine(rayPen, center, position);
        }
    }

    private void DrawCurrentDirectionMarkers(
        Graphics graphics,
        PointF center,
        OverlayMetrics metrics,
        IReadOnlyList<double> candidateAzimuths,
        SoundLoudness loudness)
    {
        var visual = GetMarkerVisual(metrics, freshness: 1, loudness);

        foreach (var azimuth in candidateAzimuths)
        {
            var position = ToPoint(center, metrics.Radius, azimuth);
            DrawDirectionMarker(graphics, position, metrics, visual);
        }
    }

    private DirectionMarkerVisual GetMarkerVisual(
        OverlayMetrics metrics,
        double freshness,
        SoundLoudness loudness) => DirectionMarkerVisualCalculator.Calculate(
            metrics.MarkerSize,
            freshness,
            GetEffectiveLoudness(loudness),
            _settings.AmbientMarkerSizePercent,
            _settings.AmbientMarkerOpacityPercent,
            _settings.LoudMarkerSizePercent,
            _settings.LoudMarkerOpacityPercent);

    private SoundLoudness GetEffectiveLoudness(SoundLoudness loudness) =>
        _settings.LoudSoundEmphasisEnabled
            ? loudness
            : SoundLoudness.Ambient;

    private void DrawDirectionMarker(
        Graphics graphics,
        PointF position,
        OverlayMetrics metrics,
        DirectionMarkerVisual visual)
    {
        var markerBaseColor = visual.IsEmphasized
            ? _loudMarkerBaseColor
            : _ambientMarkerBaseColor;
        var fillColor = FadedColor(markerBaseColor, visual.Intensity);
        using var markerBrush = new SolidBrush(fillColor);
        graphics.FillEllipse(
            markerBrush,
            position.X - visual.Size / 2,
            position.Y - visual.Size / 2,
            visual.Size,
            visual.Size);

        if (!visual.IsEmphasized || !_settings.LoudMarkerOutlineEnabled)
        {
            return;
        }

        var displayScale = metrics.MarkerSize / Math.Max(1, _settings.MarkerSize);
        var outlineThickness = Math.Clamp(
            _settings.LoudMarkerOutlineThickness * displayScale,
            0.1,
            Math.Max(0.1, visual.Size / 3));
        using var outlinePen = new Pen(_loudMarkerOutlineBaseColor, (float)outlineThickness);
        graphics.DrawEllipse(
            outlinePen,
            position.X - visual.Size / 2,
            position.Y - visual.Size / 2,
            visual.Size,
            visual.Size);
    }

    private void DrawLabels(Graphics graphics, PointF center, OverlayMetrics metrics)
    {
        using var font = new Font(Font.FontFamily, metrics.LabelFontSize, FontStyle.Bold, GraphicsUnit.Point);
        using var brush = new SolidBrush(OverlayColor());
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        var distance = metrics.LabelDistance;
        graphics.DrawString("F", font, brush, center.X, center.Y - distance, format);
        graphics.DrawString("B", font, brush, center.X, center.Y + distance, format);
        graphics.DrawString("L", font, brush, center.X - distance, center.Y, format);
        graphics.DrawString("R", font, brush, center.X + distance, center.Y, format);
    }

    private static void DrawCardinalTicks(
        Graphics graphics,
        Pen pen,
        PointF center,
        OverlayMetrics metrics)
    {
        var radius = metrics.Radius;
        var tickLength = metrics.TickLength;
        graphics.DrawLine(pen, center.X, center.Y - radius, center.X, center.Y - radius + tickLength);
        graphics.DrawLine(pen, center.X, center.Y + radius, center.X, center.Y + radius - tickLength);
        graphics.DrawLine(pen, center.X - radius, center.Y, center.X - radius + tickLength, center.Y);
        graphics.DrawLine(pen, center.X + radius, center.Y, center.X + radius - tickLength, center.Y);
    }

    private static PointF ToPoint(PointF center, float radius, double azimuth)
    {
        var radians = azimuth * Math.PI / 180;
        return new PointF(
            center.X + (float)(radius * Math.Sin(radians)),
            center.Y - (float)(radius * Math.Cos(radians)));
    }

    private Color OverlayColor() => Color.FromArgb(
        255,
        _overlayBaseColor.R,
        _overlayBaseColor.G,
        _overlayBaseColor.B);

    private static Color FadedColor(Color baseColor, double intensity) => Color.FromArgb(
        255,
        (int)Math.Round(baseColor.R * Math.Clamp(intensity, 0, 1)),
        (int)Math.Round(baseColor.G * Math.Clamp(intensity, 0, 1)),
        (int)Math.Round(baseColor.B * Math.Clamp(intensity, 0, 1)));

    private OverlayMetrics GetMetrics() => OverlayMetrics.FitToDisplayHeight(
        _settings.RingThickness,
        _settings.MarkerSize,
        _targetScreen.Bounds.Height,
        _settings.OverlayHeightPercent);

    private void UpdateOverlayBounds()
    {
        var metrics = GetMetrics();
        var visualRadius = (int)Math.Ceiling(metrics.Radius) + metrics.Padding;
        var size = visualRadius * 2 + 1;
        var screenBounds = _targetScreen.Bounds;
        var centerX = screenBounds.Left + screenBounds.Width / 2 + _settings.HorizontalOffset;
        var centerY = screenBounds.Top + screenBounds.Height / 2 + _settings.VerticalOffset;
        var nextBounds = new Rectangle(centerX - visualRadius, centerY - visualRadius, size, size);

        if (Bounds != nextBounds)
        {
            Bounds = nextBounds;
        }
    }
}

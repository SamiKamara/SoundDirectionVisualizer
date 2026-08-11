using System.Drawing.Drawing2D;

namespace SoundDirectionVisualizer.App.UI;

internal sealed class DarkCheckBox : CheckBox
{
    private const int GlyphSize = 14;
    private const int TextGap = 7;
    private bool _hovered;

    public DarkCheckBox()
    {
        AutoSize = true;
        BackColor = Color.Transparent;
        ForeColor = DarkUiTheme.PrimaryText;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.SupportsTransparentBackColor
            | ControlStyles.UserPaint,
            true);
    }

    internal Rectangle GlyphBounds => new(
        Padding.Left,
        Padding.Top + Math.Max(0, (ClientSize.Height - Padding.Vertical - GlyphSize) / 2),
        GlyphSize,
        GlyphSize);

    public override Size GetPreferredSize(Size proposedSize)
    {
        var textSize = TextRenderer.MeasureText(
            Text,
            Font,
            Size.Empty,
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        return new Size(
            Padding.Horizontal + GlyphSize + TextGap + textSize.Width,
            Padding.Vertical + Math.Max(GlyphSize, textSize.Height));
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaintBackground(eventArgs);

        var glyph = GlyphBounds;
        var fillColor = Checked
            ? Enabled ? DarkUiTheme.Accent : DarkUiTheme.Border
            : DarkUiTheme.InputBackground;
        var borderColor = Enabled && (_hovered || Focused)
            ? DarkUiTheme.AccentHover
            : DarkUiTheme.Border;

        using (var fill = new SolidBrush(fillColor))
        using (var border = new Pen(borderColor))
        {
            eventArgs.Graphics.FillRectangle(fill, glyph);
            eventArgs.Graphics.DrawRectangle(
                border,
                glyph.X,
                glyph.Y,
                glyph.Width - 1,
                glyph.Height - 1);
        }

        if (CheckState == CheckState.Checked)
        {
            var previousSmoothingMode = eventArgs.Graphics.SmoothingMode;
            eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var checkmark = new Pen(DarkUiTheme.WindowBackground, 2.2F)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            eventArgs.Graphics.DrawLines(
                checkmark,
                [
                    new PointF(glyph.Left + 3.2F, glyph.Top + 7.2F),
                    new PointF(glyph.Left + 6F, glyph.Top + 10F),
                    new PointF(glyph.Left + 11F, glyph.Top + 4.2F)
                ]);
            eventArgs.Graphics.SmoothingMode = previousSmoothingMode;
        }
        else if (CheckState == CheckState.Indeterminate)
        {
            using var indicator = new SolidBrush(DarkUiTheme.WindowBackground);
            eventArgs.Graphics.FillRectangle(
                indicator,
                glyph.Left + 3,
                glyph.Top + 6,
                glyph.Width - 6,
                3);
        }

        var textBounds = new Rectangle(
            glyph.Right + TextGap,
            0,
            Math.Max(0, ClientSize.Width - glyph.Right - TextGap),
            ClientSize.Height);
        var textColor = Enabled ? ForeColor : DarkUiTheme.SecondaryText;
        TextRenderer.DrawText(
            eventArgs.Graphics,
            Text,
            Font,
            textBounds,
            textColor,
            Color.Transparent,
            TextFormatFlags.Left
            | TextFormatFlags.NoPadding
            | TextFormatFlags.NoPrefix
            | TextFormatFlags.SingleLine
            | TextFormatFlags.VerticalCenter);

        if (Focused && ShowFocusCues)
        {
            ControlPaint.DrawFocusRectangle(eventArgs.Graphics, textBounds, textColor, Color.Transparent);
        }
    }

    protected override void OnCheckStateChanged(EventArgs eventArgs)
    {
        base.OnCheckStateChanged(eventArgs);
        Invalidate();
    }

    protected override void OnEnabledChanged(EventArgs eventArgs)
    {
        base.OnEnabledChanged(eventArgs);
        Invalidate();
    }

    protected override void OnFontChanged(EventArgs eventArgs)
    {
        base.OnFontChanged(eventArgs);
        if (AutoSize)
        {
            Size = GetPreferredSize(Size.Empty);
        }

        Invalidate();
    }

    protected override void OnGotFocus(EventArgs eventArgs)
    {
        base.OnGotFocus(eventArgs);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs eventArgs)
    {
        base.OnLostFocus(eventArgs);
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs eventArgs)
    {
        base.OnMouseEnter(eventArgs);
        _hovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs eventArgs)
    {
        base.OnMouseLeave(eventArgs);
        _hovered = false;
        Invalidate();
    }
}

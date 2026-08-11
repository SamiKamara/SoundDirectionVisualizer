namespace SoundDirectionVisualizer.App.UI;

internal sealed class DarkTabControl : TabControl
{
    public DarkTabControl()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint,
            true);
        ItemSize = new Size(150, 42);
        Padding = new Point(18, 6);
        SizeMode = TabSizeMode.Fixed;
    }

    protected override void OnPaintBackground(PaintEventArgs eventArgs)
    {
        eventArgs.Graphics.Clear(DarkUiTheme.WindowBackground);
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        eventArgs.Graphics.Clear(DarkUiTheme.WindowBackground);

        using var borderPen = new Pen(DarkUiTheme.Border);
        var displayRectangle = DisplayRectangle;
        if (displayRectangle.Width > 0 && displayRectangle.Height > 0)
        {
            var borderRectangle = Rectangle.Inflate(displayRectangle, 1, 1);
            borderRectangle.Width -= 1;
            borderRectangle.Height -= 1;
            eventArgs.Graphics.DrawRectangle(borderPen, borderRectangle);
        }

        for (var index = 0; index < TabCount; index++)
        {
            var bounds = GetTabRect(index);
            var selected = index == SelectedIndex;
            using var background = new SolidBrush(selected
                ? DarkUiTheme.CardBackground
                : DarkUiTheme.WindowBackground);
            eventArgs.Graphics.FillRectangle(background, bounds);

            var textColor = selected ? DarkUiTheme.PrimaryText : DarkUiTheme.SecondaryText;
            TextRenderer.DrawText(
                eventArgs.Graphics,
                TabPages[index].Text,
                Font,
                bounds,
                textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            if (selected)
            {
                using var accent = new SolidBrush(DarkUiTheme.Accent);
                eventArgs.Graphics.FillRectangle(accent, bounds.Left + 10, bounds.Bottom - 3, bounds.Width - 20, 3);
            }
        }
    }

    protected override void OnSelectedIndexChanged(EventArgs eventArgs)
    {
        base.OnSelectedIndexChanged(eventArgs);
        Invalidate();
    }
}

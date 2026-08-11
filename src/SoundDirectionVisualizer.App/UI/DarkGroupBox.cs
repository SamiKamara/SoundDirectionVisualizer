namespace SoundDirectionVisualizer.App.UI;

internal sealed class DarkGroupBox : GroupBox
{
    public DarkGroupBox()
    {
        BackColor = DarkUiTheme.CardBackground;
        ForeColor = DarkUiTheme.PrimaryText;
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        eventArgs.Graphics.Clear(BackColor);
        var textSize = TextRenderer.MeasureText(Text, Font);
        var borderY = Math.Max(8, textSize.Height / 2);
        using var borderPen = new Pen(DarkUiTheme.Border);
        using var textBrush = new SolidBrush(ForeColor);
        using var backgroundBrush = new SolidBrush(BackColor);
        var borderBounds = new Rectangle(0, borderY, Width - 1, Height - borderY - 1);
        eventArgs.Graphics.DrawRectangle(borderPen, borderBounds);

        var textBounds = new Rectangle(13, 0, textSize.Width + 8, textSize.Height);
        eventArgs.Graphics.FillRectangle(backgroundBrush, textBounds);
        eventArgs.Graphics.DrawString(Text, Font, textBrush, 17, 0);
    }
}

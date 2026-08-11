using System.ComponentModel;

namespace SoundDirectionVisualizer.App.UI;

public sealed class HotkeyTextBox : Control
{
    private const int BorderThickness = 1;
    private const int HorizontalTextPadding = 6;
    private HotkeyDefinition _hotkey = HotkeyDefinition.Empty();

    public HotkeyTextBox()
    {
        AccessibleRole = AccessibleRole.Text;
        BackColor = DarkUiTheme.InputBackground;
        Cursor = Cursors.IBeam;
        ForeColor = DarkUiTheme.PrimaryText;
        Size = new Size(200, 31);
        TabStop = true;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.Selectable
            | ControlStyles.UserPaint,
            true);
        Text = _hotkey.ToDisplayString();
    }

    internal Rectangle TextBounds
    {
        get
        {
            var textSize = TextRenderer.MeasureText(
                Text,
                Font,
                Size.Empty,
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            var contentTop = BorderThickness;
            var contentHeight = Math.Max(0, ClientSize.Height - (BorderThickness * 2));
            var textTop = contentTop + Math.Max(0, (contentHeight - textSize.Height) / 2);
            return new Rectangle(
                BorderThickness + HorizontalTextPadding,
                textTop,
                Math.Max(0, ClientSize.Width - (2 * (BorderThickness + HorizontalTextPadding))),
                Math.Min(textSize.Height, contentHeight));
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public HotkeyDefinition Hotkey
    {
        get => _hotkey.Clone();
        set
        {
            _hotkey = value?.Clone() ?? HotkeyDefinition.Empty();
            Text = _hotkey.ToDisplayString();
        }
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        eventArgs.Graphics.Clear(BackColor);
        using (var border = new Pen(Focused ? DarkUiTheme.Accent : DarkUiTheme.Border))
        {
            eventArgs.Graphics.DrawRectangle(
                border,
                0,
                0,
                Math.Max(0, ClientSize.Width - 1),
                Math.Max(0, ClientSize.Height - 1));
        }

        TextRenderer.DrawText(
            eventArgs.Graphics,
            Text,
            Font,
            TextBounds,
            Enabled ? ForeColor : DarkUiTheme.SecondaryText,
            BackColor,
            TextFormatFlags.EndEllipsis
            | TextFormatFlags.HorizontalCenter
            | TextFormatFlags.NoPadding
            | TextFormatFlags.NoPrefix
            | TextFormatFlags.SingleLine
            | TextFormatFlags.VerticalCenter);
    }

    protected override void OnTextChanged(EventArgs eventArgs)
    {
        base.OnTextChanged(eventArgs);
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

    protected override void OnMouseDown(MouseEventArgs eventArgs)
    {
        base.OnMouseDown(eventArgs);
        Focus();
    }

    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        if (keyData is Keys.Tab or (Keys.Shift | Keys.Tab))
        {
            return base.ProcessCmdKey(ref message, keyData);
        }

        var key = keyData & Keys.KeyCode;
        if (key is Keys.Back or Keys.Delete)
        {
            Hotkey = HotkeyDefinition.Empty();
            return true;
        }

        if (HotkeyDefinition.IsModifierKey(key))
        {
            return true;
        }

        var captured = HotkeyDefinition.FromKeyData(keyData);
        if (captured.IsValid)
        {
            Hotkey = captured;
        }

        return true;
    }
}

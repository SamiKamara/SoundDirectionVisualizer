namespace SoundDirectionVisualizer.App.UI;

internal static class DarkUiTheme
{
    internal static readonly Color WindowBackground = Color.FromArgb(13, 17, 29);
    internal static readonly Color CardBackground = Color.FromArgb(24, 29, 40);
    internal static readonly Color RaisedBackground = Color.FromArgb(31, 37, 50);
    internal static readonly Color InputBackground = Color.FromArgb(18, 23, 34);
    internal static readonly Color Border = Color.FromArgb(52, 61, 78);
    internal static readonly Color PrimaryText = Color.FromArgb(240, 244, 250);
    internal static readonly Color SecondaryText = Color.FromArgb(157, 169, 188);
    internal static readonly Color Accent = Color.FromArgb(62, 213, 240);
    internal static readonly Color AccentHover = Color.FromArgb(91, 224, 247);
    internal static readonly Color AccentPressed = Color.FromArgb(42, 172, 199);
    internal static readonly Color Selection = Color.FromArgb(35, 78, 94);

    internal static Button CreateButton(string text, bool primary, int width)
    {
        var button = new Button
        {
            AutoSize = false,
            BackColor = primary ? Accent : RaisedBackground,
            FlatStyle = FlatStyle.Flat,
            ForeColor = primary ? WindowBackground : PrimaryText,
            Height = 38,
            Margin = new Padding(8, 0, 0, 0),
            Text = text,
            UseVisualStyleBackColor = false,
            Width = width
        };
        button.FlatAppearance.BorderColor = primary ? Accent : Border;
        button.FlatAppearance.MouseDownBackColor = primary ? AccentPressed : Color.FromArgb(38, 46, 61);
        button.FlatAppearance.MouseOverBackColor = primary ? AccentHover : Color.FromArgb(42, 50, 66);
        return button;
    }

    internal static DarkCheckBox CreateCheckBox(string text) => new()
    {
        Text = text
    };

    internal static void ApplyTo(Control root)
    {
        foreach (Control control in root.Controls)
        {
            switch (control)
            {
                case TabPage page:
                    page.BackColor = WindowBackground;
                    page.ForeColor = PrimaryText;
                    break;
                case HotkeyTextBox hotkeyTextBox:
                    hotkeyTextBox.BackColor = InputBackground;
                    hotkeyTextBox.ForeColor = PrimaryText;
                    break;
                case TextBox textBox:
                    textBox.BackColor = InputBackground;
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                    textBox.ForeColor = PrimaryText;
                    break;
                case ComboBox comboBox:
                    comboBox.BackColor = InputBackground;
                    comboBox.FlatStyle = FlatStyle.Flat;
                    comboBox.ForeColor = PrimaryText;
                    break;
                case NumericUpDown numeric:
                    numeric.BackColor = InputBackground;
                    numeric.BorderStyle = BorderStyle.FixedSingle;
                    numeric.ForeColor = PrimaryText;
                    break;
                case DarkCheckBox checkBox:
                    checkBox.BackColor = Color.Transparent;
                    checkBox.ForeColor = PrimaryText;
                    break;
            }

            ApplyTo(control);
        }
    }
}

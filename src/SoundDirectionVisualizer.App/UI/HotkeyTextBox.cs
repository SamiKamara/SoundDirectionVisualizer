using System.ComponentModel;

namespace SoundDirectionVisualizer.App.UI;

public sealed class HotkeyTextBox : TextBox
{
    private HotkeyDefinition _hotkey = HotkeyDefinition.Empty();

    public HotkeyTextBox()
    {
        ReadOnly = true;
        ShortcutsEnabled = false;
        Text = _hotkey.ToDisplayString();
        BackColor = SystemColors.Window;
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

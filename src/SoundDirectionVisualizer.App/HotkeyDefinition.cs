namespace SoundDirectionVisualizer.App;

[Flags]
public enum KeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8
}

public sealed class HotkeyDefinition
{
    public Keys Key { get; set; }

    public KeyModifiers Modifiers { get; set; }

    public bool IsEmpty => Key == Keys.None;

    public bool IsValid => !IsEmpty && !IsModifierKey(Key);

    public HotkeyDefinition Clone() => new() { Key = Key, Modifiers = Modifiers };

    public string ToDisplayString(string emptyValue = "Not set")
    {
        if (IsEmpty)
        {
            return emptyValue;
        }

        var parts = new List<string>();
        if (Modifiers.HasFlag(KeyModifiers.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(KeyModifiers.Windows)) parts.Add("Win");
        parts.Add(Key.ToString());
        return string.Join("+", parts);
    }

    public static HotkeyDefinition FromKeyData(Keys keyData) => new()
    {
        Key = keyData & Keys.KeyCode,
        Modifiers = GetModifiers(keyData)
    };

    public static HotkeyDefinition DefaultToggle() => new()
    {
        Key = Keys.D,
        Modifiers = KeyModifiers.Alt
    };

    public static HotkeyDefinition DefaultCycle() => new()
    {
        Key = Keys.F10,
        Modifiers = KeyModifiers.Control | KeyModifiers.Alt
    };

    public static HotkeyDefinition DefaultOpenSettings() => new()
    {
        Key = Keys.D,
        Modifiers = KeyModifiers.Control | KeyModifiers.Alt
    };

    public static HotkeyDefinition Empty() => new();

    public static bool IsModifierKey(Keys key) =>
        key is Keys.ControlKey or Keys.Menu or Keys.ShiftKey or Keys.LWin or Keys.RWin;

    private static KeyModifiers GetModifiers(Keys keyData)
    {
        var result = KeyModifiers.None;
        var modifiers = keyData & Keys.Modifiers;
        if (modifiers.HasFlag(Keys.Control)) result |= KeyModifiers.Control;
        if (modifiers.HasFlag(Keys.Alt)) result |= KeyModifiers.Alt;
        if (modifiers.HasFlag(Keys.Shift)) result |= KeyModifiers.Shift;
        return result;
    }
}

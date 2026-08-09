using SoundDirectionVisualizer.App.Native;

namespace SoundDirectionVisualizer.App.Services;

public enum HotkeyAction
{
    ToggleOverlay,
    CycleMonitor,
    OpenSettings
}

public sealed class GlobalHotkeyManager : IDisposable
{
    private readonly HotkeyMessageWindow _messageWindow = new();
    private readonly Dictionary<int, HotkeyAction> _registrations = new();
    private int _nextHotkeyId = 1;

    public GlobalHotkeyManager()
    {
        _messageWindow.HotkeyPressed += HandleHotkeyPressed;
    }

    public event EventHandler<HotkeyAction>? HotkeyPressed;

    public void ReplaceBindings(
        IReadOnlyDictionary<HotkeyAction, HotkeyDefinition> bindings,
        out List<HotkeyAction> failures)
    {
        failures = new List<HotkeyAction>();
        ClearBindings();

        foreach (var binding in bindings)
        {
            if (binding.Value.IsEmpty)
            {
                continue;
            }

            if (!binding.Value.IsValid || !TryRegister(binding.Key, binding.Value))
            {
                failures.Add(binding.Key);
            }
        }
    }

    public void ClearBindings()
    {
        foreach (var id in _registrations.Keys)
        {
            _ = NativeMethods.UnregisterHotKey(_messageWindow.Handle, id);
        }

        _registrations.Clear();
        _nextHotkeyId = 1;
    }

    public void Dispose()
    {
        ClearBindings();
        _messageWindow.HotkeyPressed -= HandleHotkeyPressed;
        _messageWindow.Dispose();
    }

    private bool TryRegister(HotkeyAction action, HotkeyDefinition definition)
    {
        var id = _nextHotkeyId++;
        var modifiers = ToNativeModifiers(definition.Modifiers);
        var key = (uint)definition.Key;
        var registered = NativeMethods.RegisterHotKey(
                _messageWindow.Handle,
                id,
                modifiers | NativeMethods.ModNoRepeat,
                key)
            || NativeMethods.RegisterHotKey(_messageWindow.Handle, id, modifiers, key);

        if (!registered)
        {
            return false;
        }

        _registrations[id] = action;
        return true;
    }

    private void HandleHotkeyPressed(object? sender, int id)
    {
        if (_registrations.TryGetValue(id, out var action))
        {
            HotkeyPressed?.Invoke(this, action);
        }
    }

    private static uint ToNativeModifiers(KeyModifiers modifiers)
    {
        var result = 0u;
        if (modifiers.HasFlag(KeyModifiers.Alt)) result |= NativeMethods.ModAlt;
        if (modifiers.HasFlag(KeyModifiers.Control)) result |= NativeMethods.ModControl;
        if (modifiers.HasFlag(KeyModifiers.Shift)) result |= NativeMethods.ModShift;
        if (modifiers.HasFlag(KeyModifiers.Windows)) result |= NativeMethods.ModWin;
        return result;
    }

    private sealed class HotkeyMessageWindow : NativeWindow, IDisposable
    {
        public HotkeyMessageWindow() => CreateHandle(new CreateParams());

        public event EventHandler<int>? HotkeyPressed;

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == NativeMethods.WmHotkey)
            {
                HotkeyPressed?.Invoke(this, message.WParam.ToInt32());
                return;
            }

            base.WndProc(ref message);
        }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
            {
                DestroyHandle();
            }
        }
    }
}

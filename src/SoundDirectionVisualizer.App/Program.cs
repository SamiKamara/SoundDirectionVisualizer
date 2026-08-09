namespace SoundDirectionVisualizer.App;

internal static class Program
{
    private const string SingleInstanceMutexName = @"Local\SoundDirectionVisualizer.SingleInstance";
    private const string OpenSettingsEventName = @"Local\SoundDirectionVisualizer.OpenSettings";

    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            SignalOpenSettings();
            return;
        }

        ApplicationConfiguration.Initialize();
        using var openSettingsEvent = new EventWaitHandle(
            false,
            EventResetMode.AutoReset,
            OpenSettingsEventName);
        Application.Run(new SoundDirectionVisualizerApplicationContext(
            openSettingsEvent,
            openSettingsOnStartup: true));
    }

    private static void SignalOpenSettings()
    {
        try
        {
            using var openSettingsEvent = EventWaitHandle.OpenExisting(OpenSettingsEventName);
            openSettingsEvent.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
        }
    }
}

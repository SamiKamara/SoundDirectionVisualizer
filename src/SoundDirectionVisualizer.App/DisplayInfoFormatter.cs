using System.Drawing;

namespace SoundDirectionVisualizer.App;

public static class DisplayInfoFormatter
{
    public static Screen ResolveScreen(string? deviceName)
    {
        var screens = Screen.AllScreens;
        var resolved = screens.FirstOrDefault(screen =>
            string.Equals(screen.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));
        return resolved ?? Screen.PrimaryScreen ?? screens.First();
    }

    public static string ToDisplayLabel(Screen screen)
    {
        var screens = Screen.AllScreens;
        var index = Array.FindIndex(screens, candidate =>
            string.Equals(candidate.DeviceName, screen.DeviceName, StringComparison.OrdinalIgnoreCase));
        var label = $"Display {index + 1} ({screen.Bounds.Width}x{screen.Bounds.Height})";

        if (screen.Primary)
        {
            label += " Primary";
        }

        if (screen.Bounds.Location != Point.Empty)
        {
            label += $" [{screen.Bounds.X},{screen.Bounds.Y}]";
        }

        return label;
    }
}

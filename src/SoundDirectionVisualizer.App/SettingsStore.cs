using System.Text.Json;
using System.Text.Json.Serialization;

namespace SoundDirectionVisualizer.App;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _settingsPath;

    public SettingsStore(string? settingsPath = null)
    {
        _settingsPath = settingsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SoundDirectionVisualizer",
            "settings.json");
    }

    public string SettingsPath => _settingsPath;

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return CreateDefaults();
            }

            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsPath), JsonOptions)
                ?? CreateDefaults();
            settings.Normalize();
            return settings;
        }
        catch
        {
            return CreateDefaults();
        }
    }

    public void Save(AppSettings settings)
    {
        settings.Normalize();
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    private static AppSettings CreateDefaults()
    {
        var settings = new AppSettings();
        settings.Normalize();
        return settings;
    }
}

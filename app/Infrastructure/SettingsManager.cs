using System.Text.Json;
using TransVoice.Live.Common;

namespace TransVoice.Live.Infrastructure;

public class SettingsManager
{
    private readonly string _settingsPath;

    public SettingsManager()
    {
        _settingsPath = Path.Combine(PathResolver.GetRootDirectory(), "settings.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(_settingsPath))
            return new AppSettings();

        try
        {
            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        settings.IsConfigured = true;
        var json = JsonSerializer.Serialize(
            settings,
            new JsonSerializerOptions { WriteIndented = true }
        );
        File.WriteAllText(_settingsPath, json);
    }

    public bool Exists => File.Exists(_settingsPath);
}

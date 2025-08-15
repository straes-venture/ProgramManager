using FileHunter;
using System.IO;
using System.Text.Json;

public class StateManager
{
    private readonly string _stateFilePath = "state.json";
    private readonly string _settingsFilePath = "settings.json";

    public AppState LoadState()
    {
        if (!File.Exists(_stateFilePath))
            return new AppState();

        string jsonState = File.ReadAllText(_stateFilePath);
        var state = JsonSerializer.Deserialize<AppState>(jsonState);
        return state ?? new AppState();
    }

    public AppSettings LoadSettings()
    {
        if (!File.Exists(_settingsFilePath))
            return new AppSettings();

        string jsonSettings = File.ReadAllText(_settingsFilePath);
        var settings = JsonSerializer.Deserialize<AppSettings>(jsonSettings);
        return settings ?? new AppSettings();
    }

    public void SaveState(AppState state)
    {
        string jsonState = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_stateFilePath, jsonState);
    }

    public void SaveSettings(AppSettings settings)
    {
        string jsonSettings = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_settingsFilePath, jsonSettings);
    }
}

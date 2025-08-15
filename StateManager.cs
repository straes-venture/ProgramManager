using FileHunter;
using System;
using System.IO;
using System.Text.Json;

public class StateManager
{
    private readonly string _stateFilePath;
    private readonly string _settingsFilePath;

    public StateManager(string? stateFilePath = null, string? settingsFilePath = null)
    {
        _stateFilePath = stateFilePath ?? "state.json";
        _settingsFilePath = settingsFilePath ?? "settings.json";
    }

    public AppState LoadState()
    {
        try
        {
            if (!File.Exists(_stateFilePath))
                return new AppState();

            string jsonState = File.ReadAllText(_stateFilePath);
            var state = JsonSerializer.Deserialize<AppState>(jsonState);
            return state ?? new AppState();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load state from '{_stateFilePath}'.", ex);
        }
    }

    public AppSettings LoadSettings()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
                return new AppSettings();

            string jsonSettings = File.ReadAllText(_settingsFilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(jsonSettings);
            return settings ?? new AppSettings();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load settings from '{_settingsFilePath}'.", ex);
        }
    }

    public void SaveState(AppState state)
    {
        try
        {
            string jsonState = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_stateFilePath, jsonState);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save state to '{_stateFilePath}'.", ex);
        }
    }

    public void SaveSettings(AppSettings settings)
    {
        try
        {
            string jsonSettings = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFilePath, jsonSettings);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save settings to '{_settingsFilePath}'.", ex);
        }
    }
}

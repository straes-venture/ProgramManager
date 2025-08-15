using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProgramManager;

/// <summary>
/// Centralized persistence for state and settings.
/// - Stores files under a unified base directory (default: %APPDATA%\GLACTPM).
/// - You can override the base directory and/or explicit file paths.
/// - Provides generic helpers so you can use your own AppState/AppSettings types.
/// </summary>
public sealed class StateManager
{
    private readonly string _baseDir;
    private readonly string _stateFilePath;
    private readonly string _settingsFilePath;

    // Consistent JSON options across reads/writes.
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Preferred constructor.
    /// - baseDir: directory where both state.json and settings.json will live.
    /// - stateFilePath/settingsFilePath: explicit overrides (optional).
    /// </summary>
    public StateManager(string? baseDir = null, string? stateFilePath = null, string? settingsFilePath = null)
    {
        _baseDir = baseDir ?? GetDefaultBaseDir();
        Directory.CreateDirectory(_baseDir);

        _stateFilePath = string.IsNullOrWhiteSpace(stateFilePath)
            ? Path.Combine(_baseDir, "state.json")
            : stateFilePath!;

        _settingsFilePath = string.IsNullOrWhiteSpace(settingsFilePath)
            ? Path.Combine(_baseDir, "settings.json")
            : settingsFilePath!;
    }

    /// <summary>
    /// Legacy-compat: allow passing only a state-file path.
    /// </summary>
    public StateManager(string stateFilePath)
        : this(baseDir: null, stateFilePath: stateFilePath, settingsFilePath: null) { }

    private static string GetDefaultBaseDir()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "GLACTPM");
    }

    // ----------------------------
    //  State load/save (generic)
    // ----------------------------

    public TState LoadState<TState>(Func<TState> makeDefault)
    {
        if (!File.Exists(_stateFilePath)) return makeDefault();
        var json = File.ReadAllText(_stateFilePath);
        var result = JsonSerializer.Deserialize<TState>(json, JsonOpts);
        return result is not null ? result : makeDefault();
    }

    public void SaveState<TState>(TState state)
    {
        var json = JsonSerializer.Serialize(state, JsonOpts);
        File.WriteAllText(_stateFilePath, json);
    }

    // -------------------------------
    //  Settings load/save (generic)
    // -------------------------------

    public void SaveSettings<TSettings>(TSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOpts);
        File.WriteAllText(_settingsFilePath, json);
    }

    public TSettings? LoadSettings<TSettings>()
    {
        if (!File.Exists(_settingsFilePath)) return default;
        var json = File.ReadAllText(_settingsFilePath);
        return JsonSerializer.Deserialize<TSettings>(json, JsonOpts);
    }

    public TSettings LoadSettingsOr<TSettings>(TSettings fallback)
    {
        var loaded = LoadSettings<TSettings>();
        return loaded is null ? fallback : loaded;
    }

    // -------------------------------
    //  Paths (useful for diagnostics)
    // -------------------------------
    public string BaseDirectory => _baseDir;
    public string StatePath => _stateFilePath;
    public string SettingsPath => _settingsFilePath;
}

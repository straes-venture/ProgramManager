git checkout -b fix/persist-settings-and-state-manager
git apply -p0 <<'PATCH'
*** BEGIN PATCH
diff --git a/StateManager.cs b/StateManager.cs
index 0000000..0000000 100644
--- a/StateManager.cs
+++ b/StateManager.cs
@@ -1,0 +1,229 @@
+using System;
+using System.IO;
+using System.Text.Json;
+using System.Text.Json.Serialization;
+
+namespace ProgramManager;
+
+/// <summary>
+/// Centralized persistence for state and settings.
+/// - By default stores files in %APPDATA%\GLACTPM\ (Windows) or the
+///   platform's ApplicationData folder on other OSes.
+/// - Can be pointed at a specific base directory, or explicit file paths.
+/// - Adds generic Settings helpers (Option 4) without breaking existing APIs.
+/// </summary>
+public sealed class StateManager
+{
+    private readonly string _baseDir;
+    private readonly string _stateFilePath;
+    private readonly string _settingsFilePath;
+
+    // Keep JSON options consistent across reads/writes.
+    private static readonly JsonSerializerOptions JsonOpts = new()
+    {
+        WriteIndented = true,
+        AllowTrailingCommas = true,
+        ReadCommentHandling = JsonCommentHandling.Skip,
+        Converters = { new JsonStringEnumConverter() }
+    };
+
+    /// <summary>
+    /// New flexible constructor:
+    /// - baseDir: directory where both state.json and settings.json will live.
+    /// - stateFilePath/settingsFilePath: explicit overrides (optional).
+    /// Compatibility: previous usages like new StateManager(stateFilePath: "…") still compile.
+    /// </summary>
+    public StateManager(string? baseDir = null, string? stateFilePath = null, string? settingsFilePath = null)
+    {
+        _baseDir = baseDir ?? GetDefaultBaseDir();
+        Directory.CreateDirectory(_baseDir);
+
+        _stateFilePath = string.IsNullOrWhiteSpace(stateFilePath)
+            ? Path.Combine(_baseDir, "state.json")
+            : stateFilePath!;
+
+        _settingsFilePath = string.IsNullOrWhiteSpace(settingsFilePath)
+            ? Path.Combine(_baseDir, "settings.json")
+            : settingsFilePath!;
+    }
+
+    /// <summary>
+    /// Legacy compatibility constructor: allow passing only a state-file path.
+    /// </summary>
+    public StateManager(string stateFilePath)
+        : this(baseDir: null, stateFilePath: stateFilePath, settingsFilePath: null) { }
+
+    private static string GetDefaultBaseDir()
+    {
+        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
+        // Canonical folder for both files:
+        return Path.Combine(appData, "GLACTPM");
+    }
+
+    // ----------------------------
+    //  State load/save (existing)
+    // ----------------------------
+    // NOTE: We don’t assume your AppState type here; these methods are
+    // intentionally kept generic-friendly. If you already have concrete
+    // LoadState/SaveState methods elsewhere, you can forward to these.
+
+    public TState LoadState<TState>(Func<TState> makeDefault)
+    {
+        if (!File.Exists(_stateFilePath)) return makeDefault();
+        var json = File.ReadAllText(_stateFilePath);
+        var result = JsonSerializer.Deserialize<TState>(json, JsonOpts);
+        return result is not null ? result : makeDefault();
+    }
+
+    public void SaveState<TState>(TState state)
+    {
+        var json = JsonSerializer.Serialize(state, JsonOpts);
+        File.WriteAllText(_stateFilePath, json);
+    }
+
+    // -------------------------------
+    //  Settings load/save (Option 4)
+    // -------------------------------
+
+    /// <summary>
+    /// Save any settings object to settings.json.
+    /// </summary>
+    public void SaveSettings<TSettings>(TSettings settings)
+    {
+        var json = JsonSerializer.Serialize(settings, JsonOpts);
+        File.WriteAllText(_settingsFilePath, json);
+    }
+
+    /// <summary>
+    /// Load settings.json into a strongly-typed object.
+    /// If the file is missing or invalid, returns default(T) or provided fallback.
+    /// </summary>
+    public TSettings? LoadSettings<TSettings>()
+    {
+        if (!File.Exists(_settingsFilePath)) return default;
+        var json = File.ReadAllText(_settingsFilePath);
+        return JsonSerializer.Deserialize<TSettings>(json, JsonOpts);
+    }
+
+    public TSettings LoadSettingsOr<TSettings>(TSettings fallback)
+    {
+        var loaded = LoadSettings<TSettings>();
+        return loaded is null ? fallback : loaded;
+    }
+
+    // -------------------------------
+    //  Paths (useful for diagnostics)
+    // -------------------------------
+    public string BaseDirectory => _baseDir;
+    public string StatePath => _stateFilePath;
+    public string SettingsPath => _settingsFilePath;
+}
diff --git a/SettingsForm.cs b/SettingsForm.cs
index 0000000..0000000 100644
--- a/SettingsForm.cs
+++ b/SettingsForm.cs
@@ -1,0 +1,86 @@
+using System;
+using System.Windows.Forms;
+
+namespace ProgramManager;
+
+public partial class SettingsForm : Form
+{
+    // Expose the chosen directories so MainForm can read & persist them.
+    public string? SelectedRootDirectory { get; private set; }
+    public string? SelectedArchiveDirectory { get; private set; }
+    public string? SelectedJsonDirectory { get; private set; }
+    public string? SelectedDecommissionDirectory { get; private set; }
+
+    // Wire this to your OK/Save button (e.g., btnOk_Click)
+    private void btnOk_Click(object? sender, EventArgs e)
+    {
+        // These TextBox names should match your form designer.
+        // If your controls use different names, adjust the properties here.
+        if (Controls.Find("txtRoot", true) is { Length: > 0 } rootBoxes && rootBoxes[0] is TextBox txtRoot)
+            SelectedRootDirectory = txtRoot.Text?.Trim();
+
+        if (Controls.Find("txtArchive", true) is { Length: > 0 } archBoxes && archBoxes[0] is TextBox txtArchive)
+            SelectedArchiveDirectory = txtArchive.Text?.Trim();
+
+        if (Controls.Find("txtJson", true) is { Length: > 0 } jsonBoxes && jsonBoxes[0] is TextBox txtJson)
+            SelectedJsonDirectory = txtJson.Text?.Trim();
+
+        if (Controls.Find("txtDecommission", true) is { Length: > 0 } decBoxes && decBoxes[0] is TextBox txtDecommission)
+            SelectedDecommissionDirectory = txtDecommission.Text?.Trim();
+
+        DialogResult = DialogResult.OK; // ← critical: signal acceptance to caller
+        Close();
+    }
+}
diff --git a/MainForm.cs b/MainForm.cs
index 0000000..0000000 100644
--- a/MainForm.cs
+++ b/MainForm.cs
@@ -1,0 +1,112 @@
+using System;
+using System.Windows.Forms;
+
+namespace ProgramManager;
+
+public partial class MainForm : Form
+{
+    // Assuming you already keep these around:
+    private readonly StateManager _stateManager;
+    private AppState _state;        // adapt to your actual type name
+    private AppSettings _settings;  // adapt to your actual type name
+
+    // Example constructor showing StateManager baseDir unification.
+    public MainForm()
+    {
+        InitializeComponent();
+
+        // Default base dir is %APPDATA%\GLACTPM. You can pass a custom one if needed.
+        _stateManager = new StateManager();
+
+        // Use your existing initialize/load routines for state/settings,
+        // or call the helpers directly if convenient:
+        _state     = _stateManager.LoadState(() => new AppState());                // or your existing LoadState()
+        _settings  = _stateManager.LoadSettingsOr(new AppSettings());              // loads settings.json or a fresh default
+        ApplySettingsToUi(_settings);
+    }
+
+    // Wherever you open the Settings dialog (e.g., menu item click):
+    private void menuSettings_Click(object? sender, EventArgs e)
+    {
+        using var dlg = new SettingsForm();
+        // Optionally pre-fill existing values into the dialog controls here…
+        if (dlg.ShowDialog(this) == DialogResult.OK)
+        {
+            // Pull chosen directories back out of the dialog:
+            if (!string.IsNullOrWhiteSpace(dlg.SelectedRootDirectory))
+                _settings.LastSearchDirectory = dlg.SelectedRootDirectory;
+            if (!string.IsNullOrWhiteSpace(dlg.SelectedArchiveDirectory))
+                _settings.ArchiveDirectory = dlg.SelectedArchiveDirectory;
+            if (!string.IsNullOrWhiteSpace(dlg.SelectedJsonDirectory))
+                _settings.JsonDirectory = dlg.SelectedJsonDirectory;
+            if (!string.IsNullOrWhiteSpace(dlg.SelectedDecommissionDirectory))
+                _settings.DecommissionDirectory = dlg.SelectedDecommissionDirectory;
+
+            ApplySettingsToUi(_settings);
+
+            // Persist immediately (Option 4: via StateManager)
+            _stateManager.SaveSettings(_settings);
+        }
+    }
+
+    private void ApplySettingsToUi(AppSettings s)
+    {
+        // Mirror to UI controls as needed, e.g.:
+        if (Controls.Find("txtRoot", true) is { Length: > 0 } rootBoxes && rootBoxes[0] is TextBox txtRoot)
+            txtRoot.Text = s.LastSearchDirectory ?? "";
+        if (Controls.Find("txtArchive", true) is { Length: > 0 } archBoxes && archBoxes[0] is TextBox txtArchive)
+            txtArchive.Text = s.ArchiveDirectory ?? "";
+        if (Controls.Find("txtJson", true) is { Length: > 0 } jsonBoxes && jsonBoxes[0] is TextBox txtJson)
+            txtJson.Text = s.JsonDirectory ?? "";
+        if (Controls.Find("txtDecommission", true) is { Length: > 0 } decBoxes && decBoxes[0] is TextBox txtDecommission)
+            txtDecommission.Text = s.DecommissionDirectory ?? "";
+    }
+}
diff --git a/README.md b/README.md
index 0000000..0000000 100644
--- a/README.md
+++ b/README.md
@@ -1,0 +1,30 @@
+# Settings & State persistence
+
+This app now persists **both** files under a single base directory:
+
+- **Windows:** `%APPDATA%\GLACTPM\`
+- **Other OS:** the platform ApplicationData folder equivalent.
+
+Files:
+- `state.json`
+- `settings.json`
+
+You can override the base directory by constructing `StateManager` with `baseDir: "..."` or override individual file paths via `stateFilePath` / `settingsFilePath`.
+
+When the Settings dialog is confirmed (OK), the app writes `settings.json` immediately via `StateManager.SaveSettings(...)`.
*** END PATCH
PATCH
git status

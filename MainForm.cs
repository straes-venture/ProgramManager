// ==============================================================================================
// MainForm.cs
// ==============================================================================================
// PURPOSE:
//   - UI and orchestration: builds the form, runs the search, populates the grid, maintains the
//     TreeView, and manages the Details panel. Also handles persistence and inter-control syncing.
//   - Provides an Archive directory picker and a Clean Up workflow that moves extra ACD/RSS files
//     into the archive directory FLAT (no subfolders).
// ==============================================================================================

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using Microsoft.VisualBasic.FileIO;
using System.IO.Compression;
using FileHunter;

namespace ProgramManager
{
    // ==============================================================================================
    // [BEGIN] MAIN FORM (UI + LOGIC COORDINATION)
    // ----------------------------------------------------------------------------------------------
    public class MainForm : Form
    {
        // ----- [BEGIN] UI FIELD DECLARATIONS -------------------------------------------------------
        // Make all UI fields nullable
        private TextBox? txtRoot;
        private Button? btnBrowse;
        private Button? btnSearch;
        private TextBox? txtArchive;
        private Button? btnBrowseArchive;
        private Button? btnCleanup;
        private SplitContainer? split;
        private TreeView? navTree;
        private SplitContainer? rightSplit;
        private DataGridView? grid;
        private TextBox? txtNotes;

        // Details panel
        private Panel? detailsPanel;
        private Panel? detailsHeaderPanel;
        private Label? lblDetailsHeader;
        private Label? lblDuplicateWarn;
        private ListView? lvDetails;

        private Button? btnViewOldest;

        private ContextMenuStrip? lvDetailsMenu;

        // New menu bar fields
        private MenuStrip? menuBar;
        private ToolStripMenuItem? fileMenu;
        private ToolStripMenuItem? helpMenu;
        private ToolStripMenuItem? settingsMenu;
        private SettingsForm? settingsForm;

        // ----- [END] UI FIELD DECLARATIONS ---------------------------------------------------------

        // ----- [BEGIN] STATE MANAGEMENT FIELDS -----------------------------------------------------
        private readonly string? statePath;
        private AppState state = new();
        private AppSettings settings;

        private List<string> oldestFiles = new List<string>();

        private string jsonDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GLACTPM");

        // ----- [END] STATE MANAGEMENT FIELDS -------------------------------------------------------

        // ----- [BEGIN] VISUAL STYLES / CONSTANTS ---------------------------------------------------
        private static readonly string MissingMerDisplay = "MER file not found";
        private static readonly Color MissingBackColor = Color.MistyRose;
        private static readonly Color MissingForeColor = Color.Maroon;
        private static readonly Color WarnBack = Color.MistyRose;
        private static readonly Color WarnFore = Color.Maroon;
        // ----- [END] VISUAL STYLES / CONSTANTS -----------------------------------------------------

        // ------------------------------------------------------------------------------------------
        // [BEGIN] CONSTRUCTOR: BUILD UI, WIRE EVENTS, LOAD STATE, REBUILD TREE
        // ------------------------------------------------------------------------------------------

        public MainForm(int minNotesWidth)
        {
            // === Window ===
            Text = "Greeley LACT Program Manager";
            Width = 1400;
            Height = 860;
            StartPosition = FormStartPosition.CenterScreen;

            // === Menu Bar ===
            menuBar = new MenuStrip();
            menuBar.BackColor = SystemColors.Control;
            fileMenu = new ToolStripMenuItem("File");
            fileMenu.DropDownItems.Add("New Unit...", null, (s, e) => ShowNewUnitDialog());
            fileMenu.DropDownItems.Add("Decommission...", null, (s, e) => ShowDecommissionDialog());
            fileMenu.DropDownItems.Add("Save Results...", null, (s, e) => SaveJsonFile());
            fileMenu.DropDownItems.Add("Load Results...", null, (s, e) => LoadJsonFile());
            fileMenu.DropDownItems.Add("Save Settings...", null, (s, e) => SaveSettingsOnly());
            fileMenu.DropDownItems.Add("Load Settings...", null, (s, e) => LoadSettingsOnly());
            fileMenu.DropDownItems.Add("Exit", null, (s, e) => Close()); ;
            helpMenu = new ToolStripMenuItem("Help");
            helpMenu.DropDownItems.Add("About", null, (s, e) => MessageBox.Show(this, "Greeley LACT Program Manager\nVersion 1.0\nJeremiah Lee\n2025", "About", MessageBoxButtons.OK, MessageBoxIcon.Information));
            settingsMenu = new ToolStripMenuItem("Settings");
            settingsMenu.Click += (s, e) =>
            {
                var rootText = txtRoot?.Text ?? string.Empty;
                var archiveText = txtArchive?.Text ?? string.Empty;
                var jsonText = jsonDirectory ?? string.Empty;
                if (settingsForm == null) settingsForm = new SettingsForm(rootText, archiveText, jsonText);
                if (settingsForm.ShowDialog(this) == DialogResult.OK)
                {
                    if (txtRoot != null)
                        txtRoot.Text = settingsForm.ProgramDirectory;
                    if (txtArchive != null)
                        txtArchive.Text = settingsForm.ArchiveDirectory;
                    jsonDirectory = settingsForm.JsonDirectory;
                    settings.ProgramDirectory = settingsForm.ProgramDirectory;
                    settings.ArchiveDirectory = settingsForm.ArchiveDirectory;
                    settings.JsonDirectory = settingsForm.JsonDirectory;
                    settings.DecommissionDirectory = settingsForm.DecommissionDirectory;
                    SaveState(); // Optionally save immediately
                }
            };
            var actionsMenu = new ToolStripMenuItem("Actions");
            actionsMenu.DropDownItems.Add("Search/Update", null, (s, e) => RunSearch(
                // Persist + rebuild tree
                settings));
            actionsMenu.DropDownItems.Add("View Multiple Files", null, (s, e) => ShowOldestFilesDialog());
            actionsMenu.DropDownItems.Add("Delete BAKs", null, (s, e) => CleanupDuplicates());
            actionsMenu.DropDownItems.Add("Zip Programs", null, (s, e) => ZipAllPrograms());
            actionsMenu.DropDownItems.Add("Zip MERs", null, (s, e) => ZipAllMERs());

            menuBar.Items.Add(fileMenu);
            menuBar.Items.Add(settingsMenu);
            menuBar.Items.Add(actionsMenu); // Add Actions before Help
            menuBar.Items.Add(helpMenu);

            // Add Filters menu before Help
            var filtersMenu = new ToolStripMenuItem("Filters");
            var chkMissingOnlyMenu = new ToolStripMenuItem("Missing MER Only") { CheckOnClick = true };
            var chkNoProgramFilesMenu = new ToolStripMenuItem("Missing Program Only") { CheckOnClick = true };

            // In the MainForm constructor, update the Filters menu event wiring:
            chkMissingOnlyMenu.CheckedChanged += (s, e) =>
            {
                if (chkMissingOnlyMenu.Checked && chkNoProgramFilesMenu.Checked)
                    chkNoProgramFilesMenu.Checked = false;
                RefreshFilterFromTree();
            };
            chkNoProgramFilesMenu.CheckedChanged += (s, e) =>
            {
                if (chkNoProgramFilesMenu.Checked && chkMissingOnlyMenu.Checked)
                    chkMissingOnlyMenu.Checked = false;
                RefreshFilterFromTree();
            };

            filtersMenu.DropDownItems.Add(chkMissingOnlyMenu);
            filtersMenu.DropDownItems.Add(chkNoProgramFilesMenu);
            menuBar.Items.Insert(menuBar.Items.IndexOf(helpMenu), filtersMenu);

            MainMenuStrip = menuBar;
            Controls.Add(menuBar);

            // === Top row: path + actions ===
            // Adjust positions and widths for better screen usage
            txtRoot = new TextBox { Left = 12, Top = 20, Width = 600, Height = 28, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            btnBrowse = new Button { Left = txtRoot.Right + 8, Top = 20, Width = 160, Height = 28, Text = "Program Directory...", Anchor = AnchorStyles.Top | AnchorStyles.Right };
            btnSearch = new Button { Left = btnBrowse.Right + 8, Top = 20, Width = 120, Height = 28, Text = "Search", Anchor = AnchorStyles.Top | AnchorStyles.Right };

            // Archive row
            txtArchive = new TextBox { Left = 12, Top = 60, Width = 600, Height = 28, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            btnBrowseArchive = new Button { Left = txtArchive.Right + 8, Top = 60, Width = 160, Height = 28, Text = "Archive Directory…", Anchor = AnchorStyles.Top | AnchorStyles.Right };
            btnViewOldest = new Button { Left = btnBrowseArchive.Right + 8, Top = 60, Width = 160, Height = 28, Text = "View Multiple Files", Anchor = AnchorStyles.Top | AnchorStyles.Right };
            btnCleanup = new Button { Left = btnViewOldest.Right + 8, Top = 60, Width = 160, Height = 28, Text = "Delete BAKs", Anchor = AnchorStyles.Top | AnchorStyles.Right };

            // Move this block to AFTER split is created and added to Controls
            split = new SplitContainer
            {
                Left = 12,
                Top = 50,
                Width = ClientSize.Width - 24,
                Height = ClientSize.Height - 112,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                SplitterDistance = (ClientSize.Width - 24) / 6, // Reduce left panel to 1/6 of total width
                Orientation = Orientation.Vertical
            };
            Controls.Add(split);

            // Restore these controls to MainForm:
            // Controls.Add(txtRoot);
            // Controls.Add(btnBrowse);
            // Controls.Add(btnSearch);
            // Controls.Add(txtArchive);
            // Controls.Add(btnBrowseArchive);
            // Controls.Add(btnViewOldest);
            // Controls.Add(btnCleanup);
            // Controls.Add(chkMissingOnly);
            // Controls.Add(chkNoProgramFiles);

            // Optionally, set Visible = false if you want to keep them for logic but hide them
            txtRoot.Visible = false;
            btnBrowse.Visible = false;
            btnSearch.Visible = false;
            txtArchive.Visible = false;
            btnBrowseArchive.Visible = false;
            btnViewOldest.Visible = false;
            btnCleanup.Visible = false;

            // Set new positions and widths for a more balanced layout
            txtRoot.SetBounds(12, 20, 600, 28);
            btnBrowse.SetBounds(txtRoot.Right + 8, 20, 160, 28);
            btnSearch.SetBounds(btnBrowse.Right + 8, 20, 120, 28);

            txtArchive.SetBounds(12, 60, 600, 28);
            btnBrowseArchive.SetBounds(txtArchive.Right + 8, 60, 160, 28);
            btnViewOldest.SetBounds(btnBrowseArchive.Right + 8, 60, 160, 28);
            btnCleanup.SetBounds(btnViewOldest.Right + 8, 60, 160, 28);

            // Add controls to the form if not already present
            if (!Controls.Contains(txtRoot)) Controls.Add(txtRoot);
            if (!Controls.Contains(btnBrowse)) Controls.Add(btnBrowse);
            if (!Controls.Contains(btnSearch)) Controls.Add(btnSearch);
            if (!Controls.Contains(txtArchive)) Controls.Add(txtArchive);
            if (!Controls.Contains(btnBrowseArchive)) Controls.Add(btnBrowseArchive);
            if (!Controls.Contains(btnViewOldest)) Controls.Add(btnViewOldest);
            if (!Controls.Contains(btnCleanup)) Controls.Add(btnCleanup);

            // Left: TreeView
            navTree = new TreeView { Dock = DockStyle.Fill, HideSelection = false };
            navTree.AfterSelect += NavTree_AfterSelect;
            // Left: TreeView
            if (split != null)
            {
                split.Panel1.Controls.Add(navTree);
            }

            // Right: nested split (grid over details)
            rightSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                FixedPanel = FixedPanel.Panel2 // keep details visible
            };

            // Create a vertical split for grid (left) and notes (right)
            var gridAndNotesSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                //SplitterDistance = (int)((ClientSize.Width - 24) * 0.65), // 65% grid, 35% notes
                SplitterDistance = this.Width - minNotesWidth - split.SplitterDistance,
                FixedPanel = FixedPanel.Panel2
            };
            split.Panel2.Controls.Add(gridAndNotesSplit);

            // Left: grid and details (stacked vertically)
            rightSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                FixedPanel = FixedPanel.Panel2,
                MinimumSize = new Size(400, 0)
            };
            gridAndNotesSplit.Panel1.Controls.Add(rightSplit);

            // Top-left: grid
            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false
            };
            rightSplit.Panel1.Controls.Add(grid);

            // Columns (requested order)
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Location", HeaderText = "Location" });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Unit", HeaderText = "Unit" });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ProgramFile", HeaderText = "Program File" });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ProgramFileModified", HeaderText = "Program File Modified" });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "QuickPanelFile", HeaderText = "Quick Panel File" });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "QuickPanelFileModified", HeaderText = "Quick Panel File Modified" });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Quarter", HeaderText = "Quarter" });

            // Bottom-left: details panel (unchanged)
            detailsPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
            rightSplit.Panel2.Controls.Add(detailsPanel);

            // Header panel holds labels and docks at top
            detailsHeaderPanel = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(0, 0, 0, 4)
            };

            lblDetailsHeader = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Text = "Details: (select a Unit on the left)",
                Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
                BackColor = SystemColors.Control
            };

            lblDuplicateWarn = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Text = "",
                ForeColor = WarnFore,
                BackColor = SystemColors.Control,
                Padding = new Padding(0, 4, 0, 0)
            };

            detailsHeaderPanel.Controls.Add(lblDuplicateWarn);
            detailsHeaderPanel.Controls.Add(lblDetailsHeader);

            // ListView fills remaining space
            lvDetails = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Margin = new Padding(0)
            };
            lvDetails.Columns.Add("File Name", 240);
            lvDetails.Columns.Add("Type", 90);
            lvDetails.Columns.Add("Modified", 160);
            lvDetails.Columns.Add("Size (KB)", 90, HorizontalAlignment.Right);
            lvDetails.Columns.Add("Directory", 320);
            lvDetails.Columns.Add("Full Path", 500);

            // Add in this order: list first (Fill), then header (Top) so header sits above list
            detailsPanel.Controls.Add(lvDetails);
            detailsPanel.Controls.Add(detailsHeaderPanel);

            // 3. Notes section (right of grid, top to bottom)
            var notesPanel = new Panel { Dock = DockStyle.Fill, MinimumSize = new Size(150, 0) };
            gridAndNotesSplit.Panel2.Controls.Add(notesPanel);
            minNotesWidth = this.Width / 4;
            int maxNotesWidth = this.Width / 2;

            gridAndNotesSplit.Panel2MinSize = 120;
            gridAndNotesSplit.Panel2.MaximumSize = new Size(maxNotesWidth, 0);

            // Set SplitterDistance so notes panel starts at its minimum width
            gridAndNotesSplit.SplitterDistance = gridAndNotesSplit.Width - minNotesWidth;

            var notesLabel = new Label
            {
                Text = "Notes",
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                Height = 32,
                AutoSize = false,
                MinimumSize = new Size(0, 32),
                MaximumSize = new Size(0, 32),
                TextAlign = ContentAlignment.MiddleLeft
            };

            txtNotes = new TextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                Multiline = true,
                Font = new Font("Segoe UI", 10),
                ScrollBars = ScrollBars.Vertical,
                PlaceholderText = "No notes entered for this unit"
            };

            var btnSaveNote = new Button
            {
                Text = "Save Note",
                Dock = DockStyle.Bottom,
                Height = 40
            };
            btnSaveNote.Click += (_, __) => SaveCurrentNote();

            //notesPanel.Controls.Add(notesLabel); // Dock: Top
            notesPanel.Controls.Add(btnSaveNote);// Dock: Bottom
            notesPanel.Controls.Add(txtNotes);   // Dock: Fill

            notesPanel.PerformLayout();

            var stateManager = new StateManager();
            settings = stateManager.LoadSettings();

            // Events
            btnBrowse.Click += (_, __) => BrowseFolder();
            btnBrowseArchive.Click += (_, __) => BrowseArchive();
            btnCleanup.Click += (_, __) => CleanupDuplicates();
            btnViewOldest.Click += (_, __) => ShowOldestFilesDialog();

            // GRID -> TREE synchronization (event wiring)
            grid.CellClick += GridRowSelectsTreeNode;
            grid.CellDoubleClick += GridRowSelectsTreeNode;

            // Make files in details panel openable with default program
            lvDetails.ItemActivate += (s, e) =>
            {
                if (lvDetails.SelectedItems.Count > 0)
                {
                    var item = lvDetails.SelectedItems[0];
                    // Check if this is a directory header line
                    if (item.Text.StartsWith("[Directory]"))
                    {
                        var dirPath = item.SubItems[4].Text;
                        if (!string.IsNullOrWhiteSpace(dirPath) && Directory.Exists(dirPath))
                        {
                            try
                            {
                                Process.Start(new ProcessStartInfo
                                {
                                    FileName = dirPath,
                                    UseShellExecute = true
                                });
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(this, $"Unable to open directory:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    }
                    else
                    {
                        // Existing file open logic
                        var filePath = item.Tag as string;
                        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                        {
                            try
                            {
                                Process.Start(new ProcessStartInfo
                                {
                                    FileName = filePath,
                                    UseShellExecute = true
                                });
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(this, $"Unable to open file:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    }
                }
            };

            // State path
            var appDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FileHunter");
            Directory.CreateDirectory(appDir);
            statePath = Path.Combine(appDir, "state.json");

            // Initial state + tree
            LoadState();
            RebuildTreeFromState();

            // Ensure details panel never collapses (post-layout init)
            this.Shown += (_, __) => InitializeSplitters();
            this.ResizeEnd += (_, __) => InitializeSplitters();

            // Add context menu for details ListView
            lvDetailsMenu = new ContextMenuStrip();
            lvDetailsMenu.Items.Add("Open", null, (s, e) => OpenSelectedDetail());
            lvDetailsMenu.Items.Add("Open Directory", null, (s, e) => OpenSelectedDirectory());
            lvDetailsMenu.Items.Add("Copy File Path", null, (s, e) => CopySelectedPath());
            lvDetailsMenu.Items.Add("Archive File", null, (s, e) => ArchiveSelectedFile());
            //lvDetailsMenu.Items.Add("Edit Metadata", null, (s, e) => EditSelectedMetadata());
            lvDetailsMenu.Items.Add("Edit File Properties", null, (s, e) => EditSelectedFileProperties());

            lvDetails.ContextMenuStrip = lvDetailsMenu;
            lvDetails.MouseUp += (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    var hitTest = lvDetails.HitTest(e.Location);
                    var item = hitTest.Item;
                    if (item != null && !item.Text.StartsWith("[Directory]"))
                    {
                        lvDetails.SelectedItems.Clear();
                        item.Selected = true; // Select the item under the mouse
                        lvDetailsMenu.Show(lvDetails, e.Location);
                    }
                }
            };

            this.Shown += (s, e) =>
            {
                var missing = new List<string>();
                var root = txtRoot?.Text?.Trim() ?? "";
                var archive = txtArchive?.Text?.Trim() ?? "";
                var json = jsonDirectory ?? "";
                var decomm = settingsForm?.DecommissionDirectory ?? "";

                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) missing.Add("Program Directory");
                if (string.IsNullOrWhiteSpace(archive) || !Directory.Exists(archive)) missing.Add("Archive Directory");
                if (string.IsNullOrWhiteSpace(json) || !Directory.Exists(json)) missing.Add("Results Directory");
                if (string.IsNullOrWhiteSpace(decomm) || !Directory.Exists(decomm)) missing.Add("Decommission Folder");

                if (missing.Count > 0)
                {
                    MessageBox.Show(this,
                        $"The following directories are missing or invalid:\n\n{string.Join("\n", missing)}\n\nPlease set them in Settings.",
                        "Missing Directories",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            };
        }

            // Update ShowDecommissionDialog to filter units by selected location
            private void ShowDecommissionDialog()
            {
                var locations = state.Results?.Select(r => r.Location ?? "")
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(l => l, StringComparer.OrdinalIgnoreCase)
                    .ToList() ?? new List<string>();

                var dlg = new Form
                {
                    Text = "Decommission Unit",
                    Width = 400,
                    Height = 260,
                    StartPosition = FormStartPosition.CenterParent,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    MinimizeBox = false
                };

                var lblLocation = new Label { Text = "Location:", Left = 20, Top = 20, Width = 80 };
                var cmbLocation = new ComboBox { Left = 110, Top = 20, Width = 240, DropDownStyle = ComboBoxStyle.DropDownList };
                cmbLocation.Items.AddRange(locations.ToArray());
                if (cmbLocation.Items.Count > 0) cmbLocation.SelectedIndex = 0;

                var lblUnit = new Label { Text = "Unit:", Left = 20, Top = 60, Width = 80 };
                var cmbUnit = new ComboBox { Left = 110, Top = 60, Width = 240, DropDownStyle = ComboBoxStyle.DropDownList };

                void UpdateUnits()
                {
                    var selectedLoc = cmbLocation.SelectedItem?.ToString() ?? "";
                    var units = state.Results?.Where(r => string.Equals(r.Location, selectedLoc, StringComparison.OrdinalIgnoreCase))
                        .Select(r => r.Unit ?? "")
                        .Where(u => !string.IsNullOrWhiteSpace(u))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(u => u, StringComparer.OrdinalIgnoreCase)
                        .ToList() ?? new List<string>();
                    cmbUnit.Items.Clear();
                    cmbUnit.Items.AddRange(units.ToArray());
                    if (cmbUnit.Items.Count > 0) cmbUnit.SelectedIndex = 0;
                }
                cmbLocation.SelectedIndexChanged += (s, e) => UpdateUnits();
                UpdateUnits();

                var lblDate = new Label { Text = "Date:", Left = 20, Top = 100, Width = 80 };
                var dtDate = new DateTimePicker { Left = 110, Top = 100, Width = 240, Value = DateTime.Today };

                var btnOk = new Button { Text = "Decommission", Left = 110, Top = 160, Width = 120, DialogResult = DialogResult.OK };
                var btnCancel = new Button { Text = "Cancel", Left = 240, Top = 160, Width = 80, DialogResult = DialogResult.Cancel };

                dlg.Controls.AddRange(new Control[] { lblLocation, cmbLocation, lblUnit, cmbUnit, lblDate, dtDate, btnOk, btnCancel });
                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    string root = txtRoot?.Text?.Trim() ?? "";
                    string decommFolder = settingsForm?.DecommissionDirectory ?? "";
                    if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                    {
                        MessageBox.Show(this, "Please choose a valid Program Directory first.", "Invalid Folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    if (string.IsNullOrWhiteSpace(decommFolder) || !Directory.Exists(decommFolder))
                    {
                        MessageBox.Show(this, "Please choose a valid Decommission Folder in Settings.", "Invalid Folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    string location = cmbLocation.SelectedItem?.ToString() ?? "";
                    string unit = cmbUnit.SelectedItem?.ToString() ?? "";
                    if (string.IsNullOrWhiteSpace(location) || string.IsNullOrWhiteSpace(unit))
                    {
                        MessageBox.Show(this, "Please select a location and unit.", "Input Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    string unitPath = Path.Combine(root, location, unit);
                    if (!Directory.Exists(unitPath))
                    {
                        MessageBox.Show(this, "Unit folder does not exist.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    string zipName = $"{location}-{unit}-{dtDate.Value:yyyyMMdd}.zip";
                    string zipPath = Path.Combine(decommFolder, zipName);
                    try
                    {
                        if (File.Exists(zipPath)) File.Delete(zipPath);
                        System.IO.Compression.ZipFile.CreateFromDirectory(unitPath, zipPath);
                        MessageBox.Show(this, $"Unit folder zipped and moved to:\n{zipPath}", "Decommission Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, $"Failed to decommission:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        // [END] CONSTRUCTOR
        // ------------------------------------------------------------------------------------------


        // ------------------------------------------------------------------------------------------
        // [BEGIN] LAYOUT INITIALIZATION (post-layout sizing of split panels)
        // ------------------------------------------------------------------------------------------
        private void InitializeSplitters()
        {
            try
            {
                if (rightSplit != null)
                {
                    rightSplit.Panel2MinSize = 160;
                    rightSplit.IsSplitterFixed = false;

                    int h = rightSplit.ClientSize.Height;
                    if (h > 0)
                    {
                        rightSplit.SplitterDistance = Math.Max(220, (int)(h * 0.62));
                    }
                }
            }
            catch { /* non-fatal */ }
        }
        // [END] LAYOUT INITIALIZATION
        // ------------------------------------------------------------------------------------------


        // ------------------------------------------------------------------------------------------
        // [BEGIN] UI ACTION: BROWSE FOR FOLDER
        // ------------------------------------------------------------------------------------------
        private void BrowseFolder()
        {
            if (txtRoot != null)
            {
                using var dlg = new FolderBrowserDialog { Description = "Select the Level 1 folder (root of your search).", ShowNewFolderButton = false };
                if (Directory.Exists(txtRoot.Text)) dlg.SelectedPath = txtRoot.Text;
                if (dlg.ShowDialog(this) == DialogResult.OK) txtRoot.Text = dlg.SelectedPath;
            }
        }
        // [END] UI ACTION: BROWSE FOR FOLDER
        // ------------------------------------------------------------------------------------------

        // ------------------------------------------------------------------------------------------
        // [BEGIN] UI ACTION: BROWSE ARCHIVE FOLDER
        // ------------------------------------------------------------------------------------------
        private void BrowseArchive()
        {
            if (txtArchive != null)
            {
                using var dlg = new FolderBrowserDialog { Description = "Select an Archive folder (flat).", ShowNewFolderButton = true };
                if (Directory.Exists(txtArchive.Text)) dlg.SelectedPath = txtArchive.Text;
                if (dlg.ShowDialog(this) == DialogResult.OK) txtArchive.Text = dlg.SelectedPath;
            }
        }
        // ------------------------------------------------------------------------------------------
        // In RunSearch(), update oldestFiles to include ALL program files in duplicate folders, not just the oldest.
        private void RunSearch(AppSettings settings)
        {
            var root = txtRoot?.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                MessageBox.Show(this, "Please choose a valid directory.", "Invalid Folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Gather all directories first for progress bar
            var allDirs = Directory.EnumerateDirectories(root, "*", System.IO.SearchOption.AllDirectories).Prepend(root).ToList();

            // Progress bar dialog
            using var progressForm = new Form
            {
                Text = "Searching Files...",
                Width = 400,
                Height = 120,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                ControlBox = false
            };
            var progressBar = new ProgressBar
            {
                Dock = DockStyle.Top,
                Minimum = 0,
                Maximum = allDirs.Count,
                Value = 0,
                Height = 32,
                Style = ProgressBarStyle.Continuous
            };
            var lbl = new Label
            {
                Dock = DockStyle.Top,
                Text = "Searching directories...",
                TextAlign = ContentAlignment.MiddleCenter,
                Height = 32
            };
            progressForm.Controls.Add(lbl);
            progressForm.Controls.Add(progressBar);

            progressForm.Load += (s, e) =>
            {
                // Center relative to MainForm
                progressForm.Location = new Point(
                    this.Location.X + (this.Width - progressForm.Width) / 2,
                    this.Location.Y + (this.Height - progressForm.Height) / 2
                );
            };
            progressForm.Show();

            Cursor = Cursors.WaitCursor;
            try
            {
                // Enumerate .ACD/.RSS (exclude bak files)
                var programPaths = Directory.EnumerateFiles(root, "*.*", System.IO.SearchOption.AllDirectories)
                    .Where(p => (Helpers.HasExtension(p, ".ACD") || Helpers.HasExtension(p, ".RSS")) && !Helpers.IsBakFile(p))
                    .ToList();

                var rows = new List<SearchRow>();

                oldestFiles.Clear();
                int dirIndex = 0;
                foreach (var dir in allDirs)
                {
                    dirIndex++;
                    progressBar.Value = dirIndex;
                    lbl.Text = $"Processing {dirIndex} of {allDirs.Count} directories...";
                    Application.DoEvents();

                    var filesInDir = programPaths
                        .Where(p => string.Equals(Path.GetDirectoryName(p), dir, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    int countInDir = filesInDir.Count;
                    var merFiles = Directory.EnumerateFiles(dir, "*.MER", System.IO.SearchOption.TopDirectoryOnly).ToList();
                    int merCount = merFiles.Count;
                    var bakFiles = Directory.EnumerateFiles(dir, "*", System.IO.SearchOption.TopDirectoryOnly)
                        .Where(Helpers.IsBakFile)
                        .ToList();
                    int bakCount = bakFiles.Count;

                    // Add a row for directories with program files
                    if (countInDir > 0)
                    {
                        string programName = countInDir > 1 ? "Multiple program files" : Path.GetFileName(filesInDir[0]);
                        if (bakCount > 0)
                            programName += $" (plus {bakCount} bak file{(bakCount > 1 ? "s" : "")})";

                        DateTime programModified = File.GetLastWriteTime(filesInDir[0]);

                        string quickPanelName;
                        DateTime? quickPanelModified;
                        if (merCount > 1)
                        {
                            quickPanelName = "Multiple MER files";
                            quickPanelModified = null;
                        }
                        else
                        {
                            string? merPath = merFiles.FirstOrDefault();
                            quickPanelName = merPath != null ? Path.GetFileName(merPath) : MissingMerDisplay;
                            quickPanelModified = merPath != null ? File.GetLastWriteTime(merPath) : (DateTime?)null;
                        }

                        string relDir = Helpers.GetRelativePathPortable(root, dir);
                        var (location, unit) = Helpers.ExtractLocationUnit(relDir);
                        string quarter = Helpers.ToQuarter(programModified);

                        rows.Add(new SearchRow
                        {
                            Location = location,
                            Unit = unit,
                            ProgramFile = programName,
                            ProgramFileModified = programModified,
                            QuickPanelFile = quickPanelName,
                            QuickPanelFileModified = quickPanelModified,
                            Quarter = quarter,
                            DirectoryPath = dir,
                            ProgramCountInDir = countInDir
                        });

                        // Update: add all program files in this directory to oldestFiles
                        oldestFiles.AddRange(filesInDir);
                    }
                    // Add a row for directories with MER files but no program files
                    else if (merCount > 0)
                    {
                        string quickPanelName;
                        DateTime? quickPanelModified;
                        if (merCount > 1)
                        {
                            quickPanelName = "Multiple MER files";
                            quickPanelModified = null;
                        }
                        else
                        {
                            string? merPath = merFiles.FirstOrDefault();
                            quickPanelName = merPath != null ? Path.GetFileName(merPath) : MissingMerDisplay;
                            quickPanelModified = merPath != null ? File.GetLastWriteTime(merPath) : (DateTime?)null;
                        }

                        string relDir = Helpers.GetRelativePathPortable(root, dir);
                        var (location, unit) = Helpers.ExtractLocationUnit(relDir);

                        rows.Add(new SearchRow
                        {
                            Location = location,
                            Unit = unit,
                            ProgramFile = "Program file not found",
                            ProgramFileModified = DateTime.MinValue,
                            QuickPanelFile = quickPanelName,
                            QuickPanelFileModified = quickPanelModified,
                            Quarter = "",
                            DirectoryPath = dir,
                            ProgramCountInDir = 0
                        });
                    }
                }

                // Order and display
                var ordered = rows.OrderBy(r => r.Location, StringComparer.OrdinalIgnoreCase)
                                  .ThenBy(r => r.Unit, StringComparer.OrdinalIgnoreCase)
                                  .ThenBy(r => r.ProgramFile, StringComparer.OrdinalIgnoreCase)
                                  .ToList();

                PopulateGrid(ordered);
                settings.LastSearchDirectory = root;
                state.Results = ordered;
                SaveState();
                RebuildTreeFromState();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Search failed:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                progressForm.Close();
                Cursor = Cursors.Default;
            }
        }
        // [END] CORE SEARCH PIPELINE
        // ------------------------------------------------------------------------------------------


        // ------------------------------------------------------------------------------------------
        // [BEGIN] TREEVIEW NAVIGATION + FILTERING + DETAILS
        // ------------------------------------------------------------------------------------------
        private void RebuildTreeFromState()
        {
            if (navTree == null) return;
            navTree.BeginUpdate();
            navTree.Nodes.Clear();

            var rootNode = navTree.Nodes.Add("All Results");
            rootNode.Tag = new TreeTag { Kind = TreeTagKind.All };

            if (state?.Results != null && state.Results.Any())
            {
                var byLoc = state.Results.GroupBy(r => r.Location ?? "", StringComparer.OrdinalIgnoreCase)
                                         .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

                foreach (var locGroup in byLoc)
                {
                    var locText = string.IsNullOrWhiteSpace(locGroup.Key) ? "(no location)" : locGroup.Key;
                    var locNode = rootNode.Nodes.Add(locText);
                    locNode.Tag = new TreeTag { Kind = TreeTagKind.Location, Location = locGroup.Key };

                    var byUnit = locGroup.GroupBy(r => r.Unit ?? "", StringComparer.OrdinalIgnoreCase)
                                         .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);
                    foreach (var unitGroup in byUnit)
                    {
                        var unitText = string.IsNullOrWhiteSpace(unitGroup.Key) ? "(no unit)" : unitGroup.Key;
                        var unitNode = locNode.Nodes.Add(unitText);
                        unitNode.Tag = new TreeTag { Kind = TreeTagKind.Unit, Location = locGroup.Key, Unit = unitGroup.Key };
                    }
                }

                rootNode.Expand(); // show locations
            }

            navTree.EndUpdate();
            navTree.SelectedNode = rootNode; // default to "All"
            ClearDetailsPanel();              // visible + instructional
        }

        private void NavTree_AfterSelect(object? sender, TreeViewEventArgs e)
        {
            RefreshFilterFromTree();

            // Populate details only for Unit nodes
            var tag = e.Node?.Tag as TreeTag;
            if (tag?.Kind == TreeTagKind.Unit) PopulateUnitDetails(tag);
            else ClearDetailsPanel();
        }

        private void RefreshFilterFromTree()
        {
            var tag = navTree.SelectedNode?.Tag as TreeTag ?? new TreeTag { Kind = TreeTagKind.All };
            if (state?.Results == null == true) { PopulateGrid(new List<SearchRow>()); return; }
            PopulateGridFiltered(tag);
        }

        private void PopulateGridFiltered(TreeTag tag)
        {
            IEnumerable<SearchRow> filtered = state.Results ?? Enumerable.Empty<SearchRow>();

            // Apply selection
            if (tag.Kind == TreeTagKind.Location)
            {
                filtered = filtered.Where(r => string.Equals(r.Location ?? "", tag.Location ?? "", StringComparison.OrdinalIgnoreCase));
            }
            else if (tag.Kind == TreeTagKind.Unit)
            {
                filtered = filtered.Where(r =>
                    string.Equals(r.Location ?? "", tag.Location ?? "", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(r.Unit ?? "", tag.Unit ?? "", StringComparison.OrdinalIgnoreCase));
            }

            // Use menu items for filtering
            var filtersMenu = menuBar.Items.OfType<ToolStripMenuItem>().FirstOrDefault(m => m.Text == "Filters");
            var chkMissingOnlyMenu = filtersMenu?.DropDownItems.OfType<ToolStripMenuItem>().FirstOrDefault(m => m.Text == "Missing MER Only");
            var chkNoProgramFilesMenu = filtersMenu?.DropDownItems.OfType<ToolStripMenuItem>().FirstOrDefault(m => m.Text == "Missing Program Only");

            if (chkNoProgramFilesMenu != null && chkNoProgramFilesMenu.Checked)
            {
                filtered = filtered.Where(r => r.ProgramCountInDir == 0);
            }
            // If only "MER missing" is checked
            else if (chkMissingOnlyMenu != null && chkMissingOnlyMenu.Checked)
            {
                filtered = filtered.Where(IsMerMissing);
            }

            // Order and display
            filtered = filtered.OrderBy(r => r.Location, StringComparer.OrdinalIgnoreCase)
                               .ThenBy(r => r.Unit, StringComparer.OrdinalIgnoreCase)
                               .ThenBy(r => r.ProgramFile, StringComparer.OrdinalIgnoreCase);

            PopulateGrid(filtered.ToList());
        }

        // ----- [BEGIN] DETAILS PANEL POPULATION (MER de-dup per directory) -----------------------
        private void PopulateUnitDetails(TreeTag tag)
        {
            if (lblDetailsHeader != null)
                lblDetailsHeader.Text = $"Details: Location = {tag.Location ?? "(no location)"}  |  Unit = {tag.Unit ?? "(no unit)"}";

            if (lvDetails != null)
            {
                lvDetails.BeginUpdate();
                lvDetails.Items.Clear();
            }

            if (state?.Results == null)
            {
                if (lblDuplicateWarn != null)
                {
                    lblDuplicateWarn.Text = "";
                    lblDuplicateWarn.BackColor = SystemColors.Control;
                }
                if (lvDetails != null)
                    lvDetails.EndUpdate();
                return;
            }

            // Load note for this location/unit
            var key = GetNoteKey(tag.Location ?? "", tag.Unit ?? "");
            if (txtNotes != null)
                txtNotes.Text = state.Notes != null && state.Notes.TryGetValue(key, out var note) ? note : "";

            // Group rows by directory and list all actual files in each directory
            var rows = state.Results
                .Where(r =>
                    string.Equals(r.Location ?? "", tag.Location ?? "", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(r.Unit ?? "", tag.Unit ?? "", StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.DirectoryPath ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.ProgramFile ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();

            int multiFolderCount = 0;

            foreach (var dir in rows.Select(r => r.DirectoryPath ?? string.Empty).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(dir)) continue;

                // Directory header
                var dirHeader = new ListViewItem(new[]
                {
                    $"[Directory] {dir}", "", "", "", dir, dir
                })
                { ForeColor = Color.DimGray };
                if (lvDetails != null)
                    lvDetails.Items.Add(dirHeader);

                // List all .ACD/.RSS files (not just summary row)
                var programFiles = Directory.EnumerateFiles(dir, "*.*", System.IO.SearchOption.TopDirectoryOnly)
                    .Where(p => (Helpers.HasExtension(p, ".ACD") || Helpers.HasExtension(p, ".RSS")) && !Helpers.IsBakFile(p))
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (programFiles.Count > 1) multiFolderCount++;

                foreach (var file in programFiles)
                {
                    var info = new FileInfo(file);
                    if (lvDetails != null)
                    {
                        var item = new ListViewItem(new[]
                        {
                            Path.GetFileName(file),
                            "Program",
                            info.LastWriteTime.ToString(),
                            (info.Length / 1024).ToString("N0"),
                            dir,
                            file
                        });
                        item.Tag = file;
                        lvDetails.Items.Add(item);
                    }
                }

                // List all MER files
                var merFiles = Directory.EnumerateFiles(dir, "*.MER", System.IO.SearchOption.TopDirectoryOnly)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (var file in merFiles)
                {
                    var info = new FileInfo(file);
                    if (lvDetails != null)
                    {
                        var item = new ListViewItem(new[]
                        {
                            Path.GetFileName(file),
                            "MER",
                            info.LastWriteTime.ToString(),
                            (info.Length / 1024).ToString("N0"),
                            dir,
                            file
                        });
                        item.Tag = file;
                        lvDetails.Items.Add(item);
                    }
                }

                // If no MER files, show missing
                if (merFiles.Count == 0 && lvDetails != null)
                {
                    var missItem = new ListViewItem(new[]
                    {
                        MissingMerDisplay, "MER", "", "", dir, Path.Combine(dir, "(not found)")
                    })
                    { ForeColor = WarnFore };
                    lvDetails.Items.Add(missItem);
                }
            }

            if (lblDuplicateWarn != null)
            {
                lblDuplicateWarn.Text = multiFolderCount > 0
                    ? $"Warning: {multiFolderCount} folder(s) contain multiple program files."
                    : "";
                lblDuplicateWarn.BackColor = multiFolderCount > 0 ? WarnBack : SystemColors.Control;
            }

            if (lvDetails != null)
                lvDetails.EndUpdate();
        }
        // ----- [END] DETAILS PANEL POPULATION -----------------------------------------------------

        private void ClearDetailsPanel()
        {
            if (lblDetailsHeader != null)
                lblDetailsHeader.Text = "Details: (select a Unit on the left to see files in its folders)";
            if (lblDuplicateWarn != null)
            {
                lblDuplicateWarn.Text = "";
                lblDuplicateWarn.BackColor = SystemColors.Control;
            }
            if (lvDetails != null)
                lvDetails.Items.Clear();
        }
        // [END] TREEVIEW NAVIGATION + FILTERING + DETAILS
        // ------------------------------------------------------------------------------------------


        // ------------------------------------------------------------------------------------------
        // [BEGIN] GRID -> TREE SYNCHRONIZATION
        // ------------------------------------------------------------------------------------------
        private void GridRowSelectsTreeNode(object? sender, DataGridViewCellEventArgs e)
        {
            if (grid == null) return;
            if (e.RowIndex < 0 || e.RowIndex >= grid.Rows.Count) return;

            var row = grid.Rows[e.RowIndex];
            var loc = row.Cells["Location"].Value?.ToString() ?? "";
            var unit = row.Cells["Unit"].Value?.ToString() ?? "";

            // Try to select the matching node in the tree. If found, AfterSelect will handle filtering + details.
            if (!SelectTreeNodeFor(loc, unit))
            {
                // Graceful fallback: open details directly without changing the tree selection.
                var tag = new TreeTag { Kind = TreeTagKind.Unit, Location = loc, Unit = unit };
                PopulateUnitDetails(tag);
            }
        }
        // [END] GRID -> TREE SYNCHRONIZATION
        // ------------------------------------------------------------------------------------------


        // ------------------------------------------------------------------------------------------
        // [BEGIN] GRID POPULATION + ROW HIGHLIGHTING
        // ------------------------------------------------------------------------------------------
        private void PopulateGrid(IList<SearchRow> rows)
        {
            if (grid == null) return;
            grid.SuspendLayout();
            grid.Rows.Clear();

            foreach (var r in rows)
            {
                int idx = grid.Rows.Add(
                    r.Location,
                    r.Unit,
                    r.ProgramFile,
                    r.ProgramFileModified,
                    r.QuickPanelFile,
                    r.QuickPanelFileModified?.ToString() ?? "",
                    r.Quarter
                );

                if (IsMerMissing(r))
                {
                    var row = grid.Rows[idx];
                    row.DefaultCellStyle.BackColor = MissingBackColor;
                    row.DefaultCellStyle.ForeColor = MissingForeColor;
                }
                else if (string.Equals(r.ProgramFile, "Program file not found", StringComparison.OrdinalIgnoreCase))
                {
                    var row = grid.Rows[idx];
                    row.DefaultCellStyle.BackColor = MissingBackColor;
                    row.DefaultCellStyle.ForeColor = MissingForeColor;
                }
            }

            grid.ResumeLayout();
        }

        private bool IsMerMissing(SearchRow r)
        {
            // Exclude BAK summary rows from "MER missing" filter
            if (r.ProgramFile.StartsWith("[Total files with 'bak' in name:", StringComparison.OrdinalIgnoreCase))
                return false;

            return r.QuickPanelFileModified == null ||
                   string.Equals(r.QuickPanelFile ?? "", MissingMerDisplay, StringComparison.OrdinalIgnoreCase);
        }
        // [END] GRID POPULATION + ROW HIGHLIGHTING
        // ------------------------------------------------------------------------------------------


        // ------------------------------------------------------------------------------------------
        // [BEGIN] TREE SEARCH HELPER
        // ------------------------------------------------------------------------------------------
        private bool SelectTreeNodeFor(string location, string unit)
        {
            if (navTree.Nodes.Count == 0) return false;

            // Tree root is "All Results"
            var root = navTree.Nodes[0];

            // Normalize display for "(no location)" and "(no unit)"
            string locDisplay = string.IsNullOrWhiteSpace(location) ? "(no location)" : location;
            string unitDisplay = string.IsNullOrWhiteSpace(unit) ? "(no unit)" : unit;

            // 1) Find Location node
            TreeNode? locNode = null;
            foreach (TreeNode n in root.Nodes)
            {
                if (string.Equals(n.Text, locDisplay, StringComparison.OrdinalIgnoreCase))
                {
                    locNode = n;
                    break;
                }
            }
            if (locNode == null) return false;

            // 2) Find Unit node under that Location
            TreeNode? unitNode = null;
            foreach (TreeNode n in locNode.Nodes)
            {
                if (string.Equals(n.Text, unitDisplay, StringComparison.OrdinalIgnoreCase))
                {
                    unitNode = n;
                    break;
                }
            }
            if (unitNode == null) return false;

            // 3) Select and reveal
            if (!locNode.IsExpanded) locNode.Expand();
            navTree.SelectedNode = unitNode;  // triggers AfterSelect -> filter + details
            navTree.Focus();                   // optional: move keyboard focus to tree
            return true;
        }
        // [END] TREE SEARCH HELPER
        // ------------------------------------------------------------------------------------------


        // ------------------------------------------------------------------------------------------
        // [BEGIN] CLEANUP: DETECT DUPLICATE PROGRAM FOLDERS AND MOVE OLD FILES TO FLAT ARCHIVE
        // ------------------------------------------------------------------------------------------
        private void CleanupDuplicates()
        {
            string searchRoot = txtRoot != null ? txtRoot.Text.Trim() : string.Empty;
            string archiveRoot = txtArchive != null ? txtArchive.Text.Trim() : string.Empty;

            // Validation: require both directories
            if (string.IsNullOrWhiteSpace(searchRoot) || !Directory.Exists(searchRoot))
            {
                MessageBox.Show(this, "Select a valid search directory first.", "Missing Search Directory",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(archiveRoot))
            {
                MessageBox.Show(this, "Select an archive directory.", "Missing Archive Directory",
            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!Directory.Exists(archiveRoot))
            {
                try { Directory.CreateDirectory(archiveRoot); }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Unable to create archive directory:\n{ex.Message}", "Archive Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            // Do not allow archive inside search root to avoid recursive chaos
            var fullSearch = Path.GetFullPath(searchRoot);
            var fullArchive = Path.GetFullPath(archiveRoot);
            if (fullArchive.StartsWith(fullSearch, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "Archive directory cannot be inside the search directory.", "Invalid Archive",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Gather bak files to delete
            var bakFilesToDelete = new List<string>();
            foreach (var dir in Directory.EnumerateDirectories(searchRoot, "*", System.IO.SearchOption.AllDirectories).Prepend(searchRoot))
            {
                bakFilesToDelete.AddRange(
                    Directory.EnumerateFiles(dir, "*", System.IO.SearchOption.TopDirectoryOnly)
                        .Where(Helpers.IsBakFile)
                );
            }

            // Gather duplicate ACD/RSS files to archive (excluding bak)
            var results = state.Results ?? new List<SearchRow>();
            var programsByDir = results
                .Where(r =>
                    (r.ProgramFile.EndsWith(".ACD", StringComparison.OrdinalIgnoreCase) ||
                     r.ProgramFile.EndsWith(".RSS", StringComparison.OrdinalIgnoreCase)) &&
                    !Helpers.IsBakFile(r.ProgramFile)
                )
                .GroupBy(r => r.DirectoryPath ?? "", StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .ToList();

            var archiveFiles = new List<string>();
            foreach (var group in programsByDir)
            {
                var progFiles = group
                    .Select(i => Path.Combine(group.Key, Helpers.StripCountSuffix(i.ProgramFile)))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Where(File.Exists)
                    .OrderBy(p => File.GetLastWriteTime(p)) // oldest first
                    .ToList();

                if (progFiles.Count > 1)
                {
                    archiveFiles.AddRange(progFiles); // Archive all files in duplicate folders
                }
                // If there is only one file, do not archive
                // If there are zero files, do nothing
            }

            // Show confirmation dialog
            using (var confirm = new CleanupConfirmForm(bakFilesToDelete, archiveFiles))
            {
                var dr = confirm.ShowDialog(this);
                if (dr != DialogResult.OK) return;
            }

            // Delete bak files (send to recycle bin)
            int bakDeleted = 0, bakFailed = 0;
            foreach (var bakFile in bakFilesToDelete)
            {
                try
                {
                    FileSystem.DeleteFile(
                        bakFile,
                        UIOption.OnlyErrorDialogs,
                        RecycleOption.SendToRecycleBin
                    );
                    bakDeleted++;
                }
                catch { bakFailed++; }
            }

            // Archive duplicate ACD/RSS files
            int moved = 0, failed = 0;
            var errors = new System.Text.StringBuilder();
            foreach (var file in archiveFiles)
            {
                try
                {
                    string dest = Helpers.FlatArchiveDestination(archiveRoot, file);
                    File.Move(file, dest);
                    moved++;
                }
                catch (Exception ex)
                {
                    failed++;
                    errors.AppendLine($"{file} -> {ex.Message}");
                }
            }

            MessageBox.Show(this,
                $"Cleanup complete.{Environment.NewLine}Deleted bak files: {bakDeleted}, Failed: {bakFailed}" +
                $"{Environment.NewLine}Archived: {moved}, Failed: {failed}" +
                (failed > 0 ? $"{Environment.NewLine}{Environment.NewLine}Errors:{Environment.NewLine}{errors}" : ""),
                "Cleanup Results", MessageBoxButtons.OK, failed == 0 && bakFailed == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

            RunSearch(
                // Persist + rebuild tree
                settings);
        }
        // [END] CLEANUP: DETECT DUPLICATE PROGRAM FOLDERS AND MOVE OLD FILES TO FLAT ARCHIVE
        // ------------------------------------------------------------------------------------------


        // ------------------------------------------------------------------------------------------
        // [BEGIN] STATE LOAD / SAVE
        // ------------------------------------------------------------------------------------------
        private void LoadState()
        {
            try
            {
                var path = Path.Combine(
                    string.IsNullOrWhiteSpace(jsonDirectory)
                        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FileHunter")
                        : jsonDirectory,
                    "state.json"
                );
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);

                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    state = JsonSerializer.Deserialize<AppState>(json) ?? new AppState();
                    txtRoot.Text = settings.LastSearchDirectory ?? "";
                    txtArchive.Text = settings.ArchiveDirectory ?? "";
                }
            }
            catch { state = new AppState(); }
        }

        private void SaveState()
        {
            settings.LastSearchDirectory = txtRoot.Text?.Trim() ?? settings.LastSearchDirectory;
           settings.ArchiveDirectory = txtArchive.Text?.Trim() ?? settings.ArchiveDirectory;
            var path = Path.Combine(
                string.IsNullOrWhiteSpace(jsonDirectory)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FileHunter")
                    : jsonDirectory,
                "state.json"
            );
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var opts = new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(state, opts));
        }

        private void SaveJsonFile()
        {
            var saveDlg = new SaveFileDialog
            {
                Title = "Save State File",
                Filter = "JSON Files (*.json)|*.json",
                FileName = "state.json",
                InitialDirectory = jsonDirectory
            };
            if (saveDlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                var opts = new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
                string json = JsonSerializer.Serialize(state, opts);

                // Show popup with JSON content before saving
                MessageBox.Show(this, json, "State Data to be Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);

                File.WriteAllText(saveDlg.FileName, json);
                MessageBox.Show(this, $"State saved to:\n{saveDlg.FileName}", "State Save Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to save JSON:\n{ex.Message}", "State Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadJsonFile()
        {
            var openDlg = new OpenFileDialog
            {
                Title = "Load State File",
                Filter = "JSON Files (*.json)|*.json",
                InitialDirectory = jsonDirectory
            };
            if (openDlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                var json = File.ReadAllText(openDlg.FileName);
                state = JsonSerializer.Deserialize<AppState>(json) ?? new AppState();
                txtRoot.Text = settings.LastSearchDirectory ?? "";
                txtArchive.Text = settings.ArchiveDirectory ?? "";
                if (state.Results?.Any() == true)
                {
                    var ordered = state.Results.OrderBy(r => r.Location, StringComparer.OrdinalIgnoreCase)
                                       .ThenBy(r => r.Unit, StringComparer.OrdinalIgnoreCase)
                                       .ThenBy(r => r.ProgramFile, StringComparer.OrdinalIgnoreCase)
                                       .ToList();
                    PopulateGrid(ordered);
                }
                RebuildTreeFromState();
                MessageBox.Show(this, $"State loaded from:\n{openDlg.FileName}", "Load Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to load State:\n{ex.Message}", "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Update SaveSettingsOnly and LoadSettingsOnly to use file dialogs for selecting the file path

        private void SaveSettingsOnly()
        {
            var saveDlg = new SaveFileDialog
            {
                Title = "Save Settings File",
                Filter = "JSON Files (*.json)|*.json",
                FileName = "settings.json",
                InitialDirectory = jsonDirectory
            };
            if (saveDlg.ShowDialog(this) != DialogResult.OK) return;

            var settings = new AppSettings
            {
                ProgramDirectory = txtRoot.Text.Trim(),
                ArchiveDirectory = txtArchive.Text.Trim(),
                JsonDirectory = (jsonDirectory ?? "").Trim(),
                DecommissionDirectory = (settingsForm?.DecommissionDirectory ?? "").Trim()
            };
            Directory.CreateDirectory(Path.GetDirectoryName(saveDlg.FileName)!);
            var opts = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(saveDlg.FileName, System.Text.Json.JsonSerializer.Serialize(settings, opts));
            MessageBox.Show(this, $"Settings saved to:\n{saveDlg.FileName}", "Save Settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void LoadSettingsOnly()
        {
            var openDlg = new OpenFileDialog
            {
                Title = "Load Settings File",
                Filter = "JSON Files (*.json)|*.json",
                InitialDirectory = jsonDirectory
            };
            if (openDlg.ShowDialog(this) != DialogResult.OK) return;

            if (!File.Exists(openDlg.FileName))
            {
                MessageBox.Show(this, "Settings file not found.", "Load Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var json = File.ReadAllText(openDlg.FileName);
            var settings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            txtRoot.Text = settings.ProgramDirectory ?? "";
            txtArchive.Text = settings.ArchiveDirectory ?? "";
            jsonDirectory = settings.JsonDirectory ?? jsonDirectory;
            if (settingsForm != null)
                settingsForm.DecommissionDirectory = settings.DecommissionDirectory ?? settingsForm.DecommissionDirectory;
            MessageBox.Show(this, $"Settings loaded from:\n{openDlg.FileName}", "Load Settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowOldestFilesDialog()
        {
            var root = txtRoot.Text.Trim();
            var multipleMerFiles = new List<string>();
            var multipleProgramFiles = new List<string>();

            if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
            {
                var allDirs = Directory.EnumerateDirectories(root, "*", System.IO.SearchOption.AllDirectories).Prepend(root);
                foreach (var dir in allDirs)
                {
                    // Collect MER files
                    var merFiles = Directory.EnumerateFiles(dir, "*.MER", System.IO.SearchOption.TopDirectoryOnly).ToList();
                    if (merFiles.Count > 1)
                    {
                        multipleMerFiles.AddRange(merFiles);
                    }

                    // Collect program files (.ACD/.RSS, not bak)
                    var programFiles = Directory.EnumerateFiles(dir, "*.*", System.IO.SearchOption.TopDirectoryOnly)
                        .Where(p => (Helpers.HasExtension(p, ".ACD") || Helpers.HasExtension(p, ".RSS")) && !Helpers.IsBakFile(p))
                        .ToList();
                    if (programFiles.Count > 1)
                    {
                        multipleProgramFiles.AddRange(programFiles);
                    }
                }
            }

            if (multipleProgramFiles.Count == 0 && multipleMerFiles.Count == 0)
            {
                MessageBox.Show(this, "No duplicate program files or multiple MER files found. Run Search to detect duplicates.", "Multiple Files", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var dlg = new Form
            {
                Text = "Multiple Files",
                Width = 1000,
                Height = 600,
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Color.White
            };

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                ColumnCount = 1,
                RowCount = 1,
                Padding = new Padding(20),
                BackColor = Color.White
            };
            dlg.Controls.Add(mainLayout);

            void AddSectionHeader(string text, Color color)
            {
                var header = new Label
                {
                    Text = text,
                    Font = new Font("Segoe UI", 16, FontStyle.Bold),
                    ForeColor = color,
                    AutoSize = true,
                    Padding = new Padding(0, 16, 0, 8)
                };
                mainLayout.Controls.Add(header);
                mainLayout.SetColumn(header, 0);
            }

            void AddFileCard(string file, string type, Color accent, string buttonText)
            {
                var info = new FileInfo(file);
                var card = new Panel
                {
                    Width = 900,
                    Height = 100,
                    Margin = new Padding(0, 0, 0, 16),
                    BackColor = accent,
                    BorderStyle = BorderStyle.FixedSingle
                };

                var icon = new PictureBox
                {
                    Image = type == "MER" ? SystemIcons.Information.ToBitmap() : SystemIcons.Application.ToBitmap(),
                    SizeMode = PictureBoxSizeMode.StretchImage,
                    Location = new Point(12, 18),
                    Size = new Size(48, 48)
                };
                card.Controls.Add(icon);

                var nameLabel = new Label
                {
                    Text = $"{type}: {Path.GetFileName(file)}",
                    Font = new Font("Segoe UI", 12, FontStyle.Bold),
                    ForeColor = Color.Black,
                    Location = new Point(70, 12),
                    AutoSize = true
                };
                card.Controls.Add(nameLabel);

                var detailsLabel = new Label
                {
                    Text = $"Modified: {info.LastWriteTime}\nSize: {info.Length / 1024:N0} KB\nPath: {file}",
                    Font = new Font("Segoe UI", 10, FontStyle.Regular),
                    ForeColor = Color.DimGray,
                    Location = new Point(70, 38),
                    AutoSize = true,
                    MaximumSize = new Size(600, 0)
                };
                card.Controls.Add(detailsLabel);

                var btn = new Button
                {
                    Text = buttonText,
                    Font = new Font("Segoe UI", 12, FontStyle.Bold),
                    BackColor = Color.Gainsboro,
                    ForeColor = Color.Black,
                    Location = new Point(700, 30),
                    Width = 150,
                    Height = 40,
                    Tag = file
                };
                btn.Click += (s, e) =>
                {
                    string archiveRoot = txtArchive.Text.Trim();
                    if (string.IsNullOrWhiteSpace(archiveRoot) || !Directory.Exists(archiveRoot))
                    {
                        MessageBox.Show(dlg, "Select a valid archive directory first.", "Archive Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    try
                    {
                        string dest = Helpers.FlatArchiveDestination(archiveRoot, file);
                        File.Move(file, dest);
                        MessageBox.Show(dlg, $"Archived:\n{file}\n\nto\n{dest}", "Archive Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        btn.Enabled = false;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(dlg, $"Failed to archive:\n{file}\n\n{ex.Message}", "Archive Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };
                card.Controls.Add(btn);

                mainLayout.Controls.Add(card);
                mainLayout.SetColumn(card, 0);
            }

            if (multipleProgramFiles.Count > 0)
            {
                AddSectionHeader("Duplicate Program Files (.ACD/.RSS)", Color.MediumSlateBlue);
                foreach (var file in multipleProgramFiles)
                    AddFileCard(file, "Program", Color.LightGray, "Archive");
            }

            if (multipleMerFiles.Count > 0)
            {
                AddSectionHeader("Multiple MER Files in Folders", Color.Black);
                foreach (var file in multipleMerFiles)
                    AddFileCard(file, "MER", Color.LightGray, "Archive");
            }

            var closeBtn = new Button
            {
                Text = "Close",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Width = 120,
                Height = 40,
                BackColor = Color.Gainsboro,
                ForeColor = Color.Black,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            closeBtn.Click += (s, e) => dlg.Close();
            mainLayout.Controls.Add(closeBtn);

            dlg.ShowDialog(this);
        }

        private void OpenSelectedDetail()
        {
            if (lvDetails.SelectedItems.Count == 0) return;
            var item = lvDetails.SelectedItems[0];
            if (item.Text.StartsWith("[Directory]")) return;
            var filePath = item.Tag as string;
            if (string.IsNullOrEmpty(filePath)) return;

            // Find the SearchRow for this file
            var row = state.Results?.FirstOrDefault(r => 
                string.Equals(Path.Combine(r.DirectoryPath ?? "", r.ProgramFile), filePath, StringComparison.OrdinalIgnoreCase));
            if (row == null) return;

            using var dlg = new EditMetadataForm(row);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                // Update the SearchRow with new metadata
                dlg.ApplyChanges(row);
                SaveState();
                PopulateUnitDetails(new TreeTag { Kind = TreeTagKind.Unit, Location = row.Location, Unit = row.Unit });
            }
        }

        private void OpenSelectedDirectory()
        {
            if (lvDetails.SelectedItems.Count == 0) return;
            var item = lvDetails.SelectedItems[0];
            string dirPath = item.Text.StartsWith("[Directory]")
                ? item.SubItems[4].Text
                : Path.GetDirectoryName(item.Tag as string ?? string.Empty) ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(dirPath) && Directory.Exists(dirPath))
            {
                Process.Start(new ProcessStartInfo { FileName = dirPath, UseShellExecute = true });
            }
        }

        private void CopySelectedPath()
        {
            if (lvDetails.SelectedItems.Count == 0) return;
            var item = lvDetails.SelectedItems[0];
            string path = item.Text.StartsWith("[Directory]")
                ? item.SubItems[4].Text
                : item.Tag as string ?? "";
            if (!string.IsNullOrWhiteSpace(path))
                Clipboard.SetText(path);
        }

        private void ZipAllPrograms()
        {
            var root = txtRoot.Text.Trim();
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                MessageBox.Show(this, "Please choose a valid directory.", "Invalid Folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var saveDlg = new SaveFileDialog
            {
                Title = "Save Programs Zip",
                Filter = "Zip Files (*.zip)|*.zip",
                FileName = "Programs.zip",
                OverwritePrompt = true
            };
            if (saveDlg.ShowDialog(this) != DialogResult.OK) return;

            // Ensure the file is deleted before creating the zip
            if (File.Exists(saveDlg.FileName))
            {
                try
                {
                    File.Delete(saveDlg.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Unable to overwrite zip file:\n{ex.Message}", "Zip Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            var programFiles = Directory.EnumerateFiles(root, "*.*", System.IO.SearchOption.AllDirectories)
                .Where(p => (Helpers.HasExtension(p, ".ACD") || Helpers.HasExtension(p, ".RSS")) && !Helpers.IsBakFile(p))
                .ToList();

            // Progress bar dialog
            using var progressForm = new Form
            {
                Text = "Trying to make it all fit...",
                Width = 400,
                Height = 120,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                ControlBox = false
            };
            var progressBar = new ProgressBar
            {
                Dock = DockStyle.Top,
                Minimum = 0,
                Maximum = programFiles.Count,
                Value = 0,
                Height = 32,
                Style = ProgressBarStyle.Continuous
            };
            var lbl = new Label
            {
                Dock = DockStyle.Top,
                Text = "Zipping files...",
                TextAlign = ContentAlignment.MiddleCenter,
                Height = 32
            };
            progressForm.Controls.Add(lbl);
            progressForm.Controls.Add(progressBar);

            progressForm.Load += (s, e) =>
            {
                // Center relative to MainForm
                progressForm.Location = new Point(
                    this.Location.X + (this.Width - progressForm.Width) / 2,
                    this.Location.Y + (this.Height - progressForm.Height) / 2
                );
            };
            progressForm.Show();

            try
            {
                using (var zip = ZipFile.Open(saveDlg.FileName, ZipArchiveMode.Create))
                {
                    int i = 0;
                    foreach (var file in programFiles)
                    {
                        var entryName = Path.GetFileName(file);
                        zip.CreateEntryFromFile(file, entryName);
                        i++;
                        progressBar.Value = i;
                        lbl.Text = $"Zipping {i} of {programFiles.Count} files...";
                        Application.DoEvents();
                    }
                }
                progressForm.Close();
                MessageBox.Show(this, $"Programs zipped to:\n{saveDlg.FileName}", "Zip Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                progressForm.Close();
                MessageBox.Show(this, $"Failed to zip programs:\n{ex.Message}", "Zip Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ZipAllMERs()
        {
            var root = txtRoot.Text.Trim();
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                MessageBox.Show(this, "Please choose a valid directory.", "Invalid Folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var saveDlg = new SaveFileDialog
            {
                Title = "Save MERs Zip",
                Filter = "Zip Files (*.zip)|*.zip",
                FileName = "MERs.zip",
                OverwritePrompt = true
            };
            if (saveDlg.ShowDialog(this) != DialogResult.OK) return;

            // Ensure the file is deleted before creating the zip
            if (File.Exists(saveDlg.FileName))
            {
                try
                {
                    File.Delete(saveDlg.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Unable to overwrite zip file:\n{ex.Message}", "Zip Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            var merFiles = Directory.EnumerateFiles(root, "*.MER", System.IO.SearchOption.AllDirectories).ToList();

            // Progress bar dialog
            using var progressForm = new Form
            {
                Text = "Zipping MERs...",
                Width = 400,
                Height = 120,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                ControlBox = false
            };
            var progressBar = new ProgressBar
            {
                Dock = DockStyle.Top,
                Minimum = 0,
                Maximum = merFiles.Count,
                Value = 0,
                Height = 32,
                Style = ProgressBarStyle.Continuous
            };
            var lbl = new Label
            {
                Dock = DockStyle.Top,
                Text = "Zipping files...",
                TextAlign = ContentAlignment.MiddleCenter,
                Height = 32
            };
            progressForm.Controls.Add(lbl);
            progressForm.Controls.Add(progressBar);

            progressForm.Load += (s, e) =>
            {
                // Center relative to MainForm
                progressForm.Location = new Point(
                    this.Location.X + (this.Width - progressForm.Width) / 2,
                    this.Location.Y + (this.Height - progressForm.Height) / 2
                );
            };
            progressForm.Show();

            try
            {
                using (var zip = ZipFile.Open(saveDlg.FileName, ZipArchiveMode.Create))
                {
                    int i = 0;
                    foreach (var file in merFiles)
                    {
                        var entryName = Path.GetFileName(file);
                        zip.CreateEntryFromFile(file, entryName);
                        i++;
                        progressBar.Value = i;
                        lbl.Text = $"Zipping {i} of {merFiles.Count} files...";
                        Application.DoEvents();
                    }
                }
                progressForm.Close();
                MessageBox.Show(this, $"MERs zipped to:\n{saveDlg.FileName}", "Zip Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                progressForm.Close();
                MessageBox.Show(this, $"Failed to zip MERs:\n{ex.Message}", "Zip Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string GetNoteKey(string location, string unit)
        {
            return $"{location}::{unit}";
        }

        private void SaveCurrentNote()
        {
            if (txtNotes == null || navTree == null || state == null) return;

            var selectedNode = navTree.SelectedNode;
            var tag = selectedNode?.Tag as TreeTag;
            if (tag == null || tag.Kind != TreeTagKind.Unit) return;

            var key = GetNoteKey(tag.Location ?? "", tag.Unit ?? "");
            if (state.Notes == null)
                state.Notes = new Dictionary<string, string>();

            state.Notes[key] = txtNotes.Text ?? "";
            SaveState();
            MessageBox.Show(this, "Note saved.", "Save Note", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ArchiveSelectedFile()
        {
            if (lvDetails == null || lvDetails.SelectedItems.Count == 0) return;
            var item = lvDetails.SelectedItems[0];
            if (item.Text.StartsWith("[Directory]")) return;
            var filePath = item.Tag as string;
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                MessageBox.Show(this, "File not found.", "Archive Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string archiveRoot = txtArchive?.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(archiveRoot) || !Directory.Exists(archiveRoot))
            {
                MessageBox.Show(this, "Select a valid archive directory first.", "Archive Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                string dest = Helpers.FlatArchiveDestination(archiveRoot, filePath);
                File.Move(filePath, dest);
                MessageBox.Show(this, $"Archived:\n{filePath}\n\nto\n{dest}", "Archive Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                item.ForeColor = Color.Gray;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to archive:\n{filePath}\n\n{ex.Message}", "Archive Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private SearchRow CreateRowForFile(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath) ?? "";
            var ext = Path.GetExtension(filePath).ToUpperInvariant();
            var info = new FileInfo(filePath);

            var row = new SearchRow
            {
                DirectoryPath = dir,
                Location = "", // Optionally fill from ExtractLocationUnit if needed
                Unit = "",
                Quarter = "",
                ProgramCountInDir = 1
            };

            if (ext == ".MER")
            {
                row.QuickPanelFile = Path.GetFileName(filePath);
                row.QuickPanelFileModified = info.Exists ? info.LastWriteTime : (DateTime?)null;
            }
            else // .ACD or .RSS
            {
                row.ProgramFile = Path.GetFileName(filePath);
                row.ProgramFileModified = info.Exists ? info.LastWriteTime : DateTime.MinValue;
            }

            return row;
        }

        private void EditSelectedFileProperties()
        {
            if (lvDetails == null || lvDetails.SelectedItems.Count == 0) return;
            var item = lvDetails.SelectedItems[0];
            if (item.Text.StartsWith("[Directory]")) return;
            var filePath = item.Tag as string;
            if (string.IsNullOrEmpty(filePath)) return;

            var row = CreateRowForFile(filePath);

            using var dlg = new FilePropertiesForm(filePath, row);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                dlg.ApplyChanges(row);
                SaveState();
                PopulateUnitDetails(new TreeTag { Kind = TreeTagKind.Unit, Location = row.Location, Unit = row.Unit });
            }
        }

        private void OpenSelectedFileProperties()
        {
            if (lvDetails == null || lvDetails.SelectedItems.Count == 0) return;
            var item = lvDetails.SelectedItems[0];
            if (item.Text.StartsWith("[Directory]")) return;
            var filePath = item.Tag as string;
            if (string.IsNullOrEmpty(filePath)) return;

            var row = CreateRowForFile(filePath);

            using var dlg = new FilePropertiesForm(filePath, row);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                dlg.ApplyChanges(row);
                SaveState();
                PopulateUnitDetails(new TreeTag { Kind = TreeTagKind.Unit, Location = row.Location, Unit = row.Unit });
            }
        }

        private void ShowNewUnitDialog()
        {
            // Get existing locations from state
            var locations = state.Results?.Select(r => r.Location ?? "")
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(l => l, StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            using var dlg = new NewUnitDialog(locations);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                string root = txtRoot?.Text?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                {
                    MessageBox.Show(this, "Please choose a valid Program Directory first.", "Invalid Folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                string location = dlg.SelectedLocation;
                foreach (var unit in dlg.UnitNames)
                {
                    string unitPath = Path.Combine(root, location, unit);
                    Directory.CreateDirectory(unitPath);
                    Directory.CreateDirectory(Path.Combine(unitPath, "2 - Archive"));
                    Directory.CreateDirectory(Path.Combine(unitPath, "5 - PLC Program"));
                    Directory.CreateDirectory(Path.Combine(unitPath, "6 - Flow Computer Program"));
                    Directory.CreateDirectory(Path.Combine(unitPath, "7 - Auxiliary Equip. Programs"));
                }
                MessageBox.Show(this, "Folder structure created.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                RunSearch(
                // Persist + rebuild tree
                settings);
            }
        }
    }
}
using System.Text.Json; // Add this if not present
using System.Windows.Forms;
using System.Drawing;
using System.IO;

public class SettingsForm : Form
{
    public string ProgramDirectory = string.Empty;
    public string ArchiveDirectory = string.Empty;
    public string JsonDirectory = string.Empty; // Add this
    public string DecommissionDirectory = string.Empty; // Add this

    private TextBox txtRoot;
    private Button btnBrowse;
    private TextBox txtArchive;
    private Button btnBrowseArchive;
    private TextBox txtJson; // Add this
    private Button btnBrowseJson; // Add this
    private TextBox txtDecommission;
    private Button btnBrowseDecommission;
    private Button btnOK;
    private Button btnCancel;

    public SettingsForm(string initialRoot, string initialArchive, string initialJson)
    {
        Text = "Settings";
        Width = 500;
        Height = 220;
        StartPosition = FormStartPosition.CenterParent;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 5,
            Padding = new Padding(12),
            AutoSize = true,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

        var lblRoot = new Label { Text = "Program Directory:", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill };
        txtRoot = new TextBox { Text = initialRoot, Dock = DockStyle.Fill };
        btnBrowse = new Button { Text = "Browse...", Dock = DockStyle.Fill };
        btnBrowse.Click += (s, e) =>
        {
            using var dlg = new FolderBrowserDialog();
            if (dlg.ShowDialog(this) == DialogResult.OK) txtRoot.Text = dlg.SelectedPath;
        };

        var lblArchive = new Label { Text = "Archive Directory:", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill };
        txtArchive = new TextBox { Text = initialArchive, Dock = DockStyle.Fill };
        btnBrowseArchive = new Button { Text = "Browse...", Dock = DockStyle.Fill };
        btnBrowseArchive.Click += (s, e) =>
        {
            using var dlg = new FolderBrowserDialog();
            if (dlg.ShowDialog(this) == DialogResult.OK) txtArchive.Text = dlg.SelectedPath;
        };

        var lblJson = new Label { Text = "Results Directory:", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill };
        txtJson = new TextBox { Text = initialJson, Dock = DockStyle.Fill };
        btnBrowseJson = new Button { Text = "Browse...", Dock = DockStyle.Fill };
        btnBrowseJson.Click += (s, e) =>
        {
            using var dlg = new FolderBrowserDialog { Description = "Select a directory for JSON files.", ShowNewFolderButton = true };
            if (Directory.Exists(txtJson.Text)) dlg.SelectedPath = txtJson.Text;
            if (dlg.ShowDialog(this) == DialogResult.OK) txtJson.Text = dlg.SelectedPath;
        };

        var lblDecommission = new Label { Text = "Decommission Folder:", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill };
        txtDecommission = new TextBox { Text = "", Dock = DockStyle.Fill };
        btnBrowseDecommission = new Button { Text = "Browse...", Dock = DockStyle.Fill };
        btnBrowseDecommission.Click += (s, e) =>
        {
            using var dlg = new FolderBrowserDialog { Description = "Select a folder for decommissioned units.", ShowNewFolderButton = true };
            if (Directory.Exists(txtDecommission.Text)) dlg.SelectedPath = txtDecommission.Text;
            if (dlg.ShowDialog(this) == DialogResult.OK) txtDecommission.Text = dlg.SelectedPath;
        };

        btnOK = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 80, Anchor = AnchorStyles.Right };
        btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 80, Anchor = AnchorStyles.Right };

        layout.Controls.Add(lblRoot, 0, 0);
        layout.Controls.Add(txtRoot, 1, 0);
        layout.Controls.Add(btnBrowse, 2, 0);

        layout.Controls.Add(lblArchive, 0, 1);
        layout.Controls.Add(txtArchive, 1, 1);
        layout.Controls.Add(btnBrowseArchive, 2, 1);

        layout.Controls.Add(lblJson, 0, 2);
        layout.Controls.Add(txtJson, 1, 2);
        layout.Controls.Add(btnBrowseJson, 2, 2);

        layout.Controls.Add(lblDecommission, 0, 3);
        layout.Controls.Add(txtDecommission, 1, 3);
        layout.Controls.Add(btnBrowseDecommission, 2, 3);

        var btnPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, AutoSize = true };
        btnPanel.Controls.Add(btnCancel);
        btnPanel.Controls.Add(btnOK);
        layout.SetColumnSpan(btnPanel, 3);
        layout.Controls.Add(btnPanel, 0, 4);

        Controls.Add(layout);

        AcceptButton = btnOK;
        CancelButton = btnCancel;

        btnOK.Click += (s, e) =>
        {
            ProgramDirectory = txtRoot.Text.Trim();
            ArchiveDirectory = txtArchive.Text.Trim();
            JsonDirectory = txtJson.Text.Trim();
            DecommissionDirectory = txtDecommission.Text.Trim();
            DialogResult = DialogResult.OK;

            // Prepare settings object for serialization
            var settings = new
            {
                ProgramDirectory,
                ArchiveDirectory,
                JsonDirectory,
                DecommissionDirectory
            };

            // Serialize to JSON (debug info)
            string jsonSettings = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });

            // Show debug info in message box
            MessageBox.Show(this, $"Settings to be written to JSON:\n\n{jsonSettings}...{initialJson}", "Settings Debug Info", MessageBoxButtons.OK, MessageBoxIcon.Information);

            // You may want to actually write to file here, e.g.:
            // File.WriteAllText(Path.Combine(JsonDirectory, "settings.json"), jsonSettings);

            Close();
        };
    }
}
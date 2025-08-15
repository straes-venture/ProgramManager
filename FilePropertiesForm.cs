using System;
using System.IO;
using System.Windows.Forms;
using System.Drawing;

namespace FileHunter
{
    public class FilePropertiesForm : Form
    {
        private TextBox txtTitle;
        private DateTimePicker dtModified;
        private DateTimePicker dtCreated;
        private CheckBox chkReadOnly;
        private CheckBox chkHidden;
        private TextBox txtFileName;
        private Label lblFileName;
        private Button btnOK;
        private Button btnCancel;

        private string filePath;
        private SearchRow row;

        public FilePropertiesForm(string filePath, SearchRow row)
        {
            this.filePath = filePath;
            this.row = row;

            Text = "Edit File Properties";
            Width = 420;
            Height = 340;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var info = new FileInfo(filePath);

            lblFileName = new Label { Text = $"File: {info.Name}", Left = 20, Top = 20, Width = 340 };

            var lblNewFileName = new Label { Text = "File Name:", Left = 20, Top = 60, Width = 100 };
            txtFileName = new TextBox { Left = 130, Top = 60, Width = 220, Text = info.Name };

            var lblTitle = new Label { Text = "Title:", Left = 20, Top = 100, Width = 100 };
            txtTitle = new TextBox { Left = 130, Top = 100, Width = 220, Text = row.ProgramFile };

            var lblCreated = new Label { Text = "Created Date:", Left = 20, Top = 140, Width = 100 };
            dtCreated = new DateTimePicker { Left = 130, Top = 140, Width = 220, Value = info.Exists ? info.CreationTime : DateTime.Now };

            var lblModified = new Label { Text = "Modified Date:", Left = 20, Top = 180, Width = 100 };
            dtModified = new DateTimePicker { Left = 130, Top = 180, Width = 220, Value = info.Exists ? info.LastWriteTime : DateTime.Now };

            chkReadOnly = new CheckBox { Text = "Read-only", Left = 130, Top = 220, Width = 100, Checked = info.Exists && info.IsReadOnly };
            chkHidden = new CheckBox { Text = "Hidden", Left = 240, Top = 220, Width = 100, Checked = info.Exists && (info.Attributes & FileAttributes.Hidden) != 0 };

            btnOK = new Button { Text = "OK", Left = 130, Top = 260, Width = 80, DialogResult = DialogResult.OK };
            btnCancel = new Button { Text = "Cancel", Left = 220, Top = 260, Width = 80, DialogResult = DialogResult.Cancel };

            Controls.AddRange(new Control[] {
                lblFileName,
                lblNewFileName, txtFileName,
                lblTitle, txtTitle,
                lblCreated, dtCreated,
                lblModified, dtModified,
                chkReadOnly, chkHidden,
                btnOK, btnCancel
            });

            AcceptButton = btnOK;
            CancelButton = btnCancel;
        }

        public void ApplyChanges(SearchRow row)
        {
            // Update title in SearchRow
            row.ProgramFile = txtTitle.Text;

            // Update file name if changed
            var info = new FileInfo(filePath);
            string newFileName = txtFileName.Text.Trim();
            string newFilePath = Path.Combine(info.DirectoryName ?? "", newFileName);
            if (info.Exists && !string.Equals(info.Name, newFileName, StringComparison.OrdinalIgnoreCase))
            {
                info.MoveTo(newFilePath);
                filePath = newFilePath;
            }

            // Update created and modified dates
            if (File.Exists(filePath))
            {
                File.SetCreationTime(filePath, dtCreated.Value);
                File.SetLastWriteTime(filePath, dtModified.Value);

                // Update read-only and hidden attributes
                var attributes = File.GetAttributes(filePath);
                if (chkReadOnly.Checked)
                    attributes |= FileAttributes.ReadOnly;
                else
                    attributes &= ~FileAttributes.ReadOnly;

                if (chkHidden.Checked)
                    attributes |= FileAttributes.Hidden;
                else
                    attributes &= ~FileAttributes.Hidden;

                File.SetAttributes(filePath, attributes);
            }

            // Update SearchRow modified date
            row.ProgramFileModified = dtModified.Value;
        }
    }
}
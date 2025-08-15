using System;
using System.Windows.Forms;
using System.Drawing;

namespace FileHunter
{
    public class EditMetadataForm : Form
    {
        private TextBox txtLocation;
        private TextBox txtUnit;
        private TextBox txtProgramFile;
        private TextBox txtQuarter;
        private TextBox txtDirectoryPath;
        private NumericUpDown numProgramCount;
        private Button btnOK;
        private Button btnCancel;

        private SearchRow row;

        public EditMetadataForm(SearchRow row)
        {
            this.row = row;
            Text = "Edit Metadata";
            Width = 400;
            Height = 350;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var lblLocation = new Label { Text = "Location:", Left = 20, Top = 20, Width = 100 };
            txtLocation = new TextBox { Left = 130, Top = 20, Width = 220, Text = row.Location };

            var lblUnit = new Label { Text = "Unit:", Left = 20, Top = 60, Width = 100 };
            txtUnit = new TextBox { Left = 130, Top = 60, Width = 220, Text = row.Unit };

            var lblProgramFile = new Label { Text = "Program File:", Left = 20, Top = 100, Width = 100 };
            txtProgramFile = new TextBox { Left = 130, Top = 100, Width = 220, Text = row.ProgramFile };

            var lblQuarter = new Label { Text = "Quarter:", Left = 20, Top = 140, Width = 100 };
            txtQuarter = new TextBox { Left = 130, Top = 140, Width = 220, Text = row.Quarter };

            var lblDirectoryPath = new Label { Text = "Directory Path:", Left = 20, Top = 180, Width = 100 };
            txtDirectoryPath = new TextBox { Left = 130, Top = 180, Width = 220, Text = row.DirectoryPath ?? "" };

            var lblProgramCount = new Label { Text = "Program Count:", Left = 20, Top = 220, Width = 100 };
            numProgramCount = new NumericUpDown { Left = 130, Top = 220, Width = 80, Value = row.ProgramCountInDir, Minimum = 0, Maximum = 100 };

            btnOK = new Button { Text = "OK", Left = 130, Top = 260, Width = 80, DialogResult = DialogResult.OK };
            btnCancel = new Button { Text = "Cancel", Left = 220, Top = 260, Width = 80, DialogResult = DialogResult.Cancel };

            Controls.AddRange(new Control[] {
                lblLocation, txtLocation,
                lblUnit, txtUnit,
                lblProgramFile, txtProgramFile,
                lblQuarter, txtQuarter,
                lblDirectoryPath, txtDirectoryPath,
                lblProgramCount, numProgramCount,
                btnOK, btnCancel
            });

            AcceptButton = btnOK;
            CancelButton = btnCancel;
        }

        public void ApplyChanges(SearchRow row)
        {
            row.Location = txtLocation.Text;
            row.Unit = txtUnit.Text;
            row.ProgramFile = txtProgramFile.Text;
            row.Quarter = txtQuarter.Text;
            row.DirectoryPath = txtDirectoryPath.Text;
            row.ProgramCountInDir = (int)numProgramCount.Value;
        }
    }
}
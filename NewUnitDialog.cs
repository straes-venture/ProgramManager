using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace FileHunter
{
    public class NewUnitDialog : Form
    {
        public string SelectedLocation { get; private set; } = "";
        public List<string> UnitNames { get; private set; } = new();

        private ComboBox cmbLocation;
        private TextBox txtNewLocation;
        private NumericUpDown numUnits;
        private Panel unitsPanel;
        private Button btnCreate;
        private Button btnCancel;

        public NewUnitDialog(IEnumerable<string> existingLocations)
        {
            Text = "Create New Unit(s)";
            Width = 500;
            Height = 420;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var lblLocation = new Label { Text = "Select Location:", Left = 20, Top = 20, Width = 120 };
            cmbLocation = new ComboBox { Left = 150, Top = 18, Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbLocation.Items.Add("Select Existing Location or Add New");
            cmbLocation.Items.Add("<New Location>");
            cmbLocation.Items.AddRange(existingLocations.ToArray());
            cmbLocation.SelectedIndex = 0;

            txtNewLocation = new TextBox { Left = 150, Top = 58, Width = 220, Visible = false };
            var lblNewLocation = new Label { Text = "New Location:", Left = 20, Top = 60, Width = 120, Visible = false };

            cmbLocation.SelectedIndexChanged += (s, e) =>
            {
                bool isNew = cmbLocation.SelectedItem?.ToString() == "<New Location>";
                txtNewLocation.Visible = lblNewLocation.Visible = isNew;
            };

            var lblNumUnits = new Label { Text = "Number of Units:", Left = 20, Top = 100, Width = 120 };
            numUnits = new NumericUpDown { Left = 150, Top = 100, Width = 80, Minimum = 1, Maximum = 20, Value = 1 };

            unitsPanel = new Panel { Left = 20, Top = 140, Width = 440, Height = 180, AutoScroll = true, BorderStyle = BorderStyle.FixedSingle };

            void UpdateUnitFields()
            {
                unitsPanel.Controls.Clear();
                for (int i = 0; i < numUnits.Value; i++)
                {
                    var lbl = new Label { Text = $"Unit {i + 1} Name:", Left = 8, Top = 8 + i * 32, Width = 100 };
                    var txt = new TextBox { Left = 120, Top = 8 + i * 31, Width = 280, Name = $"txtUnit{i}" };
                    unitsPanel.Controls.Add(lbl);
                    unitsPanel.Controls.Add(txt);
                }
            }
            numUnits.ValueChanged += (s, e) => UpdateUnitFields();
            UpdateUnitFields();

            btnCreate = new Button { Text = "Create Folder Structure", Left = 120, Top = 350, Width = 180, DialogResult = DialogResult.OK };
            btnCancel = new Button { Text = "Cancel", Left = 320, Top = 350, Width = 80, DialogResult = DialogResult.Cancel };

            Controls.AddRange(new Control[] {
                lblLocation, cmbLocation,
                lblNewLocation, txtNewLocation,
                lblNumUnits, numUnits,
                unitsPanel,
                btnCreate, btnCancel
            });

            AcceptButton = btnCreate;
            CancelButton = btnCancel;

            btnCreate.Click += (s, e) =>
            {
                SelectedLocation = cmbLocation.SelectedItem?.ToString() == "<New Location>"
                    ? txtNewLocation.Text.Trim()
                    : cmbLocation.SelectedItem?.ToString() ?? "";
                UnitNames = unitsPanel.Controls.OfType<TextBox>().Select(t => t.Text.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToList();
                if (string.IsNullOrWhiteSpace(SelectedLocation) || UnitNames.Count == 0)
                {
                    MessageBox.Show(this, "Please enter a location and at least one unit name.", "Input Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None;
                }
            };
        }
    }
}
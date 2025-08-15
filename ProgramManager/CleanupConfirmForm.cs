using System;
using System.Windows.Forms;
using System.Collections.Generic;

namespace FileHunter
{
    public class CleanupConfirmForm : Form
    {
        public CleanupConfirmForm(List<string> bakFiles, List<string> archiveFiles)
        {
            Text = "Confirm Cleanup";
            Width = 800;
            Height = 600;
            StartPosition = FormStartPosition.CenterParent;

            var label = new Label
            {
                Text = "The following files will be deleted (bak) or archived (ACD/RSS):",
                Dock = DockStyle.Top,
                Padding = new Padding(8),
                AutoSize = true
            };

            var listBox = new ListBox
            {
                Dock = DockStyle.Fill,
                HorizontalScrollbar = true
            };

            if (bakFiles.Count > 0)
            {
                listBox.Items.Add("=== Files to be deleted (bak) ===");
                foreach (var file in bakFiles) listBox.Items.Add(file);
            }
            if (archiveFiles.Count > 0)
            {
                listBox.Items.Add("=== Files to be archived (ACD/RSS) ===");
                foreach (var file in archiveFiles) listBox.Items.Add(file);
            }

            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(8),
                Height = 48
            };

            var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 100 };
            var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 100 };
            panel.Controls.Add(btnOk);
            panel.Controls.Add(btnCancel);

            Controls.Add(listBox);
            Controls.Add(label);
            Controls.Add(panel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }
    }
}
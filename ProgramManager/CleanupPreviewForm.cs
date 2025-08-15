// ==============================================================================================
// CleanupPreviewForm.cs
// ==============================================================================================
// PURPOSE:
//   - Modal preview listing all files that will be moved to the Archive directory during cleanup.
//   - Shows Source Directory, File Name, Destination (flat archive), and Modified time.
//   - Lets the user confirm before any filesystem changes occur.
// ==============================================================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace FileHunter
{
    public class CleanupPreviewForm : Form
    {
        private readonly ListView _list;
        private readonly Button _btnOk;
        private readonly Button _btnCancel;

        // moves: list of (sourceFullPath, destinationFullPath)
        public CleanupPreviewForm(List<(string source, string dest)> moves)
        {
            Text = "Cleanup Preview (Archive Flat)";
            Width = 1000;
            Height = 560;
            StartPosition = FormStartPosition.CenterParent;

            _list = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };
            _list.Columns.Add("Source Directory", 360);
            _list.Columns.Add("File Name", 260);
            _list.Columns.Add("Modified", 140);
            _list.Columns.Add("Destination (Archive)", 400);

            foreach (var grp in moves.GroupBy(m => Path.GetDirectoryName(m.source) ?? "", StringComparer.OrdinalIgnoreCase)
                                     .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                var lvGroup = new ListViewGroup(grp.Key, HorizontalAlignment.Left);
                _list.Groups.Add(lvGroup);

                foreach (var m in grp.OrderBy(x => Path.GetFileName(x.source), StringComparer.OrdinalIgnoreCase))
                {
                    DateTime mod = DateTime.MinValue;
                    if (File.Exists(m.source))
                    {
                        try { mod = File.GetLastWriteTime(m.source); } catch { }
                    }
                    var item = new ListViewItem(new[]
                    {
                        Path.GetDirectoryName(m.source) ?? "",
                        Path.GetFileName(m.source),
                        mod == DateTime.MinValue ? "" : mod.ToString(),
                        m.dest
                    }, lvGroup);
                    _list.Items.Add(item);
                }
            }

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(8) };
            _btnOk = new Button { Text = "Move Files", DialogResult = DialogResult.OK, Width = 120, Height = 30, Anchor = AnchorStyles.Right | AnchorStyles.Bottom };
            _btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 100, Height = 30, Anchor = AnchorStyles.Right | AnchorStyles.Bottom };

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 240,
                FlowDirection = FlowDirection.RightToLeft
            };
            flow.Controls.Add(_btnOk);
            flow.Controls.Add(_btnCancel);

            bottom.Controls.Add(flow);

            Controls.Add(_list);
            Controls.Add(bottom);

            AcceptButton = _btnOk;
            CancelButton = _btnCancel;
        }
    }
}

using System.Windows.Forms;

namespace FileHunter.Forms
{
    public class SettingsForm : Form
    {
        public string ProgramDirectory = string.Empty;
        public string ArchiveDirectory = string.Empty;
        public string JsonDirectory = string.Empty;

        private TextBox txtRoot;
        private Button btnBrowse;
        private TextBox txtArchive;
        private Button btnBrowseArchive;
        private TextBox txtJson;
        private Button btnBrowseJson;
        private Button btnOK;
        private Button btnCancel;

        public SettingsForm(string initialRoot, string initialArchive, string initialJson)
        {
            // Build UI and wire up events here
        }
    }
}
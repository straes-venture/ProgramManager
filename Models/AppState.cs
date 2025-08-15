using System.Collections.Generic;

namespace FileHunter.Models
{
    public class AppState
    {
        public string? LastSearchDirectory { get; set; }
        public string? ArchiveDirectory { get; set; }
        public List<SearchRow>? Results { get; set; } = new();
        public Dictionary<string, string>? Notes { get; set; } = new();
    }

    public class SearchRow
    {
        public string Location { get; set; } = "";
        public string Unit { get; set; } = "";
        public string ProgramFile { get; set; } = "";
        public DateTime ProgramFileModified { get; set; }
        public string QuickPanelFile { get; set; } = "";
        public DateTime? QuickPanelFileModified { get; set; }
        public string Quarter { get; set; } = "";
        // Support for details pane
        public string? DirectoryPath { get; set; }
        public int ProgramCountInDir { get; set; }
    }   
}
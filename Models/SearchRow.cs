using System;

namespace FileHunter.Models
{
    public class SearchRow
    {
        public string Location { get; set; } = "";
        public string Unit { get; set; } = "";
        public string ProgramFile { get; set; } = "";
        public DateTime ProgramFileModified { get; set; }
        public string QuickPanelFile { get; set; } = "";
        public DateTime? QuickPanelFileModified { get; set; }
        public string Quarter { get; set; } = "";
        public string? DirectoryPath { get; set; } = "";
        public int ProgramCountInDir { get; set; } = 1;
    }
}
// ==============================================================================================
// Models.cs
// ==============================================================================================
// PURPOSE:
//   - Data contracts and enums used by the application.
// ==============================================================================================

using System;
using System.Collections.Generic;

namespace FileHunter
{
    // ==============================================================================================
    // [BEGIN] DATA CONTRACTS / MODELS
    // ----------------------------------------------------------------------------------------------
    public class AppState
    {
        public List<SearchRow>? Results { get; set; } = new();
        public Dictionary<string, string>? Notes { get; set; } = new();
        public int ResultsCount => Results?.Count ?? 0;
        public bool HasNotes => Notes != null && Notes.Count > 0;
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
        public string? DirectoryPath { get; set; } = "";
        public int ProgramCountInDir { get; set; } = 1;
    }

    public enum TreeTagKind { All, Location, Unit }

    public class TreeTag
    {
        public TreeTagKind Kind { get; set; }
        public string? Location { get; set; }
        public string? Unit { get; set; }
    }

    public class AppSettings
    {
        public string? LastSearchDirectory { get; set; }
        public string? ProgramDirectory { get; set; }
        public string? ArchiveDirectory { get; set; }
        public string? JsonDirectory { get; set; }
        public string? DecommissionDirectory { get; set; }
    }
}

    // Add other small helper classes/enums here as needed
    // [END] DATA CONTRACTS / MODELS
    // ==============================================================================================



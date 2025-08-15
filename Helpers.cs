// ==============================================================================================
// Helpers.cs
// ==============================================================================================
// PURPOSE:
//   - Shared helper/utility methods used across the app.
//   - Extra-verbose comments and explicit sections for clarity.
// ==============================================================================================

using System;
using System.IO;

namespace FileHunter
{
    internal static class Helpers
    {
        // ------------------------------------------------------------------------------------------
        // [BEGIN] GENERAL FILE HELPERS
        // ------------------------------------------------------------------------------------------
        public static bool HasExtension(string path, string ext)
            => string.Equals(Path.GetExtension(path), ext, StringComparison.OrdinalIgnoreCase);

        // Identifies if the file has "bak" anywhere in its filename (case-insensitive)
        public static bool IsBakFile(string path)
        {
            var fileName = Path.GetFileName(path);
            return fileName != null && fileName.IndexOf("bak", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static string ToQuarter(DateTime dt)
        {
            // Quarter (1..4) formatted as "YY-Q#"
            int q = ((dt.Month - 1) / 3) + 1;
            string yy = (dt.Year % 100).ToString("D2");
            return $"{yy}-Q{q}";
        }

        // Extract Location/Unit from a relative directory path "Loc/Unit/Deeper/...":
        //   - Location = first segment (if any)
        //   - Unit     = second segment (if any)
        public static (string location, string unit) ExtractLocationUnit(string relativeDir)
        {
            if (string.IsNullOrEmpty(relativeDir)) return ("", "");
            var parts = relativeDir.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            string location = parts.Length >= 1 ? parts[0] : "";
            string unit = parts.Length >= 2 ? parts[1] : "";
            return (location, unit);
        }

        // Portable relative path wrapper. Ensures trailing slash for base so GetRelativePath is correct.
        public static string GetRelativePathPortable(string basePath, string path)
            => Path.GetRelativePath(EnsureTrailingSlash(basePath), path);

        public static string EnsureTrailingSlash(string p)
        {
            if (string.IsNullOrEmpty(p)) return p;
            if (!p.EndsWith(Path.DirectorySeparatorChar) && !p.EndsWith(Path.AltDirectorySeparatorChar))
                return p + Path.DirectorySeparatorChar;
            return p;
        }

        // SafeFileSizeKB: get file size in KB; returns 0 on any failure
        public static long SafeFileSizeKB(string fullPath)
        {
            try
            {
                var fi = new FileInfo(fullPath);
                return fi.Exists ? Math.Max(1, fi.Length / 1024) : 0;
            }
            catch { return 0; }
        }

        // When ProgramFile is displayed with a suffix " [N in folder]", strip it back to the real name
        public static string StripCountSuffix(string programFileDisplay)
        {
            int idx = programFileDisplay.LastIndexOf(" [");
            if (idx > 0 && programFileDisplay.EndsWith(" in folder]", StringComparison.OrdinalIgnoreCase))
                return programFileDisplay.Substring(0, idx);
            return programFileDisplay;
        }
        // [END] GENERAL FILE HELPERS
        // ------------------------------------------------------------------------------------------

        // ------------------------------------------------------------------------------------------
        // [BEGIN] ARCHIVE HELPERS (FLAT DESTINATION)
        // ------------------------------------------------------------------------------------------
        // Make a unique name in a target directory by appending " (n)" before the extension if needed.
        public static string MakeUniqueNameInDirectory(string directory, string fileName)
        {
            string candidate = Path.Combine(directory, fileName);
            if (!File.Exists(candidate)) return candidate;

            string name = Path.GetFileNameWithoutExtension(fileName);
            string ext = Path.GetExtension(fileName);
            int i = 2;
            while (true)
            {
                string next = Path.Combine(directory, $"{name} ({i}){ext}");
                if (!File.Exists(next)) return next;
                i++;
            }
        }

        // Compute the destination path inside a FLAT archive root (no subfolders).
        public static string FlatArchiveDestination(string archiveRoot, string fullSourcePath)
        {
            Directory.CreateDirectory(archiveRoot);
            string? fileName = Path.GetFileName(fullSourcePath);
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentException("Source path does not contain a valid file name.", nameof(fullSourcePath));
            return MakeUniqueNameInDirectory(archiveRoot, fileName);
        }
        // [END] ARCHIVE HELPERS (FLAT DESTINATION)
        // ------------------------------------------------------------------------------------------
    }
}

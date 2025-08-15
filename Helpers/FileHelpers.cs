using System;
using System.IO;

namespace FileHunter.Helpers
{
    public static class FileHelpers
    {
        public static bool HasExtension(string path, string ext)
            => string.Equals(Path.GetExtension(path), ext, StringComparison.OrdinalIgnoreCase);

        public static bool IsBakFile(string path)
        {
            var fileName = Path.GetFileName(path);
            return fileName != null && fileName.IndexOf("bak", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static string ToQuarter(DateTime dt)
        {
            int q = ((dt.Month - 1) / 3) + 1;
            string yy = (dt.Year % 100).ToString("D2");
            return $"{yy}-Q{q}";
        }

        public static (string location, string unit) ExtractLocationUnit(string relativeDir)
        {
            if (string.IsNullOrEmpty(relativeDir)) return ("", "");
            var parts = relativeDir.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            string location = parts.Length >= 1 ? parts[0] : "";
            string unit = parts.Length >= 2 ? parts[1] : "";
            return (location, unit);
        }

        public static string GetRelativePathPortable(string basePath, string path)
            => Path.GetRelativePath(EnsureTrailingSlash(basePath), path);

        public static string EnsureTrailingSlash(string p)
        {
            if (string.IsNullOrEmpty(p)) return p;
            if (!p.EndsWith(Path.DirectorySeparatorChar) && !p.EndsWith(Path.AltDirectorySeparatorChar))
                return p + Path.DirectorySeparatorChar;
            return p;
        }

        public static long SafeFileSizeKB(string fullPath)
        {
            try
            {
                var fi = new FileInfo(fullPath);
                return fi.Exists ? Math.Max(1, fi.Length / 1024) : 0;
            }
            catch { return 0; }
        }

        public static string StripCountSuffix(string programFileDisplay)
        {
            int idx = programFileDisplay.LastIndexOf(" [");
            if (idx > 0 && programFileDisplay.EndsWith(" in folder]", StringComparison.OrdinalIgnoreCase))
                return programFileDisplay.Substring(0, idx);
            return programFileDisplay;
        }

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

        public static string FlatArchiveDestination(string archiveRoot, string fullSourcePath)
        {
            Directory.CreateDirectory(archiveRoot);
            string fileName = Path.GetFileName(fullSourcePath);
            return MakeUniqueNameInDirectory(archiveRoot, fileName);
        }
    }
}
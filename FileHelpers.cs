using System;
using System.IO;

namespace FileHunter
{
    public static class FileHelperMethods // Renamed to avoid duplicate
    {
        // Example helper methods
        public static bool HasExtension(string path, string ext) => 
            Path.GetExtension(path).Equals(ext, StringComparison.OrdinalIgnoreCase);

        public static bool IsBakFile(string path) =>
            Path.GetFileName(path).Contains("bak", StringComparison.OrdinalIgnoreCase);

        public static string GetRelativePathPortable(string root, string path) =>
            Path.GetRelativePath(root, path);

        public static (string location, string unit) ExtractLocationUnit(string relDir)
        {
            // Implement logic here
            return ("", "");
        }

        public static string ToQuarter(DateTime dt)
        {
            // Implement logic here
            return "";
        }

        public static string FlatArchiveDestination(string archiveRoot, string file)
        {
            // Implement logic here
            return Path.Combine(archiveRoot, Path.GetFileName(file));
        }

        public static string StripCountSuffix(string fileName)
        {
            // Implement logic here
            return fileName;
        }
    }
}
public class AppState
{
    public string? LastSearchDirectory { get; set; }
    public string? ArchiveDirectory { get; set; }
    public List<SearchRow>? Results { get; set; }
    // Add this property to fix CS1061
    public Dictionary<string, string>? Notes { get; set; }
}
namespace FileHunter.Models
{
    public enum TreeTagKind { All, Location, Unit }
    public class TreeTag
    {
        public TreeTagKind Kind { get; set; }
        public string? Location { get; set; }
        public string? Unit { get; set; }
    }
}
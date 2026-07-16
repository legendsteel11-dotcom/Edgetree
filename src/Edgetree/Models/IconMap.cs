namespace SidebarExplorer.App.Models;

public class IconMap
{
    public Dictionary<string, string> FileExtensions { get; set; } = new();
    public Dictionary<string, string> FileNames { get; set; } = new();
    public Dictionary<string, string> FolderNames { get; set; } = new();
    public Dictionary<string, string> FolderNamesExpanded { get; set; } = new();
    public string? DefaultFile { get; set; }
    public string? DefaultFolder { get; set; }
    public string? DefaultFolderExpanded { get; set; }
}

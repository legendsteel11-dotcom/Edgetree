namespace SidebarExplorer.App.Models;

// A folder's own remembered sort ("정렬 -> 이름순/최신순") that stays independent
// of the app-wide default set from the options menu - see
// AppSettings.FolderSortOverrides and Services.FileSystemService.SortOverrides.
public class FolderSortOverrideEntry
{
    public string Path { get; set; } = string.Empty;
    public bool SortByDate { get; set; }
    public bool SortDescending { get; set; }
}

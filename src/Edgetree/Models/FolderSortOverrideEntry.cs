namespace SidebarExplorer.App.Models;

// A folder's own remembered sort ("정렬 -> 이름순/최신순") that stays independent
// of the app-wide default set from the options menu - see
// AppSettings.FolderSortOverrides and Services.FileSystemService.SortOverrides.
public class FolderSortOverrideEntry
{
    public string Path { get; set; } = string.Empty;

    // "name" | "date" | "type" | "size". Replaced the SortByDate boolean when
    // 유형/크기 were added - a second flag would have made three fields say what
    // one name says. SortByDate is still written (see MainWindow's save path)
    // so a settings file moved back to an older build still sorts sensibly,
    // and an entry written by one of those builds - no SortField at all -
    // reads back through it.
    public string SortField { get; set; } = string.Empty;

    public bool SortByDate { get; set; }
    public bool SortDescending { get; set; }
}

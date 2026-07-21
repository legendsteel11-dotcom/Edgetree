using System.ComponentModel;
using System.Windows.Media;
using SidebarExplorer.App.Services;

namespace SidebarExplorer.App.Models;

// One row in the search results list, which interleaves folder headers with
// files: consecutive results sharing a folder collapse under a single header
// (see MainWindow.RunSearchFilter). A header carries only its DirectoryPath; a
// file row carries the SearchEntry it came from. The results DataTemplate
// switches layout on IsHeader, and the click/keyboard/context-menu handlers act
// only on rows whose Entry is non-null.
//
// INotifyPropertyChanged exists solely for Icon: in Windows-shell icon mode a
// per-file icon (.exe 등) can arrive from a background extraction after the
// row is already on screen (see ShellIconService), and the callback re-raises
// Icon so the row picks it up. Everything else is init-only as before.
public sealed class SearchRow : INotifyPropertyChanged
{
    public bool IsHeader { get; init; }

    // The synthetic "… 더 보기 (N개)" row appended when the results were capped -
    // clicking it raises the display limit (see MainWindow). Its own kind so
    // the click handler can tell it apart from a real file row.
    public bool IsShowMore { get; init; }
    public string ShowMoreLabel { get; init; } = string.Empty;

    public string DirectoryPath { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public FileSearchService.SearchEntry? Entry { get; init; }

    // Mode-aware (PNG set vs. Windows shell icons - same switch as the tree,
    // see ShellIconService): a per-extension file icon for file rows, a folder
    // icon for header rows. Whether it actually shows is gated by the
    // ShowFolderIcons / ShowFileIcons toggles in the results template, same as
    // the tree.
    public ImageSource? Icon => IsShowMore
        ? null
        : IsHeader
            ? ShellIconService.GetFolderIcon(FolderNameOf(DirectoryPath), isExpanded: false)
            : ShellIconService.GetFileIcon(FileName, Entry?.FullPath ?? string.Empty, RaiseIconChanged);

    // Where the query matched inside FileName, so that run can be drawn in the
    // highlight color (see SearchHighlightBehavior). -1/0 means "don't
    // highlight" - used for header rows and for wildcard queries, where there's
    // no single literal substring to point at.
    public int MatchStart { get; init; } = -1;
    public int MatchLength { get; init; }

    public static SearchRow Header(string directoryPath) => new()
    {
        IsHeader = true,
        DirectoryPath = directoryPath
    };

    public static SearchRow ShowMore(string label) => new()
    {
        IsShowMore = true,
        ShowMoreLabel = label
    };

    public static SearchRow File(FileSearchService.SearchEntry entry, int matchStart, int matchLength) => new()
    {
        IsHeader = false,
        DirectoryPath = entry.DirectoryPath,
        FileName = entry.FileName,
        Entry = entry,
        MatchStart = matchStart,
        MatchLength = matchLength
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void RaiseIconChanged()
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Icon)));

    private static string FolderNameOf(string directoryPath)
    {
        string trimmed = directoryPath.TrimEnd('\\', '/');
        int slash = trimmed.LastIndexOfAny(new[] { '\\', '/' });
        return slash >= 0 ? trimmed[(slash + 1)..] : trimmed;
    }
}

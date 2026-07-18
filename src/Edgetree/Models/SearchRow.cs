using SidebarExplorer.App.Services;

namespace SidebarExplorer.App.Models;

// One row in the search results list, which interleaves folder headers with
// files: consecutive results sharing a folder collapse under a single header
// (see MainWindow.RunSearchFilter). A header carries only its DirectoryPath; a
// file row carries the SearchEntry it came from. The results DataTemplate
// switches layout on IsHeader, and the click/keyboard/context-menu handlers act
// only on rows whose Entry is non-null.
public sealed class SearchRow
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

    // Resolved once at row creation (the same Material icon set the tree uses):
    // a per-extension file icon for file rows, the folder's own themed icon for
    // header rows. Whether it actually shows is gated by the ShowFolderIcons /
    // ShowFileIcons toggles in the results template, same as the tree.
    public string IconUri { get; init; } = string.Empty;

    // Where the query matched inside FileName, so that run can be drawn in the
    // highlight color (see SearchHighlightBehavior). -1/0 means "don't
    // highlight" - used for header rows and for wildcard queries, where there's
    // no single literal substring to point at.
    public int MatchStart { get; init; } = -1;
    public int MatchLength { get; init; }

    public static SearchRow Header(string directoryPath) => new()
    {
        IsHeader = true,
        DirectoryPath = directoryPath,
        IconUri = IconResolver.ResolveFolderIcon(FolderNameOf(directoryPath))
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
        IconUri = IconResolver.ResolveFileIcon(entry.FileName),
        MatchStart = matchStart,
        MatchLength = matchLength
    };

    private static string FolderNameOf(string directoryPath)
    {
        string trimmed = directoryPath.TrimEnd('\\', '/');
        int slash = trimmed.LastIndexOfAny(new[] { '\\', '/' });
        return slash >= 0 ? trimmed[(slash + 1)..] : trimmed;
    }
}

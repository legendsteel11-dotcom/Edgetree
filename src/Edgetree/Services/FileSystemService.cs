using System.IO;
using System.Runtime.InteropServices;
using SidebarExplorer.App.Models;

namespace SidebarExplorer.App.Services;

public enum FileSortField
{
    Name,
    Date
}

// A folder's own remembered sort override ("정렬 -> 전역 정렬 따르기" not chosen
// for it) - see FileSystemService.SortOverrides and Models.AppSettings.
public readonly record struct FolderSortOverride(FileSortField Field, bool Descending);

public static class FileSystemService
{
    // Set from MainWindow's "정렬" options submenu; read here on every
    // (re)load, unless the folder being loaded has its own entry in
    // SortOverrides below. Folders are always grouped before files (see
    // LoadChildren) regardless of this - only the order within each group
    // changes.
    public static FileSortField SortField = FileSortField.Name;
    public static bool SortDescending = false;

    // Per-folder sort overrides, keyed by the folder's own FullPath (not its
    // children's) - set from AppSettings.FolderSortOverrides at startup and
    // whenever a folder's own right-click "정렬" is used, same "static mirror
    // of settings, read at load time" pattern as SortField/SortDescending
    // above and FileSystemItem.DisplayCap. OrdinalIgnoreCase since Windows
    // paths are case-insensitive.
    public static readonly Dictionary<string, FolderSortOverride> SortOverrides =
        new(StringComparer.OrdinalIgnoreCase);

    // Mirrors AppSettings.IsLightMode - same static pattern as the fields
    // above, kept in sync by MainWindow.ApplyColorSettings (every color
    // change, including the light/dark toggle itself, runs through there).
    // Only consulted by FormatSortOverrideIconUri below, to pick the "_L"
    // light-mode icon variant.
    public static bool IsLightMode = false;

    // Windows Explorer's own "smart" name sort (digit runs compared as
    // numbers, so "file2" sorts before "file10") - plain ordinal/ordinal-
    // ignore-case comparison sorts "file10" before "file2" character by
    // character, which reads as broken sorting for any real folder of
    // sequentially-numbered files or folders. shlwapi's StrCmpLogicalW is the
    // exact function Explorer itself uses for this, so results match what
    // users already expect from Windows.
    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int StrCmpLogicalW(string psz1, string psz2);

    private sealed class NaturalStringComparer : IComparer<string?>
    {
        public static readonly NaturalStringComparer Instance = new();
        public int Compare(string? x, string? y) => StrCmpLogicalW(x ?? string.Empty, y ?? string.Empty);
    }

    // The same Explorer-style natural name comparison LoadChildren uses,
    // exposed so the file-search results can sort names identically (see
    // MainWindow's results-only sort).
    public static IComparer<string?> NaturalNameComparer => NaturalStringComparer.Instance;

    public static string NormalizeSortOverridePath(string path) => path.TrimEnd('\\');

    // Resolves which of the pre-made sort-override icon images (see
    // FileSystemItem.SortOverrideIconUri) matches the current field/direction
    // - aliginIconNameAsc.png / aliginIconNameDesc.png / aliginIconDateAsc.png
    // / aliginIconDateDesc.png under Resources/Icons, provided by the user,
    // each already encoding both the field (color) and direction (which
    // triangle is filled) visually, so no separate text label is needed. Each
    // also has its own "_L" suffixed light-mode variant (also provided by the
    // user) picked instead whenever IsLightMode is on, since the dark
    // versions read poorly against a light background.
    // Shown for a folder with NO override of its own - i.e. "follows the global
    // sort". Same neutral icon the search view's sort button uses for its own
    // default state, and deliberately without an "_L" light variant (it's a
    // mid-tone that reads on both themes), so it never changes with IsLightMode.
    public const string NoSortOverrideIconUri = "pack://application:,,,/Resources/Icons/aliginIconDefault.png";

    public static string FormatSortOverrideIconUri(FileSortField field, bool descending)
    {
        string name = field == FileSortField.Date ? "Date" : "Name";
        string direction = descending ? "Desc" : "Asc";
        string suffix = IsLightMode ? "_L" : string.Empty;
        return $"pack://application:,,,/Resources/Icons/aliginIcon{name}{direction}{suffix}.png";
    }

    // The ToolTip naming whichever sort the icon above is currently showing -
    // see Strings.SortTooltipFormat for why the icons don't stand alone.
    public static string FormatSortTooltip(FileSortField field, bool descending)
        => string.Format(Strings.SortTooltipFormat, field == FileSortField.Date
            ? (descending ? Strings.SortModeDateDesc : Strings.SortModeDateAsc)
            : (descending ? Strings.SortModeNameDesc : Strings.SortModeNameAsc));

    // Same, for the neutral icon's own state. A property rather than a cached
    // string so it picks up the English strings after Strings.Initialize.
    public static string NoSortOverrideTooltip
        => string.Format(Strings.SortTooltipFormat, Strings.SortModeFollowGlobal);

    // Static mirror of AppSettings.BookmarkPaths, same pattern as
    // SortOverrides/SortField: FileSystemItem instances are created lazily
    // (and re-created by refreshes), so each constructor consults this set to
    // decide its own IsBookmarked instead of anyone having to walk the tree
    // re-applying flags. MainWindow keeps it in sync with the settings list.
    public static readonly HashSet<string> BookmarkedPaths = new(StringComparer.OrdinalIgnoreCase);

    public static List<FileSystemItem> GetDriveRoots()
    {
        var roots = new List<FileSystemItem>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady)
            {
                continue;
            }

            string driveName = drive.Name.TrimEnd('\\');
            string? label = TryGetVolumeLabel(drive);
            string displayName = string.IsNullOrWhiteSpace(label) ? driveName : $"{label} ({driveName})";

            roots.Add(new FileSystemItem(displayName, drive.RootDirectory.FullName, isDirectory: true)
            {
                IsOnNetworkDrive = drive.DriveType == DriveType.Network
            });
        }

        return roots;
    }

    // Hidden/System items are skipped so the tree matches Windows Explorer's
    // default (and the file search, which already skips them). RecurseSub-
    // directories stays false - LoadChildren only ever loads one level.
    private static readonly EnumerationOptions VisibleEntryOptions = new()
    {
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
        IgnoreInaccessible = true
    };

    // readFailed distinguishes "this folder is genuinely empty" from "the
    // read itself blew up" (sleeping/disconnected NAS, permissions, drive
    // yanked). Callers MUST treat a failed read as unknown, never as empty:
    // recording it as empty is how a network drive's root lost its expander
    // arrow for the rest of the session (2026-07-23 NAS report) - and a
    // background merge doing the same would strip every loaded row beneath
    // the drive because it blinked once.
    public static List<FileSystemItem> LoadChildren(string path, FileSystemItem parent, out bool readFailed)
    {
        readFailed = false;
        var result = new List<FileSystemItem>();
        var (field, descending) = SortOverrides.TryGetValue(NormalizeSortOverridePath(path), out var over)
            ? (over.Field, over.Descending)
            : (SortField, SortDescending);

        try
        {
            var directories = SortPaths(Directory.EnumerateDirectories(path, "*", VisibleEntryOptions), isDirectory: true, field, descending);

            foreach (var dir in directories)
            {
                var name = Path.GetFileName(dir);
                if (!string.IsNullOrEmpty(name))
                {
                    result.Add(new FileSystemItem(name, dir, isDirectory: true, parent));
                }
            }
        }
        catch (UnauthorizedAccessException) { readFailed = true; }
        catch (IOException) { readFailed = true; }

        try
        {
            var files = SortPaths(Directory.EnumerateFiles(path, "*", VisibleEntryOptions), isDirectory: false, field, descending);

            foreach (var file in files)
            {
                var name = Path.GetFileName(file);
                if (!string.IsNullOrEmpty(name))
                {
                    result.Add(new FileSystemItem(name, file, isDirectory: false, parent));
                }
            }
        }
        catch (UnauthorizedAccessException) { readFailed = true; }
        catch (IOException) { readFailed = true; }

        return result;
    }

    private static IEnumerable<string> SortPaths(IEnumerable<string> paths, bool isDirectory, FileSortField field, bool descending)
    {
        if (field == FileSortField.Date)
        {
            return descending
                ? paths.OrderByDescending(p => GetLastWriteTime(p, isDirectory))
                : paths.OrderBy(p => GetLastWriteTime(p, isDirectory));
        }

        return descending
            ? paths.OrderByDescending(Path.GetFileName, NaturalStringComparer.Instance)
            : paths.OrderBy(Path.GetFileName, NaturalStringComparer.Instance);
    }

    private static DateTime GetLastWriteTime(string path, bool isDirectory)
        => isDirectory ? Directory.GetLastWriteTime(path) : File.GetLastWriteTime(path);

    private static string? TryGetVolumeLabel(DriveInfo drive)
    {
        try
        {
            return drive.VolumeLabel;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}

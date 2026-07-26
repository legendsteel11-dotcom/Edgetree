using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using SidebarExplorer.App.Models;

namespace SidebarExplorer.App.Services;

public enum FileSortField
{
    Name,
    Date,
    Type,
    Size
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

    // The row's sort icon, as vector geometry rather than the four hand-drawn
    // PNGs it replaces (aliginIcon{Name,Date}{Asc,Desc}.png plus their "_L"
    // light-mode twins). Those encoded the FIELD as well as the direction,
    // which stopped scaling the moment 유형/크기 were added - four fields would
    // have meant four more drawings in two themes. The icon's job is now:
    //
    //   sort glyph      - this folder just follows the app-wide default
    //   arrow to top    - it has its own sort, ascending
    //   arrow to bottom - it has its own sort, descending
    //
    // The field itself is named in words by the menu the icon opens, which is
    // where a fourth (or fifth) sort order costs one row and no artwork. Being
    // a path rather than an image, it takes the row's own foreground brush and
    // so follows the light/dark theme with no second asset.
    //
    // Material Symbols "sort" / "vertical_align_top" / "vertical_align_bottom"
    // on their 960 grid (exported by the user at 16dp, wght500). WPF's path
    // mini-language matches SVG's own `d`, so these are the exported strings
    // verbatim - do not hand-edit the coordinates.
    private static Geometry ParseFrozen(string path)
    {
        var geometry = Geometry.Parse(path);
        geometry.Freeze();
        return geometry;
    }

    public static readonly Geometry FollowsGlobalSortGeometry = ParseFrozen(
        "M135.87-247.74v-82.76h252.2v82.76h-252.2Zm0-190.76v-82.76h446.11v82.76H135.87Zm0-190.76v-82.76h688.26v82.76H135.87Z");

    private static readonly Geometry AscendingSortGeometry = ParseFrozen(
        "M183.87-741.13v-83h592.26v83H183.87ZM438.5-135.87v-375.41l-99.5 99.5-58.65-58.65L480-670.09l199.65 199.66L621-411.78l-99.5-99.5v375.41h-83Z");

    private static readonly Geometry DescendingSortGeometry = ParseFrozen(
        "M183.87-135.87v-83h592.26v83H183.87ZM480-289.91 280.35-489.57 339-548.22l99.5 99.5v-375.41h83v375.41l99.5-99.5 58.65 58.65L480-289.91Z");

    public static Geometry SortOverrideGeometry(bool descending)
        => descending ? DescendingSortGeometry : AscendingSortGeometry;

    // The SEARCH view's own sort button still uses the original PNG set (name/
    // date x asc/desc, each with an "_L" light variant, plus the neutral one).
    // It is a different control with different states - it also groups by
    // folder - and nothing about 유형/크기 reaches it, so it was left alone
    // rather than dragged through this change for symmetry's sake.
    public const string NoSortOverrideIconUri = "pack://application:,,,/Resources/Icons/aliginIconDefault.png";

    public static string FormatSortOverrideIconUri(FileSortField field, bool descending)
    {
        string name = field == FileSortField.Date ? "Date" : "Name";
        string direction = descending ? "Desc" : "Asc";
        string suffix = IsLightMode ? "_L" : string.Empty;
        return $"pack://application:,,,/Resources/Icons/aliginIcon{name}{direction}{suffix}.png";
    }

    // The ToolTip naming whichever sort is active on this folder - the icon
    // shows the direction but never the field, so this is where "이름"/"크기"
    // actually gets said without opening the menu.
    public static string FormatSortTooltip(FileSortField field, bool descending)
        => string.Format(Strings.SortTooltipFormat, DescribeSort(field, descending));

    private static string DescribeSort(FileSortField field, bool descending)
    {
        string name = field switch
        {
            FileSortField.Date => Strings.MenuSortByDate,
            FileSortField.Type => Strings.MenuSortByType,
            FileSortField.Size => Strings.MenuSortBySize,
            _ => Strings.MenuSortByName
        };
        return $"{name} · {(descending ? Strings.MenuSortDescending : Strings.MenuSortAscending)}";
    }

    // Sort field <-> settings string. Stored by name rather than the old
    // "SortByDate" boolean so a third and fourth field didn't need a second
    // flag; the boolean is still written alongside for older builds.
    public static FileSortField ParseSortField(string? name) => name?.Trim().ToLowerInvariant() switch
    {
        "date" => FileSortField.Date,
        "type" => FileSortField.Type,
        "size" => FileSortField.Size,
        _ => FileSortField.Name
    };

    public static string FormatSortFieldName(FileSortField field) => field switch
    {
        FileSortField.Date => "date",
        FileSortField.Type => "type",
        FileSortField.Size => "size",
        _ => "name"
    };

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

    // Explorer's own semantics, since that's the ordering people already have
    // in their hands: folders and files are sorted as separate blocks (the
    // caller lists folders first either way), and a folder has neither a size
    // nor a type of its own - Explorer shows them all as one kind - so those
    // two orders fall back to the name order for folders while the direction
    // still applies to the whole listing.
    private static IEnumerable<string> SortPaths(IEnumerable<string> paths, bool isDirectory, FileSortField field, bool descending)
    {
        var effective = isDirectory && field is FileSortField.Size or FileSortField.Type
            ? FileSortField.Name
            : field;

        IOrderedEnumerable<string> ordered = effective switch
        {
            FileSortField.Date => descending
                ? paths.OrderByDescending(p => GetLastWriteTime(p, isDirectory))
                : paths.OrderBy(p => GetLastWriteTime(p, isDirectory)),
            FileSortField.Size => descending
                ? paths.OrderByDescending(GetFileLength)
                : paths.OrderBy(GetFileLength),
            FileSortField.Type => descending
                ? paths.OrderByDescending(GetTypeKey, NaturalStringComparer.Instance)
                : paths.OrderBy(GetTypeKey, NaturalStringComparer.Instance),
            _ => descending
                ? paths.OrderByDescending(Path.GetFileName, NaturalStringComparer.Instance)
                : paths.OrderBy(Path.GetFileName, NaturalStringComparer.Instance)
        };

        // Equal sizes, types or timestamps fall back to the name order rather
        // than whatever order the file system happened to hand back, so a
        // folder full of same-type files doesn't reshuffle between reads.
        return effective == FileSortField.Name
            ? ordered
            : ordered.ThenBy(Path.GetFileName, NaturalStringComparer.Instance);
    }

    private static long GetFileLength(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }

    // The extension, lower-cased and without its dot. Explorer sorts by the
    // shell's registered type NAME ("PNG 파일"), which needs a per-file shell
    // lookup; the extension groups the same files together in the same order
    // for a fraction of the cost, and files with no extension come first.
    private static string GetTypeKey(string path)
        => Path.GetExtension(path).TrimStart('.').ToLowerInvariant();

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

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

    public static string NormalizeSortOverridePath(string path) => path.TrimEnd('\\');

    // "N↑"/"N↓"/"D↑"/"D↓" - the compact label shown next to a folder's own
    // sort-override icon (see FileSystemItem.SortOverrideLabel) so the icon
    // conveys which of the 4 combinations is active without needing separate
    // per-field icon art.
    public static string FormatSortOverrideLabel(FileSortField field, bool descending)
        => (field == FileSortField.Date ? "D" : "N") + (descending ? "↓" : "↑");

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

            roots.Add(new FileSystemItem(displayName, drive.RootDirectory.FullName, isDirectory: true));
        }

        return roots;
    }

    public static List<FileSystemItem> LoadChildren(string path, FileSystemItem parent)
    {
        var result = new List<FileSystemItem>();
        var (field, descending) = SortOverrides.TryGetValue(NormalizeSortOverridePath(path), out var over)
            ? (over.Field, over.Descending)
            : (SortField, SortDescending);

        try
        {
            var directories = SortPaths(Directory.EnumerateDirectories(path), isDirectory: true, field, descending);

            foreach (var dir in directories)
            {
                var name = Path.GetFileName(dir);
                if (!string.IsNullOrEmpty(name))
                {
                    result.Add(new FileSystemItem(name, dir, isDirectory: true, parent));
                }
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }

        try
        {
            var files = SortPaths(Directory.EnumerateFiles(path), isDirectory: false, field, descending);

            foreach (var file in files)
            {
                var name = Path.GetFileName(file);
                if (!string.IsNullOrEmpty(name))
                {
                    result.Add(new FileSystemItem(name, file, isDirectory: false, parent));
                }
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }

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

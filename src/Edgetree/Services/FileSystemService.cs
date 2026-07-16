using System.IO;
using SidebarExplorer.App.Models;

namespace SidebarExplorer.App.Services;

public enum FileSortField
{
    Name,
    Date
}

public static class FileSystemService
{
    // Set from MainWindow's "정렬" options submenu; read here on every
    // (re)load. Folders are always grouped before files (see LoadChildren)
    // regardless of this - only the order within each group changes.
    public static FileSortField SortField = FileSortField.Name;
    public static bool SortDescending = false;

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

        try
        {
            var directories = SortPaths(Directory.EnumerateDirectories(path), isDirectory: true);

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
            var files = SortPaths(Directory.EnumerateFiles(path), isDirectory: false);

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

    private static IEnumerable<string> SortPaths(IEnumerable<string> paths, bool isDirectory)
    {
        if (SortField == FileSortField.Date)
        {
            return SortDescending
                ? paths.OrderByDescending(p => GetLastWriteTime(p, isDirectory))
                : paths.OrderBy(p => GetLastWriteTime(p, isDirectory));
        }

        return SortDescending
            ? paths.OrderByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            : paths.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase);
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

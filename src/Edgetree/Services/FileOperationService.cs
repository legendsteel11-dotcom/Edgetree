using System.IO;
using Microsoft.VisualBasic.FileIO;
using Clipboard = System.Windows.Clipboard;

namespace SidebarExplorer.App.Services;

public static class FileOperationService
{
    public static void CopyPathToClipboard(string path)
    {
        Clipboard.SetText(path);
    }

    public static void CopyToClipboard(string path)
    {
        var files = new System.Collections.Specialized.StringCollection();
        files.Add(path);
        Clipboard.SetFileDropList(files);
    }

    // Returns false only when the clipboard has nothing pasteable, so the
    // caller can distinguish "nothing to do" from "tried and failed".
    public static bool TryPaste(string destinationFolder, out string? error)
    {
        error = null;
        if (!Clipboard.ContainsFileDropList())
        {
            return false;
        }

        foreach (string? sourcePath in Clipboard.GetFileDropList())
        {
            if (sourcePath is null)
            {
                continue;
            }
            try
            {
                CopyEntry(sourcePath, destinationFolder);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                error = ex.Message;
            }
        }

        return true;
    }

    private static void CopyEntry(string sourcePath, string destinationFolder)
    {
        string name = Path.GetFileName(sourcePath.TrimEnd(Path.DirectorySeparatorChar));
        string destPath = GetUniqueDestination(Path.Combine(destinationFolder, name));

        if (Directory.Exists(sourcePath))
        {
            CopyDirectoryRecursive(sourcePath, destPath, overwrite: false);
        }
        else if (File.Exists(sourcePath))
        {
            File.Copy(sourcePath, destPath);
        }
    }

    private static void CopyDirectoryRecursive(string sourceDir, string destDir, bool overwrite)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.EnumerateFiles(sourceDir))
        {
            string destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, overwrite ? destFile : GetUniqueDestination(destFile), overwrite);
        }
        foreach (var dir in Directory.EnumerateDirectories(sourceDir))
        {
            CopyDirectoryRecursive(dir, Path.Combine(destDir, Path.GetFileName(dir)), overwrite);
        }
    }

    // Dragging a file in from outside the app always copies (never removes
    // the external original - the safer default for a sidebar utility, not a
    // full file manager) and asks per top-level dropped item before
    // overwriting anything that already exists at the destination; declining
    // just skips that item and moves on to the next.
    public static bool TryImportDroppedPaths(IReadOnlyList<string> sourcePaths, string destinationFolder,
        Func<string, bool> confirmOverwrite, out string? error)
    {
        error = null;
        foreach (string sourcePath in sourcePaths)
        {
            try
            {
                string name = Path.GetFileName(sourcePath.TrimEnd(Path.DirectorySeparatorChar));
                string destPath = Path.Combine(destinationFolder, name);
                bool exists = File.Exists(destPath) || Directory.Exists(destPath);

                if (exists && !confirmOverwrite(name))
                {
                    continue;
                }

                if (Directory.Exists(sourcePath))
                {
                    CopyDirectoryRecursive(sourcePath, destPath, overwrite: exists);
                }
                else if (File.Exists(sourcePath))
                {
                    File.Copy(sourcePath, destPath, overwrite: exists);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                error = ex.Message;
            }
        }

        return true;
    }

    // Pasting into a folder that already has an item with the same name
    // appends " (2)", " (3)", ... instead of overwriting.
    private static string GetUniqueDestination(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return path;
        }

        string dir = Path.GetDirectoryName(path) ?? string.Empty;
        string name = Path.GetFileNameWithoutExtension(path);
        string ext = Path.GetExtension(path);

        for (int i = 2; ; i++)
        {
            string candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    // Auto-numbered on conflict ("새 폴더 (2)", ...), same convention as paste,
    // so calling this repeatedly in the same folder never overwrites/fails.
    public static bool TryCreateFolder(string parentDirectory, out string? createdPath, out string? error)
    {
        createdPath = null;
        error = null;
        try
        {
            string path = GetUniqueDestination(Path.Combine(parentDirectory, "새 폴더"));
            Directory.CreateDirectory(path);
            createdPath = path;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool TryRename(string path, string newName, out string? error)
    {
        error = null;
        try
        {
            string? dir = Path.GetDirectoryName(path);
            if (dir is null)
            {
                error = "이름을 바꿀 수 없습니다.";
                return false;
            }
            string newPath = Path.Combine(dir, newName);

            if (Directory.Exists(path))
            {
                Directory.Move(path, newPath);
            }
            else if (File.Exists(path))
            {
                File.Move(path, newPath);
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = ex.Message;
            return false;
        }
    }

    // Sends to the Recycle Bin rather than permanently deleting, so an
    // accidental delete from the sidebar is still recoverable.
    public static bool TryDeleteToRecycleBin(string path, out string? error)
    {
        error = null;
        try
        {
            if (Directory.Exists(path))
            {
                FileSystem.DeleteDirectory(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            }
            else if (File.Exists(path))
            {
                FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            error = ex.Message;
            return false;
        }
    }
}

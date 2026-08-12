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

    // Where Windows keeps "was this a copy or a cut" - the file list alone
    // doesn't say. Writing it is what makes Ctrl+X here paste as a MOVE in
    // Explorer, and reading it is how a cut made in Explorer arrives here as
    // one. (DROPEFFECT_COPY = 1, DROPEFFECT_MOVE = 2; Explorer writes 5 =
    // COPY|LINK for a copy, hence the bit test rather than an equality one.)
    private const string PreferredDropEffect = "Preferred DropEffect";
    private const int DropEffectCopy = 1;
    private const int DropEffectMove = 2;

    public static void CopyToClipboard(string path)
        => CopyToClipboard(new[] { path });

    public static void CopyToClipboard(IEnumerable<string> paths)
        => SetClipboardFiles(paths, move: false);

    // Ctrl+X. Nothing is moved here - the clipboard just records the intent,
    // and the paste is what acts on it (same as Explorer, so a cut left
    // unpasted costs nothing).
    public static bool CutToClipboard(string path)
        => CutToClipboard(new[] { path });

    public static bool CutToClipboard(IEnumerable<string> paths)
        => SetClipboardFiles(paths, move: true);

    private static bool SetClipboardFiles(IEnumerable<string> paths, bool move)
    {
        var files = new System.Collections.Specialized.StringCollection();
        foreach (string path in paths)
        {
            files.Add(path);
        }
        if (files.Count == 0)
        {
            return false;
        }

        var data = new System.Windows.DataObject();
        data.SetFileDropList(files);
        data.SetData(PreferredDropEffect,
            new MemoryStream(BitConverter.GetBytes(move ? DropEffectMove : DropEffectCopy)));

        try
        {
            // copy: true so the list outlives this process - a cut made here
            // and pasted in Explorer after the sidebar is closed still works.
            Clipboard.SetDataObject(data, copy: true);
            return true;
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            // Another process had the clipboard open. Nothing was placed, so
            // the caller must not mark anything as cut.
            return false;
        }
    }

    // What a paste turned out to be, so the caller knows whether the source
    // folders need refreshing too (a move empties them) and can drop its cut
    // markers once they've been acted on.
    public sealed record PasteOutcome(bool WasMove, IReadOnlyList<string> SourcePaths)
    {
        public static readonly PasteOutcome None = new(false, Array.Empty<string>());
    }

    // Returns false only when the clipboard has nothing pasteable, so the
    // caller can distinguish "nothing to do" from "tried and failed".
    public static bool TryPaste(string destinationFolder, out PasteOutcome outcome, out string? error)
    {
        error = null;
        outcome = PasteOutcome.None;

        string[] sourcePaths;
        bool move;
        try
        {
            var data = Clipboard.GetDataObject();
            if (data is null || !data.GetDataPresent(System.Windows.DataFormats.FileDrop) ||
                data.GetData(System.Windows.DataFormats.FileDrop) is not string[] paths || paths.Length == 0)
            {
                return false;
            }
            sourcePaths = paths;
            move = IsMoveRequested(data);
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            return false;
        }

        outcome = new PasteOutcome(move, sourcePaths);

        foreach (string sourcePath in sourcePaths)
        {
            try
            {
                if (move)
                {
                    MoveEntry(sourcePath, destinationFolder);
                }
                else
                {
                    CopyEntry(sourcePath, destinationFolder);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                error = ex.Message;
            }
        }

        return true;
    }

    // Is the clipboard STILL holding exactly the cut these paths were marked
    // for? Anything else - a copy somewhere, another app's cut, a paste that
    // consumed ours (Explorer empties the clipboard after a move) - means the
    // markers are stale. null = couldn't tell, because another process had the
    // clipboard open at that instant; the caller must leave the markers alone
    // rather than clear them on a failed read (our own cut raises the change
    // notification too, and clearing there would erase what was just set).
    public static bool? ClipboardStillHoldsCut(IReadOnlyCollection<string> paths)
    {
        try
        {
            var data = Clipboard.GetDataObject();
            if (data is null || !data.GetDataPresent(System.Windows.DataFormats.FileDrop) ||
                !IsMoveRequested(data) ||
                data.GetData(System.Windows.DataFormats.FileDrop) is not string[] onClipboard ||
                onClipboard.Length != paths.Count)
            {
                return false;
            }

            foreach (string path in onClipboard)
            {
                if (!paths.Contains(path, StringComparer.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            return null;
        }
    }

    private static bool IsMoveRequested(System.Windows.IDataObject data)
    {
        if (data.GetData(PreferredDropEffect) is MemoryStream stream && stream.Length >= 4)
        {
            var bytes = new byte[4];
            stream.Position = 0;
            if (stream.Read(bytes, 0, 4) == 4)
            {
                return (BitConverter.ToInt32(bytes, 0) & DropEffectMove) == DropEffectMove;
            }
        }

        return false;
    }

    private static void CopyEntry(string sourcePath, string destinationFolder)
    {
        string trimmedSource = sourcePath.TrimEnd(Path.DirectorySeparatorChar);
        string name = Path.GetFileName(trimmedSource);

        if (Directory.Exists(sourcePath))
        {
            // Into itself or into its own subtree. MoveEntry has refused this
            // from the start; copy never did, and the difference was not
            // harmless - copying a folder into ITSELF walks into the copy it
            // just made and does it again, forever, writing the whole time.
            // Reported 2026-08-02 as the app hanging (Windows killed it as
            // "not responding") after a Ctrl+C, Ctrl+V on one selected folder,
            // which is as ordinary a sequence as this app has: paste targets
            // the selected folder, so the source IS the destination. It left
            // behind a folder nested into itself dozens of levels deep.
            if (IsSameOrBeneath(destinationFolder, trimmedSource))
            {
                throw new IOException(Strings.CopyIntoSelfError);
            }

            CopyDirectoryRecursive(sourcePath, GetUniqueDestination(Path.Combine(destinationFolder, name)),
                overwrite: false);
        }
        else if (File.Exists(sourcePath))
        {
            File.Copy(sourcePath, GetUniqueDestination(Path.Combine(destinationFolder, name)));
        }
    }

    // The cut half of paste. Name conflicts number up to " (2)" exactly like a
    // copied paste does - the app has never asked before writing, and a move
    // is not the place to start.
    private static void MoveEntry(string sourcePath, string destinationFolder)
    {
        string trimmedSource = sourcePath.TrimEnd(Path.DirectorySeparatorChar);
        string name = Path.GetFileName(trimmedSource);

        // Cut and pasted back into the folder it came from: Explorer does
        // nothing here, and the alternative - numbering it up to " (2)" - would
        // silently turn a move into a copy.
        if (Path.GetDirectoryName(trimmedSource) is { } sourceParent && IsSamePath(sourceParent, destinationFolder))
        {
            return;
        }

        if (Directory.Exists(sourcePath))
        {
            // Into itself or into its own subtree: refuse before anything is
            // written, since carrying it out would consume the source.
            if (IsSameOrBeneath(destinationFolder, trimmedSource))
            {
                throw new IOException(Strings.MoveIntoSelfError);
            }

            string destPath = GetUniqueDestination(Path.Combine(destinationFolder, name));
            if (IsSameVolume(trimmedSource, destPath))
            {
                Directory.Move(sourcePath, destPath);
            }
            else
            {
                // Directory.Move can't cross volumes. Copy the whole tree
                // FIRST and delete the source only once that has succeeded -
                // a failure halfway leaves the original untouched, so there is
                // never a half-moved folder to roll back.
                CopyDirectoryRecursive(sourcePath, destPath, overwrite: false);
                Directory.Delete(sourcePath, recursive: true);
            }
        }
        else if (File.Exists(sourcePath))
        {
            // File.Move handles crossing volumes on its own (copy + delete,
            // with the delete only after the copy landed).
            File.Move(sourcePath, GetUniqueDestination(Path.Combine(destinationFolder, name)));
        }
    }

    private static bool IsSamePath(string a, string b)
        => string.Equals(a.TrimEnd(Path.DirectorySeparatorChar), b.TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);

    private static bool IsSameOrBeneath(string candidate, string root)
    {
        string trimmedCandidate = candidate.TrimEnd(Path.DirectorySeparatorChar);
        string trimmedRoot = root.TrimEnd(Path.DirectorySeparatorChar);
        return string.Equals(trimmedCandidate, trimmedRoot, StringComparison.OrdinalIgnoreCase) ||
            trimmedCandidate.StartsWith(trimmedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSameVolume(string a, string b)
        => string.Equals(Path.GetPathRoot(Path.GetFullPath(a)), Path.GetPathRoot(Path.GetFullPath(b)),
            StringComparison.OrdinalIgnoreCase);

    private static void CopyDirectoryRecursive(string sourceDir, string destDir, bool overwrite)
    {
        // Both listings are taken BEFORE anything is written. EnumerateFiles/
        // Directories stream lazily, so a folder created inside the source
        // while the walk is still running gets picked up by that same walk and
        // copied in turn - which is how a folder pasted into itself produced an
        // endless chain of copies. The caller now refuses that case outright;
        // this is the second lock on the same door, and it also covers the
        // cross-volume move path that comes through here.
        var files = Directory.EnumerateFiles(sourceDir).ToList();
        var dirs = Directory.EnumerateDirectories(sourceDir).ToList();

        Directory.CreateDirectory(destDir);
        foreach (var file in files)
        {
            string destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, overwrite ? destFile : GetUniqueDestination(destFile), overwrite);
        }
        foreach (var dir in dirs)
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
                    // Same guard as CopyEntry's, for the drag-in route: an
                    // outside folder dropped onto something inside itself is
                    // the same endless walk.
                    if (IsSameOrBeneath(destinationFolder, sourcePath.TrimEnd(Path.DirectorySeparatorChar)))
                    {
                        throw new IOException(Strings.CopyIntoSelfError);
                    }

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

    // Shift+드래그. The MOVE half of the drop above, and deliberately NOT a
    // second idea of what moving means: it goes through MoveEntry, the same
    // call 잘라내기+붙여넣기 has always used. So a name that already exists
    // numbers up to " (2)" rather than asking, a folder dropped into its own
    // subtree is refused before anything is written, and a move across volumes
    // copies in full BEFORE the source is removed.
    //
    // Only ever reached for a drag that started inside this app (see
    // MainWindow's InternalDragFormat). A file dragged in from Explorer still
    // copies whatever keys are held - taking someone's file out of a folder
    // this app does not own is not a sidebar's business, and Explorer's own
    // idea of who deletes the source in a cross-application move is not
    // something to be guessing at.
    //
    // The source FOLDERS come back because a move empties them, and the tree
    // may well have them open: refreshing only the destination would leave the
    // rows that just left still sitting there.
    public static bool TryMoveDroppedPaths(IReadOnlyList<string> sourcePaths, string destinationFolder,
        out IReadOnlyList<string> emptiedFolders, out string? error)
    {
        error = null;
        var folders = new List<string>();
        foreach (string sourcePath in sourcePaths)
        {
            try
            {
                string? parent = Path.GetDirectoryName(sourcePath.TrimEnd(Path.DirectorySeparatorChar));
                MoveEntry(sourcePath, destinationFolder);
                if (parent is not null && !folders.Contains(parent, StringComparer.OrdinalIgnoreCase))
                {
                    folders.Add(parent);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // One item's failure is not the other five's - same rule the
                // copy path beside this one follows.
                error = ex.Message;
            }
        }

        emptiedFolders = folders;
        return true;
    }

    // Pasting into a folder that already has an item with the same name
    // appends " (2)", " (3)", ... instead of overwriting. Shared with
    // ArchiveService so a second 압축 of the same row numbers up the same way
    // the rest of the app does.
    internal static string GetUniqueDestination(string path)
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
            string path = GetUniqueDestination(Path.Combine(parentDirectory, Strings.NewFolderDefaultName));
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
                error = Strings.RenameFailedBody;
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

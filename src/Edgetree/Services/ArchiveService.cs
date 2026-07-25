using System.IO;
using System.IO.Compression;

namespace SidebarExplorer.App.Services;

// zip compress/extract on top of .NET's own System.IO.Compression - no
// external dependency, so the single-exe distribution stays as it is.
//
// Both operations build their result under a HIDDEN temporary name in the
// destination folder and rename it into place only once it has closed
// cleanly. Two reasons: a half-written archive never shows up in the tree as
// something the user can click and open (the tree skips Hidden/System
// entries - see FileSystemService.VisibleEntryOptions), and a failure part
// way through leaves nothing behind. Staging under %TEMP% would have given
// the same invisibility but cost a full second pass over every byte, since
// the move back would cross volumes; a same-folder rename is metadata only.
public static class ArchiveService
{
    public readonly record struct ArchiveResult(bool Success, string? CreatedPath, int SkippedCount, string? Error);

    public static bool IsZipPath(string path)
        => string.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase);

    // Packs every source path into one archive named baseName.zip inside
    // destinationFolder. Files that can't be read (locked, permission denied)
    // are skipped and counted rather than aborting the whole run - one locked
    // file shouldn't throw away several minutes of work on a large folder.
    public static ArchiveResult CreateZip(IReadOnlyList<string> sourcePaths, string destinationFolder, string baseName)
    {
        string finalPath = FileOperationService.GetUniqueDestination(Path.Combine(destinationFolder, baseName + ".zip"));
        string tempPath = FileOperationService.GetUniqueDestination(
            Path.Combine(destinationFolder, "~" + baseName + ".zip.tmp"));
        int skipped = 0;

        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                File.SetAttributes(tempPath, FileAttributes.Hidden | FileAttributes.Temporary);

                using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
                foreach (string source in sourcePaths)
                {
                    if (Directory.Exists(source))
                    {
                        string name = Path.GetFileName(source.TrimEnd(Path.DirectorySeparatorChar));
                        AddDirectory(archive, source, name, tempPath, ref skipped);
                    }
                    else if (File.Exists(source))
                    {
                        AddFile(archive, source, Path.GetFileName(source), ref skipped);
                    }
                }
            }

            // The Hidden/Temporary marks were only there to keep the partial
            // file out of the tree; the finished archive is an ordinary file.
            File.SetAttributes(tempPath, FileAttributes.Normal);
            File.Move(tempPath, finalPath);
            return new ArchiveResult(true, finalPath, skipped, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            TryDeleteTemp(tempPath);
            return new ArchiveResult(false, null, skipped, ex.Message);
        }
    }

    private static void AddDirectory(ZipArchive archive, string directory, string entryPrefix, string tempPath,
        ref int skipped)
    {
        string[] files;
        string[] subdirectories;
        try
        {
            files = Directory.GetFiles(directory);
            subdirectories = Directory.GetDirectories(directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            skipped++;
            return;
        }

        // An empty folder still belongs in the archive (Explorer keeps them
        // too), and a zip records that as a bare "path/" entry.
        if (files.Length == 0 && subdirectories.Length == 0)
        {
            archive.CreateEntry(entryPrefix + "/");
            return;
        }

        foreach (string file in files)
        {
            // A selection can pair a folder with something inside it, which
            // would put our own growing temp archive inside the very tree
            // being packed - it must never swallow itself.
            if (string.Equals(file, tempPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            AddFile(archive, file, entryPrefix + "/" + Path.GetFileName(file), ref skipped);
        }

        foreach (string subdirectory in subdirectories)
        {
            AddDirectory(archive, subdirectory, entryPrefix + "/" + Path.GetFileName(subdirectory), tempPath,
                ref skipped);
        }
    }

    private static void AddFile(ZipArchive archive, string file, string entryName, ref int skipped)
    {
        try
        {
            archive.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            skipped++;
        }
    }

    // Unpacks into a folder named after the archive, beside it ("photos.zip"
    // -> "photos\", auto-numbered on conflict like paste). ExtractToDirectory
    // rejects entries that would escape the destination, so a hand-crafted
    // archive can't write outside the folder it claims.
    public static ArchiveResult ExtractZip(string zipPath)
    {
        string? directory = Path.GetDirectoryName(zipPath);
        if (directory is null)
        {
            return new ArchiveResult(false, null, 0, null);
        }

        string baseName = Path.GetFileNameWithoutExtension(zipPath);
        string finalDirectory = FileOperationService.GetUniqueDestination(Path.Combine(directory, baseName));
        string tempDirectory = FileOperationService.GetUniqueDestination(Path.Combine(directory, "~" + baseName + ".tmp"));

        try
        {
            var temp = Directory.CreateDirectory(tempDirectory);
            temp.Attributes |= FileAttributes.Hidden;
            ZipFile.ExtractToDirectory(zipPath, tempDirectory);

            // Cleared before the move: Directory.Move carries attributes over,
            // and a hidden result folder would look like nothing happened.
            temp.Attributes &= ~FileAttributes.Hidden;
            Directory.Move(tempDirectory, finalDirectory);
            return new ArchiveResult(true, finalDirectory, 0, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException
                                       or NotSupportedException)
        {
            TryDeleteTempDirectory(tempDirectory);
            return new ArchiveResult(false, null, 0, ex.Message);
        }
    }

    private static void TryDeleteTemp(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Nothing useful to do about a leftover temp file - it stays
            // hidden, and the operation's own error is what the user sees.
        }
    }

    private static void TryDeleteTempDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}

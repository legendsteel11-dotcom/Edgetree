using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SidebarExplorer.App.Services;

// Thumbnails kept on local disk, so a folder that has been browsed once opens
// from here instead of from the file it came out of.
//
// Windows has a thumbnail cache of its own and it is genuinely good - measured
// at 11ms an answer where generating one cold takes 869ms - but it does not
// cover this case on its own. Two reasons, both measured (2026-08-11, a folder
// of 1359 photos on a NAS):
//
//   - Most files here never reach the shell at all. 85% of them answered from
//     the thumbnail inside their own JPEG header, at ~10ms EACH OVER SMB. That
//     is network traffic paid again on every visit, and Windows' cache holds
//     nothing for it because the shell was never asked.
//   - Windows' cache is a fixed size and evicts. When it drops an entry, the
//     869ms comes back. Nothing here can stop that; a cache of our own means it
//     stops mattering.
//
// The entry is keyed by PATH and validated by the file's own write time and
// length, so a file that changes is regenerated and a file that does not is
// read from here for ever. A file that is new to a folder simply has no entry
// and makes one on first sight - which is the whole behaviour asked for, and it
// falls out of the design rather than needing a rule.
public static class ThumbnailCacheService
{
    // "ETTC" - checked on read so a truncated or foreign file is a miss rather
    // than an exception.
    private const uint Magic = 0x43545445;
    // 2 (2026-08-21): 셸에서 받은 그림의 줄 순서를 비트맵마다 확인하도록 고치기
    // 전에 쓰인 항목들은 뒤집힌 채로 저장돼 있다. 원본이 안 바뀌었으므로 그대로
    // 두면 계속 뒤집혀 나오고, 사용자가 캐싱 파일 정리를 직접 해야만 사라진다.
    // 판 번호를 올리면 안 맞는 항목이 전부 없는 것으로 처리되어 다음에 볼 때
    // 다시 받아온다. **대가는 업데이트 직후 폴더마다 한 번씩 다시 받아오는
    // 것이고, NAS 폴더의 첫 방문이 그만큼 느려진다.**
    private const int Version = 2;

    // Above this the oldest entries go. 300MB is roughly 20,000 thumbnails at
    // the size they encode to, which is more folders than anyone browses in a
    // stretch; the point of the cap is that an app that writes to disk for ever
    // must have one, not that this number is special.
    private const long MaxBytes = 300L * 1024 * 1024;
    // Trimmed down to this rather than to the cap itself, so a full cache does
    // not trim on every single write.
    private const long TrimTargetBytes = (long)(MaxBytes * 0.8);

    private static readonly object Gate = new();
    private static string? _root;
    private static long _bytes;
    private static bool _measured;
    private static bool _trimming;

    private static string? Root
    {
        get
        {
            lock (Gate)
            {
                if (_root is not null)
                {
                    return _root;
                }

                try
                {
                    string dir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "Edgetree", "thumb-cache");
                    Directory.CreateDirectory(dir);
                    _root = dir;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    // No cache is a slow app, not a broken one - every caller
                    // treats a miss as "fetch it then".
                    return null;
                }

                return _root;
            }
        }
    }

    // 256 buckets by the first byte of the hash. One directory holding tens of
    // thousands of files is slow to enumerate on every trim, and the trim is the
    // one operation here that walks the whole thing.
    private static string? EntryPath(string filePath)
    {
        if (Root is not { } root)
        {
            return null;
        }

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(filePath.ToLowerInvariant()));
        string name = Convert.ToHexString(hash);
        return Path.Combine(root, name[..2], name + ".etc");
    }

    // Null on any miss at all: no entry, a stale one, a damaged one, or a cache
    // that could not be opened. requestedSize is honoured as a FLOOR - an entry
    // made for a bigger ask still answers a smaller one, since the image is only
    // ever scaled down from here, but never the other way round or the strip
    // would show something upscaled and soft.
    public static ImageSource? TryRead(string filePath, int requestedSize)
    {
        if (EntryPath(filePath) is not { } entry || !File.Exists(entry))
        {
            return null;
        }

        try
        {
            var info = new FileInfo(filePath);
            if (!info.Exists)
            {
                return null;
            }

            using var stream = File.OpenRead(entry);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            if (reader.ReadUInt32() != Magic || reader.ReadInt32() != Version)
            {
                return null;
            }

            long writtenTicks = reader.ReadInt64();
            long writtenLength = reader.ReadInt64();
            int cachedSize = reader.ReadInt32();
            // The path is stored and compared because two different files can
            // hash to one name. Vanishingly unlikely, and the consequence would
            // be showing the wrong picture - which is the one kind of wrong this
            // must not be.
            string cachedPath = reader.ReadString();

            if (writtenTicks != info.LastWriteTimeUtc.Ticks ||
                writtenLength != info.Length ||
                cachedSize < requestedSize ||
                !string.Equals(cachedPath, filePath, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // OnLoad, and copied out of the file first: what is returned outlives
            // this method and a delay-created bitmap tied to a closed stream
            // draws nothing at all - the same trap the embedded-thumbnail path
            // fell into (2026-08-11).
            var buffer = new MemoryStream(reader.ReadBytes((int)(stream.Length - stream.Position)));

            // DECODED AT THE SIZE ASKED FOR, not at the size stored. An entry
            // written for a 256px strip is still the right answer for a 128px
            // one - that is what "cachedSize < requestedSize" above is for - but
            // decoding it whole and shrinking afterwards materialises the big
            // picture anyway, and its pixels are unmanaged, so the discarded
            // copy sits in the process until something collects it. A strip of
            // 2,402 files made roughly 470MB of exactly that (2026-08-12).
            //
            // Width only, so the aspect ratio is kept. A portrait thumbnail
            // therefore comes back a little taller than the ask rather than
            // exactly within it, which is a rounding error next to decoding it
            // at full size - and the caller trims anything still over.
            var image = new BitmapImage();
            image.BeginInit();
            image.StreamSource = buffer;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.None;
            if (cachedSize > requestedSize)
            {
                image.DecodePixelWidth = requestedSize;
            }

            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException
                                     or EndOfStreamException or NotSupportedException
                                     or FileFormatException or ArgumentException or OverflowException)
        {
            return null;
        }
    }

    public static void Write(string filePath, ImageSource image, int requestedSize)
    {
        if (image is not BitmapSource source || EntryPath(filePath) is not { } entry)
        {
            return;
        }

        try
        {
            var info = new FileInfo(filePath);
            if (!info.Exists)
            {
                return;
            }

            // JPEG, and the trade is stated rather than hidden: it drops the
            // alpha channel. A thumbnail is a preview of a photograph in all but
            // a handful of cases, and PNG at this size is five to ten times the
            // bytes - which would turn a 20MB folder into 150MB and make the cap
            // above bite in an afternoon.
            var encoder = new JpegBitmapEncoder { QualityLevel = 82 };
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var pixels = new MemoryStream();
            encoder.Save(pixels);

            Directory.CreateDirectory(Path.GetDirectoryName(entry)!);
            // Written beside and renamed into place: a half-written entry that
            // survived would be read back as a damaged picture, and this is a
            // cache - it can afford to lose a write but not to serve rubbish.
            string temp = entry + ".tmp";
            using (var stream = File.Create(temp))
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(Magic);
                writer.Write(Version);
                writer.Write(info.LastWriteTimeUtc.Ticks);
                writer.Write(info.Length);
                writer.Write(requestedSize);
                writer.Write(filePath);
                writer.Write(pixels.ToArray());
            }

            File.Move(temp, entry, overwrite: true);
            NoteWritten(new FileInfo(entry).Length);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException
                                     or NotSupportedException or ArgumentException)
        {
            // A cache that cannot be written is still a working app.
        }
    }

    private static void NoteWritten(long bytes)
    {
        bool trim;
        lock (Gate)
        {
            _bytes += bytes;
            trim = _measured && _bytes > MaxBytes && !_trimming;
            if (trim)
            {
                _trimming = true;
            }
        }

        if (trim)
        {
            Task.Run(Trim);
        }
    }

    // Called once at startup, off the UI thread. Until it has finished the cache
    // still reads and writes; it simply does not know its own size yet, and a
    // trim decided on a half-counted total would throw away entries for nothing.
    public static void Measure()
    {
        if (Root is not { } root)
        {
            return;
        }

        try
        {
            long total = 0;
            foreach (string file in Directory.EnumerateFiles(root, "*.etc", SearchOption.AllDirectories))
            {
                total += new FileInfo(file).Length;
            }

            bool trim;
            lock (Gate)
            {
                _bytes = total;
                _measured = true;
                trim = _bytes > MaxBytes && !_trimming;
                if (trim)
                {
                    _trimming = true;
                }
            }

            if (trim)
            {
                Trim();
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    // Oldest first, by the time the ENTRY was written rather than the file it
    // describes: what is being reclaimed is space taken by folders nobody has
    // opened lately.
    private static void Trim()
    {
        try
        {
            if (Root is not { } root)
            {
                return;
            }

            var entries = Directory.EnumerateFiles(root, "*.etc", SearchOption.AllDirectories)
                .Select(path => new FileInfo(path))
                .OrderBy(info => info.LastWriteTimeUtc)
                .ToList();

            long total = entries.Sum(info => info.Length);
            foreach (var info in entries)
            {
                if (total <= TrimTargetBytes)
                {
                    break;
                }

                try
                {
                    long size = info.Length;
                    info.Delete();
                    total -= size;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                }
            }

            lock (Gate)
            {
                _bytes = total;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
        finally
        {
            lock (Gate)
            {
                _trimming = false;
            }
        }
    }

    public static long CurrentBytes
    {
        get
        {
            lock (Gate)
            {
                return _bytes;
            }
        }
    }

    // Returns how many bytes went. The caller says so; this class does not know
    // whether anyone asked for it or it happened on its own.
    public static long Clear()
    {
        if (Root is not { } root)
        {
            return 0;
        }

        long freed = 0;
        try
        {
            foreach (string file in Directory.EnumerateFiles(root, "*.etc", SearchOption.AllDirectories))
            {
                try
                {
                    var info = new FileInfo(file);
                    long size = info.Length;
                    info.Delete();
                    freed += size;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }

        lock (Gate)
        {
            _bytes = Math.Max(0, _bytes - freed);
        }

        return freed;
    }
}

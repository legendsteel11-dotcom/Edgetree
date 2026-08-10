using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Application = System.Windows.Application;

namespace SidebarExplorer.App.Services;

// Fetches the same thumbnail Windows Explorer's "큰 아이콘" view shows for a
// file, via IShellItemImageFactory - which also means the OS thumbnail cache
// (thumbcache) does the caching: a file thumbnailed once by anything on the
// system comes back instantly, and this app deliberately keeps no cache of its
// own (the context menu shows one image at a time; it's not an image viewer).
//
// Always asynchronous: first-time generation can take real time (a large image
// on a NAS), so the menu opens immediately with a reserved empty slot and the
// image lands via the callback when ready - same two-stage pattern as
// ShellIconService's per-file icons.
//
// GDI/COM discipline, same containment rule as the icon service: the HBITMAP
// and the COM factory both live and die inside Extract; only a frozen WPF
// ImageSource ever leaves this class.
public static class ShellThumbnailService
{
    // onCompleted is invoked on the UI thread, with a null image when the
    // file has no thumbnail to give (unsupported/corrupted/unreadable) - the
    // caller collapses its slot rather than showing an empty box forever.
    // pixelWidth/Height are the ORIGINAL image's dimensions (the thumbnail is
    // scaled, so they can't be read off it), 0 when they couldn't be read -
    // decoded header-only, on the same background hop as the thumbnail.
    public static void GetThumbnail(string path, int pixelSize, Action<ImageSource?, int, int> onCompleted)
        => GetPreview(path, pixelSize, thumbnailOnly: true, onCompleted);

    // For callers that only want the picture. The dimensions below cost a
    // SECOND open of the same file - cheap locally, a second network round trip
    // over SMB - and the filmstrip throws them away: it asks for one thumbnail
    // per realised cell, so on a NAS folder that was one wasted open per cell
    // scrolled past (found 2026-08-10, chasing a stutter that grew while
    // browsing 1329 photos).
    public static void GetThumbnailOnly(string path, int pixelSize, Action<ImageSource?> onCompleted)
        => GetPreview(path, pixelSize, thumbnailOnly: true,
            (image, _, _) => onCompleted(image), readDimensions: false);

    // The picture a JPEG already carries inside itself.
    //
    // This is the difference between us and the viewers that open a folder of
    // 1359 NAS photos in seconds (user's comparison with FastStone, and the
    // measurement that made sense of it: 869ms average per shell thumbnail on a
    // cold NAS folder, worst 2784ms). The shell builds its thumbnail by reading
    // and decoding the WHOLE file - 2-5MB each over SMB. Almost every JPEG a
    // phone or camera writes already holds a small one in its EXIF header, and
    // reading that touches only the first few tens of KB.
    //
    // DelayCreation + CacheOption.None is what keeps it to the header: the
    // decoder is asked for the thumbnail and nothing else, so the pixels of the
    // full image are never fetched.
    //
    // Returns null when there is no embedded thumbnail (PNG, most screenshots,
    // some editors' output) - the caller falls back to the shell, which is
    // still the only answer for those.
    public static ImageSource? TryReadEmbedded(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var frame = BitmapDecoder.Create(stream,
                BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];
            if (frame.Thumbnail is not { } embedded)
            {
                return null;
            }

            // CachedBitmap, and it has to be INSIDE the using: what the decoder
            // hands back is delay-created and still tied to this stream, so
            // freezing it and returning it produced cells that simply never
            // drew - a strip with holes in it rather than a slow one (user,
            // 2026-08-11: "이빨빠진 모양새"). The pixels are read here, while
            // the file is still open, and what leaves this method owns them.
            var cached = new CachedBitmap(embedded, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);

            // The header's orientation applies to the embedded picture exactly
            // as it does to the full one, and the shell already honours it - so
            // without this the strip would disagree with itself depending on
            // which path answered.
            var oriented = ApplyOrientation(cached, ReadOrientation(frame));
            oriented.Freeze();
            return oriented;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException
                                      or FileFormatException or ArgumentException or OverflowException)
        {
            return null;
        }
    }

    private static int ReadOrientation(BitmapFrame frame)
    {
        try
        {
            if (frame.Metadata is not BitmapMetadata metadata)
            {
                return 1;
            }

            object? value = metadata.Format switch
            {
                "jpg" or "jpeg" => metadata.GetQuery("/app1/ifd/{ushort=274}"),
                "tiff" => metadata.GetQuery("/ifd/{ushort=274}"),
                _ => null,
            };

            return value is ushort tag && tag is >= 1 and <= 8 ? tag : 1;
        }
        catch (Exception e) when (e is NotSupportedException or ArgumentException
                                     or InvalidOperationException or IOException)
        {
            return 1;
        }
    }

    private static BitmapSource ApplyOrientation(BitmapSource source, int orientation)
    {
        if (orientation <= 1)
        {
            return source;
        }

        BitmapSource result = orientation is 2 or 4 or 5 or 7
            ? new TransformedBitmap(source, new ScaleTransform(-1, 1))
            : source;

        double angle = orientation switch
        {
            3 or 4 => 180,
            5 or 8 => 270,
            6 or 7 => 90,
            _ => 0,
        };

        return angle == 0 ? result : new TransformedBitmap(result, new RotateTransform(angle));
    }

    // thumbnailOnly:false lets the shell fall back to the file-type icon -
    // what the viewer panel shows for a non-image selection. The context
    // menu's thumbnail slot keeps thumbnailOnly:true (see the flag note in
    // Extract).
    // How many of these are running right now. Nothing here throttles - every
    // call is its own Task.Run - so on a folder of 1329 files the filmstrip can
    // put one in flight per cell it realises, all on the same SMB connection.
    // Read by the viewer's load instrument; if this number climbs while
    // browsing, the strip is the thing to bound (2026-08-10).
    private static int _inFlight;

    public static int InFlight => Volatile.Read(ref _inFlight);

    // Set by the viewer's instrument in Debug builds. What it is looking for:
    // this Task.Run is an MTA thread pool thread, and a shell COM object created
    // there can come back as a PROXY whose calls marshal onto the app's STA
    // thread - i.e. onto the UI thread. If that is what is happening, an
    // Extract that takes 1.5s on a cold NAS freezes the window for 1.5s, which
    // matches both the size and the timing of the stalls left unexplained after
    // everything else was timed and cleared (2026-08-10).
    public static Action<string>? Trace;

    // A few at a time, NEWEST FIRST, because both halves were measured on a
    // cold NAS folder (2026-08-10):
    //
    //   314 extractions, average 869ms each, worst 2784ms
    //
    // Every one of those used to be its own Task.Run, fired the moment a cell
    // was realised, all onto one SMB connection. Three things came of it: the
    // NAS itself stopped answering twice, each call got slower because they
    // were competing, and - the part that makes it feel broken rather than
    // slow - the queue was FIFO, so the cells actually on screen waited behind
    // hundreds for cells scrolled past long ago.
    //
    // A STACK fixes the last one for free: the newest request is the one the
    // eye is on. Old entries still run, just last, and the OS thumbnail cache
    // makes the second visit to any folder cheap regardless.
    //
    // NOT a semaphore around Task.Run: that would hold a pool thread per
    // waiting job, and 300 blocked threads is its own outage.
    // The bound exists for the NAS and is priced for the NAS: two, because
    // three dropped it off the network again while a cold folder was being
    // browsed (2026-08-10). Local disks were paying it too, and a local folder
    // of 170 photos filled its strip "느긋하게" as a result (user, 2026-08-11).
    //
    // So the caller sets it per folder. Everything in this class is a queue in
    // front of one resource; how much of that resource there is depends on
    // where the files are, and only the caller knows that.
    private static int _maxWorkers = 2;

    public static int MaxWorkers
    {
        get => Volatile.Read(ref _maxWorkers);
        set => Volatile.Write(ref _maxWorkers, Math.Clamp(value, 1, 8));
    }
    private static readonly object PendingGate = new();
    private static readonly Stack<(string Path, int PixelSize, bool ThumbnailOnly,
        bool ReadDimensions, Action<ImageSource?, int, int> OnCompleted)> Pending = new();
    private static int _workers;

    public static void GetPreview(string path, int pixelSize, bool thumbnailOnly,
        Action<ImageSource?, int, int> onCompleted, bool readDimensions = true)
    {
        Interlocked.Increment(ref _inFlight);
        lock (PendingGate)
        {
            Pending.Push((path, pixelSize, thumbnailOnly, readDimensions, onCompleted));
            if (_workers >= MaxWorkers)
            {
                return;
            }

            _workers++;
        }

        Task.Run(() =>
        {
            while (true)
            {
                (string Path, int PixelSize, bool ThumbnailOnly,
                    bool ReadDimensions, Action<ImageSource?, int, int> OnCompleted) job;
                lock (PendingGate)
                {
                    if (!Pending.TryPop(out job))
                    {
                        _workers--;
                        return;
                    }
                }

                RunPreview(job.Path, job.PixelSize, job.ThumbnailOnly, job.ReadDimensions, job.OnCompleted);
            }
        });
    }

    private static void RunPreview(string path, int pixelSize, bool thumbnailOnly,
        bool readDimensions, Action<ImageSource?, int, int> onCompleted)
    {
        try
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();

            // The header first, and the shell only if the file has no thumbnail
            // of its own. Traced separately so the two costs stay comparable -
            // the whole point of this path is that they should not be.
            ImageSource? thumbnail = null;
            string source = "embedded";
            if (thumbnailOnly)
            {
                thumbnail = TryReadEmbedded(path);
            }

            if (thumbnail is null)
            {
                source = "shell   ";
                thumbnail = Extract(path, pixelSize, thumbnailOnly);
            }

            if (Trace is { } trace)
            {
                double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0)
                    * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                if (ms >= 100)
                {
                    trace($"  thumb {source} {ms,7:F0} ms  {System.IO.Path.GetFileName(path)}");
                }
            }

            int pixelWidth = 0, pixelHeight = 0;
            if (readDimensions)
            {
                try
                {
                    using var stream = File.OpenRead(path);
                    var frame = BitmapDecoder.Create(stream,
                        BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];
                    pixelWidth = frame.PixelWidth;
                    pixelHeight = frame.PixelHeight;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException or ArgumentException)
                {
                    // No WIC codec / unreadable header - the info line just omits
                    // the dimensions.
                }
            }

            Application.Current?.Dispatcher.BeginInvoke(() => onCompleted(thumbnail, pixelWidth, pixelHeight));
        }
        finally
        {
            Interlocked.Decrement(ref _inFlight);
        }
    }

    private static ImageSource? Extract(string path, int pixelSize, bool thumbnailOnly)
    {
        IShellItemImageFactory? factory = null;
        IntPtr hBitmap = IntPtr.Zero;
        try
        {
            var factoryIid = typeof(IShellItemImageFactory).GUID;
            if (SHCreateItemFromParsingName(path, IntPtr.Zero, ref factoryIid, out factory) != 0 ||
                factory is null)
            {
                return null;
            }

            // THUMBNAILONLY (the thumbnail slot's mode): fail rather than fall
            // back to a file-type icon - the menu slot is for an actual
            // preview, and the row already has its icon. Without it (the
            // viewer's non-image mode) the shell answers with the icon
            // instead. BIGGERSIZEOK: a cached larger image is fine, the Image
            // control scales it down.
            var size = new SIZE { cx = pixelSize, cy = pixelSize };
            int flags = SIIGBF_BIGGERSIZEOK | (thumbnailOnly ? SIIGBF_THUMBNAILONLY : 0);
            if (factory.GetImage(size, flags, out hBitmap) != 0 ||
                hBitmap == IntPtr.Zero)
            {
                return null;
            }

            return ToBitmapSource(hBitmap);
        }
        catch (Exception e) when (e is COMException or ArgumentException or InvalidCastException)
        {
            return null;
        }
        finally
        {
            if (hBitmap != IntPtr.Zero)
            {
                DeleteObject(hBitmap);
            }
            if (factory is not null)
            {
                Marshal.ReleaseComObject(factory);
            }
        }
    }

    private static BitmapSource? ToBitmapSource(IntPtr hBitmap)
    {
        // Reading the DIB's bits directly preserves the (premultiplied) alpha
        // channel that CreateBitmapSourceFromHBitmap throws away - without
        // this, a transparent PNG's thumbnail lands on a solid black square.
        // The buffer is read as top-down, which is what GetImage's thumbnails
        // are in practice.
        //
        // A HEIGHT-SIGN CHECK WAS TRIED HERE AND REMOVED (2026-08-09). The
        // theory was that bottom-up DIBs (biHeight > 0) needed their rows
        // reversed, which would explain the upside-down folder icons. It
        // never demonstrated a fix - the icons kept flipping with it in
        // place, and what actually solved them was moving the ICON path off
        // GetImage entirely and onto an HICON from the system image list
        // (ShellIconService.GetViewerIcon), which carries no orientation
        // header to be wrong about. Meanwhile a video's thumbnail came back
        // upside down WITH the check in place, i.e. the header said bottom-up
        // for a bitmap whose rows were not. The lesson is that these headers
        // cannot be trusted in either direction, so the code does not consult
        // them: thumbnails are read the way they were read before the theory,
        // which is the way that worked.
        if (GetObject(hBitmap, Marshal.SizeOf<BITMAP>(), out BITMAP bmp) != 0 &&
            bmp.bmBitsPixel == 32 && bmp.bmBits != IntPtr.Zero)
        {
            var source = BitmapSource.Create(
                bmp.bmWidth, bmp.bmHeight, 96, 96, PixelFormats.Pbgra32, null,
                bmp.bmBits, bmp.bmWidthBytes * bmp.bmHeight, bmp.bmWidthBytes);
            source.Freeze();
            return source;
        }

        // Not a 32bpp DIB (rare) - the alpha-less conversion is still a
        // correct picture for opaque formats.
        var fallback = Imaging.CreateBitmapSourceFromHBitmap(
            hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        fallback.Freeze();
        return fallback;
    }

    private const int SIIGBF_BIGGERSIZEOK = 0x00000001;
    private const int SIIGBF_THUMBNAILONLY = 0x00000008;

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int cx;
        public int cy;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public IntPtr bmBits;
    }

    [ComImport]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(SIZE size, int flags, out IntPtr phbm);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(
        string pszPath, IntPtr pbc, ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory? ppv);

    [DllImport("gdi32.dll")]
    private static extern int GetObject(IntPtr h, int c, out BITMAP pv);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);
}

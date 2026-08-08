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

    // thumbnailOnly:false lets the shell fall back to the file-type icon -
    // what the viewer panel shows for a non-image selection. The context
    // menu's thumbnail slot keeps thumbnailOnly:true (see the flag note in
    // Extract).
    public static void GetPreview(string path, int pixelSize, bool thumbnailOnly, Action<ImageSource?, int, int> onCompleted)
    {
        Task.Run(() =>
        {
            var thumbnail = Extract(path, pixelSize, thumbnailOnly);

            int pixelWidth = 0, pixelHeight = 0;
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

            Application.Current?.Dispatcher.BeginInvoke(() => onCompleted(thumbnail, pixelWidth, pixelHeight));
        });
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
        // GetImage hands back a 32bpp top-down DIB section in practice;
        // reading its bits directly preserves the (premultiplied) alpha
        // channel that CreateBitmapSourceFromHBitmap throws away - without
        // this, a transparent PNG's thumbnail lands on a solid black square.
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

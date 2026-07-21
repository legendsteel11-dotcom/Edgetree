using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Application = System.Windows.Application;

namespace SidebarExplorer.App.Services;

// The single place both icon modes ("아이콘 방식") are served from: the bundled
// Material-style PNG set (the app's original look) or the icons Windows
// Explorer itself shows. Which one is a live app-wide switch (UseShellIcons,
// set from AppSettings at startup and by the options menu), so the models'
// Icon getters just call in here and don't know about modes at all.
//
// Explorer mode works in two stages, exactly like Explorer itself:
//  1. Immediately: an extension-generic icon via SHGetFileInfo with
//     SHGFI_USEFILEATTRIBUTES, which resolves from the extension string alone -
//     no disk or network I/O, so a NAS full of files costs nothing extra.
//  2. For the handful of types whose icon is per-FILE, not per-extension
//     (.exe/.lnk/.ico/...), the real icon is extracted on a background thread
//     and swapped in through the caller's change callback when it arrives. A
//     slow network .exe therefore shows its generic icon a moment longer -
//     the UI thread is never blocked waiting on one.
//
// Threading contract: every public entry point runs on the UI thread (they're
// called from binding getters and dispatcher callbacks), so the caches need no
// locks. The background stage touches no shared state - it hands its result
// back via Dispatcher.BeginInvoke.
//
// GDI discipline: every HICON this class obtains is destroyed inside
// ExtractShellIcon before it returns - the caches hold only frozen WPF
// ImageSources, never native handles, so no handle can leak however long the
// app runs (this app deliberately had zero native handle usage before this
// feature; keep it contained here).
public static class ShellIconService
{
    public static bool UseShellIcons;

    // Per-path icons are unbounded in principle (every distinct .exe/.lnk the
    // user ever scrolls past), so past this point the cache is simply reset -
    // crude, but it bounds memory and at worst re-extracts icons that were
    // last seen thousands of files ago.
    private const int MaxPerPathCacheEntries = 2048;

    private static readonly Dictionary<string, ImageSource> PackCache = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, ImageSource> GenericShellCache = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, ImageSource> PerPathCache = new(StringComparer.OrdinalIgnoreCase);

    // One background fetch per path however many rows show it (tree + search
    // results can both hold the same file); late callers just add their
    // callback to the waiting list.
    private static readonly Dictionary<string, List<Action>> PendingFetches = new(StringComparer.OrdinalIgnoreCase);

    // The types whose icon belongs to the individual file rather than its
    // extension. Everything else gets the (instant) extension-generic icon,
    // which for those types is already exactly what Explorer shows.
    private static readonly HashSet<string> PerFileIconExtensions = new(StringComparer.Ordinal)
    {
        ".exe", ".lnk", ".ico", ".cur", ".url", ".appref-ms"
    };

    // iconChanged is invoked (on the UI thread) when a per-file icon arrives
    // later than this call - the caller raises PropertyChanged on its Icon so
    // the row re-reads it and picks the real icon up from the cache.
    public static ImageSource? GetFileIcon(string fileName, string fullPath, Action? iconChanged)
    {
        if (!UseShellIcons)
        {
            return GetPackIcon(IconResolver.ResolveFileIcon(fileName));
        }

        string extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (fullPath.Length > 0 && PerFileIconExtensions.Contains(extension))
        {
            if (PerPathCache.TryGetValue(fullPath, out var cached))
            {
                return cached;
            }
            QueuePerFileFetch(fullPath, extension, iconChanged);
        }

        return GetGenericFileIcon(extension);
    }

    public static ImageSource? GetFolderIcon(string folderName, bool isExpanded)
        => UseShellIcons
            ? GetGenericFolderIcon(isExpanded)
            : GetPackIcon(IconResolver.ResolveFolderIcon(folderName, isExpanded));

    // The favorites panel's rows all share one folder icon (favorites are
    // always folders); MainWindow stores this in a DynamicResource the
    // favorites DataTemplate binds, refreshed on every mode switch.
    public static ImageSource? GetFavoritesFolderIcon()
        => UseShellIcons
            ? GetGenericFolderIcon(isExpanded: false)
            : GetPackIcon("pack://application:,,,/Resources/Icons/folder.png");

    private static ImageSource? GetPackIcon(string packUri)
    {
        if (!PackCache.TryGetValue(packUri, out var image))
        {
            var bitmap = new BitmapImage(new Uri(packUri));
            bitmap.Freeze();
            PackCache[packUri] = image = bitmap;
        }
        return image;
    }

    private static ImageSource? GetGenericFileIcon(string extension)
    {
        string key = "f:" + extension;
        if (GenericShellCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        // The path handed to SHGetFileInfo is a dummy name - with
        // SHGFI_USEFILEATTRIBUTES only its extension matters and nothing is
        // read from disk.
        var icon = ExtractShellIcon("file" + extension, FILE_ATTRIBUTE_NORMAL,
            useAttributesOnly: true, openFolder: false);
        if (icon is null)
        {
            // Shell refused (rare) - fall back to the bundled set rather than
            // an empty gap, and don't cache so a transient failure can heal.
            return GetPackIcon(IconResolver.ResolveFileIcon("file" + extension));
        }

        GenericShellCache[key] = icon;
        return icon;
    }

    private static ImageSource? GetGenericFolderIcon(bool isExpanded)
    {
        string key = isExpanded ? "d:open" : "d:closed";
        if (GenericShellCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var icon = ExtractShellIcon("folder", FILE_ATTRIBUTE_DIRECTORY,
            useAttributesOnly: true, openFolder: isExpanded);
        if (icon is null)
        {
            return GetPackIcon(IconResolver.ResolveFolderIcon("folder", isExpanded));
        }

        GenericShellCache[key] = icon;
        return icon;
    }

    private static void QueuePerFileFetch(string fullPath, string extension, Action? iconChanged)
    {
        if (PendingFetches.TryGetValue(fullPath, out var waiting))
        {
            if (iconChanged is not null)
            {
                waiting.Add(iconChanged);
            }
            return;
        }

        var callbacks = new List<Action>();
        if (iconChanged is not null)
        {
            callbacks.Add(iconChanged);
        }
        PendingFetches[fullPath] = callbacks;

        Task.Run(() =>
        {
            // No SHGFI_USEFILEATTRIBUTES: this actually opens the file to get
            // ITS icon, which is the whole point - and why it's off the UI
            // thread. The result is frozen, so creating it here and reading it
            // on the UI thread later is safe.
            var icon = ExtractShellIcon(fullPath, 0, useAttributesOnly: false, openFolder: false);

            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                if (!PendingFetches.Remove(fullPath, out var toNotify))
                {
                    return;
                }

                if (PerPathCache.Count >= MaxPerPathCacheEntries)
                {
                    PerPathCache.Clear();
                }

                // On failure (file gone, network drop) the generic icon is
                // cached under the path instead, so scrolling past the same
                // file doesn't re-attempt the extraction every time. Same
                // resting state Explorer shows for it.
                var resolved = icon ?? GetGenericFileIcon(extension);
                if (resolved is not null)
                {
                    PerPathCache[fullPath] = resolved;
                }

                if (icon is not null)
                {
                    foreach (var callback in toNotify)
                    {
                        callback();
                    }
                }
            });
        });
    }

    // The one place native icon handles exist: extracted, converted, and
    // destroyed before returning. 32px (SHGFI_LARGEICON) rather than the 16px
    // small size because the app scales icons up with the tree font size
    // (Ctrl +/-), and 16px upscaled goes blurry much sooner.
    private static ImageSource? ExtractShellIcon(string path, uint fileAttributes,
        bool useAttributesOnly, bool openFolder)
    {
        var info = new SHFILEINFO();
        uint flags = SHGFI_ICON | SHGFI_LARGEICON;
        if (useAttributesOnly)
        {
            flags |= SHGFI_USEFILEATTRIBUTES;
        }
        if (openFolder)
        {
            flags |= SHGFI_OPENICON;
        }

        try
        {
            if (SHGetFileInfo(path, fileAttributes, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), flags) == IntPtr.Zero ||
                info.hIcon == IntPtr.Zero)
            {
                return null;
            }
        }
        catch (Exception e) when (e is ExternalException or ArgumentException)
        {
            return null;
        }

        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(
                info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        catch (Exception e) when (e is ExternalException or ArgumentException)
        {
            return null;
        }
        finally
        {
            DestroyIcon(info.hIcon);
        }
    }

    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_LARGEICON = 0x000000000;
    private const uint SHGFI_OPENICON = 0x000000002;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes,
        ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);
}

using System.IO;
using System.Runtime.InteropServices;

namespace SidebarExplorer.App.Services;

// Duration and frame size for a video, from the same Windows property store
// Explorer's own details pane reads - so whatever the machine can describe,
// this describes, with no dependency taken and no decoding done.
//
// Deliberately NOT the Shell.Application/GetDetailsOf route: that one returns
// LOCALIZED strings addressed by column index, and both the index and the
// text move with the Windows version and the display language.
//
// Always off the UI thread (see GetAsync): a property store on a network path
// is a disk call, and this app's own rule is that nothing blocks the UI for a
// file it is merely describing.
public static class ShellMediaInfoService
{
    public readonly record struct MediaInfo(TimeSpan? Duration, int Width, int Height)
    {
        public bool HasFrameSize => Width > 0 && Height > 0;
        public bool IsEmpty => Duration is null && !HasFrameSize;
    }

    public static void GetAsync(string path, Action<MediaInfo> onCompleted)
    {
        Task.Run(() =>
        {
            var info = Get(path);
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => onCompleted(info));
        });
    }

    public static MediaInfo Get(string path)
    {
        IPropertyStore? store = null;
        try
        {
            var iid = typeof(IPropertyStore).GUID;
            if (SHGetPropertyStoreFromParsingName(path, IntPtr.Zero, GPS_READWRITE_DEFAULT, ref iid, out store) != 0 ||
                store is null)
            {
                return default;
            }

            // System.Media.Duration is in 100-nanosecond units, the same unit
            // TimeSpan counts in.
            TimeSpan? duration = ReadUInt64(store, MediaDurationKey) is { } ticks && ticks > 0
                ? TimeSpan.FromTicks((long)ticks)
                : null;
            int width = (int)(ReadUInt32(store, VideoFrameWidthKey) ?? 0);
            int height = (int)(ReadUInt32(store, VideoFrameHeightKey) ?? 0);
            return new MediaInfo(duration, width, height);
        }
        catch (Exception e) when (e is COMException or ArgumentException or FileNotFoundException
                                      or UnauthorizedAccessException or InvalidCastException)
        {
            // No provider for this type, an unreachable path, a file that went
            // away - the caller simply shows what it already had.
            return default;
        }
        finally
        {
            if (store is not null)
            {
                Marshal.ReleaseComObject(store);
            }
        }
    }

    private static ulong? ReadUInt64(IPropertyStore store, PROPERTYKEY key)
    {
        var value = new PROPVARIANT();
        try
        {
            if (store.GetValue(ref key, out value) != 0)
            {
                return null;
            }
            return PropVariantToUInt64(ref value, out ulong result) == 0 ? result : null;
        }
        finally
        {
            PropVariantClear(ref value);
        }
    }

    private static uint? ReadUInt32(IPropertyStore store, PROPERTYKEY key)
    {
        var value = new PROPVARIANT();
        try
        {
            if (store.GetValue(ref key, out value) != 0)
            {
                return null;
            }
            return PropVariantToUInt32(ref value, out uint result) == 0 ? result : null;
        }
        finally
        {
            PropVariantClear(ref value);
        }
    }

    // The canonical keys, spelled out rather than resolved by name at runtime
    // (PSGetPropertyKeyFromName would be a second COM call per property for
    // values that have been fixed since Vista).
    private static readonly PROPERTYKEY MediaDurationKey =
        new(new Guid("64440490-4C8B-11D1-8B70-080036B11A03"), 3);   // System.Media.Duration
    private static readonly PROPERTYKEY VideoFrameWidthKey =
        new(new Guid("64440491-4C8B-11D1-8B70-080036B11A03"), 3);   // System.Video.FrameWidth
    private static readonly PROPERTYKEY VideoFrameHeightKey =
        new(new Guid("64440491-4C8B-11D1-8B70-080036B11A03"), 4);   // System.Video.FrameHeight

    private const uint GPS_READWRITE_DEFAULT = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPERTYKEY
    {
        public Guid FormatId;
        public uint PropertyId;

        public PROPERTYKEY(Guid formatId, uint propertyId)
        {
            FormatId = formatId;
            PropertyId = propertyId;
        }
    }

    // Only ever handed to the propsys helpers below, which know how to read
    // whatever variant type actually came back - so this needs to be the right
    // SIZE, not a faithful union.
    [StructLayout(LayoutKind.Sequential)]
    private struct PROPVARIANT
    {
        public ushort VarType;
        public ushort Reserved1;
        public ushort Reserved2;
        public ushort Reserved3;
        public IntPtr Value1;
        public IntPtr Value2;
    }

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PROPERTYKEY key);
        [PreserveSig] int GetValue(ref PROPERTYKEY key, out PROPVARIANT value);
        [PreserveSig] int SetValue(ref PROPERTYKEY key, ref PROPVARIANT value);
        [PreserveSig] int Commit();
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHGetPropertyStoreFromParsingName(
        string path, IntPtr bindContext, uint flags, ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IPropertyStore? propertyStore);

    [DllImport("propsys.dll", PreserveSig = true)]
    private static extern int PropVariantToUInt64(ref PROPVARIANT value, out ulong result);

    [DllImport("propsys.dll", PreserveSig = true)]
    private static extern int PropVariantToUInt32(ref PROPVARIANT value, out uint result);

    [DllImport("ole32.dll", PreserveSig = true)]
    private static extern int PropVariantClear(ref PROPVARIANT value);
}

using System.IO;
using System.Runtime.InteropServices;

namespace SidebarExplorer.App.Native;

internal static class NativeMethods
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_APPWINDOW = 0x00040000;
    private const int WS_EX_TOPMOST = 0x00000008;

    private static readonly IntPtr HwndTopmost = new(-1);
    private static readonly IntPtr HwndNoTopmost = new(-2);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;

    private const int ASFW_ANY = -1;

    // DWMWA_WINDOW_CORNER_PREFERENCE (Windows 11 22000+ only). DwmSetWindowAttribute
    // just returns a failure HRESULT on older Windows instead of throwing, so this
    // is a safe no-op there - the window keeps its already-square default look.
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_DONOTROUND = 1;
    private const int DWMWCP_ROUND = 2;

    // Forces ShellExecuteEx to go through the target's IContextMenu shortcut-menu
    // handler (the same path Explorer's own "Open" uses) instead of a bare
    // CreateProcess with the registered command line. Apps whose single-instance
    // handling depends on that shell-level DDE/negotiation - as opposed to just
    // checking on every raw launch - only respond correctly when this is set.
    private const uint SEE_MASK_INVOKEIDLIST = 0x0000000C;
    private const int SW_SHOWNORMAL = 1;

    private const uint ABM_GETSTATE = 0x00000004;
    private const uint ABM_GETTASKBARPOS = 0x00000005;
    private const int ABS_AUTOHIDE = 0x0000001;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct APPBARDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public RECT rc;
        public int lParam;
    }

    [DllImport("shell32.dll")]
    private static extern IntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

    // Which screen edge an AUTO-HIDDEN taskbar sits on, in that monitor's own
    // physical pixels - null when the taskbar is always visible.
    //
    // Worth asking because Windows does NOT subtract an auto-hidden taskbar
    // from the work area: it reports the whole screen. A window that fills
    // that work area therefore covers the strip the taskbar pops out of, and
    // the cursor arriving there lands inside the window instead of on the
    // screen edge - so the taskbar never comes up (measured 2026-07-28 on a
    // 3840x2160 screen: work area 3840x2160, taskbar rect y 2112-2160).
    //
    // Returns the taskbar's own rect too, so the caller can tell whether it
    // even belongs to the monitor being positioned on.
    internal static (int Edge, int Left, int Top, int Right, int Bottom)? GetAutoHiddenTaskbar()
    {
        var state = new APPBARDATA();
        state.cbSize = Marshal.SizeOf(state);
        if (((int)SHAppBarMessage(ABM_GETSTATE, ref state) & ABS_AUTOHIDE) == 0)
        {
            return null;
        }

        var pos = new APPBARDATA();
        pos.cbSize = Marshal.SizeOf(pos);
        if (SHAppBarMessage(ABM_GETTASKBARPOS, ref pos) == IntPtr.Zero)
        {
            return null;
        }

        return ((int)pos.uEdge, pos.rc.Left, pos.rc.Top, pos.rc.Right, pos.rc.Bottom);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int dwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("kernel32.dll")]
    private static extern uint SetErrorMode(uint mode);

    private const uint SEM_FAILCRITICALERRORS = 0x0001;
    private const uint SEM_NOOPENFILEERRORBOX = 0x8000;

    // Windows, not the app, puts up a box when a drive stops answering mid-
    // read ("network path not found", "no disk in the drive"). A sidebar that
    // keeps a NAS folder open hits that every time the NAS is switched off -
    // one flashed up during a 2026-07-26 test - and the app cannot dismiss or
    // even see it. This tells the OS to fail those calls back to us instead of
    // interrupting the user with a dialog they didn't ask for; every such call
    // here already handles failure (see FileSystemService's readFailed rule).
    public static void SuppressDeviceErrorDialogs()
        => SetErrorMode(SetErrorMode(0) | SEM_FAILCRITICALERRORS | SEM_NOOPENFILEERRORBOX);

    // The window manager's own answer, not WPF's belief about it.
    public static bool HasTopmostStyle(IntPtr hWnd)
        => (GetWindowLong(hWnd, GWL_EXSTYLE) & WS_EX_TOPMOST) != 0;

    // WPF's Topmost property writes through to the window only when its value
    // CHANGES, so a window that lost its topmost z-order behind the
    // framework's back can never be restored by assigning Topmost = true again
    // - the property already reads true. This goes straight to the window
    // manager, and re-raises the window within the topmost band as well, which
    // reading the style bit alone can't tell you is needed.
    //
    // Measured 2026-07-25: freshly launched, the window's real extended style
    // was 0x00000080 - TOOLWINDOW only, no TOPMOST - while the app believed
    // Topmost was true. Every read-modify-write of GWL_EXSTYLE here
    // (MakeToolWindow/MakeAppWindow, called at startup and at every dock
    // change) is a chance to land in that state, which is why the callers
    // re-state the intended z-order afterwards rather than trusting it to
    // survive.
    public static bool SetTopmost(IntPtr hWnd, bool topmost)
        => SetWindowPos(hWnd, topmost ? HwndTopmost : HwndNoTopmost, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

    // Clipboard change notification. Nothing else tells an app that its own
    // cut has been consumed or replaced - the 잘라내기 markers would otherwise
    // outlive the clipboard entry they stand for (2026-07-28: pasting an
    // Edgetree cut in Explorer left the row faded here forever).
    public const int WM_CLIPBOARDUPDATE = 0x031D;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private static readonly IntPtr HwndBroadcast = new(0xffff);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellExecuteInfo
    {
        public int cbSize;
        public uint fMask;
        public IntPtr hwnd;
        public string? lpVerb;
        public string? lpFile;
        public string? lpParameters;
        public string? lpDirectory;
        public int nShow;
        public IntPtr hInstApp;
        public IntPtr lpIDList;
        public string? lpClass;
        public IntPtr hkeyClass;
        public uint dwHotKey;
        public IntPtr hIcon;
        public IntPtr hProcess;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool ShellExecuteEx(ref ShellExecuteInfo executeInfo);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    public static void MakeToolWindow(IntPtr hWnd)
    {
        var exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
        exStyle |= WS_EX_TOOLWINDOW;
        exStyle &= ~WS_EX_APPWINDOW;
        SetWindowLong(hWnd, GWL_EXSTYLE, exStyle);
    }

    public static void MakeAppWindow(IntPtr hWnd)
    {
        var exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
        exStyle &= ~WS_EX_TOOLWINDOW;
        exStyle |= WS_EX_APPWINDOW;
        SetWindowLong(hWnd, GWL_EXSTYLE, exStyle);
    }

    // Shared by every Edgetree process regardless of build/version -
    // RegisterWindowMessage guarantees the OS hands back the same id
    // system-wide for the same string, so a second instance can broadcast it
    // (see BroadcastActivateMessage) and the first instance's own WndProc
    // hook (MainWindow.xaml.cs's MainWindow_SourceInitialized) recognizes it
    // without any other shared state between the two processes.
    public static readonly uint ActivateMessage =
        RegisterWindowMessage("Edgetree-Activate-8f1d6b2e-4a3f-4c9e-9b1a-2d7e5c6f8a90");

    // Broadcast rather than targeting a specific hwnd - a second instance has
    // no reliable way to find the first instance's hwnd directly (it may be
    // docked/tool-window or hidden to the tray), but every top-level window
    // on the system receives a broadcast, so the surviving instance's own
    // hook just needs to recognize its registered message id and every other
    // window on the system silently ignores it.
    public static void BroadcastActivateMessage()
    {
        PostMessage(HwndBroadcast, ActivateMessage, IntPtr.Zero, IntPtr.Zero);
    }

    // Our sidebar typically has foreground focus at the moment it launches a
    // sibling process (e.g. opening a file), and Windows' foreground-lock
    // policy then keeps that new window/dialog from activating on top - it
    // opens but stays hidden behind everything. Granting the next
    // SetForegroundWindow call permission fixes that.
    public static void AllowNextWindowToActivate()
    {
        AllowSetForegroundWindow(ASFW_ANY);
    }

    // Docked, the window is flush against the screen edge - rounded corners
    // there would just look like a gap against the taskbar/desktop. Floating,
    // it's a normal window on the desktop like any other, so it should round
    // the same way Windows 11 apps do by default.
    public static void SetWindowCornerPreference(IntPtr hWnd, bool rounded)
    {
        int preference = rounded ? DWMWCP_ROUND : DWMWCP_DONOTROUND;
        DwmSetWindowAttribute(hWnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
    }

    // Returns false if the file has no associated app (or the call otherwise
    // failed) so the caller can fall back to the "Open With" picker.
    public static bool TryOpenWithShellVerb(string path, string verb = "open")
    {
        var info = new ShellExecuteInfo
        {
            fMask = SEE_MASK_INVOKEIDLIST,
            lpVerb = verb,
            lpFile = path,
            lpDirectory = Path.GetDirectoryName(path),
            nShow = SW_SHOWNORMAL
        };
        info.cbSize = Marshal.SizeOf(info);

        return ShellExecuteEx(ref info);
    }
}

using System.IO;
using System.Runtime.InteropServices;

namespace SidebarExplorer.App.Native;

internal static class NativeMethods
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_APPWINDOW = 0x00040000;

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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int dwProcessId);

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

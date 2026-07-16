using System.Threading;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;
using SidebarExplorer.App.Native;
using SidebarExplorer.App.Services;

namespace SidebarExplorer.App;

public partial class App : Application
{
    private NotifyIcon? _trayIcon;

    // Held for the app's whole lifetime (a field, not a local) so it isn't
    // released early by the GC - see OnStartup/OnExit.
    private Mutex? _singleInstanceMutex;

    // Minimize-to-tray (MainWindow's "_" button calls Hide(), not Close()) needs
    // some way back - so the icon stays visible regardless of the "always show
    // tray icon" setting whenever the window is currently hidden.
    public bool IsTrayIconVisible
    {
        get => _trayIcon?.Visible ?? false;
        set
        {
            if (_trayIcon is not null)
            {
                _trayIcon.Visible = value;
            }
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        // Named (not per-version) so an old build and a freshly built one
        // still see each other as the same app - the whole point is blocking
        // duplicate launches regardless of which exe/version is running.
        _singleInstanceMutex = new Mutex(true, "Local\\Edgetree-SingleInstance-8f1d6b2e-4a3f-4c9e-9b1a-2d7e5c6f8a90", out bool createdNew);
        if (!createdNew)
        {
            // Another Edgetree process already holds the mutex - ask it to
            // come to the foreground instead of opening a second window, and
            // exit before constructing anything (window, tray icon, Strings)
            // so there's no flicker. MainWindow_SourceInitialized's WndProc
            // hook is what receives this on the other end.
            NativeMethods.BroadcastActivateMessage();
            Environment.Exit(0);
        }

        // Must run before base.OnStartup(e) - that call is what actually
        // constructs the StartupUri (MainWindow) window, and every x:Static
        // Strings.* reference in its XAML resolves to whatever's in these
        // fields at that exact moment.
        Strings.Initialize(new SettingsService().Load().Language);

        base.OnStartup(e);

        var iconUri = new Uri("pack://application:,,,/Resources/app.ico");
        using var iconStream = GetResourceStream(iconUri)!.Stream;

        _trayIcon = new NotifyIcon
        {
            Icon = new System.Drawing.Icon(iconStream),
            Visible = true,
            Text = "Edgetree"
        };
        _trayIcon.MouseClick += TrayIcon_MouseClick;

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(Strings.TrayOpen, null, (_, _) => RestoreMainWindow());
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(Strings.TrayExit, null, (_, _) => Shutdown());
        _trayIcon.ContextMenuStrip = contextMenu;
    }

    private void TrayIcon_MouseClick(object? sender, System.Windows.Forms.MouseEventArgs e)
    {
        if (e.Button == System.Windows.Forms.MouseButtons.Left)
        {
            RestoreMainWindow();
        }
    }

    public void RestoreMainWindow()
    {
        if (MainWindow is not { } window)
        {
            return;
        }

        window.Show();
        window.WindowState = WindowState.Normal;
        window.Activate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}

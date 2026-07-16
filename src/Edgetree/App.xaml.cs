using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;
using SidebarExplorer.App.Services;

namespace SidebarExplorer.App;

public partial class App : Application
{
    private NotifyIcon? _trayIcon;

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
        base.OnExit(e);
    }
}

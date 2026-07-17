using System.Threading;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;
using SidebarExplorer.App.Native;
using SidebarExplorer.App.Services;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

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

    // General app "chrome" (menus/context menus/the Color Settings and About
    // dialogs, header icons) - deliberately separate from AppSettings' 15
    // user-customizable tree colors (see App.xaml's own comment). Called from
    // MainWindow.ApplyColorSettings, which already runs at startup and on
    // every color/theme change, so this needs no separate wiring of its own.
    // Dark values match what used to be hardcoded throughout MainWindow.xaml/
    // ColorSettingsWindow.xaml/AboutWindow.xaml before this existed - a
    // hand-picked light palette for the other side, same reasoning as the
    // tree's own light palette (not a mathematical inversion).
    public void ApplyChromeTheme(bool isLightMode)
    {
        if (isLightMode)
        {
            SetChromeBrush("ForegroundText", "#FF3B3B3B");
            SetChromeBrush("HighlightForeground", "#FF000000");
            SetChromeBrush("HoverBackground", "#FFE8E8E8");
            SetChromeBrush("SecondaryForeground", "#FF6E6E6E");
            SetChromeBrush("PanelBackground", "#FFFFFFFF");
            SetChromeBrush("PanelBorder", "#FFD4D4D4");
            SetChromeBrush("SeparatorBrush", "#26000000");
            SetChromeBrush("ControlBackground", "#FFECECEC");
            SetChromeBrush("ControlBorder", "#FFC0C0C0");
            SetChromeBrush("ControlHoverBackground", "#FFDCDCDC");
            SetChromeBrush("ControlHoverBorder", "#FFA0A0A0");
            SetChromeBrush("DialogBackground", "#FFFFFFFF");
            SetChromeBrush("DialogHeaderBackground", "#FFF3F3F3");
            SetChromeBrush("DialogForeground", "#FF1E1E1E");
            SetChromeBrush("AccentForeground", "#FF0969DA");
        }
        else
        {
            SetChromeBrush("ForegroundText", "#FFA8AAAE");
            SetChromeBrush("HighlightForeground", "#FFF0F2F6");
            SetChromeBrush("HoverBackground", "#FF2A2D2E");
            SetChromeBrush("SecondaryForeground", "#FF9A9A9A");
            SetChromeBrush("PanelBackground", "#FF282828");
            SetChromeBrush("PanelBorder", "#FF454545");
            SetChromeBrush("SeparatorBrush", "#26FFFFFF");
            SetChromeBrush("ControlBackground", "#FF3C3C3C");
            SetChromeBrush("ControlBorder", "#FF5A5A5A");
            SetChromeBrush("ControlHoverBackground", "#FF505050");
            SetChromeBrush("ControlHoverBorder", "#FF7A7A7A");
            SetChromeBrush("DialogBackground", "#FF252526");
            SetChromeBrush("DialogHeaderBackground", "#FF2D2D2D");
            SetChromeBrush("DialogForeground", "#FFCCCCCC");
            SetChromeBrush("AccentForeground", "#FF4FA8FF");
        }
    }

    private void SetChromeBrush(string resourceKey, string hex)
    {
        if (ColorConverter.ConvertFromString(hex) is Color color)
        {
            Resources[resourceKey] = new SolidColorBrush(color);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}

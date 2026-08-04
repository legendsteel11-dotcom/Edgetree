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
    private ToolStripMenuItem? _trayToggleItem;

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

        // BeginSession first: it stamps the post-mortem of the previous
        // session, which reads better above this session's own start line.
        ExitLog.BeginSession();
        ExitLog.Record($"--- started (pid {Environment.ProcessId})");

        // Before anything touches a drive - see the method's own note.
        NativeMethods.SuppressDeviceErrorDialogs();

        // Windows signing the user out or shutting down closes the app without
        // any click, and looks exactly like "it just disappeared" afterwards.
        SessionEnding += (_, args) => ExitLog.Record($"windows session ending ({args.ReasonSessionEnding})");

#if DEBUG
        // Neither handler swallows anything (Handled stays false) - they only
        // get the exception written down before the process goes, since a
        // crash on a background thread can otherwise leave nothing behind.
        DispatcherUnhandledException += (_, args) =>
            ExitLog.Record($"UNHANDLED (ui thread): {args.Exception}");
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            ExitLog.Record($"UNHANDLED (background): {args.ExceptionObject}");
#endif

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
        _trayToggleItem = new ToolStripMenuItem(Strings.TrayOpen, null, (_, _) => ToggleMainWindowTray());
        contextMenu.Items.Add(_trayToggleItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(Strings.TrayAbout, null, (_, _) => ShowAboutCentered());
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(Strings.TrayExit, null, (_, _) =>
        {
            ExitLog.Record("tray menu: exit");
            Shutdown();
        });

        // The window can be shown/hidden by other means (title bar "_"
        // button, restoring from taskbar) between menu openings, so the
        // toggle item's label is refreshed right before it's actually shown
        // rather than once at construction time.
        contextMenu.Opening += (_, _) => UpdateTrayToggleItem();
        _trayIcon.ContextMenuStrip = contextMenu;
    }

    private void TrayIcon_MouseClick(object? sender, System.Windows.Forms.MouseEventArgs e)
    {
        if (e.Button == System.Windows.Forms.MouseButtons.Left)
        {
            RestoreMainWindow();
        }
    }

    private void UpdateTrayToggleItem()
    {
        if (_trayToggleItem is null)
        {
            return;
        }

        bool isWindowVisible = MainWindow is { IsVisible: true };
        _trayToggleItem.Text = isWindowVisible ? Strings.TrayHide : Strings.TrayOpen;
    }

    // Same open/hide split the title bar's own "_" button and tray click use
    // (see MainWindow.MinimizeButton_Click / RestoreMainWindow) - just picks
    // which of the two applies based on current visibility, per the TODO
    // request that this read "트레이로/닫기" instead of always "열기/닫기".
    private void ToggleMainWindowTray()
    {
        if (MainWindow is not { } window)
        {
            return;
        }

        if (window.IsVisible)
        {
            // Same persist-before-hiding rule as the title bar's "_" button -
            // see MainWindow.MinimizeButton_Click for why.
            (window as MainWindow)?.SaveStateBeforeHiding();
            window.Hide();
            IsTrayIconVisible = true;
        }
        else
        {
            RestoreMainWindow();
        }
    }

    // Opened from the tray, where the window this would normally be
    // positioned relative to (the options button) may itself be hidden - so
    // this centers on the screen instead, per the TODO request, rather than
    // reusing MainWindow.PositionNearOptionsButton.
    private void ShowAboutCentered()
    {
        var window = new AboutWindow((MainWindow as MainWindow)?.UpdateAvailableVersion)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Owner = MainWindow
        };
        window.ShowDialog();
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
            // Menu text runs ~10% DARKER than ForegroundText in light mode
            // (and ~10% brighter in dark mode below) - user call 2026-07-21:
            // menu labels wanted a touch more presence than the shared chrome
            // color in both themes, which "more" means toward-black on white
            // and toward-white on dark. Split from ForegroundText so headers/
            // footers sharing that brush stay as they were.
            SetChromeBrush("MenuForeground", "#FF353535");
            SetChromeBrush("MenuDisabledForeground", "#FF5F5F5F");
            SetChromeBrush("HighlightForeground", "#FF000000");
            SetChromeBrush("HoverBackground", "#FFE8E8E8");
            // A step past HoverBackground, because a menu's background is the
            // panel white behind it rather than the dialog grey - see the dark
            // value below for the case that forced this brush to exist.
            SetChromeBrush("MenuHighlightBackground", "#FFDCDCDC");
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
            // ForegroundText #A8AAAE and the old hardcoded disabled #6A6A6A,
            // each with RGB scaled by ~1.1 - see the light-mode comment.
            SetChromeBrush("MenuForeground", "#FFB9BBBF");
            SetChromeBrush("MenuDisabledForeground", "#FF757575");
            SetChromeBrush("HighlightForeground", "#FFF0F2F6");
            SetChromeBrush("HoverBackground", "#FF2A2D2E");
            // HoverBackground is two points off the menu background below
            // (#282A2C) and vanished against it once menus stopped borrowing
            // the tree's selection colour. Matched instead to how plainly a
            // tree row answers the pointer.
            SetChromeBrush("MenuHighlightBackground", "#FF3A3D41");
            SetChromeBrush("SecondaryForeground", "#FF9A9A9A");
            // RGB 40/42/44 (user-picked 2026-07-21, was a flat #282828): a
            // hint of blue-grey so menus separate from the tree behind them.
            SetChromeBrush("PanelBackground", "#FF282A2C");
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
        // The catch-all: whatever reason was (or wasn't) stamped above, this
        // is the process actually going away.
        ExitLog.Record($"process exiting (code {e.ApplicationExitCode})");

        // Reaching here at all is what makes this exit "clean" - dropping the
        // heartbeat is how the next launch knows not to cry foul.
        ExitLog.EndSession();

        _trayIcon?.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}

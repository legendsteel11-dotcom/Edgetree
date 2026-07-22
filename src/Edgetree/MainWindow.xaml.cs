using System.IO;
using System.Reflection;
using System.Text.Json;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using SidebarExplorer.App.Models;
using SidebarExplorer.App.Native;
using SidebarExplorer.App.Services;
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Controls.TextBox;
using Button = System.Windows.Controls.Button;
using Application = System.Windows.Application;
using DragDrop = System.Windows.DragDrop;
using DataObject = System.Windows.DataObject;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using DragEventArgs = System.Windows.DragEventArgs;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace SidebarExplorer.App;

public partial class MainWindow : Window
{
    private const double MinExpandedWidth = 180;
    private const double MaxExpandedWidth = 1200;
    private const int ToggleAnimationMs = 200;
    // Topped out at 16 until a user asked for larger text for presbyopia; the
    // extra steps go to 20, which is where a docked sidebar of realistic width
    // still holds a useful amount of a file name. Everything that matters
    // scales off this (see ApplyLayoutMetrics: icons, row padding, indent
    // margins), so the extra steps needed no other layout work.
    private static readonly double[] TreeFontSizeSteps = { 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 };
    private const double DefaultTreeFontSize = 12;
    private const double HeaderHeight = 36;
    private const double FloatingResizeBorder = 6;
    private const double DefaultFloatingHeight = 600;
    private const double UndockCornerOffset = 40;
    private const int AutoHideRehideDelayMs = 400;

    private readonly SettingsService _settingsService = new();
    private AppSettings _settings = new();
    private List<FileSystemItem> _roots = new();
    private bool _isDocked = true;

    // Remembers the floating window's own bounds across a dock -> undock
    // round trip within the same run (null until the first time this app
    // instance goes floating). Without this, every Undock() recomputed a
    // fresh default position/size from scratch, discarding whatever the user
    // had just dragged/resized it to before docking it again.
    private double? _floatingLeft;
    private double? _floatingTop;
    private double? _floatingWidth;
    private double? _floatingHeight;

    // Whether auto-hide is currently peeked open (see MainWindow_MouseEnter/
    // Leave) - transient, not persisted; a fresh launch always starts
    // re-hidden to the sliver even if _settings.IsAutoHidden was saved true.
    private bool _isAutoHideRevealed;
    private System.Windows.Threading.DispatcherTimer? _autoHideRehideTimer;

    // Non-null while "Collapse All" has collapsed the tree via
    // CollapseAllButton_Click - holds the paths that were expanded right
    // before, so clicking the button again restores exactly that state
    // instead of just collapsing an already-collapsed tree. Cleared back to
    // null once restored.
    private List<string>? _collapseAllRestorePaths;

    // Non-null right after double-clicking the resize thumb has fit the
    // window to its widest currently-realized row - holds the width from
    // just before that, so double-clicking again restores it instead of
    // re-fitting an already-fitted window. _contentFitWidthApplied is the
    // width the fit itself actually set, checked against the window's
    // current Width at the next double-click (see
    // ResizeThumb_MouseDoubleClick) - if anything moved Width since (a
    // manual drag, or anything else that changes it), the toggle is stale
    // and that click fits fresh instead of jumping back to an old value.
    // Deliberately not keyed off DragDelta specifically: that only catches
    // this exact Thumb's own drag gesture, and would miss any other way
    // Width might change between the fit and the next double-click. Not
    // persisted - a transient in-session gesture like
    // _collapseAllRestorePaths above, not a setting.
    private double? _contentFitRestoreWidth;
    private double? _contentFitWidthApplied;

    // Set right before shutting down for a settings reset, so
    // MainWindow_Closing skips SaveCurrentWidth - otherwise it would
    // overwrite the freshly-saved defaults with the still-live pre-reset
    // window width/expanded folders/selection on its way out.
    private bool _settingsResetPending;

    private System.Windows.Point? _headerDragStart;
    private FileSystemItem? _selectedItem;
    private bool _isNavigatingFromFavorite;
    private System.Windows.Point? _itemDragStart;
    private FileSystemItem? _itemDragCandidate;


    // Explorer-style "slow double-click" rename: a second, separate click on an
    // already-selected file starts an inline rename, but only once enough time
    // has passed to rule out a real double-click (which opens the file). Files
    // only - see the scheduling call in TreeViewItem_PreviewMouseLeftButtonDown.
    private System.Windows.Threading.DispatcherTimer? _pendingRenameTimer;
    private FileSystemItem? _pendingRenameItem;

    // Bumped once at the start of every favorites navigation (NavigateToPath),
    // and nowhere else - notably NOT on plain selection changes, which the walk
    // itself triggers (see ExplorerTree_SelectedItemChanged). RevealChainStep
    // captures the value current as of its own walk and checks it before each
    // step (including deferred container-wait retries - see its comment), so a
    // still-in-progress walk superseded by a newer favorite click stops instead
    // of the two interleaving.
    private int _navigationToken;

    // Debounces a changed folder's live-refresh (see StartDriveWatchers) - a
    // save/copy operation fires several watcher events in quick succession
    // for what's really one logical change, so each pending folder path gets
    // one timer that keeps getting restarted until events stop arriving for
    // it. Keyed by path, not by FileSystemItem: the watcher event only ever
    // gives a path string, and the item it refers to might not even be
    // loaded yet.
    private readonly Dictionary<string, PendingExternalRefresh> _pendingExternalRefreshes = new();

    // The debounce timer for one folder, plus when that folder's most recent
    // change was reported - the timestamp is what lets the tick tell a listing
    // that predates the change from one already re-read after it (see
    // QueueExternalRefresh).
    private sealed class PendingExternalRefresh(System.Windows.Threading.DispatcherTimer timer)
    {
        public System.Windows.Threading.DispatcherTimer Timer { get; } = timer;
        public long LastEventTicks { get; set; } = Environment.TickCount64;
    }

    // One recursive watcher per drive root (see StartDriveWatchers), covering
    // every folder on that drive regardless of expand state - NOT one
    // watcher per expanded folder, which an earlier version of this used and
    // which scaled directly with how many folders someone had open. That
    // turned out to noticeably worsen resize-drag flicker with a large
    // number of folders expanded, presumably from the sheer count of live
    // OS-level watcher handles/thread-pool callbacks competing for the UI
    // thread during a fast-repeating operation - a handful of recursive
    // watchers (one per drive, however many folders are open) doesn't have
    // that scaling problem.
    private readonly List<FileSystemWatcher> _driveWatchers = new();

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += MainWindow_SourceInitialized;
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        SizeChanged += MainWindow_SizeChanged;
        SystemParameters.StaticPropertyChanged += SystemParameters_StaticPropertyChanged;
        SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
    }

    // Defensive - not a confirmed fix for any specific reported symptom, just
    // cheap insurance against a real risk this app's own design carries: a
    // Topmost + tool-window (hidden from Alt+Tab and the taskbar - see
    // MakeToolWindow) auto-hide sliver is exactly the shape of window that,
    // if a sleep/resume graphics-driver reset leaves it "up and still
    // catching clicks but not actually rendering" (a known category of WPF
    // issue, worse with Topmost), becomes invisible AND unreachable by any
    // normal means - no taskbar entry, no Alt+Tab entry, just something
    // silently eating clicks until Task Manager kills it. Re-snapping the
    // position (in case the monitor/work-area changed while asleep - the
    // existing SystemParameters_StaticPropertyChanged handler only ever
    // catches that for the primary monitor's own WorkArea) and cycling
    // Topmost off/on (nudging DWM to recompose it) on every resume costs
    // nothing and can only help.
    private void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode != PowerModes.Resume)
        {
            return;
        }

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_isDocked)
            {
                PositionToWorkArea();
            }
            if (Topmost)
            {
                Topmost = false;
                Topmost = true;
            }
        }));
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.MakeToolWindow(hwnd);

        // A second Edgetree launch (any build/version) broadcasts this
        // instead of opening its own window - see App.OnStartup - so this
        // instance can come to the foreground the same way the tray icon's
        // "Open" does, regardless of whether it's currently docked or
        // hidden to the tray.
        HwndSource.FromHwnd(hwnd).AddHook(SingleInstanceWndProc);
    }

    private IntPtr SingleInstanceWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == unchecked((int)NativeMethods.ActivateMessage))
        {
            (Application.Current as App)?.RestoreMainWindow();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void MainWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        VersionFooterText.Text = $"Edgetree v{version}";

        _settings = _settingsService.Load();
        FileSystemService.SortField = _settings.SortByDate ? FileSortField.Date : FileSortField.Name;
        FileSystemService.SortDescending = _settings.SortDescending;
        FileSystemItem.DisplayCap = Math.Clamp(_settings.MaxItemsPerFolder, 1, 50);

        // Must be set before the tree/favorites below ever read an icon, same
        // as the sort/display statics above.
        ShellIconService.UseShellIcons = _settings.UseShellIcons;
        Resources["FavoriteFolderIconSource"] = ShellIconService.GetFavoritesFolderIcon();

        FileSystemService.SortOverrides.Clear();
        foreach (var entry in _settings.FolderSortOverrides)
        {
            FileSystemService.SortOverrides[FileSystemService.NormalizeSortOverridePath(entry.Path)] =
                new FolderSortOverride(entry.SortByDate ? FileSortField.Date : FileSortField.Name, entry.SortDescending);
        }

        // Re-applies the Run key every launch rather than only the moment the
        // option is toggled - the previous behavior left a stale registration
        // (wrong exe path) in place forever after the exe was moved/rebuilt,
        // since nothing ever re-wrote it until the user happened to flip the
        // checkbox off and back on. Quiet on failure (unlike
        // StartWithWindowsMenuItem_Click's SetStartWithWindows) - a policy
        // that blocks the Run key shouldn't pop a warning on every single
        // launch, just the one time the user actively tries to turn this on.
        try
        {
            TrySetStartWithWindows(_settings.StartWithWindows);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
        }

        // Before SetExpandedContentVisibility below - it already sizes
        // whichever row currently hosts favorites (via FavoritesRowDef,
        // which reads _settings.FavoritesAtBottom), so the controls
        // themselves need to already be in the right Grid.Row by then, not
        // just left at their XAML-default (top) positions.
        ApplyFavoritesPosition();

        Width = _settings.IsAutoHidden ? AutoHideSliverWidth : ClampExpandedWidth(_settings.ExpandedWidth);
        ApplyHeaderMetrics();
        SetExpandedContentVisibility(_settings.IsAutoHidden ? Visibility.Collapsed : Visibility.Visible);
        PositionToWorkArea();
        UpdateResizeThumbVisibility();

        ExplorerTree.FontSize = TreeFontSizeSteps.Contains(_settings.TreeFontSize)
            ? _settings.TreeFontSize
            : DefaultTreeFontSize;

        _roots = FileSystemService.GetDriveRoots();
        ExplorerTree.ItemsSource = _roots;
        StartDriveWatchers();

        // Row sizing for the current favorite count/collapsed state was
        // already handled above by SetExpandedContentVisibility.
        FavoritesList.ItemsSource = _settings.Favorites;

        InitializeSearch();

        Topmost = _settings.AlwaysOnTop || _settings.IsAutoHidden;
        if (Application.Current is App app)
        {
            app.IsTrayIconVisible = _settings.AlwaysShowTrayIcon;
        }

        ApplyColorSettings();
        ApplyFolderIconVisibility();
        ApplyFileIconVisibility();
        ApplyTitleTextVisibility();

        // Deferred rather than done inline above: restoring possibly many
        // expanded folders plus the last selection means synchronous disk
        // I/O per folder (EnsureChildrenLoaded), which - done inline here -
        // blocked this handler from returning, and with it the window from
        // ever painting its first frame, until all of it finished. Queuing
        // it at Background priority instead lets the window actually show
        // up (drive roots visible, just not yet re-expanded) before that
        // work runs, rather than reading as a startup freeze/flicker.
        Dispatcher.BeginInvoke(RestoreTreeState, System.Windows.Threading.DispatcherPriority.Background);

        StartStuckCaptureWatchdog();

        // Resuming from sleep and changing the display layout both make Windows
        // rebuild the surfaces WPF renders onto. The reported symptom is rows
        // vanishing from the middle of the tree after the app has been up a
        // long time, reappearing the moment anything is clicked or moved - i.e.
        // the data is intact and only the drawing is missing, and the thing
        // that restores it is a layout pass. The user resumes from sleep
        // constantly and does NOT see this while scrolling, which points here
        // rather than at container recycling.
        //
        // These events arrive on a system thread, hence the marshal.
        Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerModeChanged;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        _ = CheckForUpdateOnceAsync();
    }

    // Display-only update check, ONCE per process start: asks GitHub for the
    // latest release tag and, if it's newer than this build, lights the small
    // dot on the options button and notes the version in its tooltip. No
    // download, no prompt, no retry, no periodic re-check - and any failure
    // (offline, rate-limited, API shape change) silently means "no dot this
    // run", never an error surface. One unauthenticated API call per launch
    // is far inside GitHub's 60/hour-per-IP limit.
    private async Task CheckForUpdateOnceAsync()
    {
        try
        {
            using var http = new System.Net.Http.HttpClient();
            http.Timeout = TimeSpan.FromSeconds(10);
            // GitHub's API rejects requests without a User-Agent outright.
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Edgetree");

            string json = await http.GetStringAsync(
                "https://api.github.com/repos/legendsteel11-dotcom/Edgetree/releases/latest");

            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("tag_name", out var tagElement) is false ||
                tagElement.GetString() is not { } tag ||
                !Version.TryParse(tag.TrimStart('v', 'V'), out var latest))
            {
                return;
            }

            var current = Assembly.GetExecutingAssembly().GetName().Version;
            if (current is null || latest <= new Version(current.Major, current.Minor, current.Build))
            {
                return;
            }

            // Still on the UI thread here - no ConfigureAwait(false) above,
            // deliberately, so these touch the controls safely.
            UpdateAvailableDot.Visibility = Visibility.Visible;
            OptionsButton.ToolTip =
                $"{Strings.ToolTipOptions} — {string.Format(Strings.ToolTipUpdateAvailable, "v" + latest)}";
        }
        catch (Exception e) when (e is System.Net.Http.HttpRequestException
            or TaskCanceledException
            or System.Text.Json.JsonException
            or InvalidOperationException
            or FormatException)
        {
            // 표시만 - a check that can't complete simply doesn't show a dot.
        }
    }

    private void OnPowerModeChanged(object sender, Microsoft.Win32.PowerModeChangedEventArgs e)
    {
        if (e.Mode == Microsoft.Win32.PowerModes.Resume)
        {
            Dispatcher.BeginInvoke(() => ForceTreeRedraw("resume"),
                System.Windows.Threading.DispatcherPriority.Background);
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
        => Dispatcher.BeginInvoke(() => ForceTreeRedraw("display-change"),
            System.Windows.Threading.DispatcherPriority.Background);

    // Re-runs measure/arrange on the tree and its panels. Cheap - it costs one
    // layout pass over what is on screen, and only after events that happen
    // seconds apart at most.
    private void ForceTreeRedraw(string reason)
    {
        ExplorerTree.InvalidateMeasure();
        ExplorerTree.InvalidateArrange();
        ExplorerTree.InvalidateVisual();
        ExplorerTree.UpdateLayout();

        FavoritesList.InvalidateMeasure();
        FavoritesList.InvalidateArrange();
        FavoritesList.UpdateLayout();

        LogRedraw(reason);
    }

    // Debug only, same purpose as the capture watchdog's log: if rows still go
    // missing, this says whether a resume or display change had just happened -
    // which either confirms the theory above or rules it out and points the
    // next attempt somewhere else.
    [System.Diagnostics.Conditional("DEBUG")]
    private static void LogRedraw(string reason)
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Edgetree");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, "redraw.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  forced redraw after {reason}{Environment.NewLine}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    private void RestoreTreeState()
    {
        // Restore whichever folders (including drive roots) were expanded
        // when the app last closed - shallowest first, same convention
        // RefreshAllLoadedFolders uses, since a parent has to be walked
        // (EnsureChildrenLoaded) before a deeper path is even reachable.
        // Missing paths (drive unplugged, folder deleted/renamed) are just
        // skipped - see ExpandPathIfPossible/FindItemForPath.
        foreach (var path in _settings.ExpandedFolderPaths.OrderBy(p => p.Length))
        {
            ExpandPathIfPossible(path);
        }
        if (_settings.LastSelectedPath is { } lastSelectedPath)
        {
            NavigateToPath(lastSelectedPath);
        }

        // Nothing restored expanded means the title bar's collapse toggle has
        // nothing to collapse - start it greyed out. (Each restored expansion
        // above already raised TreeViewItem.Expanded, so this is really only
        // deciding the empty case; it's called unconditionally anyway rather
        // than relying on that ordering.)
        UpdateCollapseAllButtonState();
    }

    // Replaces the resource dictionary entries with brand-new brushes, rather
    // than mutating the existing ones' Color in place: every Style in
    // MainWindow.xaml that references these five keys gets sealed the first
    // time it's applied, and WPF freezes any Freezable (including a shared
    // brush) reachable from a sealed Style's Setters - so the brush objects
    // are already read-only by the time this first runs. Every XAML reference
    // to these five keys uses DynamicResource (not StaticResource) precisely
    // so replacing the dictionary entry here is picked up live. Called once
    // at startup and again by ColorSettingsWindow after each pick/reset.
    public void ApplyColorSettings()
    {
        bool light = _settings.IsLightMode;

        // Mirrors the light/dark toggle for FormatSortOverrideIconUri, and
        // refreshes the icon on every already-loaded folder that currently
        // has a sort override - those were computed once (at construction or
        // last override change) and otherwise wouldn't notice a theme flip
        // that happened afterward.
        FileSystemService.IsLightMode = light;
        foreach (var root in _roots)
        {
            RefreshSortOverrideIconForTheme(root);
        }

        // Menus/context menus/the Color Settings and About dialogs/header
        // icons - general chrome, not part of the 15 colors below.
        (Application.Current as App)?.ApplyChromeTheme(light);

        // Menu/context-menu drop shadow (see MenuDropShadow's own comment in
        // the XAML) - the same dark, fairly strong shadow read as too heavy
        // against a light-mode menu's white background, so light mode gets a
        // softer one (lower opacity) instead of just reusing the dark value.
        Resources["MenuDropShadow"] = new DropShadowEffect
        {
            Color = Colors.Black,
            Direction = 270,
            ShadowDepth = 2,
            BlurRadius = 8,
            Opacity = light ? 0.15 : 0.4
        };

        SetBrushColor("SidebarBackground", light ? _settings.LightBackgroundColorHex : _settings.BackgroundColorHex);
        SetBrushColor("FolderNameForeground", light ? _settings.LightFolderNameColorHex : _settings.FolderNameColorHex);
        SetBrushColor("FolderNameHighlightForeground", light ? _settings.LightFolderNameHighlightColorHex : _settings.FolderNameHighlightColorHex);
        SetBrushColor("FileNameForeground", light ? _settings.LightFileNameColorHex : _settings.FileNameColorHex);
        SetBrushColor("FileNameHighlightForeground", light ? _settings.LightFileNameHighlightColorHex : _settings.FileNameHighlightColorHex);
        SetBrushColor("TreeRowSelectedActiveBackground", light ? _settings.LightSelectionColorHex : _settings.SelectionColorHex);
        SetBrushColor("FavoritesBackground", light ? _settings.LightHistoryBackgroundColorHex : _settings.HistoryBackgroundColorHex);
        SetBrushColor("TreeRowHoverBackground", light ? _settings.LightHoverBackgroundColorHex : _settings.HoverBackgroundColorHex);
        SetBrushColor("FolderNameHoverForeground", light ? _settings.LightFolderNameHoverColorHex : _settings.FolderNameHoverColorHex);
        SetBrushColor("FileNameHoverForeground", light ? _settings.LightFileNameHoverColorHex : _settings.FileNameHoverColorHex);
        SetBrushColor("ShowMoreForeground", light ? _settings.LightShowMoreColorHex : _settings.ShowMoreColorHex);
        SetBrushColor("TreeGuideLineBrush", light ? _settings.LightGuideLineColorHex : _settings.GuideLineColorHex);
        SetBrushColor("TreeGuideLineActiveBrush", light ? _settings.LightGuideLineActiveColorHex : _settings.GuideLineActiveColorHex);
        SetBrushColor("PanelDividerBrush", light ? _settings.LightPanelDividerColorHex : _settings.PanelDividerColorHex);
        SetBrushColor("HeaderBackground", light ? _settings.LightHeaderBackgroundColorHex : _settings.HeaderBackgroundColorHex);

        // The results sort button's icon has its own light/dark variants (same
        // as the folder override icon) - re-resolve it now that IsLightMode
        // above reflects the current theme. ApplyColorSettings only ever runs
        // from Loaded onward (after InitializeComponent), so the element exists.
        UpdateSearchSortIcon();
    }

    private void SetBrushColor(string resourceKey, string hex)
    {
        if (ColorConverter.ConvertFromString(hex) is Color color)
        {
            Resources[resourceKey] = new SolidColorBrush(color);
        }
    }

    // Every directory's SortOverrideIconUri (override or, absent one, the
    // global-default preview - see FileSystemItem's constructor) is a plain
    // cached string computed once, so a theme flip alone wouldn't otherwise
    // touch an already-realized instance - this walks every already-loaded
    // folder and recomputes it fresh, mirroring the same override-or-global
    // resolution the constructor itself does.
    private static void RefreshSortOverrideIconForTheme(FileSystemItem item)
    {
        if (item.IsDirectory)
        {
            if (FileSystemService.SortOverrides.TryGetValue(
                FileSystemService.NormalizeSortOverridePath(item.FullPath), out var over))
            {
                item.SortOverrideIconUri = FileSystemService.FormatSortOverrideIconUri(over.Field, over.Descending);
                item.SortOverrideTooltip = FileSystemService.FormatSortTooltip(over.Field, over.Descending);
            }
            else
            {
                item.SortOverrideIconUri = FileSystemService.NoSortOverrideIconUri;
                item.SortOverrideTooltip = FileSystemService.NoSortOverrideTooltip;
            }
        }

        if (item.ChildrenLoaded)
        {
            foreach (var child in item.Children)
            {
                RefreshSortOverrideIconForTheme(child);
            }
        }
    }

    private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // Ctrl+"+" is very often physically Ctrl+Shift+= on standard keyboards
        // (the unshifted key produces "="); requiring an exact Modifiers match
        // rejected that combination outright, so just require Control to be down.
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            return;
        }

        switch (e.Key)
        {
            case Key.OemPlus or Key.Add:
                StepTreeFontSize(+1);
                e.Handled = true;
                break;
            case Key.OemMinus or Key.Subtract:
                StepTreeFontSize(-1);
                e.Handled = true;
                break;
            case Key.D0 or Key.NumPad0:
                SetTreeFontSize(DefaultTreeFontSize);
                e.Handled = true;
                break;
            case Key.F:
                SetSearchViewActive(true);
                e.Handled = true;
                break;
            case Key.E:
                SetSearchViewActive(false);
                e.Handled = true;
                break;
        }
    }

    private void StepTreeFontSize(int direction)
    {
        int currentIndex = Array.IndexOf(TreeFontSizeSteps, ExplorerTree.FontSize);
        if (currentIndex < 0)
        {
            currentIndex = Array.IndexOf(TreeFontSizeSteps, DefaultTreeFontSize);
        }
        int newIndex = Math.Clamp(currentIndex + direction, 0, TreeFontSizeSteps.Length - 1);
        SetTreeFontSize(TreeFontSizeSteps[newIndex]);
    }

    private void SetTreeFontSize(double size)
    {
        ExplorerTree.FontSize = size;
        _settings.TreeFontSize = size;
        ApplyLayoutMetrics();

        // FavoriteRowHeight scales with ExplorerTree.FontSize (see its own
        // comment), so a fitted favorites panel needs re-fitting to the new
        // row height too - same treatment AddFavorite_Click already gives a
        // newly-added favorite, just triggered by a font-size change instead
        // of a count change.
        FitFavoritesPanel();
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_settingsResetPending)
        {
            SaveCurrentWidth();
        }

        // Stop a search scan's background walk promptly rather than letting it
        // keep doing disk I/O during shutdown.
        _searchScanCts?.Cancel();

        // SystemEvents holds these statically, so an unhooked handler keeps
        // this window alive for the life of the process.
        Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;

        foreach (var watcher in _driveWatchers)
        {
            watcher.Dispose();
        }
    }

    // A scaling change - Settings > Display, or dragging onto a monitor at a
    // different scale - leaves a docked window sized for the scale it left.
    // Height is stored in logical units, so one computed under 150% keeps that
    // number at 100%, where it now covers two thirds of the screen. Undocking
    // and re-docking fixed it only because Dock() recomputes from scratch.
    //
    // The WorkArea handler below doesn't cover this: that notification can
    // arrive before this window's own DPI has finished changing, so positioning
    // from there would recompute using the scale being left behind. Deferred to
    // Background priority for the same reason - by then VisualTreeHelper.GetDpi
    // (see GetCurrentMonitorWorkArea) reports the new scale.
    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);

        // Same rule as the WorkArea handler: a floating window is where the
        // user put it, and must not be snapped back to an edge.
        if (!_isDocked)
        {
            return;
        }

        // Deferred because WPF applies the window rect Windows suggests along
        // with WM_DPICHANGED after this returns, which would overwrite anything
        // set inline; newDpi is captured rather than re-read later, since the
        // window can still report the scale it is leaving.
        //
        // HONEST STATUS (2026-07-19): this does NOT fix changing the scale in
        // Windows' display settings while the app runs. A docked window keeps
        // the height computed under the old scale until something nudges it -
        // clicking it, moving it, or resizing. Tried inline, then Background
        // priority, then this timer; none of them changed the behaviour, which
        // suggests the event may not be raised at all for that case rather than
        // it being an ordering problem. Left in place because it is harmless
        // and should still cover a window dragged between monitors of different
        // scale (a path where WM_DPICHANGED definitely arrives), but do not
        // assume the settings-change case works. Deliberately not chased
        // further: a fresh start always positions correctly, so the symptom
        // only survives until the next launch, and the user judged it not worth
        // more time. Instrument whether OnDpiChanged fires at all before
        // attempting a third fix.
        var newScale = newDpi;
        var settle = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        settle.Tick += (_, _) =>
        {
            settle.Stop();
            if (_isDocked)
            {
                PositionToWorkArea(newScale);
            }
        };
        settle.Start();
    }

    private void SystemParameters_StaticPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Only re-snap to the work area while docked - a floating window must not
        // get yanked back to the left edge just because the taskbar moved/resized.
        if (e.PropertyName == nameof(SystemParameters.WorkArea) && _isDocked)
        {
            // Wrapped in a lambda rather than passed as a method group, which
            // crashed the app outright ("Parameter count mismatch") the moment a
            // second monitor was switched on. PositionToWorkArea gained an
            // optional parameter when the DPI-change path was added; passing it
            // as a method group binds Dispatcher.Invoke(Delegate, object[]),
            // which invokes reflectively - and reflection does not fill in
            // optional parameters the way the compiler does at a normal call
            // site. Nothing warns about this: it compiles, and only fails when
            // the display layout actually changes.
            Dispatcher.Invoke(() => PositionToWorkArea());
        }
    }

    // dpiScale is passed only from the DPI-change path, where the window's own
    // reported scale can still be the old one - see OnDpiChanged.
    private void PositionToWorkArea(DpiScale? dpiScale = null)
    {
        var workArea = GetCurrentMonitorWorkArea(dpiScale);
        Left = _settings.DockOnRight ? workArea.Right - Width : workArea.Left;
        Top = workArea.Top;
        Height = workArea.Height;
    }

    // Work area (excludes the taskbar) of whichever monitor this window's
    // bounds currently overlap - not always the primary monitor, unlike
    // SystemParameters.WorkArea (which only ever reflects the primary one,
    // regardless of which monitor the window is actually on). This is what
    // lets Dock() snap to the correct edge of whichever monitor the window
    // was last floating on, instead of always jumping back to the primary
    // display.
    //
    // System.Windows.Forms.Screen.WorkingArea is in that monitor's own
    // physical pixels, so it's converted using THIS window's own current DPI
    // scale (VisualTreeHelper.GetDpi) - safe specifically because this is
    // only ever asked about the monitor the window is already sitting on
    // (Dock() calls this before moving the window anywhere), so there's no
    // cross-monitor DPI mismatch to resolve.
    private Rect GetCurrentMonitorWorkArea(DpiScale? dpiScale = null)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var screen = hwnd != IntPtr.Zero
            ? System.Windows.Forms.Screen.FromHandle(hwnd)
            : System.Windows.Forms.Screen.PrimaryScreen;
        if (screen is null)
        {
            return SystemParameters.WorkArea;
        }

        // Caller-supplied during a DPI change, where asking the window itself
        // can still return the scale being left behind.
        var dpi = dpiScale ?? VisualTreeHelper.GetDpi(this);
        var working = screen.WorkingArea;
        return new Rect(
            working.Left / dpi.DpiScaleX,
            working.Top / dpi.DpiScaleY,
            working.Width / dpi.DpiScaleX,
            working.Height / dpi.DpiScaleY);
    }

    private static double ClampExpandedWidth(double width)
        => Math.Clamp(width, MinExpandedWidth, MaxExpandedWidth);

    // The app icon is the auto-hide toggle now - docked and expanded, a
    // click goes straight to the auto-hide sliver (there used to be an
    // intermediate icon-only rail stop here, removed since re-clicking the
    // icon from that state only went deeper into auto-hide rather than back
    // out - the rail had no way back to normal except undocking, which made
    // it a dead end rather than a useful resting state). Ignored entirely
    // once auto-hidden (see EnterAutoHide's own comment - the pin button,
    // not this, is the way back out from there). Floating has nothing to
    // collapse to, so this is a no-op there; AppIcon is just branding while
    // floating.
    private void AppIcon_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isDocked || _settings.IsAutoHidden)
        {
            return;
        }

        EnterAutoHide();
        e.Handled = true;
    }

    // Sets Width to targetWidth in one step - and, docked to the right edge,
    // Left with it so the right edge stays anchored to the screen edge (see
    // PositionToWorkArea/ResizeThumb_DragDelta, which anchor that same edge
    // their own way). Shared by the auto-hide enter/reveal/re-hide
    // transitions.
    //
    // This USED to be a 200ms eased Width/Left animation. Removed entirely
    // (2026-07-21, user call) after it kept producing irregular visible
    // ghosting/afterimages: every animation tick resizes/moves the real
    // native HWND, and whether DWM composes each of those frames cleanly is
    // outside the app's control - frame-rate capping was tried in an earlier
    // round and only made the steps MORE visible, and hiding the content
    // during the slide (still in place at the callers) reduced but never
    // eliminated the artifacts. One resize can't smear.
    //
    // A transform-based slide (window snaps, content glides via
    // TranslateTransform - ghost-free by construction) was ALSO built and
    // tried the same day, and the user still preferred instant. Both
    // alternatives are settled questions: don't re-propose an animation here.
    private void AnimateWidth(double targetWidth, Action? onCompleted = null)
    {
        double targetLeft = Left + (Width - targetWidth);
        bool anchorRightEdge = _isDocked && _settings.DockOnRight;

        Width = targetWidth;
        if (anchorRightEdge)
        {
            Left = targetLeft;
        }
        onCompleted?.Invoke();
    }

    // Clamped defensively at the point of use (like MaxItemsPerFolder/
    // TabSpacing elsewhere) rather than trusting a hand-edited settings file.
    private double AutoHideSliverWidth => Math.Clamp(_settings.AutoHideSliverWidth, 3, 8);

    // Entered by clicking the app icon while docked and expanded - shrinks
    // to a bare AutoHideSliverWidth sliver at the screen edge, which
    // MainWindow_MouseEnter/Leave then peek open/closed as the mouse crosses
    // it, the same convention as Windows' own taskbar auto-hide. Forces
    // Topmost on for as long as auto-hide stays engaged (both the sliver and
    // the temporarily-peeked-open states) - otherwise a maximized window
    // would cover the sliver and the mouse could never reach it to reveal it
    // again, regardless of the user's own "항상 위에 표시" preference.
    // ExitAutoHide restores that preference. Content is hidden immediately,
    // before the shrink animation starts, so the animation only ever has to
    // redraw the sliver's own bare background/header on each tick - showing
    // it before animating (or not hiding it at all) is what used to make the
    // reveal side of this transition stutter (see AnimateWidth's comment).
    private void EnterAutoHide()
    {
        _settings.IsAutoHidden = true;
        Topmost = true;
        SetExpandedContentVisibility(Visibility.Collapsed);
        AnimateWidth(AutoHideSliverWidth);
    }

    // Reached only via the pin button while temporarily peeked open (see
    // UpdatePinButtonVisibility) - turns auto-hide off entirely and settles
    // into a normal, always-visible expanded window rather than re-hiding
    // again. Already at full width with content visible from the peek, so
    // there's nothing left to animate here.
    private void ExitAutoHide()
    {
        _autoHideRehideTimer?.Stop();
        StopAutoHideOutsideClickWatch();
        _settings.IsAutoHidden = false;
        _isAutoHideRevealed = false;
        Topmost = _settings.AlwaysOnTop;
        UpdatePinButtonVisibility();

        // MainWindow_MouseEnter (the reveal that got us here) never itself
        // refreshes the resize thumb, so it's left in whatever state it was
        // in before the reveal (hidden/non-hit-testable) until something
        // else calls this - or dragging to resize would silently do nothing
        // despite the window now being back to a normal, resizable state.
        UpdateResizeThumbVisibility();
    }

    private void MainWindow_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDocked || !_settings.IsAutoHidden || _isAutoHideRevealed)
        {
            return;
        }

        _autoHideRehideTimer?.Stop();
        _isAutoHideRevealed = true;
        // Width first, content after: the one full tree/favorites layout pass
        // this costs happens at the final width, not at the sliver's.
        AnimateWidth(ClampExpandedWidth(_settings.ExpandedWidth), onCompleted: () =>
        {
            SetExpandedContentVisibility(Visibility.Visible);
        });
        UpdatePinButtonVisibility();

        if (!_settings.AutoHideCloseOnMouseLeave)
        {
            StartAutoHideOutsideClickWatch();
        }
    }

    // A short delay (rather than hiding the instant the cursor leaves) so
    // briefly overshooting the sliver's edge on the way in/out doesn't
    // instantly slam it shut again. Only relevant to the default
    // AutoHideCloseOnMouseLeave mode - the click-outside alternative
    // (StartAutoHideOutsideClickWatch) doesn't care about the cursor leaving
    // at all.
    private void MainWindow_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        => ArmAutoHideRehideTimer();

    private void ArmAutoHideRehideTimer()
    {
        if (!_isDocked || !_settings.IsAutoHidden || !_isAutoHideRevealed || !_settings.AutoHideCloseOnMouseLeave)
        {
            return;
        }

        _autoHideRehideTimer ??= new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(AutoHideRehideDelayMs)
        };
        _autoHideRehideTimer.Tick -= AutoHideRehideTimer_Tick;
        _autoHideRehideTimer.Tick += AutoHideRehideTimer_Tick;
        _autoHideRehideTimer.Start();
    }

    private void AutoHideRehideTimer_Tick(object? sender, EventArgs e)
    {
        _autoHideRehideTimer!.Stop();

        // Re-check - the pin button (ExitAutoHide) or a re-entry
        // (MainWindow_MouseEnter, which stops this timer) may have already
        // changed things by the time this fires.
        if (!_settings.IsAutoHidden || !_isAutoHideRevealed)
        {
            return;
        }

        // Moving onto the options menu counts as leaving the window, because
        // the menu is its own window - so this would close the sidebar out from
        // under the menu the user just opened. See IsMenuOrDialogOpen.
        if (IsMenuOrDialogOpen)
        {
            return;
        }

        // WPF's routed MouseEnter (which would normally have cancelled this
        // timer already) has been reported unreliable right at a monitor
        // boundary - docked at the edge of a secondary monitor adjacent to
        // the primary one, moving the cursor back in quickly closed this
        // instead of re-opening it, but slowly worked fine every time. A
        // last check against the OS's own raw cursor position (not the
        // routed event) catches a cursor that's really still there despite
        // the spurious Leave, and keeps polling instead of closing under it.
        var cursor = System.Windows.Forms.Cursor.Position;
        var dpi = VisualTreeHelper.GetDpi(this);
        double cursorX = cursor.X / dpi.DpiScaleX;
        double cursorY = cursor.Y / dpi.DpiScaleY;
        const double margin = 8;
        bool stillNearWindow =
            cursorX >= Left - margin && cursorX <= Left + Width + margin &&
            cursorY >= Top - margin && cursorY <= Top + Height + margin;
        if (stillNearWindow)
        {
            _autoHideRehideTimer.Start();
            return;
        }

        CloseAutoHideReveal();
    }

    private void CloseAutoHideReveal()
    {
        _isAutoHideRevealed = false;
        SetExpandedContentVisibility(Visibility.Collapsed);
        AnimateWidth(AutoHideSliverWidth);
        UpdatePinButtonVisibility();
    }

    // Alternative to the default AutoHideRehideTimer/MouseLeave close: instead
    // of closing as soon as the cursor drifts off the peeked-open window, this
    // keeps it open regardless of the cursor and only closes once the user
    // actually clicks somewhere outside it - for reading the tree without it
    // snapping shut over a stray mouse movement. Polls rather than a global
    // low-level mouse hook - simpler and much lower-risk (no unmanaged hook
    // handle to leak/crash on if cleanup is ever missed). 30ms (not the
    // original 120ms) because this only samples the instantaneous
    // MouseButtons state once per tick: an ordinary click's whole down-to-up
    // span is often shorter than 120ms, so it could fall entirely between
    // two ticks and never get seen at all - read as the first click outside
    // being silently swallowed, closing only on a second, luckier-timed
    // click. 30ms shrinks that blind spot enough to catch it reliably while
    // still being cheap (only runs while the panel is peeked open in
    // click-outside mode).
    private System.Windows.Threading.DispatcherTimer? _autoHideOutsideClickTimer;

    // How many of the app's own menus are open right now. Both auto-hide paths
    // decide by the sidebar's rectangle - the cursor leaving it, or a click
    // landing outside it - and a menu is a separate window that satisfies
    // either the instant it appears. Opening the options menu while auto-hidden
    // therefore slammed the sidebar shut, so the only way to reach a setting
    // was to pin first, change it, and unpin again - i.e. auto-hide and the
    // settings could not really be used together.
    //
    // Held as the set of menus actually open rather than a running count: a
    // count that ever missed a Closed would suppress auto-hide permanently,
    // and "the sidebar stopped hiding and I can't see why" is a far worse
    // failure than the one this fixes.
    private readonly HashSet<ContextMenu> _openMenus = new();

    private bool IsMenuOrDialogOpen
    {
        get
        {
            // Self-healing: drop anything that has closed without its event
            // reaching us, so a single missed notification can't wedge this on
            // forever. At most a handful of entries.
            _openMenus.RemoveWhere(menu => !menu.IsOpen);

            // Windows.Count > 1 covers the Color Settings and About windows -
            // separate windows too, so working inside one reads exactly like
            // walking away from the sidebar.
            //
            // The search history dropdown is a Popup rather than a ContextMenu,
            // so it reports through none of the above - but it takes mouse
            // capture exactly as a menu does, which is how it closes on an
            // outside click. The capture watchdog would otherwise pull that out
            // from under it (see StartStuckCaptureWatchdog).
            return IsCapturingUiOpen || Application.Current.Windows.Count > 1;
        }
    }

    // The subset that actually holds the mouse: menus and the history popup
    // take capture (with no button down) for as long as they are open, which is
    // how they close on an outside click. Split out from the property above
    // because the capture watchdog must key off exactly this and not off
    // whether some dialog window happens to be open - see its own comment.
    private bool IsCapturingUiOpen
    {
        get
        {
            _openMenus.RemoveWhere(menu => !menu.IsOpen);
            return _openMenus.Count > 0 || SearchHistoryPopup.IsOpen;
        }
    }

    private void AnyMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu menu)
        {
            _openMenus.Add(menu);

            // Kill the popup fade WPF applies to context menus when the OS
            // menu-animation setting is on: menus opened/closed in quick
            // succession restart the fade over each other, which reads as a
            // fluorescent-lamp flicker. The internal popup only exists after
            // the menu's first open (which is why this lives here and not
            // somewhere earlier), so each menu instance gets one last fade on
            // its very first open of the session and none after - the rapid
            // reopen case, where the flicker actually lived, is the fixed one.
            // The submenu popups' own animation is off in the XAML template.
            if (menu.Parent is System.Windows.Controls.Primitives.Popup popup &&
                popup.PopupAnimation != System.Windows.Controls.Primitives.PopupAnimation.None)
            {
                popup.PopupAnimation = System.Windows.Controls.Primitives.PopupAnimation.None;
            }
        }
    }

    private void AnyMenu_Closed(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu menu)
        {
            _openMenus.Remove(menu);
        }

        // Opening a menu moves the cursor out of the sidebar, so MouseLeave has
        // already fired and been ignored (the menu was up). Closing the menu
        // raises no new MouseLeave - the cursor never re-entered - so without
        // this the sidebar would sit open until the cursor came back and left
        // again. Re-arm here if the cursor is in fact outside; the timer's own
        // tick re-checks everything else, and ArmAutoHideRehideTimer ignores
        // the call outright unless mouse-leave mode is the active one.
        if (!IsMenuOrDialogOpen && !IsCursorInsideWindow())
        {
            ArmAutoHideRehideTimer();
        }
    }

    private bool IsCursorInsideWindow()
    {
        var cursor = System.Windows.Forms.Cursor.Position;
        var dpi = VisualTreeHelper.GetDpi(this);
        double x = cursor.X / dpi.DpiScaleX;
        double y = cursor.Y / dpi.DpiScaleY;
        return x >= Left && x <= Left + Width && y >= Top && y <= Top + Height;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetCapture();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    private System.Windows.Threading.DispatcherTimer? _stuckCaptureWatchdog;

    // The capture seen on the previous tick, and how many ticks in a row it has
    // been the same one - see the watchdog for why a single sighting isn't
    // enough to act on.
    private IntPtr _lastSeenCapture;
    private int _sameCaptureTicks;

    // A mouse capture that outlives whatever took it makes Windows route ALL
    // mouse input to this app: the sidebar still highlights rows under the
    // cursor while every other window stops responding to clicks, the pointer
    // appears to jump, and a press released over another app can land as a
    // stray click or context menu there. Reported after ~8 hours of use with
    // many drags out to another application, and confirmed by the giveaway
    // detail that Edgetree alone still tracked the mouse; it also matches an
    // earlier "nothing is clickable after waking from sleep" report.
    //
    // Capture is taken in several places (the OLE drag loop, the header grab)
    // and can be stranded by paths outside this code entirely - a drag source
    // destroyed mid-drag, a suspend/resume in the middle of a gesture. Rather
    // than trying to enumerate every way it can leak, this asserts the
    // invariant directly once a second: with no mouse button held and no menu
    // open, nothing in this app has any business holding capture.
    //
    // Both the WPF-level and Win32-level captures are checked. The OLE drag
    // loop takes the latter without WPF necessarily knowing, so Mouse.Captured
    // alone would miss exactly the case this was written for.
    private void StartStuckCaptureWatchdog()
    {
        _stuckCaptureWatchdog = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _stuckCaptureWatchdog.Tick += (_, _) =>
        {
            if (System.Windows.Forms.Control.MouseButtons != System.Windows.Forms.MouseButtons.None)
            {
                _sameCaptureTicks = 0;
                return;
            }

            // Deliberately NOT IsMenuOrDialogOpen: that also reports true while
            // a dialog window is open, and a dialog does not hold the mouse -
            // so using it here would switch this watchdog off for as long as
            // Color Settings or About were up, and switch it off permanently if
            // that window count ever failed to return to one. A safety net must
            // not share a failure mode with the thing it is protecting against.
            // Only the UI that genuinely captures with no button held is
            // excluded: menus and the history popup.
            if (IsCapturingUiOpen)
            {
                _sameCaptureTicks = 0;
                return;
            }

            var wpfCapture = Mouse.Captured;
            var win32Capture = GetCapture();
            if (wpfCapture is null && win32Capture == IntPtr.Zero)
            {
                _sameCaptureTicks = 0;
                return;
            }

            // Act only on the SAME capture seen three ticks running. A genuinely
            // stranded capture stays put and is collected three seconds later,
            // which the user never notices; a capture that is merely mid-gesture
            // does not survive the wait.
            //
            // This was added after the log showed 26 releases in three minutes,
            // each naming a DIFFERENT handle - the signature of live gestures
            // being interrupted rather than one leak recurring, most likely a
            // scrollbar thumb drag where the button state momentarily read as
            // released. The two real leaks recorded before that (both
            // "WPF(ListBox)") pass this test unchanged, since a stuck capture
            // by definition keeps reporting the same handle.
            if (win32Capture != _lastSeenCapture)
            {
                _lastSeenCapture = win32Capture;
                _sameCaptureTicks = 1;
                return;
            }

            if (++_sameCaptureTicks < 3)
            {
                return;
            }

            _sameCaptureTicks = 0;
            _lastSeenCapture = IntPtr.Zero;

            if (wpfCapture is not null)
            {
                Mouse.Capture(null);
            }

            if (win32Capture != IntPtr.Zero)
            {
                ReleaseCapture();
            }

            LogStuckCapture(wpfCapture, win32Capture);
        };
        _stuckCaptureWatchdog.Start();
    }

    // Debug builds only - this exists to answer one question ("does capture
    // actually get stranded, or is the theory wrong?") and there is no reason
    // to ship a log file for it. If the file stays empty while the symptom
    // recurs, the diagnosis is wrong and the cause is elsewhere.
    [System.Diagnostics.Conditional("DEBUG")]
    private static void LogStuckCapture(IInputElement? wpfCapture, IntPtr win32Capture)
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Edgetree");
            Directory.CreateDirectory(dir);

            string held = wpfCapture is not null && win32Capture != IntPtr.Zero
                ? $"WPF({wpfCapture.GetType().Name}) + Win32(0x{win32Capture:X})"
                : wpfCapture is not null
                    ? $"WPF({wpfCapture.GetType().Name})"
                    : $"Win32(0x{win32Capture:X})";

            File.AppendAllText(
                Path.Combine(dir, "capture-watchdog.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  released stuck capture: {held}{Environment.NewLine}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Diagnostics must never be the reason something breaks.
        }
    }

    private void StartAutoHideOutsideClickWatch()
    {
        _autoHideOutsideClickTimer ??= new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(30)
        };
        _autoHideOutsideClickTimer.Tick -= AutoHideOutsideClickTimer_Tick;
        _autoHideOutsideClickTimer.Tick += AutoHideOutsideClickTimer_Tick;
        _autoHideOutsideClickTimer.Start();
    }

    private void StopAutoHideOutsideClickWatch()
    {
        _autoHideOutsideClickTimer?.Stop();
    }

    private void AutoHideOutsideClickTimer_Tick(object? sender, EventArgs e)
    {
        // Self-terminating safety net: if some other path (pin button,
        // toggling the option mid-reveal, ...) exited this state without
        // going through StopAutoHideOutsideClickWatch, this notices within
        // one tick and stops itself instead of polling forever.
        if (!_isDocked || !_settings.IsAutoHidden || !_isAutoHideRevealed || _settings.AutoHideCloseOnMouseLeave)
        {
            StopAutoHideOutsideClickWatch();
            return;
        }

        // Clicking a menu item, or anywhere in the Color Settings window, is a
        // click outside the sidebar's own rectangle - which is exactly what
        // this watch is looking for. Stand down while either is up, or picking
        // an option would dismiss the sidebar mid-action. See
        // IsMenuOrDialogOpen.
        if (IsMenuOrDialogOpen)
        {
            return;
        }

        if (System.Windows.Forms.Control.MouseButtons == System.Windows.Forms.MouseButtons.None)
        {
            return;
        }

        var cursor = System.Windows.Forms.Cursor.Position;
        var dpi = VisualTreeHelper.GetDpi(this);
        double cursorX = cursor.X / dpi.DpiScaleX;
        double cursorY = cursor.Y / dpi.DpiScaleY;
        bool insideWindow = cursorX >= Left && cursorX <= Left + Width && cursorY >= Top && cursorY <= Top + Height;
        if (insideWindow)
        {
            return;
        }

        StopAutoHideOutsideClickWatch();
        CloseAutoHideReveal();
    }

    private void SetExpandedContentVisibility(Visibility visibility)
    {
        // Not a plain assignment like the rest below - RootPathText also has
        // its own independent "제목 표시줄 타이틀 제거" setting (see
        // ApplyTitleTextVisibility), so it stays hidden through this call
        // whenever that's on, but still hides/shows in step with everything
        // else here rather than needing a second code path.
        UpdateRootPathTextVisibility(visibility);
        ExplorerTree.Visibility = visibility;
        SearchButton.Visibility = visibility;
        CollapseAllButton.Visibility = visibility;
        OptionsButton.Visibility = visibility;
        MinimizeButton.Visibility = visibility;
        CloseButton.Visibility = visibility;
        FavoritesList.Visibility = visibility;
        FavoritesSplitter.Visibility = visibility;
        VersionFooterBorder.Visibility = visibility;

        // The search overlay only shows when it's both expanded AND search is
        // the active view - collapsing to the auto-hide sliver hides it like
        // everything else, and expanding back restores whichever view was up.
        SearchView.Visibility = visibility == Visibility.Visible && _isSearchViewActive
            ? Visibility.Visible
            : Visibility.Collapsed;

        // RowDefinition heights don't auto-shrink just because their content is
        // hidden, so without this the favorites row/splitter (and the version
        // footer row) would keep reserving their pixel height as a blank gap
        // in the auto-hide sliver.
        if (visibility == Visibility.Collapsed)
        {
            FavoritesRowDef.Height = new GridLength(0);
            FavoritesSplitterRow.Height = new GridLength(0);
            VersionFooterRow.Height = new GridLength(0);
        }
        else
        {
            UpdateFavoritesPanelVisibility();
            VersionFooterRow.Height = new GridLength(20);
        }

        UpdateResizeThumbVisibility();
        UpdatePinButtonVisibility();
    }

    // The resize thumb only makes sense docked (ResizeMode=NoResize there, so it's
    // the only way to change width) - floating windows get native edge-resize
    // instead. Normal expanded-and-docked, or docked and temporarily peeked
    // out of auto-hide (see MainWindow_MouseEnter) - the hidden auto-hide
    // sliver itself is too narrow to make sense of a drag-resize, but a peek
    // shows the same full-width content a normal expanded window does, so it
    // should be resizable the same way while it's up.
    private bool CanResizeWidth
        => _isDocked && (!_settings.IsAutoHidden || _isAutoHideRevealed);

    private void UpdateResizeThumbVisibility()
    {
        bool show = CanResizeWidth;
        ResizeThumb.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        ResizeThumb.IsHitTestVisible = show;

        // Right-docked, the window grows toward the left (see
        // ResizeThumb_DragDelta), so the grab handle needs to be on the left
        // edge instead of the right one.
        ResizeThumb.HorizontalAlignment = _settings.DockOnRight
            ? System.Windows.HorizontalAlignment.Left
            : System.Windows.HorizontalAlignment.Right;
    }

    // The pin (re-dock) button only makes sense while floating; docking itself
    // happens by dragging the header, not by clicking anything.
    // Same button, two unrelated jobs depending on which state it's shown in
    // (see PinButton_Click) - floating (re-dock), or docked and temporarily
    // peeked open out of auto-hide (stop auto-hiding and stay open).
    private void UpdatePinButtonVisibility()
    {
        bool usableForFloatingRedock = !_isDocked;
        bool usableForAutoHideReveal = _isDocked && _settings.IsAutoHidden && _isAutoHideRevealed;

        // Greyed out rather than hidden when pinning doesn't apply (already
        // docked and pinned): a button that disappears shifts every other
        // header icon sideways, which read as confusing - a dimmed pin stays
        // put and says "not available here" instead. See the IsEnabled trigger
        // in ToggleButtonStyle.
        PinButton.IsEnabled = usableForFloatingRedock || usableForAutoHideReveal;

        // It still hides completely along with the rest of the header when the
        // window collapses to the auto-hide sliver - mirroring one of the
        // buttons SetExpandedContentVisibility drives keeps that in step.
        PinButton.Visibility = CloseButton.Visibility;

        // Which edge re-docking/pinning actually snaps to depends on
        // DockOnRight, so the tooltip can't be one fixed string the way the
        // header's other buttons' tooltips are.
        PinButton.ToolTip = _settings.DockOnRight ? Strings.ToolTipPinRight : Strings.ToolTipPinLeft;
    }

    // The header's icon buttons are fixed-size, and the title between them is
    // the only flexible column - so once it has been squeezed to nothing, a
    // narrower window just pushes the buttons out past the right edge. Stepping
    // their size and spacing down keeps the whole row inside the window all the
    // way to MinExpandedWidth (180), where 6 buttons at 24px + the app icon
    // still fit. Read via DynamicResource by ToggleButtonStyle and the header
    // buttons' own margins.
    private void ApplyHeaderMetrics()
    {
        double width = ActualWidth > 0 ? ActualWidth : Width;
        var (size, gap, closeGap) = width switch
        {
            >= 250 => (32.0, 2.0, 6.0),
            >= 210 => (28.0, 1.0, 4.0),
            _ => (24.0, 0.0, 2.0),
        };

        Resources["HeaderButtonSize"] = size;
        Resources["HeaderButtonMargin"] = new Thickness(gap);
        Resources["HeaderCloseButtonMargin"] = new Thickness(gap, gap, closeGap, gap);

        // The glyph-only enlargement steps back to its original size on
        // narrow windows (user call, 2026-07-22: "224 이하"), where the
        // buttons are already tightening above and bigger drawings would
        // crowd their shrinking hit-boxes. Mutated in place: the transform is
        // shared by all six glyphs via StaticResource and is never used
        // inside a sealed Style, so it is never frozen (see the resource's
        // own comment in the XAML).
        double glyphScale = width <= 224 ? 1.0 : 1.15;
        if (Resources["HeaderGlyphGrow"] is System.Windows.Media.ScaleTransform glyphGrow &&
            glyphGrow.ScaleX != glyphScale)
        {
            glyphGrow.ScaleX = glyphScale;
            glyphGrow.ScaleY = glyphScale;
        }
    }

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e) => ApplyHeaderMetrics();

    private void HeaderGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Double-click used to toggle dock state directly here, but that made
        // a floating window someone deliberately keeps undocked snap straight
        // back to the edge on any accidental double-click (and, worse, on the
        // one double-click that recovers a window Windows' own Aero-snap got
        // stuck maximized - see Dock()/Undock()'s WindowState reset). Docking
        // is still one click away via the pin button; undocking is still a
        // header drag (HeaderGrid_MouseMove) - both cover their direction on
        // their own, so this doesn't need its own third shortcut.
        _headerDragStart = e.GetPosition(this);
        (sender as UIElement)?.CaptureMouse();
    }

    private void HeaderGrid_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_headerDragStart is not { } start || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(this);
        bool pastThreshold =
            Math.Abs(current.X - start.X) >= SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(current.Y - start.Y) >= SystemParameters.MinimumVerticalDragDistance;

        if (!pastThreshold)
        {
            return;
        }

        _headerDragStart = null;
        (sender as UIElement)?.ReleaseMouseCapture();

        // Dragging a maximized window's titlebar restoring it (and following
        // the cursor from there) is standard Windows behavior - this window
        // just never did it, since a custom WindowChrome caption doesn't get
        // that for free.
        if (WindowState == WindowState.Maximized)
        {
            // Just flipping WindowState back to Normal isn't enough on its
            // own: WPF then repositions the window to RestoreBounds (wherever
            // it happened to be the last time it was Normal), with no
            // relation to where the cursor actually is right now.
            //
            // An earlier version of this tried to preserve the exact grabbed
            // point by combining the current cursor position with `start`
            // (the window-relative point captured back in
            // HeaderGrid_MouseLeftButtonDown) - but `start` was captured via
            // GetPosition while the window was still Maximized, and that
            // turned out not to reliably track the window's true on-screen
            // bounds, so the computed position carried a growing error into
            // every repeat maximize/restore cycle instead of fixing it.
            // Recomputing purely from the CURRENT cursor position (nothing
            // carried over from a possibly-unreliable earlier read) can't
            // accumulate error, even if it's not pixel-perfect about which
            // exact point under the cursor gets grabbed - clamped onto the
            // screen so it can't end up dragged off it either.
            var cursorScreen = System.Windows.Forms.Cursor.Position;
            var dpi = VisualTreeHelper.GetDpi(this);
            double cursorX = cursorScreen.X / dpi.DpiScaleX;
            double cursorY = cursorScreen.Y / dpi.DpiScaleY;

            // RestoreBounds - captured before WindowState changes below -
            // holds this window's actual size right before it got maximized,
            // which is whatever the user had it resized to (not necessarily
            // _floatingWidth/Height or the docked ExpandedWidth default), so
            // restoring to it is what keeps that size instead of silently
            // snapping back to a smaller default.
            var restoreBounds = RestoreBounds;
            double restoredWidth = restoreBounds.Width > 0 ? restoreBounds.Width : (_floatingWidth ?? ClampExpandedWidth(_settings.ExpandedWidth));
            double restoredHeight = restoreBounds.Height > 0 ? restoreBounds.Height : (_floatingHeight ?? DefaultFloatingHeight);

            WindowState = WindowState.Normal;
            Width = restoredWidth;
            Height = restoredHeight;

            double screenLeft = SystemParameters.VirtualScreenLeft;
            double screenTop = SystemParameters.VirtualScreenTop;
            double screenRight = screenLeft + SystemParameters.VirtualScreenWidth;
            double screenBottom = screenTop + SystemParameters.VirtualScreenHeight;
            Left = Math.Clamp(cursorX - Width / 2, screenLeft, Math.Max(screenLeft, screenRight - Width));
            Top = Math.Clamp(cursorY - HeaderHeight / 2, screenTop, Math.Max(screenTop, screenBottom - Height));
        }
        else if (_isDocked)
        {
            Undock();
        }

        // Hands the rest of this same mouse gesture off to the native move loop,
        // so the window keeps following the cursor exactly like a title-bar drag.
        DragMove();
    }

    private void HeaderGrid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _headerDragStart = null;
        (sender as UIElement)?.ReleaseMouseCapture();
    }

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isDocked && _settings.IsAutoHidden)
        {
            ExitAutoHide();
        }
        else
        {
            Dock();
        }
    }

    private void Undock(bool offsetFromCorner = false)
    {
        if (!_isDocked)
        {
            return;
        }
        _isDocked = false;

        // Aero-snap (dragging the header to the screen edge, or Win+Up) can
        // still maximize this window even with no OS titlebar, since
        // WindowChrome gives it a real caption area while floating (see
        // ChromeSettings.CaptionHeight below). WPF silently ignores every
        // Left/Top/Width/Height write below while WindowState stays
        // Maximized - which is exactly why the pin button/double-click used
        // to look like it did nothing once a window got stuck that way.
        // Resetting it here first is what actually unsticks it.
        WindowState = WindowState.Normal;

        // Auto-hiding to a sliver only makes sense docked (a space-saving
        // trick for a fixed-height edge strip); a freshly undocked window
        // has no reason to be stuck there, so expand it.
        if (_settings.IsAutoHidden)
        {
            _settings.IsAutoHidden = false;
            _isAutoHideRevealed = false;
            _autoHideRehideTimer?.Stop();
            Topmost = _settings.AlwaysOnTop;
            SetExpandedContentVisibility(Visibility.Visible);
        }
        Width = _floatingWidth ?? ClampExpandedWidth(_settings.ExpandedWidth);

        ResizeMode = ResizeMode.CanResize;
        ChromeSettings.CaptionHeight = HeaderHeight;
        ChromeSettings.ResizeBorderThickness = new Thickness(FloatingResizeBorder);

        // A window styled entirely through WindowChrome (WindowStyle="None")
        // loses the OS's own drop shadow along with the rest of its native
        // frame - a bare, nonzero GlassFrameThickness (even just a 1px sliver
        // on one edge, never actually rendered as glass on Win10/11 without
        // Mica/Acrylic) is what re-enables DWM's shadow without bringing back
        // any other native chrome. Only wanted while floating - the docked
        // sidebar sits flush against the screen edge, where a shadow has
        // nothing to visually separate it from (see Dock() below, which
        // zeroes this back out).
        ChromeSettings.GlassFrameThickness = new Thickness(0, 0, 0, 1);

        var hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.MakeAppWindow(hwnd);
        NativeMethods.SetWindowCornerPreference(hwnd, rounded: true);
        ShowInTaskbar = true;

        // Height (like Width just above) is restored unconditionally,
        // regardless of drag vs. double-click - unlike Left/Top below,
        // changing Height doesn't move the window's top-left corner (where
        // the header/title bar the user might currently be dragging sits),
        // so it can't strand the cursor the way repositioning would.
        if (_floatingHeight.HasValue)
        {
            double screenTop = SystemParameters.VirtualScreenTop;
            double screenBottom = screenTop + SystemParameters.VirtualScreenHeight;
            Height = Math.Min(_floatingHeight.Value, screenBottom - screenTop);
        }
        else
        {
            Height = Math.Min(Height, DefaultFloatingHeight);
        }

        // A drag-triggered undock (offsetFromCorner: false) leaves Left/Top
        // exactly where they are - DragMove() right after Undock() continues
        // the very same mouse gesture, tracking the cursor smoothly from
        // wherever it already is. Jumping to a remembered floating spot (or
        // nudging from the corner) here would strand the cursor away from
        // the window instead of following it, since neither has anything to
        // do with where the cursor currently is mid-drag.
        if (offsetFromCorner)
        {
            if (_floatingLeft.HasValue && _floatingTop.HasValue)
            {
                // Restore exactly where it was the last time this app
                // instance floated, rather than nudging away from the corner
                // below - clamped back onto the current virtual screen in
                // case a monitor was disconnected/resized meanwhile.
                double screenLeft = SystemParameters.VirtualScreenLeft;
                double screenTop = SystemParameters.VirtualScreenTop;
                double screenRight = screenLeft + SystemParameters.VirtualScreenWidth;
                double screenBottom = screenTop + SystemParameters.VirtualScreenHeight;

                Left = Math.Clamp(_floatingLeft.Value, screenLeft, Math.Max(screenLeft, screenRight - Width));
                Top = Math.Clamp(_floatingTop.Value, screenTop, Math.Max(screenTop, screenBottom - Height));
            }
            else
            {
                // Docked position sits flush with the work area's top-left (or,
                // right-docked, top-right) corner, so without nudging it inward
                // a double-click-triggered undock (no drag to carry it
                // elsewhere) looked like nothing had happened.
                var workArea = GetCurrentMonitorWorkArea();
                Left = _settings.DockOnRight
                    ? Math.Max(Left - UndockCornerOffset, workArea.Left)
                    : Math.Min(Left + UndockCornerOffset, workArea.Right - Width);
                Top = Math.Min(Top + UndockCornerOffset, workArea.Bottom - Height);
            }
        }

        UpdateResizeThumbVisibility();
        UpdatePinButtonVisibility();
    }

    private void Dock()
    {
        if (_isDocked)
        {
            return;
        }

        // Same Aero-snap reset as Undock() - matters here too, otherwise the
        // floating bounds snapshotted right below would be the maximized
        // (full-screen) ones instead of the window's actual floating size.
        WindowState = WindowState.Normal;

        // Snapshot the floating bounds right before they're overwritten below,
        // so a later Undock() can put the window back exactly where/how it
        // was instead of falling back to a fresh default (see the fields'
        // own comment).
        _floatingLeft = Left;
        _floatingTop = Top;
        _floatingWidth = Width;
        _floatingHeight = Height;

        _isDocked = true;

        ResizeMode = ResizeMode.NoResize;
        ChromeSettings.CaptionHeight = 0;
        ChromeSettings.ResizeBorderThickness = new Thickness(0);
        ChromeSettings.GlassFrameThickness = new Thickness(0);

        var hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.MakeToolWindow(hwnd);
        NativeMethods.SetWindowCornerPreference(hwnd, rounded: false);
        ShowInTaskbar = false;

        Width = ClampExpandedWidth(_settings.ExpandedWidth);
        PositionToWorkArea();

        UpdateResizeThumbVisibility();
        UpdatePinButtonVisibility();
    }

    // Approximate single-row height (icon/text line plus the item's own
    // 4+4 vertical padding) and the list's own outer top+bottom padding, used
    // below to size the panel to its actual contents rather than always
    // snapping to whatever height was last dragged. Scales with the tree's
    // FontSize using the same ratio FontSizeToRowPaddingConverter uses for row
    // padding (FavoritesList.FontSize follows ExplorerTree.FontSize - see
    // MainWindow.xaml), so a fitted panel still matches the favorites list's
    // actual rendered row height after Ctrl+/- zoom instead of drifting away
    // from it as the font grows or shrinks.
    // A favorites row's rendered height, built from the same two inputs the row
    // itself is: its scaled content, plus RowVerticalPadding top and bottom.
    //
    // This used to be a flat `24 * fontScale`, which quietly assumed the only
    // thing that could change a row's height was the font. The "행 간격" option
    // (-4..+8, added later) also feeds RowPadding, so every row was really up
    // to 16px taller or shorter than this claimed - an error multiplied by the
    // favorite count, which is what made the gap above the panel's bottom
    // divider wander as that option was stepped. The two numbers have to come
    // from one place; that place is RowVerticalPadding.
    //
    // 18 is the content itself (icon or text line), chosen so that at the
    // default font and zero row spacing this still yields exactly the 24 it
    // always did - i.e. the previously-correct font-only case is preserved
    // bit-for-bit, and only the row-spacing term is new.
    private const double FavoriteRowContentHeight = 18;

    // Measured off a real row wherever possible, rather than predicted. Any
    // prediction restates what the row template and ApplyLayoutMetrics between
    // them actually produce, and drifts from it the moment either changes -
    // and because the result is multiplied by the favorite count, a couple of
    // pixels of drift per row is a visible gap above the panel's bottom
    // divider. That gap has now been "fixed" twice by tuning constants (see
    // FavoritesFitBottomPadding, cut 8 -> 0 for the same complaint) and came
    // back both times, which is the signal to stop guessing the number and ask
    // the row itself. The formula below survives only as a fallback for the
    // instant before any row exists to measure - startup, or 0 -> 1 favorites.
    private double FavoriteRowHeight
    {
        get
        {
            if (FavoritesList.ItemContainerGenerator.ContainerFromIndex(0)
                is ListBoxItem { ActualHeight: > 0 } firstRow)
            {
                return firstRow.ActualHeight;
            }
            return FavoriteRowContentHeight * TreeFontScale + 2 * RowVerticalPadding;
        }
    }

    // Read off the control instead of restating the XAML's value here, for the
    // same reason as above: two copies of one number drift.
    //
    // BorderThickness counts as well as Padding: the panel's bottom divider IS
    // the list's own 1px bottom border, and leaving it out cost exactly one
    // pixel - which does not show up as a one-pixel error. The list scrolls by
    // whole items, so a last row clipped by a single pixel makes WPF scroll a
    // full row to reveal it the moment that row is clicked, i.e. the list
    // visibly jumps by one line. The old row-height over-estimate happened to
    // absorb this; measuring rows exactly is what exposed it.
    private double FavoritesListChrome
        => FavoritesList.Padding.Top + FavoritesList.Padding.Bottom
           + FavoritesList.BorderThickness.Top + FavoritesList.BorderThickness.Bottom;

    // Row1 and Row3 (see the XAML comment on Grid.RowDefinitions) are neutral
    // top/bottom slots - whichever one currently holds the favorites panel is
    // the one every existing height/collapse/fit method below needs to act
    // on, so those methods go through this property instead of naming a row
    // directly. TreeRowDef is its mirror, used only to give the row NOT
    // hosting favorites Height="*" whenever the position changes.
    private RowDefinition FavoritesRowDef => _settings.FavoritesAtBottom ? Row3 : Row1;
    private RowDefinition TreeRowDef => _settings.FavoritesAtBottom ? Row1 : Row3;

    // Swaps which physical row (top or bottom) hosts the favorites panel vs
    // the tree, per the "즐겨찾기를 아래에 표시" option. Grid.Row is just an
    // int - reassigning it on the two controls is enough to move them; the
    // splitter's own Grid.Row never changes; it always sits between whichever
    // control is on top and whichever is on bottom.
    private void ApplyFavoritesPosition()
    {
        Grid.SetRow(FavoritesList, _settings.FavoritesAtBottom ? 3 : 1);
        Grid.SetRow(ExplorerTree, _settings.FavoritesAtBottom ? 1 : 3);

        // The favorites-hosting row keeps whatever height the logic below
        // gives it (collapsed/fit/dragged); the other one just needs to fill
        // the rest.
        TreeRowDef.Height = new GridLength(1, GridUnitType.Star);

        // No border flipping anymore: the divider line lives in the splitter
        // row itself (see the XAML), which IS the boundary between the two
        // panels in either position mode.
        UpdateFavoritesPanelVisibility();
    }

    // Was 8 - a cushion so the fitted height didn't land exactly on the last
    // row's bottom edge - but that read as too much empty space below the
    // last favorite (see the TODO item this came from), so it's 0 for now.
    private const double FavoritesFitBottomPadding = 0;

    // The favorites row/splitter collapse to 0 height when there are no
    // favorites, so the panel doesn't waste space for users who never use it.
    // When there are favorites, the panel fits snugly to however many there
    // currently are - capped at the remembered (user-dragged) height, so a
    // handful of favorites don't get stretched across a tall leftover gap
    // (e.g. right after going from 0 to 1) but a manually-enlarged panel is
    // still respected once there are enough favorites to fill it.
    private double ComputeFavoritesContentHeight()
    {
        // Both callers run right after ApplyLayoutMetrics has swapped the row
        // padding resource, at which point the existing containers still report
        // their previous height - so force the pending pass through before
        // measuring, or every metric change would size the panel to the metric
        // before it.
        FavoritesList.UpdateLayout();

        double height = _settings.Favorites.Count * FavoriteRowHeight
            + FavoritesListChrome + FavoritesFitBottomPadding;

        // Rounded up because being a fraction of a pixel short is not a
        // fractional problem here - see FavoritesListChrome: item-based
        // scrolling turns any shortfall at all into a whole-row jump. Row
        // heights are fractional at most zoom levels (16 * 20/12 and friends),
        // so this is a real risk rather than a theoretical one. Costs at most
        // one pixel of extra gap.
        return Math.Ceiling(height);
    }

    private void UpdateFavoritesPanelVisibility()
    {
        bool hasFavorites = _settings.Favorites.Count > 0;
        if (hasFavorites)
        {
            FavoritesRowDef.Height = new GridLength(Math.Min(ComputeFavoritesContentHeight(), _settings.FavoritesPanelHeight));
        }
        else
        {
            FavoritesRowDef.Height = new GridLength(0);
        }
        FavoritesSplitterRow.Height = hasFavorites ? GridLength.Auto : new GridLength(0);
    }

    private void FavoritesSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        _settings.FavoritesPanelHeight = FavoritesRowDef.ActualHeight;
    }

    // Double-clicking the splitter auto-fits the panel to exactly the current
    // favorites, same convention as Explorer/Excel's column-divider double-click
    // - unlike UpdateFavoritesPanelVisibility's cap (which only ever shrinks
    // unused space), this is a deliberate "fit now" action so it can grow the
    // panel too, e.g. to reveal favorites previously left below a manually
    // shrunk splitter.
    // Left button only - same latent trap as ExplorerTree_MouseDoubleClick
    // (see its comment): WPF raises this for right-button double-clicks too.
    private void FavoritesSplitter_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            FitFavoritesPanel();
        }
    }

    // Sizes the panel to exactly fit every current favorite (growing it if
    // needed, unlike UpdateFavoritesPanelVisibility's shrink-only cap) and
    // remembers that height. Used both for the explicit double-click "fit"
    // gesture and right after adding a favorite, so a newly-added one is
    // never hidden below a panel height sized for the previous, smaller count.
    private void FitFavoritesPanel()
    {
        if (_settings.Favorites.Count == 0)
        {
            return;
        }

        double contentHeight = ComputeFavoritesContentHeight();
        FavoritesRowDef.Height = new GridLength(contentHeight);
        _settings.FavoritesPanelHeight = contentHeight;

        // The fitted height fits every item, so nothing is actually scrolled
        // out of view anymore - but a scroll offset from before the resize
        // (e.g. the list had been scrolled down to reach a later favorite)
        // doesn't necessarily reset on its own, leaving the top item(s)
        // scrolled past. Scrolling the first item into view forces it back.
        FavoritesList.ScrollIntoView(FavoritesList.Items[0]);
    }

    private void AddFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (ExplorerTree.SelectedItem is not FileSystemItem { IsPlaceholder: false, IsDirectory: true } item)
        {
            return;
        }

        bool alreadyExists = _settings.Favorites.Any(f =>
            string.Equals(f.Path, item.FullPath, StringComparison.OrdinalIgnoreCase));
        if (alreadyExists)
        {
            return;
        }

        _settings.Favorites.Add(new FavoriteEntry { DisplayName = item.Name, Path = item.FullPath });

        // The panel might not have existed at all yet (0 -> 1 favorites), so
        // the row/splitter need their initial reveal here - FitFavoritesPanel
        // alone only ever sets FavoritesRowDef's height, not FavoritesSplitterRow.
        UpdateFavoritesPanelVisibility();
        FitFavoritesPanel();
    }

    private void RemoveFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (FavoritesList.SelectedItem is FavoriteEntry entry)
        {
            _settings.Favorites.Remove(entry);
            UpdateFavoritesPanelVisibility();
        }
    }

    private void FavoriteListBoxItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem item)
        {
            item.IsSelected = true;

            // Same reasoning as TreeViewItem_PreviewMouseRightButtonDown: open
            // below the row instead of at the mouse point, so the menu doesn't
            // cover the very favorite it was opened on.
            if (item.ContextMenu is { } menu)
            {
                menu.PlacementTarget = item;
                menu.Placement = PlacementMode.Bottom;
            }
        }
    }

    private void FavoriteListBoxItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Single click navigates (requirement a). The double-click workaround
        // this used to require is gone: capping every folder at
        // FileSystemItem.DisplayCap keeps the tree light enough that the reveal
        // walk realizes the target's container reliably on the first click,
        // even below a huge folder (NavigateToPath re-caps overflow first).
        if (sender is ListBoxItem { DataContext: FavoriteEntry entry })
        {
            // Re-clicking a favorite that's already revealed, expanded, and
            // selected used to still re-run the entire walk: re-collapse
            // every other folder's "more" overflow, re-expand the whole
            // chain level by level, and re-pin the selection to the top of
            // the tree - all for an end state identical to what was already
            // on screen, which read as the whole panel flashing/redrawing.
            // Nothing left to do only when it's already both selected AND
            // expanded - IsExpanded matters too, not just the path match: a
            // favorite added while its own folder was selected but still
            // collapsed (e.g. right-clicked without ever opening it) would
            // otherwise match on path alone and skip NavigateToPath - the one
            // call that actually expands it - leaving it selected but stuck
            // collapsed until some other selection change knocked it loose.
            if (ExplorerTree.SelectedItem is FileSystemItem { IsExpanded: true } selected &&
                string.Equals(selected.FullPath.TrimEnd('\\'), entry.Path.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // The walk is asynchronous (RevealChainStep defers via the
            // dispatcher), so the "navigating from a favorite" guard is set and
            // cleared inside NavigateToPath / the walk itself, not around this
            // synchronous call - clearing it here would drop the guard while the
            // walk is still running and let an intermediate selection change
            // clear the favorite we just clicked.
            NavigateToPath(entry.Path);
        }
    }

    // Expands every ancestor folder down to targetPath, and the target itself,
    // then selects/scrolls it into view in the "내 PC" tree, rather than
    // switching the tree's root - keeps the single fixed-root design favorites
    // were added on top of. pinToTop forces the revealed row to the very top
    // of the viewport (see FinishReveal) - the right call for a deliberate
    // favorites click (requirement (b): land at the top, not just visible),
    // but not for RefreshFolderPreservingState's own quiet reselect after a
    // resort/background change, where the user's current scroll position
    // should stay put; defaults to true so every existing caller keeps its
    // current behavior unless it opts out. pinParentToTop pins the target's
    // PARENT folder to the top instead of the target itself, while still
    // selecting the target - used by search-result navigation so a found file
    // lands in the context of its folder (folder at top, file selected below)
    // rather than glued to the very top with its folder scrolled off.
    private void NavigateToPath(string targetPath, bool pinToTop = true, bool pinParentToTop = false)
    {
        // Bumped here (not just when a navigation finishes and selects
        // something) so a second favorite clicked while RevealChainStep is
        // still waiting on a container from the first supersedes it right
        // away, instead of the two walks potentially interleaving.
        int myToken = ++_navigationToken;

        targetPath = targetPath.TrimEnd('\\');

        var root = _roots.FirstOrDefault(r =>
            targetPath.StartsWith(r.FullPath.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
        if (root is null)
        {
            return;
        }

        // Held for the whole (asynchronous) walk and cleared only when it
        // finishes (EndFavoriteNavigation). Selection changes the walk itself
        // causes - e.g. auto-collapse dropping the previously-open drive, which
        // makes WPF move the tree selection to that drive's root - then skip
        // the favorites-list sync instead of clearing the favorite we're
        // navigating to.
        _isNavigatingFromFavorite = true;

        // Return any folder that had its "더 보기" expanded to the capped state
        // before walking, so this navigation - and the next - never has to
        // realize or scroll past a huge list (requirement c: a favorite below
        // a fully-expanded huge folder still reaches its target in one click).
        RecapAllOverflow();

        string rootPath = root.FullPath.TrimEnd('\\');
        string relative = targetPath.Length > rootPath.Length
            ? targetPath[rootPath.Length..].Trim('\\')
            : string.Empty;
        var segments = relative.Length == 0
            ? Array.Empty<string>()
            : relative.Split('\\', StringSplitOptions.RemoveEmptyEntries);

        var chain = new List<FileSystemItem> { root };
        var current = root;
        foreach (var segment in segments)
        {
            current.EnsureChildrenLoaded();
            // Looks past the cap into the overflow (revealing it if the match
            // is hidden there) so a favorite whose path runs through a folder
            // with more than DisplayCap subfolders is still found.
            var next = current.FindChildForNavigation(segment);
            if (next is null)
            {
                break; // Path no longer exists (renamed/deleted); reveal as far as we got.
            }
            chain.Add(next);
            current = next;
        }

        RevealChain(chain, myToken, pinToTop, pinParentToTop);
    }

    // Walks the loaded tree returning every fully-revealed ("더 보기" expanded)
    // folder to its capped state. Recaps a folder before recursing into it, so
    // a folder holding thousands of revealed rows is trimmed to ~25 first and
    // the recursion only ever visits those - it never iterates the full list.
    private void RecapAllOverflow()
    {
        foreach (var root in _roots)
        {
            RecapOverflowRecursive(root);
        }
    }

    private static void RecapOverflowRecursive(FileSystemItem item)
    {
        item.RecollapseOverflow();
        foreach (var child in item.Children)
        {
            if (!child.IsPlaceholder && !child.IsShowMore && child.IsDirectory)
            {
                RecapOverflowRecursive(child);
            }
        }
    }

    // Virtualized TreeViewItem containers only exist for realized (visible)
    // items, so each level has to be brought into view and laid out before
    // its own children's containers can be looked up. Starts the (possibly
    // asynchronous - see RevealChainStep) walk down the chain from the root.
    private void RevealChain(List<FileSystemItem> chain, int token, bool pinToTop = true, bool pinParentToTop = false)
    {
        // Overflow re-capping already ran up-front in NavigateToPath, so the
        // walk starts over a light tree; this only expands the path down to
        // the target and doesn't touch any other folder's expanded state.
        RevealChainStep(chain, 0, ExplorerTree, token, pinToTop: pinToTop, pinParentToTop: pinParentToTop);
    }

    // A container not being found isn't necessarily "it'll never exist" - a
    // folder's contents having just changed (an overflow recap, a fresh
    // expand) can leave the virtualizing panel still realizing rows when this
    // runs synchronously right after, so the very next level's container
    // genuinely isn't ready *yet*. Giving up immediately there (the original
    // behavior) is what made a favorite intermittently do nothing, or need a
    // second click once whatever was in flight had finished on its own in the
    // meantime. Yielding via the dispatcher and trying again gives that
    // pending work an actual chance to complete first instead.
    private void RevealChainStep(List<FileSystemItem> chain, int index, ItemsControl container, int token, int attempt = 0, bool pinToTop = true, bool pinParentToTop = false)
    {
        // A newer favorite click superseded this walk while it was waiting on a
        // container - stop rather than risking two walks interleaving their
        // expand/select/scroll calls. Don't clear _isNavigatingFromFavorite
        // here: the newer walk set it and owns it now, and will clear it when
        // it finishes.
        if (token != _navigationToken)
        {
            return;
        }

        if (index >= chain.Count)
        {
            EndFavoriteNavigation(token);
            return;
        }

        container.UpdateLayout();
        var item = chain[index];
        if (container.ItemContainerGenerator.ContainerFromItem(item) is not TreeViewItem treeViewItem)
        {
            // A container outside the virtualizing panel's viewport+cache won't
            // materialize just by looking again - which is exactly the case for
            // a search result deep in a big folder (past the "더 보기" cap), the
            // one place this reveal is now driven to a FILE rather than a
            // shallow favorite folder. Actively scroll that index into view via
            // its host panel so the container realizes, instead of retrying
            // blindly until the ceiling and giving up (which left the previous
            // selection in place - the "wrong file opens" bug). Harmless for the
            // levels that were already realizing on their own: it only runs when
            // the container is genuinely missing.
            if (FindItemsHostPanel(container) is VirtualizingPanel hostPanel)
            {
                int itemIndex = container.Items.IndexOf(item);
                if (itemIndex >= 0)
                {
                    hostPanel.BringIndexIntoViewPublic(itemIndex);
                }
            }

            if (attempt >= 8)
            {
                // Given it a real chance - genuinely gone (renamed/deleted), not just slow.
                EndFavoriteNavigation(token);
                return;
            }
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
                new Action(() => RevealChainStep(chain, index, container, token, attempt + 1, pinToTop, pinParentToTop)));
            return;
        }

        treeViewItem.BringIntoView();
        container.UpdateLayout();

        // Expand every folder in the chain, including the target itself
        // (favorites are always directories - see AddFavorite's IsDirectory
        // check), so navigating to a favorite reveals its contents too.
        item.IsExpanded = true;
        treeViewItem.UpdateLayout();

        if (index == chain.Count - 1)
        {
            // pinParentToTop anchors the scroll on the parent folder (this
            // step's `container`, the folder whose child we just revealed)
            // while still selecting the target - so a searched file lands
            // inside its folder's context. `container` is always a realized
            // TreeViewItem for any target nested under a drive root; the
            // fallback keeps the target as anchor otherwise.
            var anchor = pinParentToTop && container is TreeViewItem parentItem ? parentItem : treeViewItem;
            FinishReveal(treeViewItem, anchor, token, pinToTop);
        }
        else
        {
            RevealChainStep(chain, index + 1, treeViewItem, token, pinToTop: pinToTop, pinParentToTop: pinParentToTop);
        }
    }

    // `selected` is the row that gets selected/focused; `anchor` is the row
    // pinned to the top of the viewport when pinToTop is set. They're the same
    // for a normal favorite navigation, but differ for a search-result jump
    // (select the file, pin its parent folder) - see NavigateToPath's
    // pinParentToTop.
    private void FinishReveal(TreeViewItem selected, TreeViewItem anchor, int token, bool pinToTop = true)
    {
        // Still guarded while this fires: setting IsSelected raises
        // SelectedItemChanged synchronously, and the guard keeps that from
        // re-syncing (and possibly clearing) the favorites list. Cleared right
        // after, so subsequent user-driven selections sync normally again.
        selected.IsSelected = true;
        selected.BringIntoView();
        selected.Focus();
        EndFavoriteNavigation(token);

        if (!pinToTop)
        {
            // RefreshFolderPreservingState's quiet reselect after a sort-
            // override change or a background disk change (see its own
            // pinToTop: false call) - the plain BringIntoView above already
            // scrolled just enough to make the row visible if it wasn't;
            // forcing it all the way to the top edge below is only correct
            // for a deliberate favorites click (requirement (b)), and was
            // exactly what made resorting a folder (or an unrelated
            // background change elsewhere on the same drive) yank the whole
            // view to the top out from under whatever the user was actually
            // looking at.
            return;
        }

        // Requirement (b): land the favorite at the top of the tree, not just
        // somewhere on screen. Deferred one layout pass (so the chain's final
        // expand has settled) and token-guarded (so a newer navigation cancels
        // it). Bringing a viewport-tall rectangle anchored at the row's own
        // top into view pins that row to the top edge. Safe to force now that
        // capping keeps the tree light - the flakiness this kind of forced
        // scroll used to cause came from doing it across thousands of realized
        // rows, which no folder ever has anymore.
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
        {
            if (token != _navigationToken)
            {
                return;
            }
            if (FindTreeScrollViewer() is { } scrollViewer)
            {
                anchor.BringIntoView(new Rect(0, 0, anchor.ActualWidth, scrollViewer.ActualHeight));
            }
        }));
    }

    private ScrollViewer? FindTreeScrollViewer()
    {
        ExplorerTree.ApplyTemplate();
        return ExplorerTree.Template.FindName("PART_TreeScrollViewer", ExplorerTree) as ScrollViewer;
    }

    // The panel that hosts an ItemsControl's own item containers (IsItemsHost).
    // Used to force a specific child index to realize (BringIndexIntoViewPublic)
    // when its container is virtualized away. Descent stops at nested
    // TreeViewItems so this returns THIS control's items panel, not a
    // grandchild folder's.
    private static VirtualizingPanel? FindItemsHostPanel(DependencyObject root)
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is System.Windows.Controls.Panel { IsItemsHost: true } panel)
            {
                return panel as VirtualizingPanel;
            }
            if (child is TreeViewItem)
            {
                continue;
            }
            if (FindItemsHostPanel(child) is { } found)
            {
                return found;
            }
        }
        return null;
    }

    // Clears the "navigating from a favorite" guard when THIS walk is the one
    // that finished it (its token is still current). A superseded walk (a newer
    // favorite click bumped the token) must not clear it - the newer walk owns
    // the guard now and clears it when it finishes.
    private void EndFavoriteNavigation(int token)
    {
        if (token == _navigationToken)
        {
            _isNavigatingFromFavorite = false;
        }
    }

    // Same triangle, mirrored vertically - swapped in directly (rather than
    // rotating the "up" glyph 180 degrees) because animating that rotation
    // visibly swings the arrow sideways mid-flip instead of reading as a
    // clean in-place change.
    private static readonly Geometry CollapseAllArrowUp = Geometry.Parse("M4,10 L8,5 L12,10 Z");
    private static readonly Geometry CollapseAllArrowDown = Geometry.Parse("M4,5 L8,10 L12,5 Z");

    private void CollapseAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (_collapseAllRestorePaths is { } pathsToRestore)
        {
            _collapseAllRestorePaths = null;
            foreach (var path in pathsToRestore.OrderBy(p => p.Length))
            {
                ExpandPathIfPossible(path);
            }
            CollapseAllArrow.Data = CollapseAllArrowUp;
            CollapseAllButton.ToolTip = Strings.ToolTipCollapseAll;
        }
        else
        {
            _collapseAllRestorePaths = CollectAllExpandedPaths();
            foreach (var root in _roots)
            {
                CollapseRecursive(root);
            }
            CollapseAllArrow.Data = CollapseAllArrowDown;
            CollapseAllButton.ToolTip = Strings.ToolTipRestoreExpanded;
        }

        UpdateCollapseAllButtonState();
    }

    // The title bar's collapse/restore toggle has nothing to do when nothing is
    // expanded AND there's no remembered set to restore - which is exactly the
    // state the options menu's one-shot "모든 펼친 폴더 접기" leaves behind. Grey it
    // out then (see ToggleButtonStyle's IsEnabled trigger); expanding any folder
    // lights it back up.
    private void UpdateCollapseAllButtonState()
    {
        CollapseAllButton.IsEnabled = _collapseAllRestorePaths is { Count: > 0 } || HasAnyExpandedFolder();
    }

    // Cheaper than CollectAllExpandedPaths for this yes/no question - stops at
    // the first expanded folder instead of materializing every path.
    private bool HasAnyExpandedFolder()
    {
        foreach (var root in _roots)
        {
            if (root.IsExpanded || HasExpandedDescendant(root))
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasExpandedDescendant(FileSystemItem item)
    {
        if (!item.ChildrenLoaded)
        {
            return false;
        }

        foreach (var child in item.Children)
        {
            if (!child.IsDirectory || child.IsPlaceholder || child.IsShowMore)
            {
                continue;
            }
            if (child.IsExpanded || HasExpandedDescendant(child))
            {
                return true;
            }
        }
        return false;
    }

    private void ExplorerTree_ItemExpandedOrCollapsed(object sender, RoutedEventArgs e)
        => UpdateCollapseAllButtonState();

    // The options-menu "모든 펼친 폴더 접기" - a one-shot cleanup, unlike the title
    // bar's collapse button next to it (a toggle that remembers what was open
    // so a second click restores it). Deliberately also clears that toggle's
    // remembered set and puts its arrow back to "collapse": without that, one
    // click on the title bar would undo the tidy-up the user just confirmed.
    private void CollapseAllExpandedMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            this,
            Strings.CollapseAllConfirmBody,
            Strings.CollapseAllConfirmTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        foreach (var root in _roots)
        {
            CollapseRecursive(root);
        }

        _collapseAllRestorePaths = null;
        CollapseAllArrow.Data = CollapseAllArrowUp;
        CollapseAllButton.ToolTip = Strings.ToolTipCollapseAll;
        UpdateCollapseAllButtonState();
    }

    private static void CollapseRecursive(FileSystemItem item)
    {
        item.IsExpanded = false;
        foreach (var child in item.Children)
        {
            if (!child.IsPlaceholder)
            {
                CollapseRecursive(child);
            }
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    // Hide() (not Close()) keeps the window - and the app, since it's still
    // the one open window - alive in the background; App's tray icon is what
    // brings it back (see App.RestoreMainWindow).
    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        // While tray-hidden the process can live for days and then end
        // WITHOUT the close path running (logoff kill, crash, a dev rebuild's
        // forced restart - the window handle is gone, so not even WM_CLOSE
        // can reach it). Everything that normally flushes on close would roll
        // back to the last graceful exit - surfaced 2026-07-23 as colors
        // reverting to days-old picks. Save at the moment the window leaves
        // the screen instead; the on-close save still runs as before.
        SaveStateBeforeHiding();
        Hide();

        // The tray icon is the only way back once hidden, so force it visible
        // for the duration even if "always show tray icon" is off.
        if (Application.Current is App app)
        {
            app.IsTrayIconVisible = true;
        }
    }

    private void OptionsButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }

    private void OptionsMenu_Opened(object sender, RoutedEventArgs e)
    {
        // This menu has its own Opened work below, so it can't just point at
        // AnyMenu_Opened in the XAML the way the plain menus do - count it here
        // instead. Its Closed is wired to AnyMenu_Closed as usual.
        AnyMenu_Opened(sender, e);

        // Same reasoning as ExplorerItemContextMenu_Opened: MenuItems declared
        // in a resource dictionary don't get auto-generated code-behind fields.
        // The two discards after the toggle group skip the separator and the
        // "색상 변경" item, neither of which has state to sync here.
        if (sender is ContextMenu
            {
                Items: [MenuItem autoCollapse, MenuItem collapseAllExpanded, MenuItem alwaysOnTop, MenuItem startWithWindows, MenuItem trayIcon, MenuItem showFolderIcons, MenuItem showFileIcons, MenuItem hideTitleBarTitle, MenuItem favoritesAtBottom, MenuItem dockOnRight, MenuItem autoHideCloseOnLeave, _, _, _, MenuItem fontSizeRow, MenuItem maxItemsRow, MenuItem tabSpacingRow, MenuItem rowSpacingRow, MenuItem autoHideSliverWidthRow, MenuItem scrollBarThicknessRow, MenuItem sortMenu, MenuItem languageMenu, MenuItem iconStyleMenu, ..]
            })
        {
            // Nothing expanded means nothing to collapse - grey it out rather
            // than offering a confirmation prompt that would do nothing.
            collapseAllExpanded.IsEnabled = CollectAllExpandedPaths().Count > 0;

            autoCollapse.IsChecked = _settings.AutoCollapseFolders;
            alwaysOnTop.IsChecked = _settings.AlwaysOnTop;
            startWithWindows.IsChecked = _settings.StartWithWindows;
            trayIcon.IsChecked = _settings.AlwaysShowTrayIcon;
            showFolderIcons.IsChecked = _settings.ShowFolderIcons;
            showFileIcons.IsChecked = _settings.ShowFileIcons;
            hideTitleBarTitle.IsChecked = _settings.HideTitleBarTitle;
            favoritesAtBottom.IsChecked = _settings.FavoritesAtBottom;
            dockOnRight.IsChecked = _settings.DockOnRight;
            autoHideCloseOnLeave.IsChecked = _settings.AutoHideCloseOnMouseLeave;

            if (sortMenu.Items is [MenuItem byName, MenuItem byDate, _, MenuItem ascending, MenuItem descending])
            {
                byName.IsChecked = !_settings.SortByDate;
                byDate.IsChecked = _settings.SortByDate;
                ascending.IsChecked = !_settings.SortDescending;
                descending.IsChecked = _settings.SortDescending;
            }

            // Each stepper's value and its two buttons' enabled state, so a row
            // already sitting at a limit shows that the moment the menu opens
            // rather than only after a click that does nothing. Font size reads
            // off the live tree, not _settings - the two agree, but the tree is
            // what SetTreeFontSize actually drives.
            UpdateStepperRow(fontSizeRow, ExplorerTree.FontSize, TreeFontSizeSteps[0], TreeFontSizeSteps[^1]);
            UpdateStepperRow(maxItemsRow, _settings.MaxItemsPerFolder, 1, 50);
            UpdateStepperRow(tabSpacingRow, _settings.TabSpacing, 4, 24);
            UpdateStepperRow(rowSpacingRow, _settings.RowSpacing, -4, 8);
            UpdateStepperRow(autoHideSliverWidthRow, _settings.AutoHideSliverWidth, 3, 8);
            UpdateStepperRow(scrollBarThicknessRow, _settings.ScrollBarThickness, 6, 20);

            // languageMenu's first child is the non-interactive restart note
            // (see the XAML) - skipped here via the leading discard.
            if (languageMenu.Items is [_, MenuItem koItem, MenuItem enItem])
            {
                koItem.IsChecked = _settings.Language != "en";
                enItem.IsChecked = _settings.Language == "en";
            }

            if (iconStyleMenu.Items is [MenuItem defaultIcons, MenuItem shellIcons])
            {
                defaultIcons.IsChecked = !_settings.UseShellIcons;
                shellIcons.IsChecked = _settings.UseShellIcons;
            }
        }
    }

    private void MaxItemsPerFolderDecrement_Click(object sender, RoutedEventArgs e)
        => StepMaxItemsPerFolder(sender, -1);

    private void MaxItemsPerFolderIncrement_Click(object sender, RoutedEventArgs e)
        => StepMaxItemsPerFolder(sender, +1);

    // Clamped to the TODO-specified 1~50 range - low enough to matter for
    // small/HD screens, high enough that capping still keeps a huge folder
    // light. Applied immediately: every already-loaded folder re-caps to the
    // new limit via the same recursive refresh sort changes use, not just
    // folders expanded from here on.
    //
    // A single click is a complete, instantaneous action, unlike editing
    // text - a free-typed TextBox here was very hard to actually use: WPF's
    // Menu manages hover/keyboard focus across its own items, and that fought
    // with the TextBox for focus the moment the mouse drifted even slightly
    // outside it mid-edit (see MenuStepperButtonStyle in the XAML).
    private void StepMaxItemsPerFolder(object sender, int delta)
    {
        int value = Math.Clamp(_settings.MaxItemsPerFolder + delta, 1, 50);
        if (value != _settings.MaxItemsPerFolder)
        {
            _settings.MaxItemsPerFolder = value;
            FileSystemItem.DisplayCap = value;
            RefreshAllLoadedFolders();
        }

        UpdateStepperRow(sender, value, 1, 50);
    }

    private void TabSpacingDecrement_Click(object sender, RoutedEventArgs e)
        => StepTabSpacing(sender, -1);

    private void TabSpacingIncrement_Click(object sender, RoutedEventArgs e)
        => StepTabSpacing(sender, +1);

    // Clamped 4~24 per the user-specified range around the original
    // hardcoded 16. Purely a layout property - unlike MaxItemsPerFolder,
    // nothing needs re-reading from disk, so applying it immediately is just
    // ApplyLayoutMetrics recomputing the DynamicResources every row's
    // template already reads (same live-swap approach as font-size zoom and
    // the folder/file icon toggles).
    private void StepTabSpacing(object sender, int delta)
    {
        int value = Math.Clamp(_settings.TabSpacing + delta, 4, 24);
        if (value != _settings.TabSpacing)
        {
            _settings.TabSpacing = value;
            ApplyLayoutMetrics();
        }

        UpdateStepperRow(sender, value, 4, 24);
    }

    private void FontSizeDecrement_Click(object sender, RoutedEventArgs e)
        => StepTreeFontSizeFromMenu(sender, -1);

    private void FontSizeIncrement_Click(object sender, RoutedEventArgs e)
        => StepTreeFontSizeFromMenu(sender, +1);

    // The menu's own view of the Ctrl +/- zoom. Unlike the other steppers here
    // the value doesn't live in _settings alone (ExplorerTree.FontSize is the
    // live source StepTreeFontSize walks), so this just delegates and then
    // reads back whatever step it actually landed on - which is also how the
    // display stays right at either end of the range, where a click is a no-op.
    private void StepTreeFontSizeFromMenu(object sender, int direction)
    {
        StepTreeFontSize(direction);

        UpdateStepperRow(sender, ExplorerTree.FontSize, TreeFontSizeSteps[0], TreeFontSizeSteps[^1]);
    }

    private void RowSpacingDecrement_Click(object sender, RoutedEventArgs e)
        => StepRowSpacing(sender, -1);

    private void RowSpacingIncrement_Click(object sender, RoutedEventArgs e)
        => StepRowSpacing(sender, +1);

    // Clamped -4~+8 per the user-specified range around the existing default
    // (0 = no change from it). Same live-swap approach as StepTabSpacing -
    // ApplyLayoutMetrics recomputes RowPadding, which every row's Style
    // already reads via DynamicResource.
    private void StepRowSpacing(object sender, int delta)
    {
        int value = Math.Clamp(_settings.RowSpacing + delta, -4, 8);
        if (value != _settings.RowSpacing)
        {
            _settings.RowSpacing = value;
            ApplyLayoutMetrics();
            // Row spacing changes a favorites row's height just as the font
            // zoom does, so the panel needs the same re-fit SetTreeFontSize
            // performs - without it the panel kept whatever height it was sized
            // to for the old rows, leaving the gap above its bottom divider to
            // drift with every step.
            FitFavoritesPanel();
        }

        UpdateStepperRow(sender, value, -4, 8);
    }

    // Options ("...") menu's "기본 정렬" - a deliberate global change, so every
    // already-loaded folder should reflect it, not just whichever one happens
    // to be selected (see RefreshAllLoadedFolders).
    private void SortFieldMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string field })
        {
            return;
        }

        _settings.SortByDate = field == "date";
        FileSystemService.SortField = _settings.SortByDate ? FileSortField.Date : FileSortField.Name;
        RefreshAllLoadedFolders();
    }

    private void SortDirectionMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string direction })
        {
            return;
        }

        _settings.SortDescending = direction == "desc";
        FileSystemService.SortDescending = _settings.SortDescending;
        RefreshAllLoadedFolders();
    }

    // Per-folder right-click menu's own "정렬": this folder gets its own
    // remembered sort override (see Models.FolderSortOverrideEntry), left
    // independent of the app-wide default above - reaching for "정렬" on one
    // specific folder reads as "sort THIS folder this way from now on", not
    // "change the app-wide default and re-sort everything I've got open".
    // Cleared via the folder's own override icon or its "전역 정렬 따르기" item
    // (FolderSortFollowGlobalMenuItem_Click below).
    private void FolderSortFieldMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string field } ||
            ExplorerTree.SelectedItem is not FileSystemItem { IsPlaceholder: false, IsDirectory: true } item)
        {
            return;
        }

        var (_, sortDescending) = GetEffectiveFolderSort(item);
        SetFolderSortOverride(item, sortByDate: field == "date", sortDescending);
    }

    private void FolderSortDirectionMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string direction } ||
            ExplorerTree.SelectedItem is not FileSystemItem { IsPlaceholder: false, IsDirectory: true } item)
        {
            return;
        }

        var (sortByDate, _) = GetEffectiveFolderSort(item);
        SetFolderSortOverride(item, sortByDate, sortDescending: direction == "desc");
    }

    // "전역 정렬 따르기" - only enabled from the menu when the selected folder
    // actually has an override (see ExplorerItemContextMenu_Opened); same
    // action as clicking the folder's own override icon.
    private void FolderSortFollowGlobalMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (ExplorerTree.SelectedItem is FileSystemItem { IsPlaceholder: false, IsDirectory: true } item)
        {
            ClearFolderSortOverride(item);
        }
    }

    // What a specific folder's own "정렬" checkboxes should currently show -
    // its own override if it has one, otherwise the app-wide default.
    private (bool SortByDate, bool SortDescending) GetEffectiveFolderSort(FileSystemItem item)
    {
        var entry = _settings.FolderSortOverrides.FirstOrDefault(o =>
            string.Equals(o.Path, item.FullPath, StringComparison.OrdinalIgnoreCase));
        return entry is not null
            ? (entry.SortByDate, entry.SortDescending)
            : (_settings.SortByDate, _settings.SortDescending);
    }

    // Sets (or updates) this folder's own remembered sort: persists it,
    // mirrors it into FileSystemService.SortOverrides so LoadChildren picks
    // it up on every future (re)load of this exact path, flips the live
    // instance's icon on immediately, and re-sorts it right now. Uses the
    // state-preserving refresh (like a background external-change refresh),
    // not the plain RefreshFolder_Click F5 uses - resorting a folder
    // shouldn't silently collapse whatever subfolders the user had expanded
    // further down inside it.
    private void SetFolderSortOverride(FileSystemItem item, bool sortByDate, bool sortDescending)
    {
        var entry = _settings.FolderSortOverrides.FirstOrDefault(o =>
            string.Equals(o.Path, item.FullPath, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            entry = new FolderSortOverrideEntry { Path = item.FullPath };
            _settings.FolderSortOverrides.Add(entry);
        }
        entry.SortByDate = sortByDate;
        entry.SortDescending = sortDescending;

        FileSystemService.SortOverrides[FileSystemService.NormalizeSortOverridePath(item.FullPath)] =
            new FolderSortOverride(sortByDate ? FileSortField.Date : FileSortField.Name, sortDescending);
        item.HasSortOverride = true;
        var field = sortByDate ? FileSortField.Date : FileSortField.Name;
        item.SortOverrideIconUri = FileSystemService.FormatSortOverrideIconUri(field, sortDescending);
        item.SortOverrideTooltip = FileSystemService.FormatSortTooltip(field, sortDescending);

        if (item.ChildrenLoaded)
        {
            RefreshFolderPreservingState(item);
        }
    }

    // Clicking the folder's own override icon: cycles N↑ -> N↓ -> D↑ -> D↓ ->
    // N↑... instead of clearing it - clearing is now a deliberate context-menu
    // action only (FolderSortFollowGlobalMenuItem_Click), since a single click
    // rotating through 4 states is a much faster way to try different sorts
    // than reopening the menu each time.
    // Five-state click rotation, mirroring the search view's sort button:
    // 전역 따름(neutral) -> 이름↑ -> 이름↓ -> 날짜↑ -> 날짜↓ -> 전역 따름.
    // Folding "follow the global sort" into the cycle means the override can be
    // cleared by clicking the icon itself, instead of only via the right-click
    // menu's "전역 정렬 따르기".
    private void RotateFolderSortOverride(FileSystemItem item)
    {
        if (!item.HasSortOverride)
        {
            SetFolderSortOverride(item, sortByDate: false, sortDescending: false);
            return;
        }

        var (sortByDate, sortDescending) = GetEffectiveFolderSort(item);
        switch (sortByDate, sortDescending)
        {
            case (false, false):
                SetFolderSortOverride(item, sortByDate: false, sortDescending: true);
                break;
            case (false, true):
                SetFolderSortOverride(item, sortByDate: true, sortDescending: false);
                break;
            case (true, false):
                SetFolderSortOverride(item, sortByDate: true, sortDescending: true);
                break;
            default:
                ClearFolderSortOverride(item);
                break;
        }
    }

    // Drops this folder's own remembered sort so it goes back to picking up
    // the app-wide default like any other folder - see SetFolderSortOverride.
    private void ClearFolderSortOverride(FileSystemItem item)
    {
        _settings.FolderSortOverrides.RemoveAll(o =>
            string.Equals(o.Path, item.FullPath, StringComparison.OrdinalIgnoreCase));
        FileSystemService.SortOverrides.Remove(FileSystemService.NormalizeSortOverridePath(item.FullPath));
        item.HasSortOverride = false;
        // Back to the neutral "follows the global sort" icon - the rotation's
        // starting point (see RotateFolderSortOverride).
        item.SortOverrideIconUri = FileSystemService.NoSortOverrideIconUri;
        item.SortOverrideTooltip = FileSystemService.NoSortOverrideTooltip;

        if (item.ChildrenLoaded)
        {
            RefreshFolderPreservingState(item);
        }
    }

    // Re-reads every already-loaded folder from disk under whatever sort
    // order/display cap is current, preserving each folder's own expanded
    // state where the same-named child still exists afterward. Used when the
    // sort order or per-folder display cap changes (every loaded folder needs
    // to reflect the new setting, not just whichever one happens to be
    // selected), and for F5 with nothing selected (see ExplorerTree_KeyDown) -
    // a true whole-app refresh.
    private void RefreshAllLoadedFolders()
    {
        // The rebuild below replaces every item instance, so whatever the
        // multi-selection held would keep only dead instances - flags lost,
        // list stale. Drop it up front.
        ClearMultiSelection();

        // Snapshot every currently-expanded, already-loaded folder's full
        // path, and the current selection, before refreshing discards the
        // actual instances that know about either - a folder's
        // RefreshChildren always rebuilds its Children as fresh, collapsed,
        // unloaded instances, even for a folder nobody had expanded.
        var expandedPaths = new List<string>();
        var showingAllPaths = new List<string>();
        foreach (var root in _roots)
        {
            CollectExpandedPaths(root, expandedPaths, showingAllPaths);
        }
        string? selectedPath = (ExplorerTree.SelectedItem as FileSystemItem)?.FullPath;

        foreach (var root in _roots)
        {
            if (root.ChildrenLoaded)
            {
                root.RefreshChildren();
            }
        }

        // Re-expand every folder that was expanded before. EnsureChildrenLoaded
        // along the way (see FindItemForPath) loads each level fresh under
        // whatever sort/cap is now current - exactly like expanding it by hand
        // would - which is what actually re-applies the new setting to the
        // folders the user had open. Shallowest paths first, since a parent
        // has to be loaded and expanded before its own child's name is even
        // reachable; sorting by string length is a cheap stand-in for
        // "ancestors before descendants" without parsing path segments.
        //
        // An earlier version of this instead tried to walk the OLD and NEW
        // trees in lockstep - RefreshChildren the parent, look up each
        // previously-expanded child by name, set IsExpanded on it, recurse -
        // all in one pass. That looked right but wasn't: the newly-matched
        // child is a brand-new, never-loaded instance, and setting its
        // IsExpanded doesn't reliably load it or realize a container under
        // virtualization (the exact class of bug the whole favorites-
        // navigation saga was about), so deeper levels silently failed to
        // re-expand, and WPF's Recycling virtualization then visually
        // reattached the stale "selected" look to whatever row ended up
        // recycled into that slot - reported as selection jumping to the
        // top-level drive with everything below one level collapsed. Doing
        // this as two clean passes - snapshot names first, THEN refresh,
        // THEN walk fresh from each root reloading on demand - sidesteps that
        // entirely.
        foreach (var path in expandedPaths.OrderBy(p => p.Length))
        {
            ExpandPathIfPossible(path);
        }
        foreach (var path in showingAllPaths.OrderBy(p => p.Length))
        {
            ShowAllChildrenIfPossible(path);
        }

        // Restoring the selection reuses the exact same reveal/expand/select
        // walk favorites navigation already relies on (including its
        // retry-until-the-container-is-actually-realized handling) - hand-
        // rolling a simpler version of that same problem here is exactly what
        // caused the bug above.
        if (selectedPath is not null)
        {
            NavigateToPath(selectedPath);
        }
    }

    // showingAllPaths collects every folder ("더 보기" clicked, all overflow
    // rows appended - see FileSystemItem.IsShowingAllChildren) whose full
    // reveal also needs replaying afterward, not just its IsExpanded - a
    // RefreshChildren silently re-caps a folder back to DisplayCap+"더 보기"
    // even when nothing about THAT folder's own sort/contents changed, purely
    // as a side effect of an ancestor being resorted/refreshed. Also checks
    // `item` itself (not just its children), so the folder actually being
    // refreshed has its own showingAll state captured too.
    private static void CollectExpandedPaths(FileSystemItem item, List<string> expandedPaths, List<string> showingAllPaths)
    {
        if (!item.IsDirectory || !item.ChildrenLoaded)
        {
            return;
        }

        if (item.IsShowingAllChildren)
        {
            showingAllPaths.Add(item.FullPath);
        }

        foreach (var child in item.Children)
        {
            if (child.IsPlaceholder || child.IsShowMore || !child.IsDirectory)
            {
                continue;
            }
            if (child.IsExpanded)
            {
                expandedPaths.Add(child.FullPath);
            }
            CollectExpandedPaths(child, expandedPaths, showingAllPaths);
        }
    }

    // Same idea as CollectExpandedPaths, but also captures a drive root's OWN
    // expanded state - which that helper can't, since it only ever looks at
    // an item's children. RefreshAllLoadedFolders never needed that (a root's
    // IsExpanded survives RefreshChildren untouched), but a full app restart
    // rebuilds _roots from scratch, so root-level state needs saving too.
    // "더 보기" reveal state isn't persisted across restarts (nothing asked
    // for that, and it's a much less surprising reset on a fresh launch than
    // mid-session), so its own showingAllPaths output here is discarded.
    private List<string> CollectAllExpandedPaths()
    {
        var result = new List<string>();
        var discardedShowingAll = new List<string>();
        foreach (var root in _roots)
        {
            if (root.IsExpanded)
            {
                result.Add(root.FullPath);
            }
            CollectExpandedPaths(root, result, discardedShowingAll);
        }
        return result;
    }

    // Replays a folder's "더 보기" reveal after ExpandPathIfPossible has
    // already loaded it fresh - same model-only approach (no container/
    // virtualization dependency), see CollectExpandedPaths's own comment.
    private void ShowAllChildrenIfPossible(string path)
    {
        if (FindItemForPath(path) is { } item)
        {
            item.EnsureChildrenLoaded();
            item.ShowAllChildren();
        }
    }

    private void ExpandPathIfPossible(string path)
    {
        if (FindItemForPath(path) is { } item)
        {
            item.EnsureChildrenLoaded();
            item.IsExpanded = true;
        }
    }

    // Walks from the matching drive root down to `path` by name, loading each
    // level on demand - the same approach NavigateToPath uses to build its
    // chain. Tolerates a segment no longer existing (renamed/deleted since)
    // by simply returning null instead of throwing.
    private FileSystemItem? FindItemForPath(string path)
    {
        path = path.TrimEnd('\\');
        var root = _roots.FirstOrDefault(r =>
            path.StartsWith(r.FullPath.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
        if (root is null)
        {
            return null;
        }

        string rootPath = root.FullPath.TrimEnd('\\');
        string relative = path.Length > rootPath.Length ? path[rootPath.Length..].Trim('\\') : string.Empty;
        var segments = relative.Length == 0
            ? Array.Empty<string>()
            : relative.Split('\\', StringSplitOptions.RemoveEmptyEntries);

        var current = root;
        foreach (var segment in segments)
        {
            current.EnsureChildrenLoaded();
            if (current.FindChildForNavigation(segment) is not { } next)
            {
                return null;
            }
            current = next;
        }
        return current;
    }

    private void LanguageMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string language } || language == _settings.Language)
        {
            return;
        }

        _settings.Language = language;
        // Persisted immediately (unlike most other settings, which only flush
        // to disk on close) since restarting right now, before that normally
        // happens, is exactly the point of the prompt below.
        _settingsService.Save(_settings);

        var result = MessageBox.Show(this, Strings.LanguageChangeBody, Strings.LanguageChangeTitle,
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            System.Diagnostics.Process.Start(Environment.ProcessPath!);
            Application.Current.Shutdown();
        }
    }

    private void IconStyleMenuItem_Click(object sender, RoutedEventArgs e)
    {
        bool useShellIcons = sender is MenuItem { Tag: "shell" };
        if (_settings.UseShellIcons == useShellIcons)
        {
            return;
        }

        // Flushes to disk on close with the rest of the settings, same as the
        // other toggles - no explicit save here.
        _settings.UseShellIcons = useShellIcons;
        ShellIconService.UseShellIcons = useShellIcons;
        ApplyIconStyle();
    }

    // Applies the current icon mode to everything already on screen, without a
    // reload: every realized tree item re-raises Icon, the favorites' shared
    // folder-icon resource is swapped, and the search results (whose rows
    // resolve their icon at creation) are rebuilt from the live index.
    private void ApplyIconStyle()
    {
        Resources["FavoriteFolderIconSource"] = ShellIconService.GetFavoritesFolderIcon();

        foreach (var root in _roots)
        {
            RefreshIconsRecursively(root);
        }

        if (_searchEntries.Count > 0)
        {
            RunSearchFilter();
        }
    }

    private static void RefreshIconsRecursively(FileSystemItem item)
    {
        item.RefreshIcon();
        foreach (var child in item.Children)
        {
            if (!child.IsPlaceholder && !child.IsShowMore)
            {
                RefreshIconsRecursively(child);
            }
        }
    }

    // Portable app (no installer, no per-user machine record) - so moving
    // settings to another PC is a manual file, not an automatic sync. Exports
    // the same AppSettings/JSON shape the app already reads/writes at its
    // normal AppData location (see SettingsService), just to a user-chosen
    // path, favorites included.
    private void ExportSettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            FileName = "Edgetree-settings.json",
            Filter = "JSON (*.json)|*.json",
            DefaultExt = ".json"
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            string json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(dialog.FileName, json);
        }
        catch (IOException ex)
        {
            MessageBox.Show(this, ex.Message, Strings.ExportSettingsFailedTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ImportSettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON (*.json)|*.json",
            DefaultExt = ".json"
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        AppSettings? imported;
        try
        {
            string json = File.ReadAllText(dialog.FileName);
            imported = JsonSerializer.Deserialize<AppSettings>(json);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            MessageBox.Show(this, ex.Message, Strings.ImportSettingsFailedTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (imported is null)
        {
            MessageBox.Show(this, Strings.ImportSettingsFailedTitle, Strings.ImportSettingsFailedTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Favorites captured on a different PC point at paths that may not
        // exist here (different drive letters, folders that only existed on
        // the old machine) - drop anything that doesn't resolve on this one
        // rather than importing dead entries.
        for (int i = imported.Favorites.Count - 1; i >= 0; i--)
        {
            if (!Directory.Exists(imported.Favorites[i].Path))
            {
                imported.Favorites.RemoveAt(i);
            }
        }

        // Saved straight to the app's normal settings location and applied on
        // next launch - same restart-required pattern as a language change
        // just above, for the same reason: too much of startup (colors, sort,
        // font size, dock state, ...) only ever runs once, in
        // MainWindow_Loaded, to safely re-run live against a whole different
        // settings object.
        _settingsService.Save(imported);

        // Also swap the live object, not just the file on disk: closing this
        // window (about to happen below, via Shutdown) runs
        // MainWindow_Closing -> SaveCurrentWidth, which unconditionally
        // re-saves whatever _settings currently points at. Left pointing at
        // the OLD settings, that re-save was silently overwriting the file
        // this method had just written with the pre-import values (e.g. a
        // color changed after the export but before this import) - the
        // import looked like it succeeded (the file was briefly correct) but
        // the very next launch read the old values back anyway.
        _settings = imported;

        var result = MessageBox.Show(this, Strings.SettingsImportedBody, Strings.SettingsImportedTitle,
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            System.Diagnostics.Process.Start(Environment.ProcessPath!);
            Application.Current.Shutdown();
        }
    }

    // One combined Yes/No (rather than a second "restart now?" prompt after
    // this one, like Import/Language use) since agreeing to an irreversible
    // full reset already implies agreeing to the restart it requires.
    private void ResetSettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(this, Strings.ResetSettingsConfirmBody, Strings.ResetSettingsConfirmTitle,
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        // Same reasoning as ImportSettingsMenuItem_Click: save the fresh
        // defaults to disk AND repoint the live _settings, since
        // MainWindow_Closing (about to run via Shutdown below) would otherwise
        // re-save whatever _settings references - _settingsResetPending is
        // what stops it from doing that with the still-live pre-reset
        // width/expanded folders/selection.
        //
        // settings.json isn't the only place state lives - StartWithWindows
        // is a Registry Run key (see SetStartWithWindows), entirely outside
        // that file, so a fresh AppSettings() alone leaves a stale entry
        // behind: the options menu would show it off (matching the new
        // default) while the app keeps actually launching at Windows
        // startup regardless. Only worth doing if it was ever turned on -
        // TrySetStartWithWindows would just no-op removing an absent value
        // otherwise, but this skips the Registry write/failure path
        // entirely for the common case where it was never touched.
        if (_settings.StartWithWindows)
        {
            try
            {
                TrySetStartWithWindows(false);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
            {
            }
        }

        _settings = new AppSettings();
        _settingsService.Save(_settings);
        _settingsResetPending = true;

        System.Diagnostics.Process.Start(Environment.ProcessPath!);
        Application.Current.Shutdown();
    }

    private void AutoCollapseMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
        {
            _settings.AutoCollapseFolders = menuItem.IsChecked;
        }
    }

    private void AlwaysOnTopMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
        {
            _settings.AlwaysOnTop = menuItem.IsChecked;
            Topmost = menuItem.IsChecked;
        }
    }

    private void StartWithWindowsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
        {
            _settings.StartWithWindows = menuItem.IsChecked;
            SetStartWithWindows(menuItem.IsChecked);
        }
    }

    private void TrayIconMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
        {
            _settings.AlwaysShowTrayIcon = menuItem.IsChecked;
            if (Application.Current is App app)
            {
                app.IsTrayIconVisible = menuItem.IsChecked;
            }
        }
    }

    private void ShowFolderIconsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
        {
            _settings.ShowFolderIcons = menuItem.IsChecked;
            ApplyFolderIconVisibility();
        }
    }

    private void ShowFileIconsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
        {
            _settings.ShowFileIcons = menuItem.IsChecked;
            ApplyFileIconVisibility();
        }
    }

    private void HideTitleBarTitleMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
        {
            _settings.HideTitleBarTitle = menuItem.IsChecked;
            ApplyTitleTextVisibility();
        }
    }

    // Only ever called while the window's content is already expanded (at
    // startup, or from the options menu - which itself only opens while
    // expanded), so that's the contentVisibility to reconcile against.
    private void ApplyTitleTextVisibility() => UpdateRootPathTextVisibility(Visibility.Visible);

    // RootPathText has two independent reasons to be hidden - the general
    // expand/collapse this window's content goes through (auto-hide sliver,
    // etc. - see SetExpandedContentVisibility) and the user's own "제목
    // 표시줄 타이틀 제거" setting - so it's only actually shown when NEITHER
    // wants it hidden.
    private void UpdateRootPathTextVisibility(Visibility contentVisibility)
    {
        RootPathText.Visibility = contentVisibility == Visibility.Visible && !_settings.HideTitleBarTitle
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void FavoritesAtBottomMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
        {
            _settings.FavoritesAtBottom = menuItem.IsChecked;
            ApplyFavoritesPosition();
        }
    }

    private void DockOnRightMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
        {
            _settings.DockOnRight = menuItem.IsChecked;
            UpdateResizeThumbVisibility();
            UpdatePinButtonVisibility();
            if (_isDocked)
            {
                PositionToWorkArea();
            }
        }
    }

    private void AutoHideCloseOnLeaveMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
        {
            _settings.AutoHideCloseOnMouseLeave = menuItem.IsChecked;

            // Both directions have to be handled while the sidebar is already
            // revealed. Switching away from click-outside mode should stop the
            // watch rather than leave it armed under a setting that no longer
            // applies - and switching TO it has to start one, which nothing did
            // before: the watch is otherwise only ever armed by
            // MainWindow_MouseEnter, so turning this option off while revealed
            // left no watcher at all and clicking outside did nothing until the
            // sidebar had been hidden and re-revealed once.
            if (menuItem.IsChecked)
            {
                StopAutoHideOutsideClickWatch();
            }
            else if (_isDocked && _settings.IsAutoHidden && _isAutoHideRevealed)
            {
                StartAutoHideOutsideClickWatch();
            }
        }
    }

    // Every stepper row in the options menu has the same shape - a label, then
    // [− value +] - so writing the value and greying out whichever button has
    // nothing left to do is one place, reached two ways: from the button that
    // was just clicked (sender is that Button), and from the row itself when
    // the menu opens (sender is the MenuItem). Without the greying out, a
    // stepper sitting at its limit still looks pressable and silently does
    // nothing, which reads as the app ignoring the click rather than the value
    // having an end.
    private static void UpdateStepperRow(object sender, double value, double min, double max)
    {
        StackPanel? stepper = sender switch
        {
            Button { Parent: StackPanel panel } => panel,
            MenuItem { Header: Grid { Children: [_, StackPanel panel] } } => panel,
            _ => null
        };

        if (stepper is not { Children: [Button minus, TextBlock valueText, Button plus] })
        {
            return;
        }

        valueText.Text = ((int)value).ToString();
        minus.IsEnabled = value > min;
        plus.IsEnabled = value < max;
    }

    private void ScrollBarThicknessDecrement_Click(object sender, RoutedEventArgs e)
        => StepScrollBarThickness(sender, -1);

    private void ScrollBarThicknessIncrement_Click(object sender, RoutedEventArgs e)
        => StepScrollBarThickness(sender, +1);

    private void StepScrollBarThickness(object sender, int delta)
    {
        int value = Math.Clamp(_settings.ScrollBarThickness + delta, 6, 20);
        if (value != _settings.ScrollBarThickness)
        {
            _settings.ScrollBarThickness = value;
            ApplyLayoutMetrics();
        }

        UpdateStepperRow(sender, value, 6, 20);
    }

    private void AutoHideSliverWidthDecrement_Click(object sender, RoutedEventArgs e)
        => StepAutoHideSliverWidth(sender, -1);

    private void AutoHideSliverWidthIncrement_Click(object sender, RoutedEventArgs e)
        => StepAutoHideSliverWidth(sender, +1);

    // Clamped to the user-specified 3~8 range - 3 is the original hardcoded
    // sliver width (thin enough that going lower risks the mouse missing it
    // entirely), 8 is thick enough to be an easy target without eating much
    // screen edge. Purely cosmetic for the next time the window actually
    // collapses to the sliver (EnterAutoHide/CloseAutoHideReveal both read
    // the AutoHideSliverWidth property fresh) - not applied live because the
    // Options button that reaches this setting is itself hidden (see
    // SetExpandedContentVisibility) for as long as the window is actually
    // in that collapsed sliver state, so there's no in-place width to
    // animate to begin with.
    private void StepAutoHideSliverWidth(object sender, int delta)
    {
        double value = Math.Clamp(_settings.AutoHideSliverWidth + delta, 3, 8);
        if (value != _settings.AutoHideSliverWidth)
        {
            _settings.AutoHideSliverWidth = value;
        }

        UpdateStepperRow(sender, value, 3, 8);
    }

    // Same live-swap approach as ApplyColorSettings: replacing the resource
    // dictionary entry is picked up immediately by every row's DynamicResource
    // reference (see the HierarchicalDataTemplate's IsDirectory DataTrigger),
    // no per-item property-changed plumbing needed.
    private void ApplyFolderIconVisibility()
    {
        Resources["FolderIconVisibility"] = _settings.ShowFolderIcons ? Visibility.Visible : Visibility.Collapsed;
        ApplyLayoutMetrics();
    }

    private void ApplyFileIconVisibility()
    {
        Resources["FileIconVisibility"] = _settings.ShowFileIcons ? Visibility.Visible : Visibility.Collapsed;
        ApplyLayoutMetrics();
    }

    // Scales the row icon's size and margins with the tree's FontSize - same
    // proportion FontSizeToRowPaddingConverter already applies to row
    // padding, so icons and their surrounding gaps grow/shrink along with
    // Ctrl+/- zoom instead of staying a fixed size that looks increasingly
    // mismatched against the text around them. Also computes everything
    // driven by "탭간격" (AppSettings.TabSpacing, user-adjustable 4~24 from
    // the "..." options menu) - the arrow column width and the guide line's
    // margin/padding are deliberately NOT zoom-scaled here, matching how
    // they were fixed literals before TabSpacing existed; only the icon/name
    // alignment shift below (which already scaled with zoom) keeps doing so.
    //
    // FileRowIconMargin/FileNameMargin also carry the VS Code-style alignment
    // rule: a file never gets its own expand arrow, so a file row's content
    // naturally sits one indent guide (TabSpacing px) to the right of where
    // the guide line beneath a sibling folder's arrow actually falls.
    // Whenever folder icons are visible, that guide line is what the eye
    // lines up files against, so file content needs pulling left by that
    // same amount - whether the file still shows its own icon (shift the
    // icon so its center lands under the guide, FileRowIconMargin) or not
    // (shift the name itself so its left edge lands there instead,
    // FileNameMargin). "Both off" is the one case that instead collapses the
    // arrow gutter itself to 0 below, so nothing here needs to shift into it.
    // How far the current font is zoomed from the default, which nearly every
    // metric below is a multiple of.
    private double TreeFontScale => ExplorerTree.FontSize / DefaultTreeFontSize;

    // The vertical padding on every tree AND favorites row: the font-scaled
    // base plus the user's flat "행 간격" offset, clamped at 0 so a small font
    // with the most negative offset can't go negative. Defined here rather than
    // inline in ApplyLayoutMetrics because FavoriteRowHeight has to predict the
    // very same number - see its own comment for what went wrong when the two
    // were worked out separately.
    private double RowVerticalPadding
        => Math.Max(0, TreeFontScale * 3.0 + _settings.RowSpacing);

    private void ApplyLayoutMetrics()
    {
        double scale = TreeFontScale;

        // Rounded to whole pixels rather than left fractional. The source PNGs
        // are 32x32 being scaled down, which is clean at the default font
        // (32 -> 16, an exact half) but lands on something like 17.33 at 13pt -
        // a destination size no source pixel maps onto evenly, so the whole
        // icon goes soft. Whole numbers also keep the row's other metrics on
        // the pixel grid that UseLayoutRounding is trying to hold (see
        // MainWindow.xaml's header comment).
        Resources["IconSize"] = Math.Round(16.0 * scale);

        // Grows past its base size once the font is zoomed above default, but
        // never shrinks below it while zoomed smaller - the sort-override
        // icon (see MainWindow.xaml's SortOverrideIconBorder) is already a
        // small, fiddly click target at its base 9x13, so scaling it down
        // further along with everything else at small font sizes would make
        // it harder to hit right when a low-resolution/small-window user is
        // most likely to have reached for a smaller font in the first place.
        double growOnlyScale = Math.Max(1.0, scale);
        Resources["SortOverrideIconWidth"] = Math.Round(9.0 * growOnlyScale);
        Resources["SortOverrideIconHeight"] = Math.Round(13.0 * growOnlyScale);

        // Row vertical padding: the font-size-scaled base (was
        // Converters/FontSizeToRowPaddingConverter's whole job, now folded in
        // here since it needed this second, independent input too) plus the
        // user's flat "행 간격" pixel offset - clamped at 0 so a small font
        // combined with the most negative offset can't go negative. Shared by
        // both ExplorerTreeViewItemStyle and FavoriteListBoxItemStyle (see
        // their own comments) so the tree and favorites rows always match.
        double verticalPadding = RowVerticalPadding;
        Resources["RowPadding"] = new Thickness(4, verticalPadding, 4, verticalPadding);

        // Not scaled by the font like the metrics around it: this is a pointer
        // target, so it wants to stay the size the user picked regardless of
        // how large the text is. The lane beside the content is the bar plus
        // its 1px divider (see MinimalScrollViewerTemplate).
        double scrollBarThickness = Math.Clamp(_settings.ScrollBarThickness, 6, 20);
        Resources["ScrollBarThickness"] = scrollBarThickness;
        Resources["ScrollGutterWidth"] = new GridLength(scrollBarThickness + 1);

        // Search result file rows use the same vertical rhythm as the tree (so
        // the "행 간격" option and font zoom move them in step), just with the
        // wider horizontal padding the results list already had.
        Resources["SearchRowPadding"] = new Thickness(8, verticalPadding, 8, verticalPadding);

        double tabSpacing = Math.Clamp(_settings.TabSpacing, 4, 24);
        Resources["TabSpacingWidth"] = new GridLength(tabSpacing);
        // Split around the guide line's own fixed 1px BorderThickness (see
        // ExplorerTreeViewItemStyle's ItemsHost) so the line stays centered
        // under the arrow column above regardless of the current spacing -
        // half margin before the line, half (minus the line itself) padding
        // after it, reproducing the original 8/1/7 split exactly at the
        // default TabSpacing of 16.
        Resources["TabSpacingGuideMargin"] = new Thickness(tabSpacing / 2, 0, 0, 0);
        Resources["TabSpacingGuidePadding"] = new Thickness(tabSpacing / 2 - 1, 0, 0, 0);

        var plainMargin = new Thickness(0, 0, 6 * scale, 0);
        Resources["FolderRowIconMargin"] = plainMargin;

        // A file's icon needs the pull left whenever it's the thing sitting
        // in that gutter at all - i.e. whenever file icons are shown,
        // regardless of whether folder icons are also on (folder icons being
        // off is the pre-existing case this already covered; folder icons
        // being on is the same gutter/guide-line geometry, just with a
        // sibling folder icon now also visible next to it).
        Resources["FileRowIconMargin"] = _settings.ShowFileIcons
            ? new Thickness(-tabSpacing * scale, 0, 6 * scale, 0)
            : plainMargin;

        // File icons off but folder icons on: RowIcon is Collapsed (see the
        // HierarchicalDataTemplate's IsDirectory trigger), so the margin
        // above never renders - the name text itself needs the same pull
        // left instead, or a file's name sits flush with a folder's icon
        // rather than under its guide line. Every other combination leaves
        // NameText where it already sits correctly.
        Resources["FileNameMargin"] = _settings.ShowFolderIcons && !_settings.ShowFileIcons
            ? new Thickness(-tabSpacing * scale, 0, 0, 0)
            : new Thickness(0);

        // Both off: files lose their reserved (but always-blank, per
        // ExpanderColumn's own HasItems=False trigger) arrow gutter too, so a
        // file's name sits flush with the true left edge - to the left of a
        // sibling folder's still-visible arrow - reading as one unindented
        // group instead of files looking oddly indented under folders that
        // no longer even show an icon to justify it.
        bool bothOff = !_settings.ShowFolderIcons && !_settings.ShowFileIcons;
        Resources["FileArrowGutterWidth"] = new GridLength(bothOff ? 0 : tabSpacing);

        // Search result rows track the same zoom (Ctrl +/-) as the tree, so the
        // filename matches the tree's current font and the folder-path header
        // stays one step smaller (floored so it can't disappear at the smallest
        // zoom). Bound as DynamicResource in the results DataTemplate.
        double searchFileFont = ExplorerTree.FontSize;
        Resources["SearchFileFontSize"] = searchFileFont;
        Resources["SearchHeaderFontSize"] = Math.Max(searchFileFont - 1, 8.0);

        // Menus (context menus + the options menu) render in their own popup
        // windows outside the tree's visual tree, so nothing above reaches
        // them - these are what tie every menu to the same Ctrl +/- zoom, via
        // DarkContextMenuStyle and the implicit MenuItem style. APP-level
        // resources, not window-level: a ContextMenu resolves app resources
        // reliably (the chrome brushes it already uses prove the path), which
        // a window resource lookup from inside a popup does not guarantee.
        //
        // The item padding's vertical base is 6, deliberately roomier than the
        // fixed 14,4 it replaces - that tight value existed for low-res
        // screens, which can now simply use a smaller font (the padding
        // follows it down) instead of everyone getting the cramped default.
        // Vertical menu padding shrinks FASTER than the font below its pivot
        // (squared scale), and linearly above it. Linear both ways was tried
        // first and read right at large sizes but too airy at small ones -
        // proportionally equal spacing doesn't LOOK equal on small text, and
        // someone zooming the font down is trying to fit more on screen, so
        // the breathing room should give way ahead of the text.
        //
        // The pivot is 14, not the tree default of 12 (user call, 2026-07-21,
        // after trying 12): full breathing room from 14pt up, the tightening
        // curve below. The vertical base of 8 is chosen so the approved
        // values are reproduced exactly at 9pt (3px) and 12pt (6px), while
        // 14pt gains the "살짝 더" room that was asked for.
        //
        // The per-row padding PLATEAUS at 6px - the approved 12pt value -
        // instead of ever reaching the old 8/8+ range (2026-07-23, two rounds
        // of feedback): first a low-resolution remote screen report ("행간이
        // 너무 넓다" - a large-font menu read as almost filling the window),
        // then the user's own VS Code side-by-side, whose menus stay compact
        // at similar font sizes by keeping rows INSIDE a group tight and
        // spending the air on group boundaries instead. That rhythm is
        // recreated here: row padding stops growing (big text already makes
        // rows taller by itself), and the separators' vertical margin went
        // 4 -> 6 in the XAML so the saved space reads as group articulation
        // rather than uniform looseness. Small fonts were explicitly
        // reported fine - the below-pivot curve is untouched (9pt -> 3px,
        // 12pt -> 6px, same as always).
        double menuVerticalScale = ExplorerTree.FontSize / 14.0;
        if (menuVerticalScale < 1.0)
        {
            menuVerticalScale *= menuVerticalScale;
        }
        double menuVerticalPadding = Math.Min(6.0, Math.Round(8.0 * menuVerticalScale));

        var appResources = Application.Current.Resources;
        appResources["MenuFontSize"] = ExplorerTree.FontSize;
        appResources["MenuGestureFontSize"] = Math.Max(8.0, Math.Round(11.0 * scale));
        // The thumbnail's info/date lines: one zoom step (1pt) below the menu
        // text - it's metadata under a picture, not a menu item.
        appResources["MenuThumbnailInfoFontSize"] = Math.Max(8.0, ExplorerTree.FontSize - 1.0);
        // The context menu's image-thumbnail slot (see UpdateThumbnailRow):
        // 4:3, sized to roughly fill the menu's own width at any font zoom.
        // The MAX matters as much as the min: the slot's Image reports the
        // picture's natural width during measure, so without a ceiling a wide
        // screenshot dragged the whole MENU out to its own width.
        appResources["MenuThumbnailWidth"] = Math.Round(160.0 * scale);
        appResources["MenuThumbnailMaxWidth"] = Math.Round(240.0 * scale);
        appResources["MenuThumbnailHeight"] = Math.Round(120.0 * scale);
        appResources["MenuItemPadding"] = new Thickness(
            Math.Round(15.0 * scale), menuVerticalPadding,
            Math.Round(15.0 * scale), menuVerticalPadding);
        appResources["MenuPadding"] = new Thickness(
            Math.Round(5.0 * scale), menuVerticalPadding,
            Math.Round(5.0 * scale), menuVerticalPadding);
    }

    private void ColorSettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var window = new ColorSettingsWindow(_settings, ApplyColorSettings) { Owner = this };
        PositionNearOptionsButton(window);
        window.ShowDialog();

        // Persisted the moment the dialog closes, not on app exit: settings
        // normally flush on close only, so any exit that skips the close path
        // (crash, task-manager kill, the dev rebuild loop's forced restarts)
        // silently reverted every color picked that session - which is
        // exactly how it surfaced (2026-07-23: colors kept snapping back to
        // ones from days earlier across a day of forced rebuild restarts).
        // Same immediate-save reasoning as the language change.
        _settingsService.Save(_settings);
    }

    private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var window = new AboutWindow { Owner = this };
        PositionNearOptionsButton(window);
        window.ShowDialog();
    }

    // Detail windows (color settings, about, ...) used to always open centered
    // on the main window - starting them near the options menu instead, where
    // the user's attention already is, means less hunting for where the new
    // window landed. Clamped to the virtual screen so a window docked near a
    // screen edge doesn't push most of the dialog off-screen; only Left is
    // clamped against the dialog's own (fixed, pre-SizeToContent) Width -
    // these dialogs all use SizeToContent="Height", so the actual height
    // isn't known yet at this point, and Top is just kept from going above
    // the screen entirely.
    private void PositionNearOptionsButton(Window window)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;

        var topLeft = OptionsButton.PointToScreen(new System.Windows.Point(0, OptionsButton.ActualHeight));
        var dpi = VisualTreeHelper.GetDpi(this);
        double left = topLeft.X / dpi.DpiScaleX;
        double top = topLeft.Y / dpi.DpiScaleY;

        double screenLeft = SystemParameters.VirtualScreenLeft;
        double screenRight = screenLeft + SystemParameters.VirtualScreenWidth;
        window.Left = Math.Clamp(left, screenLeft, Math.Max(screenLeft, screenRight - window.Width));
        window.Top = Math.Max(top, SystemParameters.VirtualScreenTop);
    }

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "Edgetree";

    // Pre-rebrand value name (app was named SidebarExplorer) - cleaned up
    // below so an install that already had startup enabled doesn't end up
    // with a dangling, wrongly-named entry sitting alongside the new one.
    private const string OldRunValueName = "SidebarExplorer";

    // Registers/unregisters the currently-running exe under the per-user Run
    // key. Points at whatever path this process was actually launched from,
    // per the user's call to implement this ahead of a proper installer -
    // re-toggle after moving/rebuilding the exe if the path changes.
    private void SetStartWithWindows(bool enabled)
    {
        try
        {
            TrySetStartWithWindows(enabled);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            // Group policy or another restriction blocked the Run key write
            // (seen elsewhere in this environment - e.g. the OpenAs_RunDLL
            // picker also silently no-ops under some managed setups). Surface
            // it rather than leaving the checkbox lying about the real state.
            MessageBox.Show(this, Strings.StartWithWindowsFailedBody,
                Strings.StartWithWindowsFailedTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static void TrySetStartWithWindows(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (key is null)
        {
            return;
        }

        key.DeleteValue(OldRunValueName, throwOnMissingValue: false);

        if (enabled)
        {
            string? exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
            {
                key.SetValue(RunValueName, $"\"{exePath}\"");
            }
        }
        else
        {
            key.DeleteValue(RunValueName, throwOnMissingValue: false);
        }
    }

    // When the user last pressed a mouse button or key inside the tree -
    // TreeViewItem.Expanded fires for MORE than deliberate expansions:
    // virtualization raises it whenever it re-creates a container for a folder
    // whose model says IsExpanded=true (scrolling back to it, a window resize,
    // a display change re-realizing the viewport). Auto-collapse running on one
    // of those phantom "expansions" collapses everything off that folder's own
    // chain - including the DEEPER half of the currently-open chain when the
    // regenerated container is an ancestor - with no user action at all.
    // Suspected cause of the "found every folder closed" report (2026-07-22
    // ~04:48, coinciding with display-change events in redraw.log). So
    // auto-collapse only follows an expansion that lands within this window of
    // a real tree gesture; anything else keeps children loading but leaves the
    // rest of the tree alone (and logs, in debug builds - see
    // LogAutoCollapseSuppressed - so the theory is checkable next time).
    private long _lastTreeUserInputTicks = long.MinValue / 2;

    private const long TreeGestureWindowMs = 1000;

    private bool IsWithinTreeGestureWindow
        => Environment.TickCount64 - _lastTreeUserInputTicks <= TreeGestureWindowMs;

    private void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
    {
        // Expanded bubbles, and the EventSetter that wires this handler is on
        // every TreeViewItem - so expanding a nested folder re-invokes this
        // same handler again for each ancestor as the event bubbles past it,
        // with sender set to that ancestor rather than the folder actually
        // expanded. Left unguarded, the auto-collapse logic below then reads
        // each ancestor as "the folder that just opened" and immediately
        // collapses the real target back down. OriginalSource stays fixed to
        // the true origin through the whole bubble, unlike sender.
        if (!ReferenceEquals(e.OriginalSource, sender))
        {
            return;
        }

        if (sender is not TreeViewItem { DataContext: FileSystemItem { IsDirectory: true } item })
        {
            return;
        }

        item.EnsureChildrenLoaded();

        // A watcher event landed on this folder while it sat collapsed (see
        // PendingExternalRefresh) - its cached listing predates that change,
        // so sync it now that it's actually coming on screen (a diff-merge:
        // surviving rows and their subtrees stay put, see
        // MergeChildrenFromDisk, which also clears the flag).
        if (item.PendingExternalRefresh)
        {
            RefreshFolderPreservingState(item);
        }

        if (IsWithinTreeGestureWindow)
        {
            ApplyAutoCollapse(item);
        }
        else
        {
            // Favorites navigation lands here too (RevealChain expands
            // programmatically) - harmless, it applies its own collapse once
            // at the end of the walk. Same for startup state restore, which
            // was never meant to auto-collapse the paths it restores.
            LogAutoCollapseSuppressed(item.FullPath);
        }
    }

    // Debug builds only - exists to confirm or kill the phantom-Expanded
    // theory above: if folders collapse on their own again, this file says
    // whether container regeneration fired Expanded around that moment (and
    // for which folders) without any tree input preceding it.
    [System.Diagnostics.Conditional("DEBUG")]
    private static void LogAutoCollapseSuppressed(string path)
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Edgetree");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, "autocollapse.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  Expanded without recent tree input (auto-collapse suppressed): {path}{Environment.NewLine}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    // Accordion mode: collapse every other open folder, keeping only this one
    // (and the ancestor chain down to it, so the path stays visible). Called
    // both from the Expanded handler above (manual clicking) and from
    // RevealChain (favorites navigation) - the latter drives IsExpanded
    // programmatically level by level, and relying solely on that to also
    // raise Expanded correctly for each intermediate level proved unreliable,
    // so favorites navigation instead calls this directly once at the end for
    // the final target, rather than depending on it firing per-level.
    private void ApplyAutoCollapse(FileSystemItem item)
    {
        if (_settings.AutoCollapseFolders)
        {
            CollapseOtherFolders(item);
        }
    }

    // The core "keep only this path expanded" logic, split out from
    // ApplyAutoCollapse so favorites navigation can invoke it unconditionally
    // (see RevealChain) regardless of the Auto Collapse setting - a folder
    // with many files left expanded elsewhere in the tree was throwing off
    // WPF's virtualized scroll-extent estimate badly enough that centering
    // the target reliably wasn't otherwise possible (see CenterInTreeView).
    private void CollapseOtherFolders(FileSystemItem item)
    {
        var keepExpanded = new HashSet<FileSystemItem> { item };
        for (var ancestor = item.Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            keepExpanded.Add(ancestor);
        }

        foreach (var root in _roots)
        {
            CollapseExcept(root, keepExpanded);
        }
    }

    private static void CollapseExcept(FileSystemItem item, HashSet<FileSystemItem> keepExpanded)
    {
        if (item.IsExpanded && !keepExpanded.Contains(item))
        {
            item.IsExpanded = false;
        }

        // Files never expand, so there's nothing under one worth recursing
        // into - skipping them here (rather than recursing in and immediately
        // finding an empty Children collection) matters for a folder holding
        // a large number of files, where this loop would otherwise pay for a
        // full method call per file on every single favorites navigation.
        foreach (var child in item.Children)
        {
            if (!child.IsPlaceholder && child.IsDirectory)
            {
                CollapseExcept(child, keepExpanded);
            }
        }
    }

    // Explorer-style shortcuts for the operations already on the context menu:
    // F2 rename, Delete, Ctrl+C/V copy-paste, Enter open. Reuses the same
    // handlers as the menu items (they only read ExplorerTree.SelectedItem,
    // not the sender), so behavior stays identical either way.
    private void ExplorerTree_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.F2:
                RenameItem_Click(sender, e);
                e.Handled = true;
                break;
            case Key.Delete:
                DeleteItem_Click(sender, e);
                e.Handled = true;
                break;
            case Key.Enter:
                OpenItem_Click(sender, e);
                e.Handled = true;
                break;
            case Key.C when Keyboard.Modifiers == ModifierKeys.Control:
                CopyItem_Click(sender, e);
                e.Handled = true;
                break;
            case Key.C when Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift):
                CopyPath_Click(sender, e);
                e.Handled = true;
                break;
            case Key.Escape when _multiSelection.Count > 0:
                ClearMultiSelection();
                e.Handled = true;
                break;
            case Key.V when Keyboard.Modifiers == ModifierKeys.Control:
                PasteItem_Click(sender, e);
                e.Handled = true;
                break;
            case Key.F5:
                // A folder selected: refresh just that one (existing,
                // narrowly-scoped behavior). Nothing usable selected: treat F5
                // as a whole-app refresh instead of a no-op.
                if (ExplorerTree.SelectedItem is FileSystemItem { IsPlaceholder: false, IsDirectory: true })
                {
                    RefreshFolder_Click(sender, e);
                }
                else
                {
                    RefreshAllLoadedFolders();
                }
                e.Handled = true;
                break;
        }
    }

    // PageUp/PageDown specifically need the PREVIEW (tunneling) phase, not
    // the regular bubbling KeyDown every other shortcut above uses -
    // TreeView/VirtualizingStackPanel has its own built-in PreviewKeyDown
    // handling for page navigation that runs first regardless of e.Handled
    // set later during bubbling, and (at least under virtualization here)
    // it jumps straight to the first/last realized row - Home/End, not a
    // page - rather than anything usable. Intercepting during Preview stops
    // that default behavior from ever running.
    private void ExplorerTree_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // Keyboard expansion (Right arrow, +) is handled by the native
        // TreeView after this tunnel - stamp the gesture here so the
        // auto-collapse guard treats it like a click (see
        // _lastTreeUserInputTicks).
        _lastTreeUserInputTicks = Environment.TickCount64;

        switch (e.Key)
        {
            case Key.PageDown:
                JumpToAdjacentVisibleFolder(+1);
                e.Handled = true;
                break;
            case Key.PageUp:
                JumpToAdjacentVisibleFolder(-1);
                e.Handled = true;
                break;
        }
    }

    // Moves the selection to the next/previous FOLDER row in whatever's
    // currently visible (expanded) in the tree, skipping over files - a
    // quick way to skim folder names without expanding anything or clicking.
    // Deliberately does NOT reuse NavigateToPath/RevealChain: those exist for
    // the favorites-click case specifically and come with side effects that
    // would be wrong here - RecapAllOverflow would silently re-collapse any
    // "더 보기"-revealed folder the user has open, and every step along the
    // chain gets force-expanded, neither of which this simple a step-by-one
    // move should ever trigger (see SelectVisibleItem below instead).
    private void JumpToAdjacentVisibleFolder(int direction)
    {
        var visible = new List<FileSystemItem>();
        foreach (var root in _roots)
        {
            FlattenVisible(root, visible);
        }
        if (visible.Count == 0)
        {
            return;
        }

        var current = ExplorerTree.SelectedItem as FileSystemItem;
        int index = current is null ? -1 : visible.IndexOf(current);
        // No current selection: PgDn starts just before the first entry, PgUp
        // just after the last - so the very first step lands on entry 0 (or
        // Count-1) rather than skipping it.
        if (index < 0)
        {
            index = direction > 0 ? -1 : visible.Count;
        }

        for (index += direction; index >= 0 && index < visible.Count; index += direction)
        {
            var candidate = visible[index];
            if (candidate.IsDirectory && !candidate.IsPlaceholder && !candidate.IsShowMore)
            {
                SelectVisibleItem(candidate);
                return;
            }
        }
    }

    // Depth-first, visual order, following only ALREADY-expanded folders -
    // exactly what's currently on screen (or would be, once scrolled to), not
    // a hypothetical fully-expanded tree.
    private static void FlattenVisible(FileSystemItem item, List<FileSystemItem> result)
    {
        if (item.IsPlaceholder)
        {
            return;
        }
        result.Add(item);
        if (item.IsDirectory && item.IsExpanded && item.ChildrenLoaded)
        {
            foreach (var child in item.Children)
            {
                FlattenVisible(child, result);
            }
        }
    }

    // Selects and scrolls to an item that's already part of the currently-
    // expanded tree (every ancestor is already expanded - see
    // JumpToAdjacentVisibleFolder/FlattenVisible), WITHOUT expanding anything
    // or recapping "더 보기" state the way NavigateToPath's reveal walk would.
    // Still needs the same realize-container-then-recurse approach as that
    // walk, since a container this far from the current scroll position may
    // not exist yet - just without any of ITS side effects.
    private void SelectVisibleItem(FileSystemItem target)
    {
        var chain = new List<FileSystemItem>();
        for (FileSystemItem? item = target; item is not null; item = item.Parent)
        {
            chain.Insert(0, item);
        }
        SelectVisibleItemStep(chain, 0, ExplorerTree);
    }

    private void SelectVisibleItemStep(List<FileSystemItem> chain, int index, ItemsControl container, int attempt = 0)
    {
        if (index >= chain.Count)
        {
            return;
        }

        container.UpdateLayout();
        var item = chain[index];
        if (container.ItemContainerGenerator.ContainerFromItem(item) is not TreeViewItem treeViewItem)
        {
            // Same tolerance as RevealChainStep - a container can genuinely
            // not exist yet for a level that's mid-realization.
            if (attempt >= 5)
            {
                return;
            }
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
                new Action(() => SelectVisibleItemStep(chain, index, container, attempt + 1)));
            return;
        }

        treeViewItem.BringIntoView();
        container.UpdateLayout();

        if (index == chain.Count - 1)
        {
            treeViewItem.IsSelected = true;
            treeViewItem.BringIntoView();
            treeViewItem.Focus();
        }
        else
        {
            SelectVisibleItemStep(chain, index + 1, treeViewItem);
        }
    }

    // The context menu advertises these shortcuts via InputGestureText, but an
    // open ContextMenu takes keyboard focus, so ExplorerTree_KeyDown never sees
    // the keys - pressing F2 with the menu up did nothing at all. Mirroring them
    // here makes them work whether the menu happens to be open or not.
    private void ExplorerItemContextMenu_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is not ContextMenu menu)
        {
            return;
        }

        // Inside an open menu, Enter means "activate whatever is highlighted",
        // and WPF highlights a MenuItem by focusing it (on arrow navigation or
        // mouse-over). So only treat Enter as the Open shortcut when nothing is
        // highlighted - where it would otherwise do nothing at all. Arrow keys,
        // Escape and submenu navigation are never intercepted.
        bool menuItemHighlighted = Keyboard.FocusedElement is MenuItem;

        Action? command = e.Key switch
        {
            Key.F2 => () => RenameItem_Click(sender, e),
            Key.Delete => () => DeleteItem_Click(sender, e),
            Key.F5 => () => RefreshFolder_Click(sender, e),
            Key.Enter when !menuItemHighlighted => () => OpenItem_Click(sender, e),
            Key.C when Keyboard.Modifiers == ModifierKeys.Control => () => CopyItem_Click(sender, e),
            Key.C when Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) => () => CopyPath_Click(sender, e),
            Key.V when Keyboard.Modifiers == ModifierKeys.Control => () => PasteItem_Click(sender, e),
            _ => null
        };

        if (command is null)
        {
            return;
        }

        e.Handled = true;

        // Close first and run the command only once that has settled: closing a
        // ContextMenu restores focus to whatever held it before, which would
        // otherwise immediately pull focus back out of the inline rename box
        // (committing the rename) the moment F2 opened it.
        menu.IsOpen = false;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, command);
    }

    private void ExplorerTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // WPF raises MouseDoubleClick for the RIGHT button too, and this
        // handler never checked which - so two right-clicks landing on the
        // same row within double-click time (easy to hit while repeatedly
        // right-clicking to open/close the context menu) opened the file.
        // Latent since this handler was written; surfaced by thumbnail
        // testing's rapid right-clicks.
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if ((e.OriginalSource as DependencyObject)?.FindAncestor<TreeViewItem>() is not { } treeViewItem)
        {
            return;
        }
        // Folders already toggle on single click (see TreeViewItem_MouseLeftButtonDown);
        // double-click is reserved for opening files so the two don't fight over IsExpanded.
        if (treeViewItem.DataContext is not FileSystemItem { IsPlaceholder: false, IsShowMore: false, IsDirectory: false } item)
        {
            return;
        }

        ShellFileService.OpenWithDefaultApp(item.FullPath);
        e.Handled = true;
    }

    // ----- Ctrl/Shift-click multi-selection ---------------------------------
    //
    // WPF's TreeView is single-select only, so the extra rows live here: the
    // items in this list carry IsMultiSelected (painted like the native
    // selection by the row style) while WPF's own selection stays on whichever
    // row was interacted with last. Invariant kept throughout: whenever this
    // list is non-empty, the natively-selected row is one of its members -
    // ExplorerTree_SelectedItemChanged enforces it by collapsing the set the
    // moment selection lands outside it (keyboard navigation, right-click on
    // an unrelated row, a favorites walk).
    //
    // Copy(Ctrl+C)/Delete/drag-out act on the whole set via
    // GetEffectiveSelection; everything else (rename, open, paste, path copy)
    // deliberately stays single-target on the native selection.
    private readonly List<FileSystemItem> _multiSelection = new();

    // Where the next Shift+click range starts from: the last plainly- or
    // Ctrl-clicked row, VS Code-style. Left alone by Shift+click itself so
    // successive Shift+clicks re-range from the same start.
    private FileSystemItem? _multiSelectAnchor;

    // A plain press on a row that's already part of the set must NOT collapse
    // the set right away - that press may be the start of dragging the whole
    // set out. The collapse is deferred to mouse-UP, and cancelled if a drag
    // actually starts in between (see TreeViewItem_PreviewMouseMove).
    private FileSystemItem? _deferredMultiClearItem;

    private void ClearMultiSelection()
    {
        foreach (var item in _multiSelection)
        {
            item.IsMultiSelected = false;
        }
        _multiSelection.Clear();
        _deferredMultiClearItem = null;
    }

    private void AddToMultiSelection(FileSystemItem item)
    {
        if (!item.IsMultiSelected)
        {
            item.IsMultiSelected = true;
            _multiSelection.Add(item);
        }
    }

    private void RemoveFromMultiSelection(FileSystemItem item)
    {
        if (item.IsMultiSelected)
        {
            item.IsMultiSelected = false;
            _multiSelection.Remove(item);
        }
    }

    // Every row currently on screen or scrolled out of view but realized in
    // the item tree, in top-to-bottom display order - the order Shift+click
    // ranges over. Recurses only into expanded folders, so a collapsed
    // folder's contents can never be swept into a range invisibly.
    private List<FileSystemItem> BuildVisibleItemsInDisplayOrder()
    {
        var result = new List<FileSystemItem>();
        void Walk(FileSystemItem item)
        {
            result.Add(item);
            if (item.IsDirectory && item.IsExpanded)
            {
                foreach (var child in item.Children)
                {
                    if (!child.IsPlaceholder && !child.IsShowMore)
                    {
                        Walk(child);
                    }
                }
            }
        }
        foreach (var root in _roots)
        {
            Walk(root);
        }
        return result;
    }

    private void SelectRange(FileSystemItem anchor, FileSystemItem target)
    {
        var visible = BuildVisibleItemsInDisplayOrder();
        int targetIndex = visible.IndexOf(target);
        if (targetIndex < 0)
        {
            return;
        }
        // An anchor that has since collapsed away or been rebuilt by a
        // refresh isn't in the visible list anymore. Before degrading to a
        // single-row range, fall back to the NATIVE selection - that's the
        // row the user visibly ranges from. Without this, a stale anchor made
        // Shift+click quietly select just the clicked row, which read as
        // "shift+click stopped working, selection just moves" (reported after
        // browsing thumbnails via right-click, which at the time didn't
        // update the anchor at all - see TreeViewItem_PreviewMouseRightButtonDown).
        int anchorIndex = visible.IndexOf(anchor);
        if (anchorIndex < 0 && ExplorerTree.SelectedItem is FileSystemItem nativeSelected)
        {
            anchorIndex = visible.IndexOf(nativeSelected);
        }
        if (anchorIndex < 0)
        {
            anchorIndex = targetIndex;
        }

        ClearMultiSelection();
        int low = Math.Min(anchorIndex, targetIndex);
        int high = Math.Max(anchorIndex, targetIndex);
        for (int i = low; i <= high; i++)
        {
            AddToMultiSelection(visible[i]);
        }
    }

    // The rows an operation should apply to: the multi-selection when one is
    // active, else the native single selection. Paths that stopped existing
    // (deleted/renamed behind a stale set entry) are dropped rather than
    // handed to an operation that would only fail on them.
    private List<FileSystemItem> GetEffectiveSelection()
    {
        if (_multiSelection.Count > 0)
        {
            return _multiSelection
                .Where(i => File.Exists(i.FullPath) || Directory.Exists(i.FullPath))
                .ToList();
        }
        return ExplorerTree.SelectedItem is FileSystemItem { IsPlaceholder: false, IsShowMore: false } single
            ? new List<FileSystemItem> { single }
            : new List<FileSystemItem>();
    }

    private void TreeViewItem_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // The drag-out candidate lives exactly as long as the press that armed
        // it - a release means no drag came of it, so it must never survive to
        // be replayed against some LATER press's movement (the sort-icon
        // phantom-drag postmortem was one such replay; the 2026-07-22 self-drop
        // incident pointed at the same stale-candidate class). Cleared before
        // the innermost filter below on purpose: this handler early-returns on
        // ancestor passes, and the candidate must die on release regardless.
        _itemDragStart = null;
        _itemDragCandidate = null;

        // Innermost-item filtering FIRST, before touching the deferred state:
        // this preview event tunnels through every ancestor TreeViewItem ahead
        // of the row actually released on, and consuming the deferred item on
        // one of those ancestor passes threw it away before the real target's
        // pass could act - which is why the click-to-collapse only worked on
        // top-level rows (a drive root has no ancestor to eat the state).
        // Same tunneling trap as the right-click handler's.
        if (sender is not TreeViewItem treeViewItem ||
            !ReferenceEquals((e.OriginalSource as DependencyObject)?.FindAncestor<TreeViewItem>(), treeViewItem))
        {
            return;
        }

        if (_deferredMultiClearItem is not { } deferred)
        {
            return;
        }
        _deferredMultiClearItem = null;

        // The release must land on the very row the press deferred for -
        // anything else (released over some other row after a cancelled
        // gesture) leaves the set alone.
        if (!ReferenceEquals(treeViewItem.DataContext, deferred))
        {
            return;
        }

        // A full click (press + release, no drag in between) on a set member:
        // collapse the multi-selection down to just that row, like Explorer.
        ClearMultiSelection();
        _multiSelectAnchor = deferred;
        treeViewItem.IsSelected = true;
        treeViewItem.Focus();
    }

    private void TreeViewItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TreeViewItem treeViewItem)
        {
            return;
        }

        // Marks a real gesture for the auto-collapse guard (see
        // _lastTreeUserInputTicks) - deliberately before the innermost-item
        // filter and the expander check: any press that can lead to an
        // expansion (row click, expander arrow) passes through here first.
        _lastTreeUserInputTicks = Environment.TickCount64;

        // Preview (tunneling) events pass through every ANCESTOR TreeViewItem on
        // the way down to the one actually clicked. Only act on the pass where
        // `sender` is that innermost/target item, identified by walking up from
        // the real click point - otherwise a click inside a nested folder would
        // also toggle its parent folders.
        var clickedItem = (e.OriginalSource as DependencyObject)?.FindAncestor<TreeViewItem>();
        if (!ReferenceEquals(clickedItem, treeViewItem))
        {
            return;
        }

        // Any fresh press supersedes a rename a previous click had queued up -
        // including the second press of a real double-click, which has to open
        // the file instead (see PendingRenameTimer_Tick).
        CancelPendingRename();

        // The synthetic "더 보기" row reveals the rest of its folder instead of
        // selecting anything. Marked handled so the base TreeViewItem doesn't
        // also select this non-file placeholder row.
        if (treeViewItem.DataContext is FileSystemItem { IsShowMore: true } showMore)
        {
            showMore.Parent?.ShowAllChildren();
            e.Handled = true;
            return;
        }

        // The small sort-override icon (see FileSystemItem.HasSortOverride)
        // rotates this folder's own remembered sort (N↑ -> N↓ -> D↑ -> D↓ ->
        // N↑...) instead of the row's normal click behavior (select + toggle
        // expand/collapse) below - matched by name rather than type, since
        // other Border ancestors exist further up the same row (RowBorder's
        // hover/selection highlight) that must NOT match. Clearing the
        // override entirely is a context-menu-only action now (see
        // FolderSortFollowGlobalMenuItem_Click).
        if ((e.OriginalSource as DependencyObject)?.FindAncestor<Border>() is { Name: "SortOverrideIconBorder" } &&
            treeViewItem.DataContext is FileSystemItem { IsPlaceholder: false, IsDirectory: true } overrideItem)
        {
            RotateFolderSortOverride(overrideItem);

            // Returning early here used to skip the drag-candidate reset
            // below entirely (it's normally reached a few lines down, on
            // every mouse-down regardless of target). A file clicked/selected
            // right before this left _itemDragCandidate/_itemDragStart
            // pointing at it; since nothing ever cleared them, the very next
            // press-and-slightly-move anywhere in the tree (even another
            // click on this same icon) replayed as a phantom drag of that
            // stale file onto whatever folder ended up under the cursor after
            // the resort reshuffled the rows - which is exactly what surfaced
            // as an unprompted "already exists, overwrite?" dialog after
            // sorting a folder containing a previously-selected file.
            _itemDragStart = null;
            _itemDragCandidate = null;
            e.Handled = true;
            return;
        }

        // The built-in expand/collapse arrow already toggles IsExpanded on its own
        // Click; skip our row-click handling for that case to avoid a double toggle.
        bool clickedOnExpander =
            (e.OriginalSource as DependencyObject)?.FindAncestor<ToggleButton>() is { } expander
            && ReferenceEquals(expander.FindAncestor<TreeViewItem>(), treeViewItem);

        // Multi-selection gestures. All three modifier branches mark the event
        // handled and return, so none of the single-selection behavior below
        // (folder expand toggle, slow-double-click rename, native selection)
        // runs for them - a Ctrl+click on a folder selects it WITHOUT toggling
        // it open, per the agreed design.
        if (!clickedOnExpander &&
            treeViewItem.DataContext is FileSystemItem { IsPlaceholder: false, IsShowMore: false, IsEditing: false } target)
        {
            var modifiers = Keyboard.Modifiers;
            if (modifiers.HasFlag(ModifierKeys.Control) && !modifiers.HasFlag(ModifierKeys.Shift))
            {
                // The row that was already natively selected joins the set
                // first, so "click A, then Ctrl+click B" yields {A, B} like
                // Explorer - not just {B}.
                if (_multiSelection.Count == 0 &&
                    ExplorerTree.SelectedItem is FileSystemItem { IsPlaceholder: false, IsShowMore: false } current &&
                    !ReferenceEquals(current, target))
                {
                    AddToMultiSelection(current);
                }

                if (target.IsMultiSelected)
                {
                    RemoveFromMultiSelection(target);
                    if (treeViewItem.IsSelected)
                    {
                        // Un-highlight for real: this row was both natively
                        // selected and in the set; dropping only the flag would
                        // leave the native highlight painting it selected.
                        treeViewItem.IsSelected = false;
                    }
                }
                else
                {
                    AddToMultiSelection(target);
                    // TreeViewItem.OnGotFocus selects the row it lands on, so
                    // this moves the native selection here too (the guard in
                    // ExplorerTree_SelectedItemChanged keeps the set because
                    // the row is a member) - and puts keyboard focus in the
                    // tree so Ctrl+C/Delete land in ExplorerTree_KeyDown.
                    treeViewItem.Focus();
                }

                _multiSelectAnchor = target;
                _itemDragStart = null;
                _itemDragCandidate = null;
                e.Handled = true;
                return;
            }

            if (modifiers.HasFlag(ModifierKeys.Shift))
            {
                var anchor = _multiSelectAnchor
                    ?? ExplorerTree.SelectedItem as FileSystemItem
                    ?? target;
                SelectRange(anchor, target);
                treeViewItem.Focus();

                _itemDragStart = null;
                _itemDragCandidate = null;
                e.Handled = true;
                return;
            }

            if (target.IsMultiSelected && _multiSelection.Count > 1)
            {
                // Plain press on a set member: might be the start of dragging
                // the whole set out, so the collapse waits for mouse-up (see
                // TreeViewItem_PreviewMouseLeftButtonUp). Folders are drag
                // candidates here too - unlike the single-item drag below,
                // there's no expand-toggle on this press to conflict with.
                _deferredMultiClearItem = target;
                _itemDragStart = e.GetPosition(ExplorerTree);
                _itemDragCandidate = target;
                e.Handled = true;
                return;
            }

            // Plain click outside the set: back to single-selection, and this
            // row is the anchor a later Shift+click ranges from.
            ClearMultiSelection();
            _multiSelectAnchor = target;
        }

        if (!clickedOnExpander &&
            treeViewItem.DataContext is FileSystemItem { IsPlaceholder: false, IsDirectory: true, IsEditing: false } item)
        {
            treeViewItem.IsSelected = true;
            item.IsExpanded = !item.IsExpanded;
        }

        // Explorer-style "slow double-click" rename. Files only: a click on a
        // folder toggles expand/collapse just above, so clicking an already-
        // selected folder is the normal way to collapse it and must never turn
        // into a rename. IsSelected still reads the PRE-click state here - this
        // is the tunneling preview, before the TreeViewItem selects itself - so
        // it means "was already selected before this click", which is exactly
        // the second-click condition. ClickCount == 1 skips the second press of
        // a fast double-click; the timer covers the rest (see
        // SchedulePendingRename).
        if (!clickedOnExpander && e.ClickCount == 1 && treeViewItem.IsSelected &&
            treeViewItem.DataContext is FileSystemItem { IsPlaceholder: false, IsShowMore: false, IsDirectory: false, IsEditing: false } file)
        {
            SchedulePendingRename(file);
        }

        // Drag-out candidate: files only (see TreeViewItem_PreviewMouseMove) -
        // folders already toggle expand/collapse above on this same click, so
        // dragging one out isn't as clean a gesture and isn't what was asked
        // for. Recorded on every qualifying mouse-down regardless of whether
        // the previous press ever turned into an actual drag.
        _itemDragStart = treeViewItem.DataContext is FileSystemItem { IsPlaceholder: false, IsDirectory: false, IsEditing: false }
            ? e.GetPosition(ExplorerTree)
            : null;
        _itemDragCandidate = _itemDragStart is null ? null : treeViewItem.DataContext as FileSystemItem;
    }

    private void TreeViewItem_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_itemDragCandidate is not { } item || _itemDragStart is not { } start)
        {
            return;
        }

        // Belt-and-braces companion to the mouse-up clear: a release over
        // empty tree space (below the last row) never reaches the row-level
        // mouse-up handler, so the first unpressed movement afterwards retires
        // the candidate here instead of leaving it armed for a later press.
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            _itemDragStart = null;
            _itemDragCandidate = null;
            return;
        }

        var current = e.GetPosition(ExplorerTree);
        bool pastThreshold =
            Math.Abs(current.X - start.X) >= SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(current.Y - start.Y) >= SystemParameters.MinimumVerticalDragDistance;

        if (!pastThreshold)
        {
            return;
        }

        _itemDragStart = null;
        _itemDragCandidate = null;

        // The press that started this drag will never be a plain click now, so
        // the deferred set-collapse it may have queued must not run on the
        // (much later) mouse-up.
        _deferredMultiClearItem = null;

        // Dragging the file out, not renaming it in place.
        CancelPendingRename();

        // FileDrop is the same format Explorer itself puts on the clipboard/
        // drag operation for real files, so any app that accepts a file
        // dropped from Explorer (mail client, another Explorer window, a
        // "drop to open" target, ...) accepts one dropped from here too.
        // Copy-only (no Move/Link) to match TreeViewItem_DragOver's own fixed
        // Copy effect for drops coming the other way - offering Move let
        // Explorer silently default to moving (removing the original) for a
        // same-drive drop, which read as an inconsistent surprise next to
        // every external-drop-in always being a safe copy.
        // A drag that starts on a multi-selection member carries the whole set
        // (files and folders alike); otherwise the single pressed file, as
        // before.
        string[] dragPaths = item.IsMultiSelected && _multiSelection.Count > 1
            ? _multiSelection
                .Where(i => File.Exists(i.FullPath) || Directory.Exists(i.FullPath))
                .Select(i => i.FullPath)
                .ToArray()
            : new[] { item.FullPath };
        if (dragPaths.Length == 0)
        {
            return;
        }

        var data = new DataObject(DataFormats.FileDrop, dragPaths);

        // Source is the TreeView, NOT the TreeViewItem this started on. The
        // item is a virtualized, recycled container, and a background refresh
        // (see QueueExternalRefresh) rebuilds a folder's children wholesale -
        // either of which can destroy the very element that DoDragDrop's modal
        // loop is holding as its drag source, part-way through a drag that
        // lasts as long as it takes to reach another application. When the loop
        // then ends, the mouse capture it took has nothing valid to hand back
        // to, and a capture that outlives the drag makes this app keep
        // receiving mouse input while the cursor is over other windows - which
        // looks exactly like the pointer jumping and clicking on its own. The
        // search-results drag already passed its stable ListBox for this
        // reason; the tree was the one place still passing the item.
        //
        // The finally is belt-and-braces for the same failure: whatever the
        // drag leaves behind, capture is not allowed to outlive this method.
        try
        {
            DragDrop.DoDragDrop(ExplorerTree, data, DragDropEffects.Copy);
        }
        finally
        {
            if (Mouse.Captured is not null)
            {
                Mouse.Capture(null);
            }
        }
    }

    // DragEnter/DragOver/Drop all bubble, and every TreeViewItem has its own
    // handler for them (see ExplorerTreeViewItemStyle) - e.Handled = true
    // here stops that same bubble from also reaching (and re-selecting) every
    // ancestor folder above whatever's actually under the cursor.
    private void TreeViewItem_DragOver(object sender, DragEventArgs e)
    {
        e.Handled = true;

        if (sender is not TreeViewItem { DataContext: FileSystemItem { IsPlaceholder: false, IsDirectory: true } } treeViewItem ||
            !e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.None;
            return;
        }

        e.Effects = DragDropEffects.Copy;

        // Reuses the tree's existing selection highlight as drop-target
        // feedback instead of a separate visual, so there's no ambiguity
        // about which folder a drop would land in.
        treeViewItem.IsSelected = true;
    }

    private void TreeViewItem_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;

        if (sender is not TreeViewItem { DataContext: FileSystemItem { IsPlaceholder: false, IsDirectory: true } item } ||
            e.Data.GetData(DataFormats.FileDrop) is not string[] droppedPaths)
        {
            return;
        }

        // Drops that would import an item into the folder it ALREADY lives in
        // are ignored, not overwrite-prompted: importing a file onto itself is
        // never what was meant, and the main way it happens is a click with a
        // few pixels of travel (a micro-drag) releasing on the parent row -
        // which is how "clicked the folder above and got 대체하시겠습니까?"
        // (2026-07-22 20:17) presented. Also dropped here: a folder onto
        // itself, and a folder into its own descendant - that one would
        // recursively copy the tree into itself until the disk fills.
        var importablePaths = droppedPaths.Where(p => !IsDropIntoOwnPlace(p, item.FullPath)).ToArray();
        if (importablePaths.Length == 0)
        {
            LogSelfDropSkipped(droppedPaths.Length, item.FullPath);
            return;
        }

        if (!FileOperationService.TryImportDroppedPaths(importablePaths, item.FullPath, ConfirmOverwrite, out var error))
        {
            return;
        }
        if (error is not null)
        {
            MessageBox.Show(this, error, Strings.ImportFailedTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        item.RefreshChildren();
    }

    // "Own place" = the item's current folder (it already lives in the drop
    // target), the item itself, or anywhere inside the item (a folder dropped
    // into its own subtree). See the guard in TreeViewItem_Drop.
    private static bool IsDropIntoOwnPlace(string sourcePath, string destinationFolder)
    {
        string source = sourcePath.TrimEnd('\\');
        string destination = destinationFolder.TrimEnd('\\');
        return string.Equals(Path.GetDirectoryName(source), destination, StringComparison.OrdinalIgnoreCase)
            || string.Equals(source, destination, StringComparison.OrdinalIgnoreCase)
            || destination.StartsWith(source + "\\", StringComparison.OrdinalIgnoreCase);
    }

    // Debug builds only - each line is one swallowed accidental self-drop,
    // which doubles as the instrument for the micro-drag theory: if these
    // lines appear during ordinary clicking (no deliberate drag), clicks are
    // indeed occasionally turning into drags onto a neighboring row.
    [System.Diagnostics.Conditional("DEBUG")]
    private static void LogSelfDropSkipped(int itemCount, string destinationFolder)
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Edgetree");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, "selfdrop.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  skipped self-drop of {itemCount} item(s) into {destinationFolder}{Environment.NewLine}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    private bool ConfirmOverwrite(string name)
    {
        var result = MessageBox.Show(this, string.Format(Strings.OverwriteConfirmBody, name),
            Strings.OverwriteConfirmTitle, MessageBoxButton.YesNo, MessageBoxImage.Question);
        return result == MessageBoxResult.Yes;
    }

    private void TreeViewItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is TreeViewItem { DataContext: FileSystemItem { IsPlaceholder: false, IsShowMore: false } } treeViewItem)
        {
            // Same innermost-item filtering as the left-button handler: this
            // preview event tunnels through every ANCESTOR TreeViewItem before
            // reaching the row actually clicked, and running the select below
            // on each ancestor pass briefly selected each one in turn. The
            // final selection still landed right, which is why this went
            // unnoticed - until the multi-selection guard in
            // ExplorerTree_SelectedItemChanged saw those momentary ancestor
            // selections as "selection left the set" and collapsed the set on
            // every right-click inside it.
            if (!ReferenceEquals((e.OriginalSource as DependencyObject)?.FindAncestor<TreeViewItem>(), treeViewItem))
            {
                return;
            }

            PrepareTreeRowContextMenu(treeViewItem);
        }
    }

    // Everything a tree row's context menu needs set up BEFORE it opens -
    // selection, Shift-range anchor, placement, thumbnail slot. Split out of
    // the right-button-down handler above so the menu-covered-row pass-through
    // (ExplorerItemContextMenu_PreviewMouseRightButtonDown) can run the exact
    // same sequence for the row it uncovers and then open the menu itself.
    private void PrepareTreeRowContextMenu(TreeViewItem treeViewItem)
    {
        treeViewItem.IsSelected = true;
        treeViewItem.Focus();

        // The anchor a later Shift+click ranges from follows right-clicks
        // too, matching Explorer. Browsing several images via right-click
        // (thumbnail peeks) and then Shift+clicking used to range from a
        // long-stale anchor instead of the row just right-clicked - which
        // collapsed the range to a single row (see SelectRange).
        _multiSelectAnchor = (FileSystemItem)treeViewItem.DataContext;

        // Default (mouse-point) placement opens the menu right on top of the
        // clicked row, hiding the very item it applies to. Anchoring it to
        // just the row's own header border - not the TreeViewItem itself,
        // whose bounds also cover any expanded children below it - and
        // opening below keeps the row visible regardless of the current
        // zoom level (row height already reflects the live font size/padding
        // at click time, so nothing here is a hardcoded pixel offset).
        if (treeViewItem.ContextMenu is { } menu)
        {
            treeViewItem.ApplyTemplate();
            menu.PlacementTarget = treeViewItem.Template.FindName("RowBorder", treeViewItem) as UIElement ?? treeViewItem;
            menu.Placement = PlacementMode.Bottom;

            // The thumbnail row must be configured BEFORE the menu opens:
            // doing it in ExplorerItemContextMenu_Opened - which fires
            // after the popup has already sized and positioned itself -
            // made the freshly-shown menu visibly jump as it re-laid out
            // around the appearing slot.
            if (menu.Items is [MenuItem thumbnailItem, Separator thumbnailSeparator, ..])
            {
                string? thumbnailPath =
                    _multiSelection.Count <= 1 &&
                    treeViewItem.DataContext is FileSystemItem { IsPlaceholder: false, IsShowMore: false, IsDirectory: false } file
                        ? file.FullPath
                        : null;
                UpdateThumbnailRow(thumbnailItem, thumbnailSeparator, thumbnailPath);
            }
        }
    }

    // WPF's MenuItem also fires on a RIGHT-button release while it sits inside
    // a ContextMenu (Win32 context menus work that way, and WPF kept the
    // behavior). Combined with the thumbnail row being a click target and the
    // menu opening right below the clicked row, "right-click image, right-click
    // the next image" often landed the second click's release on the open menu
    // - which invoked whatever item was under the cursor, most visibly "just
    // opening" the file via the thumbnail. Wired to every ContextMenu in the
    // XAML: a right-click release inside a menu never invokes anything.
    private void ContextMenu_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        => e.Handled = true;

    // The companion press half of the same gesture: a right-click PRESS on an
    // open tree menu means "I want the menu for the row under my cursor" (the
    // menu is covering the rows below the one it belongs to - exactly where
    // the next image sits while peeking through a folder of pictures). Close
    // this menu, find the tree row at that spot, and reopen the menu there -
    // the same one-right-click flow as when no menu was open. A press over a
    // part of the menu with no tree row beneath it just closes the menu.
    private void ExplorerItemContextMenu_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is not ContextMenu menu)
        {
            return;
        }
        menu.IsOpen = false;

        var position = Mouse.GetPosition(ExplorerTree);
        if (position.X < 0 || position.Y < 0 ||
            position.X >= ExplorerTree.ActualWidth || position.Y >= ExplorerTree.ActualHeight)
        {
            return;
        }
        if (ExplorerTree.InputHitTest(position) is not DependencyObject hit ||
            hit.FindAncestor<TreeViewItem>() is not
                { DataContext: FileSystemItem { IsPlaceholder: false, IsShowMore: false } } row)
        {
            return;
        }

        // Deferred one dispatcher hop: the old menu's close (capture release,
        // popup teardown) is still in flight during this handler, and
        // reopening the same shared ContextMenu instance synchronously from
        // inside its own event would race that teardown.
        Dispatcher.BeginInvoke(() =>
        {
            PrepareTreeRowContextMenu(row);
            if (row.ContextMenu is { } rowMenu)
            {
                rowMenu.IsOpen = true;
            }
        });
    }

    // Same pass-through for the search results list's menu.
    private void SearchResultContextMenu_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is not ContextMenu menu)
        {
            return;
        }
        menu.IsOpen = false;

        var position = Mouse.GetPosition(SearchResultsList);
        if (position.X < 0 || position.Y < 0 ||
            position.X >= SearchResultsList.ActualWidth || position.Y >= SearchResultsList.ActualHeight)
        {
            return;
        }
        if (SearchResultsList.InputHitTest(position) is not DependencyObject hit ||
            ItemsControl.ContainerFromElement(SearchResultsList, hit) is not ListBoxItem row)
        {
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            PrepareSearchRowContextMenu(row);
            if (row.ContextMenu is { } rowMenu)
            {
                // Programmatic opens don't get ContextMenuService's automatic
                // PlacementTarget; without one the menu has no anchor visual.
                rowMenu.PlacementTarget = row;
                rowMenu.IsOpen = true;
            }
        });
    }

    private void ExplorerTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        // The moment the native selection lands on a row OUTSIDE the
        // multi-selection - keyboard navigation, right-click on an unrelated
        // row, a favorites walk - the set no longer matches what reads as
        // "current" on screen, so it collapses. Selection changes our own
        // gestures cause keep the set: they either select a row that IS a
        // member (Ctrl/Shift-click's Focus) or clear the selection entirely
        // (Ctrl-click toggle-off), neither of which matches this condition.
        if (_multiSelection.Count > 0 && e.NewValue is FileSystemItem { IsMultiSelected: false })
        {
            ClearMultiSelection();
        }

        // The navigation token is deliberately NOT bumped here. A favorites
        // walk expands folders as it goes, and with auto-collapse on that
        // collapses the previously-open drive - which makes WPF move the tree
        // selection to that drive's root and fire this handler mid-walk.
        // Bumping the token on that self-induced selection change aborted the
        // in-flight walk (its captured token no longer matched), so the
        // favorite only actually moved on a second click. Supersession between
        // favorite clicks is already handled by NavigateToPath bumping the
        // token at the start of each walk, so nothing here needs to.

        // Only the immediate containing folder's guide line highlights (VS Code
        // shows just that, not the full ancestor chain up to the root) - which
        // is the selected item's own folder when it's a directory (so
        // expanding/selecting a folder highlights its own children's guide
        // line, not the guide line one level further up - the previous logic
        // always used .Parent, which for a selected folder meant highlighting
        // its parent's line instead of its own), or the parent when it's a file.
        FileSystemItem? previousGuideTarget = _selectedItem is { IsDirectory: true } ? _selectedItem : _selectedItem?.Parent;
        if (previousGuideTarget is not null)
        {
            previousGuideTarget.IsAncestorOfSelection = false;
        }

        _selectedItem = e.NewValue as FileSystemItem;

        FileSystemItem? newGuideTarget = _selectedItem is { IsDirectory: true } ? _selectedItem : _selectedItem?.Parent;
        if (newGuideTarget is not null)
        {
            newGuideTarget.IsAncestorOfSelection = true;
        }

        // Picking a folder directly in the tree keeps the favorites list in
        // sync: highlight it there too if it happens to be one, otherwise
        // clear whatever was left highlighted from an earlier NavigateToPath.
        // Skipped when this selection change is itself the result of
        // navigating to that same favorite (see
        // FavoriteListBoxItem_PreviewMouseLeftButtonDown) - it's already
        // selected there and this would just re-select the same entry.
        if (!_isNavigatingFromFavorite)
        {
            FavoritesList.SelectedItem = _selectedItem is null
                ? null
                : _settings.Favorites.FirstOrDefault(f =>
                    string.Equals(f.Path.TrimEnd('\\'), _selectedItem.FullPath.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
        }
    }

    private void RevealInExplorer_Click(object sender, RoutedEventArgs e)
    {
        if (ExplorerTree.SelectedItem is FileSystemItem { IsPlaceholder: false } item)
        {
            ShellFileService.RevealInExplorer(item.FullPath);
        }
    }

    private void ExplorerItemContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        AnyMenu_Opened(sender, e);

        // MenuItems declared inside a resource dictionary don't get an
        // auto-generated code-behind field the way named elements in the main
        // visual tree do, so these have to be found by position on the
        // ContextMenu itself instead.
        if (sender is ContextMenu { Items: [MenuItem thumbnailItem, Separator thumbnailSeparator, MenuItem multiInfoItem, Separator multiInfoSeparator, MenuItem addFavoriteItem, MenuItem newFolderItem, MenuItem refreshItem, MenuItem searchInFolderItem, MenuItem sortMenu, _, _, MenuItem openWithItem, _, _, _, MenuItem renameItem, _, _, MenuItem copyPathItem, _, MenuItem openWithCodeItem, ..] })
        {
            // The thumbnail row is NOT configured here - see
            // TreeViewItem_PreviewMouseRightButtonDown, which runs before the
            // menu opens (this event fires after the popup has already sized
            // itself, and revealing the slot at that point visibly bumped the
            // open menu). This only guards paths that bypass that handler
            // (e.g. the keyboard menu key): a row left over from an earlier
            // open that doesn't match the current selection is hidden rather
            // than showing the wrong file's picture.
            if (thumbnailItem.Visibility == Visibility.Visible &&
                _pendingThumbnailPath != (ExplorerTree.SelectedItem as FileSystemItem)?.FullPath)
            {
                thumbnailItem.Visibility = Visibility.Collapsed;
                thumbnailSeparator.Visibility = Visibility.Collapsed;
                _pendingThumbnailPath = null;
            }

            // The "N개 항목 선택됨" header only appears while a multi-selection
            // is active, so the menu stays exactly as it always was for the
            // everyday single-row case.
            bool isMultiSelection = _multiSelection.Count > 1;
            multiInfoItem.Visibility = isMultiSelection ? Visibility.Visible : Visibility.Collapsed;
            multiInfoSeparator.Visibility = multiInfoItem.Visibility;
            if (isMultiSelection && multiInfoItem.Header is TextBlock multiInfoText)
            {
                multiInfoText.Text = string.Format(Strings.MenuMultiSelectionInfo, _multiSelection.Count);
            }

            // Single-target-by-design actions grey out on a multi-selection
            // rather than silently acting on just one of the rows: 경로 복사
            // (multi-path copy was deliberately left out of the multi
            // operations) and 이름 바꾸기 (renaming several rows at once isn't a
            // thing this menu offers). RenameItem_Click carries the same guard
            // for the F2 path, which doesn't come through this menu.
            copyPathItem.IsEnabled = !isMultiSelection;
            renameItem.IsEnabled = !isMultiSelection;

            bool isFolder = ExplorerTree.SelectedItem is FileSystemItem { IsPlaceholder: false, IsDirectory: true };
            addFavoriteItem.IsEnabled = isFolder;

            // New folder is created as a child of the selected folder - it
            // doesn't make sense (and there's nowhere to put it) off a file.
            newFolderItem.IsEnabled = isFolder;

            // Refresh re-reads the selected folder's own contents from disk -
            // there's nothing to refresh on a plain file.
            refreshItem.IsEnabled = isFolder;

            // The search scope is always a folder (see StartScopeScan).
            searchInFolderItem.IsEnabled = isFolder;

            // Only makes sense to reach for while looking at a folder. Shows
            // that folder's own override if it has one (GetEffectiveFolderSort),
            // otherwise the app-wide default - either way this submenu always
            // reflects what THIS folder would sort by if (re)loaded right now.
            sortMenu.IsEnabled = isFolder;
            if (sortMenu.Items is [MenuItem byName, MenuItem byDate, _, MenuItem ascending, MenuItem descending, _, MenuItem followGlobal])
            {
                bool hasOverride = isFolder && ExplorerTree.SelectedItem is FileSystemItem { HasSortOverride: true };
                var (sortByDate, sortDescending) = isFolder && ExplorerTree.SelectedItem is FileSystemItem folderItem
                    ? GetEffectiveFolderSort(folderItem)
                    : (_settings.SortByDate, _settings.SortDescending);

                byName.IsChecked = !sortByDate;
                byDate.IsChecked = sortByDate;
                ascending.IsChecked = !sortDescending;
                descending.IsChecked = sortDescending;
                followGlobal.IsEnabled = hasOverride;
            }

            // "Open with" only makes sense for files - folders don't have a
            // file-association picker.
            openWithItem.IsEnabled =
                ExplorerTree.SelectedItem is FileSystemItem { IsPlaceholder: false, IsDirectory: false };

            // Files and folders both make sense here - Code.exe opens either
            // one directly - so this only gates on whether Code is actually
            // installed (see ShellFileService.IsCodeRegistered), not on
            // isFolder.
            openWithCodeItem.IsEnabled =
                ExplorerTree.SelectedItem is FileSystemItem { IsPlaceholder: false } && ShellFileService.IsCodeRegistered();
        }
    }

    // The image formats worth even asking the shell for a thumbnail of -
    // gating by extension keeps a right-click on an exe/txt from paying a
    // pointless extraction attempt. Whether a thumbnail actually EXISTS is
    // still the shell's call (SIIGBF_THUMBNAILONLY fails cleanly on a
    // corrupted file or a format missing its codec).
    private static readonly HashSet<string> ThumbnailExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".jfif", ".png", ".gif", ".bmp", ".webp",
        ".ico", ".tif", ".tiff", ".heic", ".heif", ".avif"
    };

    // The path the currently open menu requested a thumbnail for - compared on
    // the async arrival so a slow fetch can't paint its image into a menu that
    // has since been reopened on a different file.
    private string? _pendingThumbnailPath;

    // Shows/hides a context menu's thumbnail slot for the given file and
    // kicks off the async fetch - shared by the tree menu and the search
    // results menu, whose callers decide WHAT is eligible (single non-multi
    // file row / search file row) and pass its path, or null to hide. The
    // slot appears at its full fixed size immediately (the extension alone
    // decides whether a row gets one), so the image arriving later never
    // resizes the open menu; only a FAILED fetch collapses the row after the
    // fact, which is rare enough (corrupted file, missing codec) that the
    // one-off shrink is fine.
    private void UpdateThumbnailRow(MenuItem thumbnailItem, Separator thumbnailSeparator, string? filePath)
    {
        _pendingThumbnailPath = null;
        if (thumbnailItem.Header is not StackPanel
            {
                Children: [Border { Child: Grid { Children: [System.Windows.Controls.Image image] } }, TextBlock infoText, TextBlock dateText]
            })
        {
            return;
        }
        image.Source = null;
        infoText.Text = string.Empty;
        dateText.Text = string.Empty;

        bool show = filePath is not null &&
            ThumbnailExtensions.Contains(Path.GetExtension(filePath)) &&
            File.Exists(filePath);

        thumbnailItem.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        thumbnailSeparator.Visibility = thumbnailItem.Visibility;
        if (!show)
        {
            return;
        }

        string path = filePath!;
        _pendingThumbnailPath = path;

        // Format + file size under the preview (modified date on its own
        // second line), shown immediately; the pixel dimensions (which need
        // the file's header, read on the background hop) slot in between when
        // the thumbnail arrives. Read the cheap parts synchronously:
        // File.Exists above already touched this path's metadata, so the
        // FileInfo comes from the same warm cache.
        string format = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
        string sizeText = string.Empty;
        try
        {
            var fileInfo = new FileInfo(path);
            sizeText = FormatFileSize(fileInfo.Length);
            infoText.Text = format + "  ·  " + sizeText;
            dateText.Text = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm");
        }
        catch (Exception e2) when (e2 is IOException or UnauthorizedAccessException)
        {
            // Metadata gone mid-read (file deleted between the check and
            // here) - the preview fetch below will fail and collapse the
            // row on its own; no info line is fine meanwhile.
        }

        // Requested in physical pixels so the shell hands over enough
        // resolution to stay sharp on scaled displays. The slot stretches to
        // the menu's content width, which isn't known until the menu lays out
        // - the 1.5x headroom over the slot's floor width covers the widths
        // menus actually reach, and SIIGBF_BIGGERSIZEOK makes over-asking
        // cheap anyway.
        double slotWidth = Application.Current.Resources["MenuThumbnailWidth"] as double? ?? 160.0;
        int pixelSize = (int)Math.Ceiling(slotWidth * 1.5 * VisualTreeHelper.GetDpi(this).DpiScaleX);

        ShellThumbnailService.GetThumbnail(path, pixelSize, (thumbnail, pixelWidth, pixelHeight) =>
        {
            if (_pendingThumbnailPath != path)
            {
                return;
            }

            if (thumbnail is null)
            {
                // No thumbnail to be had - drop the empty slot rather than
                // leaving a blank box at the top of the menu.
                thumbnailItem.Visibility = Visibility.Collapsed;
                thumbnailSeparator.Visibility = Visibility.Collapsed;
                return;
            }

            image.Source = thumbnail;
            if (pixelWidth > 0 && pixelHeight > 0)
            {
                infoText.Text = $"{format}  ·  {pixelWidth}×{pixelHeight}  ·  {sizeText}";
            }
        });
    }

    // The thumbnail is a click target too: opening the file is the natural
    // "show me properly" follow-up to a glance, and it's why a bigger
    // preview flyout wasn't needed.
    private void ThumbnailMenuItem_Click(object sender, RoutedEventArgs e)
        => OpenItem_Click(sender, e);

    // "1.2 MB" style, one decimal from KB up - for the thumbnail's info line.
    private static string FormatFileSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):0.#} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):0.#} GB"
    };

    // Re-reads the selected folder's own children from disk - the direct,
    // scoped way to see a changed sort order (or new/removed files) without
    // the collateral damage of refreshing the whole tree, which would also
    // collapse every other expanded folder's own descendants.
    private void RefreshFolder_Click(object sender, RoutedEventArgs e)
    {
        if (ExplorerTree.SelectedItem is FileSystemItem { IsPlaceholder: false, IsDirectory: true } item)
        {
            item.RefreshChildren();
        }
    }

    // One recursive FileSystemWatcher per drive root, covering the whole
    // drive regardless of expand state - see _driveWatchers' own comment for
    // why this replaced one-watcher-per-expanded-folder. Started once at
    // startup and left running for the app's lifetime; there are only ever a
    // handful of drives, so this is cheap compared to a count that scales
    // with how many folders someone has expanded.
    private void StartDriveWatchers()
    {
        foreach (var root in _roots)
        {
            try
            {
                var watcher = new FileSystemWatcher(root.FullPath)
                {
                    IncludeSubdirectories = true,
                    // FileName/DirectoryName catch add/remove/rename. Attributes
                    // is added so toggling an item's Hidden/System attribute
                    // (which now filters it out of the tree, see
                    // FileSystemService.VisibleEntryOptions) reflects live via
                    // the Changed handler below. LastWrite is still deliberately
                    // excluded - the tree only shows names, so an in-place edit
                    // (same name) never changes the listing, and watching it
                    // would let every autosave/log write on the whole drive
                    // churn some folder's refresh for nothing. Attribute events
                    // are far rarer (an already-set archive bit doesn't re-fire
                    // on each write), and the refresh is expanded-folder-only +
                    // debounced regardless.
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Attributes,

                    // A whole-drive recursive watcher rides on one kernel change
                    // buffer, and the default is only 8KB - a burst of activity
                    // anywhere on the drive (a build, a browser cache flush)
                    // overflows it and every queued event is silently thrown
                    // away, which surfaced as "created a file in an expanded
                    // folder and it just never appeared". 64KB is the documented
                    // ceiling; the Error handler below covers the bursts that
                    // still blow past it.
                    InternalBufferSize = 64 * 1024
                };
                watcher.Created += OnDriveWatcherEvent;
                watcher.Deleted += OnDriveWatcherEvent;
                watcher.Renamed += OnDriveWatcherEvent;
                watcher.Changed += OnDriveWatcherEvent;
                watcher.Error += OnDriveWatcherError;
                watcher.EnableRaisingEvents = true;
                _driveWatchers.Add(watcher);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                // Drive not ready/accessible (empty optical drive, disconnected
                // network share, ...) right at startup - same tolerance as any
                // other filesystem race elsewhere in this app; that one drive
                // just doesn't get live updates.
            }
        }
    }

    // Fires on the FileSystemWatcher's own thread pool thread, for every
    // change anywhere on the whole drive - only e.FullPath's parent folder is
    // what might need refreshing (that's the folder whose own listing
    // changed), and only if it's actually expanded right now.
    private void OnDriveWatcherEvent(object sender, FileSystemEventArgs e)
    {
        if (Path.GetDirectoryName(e.FullPath) is not { } folderPath)
        {
            return;
        }

        Dispatcher.BeginInvoke(() => QueueExternalRefresh(folderPath));
    }

    // The watcher's change buffer overflowed (or the handle failed): an
    // unknown number of events for this drive were dropped on the floor, so
    // per-folder patching is off the table - resync everything the user
    // actually has open under that drive from disk instead. The watcher
    // itself keeps running after an overflow; nothing needs restarting.
    private void OnDriveWatcherError(object sender, ErrorEventArgs e)
    {
        if ((sender as FileSystemWatcher)?.Path is not { } rootPath)
        {
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            LogWatcherError(rootPath, e.GetException());
            if (FindLoadedItemForPath(rootPath) is { ChildrenLoaded: true } rootItem)
            {
                ResyncLoadedSubtree(rootItem);
            }
        });
    }

    // Event-loss recovery has to assume ANY loaded folder under the drive may
    // be stale, not just the root's own listing: merge every loaded folder
    // that's on screen (expanded), and flag loaded-but-collapsed ones to
    // re-read on their next expand - same contract as the normal collapsed
    // path in QueueExternalRefresh.
    private void ResyncLoadedSubtree(FileSystemItem item)
    {
        if (!item.ChildrenLoaded)
        {
            return;
        }
        if (!item.IsExpanded)
        {
            item.PendingExternalRefresh = true;
            return;
        }

        item.MergeChildrenFromDisk();
        foreach (var child in item.Children)
        {
            if (child is { IsPlaceholder: false, IsShowMore: false, IsDirectory: true })
            {
                ResyncLoadedSubtree(child);
            }
        }
    }

    // Debug builds only - says how often drive-wide bursts actually overflow
    // the enlarged buffer, i.e. whether lost events remain a live suspect for
    // "a new file didn't show up" reports or the resync above has it covered.
    [System.Diagnostics.Conditional("DEBUG")]
    private static void LogWatcherError(string rootPath, Exception exception)
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Edgetree");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, "watcher.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  watcher error on {rootPath}: {exception.GetType().Name} {exception.Message} - resyncing expanded folders{Environment.NewLine}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    // Debounced per-folder-path - see _pendingExternalRefreshes' own comment.
    private void QueueExternalRefresh(string folderPath)
    {
        if (_pendingExternalRefreshes.TryGetValue(folderPath, out var existing))
        {
            existing.LastEventTicks = Environment.TickCount64;
            existing.Timer.Stop();
            existing.Timer.Start();
            return;
        }

        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        var pending = new PendingExternalRefresh(timer);
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _pendingExternalRefreshes.Remove(folderPath);
            if (FindLoadedItemForPath(folderPath) is not { } item)
            {
                return;
            }

            // A loaded-but-collapsed folder can't be patched live (only
            // expanded folders are), but it can't be forgotten either:
            // EnsureChildrenLoaded caches, so the next expand would show the
            // listing from BEFORE this change. Mark it; TreeViewItem_Expanded
            // re-reads marked folders on the next expand. Same staleness test
            // as the expanded path below.
            if (!item.IsExpanded)
            {
                if (item.ChildrenLoaded && item.LastLoadedTicks <= pending.LastEventTicks)
                {
                    item.PendingExternalRefresh = true;
                }
                return;
            }

            // Nothing to redo if this folder was already re-read from disk
            // after the change was reported. Expanding a folder reads it fresh,
            // so a watcher event landing in that same moment would otherwise
            // tear down every child and rebuild it - and RefreshChildren
            // emptying then refilling a mid-list folder makes the tree's total
            // height collapse and grow again, which clamps the ScrollViewer's
            // offset and yanks the view upward. That is the intermittent
            // "expanding a folder sometimes jumps the view to the top" report:
            // rare because it needs a real background change to land inside
            // that window, and unrelated to how big the folder is.
            //
            // Comparing timestamps rather than just skipping anything recently
            // loaded keeps this exact: a change that genuinely lands after the
            // read still refreshes, so nothing goes stale.
            if (item.LastLoadedTicks > pending.LastEventTicks)
            {
                return;
            }

            RefreshFolderPreservingState(item);
        };
        _pendingExternalRefreshes[folderPath] = pending;
        timer.Start();
    }

    // Walks down from the matching drive root by name, same idea as
    // FindItemForPath, but must NEVER force-load an ancestor folder the user
    // never opened just to check whether some deeply nested, never-expanded
    // folder happens to contain the changed path - so this bails out (returns
    // null) the moment the next segment isn't already an existing, already-
    // loaded child, instead of loading it like FindItemForPath deliberately does
    // for restoring saved state.
    private FileSystemItem? FindLoadedItemForPath(string path)
    {
        path = path.TrimEnd('\\');
        var current = _roots.FirstOrDefault(r =>
            path.StartsWith(r.FullPath.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
        if (current is null)
        {
            return null;
        }

        string relative = path.Substring(current.FullPath.TrimEnd('\\').Length).Trim('\\');
        if (relative.Length == 0)
        {
            return current;
        }

        foreach (var segment in relative.Split('\\', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!current.ChildrenLoaded)
            {
                return null;
            }
            var next = current.Children.FirstOrDefault(c =>
                !c.IsPlaceholder && !c.IsShowMore &&
                string.Equals(c.Name, segment, StringComparison.OrdinalIgnoreCase));
            if (next is null)
            {
                return null;
            }
            current = next;
        }
        return current;
    }

    // A background/watcher-driven refresh of one folder. This went through
    // three designs; the history matters, don't regress it:
    //
    // 1. Clear-and-refill + expanded-path replay (snapshot -> RefreshChildren
    //    -> re-expand). Rebuilding an ANCESTOR wholesale destroys every
    //    realized row beneath it - during IDE project-switch churn (a
    //    refresh every few seconds) the viewport sat blank faster than it
    //    could recover (2026-07-23 02:4x incident; autocollapse.log caught
    //    whole expanded chains regenerating twice within a second).
    // 2. + a coalesced post-refresh ForceTreeRedraw. Wrong altitude:
    //    redraw.log showed dozens of external-refresh passes around the
    //    blank, so redraw-after-teardown demonstrably doesn't win.
    // 3. Diff-merge (current): MergeChildrenFromDisk keeps surviving rows'
    //    instances and containers, so expanded descendants and the scroll
    //    position are structurally undisturbed, and a no-op change (hidden
    //    file, attribute flip) performs zero collection operations. No
    //    expanded-path replay is needed - state never leaves the surviving
    //    instances. Reselect logic stays deliberately absent (the old
    //    NavigateToPath reselect was its own bug saga - see git history).
    private void RefreshFolderPreservingState(FileSystemItem item)
        => item.MergeChildrenFromDisk();

    private void OpenItem_Click(object sender, RoutedEventArgs e)
    {
        if (ExplorerTree.SelectedItem is not FileSystemItem { IsPlaceholder: false } item)
        {
            return;
        }

        if (item.IsDirectory)
        {
            // A menu click is a deliberate gesture too, but it happens outside
            // the tree - stamp it so the expansion below still auto-collapses
            // the way it always has (see _lastTreeUserInputTicks).
            _lastTreeUserInputTicks = Environment.TickCount64;
            item.IsExpanded = true;
        }
        else
        {
            ShellFileService.OpenWithDefaultApp(item.FullPath);
        }
    }

    private void OpenWithPicker_Click(object sender, RoutedEventArgs e)
    {
        if (ExplorerTree.SelectedItem is FileSystemItem { IsPlaceholder: false, IsDirectory: false } item)
        {
            ShellFileService.OpenWithPicker(item.FullPath);
        }
    }

    private void CopyItem_Click(object sender, RoutedEventArgs e)
    {
        var items = GetEffectiveSelection();
        if (items.Count > 0)
        {
            FileOperationService.CopyToClipboard(items.Select(i => i.FullPath));
        }
    }

    private void PasteItem_Click(object sender, RoutedEventArgs e)
    {
        if (ExplorerTree.SelectedItem is not FileSystemItem { IsPlaceholder: false } item)
        {
            return;
        }

        string destinationFolder = item.IsDirectory ? item.FullPath : Path.GetDirectoryName(item.FullPath) ?? string.Empty;
        if (destinationFolder.Length == 0)
        {
            return;
        }

        if (!FileOperationService.TryPaste(destinationFolder, out var error))
        {
            return; // Clipboard has nothing pasteable.
        }
        if (error is not null)
        {
            MessageBox.Show(this, error, Strings.PasteFailedTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        (item.IsDirectory ? item : item.Parent)?.RefreshChildren();
    }

    // Right-click on empty tree space (see ExplorerEmptySpaceContextMenu) has
    // no clicked-on item to anchor to, unlike every other file operation here.
    // Falls back through: selected folder -> selected file's parent -> the
    // first drive root, so it's never simply a no-op.
    private void NewFolder_Click(object sender, RoutedEventArgs e)
    {
        FileSystemItem? target = ExplorerTree.SelectedItem switch
        {
            FileSystemItem { IsPlaceholder: false, IsDirectory: true } folder => folder,
            FileSystemItem { IsPlaceholder: false, IsDirectory: false } file => file.Parent,
            _ => _roots.FirstOrDefault()
        };

        if (target is null)
        {
            return;
        }

        if (!FileOperationService.TryCreateFolder(target.FullPath, out var createdPath, out var error))
        {
            MessageBox.Show(this, error, Strings.NewFolderFailedTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        target.RefreshChildren();
        target.IsExpanded = true;

        // RefreshChildren rebuilds Children with fresh instances, so the new
        // folder has to be looked up by name rather than reusing any prior
        // reference - then dropped straight into inline rename, matching
        // Explorer/VS Code's "type the name right away" new-folder flow.
        string createdName = Path.GetFileName(createdPath!);
        var newItem = target.Children.FirstOrDefault(c =>
            !c.IsPlaceholder && string.Equals(c.Name, createdName, StringComparison.OrdinalIgnoreCase));
        if (newItem is null)
        {
            return;
        }

        if (ExplorerTree.ItemContainerGenerator.ContainerFromItem(target) is TreeViewItem targetContainer)
        {
            targetContainer.UpdateLayout();
            if (targetContainer.ItemContainerGenerator.ContainerFromItem(newItem) is TreeViewItem newContainer)
            {
                newContainer.BringIntoView();
            }
        }

        BeginInlineRename(newItem);
    }

    // VS Code-style inline rename: swaps the row's name TextBlock for a
    // TextBox in place (see the HierarchicalDataTemplate's IsEditing
    // DataTrigger) instead of opening a separate popup dialog.
    private void RenameItem_Click(object sender, RoutedEventArgs e)
    {
        // Renaming is single-target only; with a multi-selection active this
        // is greyed out on the menu (see ExplorerItemContextMenu_Opened), and
        // this guard covers the F2 path that skips the menu.
        if (_multiSelection.Count > 1)
        {
            return;
        }

        if (ExplorerTree.SelectedItem is not FileSystemItem { IsPlaceholder: false } item)
        {
            return;
        }

        BeginInlineRename(item);
    }

    private static void BeginInlineRename(FileSystemItem item)
    {
        item.EditingName = item.Name;
        item.IsEditing = true;
    }

    private void SchedulePendingRename(FileSystemItem item)
    {
        _pendingRenameItem = item;

        if (_pendingRenameTimer is null)
        {
            _pendingRenameTimer = new System.Windows.Threading.DispatcherTimer();
            _pendingRenameTimer.Tick += PendingRenameTimer_Tick;
        }

        // Long enough to rule out a real double-click, which must open the file
        // instead. Read from the user's own Windows double-click speed rather
        // than hardcoding a guess, plus a small margin so a double-click landing
        // right at that limit still cancels this in time.
        _pendingRenameTimer.Interval = TimeSpan.FromMilliseconds(
            System.Windows.Forms.SystemInformation.DoubleClickTime + 100);
        _pendingRenameTimer.Stop();
        _pendingRenameTimer.Start();
    }

    private void CancelPendingRename()
    {
        _pendingRenameTimer?.Stop();
        _pendingRenameItem = null;
    }

    private void PendingRenameTimer_Tick(object? sender, EventArgs e)
    {
        _pendingRenameTimer?.Stop();

        if (_pendingRenameItem is not { } item)
        {
            return;
        }
        _pendingRenameItem = null;

        // Bail out if anything since the click makes renaming the wrong move:
        // the selection moved on (a click elsewhere this handler never saw), an
        // edit already started some other way, or the button is still held - a
        // drag, or a press that hasn't become a click yet.
        if (!ReferenceEquals(ExplorerTree.SelectedItem, item) || item.IsEditing ||
            Mouse.LeftButton == MouseButtonState.Pressed)
        {
            return;
        }

        BeginInlineRename(item);
    }

    private void RenameTextBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not TextBox { DataContext: FileSystemItem item } textBox || !textBox.IsVisible)
        {
            return;
        }

        textBox.Focus();

        // Select just the name part (not the extension) for files, matching
        // Explorer's rename behavior; select everything for folders/extensionless names.
        int dot = item.Name.LastIndexOf('.');
        if (dot > 0 && !item.IsDirectory)
        {
            textBox.Select(0, dot);
        }
        else
        {
            textBox.SelectAll();
        }
    }

    private void RenameTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: FileSystemItem item })
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            CommitInlineRename(item);
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            item.IsEditing = false;
        }
    }

    private void RenameTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: FileSystemItem item })
        {
            CommitInlineRename(item);
        }
    }

    private void CommitInlineRename(FileSystemItem item)
    {
        // Re-entrancy guard: LostFocus fires again once IsEditing flips the
        // TextBox to Collapsed (and also if TryRename's failure MessageBox
        // below steals focus), so only the first call should act.
        if (!item.IsEditing)
        {
            return;
        }
        item.IsEditing = false;

        string newName = item.EditingName;
        if (string.IsNullOrWhiteSpace(newName) || newName == item.Name)
        {
            return;
        }

        if (!FileOperationService.TryRename(item.FullPath, newName, out var error))
        {
            MessageBox.Show(this, error, Strings.RenameFailedTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        item.Parent?.RefreshChildren();
    }

    private void DeleteItem_Click(object sender, RoutedEventArgs e)
    {
        var items = GetEffectiveSelection();
        if (items.Count == 0)
        {
            return;
        }

        string confirmBody = items.Count == 1
            ? string.Format(Strings.DeleteConfirmBody, items[0].Name)
            : string.Format(Strings.DeleteConfirmBodyMultiple, items.Count);
        var result = MessageBox.Show(
            this,
            confirmBody,
            Strings.DeleteConfirmTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        // Every failure is collected and shown once at the end rather than
        // popping a box per item mid-loop. A selection can also contain both a
        // folder and something inside it - deleting the folder first makes the
        // child's delete a silent no-op (TryDeleteToRecycleBin succeeds on an
        // already-gone path), which is the right outcome.
        var failures = new List<string>();
        var parentsToRefresh = new HashSet<FileSystemItem>();
        foreach (var item in items)
        {
            if (!FileOperationService.TryDeleteToRecycleBin(item.FullPath, out var error))
            {
                if (error is not null)
                {
                    failures.Add(error);
                }
                continue;
            }

            if (item.Parent is { } parent)
            {
                parentsToRefresh.Add(parent);
            }
            RemoveFavoritesUnder(item.FullPath);
        }

        // Refresh each affected folder once, however many of its children
        // were deleted. The set members are about to be discarded by these
        // rebuilds anyway, so the multi-selection ends here too.
        ClearMultiSelection();
        foreach (var parent in parentsToRefresh)
        {
            parent.RefreshChildren();
        }

        if (failures.Count > 0)
        {
            MessageBox.Show(this, string.Join(Environment.NewLine, failures.Distinct()),
                Strings.DeleteFailedTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // Deleting a folder that's itself favorited - or that contains a
    // favorited descendant, since the whole subtree goes with it to the
    // Recycle Bin - would otherwise leave the favorites list pointing at a
    // path that no longer exists. Drops every matching entry and re-fits the
    // panel to the new (possibly smaller, possibly empty) count.
    private void RemoveFavoritesUnder(string deletedPath)
    {
        string trimmed = deletedPath.TrimEnd('\\');
        string prefix = trimmed + '\\';

        var stale = _settings.Favorites.Where(f =>
            string.Equals(f.Path.TrimEnd('\\'), trimmed, StringComparison.OrdinalIgnoreCase) ||
            f.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stale.Count == 0)
        {
            return;
        }

        foreach (var entry in stale)
        {
            _settings.Favorites.Remove(entry);
        }
        UpdateFavoritesPanelVisibility();
    }

    private void CopyPath_Click(object sender, RoutedEventArgs e)
    {
        if (ExplorerTree.SelectedItem is FileSystemItem { IsPlaceholder: false } item)
        {
            FileOperationService.CopyPathToClipboard(item.FullPath);
        }
    }

    private void OpenTerminal_Click(object sender, RoutedEventArgs e)
    {
        if (ExplorerTree.SelectedItem is not FileSystemItem { IsPlaceholder: false } item)
        {
            return;
        }

        string folder = item.IsDirectory ? item.FullPath : Path.GetDirectoryName(item.FullPath) ?? string.Empty;
        if (folder.Length > 0)
        {
            ShellFileService.OpenTerminal(folder);
        }
    }

    private void OpenWithCode_Click(object sender, RoutedEventArgs e)
    {
        if (ExplorerTree.SelectedItem is FileSystemItem { IsPlaceholder: false } item)
        {
            ShellFileService.OpenWithCode(item.FullPath);
        }
    }

    private void ShowProperties_Click(object sender, RoutedEventArgs e)
    {
        if (ExplorerTree.SelectedItem is FileSystemItem { IsPlaceholder: false } item)
        {
            ShellFileService.ShowProperties(item.FullPath);
        }
    }

    private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (!CanResizeWidth)
        {
            return;
        }

        // Right-docked, the thumb sits on the left edge (see
        // UpdateResizeThumbVisibility) and dragging it left should grow the
        // window - the opposite sign from the left-docked case - and the
        // window has to slide left by the same amount to keep its right edge
        // anchored to the screen edge, since Width alone only grows rightward.
        double rawDelta = _settings.DockOnRight ? -e.HorizontalChange : e.HorizontalChange;
        double newWidth = ClampExpandedWidth(Width + rawDelta);
        SetExpandedWidthAnchored(newWidth);
    }

    // Shared anchoring logic between manual drag-resize and the fit/restore
    // double-click below - right-docked, the window has to slide left by
    // however much it's growing/shrinking to keep its right edge pinned to
    // the screen edge, since Width alone only grows/shrinks rightward.
    private void SetExpandedWidthAnchored(double newWidth)
    {
        newWidth = ClampExpandedWidth(newWidth);
        if (_settings.DockOnRight)
        {
            Left -= newWidth - Width;
        }
        Width = newWidth;
        _settings.ExpandedWidth = newWidth;
    }

    // Double-clicking the resize thumb auto-fits the window to exactly the
    // widest currently-realized row (tree or favorites) - same "column
    // divider double-click" convention as FavoritesSplitter_MouseDoubleClick's
    // height-fit for favorites. A second double-click (while still in the
    // fitted state) restores the width from just before the fit instead of
    // fitting again.
    private void ResizeThumb_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Left button only - same latent trap as ExplorerTree_MouseDoubleClick
        // (see its comment): WPF raises this for right-button double-clicks too.
        if (e.ChangedButton != MouseButton.Left || !CanResizeWidth)
        {
            return;
        }

        // Only "restore" if the window is still exactly at the width the
        // last fit set it to - if Width has moved since (a manual drag, or
        // anything else), the pending toggle is stale and this click should
        // fit fresh from wherever the window is now instead of jumping back
        // to an old, no-longer-relevant value.
        bool stillAtLastFit = _contentFitWidthApplied is { } lastFit && Math.Abs(Width - lastFit) < 0.5;
        if (stillAtLastFit && _contentFitRestoreWidth is { } restoreWidth)
        {
            SetExpandedWidthAnchored(restoreWidth);
            _contentFitRestoreWidth = null;
            _contentFitWidthApplied = null;
            return;
        }

        if (ComputeContentFitWidth() is not { } fitWidth)
        {
            return;
        }

        _contentFitRestoreWidth = Width;
        SetExpandedWidthAnchored(fitWidth);
        _contentFitWidthApplied = Width;
    }

    // Widest currently-realized row's own natural (untrimmed) text width plus
    // however far that text already sits from the window's left edge (icon,
    // indent, everything before it) - read directly off the real, already-
    // laid-out visual tree via TransformToVisual rather than re-deriving the
    // indent/icon math independently, so this can never drift out of sync
    // with whatever ApplyLayoutMetrics currently has TabSpacing/icons set to.
    // Only realized containers are measured - virtualization means a row
    // scrolled far out of the current view has no visual tree to measure at
    // all, which matches "fit to the window's current content" rather than
    // force-realizing a potentially huge, mostly off-screen subtree.
    private double? ComputeContentFitWidth()
    {
        double maxWidth = 0;
        bool any = false;

        foreach (var textBlock in EnumerateVisibleTreeNameTextBlocks(ExplorerTree))
        {
            maxWidth = Math.Max(maxWidth, RowFitWidth(textBlock));
            any = true;
        }

        if (FavoritesList.Visibility == Visibility.Visible)
        {
            for (int i = 0; i < FavoritesList.Items.Count; i++)
            {
                if (FavoritesList.ItemContainerGenerator.ContainerFromIndex(i) is not ListBoxItem favoriteItem)
                {
                    continue;
                }
                favoriteItem.ApplyTemplate();
                if (favoriteItem.Template.FindName("FavoriteNameText", favoriteItem) is TextBlock favoriteText)
                {
                    maxWidth = Math.Max(maxWidth, RowFitWidth(favoriteText));
                    any = true;
                }
            }
        }

        // A little breathing room past the longest line, plus the overlay
        // scrollbar's own width so it doesn't end up sitting on top of (and
        // re-clipping) the text it was just sized to fully show.
        return any ? maxWidth + 24 : null;
    }

    private double RowFitWidth(TextBlock textBlock)
    {
        System.Windows.Point origin = textBlock.TransformToVisual(this).Transform(new System.Windows.Point(0, 0));
        return origin.X + MeasureTextWidth(textBlock);
    }

    private static double MeasureTextWidth(TextBlock textBlock)
    {
        var typeface = new Typeface(textBlock.FontFamily, textBlock.FontStyle, textBlock.FontWeight, textBlock.FontStretch);
        var formatted = new FormattedText(
            textBlock.Text,
            System.Globalization.CultureInfo.CurrentUICulture,
            System.Windows.FlowDirection.LeftToRight,
            typeface,
            textBlock.FontSize,
            System.Windows.Media.Brushes.Black,
            VisualTreeHelper.GetDpi(textBlock).PixelsPerDip);
        return formatted.Width;
    }

    // Recurses only into already-expanded items, mirroring how the
    // virtualizing panel itself only ever realizes children under an
    // expanded parent - an unrealized ContainerFromIndex just returns null
    // and is skipped, rather than force-generating a container that doesn't
    // otherwise exist.
    private static IEnumerable<TextBlock> EnumerateVisibleTreeNameTextBlocks(ItemsControl container)
    {
        for (int i = 0; i < container.Items.Count; i++)
        {
            if (container.ItemContainerGenerator.ContainerFromIndex(i) is not TreeViewItem item)
            {
                continue;
            }

            if (item.DataContext is FileSystemItem { IsPlaceholder: false, IsShowMore: false, IsEditing: false } &&
                GetNameTextBlock(item) is { } textBlock)
            {
                yield return textBlock;
            }

            if (item.IsExpanded)
            {
                foreach (var nested in EnumerateVisibleTreeNameTextBlocks(item))
                {
                    yield return nested;
                }
            }
        }
    }

    // PART_Header's ContentTemplate is the HierarchicalDataTemplate applied
    // implicitly (matched by DataType, never assigned through an explicit
    // ContentTemplate="{StaticResource ...}" anywhere) - ContentTemplate.
    // FindName on it turned out not to reliably resolve "NameText" the way
    // an explicit ControlTemplate's FindName does elsewhere in this file
    // (e.g. the PART_Header lookup right below, or FavoriteNameText's own
    // lookup for the favorites list), so this walks the real, already-
    // rendered visual tree instead - it doesn't depend on how the template
    // got applied, only that the row is actually on screen.
    private static TextBlock? GetNameTextBlock(TreeViewItem item)
    {
        item.ApplyTemplate();
        if (item.Template.FindName("PART_Header", item) is not ContentPresenter presenter)
        {
            return null;
        }
        return FindDescendantByName<TextBlock>(presenter, "NameText");
    }

    // Scoped to whatever subtree is passed in (PART_Header's own content,
    // not the whole TreeViewItem) so this never accidentally wanders into an
    // expanded row's own children - those get their own separate call from
    // EnumerateVisibleTreeNameTextBlocks's own recursion instead.
    private static T? FindDescendantByName<T>(DependencyObject root, string name) where T : FrameworkElement
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed && typed.Name == name)
            {
                return typed;
            }
            if (FindDescendantByName<T>(child, name) is { } found)
            {
                return found;
            }
        }
        return null;
    }

    // See MinimizeButton_Click - both tray-hide entry points (the "_" button
    // here and App's tray-menu toggle) persist state on the way out.
    public void SaveStateBeforeHiding() => SaveCurrentWidth();

    private void SaveCurrentWidth()
    {
        // Only capture Width as the docked width while actually docked and
        // not auto-hidden - a floating window's (possibly much wider) size
        // shouldn't leak into the docked sidebar's remembered width (every
        // launch starts docked), and the auto-hide sliver's own tiny width
        // definitely shouldn't either.
        if (_isDocked && !_settings.IsAutoHidden)
        {
            _settings.ExpandedWidth = ClampExpandedWidth(Width);
        }

        _settings.ExpandedFolderPaths = CollectAllExpandedPaths();
        _settings.LastSelectedPath = (ExplorerTree.SelectedItem as FileSystemItem)?.FullPath;

        _settingsService.Save(_settings);
    }

    // ===================================================================
    // File search (Ctrl+F view). Phase 1: one folder scope, an in-memory,
    // session-only index (no disk cache, no staleness) - see
    // Services/FileSearchService and the design note in TODO.md.
    // ===================================================================

    // Folder group (default) or a global name/date sort, cycled by the sort
    // button. FolderGroup clusters same-folder files under one header; the
    // name/date modes sort globally and mostly break groups apart (with
    // still-adjacent same-folder runs re-collapsing) - see RunSearchFilter.
    private enum SearchSortMode { FolderGroup = 0, NameAsc = 1, NameDesc = 2, DateAsc = 3, DateDesc = 4 }

    // Materialized rows (folder headers interleaved with capped file rows).
    // Rebuilt and reassigned to SearchResultsList.ItemsSource wholesale on each
    // filter - one Reset beats an ObservableCollection's Clear + N Adds (a
    // notification per row) when a query can match up to the display cap.
    private List<SearchRow> _searchRows = new();

    // The full scanned scope, held on the UI thread. The background scan
    // appends to it in batches via IProgress; every keystroke filters it into
    // _searchRows. Filtering RAM is what the user experiences as "the index".
    private readonly List<FileSearchService.SearchEntry> _searchEntries = new();

    private System.Windows.Threading.DispatcherTimer? _searchDebounceTimer;
    private CancellationTokenSource? _searchScanCts;
    private bool _isSearchViewActive;
    private bool _searchScanning;
    private string? _searchScopeFolder;

    // Drag-out candidate (a result file being pressed, before the move passes
    // the drag threshold) - same press/threshold/drag pattern the tree uses, so
    // a plain click still navigates and only a real drag copies the file out.
    private System.Windows.Point? _searchDragStart;
    private FileSearchService.SearchEntry? _searchDragCandidate;

    // Results-only sort/grouping (independent of the tree's sort), cycled by
    // the button in the scope row.
    private SearchSortMode _searchSortMode = SearchSortMode.FolderGroup;

    // Re-filtering the whole growing list on every streamed batch would be
    // O(n^2) across a big scan; this throttles the mid-scan re-filter. A final,
    // unthrottled filter always runs once the scan completes.
    private int _lastSearchFilterTick;

    // How many file rows the results currently show. Starts at one page and
    // grows by a page each time "더 보기" is clicked; reset to one page whenever
    // the query or scope changes.
    private int _searchDisplayLimit = SearchResultDisplayCap;

    // Terminal-style history recall in the search box (Up = older, Down = newer
    // then back to the draft). -1 means "not navigating - showing the draft";
    // otherwise an index into _settings.SearchHistory (0 = most recent). The
    // draft is what was typed before the first Up, restored on the way back
    // down. _suppressHistoryReset guards the programmatic SetText from the
    // manual-edit reset in SearchBox_TextChanged.
    private int _searchHistoryNavIndex = -1;
    private string _searchHistoryDraft = string.Empty;
    private bool _suppressHistoryReset;

    private const int SearchResultDisplayCap = 1000;
    private const int SearchHistoryMax = 15;

    // Swapped onto SearchButtonIcon.Data (same idea as CollapseAllArrow): a
    // magnifier while the explorer shows, a left chevron ("back") while search
    // is up.
    private static readonly Geometry SearchGlyphMagnifier =
        Geometry.Parse("M4,7 A3.5,3.5 0 1 0 11,7 A3.5,3.5 0 1 0 4,7 M10,10 L13.5,13.5");
    private static readonly Geometry SearchGlyphBack =
        Geometry.Parse("M10,3 L5,8 L10,13");

    private void InitializeSearch()
    {
        SearchResultsList.ItemsSource = _searchRows;

        _searchDebounceTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _searchDebounceTimer.Tick += (_, _) =>
        {
            _searchDebounceTimer!.Stop();
            RunSearchFilter();
        };

        _searchScopeFolder = _settings.LastSearchFolder;
        _searchSortMode = (SearchSortMode)Math.Clamp(_settings.SearchSortMode, 0, 4);
        UpdateSearchScopeText();
        UpdateSearchSortIcon();
    }

    // Whether a search scope (a picked folder) is currently set at all.
    private bool HasSearchScope => _searchScopeFolder is { Length: > 0 };

    // Kept as a method rather than assigning _searchScanning directly at its
    // several call sites: it used to drive the pulsing indicator too, and the
    // single choke point is worth keeping for whatever replaces that.
    private void SetSearchScanning(bool scanning) => _searchScanning = scanning;

    // (Re)scans the current scope folder. Used by the refresh button and the
    // lazy first scan when the view opens.
    private void RescanCurrentScope()
    {
        if (_searchScopeFolder is { Length: > 0 } folder)
        {
            StartScopeScan(new[] { folder });
        }
    }

    // Replaces the bound rows in one shot (see _searchRows) - reassigning
    // ItemsSource to a fresh list is a single collection reset rather than a
    // per-row notification storm.
    private void SetSearchRows(List<SearchRow> rows)
    {
        _searchRows = rows;
        SearchResultsList.ItemsSource = rows;
    }

    // Folder-group state uses the user-provided default icon; the four sort
    // states reuse the same rotating field/direction icons as the per-folder
    // sort override (which also swap their light variant on theme change - see
    // the ApplyColorSettings call). aliginIconDefault has no "_L" light variant
    // yet, so it's used as-is for both themes.
    private void UpdateSearchSortIcon()
    {
        if (_searchSortMode == SearchSortMode.FolderGroup)
        {
            SearchSortIcon.Source = new BitmapImage(new Uri(FileSystemService.NoSortOverrideIconUri));
            SearchSortButton.ToolTip = string.Format(Strings.SortTooltipFormat, Strings.SortModeFolderGroup);
            return;
        }

        var field = _searchSortMode is SearchSortMode.DateAsc or SearchSortMode.DateDesc
            ? FileSortField.Date
            : FileSortField.Name;
        bool descending = _searchSortMode is SearchSortMode.NameDesc or SearchSortMode.DateDesc;
        SearchSortIcon.Source = new BitmapImage(new Uri(FileSystemService.FormatSortOverrideIconUri(field, descending)));
        SearchSortButton.ToolTip = FileSystemService.FormatSortTooltip(field, descending);
    }

    // Cycles 폴더그룹 -> 이름↑ -> 이름↓ -> 날짜↑ -> 날짜↓ -> 폴더그룹. Folder-group
    // is the default (clusters same-folder results); the name/date states sort
    // globally. Remembered separately from the tree's own sort.
    private void SearchSortButton_Click(object sender, RoutedEventArgs e)
    {
        _searchSortMode = (SearchSortMode)(((int)_searchSortMode + 1) % 5);
        _settings.SearchSortMode = (int)_searchSortMode;
        _settingsService.Save(_settings);
        UpdateSearchSortIcon();
        RunSearchFilter();
    }

    private void SearchButton_Click(object sender, RoutedEventArgs e)
        => SetSearchViewActive(!_isSearchViewActive);

    // Single entry point for switching between the explorer and search views -
    // keeps the button glyph/tooltip, the overlay's visibility, and focus all
    // in sync regardless of what triggered it (button, Ctrl+F/E, or Esc).
    private void SetSearchViewActive(bool active)
    {
        if (active == _isSearchViewActive)
        {
            // Already in the requested view - a repeat Ctrl+F still nudges
            // focus back to the box if it drifted (e.g. into the results).
            if (active)
            {
                FocusSearchBox();
            }
            return;
        }

        _isSearchViewActive = active;
        SearchView.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        SearchButtonIcon.Data = active ? SearchGlyphBack : SearchGlyphMagnifier;
        SearchButton.ToolTip = active ? Strings.ToolTipExitSearch : Strings.ToolTipSearch;

        if (active)
        {
            // Fresh history recall each time the view opens.
            _searchHistoryNavIndex = -1;

            // Lazy first index: a remembered scope isn't touched at startup,
            // only when search is actually opened, and only once per session
            // unless the user hits refresh (or changes scope). Usually costs a
            // file read rather than a full walk now - see LoadCachedIndexOrScan.
            if (HasSearchScope && _searchEntries.Count == 0 && !_searchScanning)
            {
                LoadCachedIndexOrScan();
            }
            else
            {
                UpdateSearchStatus();
            }
            FocusSearchBox();
        }
        else
        {
            SearchHistoryPopup.IsOpen = false;
            ExplorerTree.Focus();
        }
    }

    private void FocusSearchBox()
    {
        // Deferred: when this runs from the same input event that revealed the
        // view, the box isn't focusable yet in this layout pass.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void SearchBrowseButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = Strings.SearchBrowseFolderDialogTitle,
            UseDescriptionForTitle = true
        };
        if (_searchScopeFolder is { Length: > 0 } && Directory.Exists(_searchScopeFolder))
        {
            dialog.SelectedPath = _searchScopeFolder;
        }

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            SetSearchScope(dialog.SelectedPath);
        }
    }

    // Points the search at a folder: persists it as the remembered scope,
    // relabels the header, and gets an index for it - from disk if one was
    // saved for this exact folder, otherwise by scanning.
    private void SetSearchScope(string folder)
    {
        _searchScopeFolder = folder;
        _settings.LastSearchFolder = _searchScopeFolder;
        _settingsService.Save(_settings);
        UpdateSearchScopeText();
        LoadCachedIndexOrScan();
    }

    // The saved index is used as-is, with no background re-scan behind it: on
    // the share this was built for, re-scanning costs minutes of network
    // traffic, and spending that on every launch - for a session where the user
    // may not search at all - is worse than working from a slightly old index
    // and saying so. Refreshing stays an explicit choice.
    private void LoadCachedIndexOrScan()
    {
        if (_searchScopeFolder is not { Length: > 0 } folder)
        {
            return;
        }

        // A cache for a folder that has since been removed (or a share that
        // isn't mounted right now) would hand back results that can't lead
        // anywhere - fall through to the scan, which reports it properly.
        if (Directory.Exists(folder) && SearchIndexCache.TryLoad(folder) is { } cached)
        {
            _searchScanCts?.Cancel();
            SetSearchScanning(false);
            _searchEntries.Clear();
            _searchEntries.AddRange(cached.Entries);
            _searchIndexSavedAtUtc = cached.SavedAtUtc;
            _searchDisplayLimit = SearchResultDisplayCap;
            RunSearchFilter();
            return;
        }

        StartScopeScan(new[] { folder });
    }

    // When the in-memory index was written to disk, or null while it came from
    // a scan this session (i.e. it's current, and the status line has no age to
    // report).
    private DateTime? _searchIndexSavedAtUtc;

    // "3일 전" and friends - deliberately coarse. The point is only to let the
    // user judge whether something they created recently could be missing.
    private static string FormatSearchIndexAge(DateTime savedAtUtc)
    {
        var age = DateTime.UtcNow - savedAtUtc;
        if (age < TimeSpan.FromMinutes(1))
        {
            return Strings.SearchAgeJustNow;
        }
        if (age < TimeSpan.FromHours(1))
        {
            return string.Format(Strings.SearchAgeMinutes, (int)age.TotalMinutes);
        }
        if (age < TimeSpan.FromDays(1))
        {
            return string.Format(Strings.SearchAgeHours, (int)age.TotalHours);
        }
        return string.Format(Strings.SearchAgeDays, (int)age.TotalDays);
    }

    // Tree folder right-click -> "이 폴더에서 검색". Scope first, then switch:
    // SetSearchViewActive's lazy first scan only fires when nothing is indexed
    // AND nothing is scanning, so starting the scan here means the switch
    // won't queue a second one for the scope we just replaced.
    private void SearchInFolder_Click(object sender, RoutedEventArgs e)
    {
        if (ExplorerTree.SelectedItem is not FileSystemItem { IsPlaceholder: false, IsDirectory: true } item)
        {
            return;
        }

        // Cleared before the scan starts, so the streaming filter doesn't
        // immediately answer the PREVIOUS query against this new folder - the
        // user came here from the tree with a new folder in mind, not to re-run
        // whatever they last typed. (The folder-picker button deliberately
        // does NOT clear: changing scope from inside the search view, query
        // still in the box, reads as "same search, different folder".)
        SearchBox.Clear();

        SetSearchScope(item.FullPath);
        SetSearchViewActive(true);
    }

    private void SearchRefreshButton_Click(object sender, RoutedEventArgs e)
    {
        if (HasSearchScope)
        {
            RescanCurrentScope();
        }
    }

    // Cancels any in-flight scan, clears the in-memory index + results, and
    // walks the folder afresh on a background thread, streaming batches back to
    // the UI thread where the current query is (re-)applied as they arrive.
    private async void StartScopeScan(IReadOnlyList<string> roots)
    {
        // Supersede any in-flight scan, but don't dispose its CTS here - the
        // background walk may still be observing that token. Each scan disposes
        // its own CTS in the finally below, once its await has unwound.
        _searchScanCts?.Cancel();

        _searchEntries.Clear();
        _searchDisplayLimit = SearchResultDisplayCap;
        SetSearchRows(new List<SearchRow>());
        // Whatever was loaded from disk is gone along with the entries above, so
        // the age must go too - otherwise a scan cancelled halfway would leave
        // the status line reporting an age for an index that no longer exists.
        _searchIndexSavedAtUtc = null;

        var existingRoots = roots.Where(Directory.Exists).ToList();
        if (existingRoots.Count == 0)
        {
            SetSearchScanning(false);
            _searchScanCts = null;
            SearchStatusText.Text = Strings.SearchStatusScopeMissing;
            return;
        }

        var cts = new CancellationTokenSource();
        _searchScanCts = cts;
        // Checked (not the source) in the closures/continuation below - a token
        // stays safe to query even after its source is disposed, so this never
        // races the finally.
        var token = cts.Token;

        SetSearchScanning(true);
        _lastSearchFilterTick = 0;
        SearchStatusText.Text = string.Format(Strings.SearchStatusScanning, 0);

        // Each scan's Progress closure captures its own token, so a stale batch
        // queued before a newer scan cancelled this one no-ops instead of
        // corrupting the newer scan's list.
        var progress = new Progress<IReadOnlyList<FileSearchService.SearchEntry>>(batch =>
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            _searchEntries.AddRange(batch);
            SearchStatusText.Text = string.Format(Strings.SearchStatusScanning, _searchEntries.Count);

            int now = Environment.TickCount;
            if (now - _lastSearchFilterTick > 150 && SearchBox.Text.Trim().Length > 0)
            {
                _lastSearchFilterTick = now;
                RunSearchFilter(updateStatusWhileScanning: false);
                // RunSearchFilter would otherwise leave a result count up; put
                // the scanning progress line back so it stays visible.
                SearchStatusText.Text = string.Format(Strings.SearchStatusScanning, _searchEntries.Count);
            }
        });

        try
        {
            await FileSearchService.ScanAsync(existingRoots, progress, token);

            if (!token.IsCancellationRequested)
            {
                SetSearchScanning(false);
                RunSearchFilter();

                // Only a completed scan is worth saving - a cancelled one holds
                // whatever fraction of the folder it got through, which would
                // then look like a complete index on the next launch. Handed to
                // a worker so serializing a large index doesn't freeze the UI;
                // the list is finished being written by now, but it's copied
                // rather than shared since the next scan clears the original.
                var snapshot = _searchEntries.ToList();
                string scopeToSave = existingRoots[0];
                _ = Task.Run(() => SearchIndexCache.Save(scopeToSave, snapshot));
            }
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer scan / scope change - leave state alone.
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (!token.IsCancellationRequested)
            {
                SetSearchScanning(false);
                SearchStatusText.Text = Strings.SearchStatusScopeMissing;
            }
        }
        finally
        {
            // Only clear the field if a newer scan hasn't already replaced it.
            if (ReferenceEquals(_searchScanCts, cts))
            {
                _searchScanCts = null;
            }
            cts.Dispose();
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SearchPlaceholder.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

        // A new/changed query always starts back at the first page of results.
        _searchDisplayLimit = SearchResultDisplayCap;

        // A manual edit ends history recall - the next Up should start over
        // from the most recent entry, not continue from wherever recall left
        // off. Programmatic recall sets _suppressHistoryReset so it's exempt.
        if (!_suppressHistoryReset)
        {
            _searchHistoryNavIndex = -1;
        }

        _searchDebounceTimer?.Stop();
        _searchDebounceTimer?.Start();
    }

    private void SearchBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                _searchDebounceTimer?.Stop();
                CommitSearchHistory(SearchBox.Text);
                _searchHistoryNavIndex = -1;
                RunSearchFilter();
                e.Handled = true;
                break;
            case Key.Escape:
                if (SearchHistoryPopup.IsOpen)
                {
                    SearchHistoryPopup.IsOpen = false;
                }
                else
                {
                    SetSearchViewActive(false);
                }
                e.Handled = true;
                break;
            case Key.Up:
                // Recall an older search into the box.
                NavigateSearchHistory(older: true);
                e.Handled = true;
                break;
            case Key.Down:
                // While recalling history, Down walks back toward the draft;
                // only from the draft state does it dive into the results list
                // (landing on the first file row, past the folder header).
                if (_searchHistoryNavIndex >= 0)
                {
                    NavigateSearchHistory(older: false);
                    e.Handled = true;
                    break;
                }
                int firstFile = -1;
                for (int i = 0; i < _searchRows.Count; i++)
                {
                    if (!_searchRows[i].IsHeader)
                    {
                        firstFile = i;
                        break;
                    }
                }
                if (firstFile >= 0)
                {
                    SearchResultsList.SelectedIndex = firstFile;
                    if (SearchResultsList.ItemContainerGenerator.ContainerFromIndex(firstFile) is ListBoxItem item)
                    {
                        item.Focus();
                    }
                    e.Handled = true;
                }
                break;
        }
    }

    // Walks _settings.SearchHistory (most-recent-first). older=true moves back
    // in time (Up); older=false moves forward (Down), and stepping past the
    // most recent restores the draft the user was typing before recall began.
    private void NavigateSearchHistory(bool older)
    {
        var history = _settings.SearchHistory;
        if (history.Count == 0)
        {
            return;
        }

        if (older)
        {
            if (_searchHistoryNavIndex == -1)
            {
                _searchHistoryDraft = SearchBox.Text;
                _searchHistoryNavIndex = 0;
            }
            else if (_searchHistoryNavIndex < history.Count - 1)
            {
                _searchHistoryNavIndex++;
            }
            else
            {
                return; // already at the oldest entry
            }
        }
        else
        {
            if (_searchHistoryNavIndex <= 0)
            {
                _searchHistoryNavIndex = -1;
                SetSearchTextFromHistory(_searchHistoryDraft);
                return;
            }
            _searchHistoryNavIndex--;
        }

        SetSearchTextFromHistory(history[_searchHistoryNavIndex]);
    }

    // Sets the box text without the TextChanged handler treating it as a manual
    // edit (which would reset history recall); the recalled query still triggers
    // a live filter via the normal debounce.
    private void SetSearchTextFromHistory(string text)
    {
        _suppressHistoryReset = true;
        SearchBox.Text = text;
        SearchBox.CaretIndex = text.Length;
        _suppressHistoryReset = false;
    }

    // Applies the current query to the in-memory index, capping how many rows
    // materialize so a query matching thousands of files still renders
    // instantly (the status line notes when the display was capped).
    private void RunSearchFilter(bool updateStatusWhileScanning = true)
    {
        string query = SearchBox.Text;
        if (query.Trim().Length == 0)
        {
            SetSearchRows(new List<SearchRow>());
            if (!_searchScanning || updateStatusWhileScanning)
            {
                UpdateSearchStatus();
            }
            return;
        }

        // Refuse a match-everything query (e.g. "*", "*.*") - it just sorts and
        // counts the entire index for no useful result. Requiring at least one
        // letter or digit keeps "*.txt" / "report" working while blocking the
        // pure-wildcard case.
        if (!query.Any(char.IsLetterOrDigit))
        {
            SetSearchRows(new List<SearchRow>());
            if (!_searchScanning || updateStatusWhileScanning)
            {
                SearchStatusText.Text = Strings.SearchStatusTooBroad;
            }
            return;
        }

        var matcher = FileSearchService.BuildMatcher(query);
        var matches = new List<FileSearchService.SearchEntry>();
        foreach (var entry in _searchEntries)
        {
            if (matcher(entry.FileName))
            {
                matches.Add(entry);
            }
        }

        // For a plain substring query, the matched run is highlighted in the
        // filename (see SearchHighlightBehavior). A wildcard query has no single
        // literal substring to point at, so it's left unhighlighted.
        string trimmedQuery = query.Trim();
        bool highlightable = trimmedQuery.Length > 0
            && !trimmedQuery.Contains('*') && !trimmedQuery.Contains('?');

        // Order per the current mode. FolderGroup clusters by folder then name
        // (so a folder's matches are contiguous -> one header each); the
        // name/date modes sort globally (mostly breaking groups apart, with any
        // still-adjacent same-folder run re-collapsing under one header below).
        IEnumerable<FileSearchService.SearchEntry> ordered = _searchSortMode switch
        {
            SearchSortMode.NameAsc => matches.OrderBy(x => (string?)x.FileName, FileSystemService.NaturalNameComparer),
            SearchSortMode.NameDesc => matches.OrderByDescending(x => (string?)x.FileName, FileSystemService.NaturalNameComparer),
            SearchSortMode.DateAsc => matches.OrderBy(x => x.LastWriteTime),
            SearchSortMode.DateDesc => matches.OrderByDescending(x => x.LastWriteTime),
            _ => matches
                .OrderBy(x => (string?)x.DirectoryPath, FileSystemService.NaturalNameComparer)
                .ThenBy(x => (string?)x.FileName, FileSystemService.NaturalNameComparer),
        };

        // Collapse consecutive runs of the same folder into one header + its
        // files; the cap counts FILE rows only, so headers never eat into it.
        var rows = new List<SearchRow>();
        int total = matches.Count;
        int shownFiles = 0;
        string? currentFolder = null;
        foreach (var entry in ordered)
        {
            if (shownFiles >= _searchDisplayLimit)
            {
                break;
            }
            if (!string.Equals(entry.DirectoryPath, currentFolder, StringComparison.OrdinalIgnoreCase))
            {
                currentFolder = entry.DirectoryPath;
                rows.Add(SearchRow.Header(entry.DirectoryPath));
            }

            int matchStart = highlightable
                ? entry.FileName.IndexOf(trimmedQuery, StringComparison.OrdinalIgnoreCase)
                : -1;
            rows.Add(SearchRow.File(entry, matchStart, matchStart >= 0 ? trimmedQuery.Length : 0));
            shownFiles++;
        }

        // A clickable "더 보기" row when more matched than the current page shows
        // (SearchResultsList_PreviewMouseLeftButtonUp raises _searchDisplayLimit).
        if (total > shownFiles)
        {
            rows.Add(SearchRow.ShowMore(string.Format(Strings.ShowMoreFormat, total - shownFiles)));
        }

        SetSearchRows(rows);

        if (_searchScanning && !updateStatusWhileScanning)
        {
            return;
        }

        if (total == 0)
        {
            SearchStatusText.Text = Strings.SearchStatusNoResults;
        }
        else if (total > shownFiles)
        {
            SearchStatusText.Text = string.Format(Strings.SearchStatusResultsCapped, shownFiles, total);
        }
        else
        {
            SearchStatusText.Text = string.Format(Strings.SearchStatusResults, total);
        }
    }

    private void UpdateSearchStatus()
    {
        if (_searchScanning)
        {
            SearchStatusText.Text = string.Format(Strings.SearchStatusScanning, _searchEntries.Count);
        }
        else if (!HasSearchScope)
        {
            SearchStatusText.Text = Strings.SearchScopeNone;
        }
        else if (_searchIndexSavedAtUtc is { } savedAt)
        {
            // Takes the idle line's place rather than sitting next to it: this
            // one carries the same "now type something" moment AND the age,
            // which the narrow panel has no room to show both of.
            SearchStatusText.Text = string.Format(
                Strings.SearchStatusCached, FormatSearchIndexAge(savedAt));
        }
        else
        {
            SearchStatusText.Text = Strings.SearchStatusEmpty;
        }
    }

    private void UpdateSearchScopeText()
    {
        SearchScopeText.Text = _searchScopeFolder is { Length: > 0 }
            ? _searchScopeFolder
            : Strings.SearchScopeNone;
    }

    private void SearchHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (SearchHistoryPopup.IsOpen)
        {
            SearchHistoryPopup.IsOpen = false;
            return;
        }
        if (_settings.SearchHistory.Count == 0)
        {
            return;
        }
        SearchHistoryList.ItemsSource = _settings.SearchHistory.ToList();
        SearchHistoryPopup.IsOpen = true;
    }

    private void SearchHistoryList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // A click on the per-entry delete button is its own action - don't also
        // treat it as "pick this history entry" and fill the box.
        if ((e.OriginalSource as DependencyObject)?.FindAncestor<Button>() is not null)
        {
            return;
        }

        if (ItemsControl.ContainerFromElement(SearchHistoryList, (DependencyObject)e.OriginalSource) is ListBoxItem { Content: string query })
        {
            SearchHistoryPopup.IsOpen = false;
            SearchBox.Text = query;
            SearchBox.CaretIndex = query.Length;
            _searchDebounceTimer?.Stop();
            RunSearchFilter();
            FocusSearchBox();
        }
    }

    private void SearchHistoryDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: string query })
        {
            return;
        }

        _settings.SearchHistory.RemoveAll(h => string.Equals(h, query, StringComparison.OrdinalIgnoreCase));
        _settingsService.Save(_settings);

        if (_settings.SearchHistory.Count == 0)
        {
            SearchHistoryPopup.IsOpen = false;
            return;
        }

        // Rebind the (now shorter) list so the dropdown updates in place.
        SearchHistoryList.ItemsSource = _settings.SearchHistory.ToList();
    }

    // Records a file result as a drag-out candidate on press (file rows only -
    // not headers or the "더 보기" row). The actual drag starts in
    // SearchResultsList_PreviewMouseMove once the pointer passes the threshold.
    private void SearchResultsList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _searchDragStart = null;
        _searchDragCandidate = null;
        if (ItemsControl.ContainerFromElement(SearchResultsList, (DependencyObject)e.OriginalSource) is ListBoxItem { Content: SearchRow { Entry: { } entry } })
        {
            _searchDragStart = e.GetPosition(SearchResultsList);
            _searchDragCandidate = entry;
        }
    }

    private void SearchResultsList_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_searchDragCandidate is not { } entry || _searchDragStart is not { } start ||
            e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(SearchResultsList);
        bool pastThreshold =
            Math.Abs(current.X - start.X) >= SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(current.Y - start.Y) >= SystemParameters.MinimumVerticalDragDistance;
        if (!pastThreshold)
        {
            return;
        }

        _searchDragStart = null;
        _searchDragCandidate = null;

        // FileDrop + Copy-only, exactly like the tree's own drag-out (see
        // TreeViewItem_PreviewMouseMove): any app that accepts an Explorer file
        // drop accepts this, and Copy (never Move) means dragging a result into
        // Explorer/another app can never remove the original file.
        var data = new DataObject(DataFormats.FileDrop, new[] { entry.FullPath });

        // Already sourced from the stable list rather than a row container; the
        // finally matches the tree's for the same reason - see the note there
        // on a mouse capture outliving the drag.
        try
        {
            DragDrop.DoDragDrop(SearchResultsList, data, DragDropEffects.Copy);
        }
        finally
        {
            if (Mouse.Captured is not null)
            {
                Mouse.Capture(null);
            }
        }
    }

    private void SearchResultsList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (ItemsControl.ContainerFromElement(SearchResultsList, (DependencyObject)e.OriginalSource) is not ListBoxItem { Content: SearchRow row })
        {
            return;
        }

        if (row.IsShowMore)
        {
            _searchDisplayLimit += SearchResultDisplayCap;
            RunSearchFilter();
        }
        else if (row.Entry is { } entry)
        {
            ActivateSearchResult(entry);
        }
    }

    // Enter deliberately activates (jump to the file in the tree), NOT "열기"
    // - which is why the menu's 열기 shows no gesture while 복사/삭제/경로
    // 복사 do: those now mirror the tree's shortcuts exactly, added together
    // with their menu labels (the label is what teaches the key, so a label
    // must never name a key that doesn't work here).
    private void SearchResultsList_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (SearchResultsList.SelectedItem is not SearchRow { Entry: not null })
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Enter:
                ActivateSearchResult(((SearchRow)SearchResultsList.SelectedItem).Entry!);
                e.Handled = true;
                break;
            case Key.Delete:
                SearchDelete_Click(sender, e);
                e.Handled = true;
                break;
            case Key.C when Keyboard.Modifiers == ModifierKeys.Control:
                SearchCopy_Click(sender, e);
                e.Handled = true;
                break;
            case Key.C when Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift):
                SearchCopyPath_Click(sender, e);
                e.Handled = true;
                break;
        }
    }

    // Return to the explorer view and reveal the picked file there, reusing the
    // exact same reveal-and-select machinery favorites navigation uses (which
    // already handles files past a folder's "더 보기" cap). pinParentToTop lands
    // the file inside its folder's context (folder at the top, file selected
    // below) rather than gluing the file itself to the very top.
    private void ActivateSearchResult(FileSearchService.SearchEntry entry)
    {
        // A result can now outlive the file it names: the index may have been
        // loaded from disk (see SearchIndexCache) and the file deleted or moved
        // since that scan. Without this check the navigation just walks to a
        // path that isn't there and quietly gives up, dropping the user into
        // the tree somewhere with no explanation. Say it instead, and drop the
        // row so the list corrects itself as stale hits are found.
        if (!File.Exists(entry.FullPath))
        {
            _searchEntries.Remove(entry);
            RunSearchFilter();
            // After the filter, which would otherwise overwrite this with a
            // plain result count.
            SearchStatusText.Text = Strings.SearchResultMissing;
            return;
        }

        // Opening a result is a strong "this query was useful" signal, so
        // remember it - live typing alone never commits history (that would
        // spam it with every prefix), only an explicit Enter or this do.
        CommitSearchHistory(SearchBox.Text);

        // Deferred, and this is not cosmetic. The mouse path into here is
        // SearchResultsList_PreviewMouseLeftButtonUp - a PREVIEW handler, so it
        // runs before the ListBox handles that same release itself. A ListBox
        // takes the mouse capture on button-down (that is how it tracks
        // dragging across items), and gives it back when it processes the up.
        // Collapsing the search view inline here tears the list out of the
        // visual tree in between: the capture is taken, and the code that would
        // return it never gets a live element to return it from.
        //
        // A capture stranded that way makes Windows keep routing every mouse
        // message to this app - the sidebar still tracks the cursor while other
        // windows stop responding to clicks. Confirmed in the wild: the
        // watchdog's log recorded exactly this, "WPF(ListBox)", 23 minutes into
        // a session. Letting the event finish first means the ListBox releases
        // its own capture normally, before anything is hidden.
        Dispatcher.BeginInvoke(() =>
        {
            SetSearchViewActive(false);
            NavigateToPath(entry.FullPath, pinParentToTop: true);
        }, System.Windows.Threading.DispatcherPriority.Input);
    }

    private void CommitSearchHistory(string query)
    {
        query = query.Trim();
        if (query.Length == 0)
        {
            return;
        }

        // Move-to-front, de-duplicated case-insensitively, capped.
        _settings.SearchHistory.RemoveAll(h => string.Equals(h, query, StringComparison.OrdinalIgnoreCase));
        _settings.SearchHistory.Insert(0, query);
        while (_settings.SearchHistory.Count > SearchHistoryMax)
        {
            _settings.SearchHistory.RemoveAt(_settings.SearchHistory.Count - 1);
        }
        _settingsService.Save(_settings);
    }

    // ---- Search result context menu ----------------------------------
    // Every action targets the right-clicked result's path directly, so no
    // FileSystemItem/tree node is involved.

    private FileSearchService.SearchEntry? SelectedSearchResult
        => (SearchResultsList.SelectedItem as SearchRow)?.Entry;

    // Right-click selects the row under the cursor (WPF doesn't do this for
    // right-click on its own) so the menu's handlers act on the intended item.
    private void SearchResultsList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ItemsControl.ContainerFromElement(SearchResultsList, (DependencyObject)e.OriginalSource) is ListBoxItem item)
        {
            PrepareSearchRowContextMenu(item);
        }
    }

    // Split out for the same reason as PrepareTreeRowContextMenu: the
    // menu-covered-row pass-through reopens the menu itself and needs the
    // identical pre-open setup.
    private void PrepareSearchRowContextMenu(ListBoxItem item)
    {
        item.IsSelected = true;

        // Same pre-open timing rule as the tree's thumbnail (see
        // TreeViewItem_PreviewMouseRightButtonDown): the slot must be in
        // the menu's very first measure or the open menu visibly grows.
        // Header/"더 보기" rows carry no Entry, so they pass null = no slot.
        if (item.ContextMenu is { Items: [MenuItem thumbnailItem, Separator thumbnailSeparator, ..] })
        {
            UpdateThumbnailRow(thumbnailItem, thumbnailSeparator,
                (item.DataContext as SearchRow)?.Entry?.FullPath);
        }
    }

    private void SearchResultContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        AnyMenu_Opened(sender, e);

        // Menu items in a resource-dictionary ContextMenu have no code-behind
        // field, so items are found by position - same approach as
        // ExplorerItemContextMenu_Opened. "Code로 열기" sits at index 10 now
        // that the groups mirror the tree menu's (open / edit / path-and-tools
        // / properties).
        if (sender is ContextMenu { Items: [MenuItem thumbnailItem, Separator thumbnailSeparator, _, _, _, _, _, _, _, _, MenuItem openWithCodeItem, ..] })
        {
            openWithCodeItem.IsEnabled = ShellFileService.IsCodeRegistered();

            // Guard for opens that bypassed the right-click handler (keyboard
            // menu key): hide a slot left over from an earlier open rather
            // than show the wrong file's picture - same rule as the tree's.
            if (thumbnailItem.Visibility == Visibility.Visible &&
                _pendingThumbnailPath != SelectedSearchResult?.FullPath)
            {
                thumbnailItem.Visibility = Visibility.Collapsed;
                thumbnailSeparator.Visibility = Visibility.Collapsed;
                _pendingThumbnailPath = null;
            }
        }
    }

    private void SearchOpen_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSearchResult is { } entry)
        {
            ShellFileService.OpenWithDefaultApp(entry.FullPath);
        }
    }

    private void SearchOpenWith_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSearchResult is { } entry)
        {
            ShellFileService.OpenWithPicker(entry.FullPath);
        }
    }

    private void SearchOpenWithCode_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSearchResult is { } entry)
        {
            ShellFileService.OpenWithCode(entry.FullPath);
        }
    }

    private void SearchCopy_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSearchResult is { } entry)
        {
            FileOperationService.CopyToClipboard(entry.FullPath);
        }
    }

    private void SearchCopyPath_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSearchResult is { } entry)
        {
            FileOperationService.CopyPathToClipboard(entry.FullPath);
        }
    }

    private void SearchOpenTerminal_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSearchResult is { } entry && entry.DirectoryPath.Length > 0)
        {
            ShellFileService.OpenTerminal(entry.DirectoryPath);
        }
    }

    private void SearchReveal_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSearchResult is { } entry)
        {
            ShellFileService.RevealInExplorer(entry.FullPath);
        }
    }

    private void SearchProperties_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSearchResult is { } entry)
        {
            ShellFileService.ShowProperties(entry.FullPath);
        }
    }

    private void SearchDelete_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSearchResult is not { } entry)
        {
            return;
        }

        var result = MessageBox.Show(
            this,
            string.Format(Strings.DeleteConfirmBody, entry.FileName),
            Strings.DeleteConfirmTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        if (!FileOperationService.TryDeleteToRecycleBin(entry.FullPath, out var error))
        {
            if (error is not null)
            {
                MessageBox.Show(this, error, Strings.DeleteFailedTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            return;
        }

        // Drop it from the in-memory index and re-run the filter so the shown
        // results and the count both reflect the deletion. The tree, if the
        // parent folder is expanded there, refreshes itself via its own
        // FileSystemWatcher.
        _searchEntries.RemoveAll(x => string.Equals(x.FullPath, entry.FullPath, StringComparison.OrdinalIgnoreCase));
        RunSearchFilter();
    }
}

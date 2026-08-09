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
using SidebarExplorer.App.Behaviors;
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
    // 180 → 204 when the viewer toggle became the seventh header button - the
    // floor exists so the button row still fits (see ApplyHeaderMetrics).
    private const double MinExpandedWidth = 204;
    // 1200 → 2000 (user, 2026-08-08): with the viewer panel in the window
    // the old cap - sized for a tree-only sidebar - was suddenly the tight
    // constraint on the OUTER edge drag. This still caps only the TREE's
    // share; the viewer's own cap is MaxViewerWidth in the viewer region.
    private const double MaxExpandedWidth = 2000;
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

    // Device-independent pixels left clear of an auto-hidden taskbar's edge so
    // the cursor can still reach the screen edge and summon it. Only needs to
    // be big enough that the last row of pixels isn't ours - see
    // LeaveTaskbarRevealStrip.
    private const double TaskbarRevealStrip = 2;

    // Deliberately NOT instant, unlike the plain mouse hover reveal. During a
    // drag the cursor crosses the whole screen, and the sliver sits on the very
    // edge of it, so brushing past on the way to another window is routine -
    // hovering with an empty hand is a much better signal of intent than
    // hovering with a file. Same 400ms as the re-hide, for one thing to tune.
    private const int AutoHideDragRevealDelayMs = 400;

    private readonly SettingsService _settingsService = new();
    private AppSettings _settings = new();
    // Observable so a drive can leave and come back without the tree being
    // rebound - hiding a drive root removes it here and the view follows. It
    // was a plain List while the set of roots only ever changed at startup.
    private readonly System.Collections.ObjectModel.ObservableCollection<FileSystemItem> _roots = new();
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

    // Counting down while a drag is resting on the auto-hidden sliver, and a
    // note of whether it was that drag - rather than the cursor - that opened
    // the window, since a drag leaving again has to close what it opened
    // (no MouseLeave arrives during a drag to do it).
    private System.Windows.Threading.DispatcherTimer? _dragRevealTimer;
    private System.Windows.Threading.DispatcherTimer? _hoverRevealTimer;
    private bool _slideInFlight;
    private Action? _slideCompletion;
    private System.Windows.Threading.DispatcherTimer? _collapsedWatchTimer;
    private bool _collapsedZoneArmed;
    private int _slideToken;
    private bool _revealedByDrag;

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

            // Was a Topmost false/true cycle, the only way to make WPF write
            // the property through at all; ApplyTopmostState states it to the
            // window manager outright and reports a mismatch if there was one.
            ApplyTopmostState("resume");
        }));
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.MakeToolWindow(hwnd);

        // NOTE: no z-order call here on purpose. Settings are not loaded yet at
        // this point (MainWindow_Loaded does that), so anything decided here
        // would be decided against defaults; MainWindow_Loaded settles it
        // instead, after the style rewrite above and with the real state.

        // A second Edgetree launch (any build/version) broadcasts this
        // instead of opening its own window - see App.OnStartup - so this
        // instance can come to the foreground the same way the tray icon's
        // "Open" does, regardless of whether it's currently docked or
        // hidden to the tray.
        HwndSource.FromHwnd(hwnd).AddHook(SingleInstanceWndProc);

        // Also the only way to hear that our own 잘라내기 has been consumed or
        // replaced - by Explorer's paste, or by a copy in any other app.
        NativeMethods.AddClipboardFormatListener(hwnd);
    }

    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_WINDOWPOSCHANGED = 0x0047;

    private IntPtr SingleInstanceWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == unchecked((int)NativeMethods.ActivateMessage))
        {
            (Application.Current as App)?.RestoreMainWindow();
            handled = true;
        }
        else if (msg == WM_WINDOWPOSCHANGED && (_inTopBandDrag || _inBottomBandDrag))
        {
            // Instrument only (compiles away in Release): any real geometry
            // change the window manager performed while a band drag ran. The
            // design gives the gestures no reason to move the window at all,
            // so a line here that isn't the clip region's own
            // FRAMECHANGED|NOSIZE|NOMOVE echo is a bug with a name on it.
            LogWindowPosChanged(lParam);
        }
        else if (msg == WM_LBUTTONDOWN || msg == WM_LBUTTONUP)
        {
            // The lowest point the app can watch from: a click that reaches
            // this window shows up here even when WPF then routes it somewhere
            // we don't expect. A swallowed click with a line here was lost
            // INSIDE the app; a swallowed click with no line never reached the
            // window at all (a popup's own hwnd took it).
            LogClick(msg == WM_LBUTTONDOWN ? "win press" : "win up", null);
        }
        else if (msg == NativeMethods.WM_CLIPBOARDUPDATE)
        {
            // One hop late: this arrives while the process that wrote the
            // clipboard may still hold it open, and reading it inline would
            // just fail.
            Dispatcher.BeginInvoke(new Action(DropCutMarksIfClipboardMovedOn),
                System.Windows.Threading.DispatcherPriority.Background);
        }
        return IntPtr.Zero;
    }

    // The markers mean one thing: "this is on the clipboard, waiting to move".
    // Checking that claim directly - rather than trying to enumerate every way
    // it could stop being true - is what covers the paths nobody thought of,
    // Explorer's own paste among them.
    private void DropCutMarksIfClipboardMovedOn()
    {
        if (FileSystemService.CutPaths.Count == 0)
        {
            return;
        }

        bool? stillOurs = FileOperationService.ClipboardStillHoldsCut(FileSystemService.CutPaths);
        LogCutClipboardCheck(stillOurs);
        if (stillOurs == false)
        {
            ClearCutMarks("clipboard");
        }
    }

    // Which of the three answers came back: true = still ours (leave it),
    // false = someone else's clipboard now (clear), null = couldn't read it,
    // which is the case that would leave a marker standing with nothing behind
    // it. Written only in debug builds, same as the rest of cut.log.
    [System.Diagnostics.Conditional("DEBUG")]
    private static void LogCutClipboardCheck(bool? stillOurs)
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Edgetree");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, "cut.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  clipboard check: " +
                $"{(stillOurs is null ? "unreadable" : stillOurs.Value ? "still ours" : "moved on")}" +
                $"{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private void MainWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        _settings = _settingsService.Load();
        FileSystemService.SortField = ReadSortField(_settings.SortField, _settings.SortByDate);
        FileSystemService.SortDescending = _settings.SortDescending;
        FileSystemItem.DisplayCap = Math.Clamp(_settings.MaxItemsPerFolder, 1, 50);

        // Alongside the other listing rules, and for the same reason: a saved
        // filter has to be in force for the FIRST folder read, not from the
        // first time it is changed.
        foreach (string category in _settings.FileFilterCategories)
        {
            FileTypeFilter.SelectedCategories.Add(category);
        }

        // Before the categories are honoured, not after: 사용자 지정 selected
        // with nothing behind it claims no file at all, and if it were the only
        // kind picked the first read would come back empty.
        FileTypeFilter.SetCustomExtensions(_settings.FileFilterCustomExtensions);
        DropCustomFilterIfEmpty();

        // Must be set before the tree/favorites below ever read an icon, same
        // as the sort/display statics above.
        ShellIconService.UseShellIcons = _settings.UseShellIcons;
        Resources["FavoriteFolderIconSource"] = ShellIconService.GetFavoritesFolderIcon();

        FileSystemService.BookmarkedPaths.Clear();
        foreach (var path in _settings.BookmarkPaths)
        {
            FileSystemService.BookmarkedPaths.Add(path);
        }

        FileSystemService.HiddenPaths.Clear();
        foreach (var path in _settings.HiddenFolderPaths)
        {
            FileSystemService.HiddenPaths.Add(FileSystemService.NormalizeHiddenPath(path));
        }

        FileSystemService.SortOverrides.Clear();
        foreach (var entry in _settings.FolderSortOverrides)
        {
            FileSystemService.SortOverrides[FileSystemService.NormalizeSortOverridePath(entry.Path)] =
                new FolderSortOverride(ReadSortField(entry.SortField, entry.SortByDate), entry.SortDescending);
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

        // A stored tree width below the window floor is legal exactly when
        // the viewer is about to reopen on top of it (the split floor is the
        // smaller one) - clamping it to the window floor here would move the
        // tree share the user set with the middle divider.
        Width = _settings.IsAutoHidden ? CollapsedWidth
            : _settings.ViewerOpen ? Math.Clamp(_settings.ExpandedWidth, MinTreeSplitWidth, MaxExpandedWidth)
            : ClampExpandedWidth(_settings.ExpandedWidth);
        ApplyHeaderMetrics();
        SetExpandedContentVisibility(_settings.IsAutoHidden ? Visibility.Collapsed : Visibility.Visible);
        PositionToWorkArea();
        UpdateResizeThumbVisibility();

        // The viewer panel survives restarts. On top of the tree-only Width
        // set above, through the same one path every open takes - and never
        // over an auto-hidden start (OpenViewer declines those itself).
        if (_settings.ViewerOpen)
        {
            OpenViewer();
        }

        ExplorerTree.FontSize = TreeFontSizeSteps.Contains(_settings.TreeFontSize)
            ? _settings.TreeFontSize
            : DefaultTreeFontSize;

        ReloadRoots();
        ExplorerTree.ItemsSource = _roots;
        StartDriveWatchers();

        // Deferred one pass: the tree's own ScrollViewer only exists once the
        // template has been applied and laid out, and this must be attached
        // before the user can reach the tree - the reported jump happens during
        // ordinary browsing, not at startup. Debug-only, see AttachScrollJumpWatch.
        // Called through a lambda, not as a method group: a [Conditional] method
        // cannot be turned into a delegate (CS1618). This way the CALL vanishes
        // in Release and the lambda is simply empty.
        Dispatcher.BeginInvoke(new Action(() => AttachScrollJumpWatch()),
            System.Windows.Threading.DispatcherPriority.Loaded);

        // Row sizing for the current favorite count/collapsed state was
        // already handled above by SetExpandedContentVisibility.
        FavoritesList.ItemsSource = _settings.Favorites;
        BookmarkPanelList.ItemsSource = _bookmarkPanelRows;

        // After both sources are attached: it decides which of the two is on
        // screen, and builds the bookmark rows when that is the bookmark list.
        ApplySidePanelMode();
        ApplyTreeFontWeight();

        // Builds the footer's filter chips and marks the saved state on them.
        BuildFooterFilterChips();

        InitializeSearch();

        // Settings only exist from this handler on (they are read a few lines
        // up), which is why the window's z-order is settled HERE and not in
        // MainWindow_SourceInitialized where the extended style is rewritten -
        // that runs first, against defaults, and would decide "not on top" for
        // an app whose saved state says otherwise.
        ApplyTopmostState("startup");

        // ...and once more after the window has actually been shown: measured
        // 2026-07-25, the correction above reports success and the window is
        // still not topmost seconds later, so something in WPF's own show path
        // undoes it. Cheap, and it lands before the user can reach the sliver.
        Dispatcher.BeginInvoke(new Action(() => ApplyTopmostState("startup-shown")),
            System.Windows.Threading.DispatcherPriority.ApplicationIdle);

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

        // Deferred for the same reason as the tree restore above - and its own
        // disk work happens on a thread pool anyway (see the method).
        Dispatcher.BeginInvoke(PruneMissingBookmarks, System.Windows.Threading.DispatcherPriority.Background);

        StartStuckCaptureWatchdog();
        StartNetworkRootStatusWatch();

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
                "https://api.github.com/repos/legendsteel11/Edgetree/releases/latest");

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
            UpdateAvailableVersion = latest;
            UpdateAvailableDot.Visibility = Visibility.Visible;
            OptionsButton.ToolTip =
                $"{Strings.ToolTipOptions} — {string.Format(Strings.ToolTipUpdateAvailable, "v" + latest)}";

            // The same news on the tray icon, which is the only part of the app
            // still on screen once the sidebar is hidden or sent to the tray -
            // the dot above is behind whichever of those is in effect.
            (Application.Current as App)?.ShowUpdateAvailable(latest);
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

    // The newer release CheckForUpdateOnceAsync found, if any - read by the
    // About window (both the menu path here and App's tray path) to show its
    // download link next to the version it supersedes.
    public Version? UpdateAvailableVersion { get; private set; }

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
            NavigateToPath(lastSelectedPath, source: "restore");
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
    // The theme this last ran under. Everything gated on it below depends on
    // the theme ALONE, never on which colours are in it - and the colour
    // picker now calls this on every frame of a drag, where the theme cannot
    // have changed. Doing that work anyway meant walking the whole tree and
    // rebuilding the chrome sixty times a second to arrive at what was
    // already on screen, which is what the picker's stutter turned out to be
    // (2026-08-04).
    private bool? _lastAppliedTheme;

    public void ApplyColorSettings()
    {
        bool light = _settings.IsLightMode;
        bool themeChanged = _lastAppliedTheme != light;
        _lastAppliedTheme = light;

        if (themeChanged)
        {
            // Not for the icon any more - that became a path taking the row's
            // own brush, so a theme flip recolours it for free. The walk stays
            // for the TOOLTIP, which is a cached string built from the folder's
            // state.
            foreach (var root in _roots)
            {
                RefreshSortOverrideIconForTheme(root);
            }

            // Menus/context menus/the Color Settings and About dialogs/header
            // icons - general chrome, not part of the 16 colors below.
            (Application.Current as App)?.ApplyChromeTheme(light);

            // The DWM under-sheet that flashes on dock/undock and native
            // resizes follows the theme DECLARED ON THE WINDOW, not the
            // app's brushes - see SetImmersiveDarkMode. First run lands here
            // via MainWindow_Loaded, when the handle already exists.
            NativeMethods.SetImmersiveDarkMode(
                new WindowInteropHelper(this).Handle, dark: !light);

            // Menu/context-menu drop shadow (see MenuDropShadow's own comment
            // in the XAML) - the same dark, fairly strong shadow read as too
            // heavy against a light-mode menu's white background, so light mode
            // gets a softer one (lower opacity) instead of just reusing the
            // dark value.
            Resources["MenuDropShadow"] = new DropShadowEffect
            {
                Color = Colors.Black,
                Direction = 270,
                ShadowDepth = 2,
                BlurRadius = 8,
                Opacity = light ? 0.15 : 0.4
            };
        }

        SetBrushColor("SidebarBackground", light ? _settings.LightBackgroundColorHex : _settings.BackgroundColorHex);
        SetBrushColor("FolderNameForeground", light ? _settings.LightFolderNameColorHex : _settings.FolderNameColorHex);
        SetBrushColor("FolderNameHighlightForeground", light ? _settings.LightFolderNameHighlightColorHex : _settings.FolderNameHighlightColorHex);
        // Quieted variants of the two name colours, for the row states that mean
        // "this row is not in its ordinary condition": a hidden folder being
        // shown anyway, and anything waiting on a Ctrl+X paste. The user's own
        // colour taken a step towards the background - darker in the dark theme,
        // lighter in the light one - rather than a fixed grey, and derived here
        // for the same reason the inactive selection colour is: repick a colour
        // and these follow, and they can never clash with a palette they were
        // not told about. Deliberately NOT the offline drive's grey, which has
        // to keep meaning "this drive is not answering" by itself.
        if (ColorConverter.ConvertFromString(
                light ? _settings.LightFolderNameColorHex : _settings.FolderNameColorHex) is Color folderColor)
        {
            SetBrushColor("MutedFolderNameForeground",
                MoveTowardsBackground(folderColor, light, MutedNameBlend));
        }
        if (ColorConverter.ConvertFromString(
                light ? _settings.LightFileNameColorHex : _settings.FileNameColorHex) is Color fileColor)
        {
            SetBrushColor("MutedFileNameForeground",
                MoveTowardsBackground(fileColor, light, MutedNameBlend));
        }
        SetBrushColor("FileNameForeground", light ? _settings.LightFileNameColorHex : _settings.FileNameColorHex);
        SetBrushColor("FileNameHighlightForeground", light ? _settings.LightFileNameHighlightColorHex : _settings.FileNameHighlightColorHex);
        // The selection highlight keeps TWO variants behind its one resource
        // key: the user-picked color while this app is in the foreground, and
        // a 40%-opacity version of that SAME color while it isn't - what was
        // left selected still reads as "the row you were on", not as a live
        // selection (VS Code's unfocused-selection treatment; selection is
        // deliberately KEPT on app switch - Ctrl+V into the selected folder
        // after copying something elsewhere depends on it). Derived here, not
        // a 16th palette entry: repick the selection color in either theme
        // and the dimmed variant follows automatically. Swapping the single
        // resource (UpdateSelectionBrushForActivation) covers every consumer
        // - tree rows, multi-select rows, favorites, search results - with
        // zero per-row bindings.
        string selectionHex = light ? _settings.LightSelectionColorHex : _settings.SelectionColorHex;
        if (ColorConverter.ConvertFromString(selectionHex) is Color selectionColor)
        {
            var inactiveColor = selectionColor;
            inactiveColor.A = (byte)Math.Round(selectionColor.A * 0.4);

            // Recoloured rather than rebuilt, for the reason SetBrushColor
            // gives: these two are handed to the dictionary below, and a new
            // pair every frame is a new dictionary entry every frame.
            //
            // The IsFrozen test is not a formality - it was left out and the
            // app died on the first drag (2026-08-04): once a brush has been
            // through the dictionary WPF may have frozen it, and a frozen
            // Freezable throws on assignment rather than refusing quietly.
            if (_selectionActiveBrush is { IsFrozen: false } activeBrush &&
                _selectionInactiveBrush is { IsFrozen: false } inactiveBrush)
            {
                activeBrush.Color = selectionColor;
                inactiveBrush.Color = inactiveColor;
            }
            else
            {
                _selectionActiveBrush = new SolidColorBrush(selectionColor);
                _selectionInactiveBrush = new SolidColorBrush(inactiveColor);
            }
        }
        UpdateSelectionBrushForActivation();
        SetBrushColor("FavoritesBackground", light ? _settings.LightHistoryBackgroundColorHex : _settings.HistoryBackgroundColorHex);
        SetBrushColor("TreeRowHoverBackground", light ? _settings.LightHoverBackgroundColorHex : _settings.HoverBackgroundColorHex);

        // The footer's lit file-kind chip. Fixed per theme rather than taken
        // from the tree's selection colour, which is the user's to set and is
        // often a strong blue - that put a shout in a strip meant to be read at
        // a glance (2026-08-02).
        //
        // The two themes do NOT get the same treatment, and that is the point.
        // On dark, grey with white on it is enough to lift the chip off the
        // seven quiet ones. On light, grey had nothing to push against - the
        // whole strip is already pale - so it takes a blue instead.
        //
        // That blue is the bookmark ribbon's own #4A90E2, arrived at by trying
        // a deeper one first: a saturated navy filled the chip with more weight
        // than a footer wants ("배경이 좀 쎄다"), while the lighter blue reads
        // as a mark rather than a block. Keeping it the SAME blue the app
        // already draws with is the part worth holding on to - one accent, used
        // in both places, rather than a second one invented for this strip.
        SetBrushColor("FooterChipCheckedBackground", light ? "#4A90E2" : "#5A5A5A");
        SetBrushColor("FooterChipCheckedForeground", "#FFFFFF");
        SetBrushColor("FolderNameHoverForeground", light ? _settings.LightFolderNameHoverColorHex : _settings.FolderNameHoverColorHex);
        SetBrushColor("FileNameHoverForeground", light ? _settings.LightFileNameHoverColorHex : _settings.FileNameHoverColorHex);
        SetBrushColor("ShowMoreForeground", light ? _settings.LightShowMoreColorHex : _settings.ShowMoreColorHex);
        SetBrushColor("TreeGuideLineBrush", light ? _settings.LightGuideLineColorHex : _settings.GuideLineColorHex);
        SetBrushColor("TreeGuideLineActiveBrush", light ? _settings.LightGuideLineActiveColorHex : _settings.GuideLineActiveColorHex);
        SetBrushColor("PanelDividerBrush", light ? _settings.LightPanelDividerColorHex : _settings.PanelDividerColorHex);
        SetBrushColor("ViewerBackground", light ? _settings.LightViewerBackgroundColorHex : _settings.ViewerBackgroundColorHex);
        SetBrushColor("HeaderBackground", light ? _settings.LightHeaderBackgroundColorHex : _settings.HeaderBackgroundColorHex);
        SetBrushColor("AutoHideHandleBackground", light ? _settings.LightAutoHideHandleColorHex : _settings.AutoHideHandleColorHex);
        UpdateDerivedEdgeInks(light);

        // The results sort button's icon has its own light/dark variants (same
        // as the folder override icon) - re-resolve it now that IsLightMode
        // above reflects the current theme. ApplyColorSettings only ever runs
        // from Loaded onward (after InitializeComponent), so the element exists.
        if (themeChanged)
        {
            UpdateSearchSortIcon();
        }
    }

    // ----- 배경 따라가는 가장자리 잉크 (2026-08-09) --------------------------
    //
    // The viewer caption strip (name, info line, carousel, zoom chips), the
    // close X and the two edge chevrons all wore theme-fixed or tree-side
    // inks on USER-SET backgrounds, so a palette could swallow them whole -
    // the chevron's glyph was invisible until hover, and the caption clashed
    // outright once 랜덤 existed (user reports, 2026-08-09). The caption
    // originally matching the tree's name colours was the user's spec, made
    // when the viewer background always equalled the tree's; splitting the
    // backgrounds broke that premise, and the user asked for a way out that
    // adds NO colour rows.
    //
    // So these inks are DERIVED, never picked: each surface takes whichever
    // of two candidate inks carries more contrast against the background it
    // actually sits on. Implemented as LOCAL resource overrides scoped to
    // the viewer panel and the two chevron buttons - the shared chip/button
    // styles resolve the very same keys to their ordinary meanings
    // everywhere else. Fills carry ALPHA rather than greys so they blend
    // toward whatever they cover (the grab-line rule); text inks stay solid.
    private void UpdateDerivedEdgeInks(bool light)
    {
        Color viewerBg = ColorConverter.ConvertFromString(
            light ? _settings.LightViewerBackgroundColorHex : _settings.ViewerBackgroundColorHex)
            is Color v ? v : Colors.Black;
        Color treeBg = ColorConverter.ConvertFromString(
            light ? _settings.LightBackgroundColorHex : _settings.BackgroundColorHex)
            is Color t ? t : Colors.Black;

        ApplyEdgeInkOverrides(ViewerPanel.Resources, viewerBg);
        // The collapse chevron sits over the viewer's margin; the expand
        // chevron over the tree's own edge - each follows its actual ground.
        ApplyEdgeInkOverrides(ViewerCollapseButton.Resources, viewerBg);
        ApplyEdgeInkOverrides(ViewerExpandButton.Resources, treeBg);
    }

    private static void ApplyEdgeInkOverrides(ResourceDictionary resources, Color background)
    {
        var lightInk = Color.FromRgb(0xE8, 0xEA, 0xEE);
        var darkInk = Color.FromRgb(0x26, 0x28, 0x2C);
        // Not a fixed luminance threshold: a mid-grey background sits in the
        // band where the threshold answer and the best-contrast answer
        // disagree, and the comparison is two multiplies more.
        bool useLightInk = ContrastRatio(lightInk, background) >= ContrastRatio(darkInk, background);

        SetLocalBrush(resources, "ForegroundText",
            useLightInk ? lightInk : darkInk);
        SetLocalBrush(resources, "FileNameForeground",
            useLightInk ? Color.FromRgb(0xF0, 0xF2, 0xF6) : Color.FromRgb(0x1A, 0x1A, 0x1A));
        var hoverFill = useLightInk
            ? Color.FromArgb(0x2E, 0xFF, 0xFF, 0xFF)
            : Color.FromArgb(0x24, 0x00, 0x00, 0x00);
        SetLocalBrush(resources, "TreeRowHoverBackground", hoverFill);
        SetLocalBrush(resources, "HoverBackground", hoverFill);
        SetLocalBrush(resources, "FooterChipCheckedBackground",
            useLightInk ? Color.FromArgb(0x46, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x38, 0x00, 0x00, 0x00));
        SetLocalBrush(resources, "FooterChipCheckedForeground",
            useLightInk ? Colors.White : Colors.Black);
    }

    // Same recolour-in-place discipline as SetBrushColor, for the same
    // reason - this path runs on every frame of a picker drag.
    private static void SetLocalBrush(ResourceDictionary resources, string key, Color color)
    {
        if (resources[key] is SolidColorBrush { IsFrozen: false } existing)
        {
            if (existing.Color != color)
            {
                existing.Color = color;
            }
            return;
        }
        resources[key] = new SolidColorBrush(color);
    }

    private static double ContrastRatio(Color a, Color b)
    {
        double la = RelativeLuminance(a);
        double lb = RelativeLuminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static double RelativeLuminance(Color c)
    {
        return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);

        static double Channel(byte value)
        {
            double s = value / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
    }

    private SolidColorBrush? _selectionActiveBrush;
    private SolidColorBrush? _selectionInactiveBrush;

    // "Active" deliberately means ANY of this app's windows, not just this
    // one: while the Color Settings dialog holds focus the main window
    // reports inactive, and dimming the selection at that moment would
    // live-preview the wrong brush to the very person adjusting the
    // selection color. Callers on the deactivation side re-check one
    // dispatcher hop late because the incoming window's IsActive isn't set
    // yet while the outgoing window's Deactivated handler runs.
    private void UpdateSelectionBrushForActivation()
    {
        if (_selectionActiveBrush is null || _selectionInactiveBrush is null)
        {
            return;
        }
        bool anyAppWindowActive = Application.Current.Windows.OfType<Window>().Any(w => w.IsActive);
        var wanted = anyAppWindowActive ? _selectionActiveBrush : _selectionInactiveBrush;

        // Only when it actually changes hands. The two brushes keep their
        // identity now and are recoloured in place, so re-assigning the same
        // object would be a dictionary write - and a tree-wide invalidation -
        // for nothing.
        if (!ReferenceEquals(Resources["TreeRowSelectedActiveBackground"], wanted))
        {
            Resources["TreeRowSelectedActiveBackground"] = wanted;
        }
    }

    private void SetBrushColor(string resourceKey, string hex)
    {
        if (ColorConverter.ConvertFromString(hex) is Color color)
        {
            SetBrushColor(resourceKey, color);
        }
    }

    // Recolours the brush that is already there, and only replaces the entry
    // when it cannot.
    //
    // The difference is not small. REPLACING a dictionary entry makes WPF walk
    // the tree invalidating every element that resolves that key; changing the
    // Colour of a brush those elements already hold just redraws them. Twenty
    // replacements per frame during a picker drag is twenty such walks, and
    // that is what the stutter was made of.
    //
    // The fallback matters too: brushes declared in XAML are frozen once a
    // Style that reaches them is sealed (see ApplyColorSettings' own note), so
    // the FIRST call for those keys still has to replace. What it puts there
    // is an ordinary unfrozen brush, so every call after it takes the cheap
    // path.
    private void SetBrushColor(string resourceKey, Color color)
    {
        if (Resources[resourceKey] is SolidColorBrush { IsFrozen: false } existing)
        {
            if (existing.Color != color)
            {
                existing.Color = color;
            }
            return;
        }

        LogFrozenBrush(resourceKey);
        Resources[resourceKey] = new SolidColorBrush(color);
    }

    private readonly HashSet<string> _frozenBrushKeysLogged = new();

    // Which keys never get the cheap path, said once each. Whether the recolour
    // above actually applies depends on WPF's own freezing, which is not
    // something to assume from the outside: if a key shows up here on every
    // session it is still being replaced, and the drag is still paying for it.
    [System.Diagnostics.Conditional("DEBUG")]
    private void LogFrozenBrush(string resourceKey)
    {
        if (!_frozenBrushKeysLogged.Add(resourceKey))
        {
            return;
        }

        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Edgetree");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, "colorperf.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  frozen, replaced: {resourceKey}{Environment.NewLine}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    // How far a quieted name travels towards the background. Matched to the
    // "… 더 보기" row, which is the app's existing example of text that is
    // present but not competing: that row renders its own colour at Opacity
    // 0.65, i.e. 35% of the way to the background. Asked for by eye and then
    // taken from that row rather than guessed (user, 2026-08-02: "… 더 보기
    // 정도 되면 딱 적당할 듯").
    private const double MutedNameBlend = 0.35;

    // Blends a colour `amount` of the way towards the theme's own extreme -
    // black in the dark theme, white in the light one. A blend rather than an
    // Opacity: opacity over an unknown background is a different colour every
    // time, and the app's rule is that nothing says "secondary" by fading.
    private static Color MoveTowardsBackground(Color color, bool light, double amount)
    {
        byte target = light ? (byte)255 : (byte)0;
        return Color.FromArgb(
            color.A,
            (byte)Math.Round(color.R + (target - color.R) * amount),
            (byte)Math.Round(color.G + (target - color.G) * amount),
            (byte)Math.Round(color.B + (target - color.B) * amount));
    }

    // The sort icon itself no longer needs this walk - it is a path that takes
    // the row's brush, so a theme flip recolours it with no per-item work.
    // Its TOOLTIP is still a cached string built from the language strings and
    // the folder's own state, so that part is recomputed here (and the glyph
    // with it, which costs nothing and keeps the two in step).
    private static void RefreshSortOverrideIconForTheme(FileSystemItem item)
    {
        if (item.IsDirectory)
        {
            if (FileSystemService.SortOverrides.TryGetValue(
                FileSystemService.NormalizeSortOverridePath(item.FullPath), out var over))
            {
                item.SortOverrideIconGeometry = FileSystemService.SortOverrideGeometry(over.Descending);
                item.SortOverrideTooltip = FileSystemService.FormatSortTooltip(over.Field, over.Descending);
            }
            else
            {
                item.SortOverrideIconGeometry = FileSystemService.FollowsGlobalSortGeometry;
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
        // Esc leaves the viewer's full cover. Middle-clicking again does too,
        // but a mode that fills the screen has no visible way out of its own,
        // and Esc is what a hand reaches for. Narrow enough not to disturb the
        // tree's own Esc (which calls off a multi-selection or a pending cut).
        if (_viewerFullscreen && e.Key == Key.Escape &&
            Keyboard.FocusedElement is not System.Windows.Controls.Primitives.TextBoxBase)
        {
            SetViewerFullscreen(false);
            e.Handled = true;
            return;
        }

        // Enter toggles the full cover, but ONLY while the viewer is actually
        // showing a decoded picture. Everywhere else Enter keeps meaning what
        // it has always meant in the tree - open the row with its default app,
        // expand the folder - so what is given up is narrow: opening an image
        // in the system photo app by keyboard, while that same image is
        // already on screen here. Double-click and the row menu's 열기 both
        // still do it.
        //
        // "A decoded picture" is _viewerShowingDecodedImage, not merely a
        // Source: a video's or a PSD's SHELL PREVIEW rides in the same Image
        // element, and testing the Source alone stole Enter from those rows
        // too - pressing it toggled a fullscreen still frame instead of
        // playing the file (2026-08-09 review).
        if (_viewerOpen &&
            _viewerPixelWidth > 0 &&
            _viewerShowingDecodedImage &&
            ViewerImage.Source is not null &&
            Keyboard.Modifiers == ModifierKeys.None &&
            e.Key is Key.Enter or Key.Return &&
            Keyboard.FocusedElement is not System.Windows.Controls.Primitives.TextBoxBase)
        {
            SetViewerFullscreen(!_viewerFullscreen);
            e.Handled = true;
            return;
        }

        // Space = next picture, which is what every photo viewer does. The tree
        // has no Space action of its own, so nothing is taken away - and this
        // just asks focus to move down, landing wherever the Down arrow would
        // have, rather than defining a second idea of "next" to keep in sync.
        if (Keyboard.Modifiers == ModifierKeys.None &&
            _viewerOpen &&
            e.Key == Key.Space &&
            Keyboard.FocusedElement is not System.Windows.Controls.Primitives.TextBoxBase)
        {
            if (Keyboard.FocusedElement is UIElement focused &&
                focused.MoveFocus(new System.Windows.Input.TraversalRequest(
                    System.Windows.Input.FocusNavigationDirection.Down)))
            {
                e.Handled = true;
            }
            return;
        }

        // BARE +/- is the viewer's zoom (Ctrl+/- stays the app's font size -
        // two different scales on one pair of keys, told apart by the
        // modifier). Only while the panel is open and showing something
        // zoomable, and never while a text box has the keyboard: rename and
        // the search field both take a literal "+".
        if (Keyboard.Modifiers == ModifierKeys.None &&
            _viewerOpen &&
            _viewerPixelWidth > 0 &&
            Keyboard.FocusedElement is not System.Windows.Controls.Primitives.TextBoxBase)
        {
            switch (e.Key)
            {
                case Key.OemPlus or Key.Add:
                    StepViewerZoom(+1, null);
                    e.Handled = true;
                    return;
                case Key.OemMinus or Key.Subtract:
                    StepViewerZoom(-1, null);
                    e.Handled = true;
                    return;
            }
        }

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
        // The window itself going away - the tray "exit" path never comes
        // through here. _closeReason is set by whichever control started it;
        // "source unknown" means the close came from outside the app's own
        // buttons (Alt+F4, a task-manager end task, an OS-level close).
        ExitLog.Record($"window closing: {_closeReason ?? "source unknown"}");

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
    // keepLeft: the caller has already parked the window somewhere deliberate
    // (off the screen edge, before growing it) and does not want it pulled
    // back to the dock. Everything else here still applies.
    private void PositionToWorkArea(DpiScale? dpiScale = null, bool keepLeft = false)
    {
        // Before anything writes Left below - an animation still holding that
        // property would swallow the assignment (see StopSlide).
        StopSlide();

        var workArea = GetCurrentMonitorWorkArea(dpiScale);

        // Vertical first, horizontal last, and that order is load-bearing.
        // Each of Left/Top/Height is written straight through to the real
        // window, so the states in between are composited and visible. Setting
        // Left first meant a window arriving from off the edge landed there
        // still at its full height and only then shrank to the handle - one
        // frame of a full-height bar at the screen edge, seen as a blink on
        // every hide and as the handle "jumping up and back" at startup
        // (2026-08-05). Sized first, the window is already the right shape by
        // the time it becomes visible.
        //
        // The one place the collapsed shape is decided, which is why the handle
        // is handled HERE and nowhere else: startup, docking, DPI changes,
        // monitor changes and taskbar changes all recompute through this
        // method, so they all follow the handle for free.
        // Not the work area itself any more - the band of it the user has left
        // the sidebar occupying. Full edge until they drag one of the vertical
        // thumbs, and everything below (the handle, the bar, the hover zone)
        // measures from the band so that a shortened sidebar is not hovered
        // where it is not.
        var (bandTop, bandHeight) = DockedBand(workArea);

        if (IsCollapsedToHandle)
        {
            RootContent.Margin = new Thickness(0);
            Height = AutoHideHandleHeight(bandHeight);
            Top = bandTop + ((bandHeight - Height) / 2);
        }
        else
        {
            // The expanded docked window ALWAYS covers the whole work-area
            // edge; the band the user actually sees is RootContent's top and
            // bottom margins (so the content lays out to exactly the band)
            // plus a window region clipping away the strips outside it (see
            // ApplyWindowClipRegion below). Not Top/Height = the band, and
            // that is the point: a WPF window whose geometry changes shows
            // every late frame wrong - shifted by the move, or with
            // uninitialized surface where it grew - which a full day of
            // instrumented rounds (2026-08-07, resize.log) established cannot
            // be prevented from outside the framework: not by single
            // SetWindowPos calls, not by the OS sizing loop, not by
            // WM_NCCALCSIZE valid rects. Parked like this once, resizing the
            // band from either edge changes no window geometry at all.
            // The margins come OFF before Height is written and go back on
            // after, and that order is load-bearing. Height here can SHRINK -
            // a DPI change, a different monitor, the taskbar growing - and if
            // the previous band's margins are still in place when it does,
            // there is an instant where a small Height carries large margins.
            // A layout pass landing in that instant asks the tree to measure
            // at a NEGATIVE height, which throws out of
            // VirtualizingStackPanel ("너비와 높이는 음수일 수 없습니다") and
            // takes the process with it - crashed twice on a language change,
            // whose restart runs exactly this path while the DPI settles
            // (2026-08-07, exit.log).
            RootContent.Margin = new Thickness(0);
            Top = workArea.Top;
            Height = workArea.Height;
            RootContent.Margin = new Thickness(
                0, Math.Max(0, bandTop - workArea.Top),
                0, Math.Max(0, workArea.Bottom - (bandTop + bandHeight)));
            AssertBandFits("position");
        }

        if (!keepLeft)
        {
            Left = _settings.DockOnRight ? workArea.Right - Width : workArea.Left;
        }

        // Rides along here for the same reason the handle's own geometry does:
        // the corner clipping is sized in this window's pixels, so it is stale
        // whenever the size or the DPI it was cut for changes - and every one
        // of those changes already arrives at this method.
        ApplyWindowClipRegion();

        // The menu cap is a fraction of this same work area, so it is stale the
        // moment the window lands on a different monitor, the taskbar resizes,
        // or the DPI changes - all of which come through here.
        ApplyMenuMaxHeight();
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
        var area = new Rect(
            working.Left / dpi.DpiScaleX,
            working.Top / dpi.DpiScaleY,
            working.Width / dpi.DpiScaleX,
            working.Height / dpi.DpiScaleY);

        return LeaveTaskbarRevealStrip(area, screen);
    }

    // Windows leaves an auto-hidden taskbar OUT of the work area - it hands
    // back the whole screen. Filling that meant the sidebar covered the strip
    // the taskbar pops out of, and a cursor sent down there landed inside this
    // window rather than on the screen edge, so the taskbar never came up.
    // Only along the sidebar's own width, which is what made it look
    // intermittent (reported 2026-07-28).
    //
    // A few pixels is all it takes: the reveal only needs the cursor to reach
    // the true edge. Giving back the taskbar's full thickness would work too,
    // but auto-hide is turned on precisely to reclaim that space - taking it
    // back would undo the reason the setting exists.
    private static Rect LeaveTaskbarRevealStrip(Rect area, System.Windows.Forms.Screen screen)
    {
        if (NativeMethods.GetAutoHiddenTaskbar() is not { } bar)
        {
            return area;
        }

        // Multi-monitor: only the primary taskbar has a position to report, so
        // leave other monitors' geometry alone rather than carving a strip out
        // of a screen the taskbar isn't even on.
        var barRect = new System.Drawing.Rectangle(
            bar.Left, bar.Top, bar.Right - bar.Left, bar.Bottom - bar.Top);
        if (!screen.Bounds.IntersectsWith(barRect))
        {
            return area;
        }

        // Left/right taskbars are deliberately not handled: the fix there
        // would be to inset the sidebar from the screen edge it is docked
        // against, and sitting flush to that edge is the whole point of it.
        const int ABE_TOP = 1;
        const int ABE_BOTTOM = 3;
        return bar.Edge switch
        {
            ABE_BOTTOM => new Rect(area.Left, area.Top, area.Width, area.Height - TaskbarRevealStrip),
            ABE_TOP => new Rect(area.Left, area.Top + TaskbarRevealStrip, area.Width, area.Height - TaskbarRevealStrip),
            _ => area,
        };
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
    // tried the same day, and the user still preferred instant.
    //
    // 2026-08-05: asked for again, and granted a THIRD way that neither
    // earlier attempt tried - see SlideTo below. The width still changes in
    // one step here; what moves is the window. Do not bring the width
    // animation back: that one has a confirmed defect, not a taste problem.
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

    // ----- 슬라이드 (2026-08-05) --------------------------------------------
    //
    // The peek open and its close slide in and out by MOVING the window at its
    // final size, rather than growing and shrinking it.
    //
    // This is the one thing the 2026-07-21 round did not try. The defect it
    // found was specific: an animated WIDTH resizes the native window on every
    // tick, and a resize is a re-layout plus a fresh surface for DWM to
    // compose, which is where the irregular ghosting came from. Moving is
    // neither - the same already-rendered surface changes position, so there
    // is nothing per-frame for the app to redraw. The transform slide built
    // that day avoided the defect too, but only the CONTENT glided; the window
    // outline still appeared all at once, which is most likely why it lost to
    // an instant transition.
    //
    // Timed by DISTANCE, not by a fixed duration. A fixed 130ms covered a
    // 250px sidebar and a 1200px one at wildly different speeds, and the
    // narrow case read as a flicker rather than a movement while the wide one
    // read as smooth (2026-08-05: "폭이 커서 애니메이션 공간이 충분하면 부드럽고,
    // 납작하게 하면 너무 빨라 깜빡이는 것 같다"). Roughly constant velocity with
    // both ends clamped: long enough to be seen at any width, short enough that
    // a wide sidebar never feels like it is being waited on.
    // Slowed a long way from the first attempt (which floored at 95ms): on a
    // 60Hz display a fast slide only gets a handful of frames to cross the
    // distance, so the steps between them are visible as shake - the same
    // motion was clean on a 144Hz panel. More time means more frames for the
    // same distance, which is the direct fix. Frame-rate capping is NOT - that
    // was tried in an earlier round and made the steps more visible, not less.
    // Coming in is slower than going out. They are not the same event to
    // watch: arriving is the thing being looked AT, so it can take its time,
    // while leaving is over as far as the user is concerned the moment they
    // have moved on. Making both slow enough to read left the exit feeling
    // like it was dragging its feet.
    private double SlideDurationMs(double distance, bool arriving)
        // Retimed for the short offset above - the old numbers were scaled to a
        // journey the width of the window and would now spend most of a second
        // covering a hundred pixels.
        //
        // Leaving caps far lower than arriving (1200 → 640, user 2026-08-08):
        // the slide-out's distance is the whole window's width, and with the
        // viewer panel that can be 3800px - which pinned every wide close to
        // the old 1.2s ceiling, dragging exactly the half of the animation
        // the eye has already moved on from. Narrow windows never reached
        // either cap, so their feel is untouched.
        => Math.Clamp(360 + (distance * 0.60), 520, arriving ? 1200 : 640);

    // A slide moves the window in from beyond the docked edge - which, with a
    // second display sitting there, means it comes in ACROSS that display
    // (2026-08-05). The motion is correct and the user read it as such, but a
    // sidebar sweeping over the neighbouring monitor is worse than no
    // animation, so that case simply doesn't slide.
    private bool HasScreenBeyondDockedEdge()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        var current = System.Windows.Forms.Screen.FromHandle(hwnd);
        foreach (var screen in System.Windows.Forms.Screen.AllScreens)
        {
            if (screen.Equals(current))
            {
                continue;
            }

            // Only a display the window would actually travel over: one that
            // shares some vertical span with this monitor and lies on the side
            // the sidebar slides in from.
            bool overlapsVertically =
                screen.Bounds.Bottom > current.Bounds.Top &&
                screen.Bounds.Top < current.Bounds.Bottom;
            if (!overlapsVertically)
            {
                continue;
            }

            bool isBeyondEdge = _settings.DockOnRight
                ? screen.Bounds.Left >= current.Bounds.Right
                : screen.Bounds.Right <= current.Bounds.Left;
            if (isBeyondEdge)
            {
                return true;
            }
        }

        return false;
    }

    // The window's parked position: one full width beyond the docked edge.
    //
    // Back to moving the WINDOW rather than its contents. Translating the
    // contents can never look like a sidebar arriving, because the window's own
    // rectangle is opaque and already there - whatever slides inside it merely
    // uncovers that rectangle, and shortening the travel until the uncovered
    // strip stops being distracting also shortens it until the motion stops
    // being visible at all (2026-08-05: "그냥 애니메이션 없을 때와 별 차이가
    // 없네요"). There is no useful setting in between.
    //
    // The earlier attempt at this was abandoned over an explanation that was
    // wrong: that a window off the screen edge is not drawn out there. It is -
    // DWM holds a surface per window, which is why dragging a window half off
    // the screen and back does not repaint it. What actually went out blank was
    // a window that had not finished rendering YET, because the slide was
    // started after a layout pass rather than after a frame (see AfterNextFrame).
    private double SlideAwayLeft(double dockedLeft)
        => _settings.DockOnRight ? dockedLeft + Width : dockedLeft - Width;

    // Waits for the compositor to actually put a frame up, which a dispatcher
    // hop at Loaded priority does not: that only means layout has run. With a
    // virtualizing tree and icons arriving from a background thread, the gap
    // between "measured and arranged" and "there is something to look at" is
    // several frames wide, and starting the slide inside that gap is what sent
    // an empty sidebar across the screen.
    private void AfterNextFrame(Action action)
    {
        int frames = 0;
        EventHandler? onRendering = null;
        onRendering = (_, _) =>
        {
            // The first Rendering after this is queued still precedes the frame
            // that includes our layout; the second one follows it.
            if (++frames < 2)
            {
                return;
            }

            CompositionTarget.Rendering -= onRendering;
            action();
        };

        CompositionTarget.Rendering += onRendering;
    }

    // The animation is on a TranslateTransform, not on the window - see the
    // RootContent comment in XAML. Same teardown discipline as before: the
    // animated value outranks a plain assignment until it is removed.
    // runPendingCompletion: a slide carries work to do once it is out of sight
    // (collapsing to the handle). Abandoning the animation must not abandon
    // that too - a window left full-size while the rest of the app believes it
    // is hidden is exactly the state the intermittent "sometimes it just snaps"
    // and "the click outside gets eaten" reports came from. The one case that
    // legitimately drops it is a hide being turned back into a reveal, which
    // says so explicitly.
    private void StopSlide(bool runPendingCompletion = true)
    {
        if (!_slideInFlight)
        {
            _slideCompletion = null;
            return;
        }

        LogAutoHide($"slide    STOPPED runPending={runPendingCompletion}");

        // Invalidates the running slide's completion handler. Removing the
        // animation with BeginAnimation(..., null) does NOT reliably stop that
        // handler from firing afterwards, and when it did it wrote its own
        // target - a position off the screen edge - over whatever had just been
        // decided. That is what took the sidebar out of reach: a hide turned
        // back into a reveal put the window home, and the abandoned slide then
        // shoved it back out (2026-08-05, caught in autohide.log at left=-503
        // after a reveal had returned it to 0).
        _slideToken++;

        double current = Left;
        BeginAnimation(LeftProperty, null);
        Left = current;
        _slideInFlight = false;

        var pending = _slideCompletion;
        _slideCompletion = null;
        if (runPendingCompletion)
        {
            pending?.Invoke();
        }
    }

    // arriving: eased so the window settles as it lands. Leaving takes the
    // opposite curve - a slide out that decelerates spends its last frames
    // crawling the final few pixels, which showed as a thin bar hesitating at
    // the edge before it vanished, read as a bounce (2026-08-05: "20px 두께
    // 정도로 검은 바가 살짝 나타났다 사라져 바운스 되는 느낌"). Accelerating away
    // has nothing to linger over.
    private void SlideTo(double targetLeft, bool arriving, Action? onCompleted = null)
    {
        StopSlide();

        var slide = new System.Windows.Media.Animation.DoubleAnimation
        {
            To = targetLeft,
            Duration = TimeSpan.FromMilliseconds(
                SlideDurationMs(Math.Abs(targetLeft - Left), arriving)),
            // Arriving and leaving get different curves, not mirrored ones.
            //
            // Leaving keeps a cubic EaseIn - it starts gently and accelerates
            // away, which is the half that has read well throughout.
            //
            // Arriving had the mirror of that, a cubic EaseOut, and it covered
            // most of the distance in the first third: the sidebar appeared to
            // pop into place and then creep the last few pixels, which is the
            // "팟 하고 뜬다" that survived every change to the duration - the
            // problem was never how long it took. A power of 1.3 is barely more
            // than linear, just enough to settle at the end.
            EasingFunction = arriving
                ? new System.Windows.Media.Animation.PowerEase
                {
                    Power = 1.3,
                    EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                }
                : new System.Windows.Media.Animation.CubicEase
                {
                    EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn
                },
            // HoldEnd, not Stop. With Stop the property drops back to its base
            // value the instant the animation ends and only picks up the final
            // one when the handler below runs - one frame with the window back
            // at the docked edge, full height, contents already hidden. That
            // frame was visible as a pale full-height bar flashing in and out
            // as the sidebar finished hiding (2026-08-05: "흰색 긴 바와 같이...
            // 좀 깜빡이는 걸로 보입니다").
            FillBehavior = System.Windows.Media.Animation.FillBehavior.HoldEnd
        };

        int token = ++_slideToken;
        slide.Completed += (_, _) =>
        {
            // Anything that stopped or replaced this slide has moved the token
            // on; this handler no longer speaks for the window.
            if (token != _slideToken)
            {
                return;
            }

            // Base value first, then release the hold: written in this order
            // the held value and the base value are already the same when the
            // animation lets go, so there is no frame in between showing
            // anything else.
            Left = targetLeft;
            BeginAnimation(LeftProperty, null);
            _slideInFlight = false;

            var pending = _slideCompletion;
            _slideCompletion = null;
            pending?.Invoke();
        };

        _slideInFlight = true;
        _slideCompletion = onCompleted;
        BeginAnimation(LeftProperty, slide);
    }

    // Whether the window may travel beyond the docked edge at all. Checked by
    // the callers BEFORE they move anything: declining the slide has to mean
    // the window never leaves, not that it jumps there instantly. Moving it
    // out and back was enough to relocate the sidebar entirely - the window
    // landed on the neighbouring display, and PositionToWorkArea, which asks
    // which monitor the window is currently on, then docked it to that one
    // (2026-08-05: "아직도 왼쪽 모니터로 넘어가는데요").
    private bool CanSlide => _settings.AutoHideSlide && !HasScreenBeyondDockedEdge();


    // Clamped defensively at the point of use (like MaxItemsPerFolder/
    // TabSpacing elsewhere) rather than trusting a hand-edited settings file.
    private double AutoHideSliverWidth => Math.Clamp(_settings.AutoHideSliverWidth, 3, 8);

    // True exactly while the window is standing in as the handle - docked,
    // auto-hidden, not currently peeked open, and the option on. Everything
    // about the handle keys off this one expression so the shape can never
    // disagree with itself.
    private bool IsCollapsedToHandle =>
        _isDocked && _settings.IsAutoHidden && !_isAutoHideRevealed && _settings.AutoHideUseHandle;

    // Same width the sliver uses, but with a floor: at the 3px end a handle
    // stops being a handle. The thickness stepper still applies above that.
    private double CollapsedWidth => _settings.AutoHideUseHandle
        ? Math.Max(AutoHideSliverWidth, 6)
        : AutoHideSliverWidth;

    // The shortest the docked window may be dragged. The header alone is 36px,
    // so anything under this is a sidebar with no tree in it - and the thumbs
    // that would undo the mistake are its own two edges, which by then are
    // almost touching.
    private const double MinDockedHeight = 150;

    // Hold a modifier while dragging a docked edge and it lands on a fraction
    // of the screen instead of wherever the cursor is (user, 2026-08-09). Two
    // grids rather than one, and both fine enough to be worth reaching for:
    // SHIFT is the everyday one and CTRL refines it (the user's own pairing -
    // the first version had them the other way round and read backwards).
    // Multiples of each other on purpose, so every Shift line is also a Ctrl
    // line and refining never means starting over.
    private const int DockedSnapDivisionsCoarse = 16;
    private const int DockedSnapDivisionsFine = 32;

    // Null means no snapping. The FINER grid wins when both are down: adding
    // a second key reads as asking for more precision, not less.
    private static int? DockedSnapDivisions
    {
        get
        {
            var modifiers = Keyboard.Modifiers;
            if (modifiers.HasFlag(ModifierKeys.Control))
            {
                return DockedSnapDivisionsFine;
            }

            return modifiers.HasFlag(ModifierKeys.Shift) ? DockedSnapDivisionsCoarse : null;
        }
    }

    // Read live off the keyboard on every drag event rather than captured when
    // the drag started, so a key can be taken, swapped and let go mid-drag -
    // which is how snapping works in everything else that has it.
    private static double SnapToGrid(double value, double origin, double extent)
    {
        if (DockedSnapDivisions is not { } divisions || extent <= 0)
        {
            return value;
        }

        double step = extent / divisions;
        return origin + Math.Round((value - origin) / step) * step;
    }

    // The work area the current band drag is snapping against, captured at
    // DragStarted so a monitor query doesn't ride every mouse move.
    private double _snapGridOriginDip;
    private double _snapGridExtentDip;

    // Which slice of the screen edge the docked window occupies - the whole of
    // it by default, less once the top/bottom thumbs have been dragged.
    //
    // Clamped here rather than trusted from the file (same rule as
    // MaxItemsPerFolder and AutoHideSliverWidth), and the position is a fraction
    // of the LEFTOVER space, so no pair of values can put the band off screen.
    private (double Top, double Height) DockedBand(Rect workArea)
    {
        double height = Math.Clamp(
            workArea.Height * Math.Clamp(_settings.DockedHeightRatio, 0, 1),
            Math.Min(MinDockedHeight, workArea.Height),
            workArea.Height);

        double slack = workArea.Height - height;
        double top = workArea.Top + (slack * Math.Clamp(_settings.DockedTopRatio, 0, 1));
        return (top, height);
    }

    // A share of the sidebar's own band rather than a fixed number of pixels,
    // bounded at both ends: 12% of a 43-inch display is still a sensible grab
    // target, while a fixed 120px would be a stripe on one screen and a speck on
    // another. The clamps are what stop either extreme.
    //
    // Measured from the BAND, not the screen: a sidebar occupying the top third
    // has to leave its handle in that third, or the handle is somewhere the
    // window will never appear.
    private static double AutoHideHandleHeight(double bandHeight)
        => Math.Min(bandHeight, Math.Clamp(bandHeight * 0.12, 60, 160));

    // Only the two corners facing AWAY from the screen edge - the pair against
    // the edge has nothing to show a curve against. Rounding both pairs (which
    // is all DWM can do) would cut a notch out of the screen edge itself.
    private const double AutoHideHandleCornerRadius = 4;

    // Every window region this app uses, applied and cleared by the same
    // method so the states can't drift apart: the collapsed handle's rounded
    // corners, the expanded band's top clip (the strip between the work area's
    // top and where the band starts - see PositionToWorkArea for why the
    // window covers it at all), or no region. Called from PositionToWorkArea,
    // which every path that can change the window's size, edge or DPI already
    // goes through, and from the top grip's DragDelta, which moves the band
    // clip live.
    //
    // What was last applied is remembered so identical consecutive requests
    // don't touch the window: SetWindowRgn is a full-frame invalidate, and
    // re-issuing it for no change is exactly the per-event churn that
    // WindowChromeWorker's non-glass path was caught doing (see Dock()).
    private const int ClipUnknown = 0;
    private const int ClipHandle = 1;
    private const int ClipNone = 2;
    private const int ClipBand = 3;
    private (int Kind, int TopPx, int BottomPx) _appliedClip = (ClipUnknown, 0, 0);

    // The invariant the band arrangement rests on: the margins hold the strips
    // OUTSIDE the band, so together they can never reach the window's height -
    // whatever is left is the band, and a band is never empty.
    //
    // An INSTRUMENT, not a guard: it records and changes nothing. Clamping the
    // margins here instead would keep the app alive while quietly drawing the
    // band in the wrong place, and would make the next mis-ordered write look
    // fine. Breaking this means some path wrote Height and the margins in the
    // wrong order (see PositionToWorkArea) - the line names which one.
    [System.Diagnostics.Conditional("DEBUG")]
    private void AssertBandFits(string where)
    {
        double content = Height - RootContent.Margin.Top - RootContent.Margin.Bottom;
        if (content <= 0)
        {
            ExitLog.Record($"band invariant broken at {where}: Height={Height:F2} " +
                $"margin={RootContent.Margin.Top:F2}/{RootContent.Margin.Bottom:F2} " +
                $"content={content:F2}");
        }
    }

    private void ApplyWindowClipRegion()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        // The sizes things SHOULD have, not ActualWidth/ActualHeight: the
        // latter lag a layout pass behind PositionToWorkArea, and this runs
        // right after it. Regions are in physical pixels; everything above is
        // in DIPs.
        var dpi = VisualTreeHelper.GetDpi(this);

        if (IsCollapsedToHandle)
        {
            // Re-applied unconditionally: unlike the two cases below, its
            // shape depends on width and DPI as well, which the cheap state
            // tuple doesn't capture.
            _appliedClip = (ClipHandle, 0, 0);
            NativeMethods.SetRoundedSideRegion(
                hwnd,
                (int)Math.Round(CollapsedWidth * dpi.DpiScaleX),
                (int)Math.Round(Height * dpi.DpiScaleY),
                (int)Math.Round(AutoHideHandleCornerRadius * dpi.DpiScaleX),
                roundLeftSide: _settings.DockOnRight);
            return;
        }

        if (_isDocked && (RootContent.Margin.Top > 0.5 || RootContent.Margin.Bottom > 0.5))
        {
            var wanted = (ClipBand,
                (int)Math.Round(RootContent.Margin.Top * dpi.DpiScaleY),
                (int)Math.Round((Height - RootContent.Margin.Bottom) * dpi.DpiScaleY));
            if (_appliedClip != wanted)
            {
                _appliedClip = wanted;
                NativeMethods.SetBandRegion(hwnd, wanted.Item2, wanted.Item3);
            }
            return;
        }

        if (_appliedClip.Kind != ClipNone)
        {
            _appliedClip = (ClipNone, 0, 0);
            NativeMethods.ClearWindowRegion(hwnd);
        }
    }

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
        // EXPERIMENT (2026-08-08, user request): the viewer panel used to
        // fold here so hide/reveal only ever handled the tree-only width.
        // Now it rides along - the slide and collapse just see a wider
        // window, and RevealFromAutoHide adds the panel's width back on. If
        // hide/reveal misbehaves with the viewer open, this is the first
        // place to look (the fold was one CloseViewer() call right here).

        _settings.IsAutoHidden = true;
        StopHoverReveal();

        // After IsAutoHidden is set, never before - PositionToWorkArea reads it
        // through IsCollapsedToHandle to decide whether this is a handle or a
        // full-height sliver. Same ordering rule at all three transitions.
        // Entering auto-hide slides out the same way a peek closing does. It
        // is the same thing happening as far as the eye is concerned, and
        // having one of them animate and the other snap made the app look like
        // it had two different ways of hiding.
        if (CanSlide)
        {
            SlideTo(SlideAwayLeft(Left), arriving: false, CollapseAfterSlide);
        }
        else
        {
            CollapseAfterSlide();
        }

        UpdatePinButtonVisibility();
        ApplyTopmostState("enter");
    }

    // The single place that decides whether this window belongs on top, and
    // the only one that can be trusted to make it so.
    //
    // Two things had to be separated here. WPF's Topmost is a preference the
    // framework writes through only when the property's VALUE changes - so
    // once its belief and the real window disagree, no assignment can ever fix
    // it. And the real window's z-order is lost easily: every
    // read-modify-write of the extended style (MakeToolWindow/MakeAppWindow at
    // startup and at each dock change) can drop it. Measured 2026-07-25 on a
    // fresh launch: extended style 0x00000080, TOOLWINDOW only, no TOPMOST,
    // while the app believed Topmost was true - meaning "항상 위에 표시" was not
    // actually in effect until the option happened to be toggled, and an
    // auto-hide sliver could be covered by any window with no way back to it
    // but a restart (reported by a user 2026-07-25).
    //
    // So: recompute what the state should be, keep WPF's own belief in step
    // with it, then state it to the window manager directly either way.
    private void ApplyTopmostState(string reason)
    {
        bool shouldBeTopmost = _settings.AlwaysOnTop || (_isDocked && _settings.IsAutoHidden);
        Topmost = shouldBeTopmost;

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        bool osTopmost = NativeMethods.HasTopmostStyle(hwnd);
        if (osTopmost == shouldBeTopmost)
        {
            return;
        }

        // The window disagrees with what it should be, and neither obvious
        // remedy works on its own (both measured 2026-07-25):
        //
        // - Assigning Topmost again does nothing: WPF writes the property
        //   through only when its VALUE changes, and it already holds the
        //   value we want. So the flip below is deliberate, not superstition.
        // - A raw SetWindowPos(HWND_TOPMOST) on our own handle returns success
        //   and changes nothing, from inside the app or out. With
        //   ShowInTaskbar=false this window is OWNED by a hidden helper window
        //   WPF creates, and an owned window's z-order follows its owner -
        //   which only WPF's own setter knows to update as well.
        Topmost = !shouldBeTopmost;
        Topmost = shouldBeTopmost;

        // Re-raise within the topmost band afterwards. Harmless when the flip
        // already did the job, and it covers a window that holds the flag but
        // sits below another topmost window.
        NativeMethods.SetTopmost(hwnd, shouldBeTopmost);

        LogTopmostMismatch(reason, hwnd, shouldBeTopmost, osTopmost, NativeMethods.HasTopmostStyle(hwnd));
    }

    [System.Diagnostics.Conditional("DEBUG")]
    private static void LogTopmostMismatch(string reason, IntPtr hwnd, bool expected, bool before, bool after)
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Edgetree");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, "autohide.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {reason}: hwnd=0x{hwnd.ToInt64():X} expected={expected} was={before} nowIs={after}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
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
        StopDragReveal();
        StopHoverReveal();
        StopCollapsedWatch();
        StopSlide();
        _settings.IsAutoHidden = false;
        _isAutoHideRevealed = false;
        _revealedByDrag = false;
        ApplyWindowClipRegion();
        ApplyTopmostState("exit");
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
        // The cut markers get their re-check here as well as on activation:
        // this window is usually reached by moving the mouse onto it, NOT by
        // clicking it, so OnActivated can be minutes late (2026-07-28: "돌아와서
        // 공백에 클릭을 해서 포커스를 줘야 풀립니다"). Both cost nothing while
        // nothing is cut.
        DropCutMarksIfClipboardMovedOn();
        DropCutMarksForVanishedPaths();

        if (!_isDocked || !_settings.IsAutoHidden || _isAutoHideRevealed)
        {
            return;
        }

        StartHoverReveal();
    }

    // A short dwell before the sidebar comes out, so brushing the edge on the
    // way somewhere else doesn't summon it (2026-08-05 request). The drag path
    // has had this since v1.3.3 at 400ms; the pointer path never did, because
    // opening the instant the mouse arrives IS what makes an auto-hidden
    // sidebar feel immediate - the cost of a delay is paid on every deliberate
    // reveal too. Hence a much shorter dwell here than for drags.
    //
    // Modelled on the drag countdown deliberately, including its hard-won part:
    // the timer verifies the pointer is STILL here rather than trusting
    // MouseLeave to have cancelled it. MouseLeave doesn't reliably arrive for a
    // fast brush past a few-pixel target, and a cancel that can be missed is no
    // cancel - that was the 2026-07-27 "flash" where the window opened after
    // the pointer was long gone and then shut itself again.
    private const int AutoHideHoverRevealDelayMs = 150;

    private void StartHoverReveal()
    {
        // Repeated MouseEnter (re-entering after a moment, or the mouse moving
        // within the sliver) must not restart the countdown - holding still
        // near the edge should still open it.
        if (_hoverRevealTimer is not null)
        {
            return;
        }

        _hoverRevealTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(AutoHideHoverRevealDelayMs)
        };
        _hoverRevealTimer.Tick += HoverRevealTimer_Tick;
        _hoverRevealTimer.Start();
    }

    private void HoverRevealTimer_Tick(object? sender, EventArgs e)
    {
        StopHoverReveal();

        // Re-checked rather than assumed: the pin button, a re-dock or a drag
        // reveal could all have changed the state during the dwell.
        if (!_isDocked || !_settings.IsAutoHidden || _isAutoHideRevealed)
        {
            return;
        }

        if (!IsCursorInsideWindow())
        {
            return;
        }

        RevealFromAutoHide();
    }

    private void StopHoverReveal()
    {
        if (_hoverRevealTimer is null)
        {
            return;
        }

        _hoverRevealTimer.Stop();
        _hoverRevealTimer.Tick -= HoverRevealTimer_Tick;
        _hoverRevealTimer = null;
    }

    // Shared by the cursor reveal above and the drag reveal below. Callers
    // check the "should this open at all" conditions themselves - by the time
    // they get here, opening is decided.
    private void RevealFromAutoHide()
    {
        _autoHideRehideTimer?.Stop();
        StopHoverReveal();
        StopCollapsedWatch();
        LogAutoHide("reveal   enter");

        // A hide caught part-way out is put straight back, not re-opened from
        // scratch: the window is still full size with its content up, so all
        // it needs is its position. Rebuilding it instead re-sized and
        // re-filled it at the dock first, which was visible as a double flash.
        if (_slideInFlight && !_isAutoHideRevealed)
        {
            _isAutoHideRevealed = true;
            // The collapse this hide was going to do is no longer wanted - the
            // window is staying. Only this path may drop it.
            StopSlide(runPendingCompletion: false);
            PositionToWorkArea();
            UpdatePinButtonVisibility();
            ApplyTopmostState("reveal");

            if (!_settings.AutoHideCloseOnMouseLeave)
            {
                StartAutoHideOutsideClickWatch();
            }
            return;
        }

        _isAutoHideRevealed = true;

        // Plus the viewer panel if it stayed open through the hide (the
        // experiment note in EnterAutoHide) - ExpandedWidth is the tree
        // alone, and restoring only that would crush the panel's column.
        // Same split-floor clamp as startup: with the panel open the tree
        // share may sit below the window floor, and rounding it up here
        // would move what the middle divider set.
        double treeWidth = _viewerOpen
            ? Math.Clamp(_settings.ExpandedWidth, MinTreeSplitWidth, MaxExpandedWidth)
            : ClampExpandedWidth(_settings.ExpandedWidth);
        double expandedWidth = treeWidth + CurrentViewerPanelWidth;

        // Revealing never slides, and that asymmetry is the whole finding of
        // 2026-08-05. A window has no pixels where it is off the screen, so one
        // sliding IN hands the app a freshly exposed strip to draw on every
        // frame - the sidebar visibly builds itself as it arrives, at any
        // speed, at any width, however early the content is prepared. Tried
        // three ways round; the smear is the drawing, not the moving.
        //
        // Sliding OUT has no such problem: everything is already drawn and
        // simply leaves. That half is kept below (see CloseAutoHideReveal),
        // and it is the half worth having anyway - arriving happens under a
        // cursor that is already there waiting to use it, while leaving
        // happens after the eye has moved on.
        //
        // Do not "fix" this by animating the reveal again without a way to
        // keep the off-screen pixels. Moving the CONTENT instead was also
        // tried: no smear, but the window's own rectangle still appears all at
        // once, so a long travel exposes an empty panel and a short one is
        // indistinguishable from no animation at all.
        // Before anything moves. The content only comes back when the widening
        // finishes, so this cannot wait for that call the way it does on the
        // way in - it would be stretched across the growing window the whole
        // time (see UpdateAutoHideHandleOverlay).
        UpdateAutoHideHandleOverlay(collapsed: false);

        // Full height back before the width change, not after: the flag above
        // has already been set, so this restores the whole edge, and the
        // widening then happens at the final height.
        PositionToWorkArea();

        AnimateWidth(expandedWidth, onCompleted: () =>
        {
            SetExpandedContentVisibility(Visibility.Visible);
        });

        UpdatePinButtonVisibility();

        // Growing the window doesn't change its z-order, so a sliver that had
        // ended up behind another window peeks open behind it too - reported
        // 2026-07-25 with a code editor moved over the docked edge.
        ApplyTopmostState("reveal");

        if (!_settings.AutoHideCloseOnMouseLeave)
        {
            StartAutoHideOutsideClickWatch();
        }
    }

    // Windows sends no MouseEnter during an OLE drag - the drag loop owns the
    // mouse and delivers DragEnter/DragOver instead - so the hover reveal above
    // is blind to a file being dragged at the sliver. Without this an
    // auto-hidden sidebar cannot be a drop target at all: its content is
    // collapsed too, so there isn't a single row for the drop to land on.
    private void MainWindow_DragOver(object sender, DragEventArgs e)
    {
        // Rows handle their own DragOver and mark it handled, so reaching here
        // means the cursor is over the sliver or over chrome. Once the window
        // is open, this has nothing left to do.
        if (!_isDocked || !_settings.IsAutoHidden || _isAutoHideRevealed)
        {
            StopDragReveal();
            return;
        }

        // The sliver itself can't take the drop - be honest about that with
        // the cursor while the dwell counts down, rather than showing a copy
        // cursor over something that would silently swallow the drop.
        e.Effects = DragDropEffects.None;
        e.Handled = true;

        // DragOver repeats for as long as the drag is over the window, so the
        // countdown starts on first contact and is not restarted by the small
        // movements of a hand holding still.
        if (_dragRevealTimer is not null)
        {
            return;
        }

        _dragRevealTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(AutoHideDragRevealDelayMs)
        };
        _dragRevealTimer.Tick += DragRevealTimer_Tick;
        _dragRevealTimer.Start();
    }

    private void DragRevealTimer_Tick(object? sender, EventArgs e)
    {
        StopDragReveal();

        // Re-checked rather than assumed: the pin button or a re-dock could
        // have changed the state during the dwell.
        if (!_isDocked || !_settings.IsAutoHidden || _isAutoHideRevealed)
        {
            return;
        }

        // The condition that has to hold is "the drag is still here", so ask
        // that directly instead of relying on DragLeave to have cancelled the
        // countdown. It doesn't reliably arrive for a fast brush past a
        // 3-8px sliver, and when it's missed the window opens a moment after
        // the drag is long gone and then shuts itself again - the flash
        // reported 2026-07-27. A cancel that can be missed is no cancel.
        if (!IsCursorInsideWindow())
        {
            LogDragReveal("dwell elapsed but the pointer had already left");
            return;
        }

        LogDragReveal("revealed by drag dwell");
        _revealedByDrag = true;
        RevealFromAutoHide();
    }

    // ----- 자동 숨김 상태 전이 계측 (2026-08-05) ----------------------------
    //
    // NOT Conditional("DEBUG") - unlike LogDragReveal below, because the thing
    // it is chasing only shows up in the Release build being tested by hand,
    // roughly twice in ten open/close cycles: a click outside gets eaten, and
    // the hide that follows skips its animation. Two guesses have already been
    // spent on it. One line per transition is cheap enough to leave running
    // until it is caught.
    //
    // What to read: every reveal should be followed by exactly one rehide, and
    // slide=True on both. A rehide with slide=False, or two of anything in a
    // row, is the fault.
    private void LogAutoHide(string what)
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Edgetree");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, "autohide.log"),
                $"{DateTime.Now:HH:mm:ss.fff}  {what}  " +
                $"revealed={_isAutoHideRevealed} sliding={_slideInFlight} " +
                $"pending={_slideCompletion is not null} " +
                $"hidden={_settings.IsAutoHidden} docked={_isDocked} " +
                $"canSlide={CanSlide} left={Left:F0} width={Width:F0} height={Height:F0}" +
                Environment.NewLine);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    [System.Diagnostics.Conditional("DEBUG")]
    private static void LogDragReveal(string what)
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Edgetree");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, "autohide.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  drag reveal: {what}{Environment.NewLine}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    private void MainWindow_DragLeave(object sender, DragEventArgs e)
    {
        // DragLeave bubbles up from every row the cursor crosses on its way
        // through an open tree, so "the drag left the window" has to be
        // measured, not taken at face value - believing it would close the
        // window from under a drag that is still inside it.
        System.Windows.Point p = e.GetPosition(this);
        if (p.X >= 0 && p.Y >= 0 && p.X < ActualWidth && p.Y < ActualHeight)
        {
            return;
        }

        StopDragReveal();
        EndDragReveal();
    }

    private void MainWindow_Drop(object sender, DragEventArgs e)
    {
        // Only reached for drops onto chrome, never onto a row (those are
        // handled and don't bubble). Nothing to import - this just makes sure
        // the dwell doesn't outlive the drag that started it.
        StopDragReveal();
    }

    private void StopDragReveal()
    {
        if (_dragRevealTimer is null)
        {
            return;
        }

        _dragRevealTimer.Stop();
        _dragRevealTimer.Tick -= DragRevealTimer_Tick;
        _dragRevealTimer = null;
    }

    // A drag that opened the window and then left has to close it again: the
    // cursor never "entered" as far as Windows is concerned, so no MouseLeave
    // is coming to do it. Drops are the other ending, and those leave the
    // cursor inside, where the ordinary MouseLeave takes over.
    private void EndDragReveal()
    {
        if (!_revealedByDrag)
        {
            return;
        }

        _revealedByDrag = false;
        ArmAutoHideRehideTimer();
    }

    // A short delay (rather than hiding the instant the cursor leaves) so
    // briefly overshooting the sliver's edge on the way in/out doesn't
    // instantly slam it shut again. Only relevant to the default
    // AutoHideCloseOnMouseLeave mode - the click-outside alternative
    // (StartAutoHideOutsideClickWatch) doesn't care about the cursor leaving
    // at all.
    private void MainWindow_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        // Cancels a countdown that hasn't fired yet - the ordinary way a brush
        // past the edge ends. Not the only way it can end, which is why the
        // tick re-checks the cursor as well.
        StopHoverReveal();
        ArmAutoHideRehideTimer();
    }

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

        // Same for a drag that is still running (a scrollbar being held, say):
        // keep polling rather than closing under it. Restarted, not returned
        // from, so the close still happens once the gesture ends out here.
        if (IsPointerGestureFromInsideWindow)
        {
            _autoHideRehideTimer.Start();
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
        _revealedByDrag = false;
        LogAutoHide("rehide   enter");

        // A reveal caught mid-flight does NOT get turned around with a second
        // animation. That was tried, and it stranded the window: the reversing
        // slide ended after 91ms of its 452 (2026-08-05 log), leaving the
        // sidebar parked off the screen edge with no handle to reach it by -
        // indistinguishable from a crash from the outside. Starting an
        // animation on a property another animation is still holding is not
        // worth the picture it buys. Stop where it is and collapse.
        if (_slideInFlight)
        {
            _isAutoHideRevealed = false;
            StopSlide(runPendingCompletion: false);
            CollapseAfterSlide();
            UpdatePinButtonVisibility();
            ApplyTopmostState("rehide");
            return;
        }

        _isAutoHideRevealed = false;

        // Content stays visible and the window stays full-width for the whole
        // way out - that IS the slide. Collapsing to the handle happens once
        // the window is already off the edge, where there is nothing to see it
        // happen. The reverse order (collapse, then move) is just the old
        // instant transition with a delay in front of it.
        //
        // Armed before the slide, not after it lands: the gap it covers - an
        // edge with nothing on it to hover - IS the slide.
        StartCollapsedWatch();

        if (CanSlide)
        {
            SlideTo(SlideAwayLeft(Left), arriving: false, CollapseAfterSlide);
        }
        else
        {
            CollapseAfterSlide();
        }

        UpdatePinButtonVisibility();
        ApplyTopmostState("rehide");
    }

    // The work that has to happen once the window is out of sight, wherever
    // the hide was started from. A named method rather than a local function
    // because two paths through CloseAutoHideReveal and one through
    // EnterAutoHide all register it, and a slide that gets reversed hands it
    // to the reversal (see StopSlide) rather than dropping it - an abandoned
    // collapse leaves the window full-size while everything else believes it
    // is hidden, which is where the "sometimes it just snaps, sometimes the
    // click outside does nothing" reports came from.
    private void CollapseAfterSlide()
    {
        LogAutoHide("collapse run");
        SetExpandedContentVisibility(Visibility.Collapsed);
        AnimateWidth(CollapsedWidth);
        PositionToWorkArea();
        VerifyCollapsedPosition("collapse");
        StartCollapsedWatch();
    }

    // Runs while the sidebar is hidden OR on its way there, which is exactly
    // when nothing else is watching: no mouse events arrive at a window that
    // has left the edge, so neither a stranded sidebar nor a hover aimed at
    // where the handle is about to be has any other way of being noticed.
    //
    // The interval is set by the second job, not the first: a rescue could
    // afford to be a second late, but answering a hover cannot.
    private void StartCollapsedWatch()
    {
        // Disarmed at the start of every hide: whether this counts as a hover
        // is decided by where the cursor goes from here, not where it already
        // is.
        _collapsedZoneArmed = false;

        _collapsedWatchTimer ??= new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(120)
        };
        _collapsedWatchTimer.Tick -= CollapsedWatchTimer_Tick;
        _collapsedWatchTimer.Tick += CollapsedWatchTimer_Tick;
        _collapsedWatchTimer.Start();
    }

    private void StopCollapsedWatch() => _collapsedWatchTimer?.Stop();

    private void CollapsedWatchTimer_Tick(object? sender, EventArgs e)
    {
        if (!_isDocked || !_settings.IsAutoHidden || _isAutoHideRevealed)
        {
            StopCollapsedWatch();
            return;
        }

        VerifyCollapsedPosition("watch");

        // While the sidebar is sliding out there is nothing at the screen edge
        // yet - the window has left and the handle is not drawn until it lands.
        // Moving back to the edge during those few hundred milliseconds hit
        // nothing at all, so the reveal was simply lost and the handle seemed
        // to stay away far longer than it had (2026-08-05). The zone answers
        // for it in the meantime: invisible, but there.
        if (!_slideInFlight)
        {
            return;
        }

        // ENTERING the zone, not merely being in it. A cursor already sitting
        // there when the hide began has not asked for anything - it is where
        // the sidebar just was. Treating that as a hover reopened the sidebar
        // the instant it was dismissed, and with "close on mouse leave" off it
        // then stayed open until something else was clicked (2026-08-05, fully
        // reproducible). So the zone arms only once the cursor has been seen
        // outside it.
        if (!IsCursorInCollapsedZone())
        {
            _collapsedZoneArmed = true;
            return;
        }

        if (_collapsedZoneArmed)
        {
            _collapsedZoneArmed = false;
            RevealFromAutoHide();
        }
    }

    // The rectangle the collapsed sidebar occupies - or is about to, while it
    // is still on its way out. Computed from the work area rather than from the
    // window, precisely because the window is not there yet.
    private bool IsCursorInCollapsedZone()
    {
        var workArea = GetCurrentMonitorWorkArea();
        var (bandTop, bandHeight) = DockedBand(workArea);
        double width = CollapsedWidth;
        double height = _settings.AutoHideUseHandle
            ? AutoHideHandleHeight(bandHeight)
            : bandHeight;
        double top = _settings.AutoHideUseHandle
            ? bandTop + ((bandHeight - height) / 2)
            : bandTop;
        double left = _settings.DockOnRight ? workArea.Right - width : workArea.Left;

        var cursor = System.Windows.Forms.Cursor.Position;
        var dpi = VisualTreeHelper.GetDpi(this);
        double x = cursor.X / dpi.DpiScaleX;
        double y = cursor.Y / dpi.DpiScaleY;

        return x >= left && x <= left + width && y >= top && y <= top + height;
    }

    // A collapsed sidebar that is not at its screen edge cannot be reached at
    // all - there is no handle to hover, no window to click, and from outside
    // it is indistinguishable from the app having died (2026-08-05, exactly
    // that report). Every route into the collapsed state is meant to end at the
    // edge; this asserts it rather than trusting it, because the cost of being
    // wrong is the whole app becoming unusable until it is restarted.
    //
    // Deliberately checks the invariant itself rather than any particular way
    // of breaking it - the paths that could strand the window are the ones
    // nobody has thought of yet.
    private void VerifyCollapsedPosition(string reason)
    {
        if (!_isDocked || !_settings.IsAutoHidden || _isAutoHideRevealed || _slideInFlight)
        {
            return;
        }

        var workArea = GetCurrentMonitorWorkArea();
        double expected = _settings.DockOnRight ? workArea.Right - Width : workArea.Left;
        if (Math.Abs(Left - expected) < 1)
        {
            return;
        }

        LogAutoHide($"STRANDED at {reason} - expected left={expected:F0}, forcing back");
        PositionToWorkArea();
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
            LogClick("menu opened", null);

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
            LogClick("menu closed", null);
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

    // Asks Windows where the pointer actually is, rather than trusting an
    // enter/leave event to have arrived. Also used by the outside-click watch
    // and by the drag dwell, which has no reliable "left" event of its own.
    //
    // Measured from the visible band, not the raw window rect: the expanded
    // docked window intentionally reaches above the band to the work area's
    // top, clipped away by a region (see PositionToWorkArea) - a cursor in
    // that covered-but-clipped strip is OUTSIDE everything the user can see.
    private bool IsCursorInsideWindow()
    {
        var cursor = System.Windows.Forms.Cursor.Position;
        var dpi = VisualTreeHelper.GetDpi(this);
        double x = cursor.X / dpi.DpiScaleX;
        double y = cursor.Y / dpi.DpiScaleY;
        return x >= Left && x <= Left + Width &&
               y >= Top + RootContent.Margin.Top &&
               y <= Top + Height - RootContent.Margin.Bottom;
    }

    // A mouse gesture that STARTED in this window and is still under way, even
    // though the pointer has since left the window's rectangle. Dragging a
    // scrollbar is the everyday case: a docked sidebar is narrow, so holding
    // the thumb and moving up or down drifts sideways out of the window almost
    // immediately - and both auto-hide close paths read that as leaving.
    // Mouse-leave asks only where the pointer is; click-outside asks whether a
    // button is down while the pointer is outside, which a drag satisfies the
    // whole time. So the sidebar shut itself, the collapse tore the captured
    // ListBox out of the visual tree, and the drag died with it (reported
    // 2026-08-02 against a long search result list, in BOTH modes; pinned was
    // fine because nothing closes there).
    //
    // Capture is the honest question - it is what "a gesture owns the mouse"
    // actually means, and it covers the resize thumb, the favorites splitter
    // and text selection without naming any of them. Paired with a button
    // actually being held so a capture that leaks can't wedge auto-hide open
    // for good; the stuck-capture watchdog, which releases exactly that kind of
    // leak, is a separate mechanism and doesn't depend on this one.
    // Two captures answer it, not one: the OS sizing loop (the docked vertical
    // resize hands the mouse to Windows - see StartDockedVerticalResize) holds
    // the mouse at the Win32 level with WPF never knowing, so Mouse.Captured
    // alone would call that gesture "outside" and let auto-hide close the
    // window out from under an active resize.
    private bool IsPointerGestureFromInsideWindow
        => System.Windows.Forms.Control.MouseButtons != System.Windows.Forms.MouseButtons.None &&
           ((Mouse.Captured is DependencyObject captured && Window.GetWindow(captured) == this) ||
            GetCapture() == new WindowInteropHelper(this).Handle);

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


        // Read on every tick, including the ones that go on to ignore it, so
        // the latch never carries a press across into a later tick that has
        // nothing to do with it - a click that dismissed a menu must not
        // dismiss the sidebar one tick later.
        bool pressedSinceLastTick = NativeMethods.ConsumeMouseButtonPress();

        // Clicking a menu item, or anywhere in the Color Settings window, is a
        // click outside the sidebar's own rectangle - which is exactly what
        // this watch is looking for. Stand down while either is up, or picking
        // an option would dismiss the sidebar mid-action. See
        // IsMenuOrDialogOpen.
        if (IsMenuOrDialogOpen)
        {
            return;
        }

        // Either a button held right now, or one that came and went since the
        // last tick. The second half is what stops a quick click from falling
        // through the gap between polls (see ConsumeMouseButtonPress).
        if (!pressedSinceLastTick &&
            System.Windows.Forms.Control.MouseButtons == System.Windows.Forms.MouseButtons.None)
        {
            return;
        }

        if (IsCursorInsideWindow())
        {
            return;
        }

        // A held button outside the window is what this watch is looking for -
        // unless the button has been held since before it left, which is a drag
        // this window started, not a click somewhere else.
        if (IsPointerGestureFromInsideWindow)
        {
            return;
        }

        LogAutoHide("outside  CLOSING");
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

        UpdateAutoHideHandleOverlay(collapsed: visibility != Visibility.Visible);
        ExplorerTree.Visibility = visibility;
        SearchButton.Visibility = visibility;
        ViewerButton.Visibility = visibility;
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
            // Auto rather than the old fixed 20 - the footer's filter toggles
            // wrap to a second line on a narrow window, and a fixed height
            // would hide it.
            VersionFooterRow.Height = GridLength.Auto;
        }

        UpdateResizeThumbVisibility();
        UpdatePinButtonVisibility();
    }

    // The handle/bar's own colour, which is only ever meant to be seen while the
    // sidebar is collapsed.
    //
    // Deliberately NOT just the inverse of SetExpandedContentVisibility, though
    // that is where it is usually called from: the two come apart at both ends
    // of a reveal. Opening runs the width animation FIRST and puts the content
    // back only when it finishes, so an overlay tied to the content stays up -
    // and stretches - for the whole of the widening. That was invisible while
    // this colour defaulted to the sidebar background, and unmistakable the
    // moment it was set to red: the entire window flashed (2026-08-06).
    //
    // So it is turned off where the reveal STARTS, and it is bounded here as
    // well: never wider than the handle, and held against the docked edge. The
    // ordering fix answers the case we know about; the size is what stops any
    // other path from painting a full window in a colour meant for 6 pixels.
    private void UpdateAutoHideHandleOverlay(bool collapsed)
    {
        AutoHideHandleOverlay.Visibility = collapsed ? Visibility.Visible : Visibility.Collapsed;
        if (!collapsed)
        {
            return;
        }

        AutoHideHandleOverlay.Width = CollapsedWidth;
        // Fully qualified: this Window has a HorizontalAlignment property of its
        // own, so the bare enum name resolves to that instead and will not
        // compile.
        AutoHideHandleOverlay.HorizontalAlignment = _settings.DockOnRight
            ? System.Windows.HorizontalAlignment.Right
            : System.Windows.HorizontalAlignment.Left;
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

        // The two vertical ones follow the same rule for the same reason: docked
        // there is no frame to drag, and collapsed to a sliver there is nothing
        // worth resizing. Floating gets the OS frame back and TopResizeStrip
        // hands it the top edge (see its own note), so these must be gone by
        // then or the two would fight over the same six pixels.
        TopResizeThumb.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        TopResizeThumb.IsHitTestVisible = show;
        BottomResizeThumb.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        BottomResizeThumb.IsHitTestVisible = show;

        // Right-docked, the window grows toward the left (see
        // ResizeThumb_DragDelta), so the grab handle needs to be on the left
        // edge instead of the right one.
        ResizeThumb.HorizontalAlignment = _settings.DockOnRight
            ? System.Windows.HorizontalAlignment.Left
            : System.Windows.HorizontalAlignment.Right;
    }

    // Same button, three jobs by state (see PinButton_Click): floating ->
    // re-dock, docked+pinned -> enter auto-hide, docked+auto-hidden -> pin
    // open. Always clickable now - the old greyed-out-while-pinned state was
    // the reported confusion; the glyph's angle carries the state instead
    // (upright = pinned, lying = auto-hiding, the classic tool-window
    // pushpin language).
    private void UpdatePinButtonVisibility()
    {
        PinButton.IsEnabled = true;

        // It still hides completely along with the rest of the header when the
        // window collapses to the auto-hide sliver - mirroring one of the
        // buttons SetExpandedContentVisibility drives keeps that in step.
        PinButton.Visibility = CloseButton.Visibility;

        bool autoHiding = _isDocked && _settings.IsAutoHidden;
        PinIconRotation.Angle = autoHiding ? 90 : 0;

        // Which edge re-docking/pinning actually snaps to depends on
        // DockOnRight, so the tooltip can't be one fixed string the way the
        // header's other buttons' tooltips are.
        PinButton.ToolTip = !_isDocked
            ? (_settings.DockOnRight ? Strings.ToolTipPinRight : Strings.ToolTipPinLeft)
            : autoHiding ? Strings.ToolTipPinStayOpen : Strings.ToolTipPinAutoHide;
    }

    // The header's icon buttons are fixed-size, and the title between them is
    // the only flexible column - so once it has been squeezed to nothing, a
    // narrower window just pushes the buttons out past the right edge. Stepping
    // their size and spacing down keeps the whole row inside the window all the
    // way to MinExpandedWidth (204), where 7 buttons at 24px + the app icon
    // still fit. Read via DynamicResource by ToggleButtonStyle and the header
    // buttons' own margins.
    private void ApplyHeaderMetrics()
    {
        double width = ActualWidth > 0 ? ActualWidth : Width;
        // Each threshold is the width its own step actually NEEDS, worked out
        // rather than picked: the app icon's column is 34 (a 20px image with
        // 8+6 of margin), and the seven buttons are six at size+gap*2 plus the
        // close button at size+gap+closeGap.
        //
        //   32/2/6 → 34 + 6*36 + 40 = 290
        //   28/1/4 → 34 + 6*30 + 33 = 247
        //   24/0/2 → 34 + 6*24 + 26 = 204   (= MinExpandedWidth exactly)
        //
        // They used to read 250 and 210, which are smaller than what those
        // steps need, so between 250~289 and 210~246 the last column - the
        // CLOSE button - was pushed off the window (reported 2026-08-09, with
        // a screenshot: wider or narrower and it came back). The arithmetic
        // broke when the viewer made a seventh button; the smallest step still
        // fit, which is why only the middle bands showed it.
        //
        // If a button is ever dropped from this strip, every number here comes
        // down by that button's own width (36 / 30 / 24).
        var (size, gap, closeGap) = width switch
        {
            >= 290 => (32.0, 2.0, 6.0),
            >= 247 => (28.0, 1.0, 4.0),
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

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyHeaderMetrics();
        ClampViewerColumnToWindow();
    }

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
        //
        // What the comment above assumed still worked, however, did not: the
        // double-click that restores a window Windows has snapped to maximized
        // is the OS's own CAPTION double-click, and this header is not caption
        // - IsHitTestVisibleInChrome claims the whole of it for us, so that
        // click never reached Windows to begin with (2026-08-05, reported after
        // Win+Up left the window stuck full-screen with only a drag to get it
        // back). Restoring is therefore done here, explicitly.
        //
        // Floating gets the full caption double-click pair now (maximize AND
        // restore, user 2026-08-08): with the viewer panel attached, a
        // fullscreen-sized float is genuinely useful, the header has no
        // column left for a maximize button, and this is the gesture every
        // window already answers to. The mis-operation this block once
        // removed was the DOCK toggle, and that stays gone - a docked
        // double-click still does nothing.
        if (e.ClickCount == 2)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                e.Handled = true;
                return;
            }
            if (!_isDocked)
            {
                WindowState = WindowState.Maximized;
                e.Handled = true;
                return;
            }
        }

        _headerDragStart = e.GetPosition(this);
        (sender as UIElement)?.CaptureMouse();
    }

    // Handled and no further: the header's drag never starts, because
    // _headerDragStart is only set by the grid's own handler above and this
    // stops the event before it gets there. A miss between two buttons now
    // does nothing at all, which is the right answer for a gap - it was never
    // somewhere anyone meant to grab the window by.
    private void HeaderButtonStrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => e.Handled = true;

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

    // The pin is a full pinned/auto-hide toggle now (user call, 2026-07-23,
    // relayed "혼란스럽다" feedback on the old greyed-out-while-pinned pin):
    // docked and pinned, clicking it enters auto-hide; docked and
    // auto-hidden, clicking pins it open again; floating, it re-docks as
    // always. The glyph mirrors the state - upright while pinned, lying on
    // its side while auto-hiding (see UpdatePinButtonVisibility). The app
    // icon used to be the auto-hide entry; that was retired the same day so
    // exactly ONE control owns this state (user call, same feedback thread).
    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isDocked)
        {
            Dock();
        }
        else if (_settings.IsAutoHidden)
        {
            ExitAutoHide();
        }
        else
        {
            EnterAutoHide();
        }

        UpdatePinButtonVisibility();
    }

    private void Undock(bool offsetFromCorner = false)
    {
        if (!_isDocked)
        {
            return;
        }

        // The panel used to fold here (and in Dock) because both transitions
        // rewrite the window's bounds from remembered values. EXPERIMENT
        // (user, 2026-08-08): it rides through instead - the bounds rewrite
        // happens as before, and ApplyViewerSide + the SizeChanged clamp
        // reconcile the columns to whatever window comes out, with the
        // remembered panel width intact.
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
            _revealedByDrag = false;
            _autoHideRehideTimer?.Stop();
            StopDragReveal();
            Topmost = _settings.AlwaysOnTop;
            SetExpandedContentVisibility(Visibility.Visible);
        }
        Width = _floatingWidth ?? ClampExpandedWidth(_settings.ExpandedWidth);

        // The docked band's margin-and-clip arrangement (see PositionToWorkArea)
        // has no meaning on a floating window - but it must be converted to
        // real geometry BEFORE being cleared, not just dropped. The parked
        // window's Top is the work area's top no matter where the band sits;
        // the header the user is mid-dragging (or just double-clicked) is at
        // the BAND's top. Clearing the margin alone made the content leap up
        // to the parked Top - a bottom-band undock threw the window to the
        // screen top, far from the cursor that was dragging it (reported
        // 2026-08-07, "창이 위로 튀고... 커서가 저 멀리"). Committing the band
        // as the window's actual bounds first keeps the header exactly where
        // the cursor is; the corner-nudge and remembered-spot paths below then
        // also start from the band, which is where the user last saw the
        // window.
        // Read both edges out FIRST, then drop the margins, and only then
        // shrink the window onto them. Doing it in the written order
        // (Height -= ... before the margins go) leaves an instant where a
        // band-sized Height still carries the full margins, i.e. a negative
        // content height - see PositionToWorkArea for what that crashes.
        double bandTop = Top + RootContent.Margin.Top;
        double bandHeight = Height - RootContent.Margin.Top - RootContent.Margin.Bottom;
        RootContent.Margin = new Thickness(0);
        Top = bandTop;
        Height = bandHeight;
        _appliedClip = (ClipNone, 0, 0);
        NativeMethods.ClearWindowRegion(new WindowInteropHelper(this).Handle);

        // Floating always carries the panel on the right; a panel that was
        // on the interior-left of a right-docked window swings over here.
        ApplyViewerSide();

        // A session's FIRST float with the panel open used to inherit the
        // tree-only width and the band's full height - the panel crushed to
        // its floor inside a tall skinny strip, which is a poor first
        // picture for exactly the person seeing float mode for the first
        // time. A starter shape instead (user, 2026-08-08): the panel at
        // least 960 wide, the window built around a 16:9-ish panel - a
        // quarter-of-a-4K feel on any monitor. Only when nothing is
        // remembered: a session that has floated before restores its own
        // bounds above, and a panel already set WIDER than 960 keeps that.
        if (_viewerOpen && _floatingWidth is null)
        {
            if (_settings.ViewerWidth < 960)
            {
                _settings.ViewerWidth = Math.Clamp(960, MinViewerWidth, MaxViewerWidth);
            }

            var floatArea = GetCurrentMonitorWorkArea();
            double treeShare = Math.Clamp(_settings.ExpandedWidth, MinTreeSplitWidth, MaxExpandedWidth);
            Width = Math.Min(treeShare + ViewerPanelWidth, floatArea.Width * 0.9);
            // 9/16 of the panel plus the header and the caption strip under
            // the picture - lands the panel itself at roughly 960×540.
            Height = Math.Clamp(ViewerPanelWidth * 9.0 / 16 + 110, MinDockedHeight, floatArea.Height * 0.9);
            ApplyViewerSide();
        }

        // Floors for the native resize borders, which answer to nothing else -
        // without them the frame can be dragged down to a sliver that is
        // almost impossible to find again (reported 2026-08-07, "없어질 뻔").
        // Floating only: docked sizes are all programmatic and the auto-hide
        // sliver (3-8px) and handle must stay allowed - Dock() resets these.
        MinWidth = MinExpandedWidth;
        MinHeight = MinDockedHeight;

        ResizeMode = ResizeMode.CanResize;
        ChromeSettings.CaptionHeight = HeaderHeight;
        ChromeSettings.ResizeBorderThickness = new Thickness(FloatingResizeBorder);
        // Only meaningful now that there is a resize border to expose - see the
        // strip's own comment in XAML.
        TopResizeStrip.Visibility = Visibility.Visible;

        // A window styled entirely through WindowChrome (WindowStyle="None")
        // loses the OS's own drop shadow along with the rest of its native
        // frame - a bare, nonzero GlassFrameThickness (even just a 1px sliver
        // on one edge, never actually rendered as glass on Win10/11 without
        // Mica/Acrylic) is what re-enables DWM's shadow without bringing back
        // any other native chrome.
        //
        // Known cost, accepted 2026-08-07: with a glass frame extended, DWM
        // paints its sheet under the client in the Windows ACCENT color, and a
        // fast native resize shows that sheet in the freshly exposed area
        // until WPF's frame lands (confirmed by the slabs tracking an accent
        // recolor; the app's own colors and DWMWA_CAPTION_COLOR changed
        // nothing). The two attempted cures were worse than the flash: zeroing
        // this killed the shadow AND WindowChromeWorker's non-glass region
        // churn made every resize invalidate the window, and answering
        // WM_ERASEBKGND painted flat color over live content on each of those
        // invalidates - "the whole window blinking its content away"
        // (2026-08-07). SetMicaBackdrop below is the one lever left: with a
        // system backdrop declared, DWM's sheet is theme-colored material
        // instead of accent.
        ChromeSettings.GlassFrameThickness = new Thickness(0, 0, 0, 1);

        var hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.MakeAppWindow(hwnd);

        // Floating only, and only for what it does to resize flashes - see
        // SetMicaBackdrop. Docked never resizes natively, so it withdraws this
        // rather than carry an extra DWM state nothing exercises.
        NativeMethods.SetMicaBackdrop(hwnd, enabled: true);

        // Re-stated after the extended-style rewrite - see
        // MainWindow_SourceInitialized. Floating drops the auto-hide half of
        // the rule, so this settles back to the plain preference.
        ApplyTopmostState("undock");
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
            // Only when that remembered spot is on the monitor the window is
            // actually docked to. Undocking on a second display used to throw
            // the window back to wherever it last floated - typically the
            // primary monitor - which reads as the sidebar running away
            // (2026-08-05 report; predates that day's slide work). Restoring a
            // position is meant to preserve where you left it, and on a
            // different monitor it does the opposite.
            if (_floatingLeft.HasValue && _floatingTop.HasValue &&
                GetCurrentMonitorWorkArea() is { } monitor &&
                _floatingLeft.Value + (Width / 2) >= monitor.Left &&
                _floatingLeft.Value + (Width / 2) <= monitor.Right)
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

        // The floating floors off again - every docked size is programmatic,
        // and the auto-hide sliver/handle sit far below them (see Undock).
        MinWidth = 0;
        MinHeight = 0;

        ResizeMode = ResizeMode.NoResize;
        ChromeSettings.CaptionHeight = 0;
        ChromeSettings.ResizeBorderThickness = new Thickness(0);
        // Deliberately NOT zeroed (it used to be, to drop the floating shadow
        // at the dock). GlassFrameThickness=0 puts WindowChromeWorker into its
        // non-glass path, and that path re-applies a window region
        // (SetWindowRgn - a full-frame invalidate) on EVERY
        // WM_WINDOWPOSCHANGED. resize.log caught it 2026-08-07: one extra
        // FRAMECHANGED|NOSIZE|NOMOVE SetWindowPos after every drag event of a
        // docked vertical resize, while the band's fixed edge was provably
        // still - the forced redraw, landing a composition behind the
        // geometry, is what read as the bottom edge shaking. The glass path
        // (any nonzero thickness) applies no region at all, so the same 1px
        // bottom sliver the floating window uses stays on here. The shadow it
        // brings is mostly against the screen edges the docked window sits
        // flush with.
        ChromeSettings.GlassFrameThickness = new Thickness(0, 0, 0, 1);
        // Nothing to expose while docked, and left showing it would only take
        // the top of the header away from the header's own handlers.
        TopResizeStrip.Visibility = Visibility.Collapsed;

        var hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.MakeToolWindow(hwnd);
        NativeMethods.SetMicaBackdrop(hwnd, enabled: false);
        NativeMethods.SetWindowCornerPreference(hwnd, rounded: false);
        ShowInTaskbar = false;
        ApplyTopmostState("dock");

        // The park (PositionToWorkArea below) writes Width, Top and Height
        // straight through to the real window and only applies the band clip
        // at its end - and DWM can composite a frame in between, which with
        // a short BOTTOM band meant the whole floating window painted once
        // at the TOP of the screen on every dock (user report with
        // screenshots, 2026-08-08; same family as the 08-07 park findings).
        // Applying the TARGET band's region before anything moves closes the
        // gap: the region's rows lie outside the still-floating window, so
        // the in-between frames clip to nothing - the window simply leaves
        // for the edge. The region spans effectively infinite width (see
        // SetBandRegion), so the width change that follows is covered too,
        // and _appliedClip is set to the same tuple ApplyWindowClipRegion
        // will want, making its own pass a no-op rather than a re-cut.
        // Full-height docks (no band margins) skip this and keep their old
        // one-frame arrival, which was never the complaint.
        var dockWorkArea = GetCurrentMonitorWorkArea();
        var (dockBandTop, dockBandHeight) = DockedBand(dockWorkArea);
        double dockTopMargin = Math.Max(0, dockBandTop - dockWorkArea.Top);
        double dockBottomMargin = Math.Max(0, dockWorkArea.Bottom - (dockBandTop + dockBandHeight));
        if (dockTopMargin > 0.5 || dockBottomMargin > 0.5)
        {
            var dockDpi = VisualTreeHelper.GetDpi(this);
            var wanted = (ClipBand,
                (int)Math.Round(dockTopMargin * dockDpi.DpiScaleY),
                (int)Math.Round((dockWorkArea.Height - dockBottomMargin) * dockDpi.DpiScaleY));
            _appliedClip = wanted;
            NativeMethods.SetBandRegion(hwnd, wanted.Item2, wanted.Item3);
        }

        // The tree's remembered width plus the panel riding along (the
        // experiment note in Undock), capped to the work area so docking a
        // wide floating window can't hang the sidebar off the screen edge -
        // the panel column yields the difference (ClampViewerColumnToWindow)
        // and keeps its remembered width for the next roomier window. The
        // split-floor clamp on the tree share is the usual preservation
        // rule.
        double dockTreeWidth = _viewerOpen
            ? Math.Clamp(_settings.ExpandedWidth, MinTreeSplitWidth, MaxExpandedWidth)
            : ClampExpandedWidth(_settings.ExpandedWidth);
        Width = Math.Min(dockTreeWidth + CurrentViewerPanelWidth,
            GetCurrentMonitorWorkArea().Width);
        // Columns BEFORE the park: ApplyViewerSide after PositionToWorkArea
        // meant one more layout pass landing on the already-parked
        // full-height window - a single top-of-screen flash on every dock
        // with the panel open (user report, 2026-08-08). This way the park
        // and its band clip are the last geometry to land.
        ApplyViewerSide();
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
            // The ACTIVE list, not always the favorites one: in bookmark mode
            // FavoritesList is collapsed and has no realized rows at all, so
            // this fell through to the estimate below every time - which is
            // exactly the drift this property exists to avoid, and it showed as
            // the bookmark panel's fit-to-content being short or leaving a gap
            // (2026-08-02).
            if (ActivePanelList.ItemContainerGenerator.ContainerFromIndex(0)
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
        // BOTH panel lists move, not just the favorites one: they share the
        // panel's row, and leaving the bookmark list behind in row 1 while the
        // tree moved into it made the bookmark panel simply vanish under the
        // tree (reported 2026-08-02, the first thing tried after the mode
        // switch shipped).
        Grid.SetRow(FavoritesList, _settings.FavoritesAtBottom ? 3 : 1);
        Grid.SetRow(BookmarkPanelList, _settings.FavoritesAtBottom ? 3 : 1);
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
    // ----- 패널 모드 (즐겨찾기 / 북마크 / 표시 안 함) -------------------------
    //
    // The panel above (or below) the tree shows one of two lists, or nothing.
    // The mode is compared rather than parsed into an enum so a value this
    // build doesn't know - a newer build's, a hand-edited settings file - falls
    // back to what the app has always done instead of refusing to load.
    private bool IsBookmarkPanelMode
        => string.Equals(_settings.SidePanelMode, "bookmarks", StringComparison.OrdinalIgnoreCase);

    private bool IsPanelHiddenMode
        => string.Equals(_settings.SidePanelMode, "none", StringComparison.OrdinalIgnoreCase);

    // Fully qualified: WinForms is referenced here too (Screen, for the work
    // area of the monitor the window is on) and brings its own ListBox.
    private System.Windows.Controls.ListBox ActivePanelList
        => IsBookmarkPanelMode ? BookmarkPanelList : FavoritesList;

    // What the panel's height is sized against. Zero in "none" mode, which is
    // what collapses the row - the same path an empty favorites list already
    // took, so there is only one way for the panel to be absent.
    private int ActivePanelRowCount
        => IsPanelHiddenMode
            ? 0
            : IsBookmarkPanelMode
                ? _bookmarkPanelRows.Count
                : _settings.Favorites.Count;

    private void ApplySidePanelMode()
    {
        bool bookmarks = IsBookmarkPanelMode;
        bool hidden = IsPanelHiddenMode;

        FavoritesList.Visibility = !bookmarks && !hidden ? Visibility.Visible : Visibility.Collapsed;
        BookmarkPanelList.Visibility = bookmarks && !hidden ? Visibility.Visible : Visibility.Collapsed;

        // Built only while it is the one on screen: the rows carry an icon each,
        // and resolving those asks the disk about every bookmarked path - work
        // nobody asked for while the panel is showing something else.
        if (bookmarks)
        {
            RebuildBookmarkPanelRows();
        }

        UpdateFavoritesPanelVisibility();
    }

    private double ComputeFavoritesContentHeight()
    {
        // Both callers run right after ApplyLayoutMetrics has swapped the row
        // padding resource, at which point the existing containers still report
        // their previous height - so force the pending pass through before
        // measuring, or every metric change would size the panel to the metric
        // before it.
        ActivePanelList.UpdateLayout();

        double height = ActivePanelRowCount * FavoriteRowHeight
            + FavoritesListChrome + FavoritesFitBottomPadding;

        // Rounded up because being a fraction of a pixel short is not a
        // fractional problem here - see FavoritesListChrome: item-based
        // scrolling turns any shortfall at all into a whole-row jump. Row
        // heights are fractional at most zoom levels (16 * 20/12 and friends),
        // so this is a real risk rather than a theoretical one. Costs at most
        // one pixel of extra gap.
        return Math.Ceiling(height);
    }

    // A row that exists can be measured; one that doesn't has to be guessed at,
    // and the guess is what FavoriteRowHeight falls back to. Both are wanted -
    // but only the measurement should be allowed to stand.
    private bool HasRealizedFavoriteRow
        => ActivePanelList.ItemContainerGenerator.ContainerFromIndex(0) is ListBoxItem { ActualHeight: > 0 };

    private bool _favoritesHeightRecheckPending;
    private int _favoritesHeightRechecksLeft;

    private void UpdateFavoritesPanelVisibility()
    {
        bool hasFavorites = ActivePanelRowCount > 0;

        // A floor for the splitter, so dragging it all the way up can't squeeze
        // the panel down to a sliver (reported 2026-08-02: the divider ends up
        // riding into the header). One row is the smallest height at which the
        // panel is still a panel. Zero whenever it is meant to be absent -
        // otherwise the row could not collapse at all.
        FavoritesRowDef.MinHeight = hasFavorites
            ? Math.Ceiling(FavoriteRowHeight + FavoritesListChrome)
            : 0;

        if (hasFavorites)
        {
            bool measured = HasRealizedFavoriteRow;
            FavoritesRowDef.Height = new GridLength(Math.Min(ComputeFavoritesContentHeight(), _settings.FavoritesPanelHeight));

            // Sized off the estimate rather than a real row - startup, or the
            // very first favorite. The estimate runs short, so the panel came
            // up one row too small and clipped the last favorite; it survived
            // as far as it did because it only shows on the launch after
            // something forced a restart. Ask again once there is a row to ask.
            if (!measured)
            {
                QueueFavoritesHeightRecheck();
            }
        }
        else
        {
            FavoritesRowDef.Height = new GridLength(0);
        }
        FavoritesSplitterRow.Height = hasFavorites ? GridLength.Auto : new GridLength(0);
    }

    // Re-runs once the list has had a layout pass, which is when its containers
    // exist. Bounded rather than "until it works": if rows never realize (a
    // panel already at zero height, say) this stops instead of re-queueing
    // itself forever - a sizing correction is not worth a spin.
    private void QueueFavoritesHeightRecheck()
    {
        if (_favoritesHeightRecheckPending)
        {
            return;
        }

        _favoritesHeightRecheckPending = true;
        _favoritesHeightRechecksLeft = 5;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(RecheckFavoritesHeight));
    }

    private void RecheckFavoritesHeight()
    {
        _favoritesHeightRecheckPending = false;

        if (_settings.Favorites.Count == 0)
        {
            return;
        }

        if (!HasRealizedFavoriteRow)
        {
            if (--_favoritesHeightRechecksLeft <= 0)
            {
                return;
            }

            _favoritesHeightRecheckPending = true;
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(RecheckFavoritesHeight));
            return;
        }

        FavoritesRowDef.Height =
            new GridLength(Math.Min(ComputeFavoritesContentHeight(), _settings.FavoritesPanelHeight));
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
        if (ActivePanelRowCount == 0)
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
        ActivePanelList.ScrollIntoView(ActivePanelList.Items[0]);
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

        var entry = new FavoriteEntry { DisplayName = item.Name, Path = item.FullPath };
        bool firstFavorite = _settings.Favorites.Count == 0;
        _settings.Favorites.Add(entry);

        // The panel might not have existed at all yet (0 -> 1 favorites), so
        // the row/splitter need their initial reveal here - FitFavoritesPanel
        // alone only ever sets FavoritesRowDef's height, not FavoritesSplitterRow.
        UpdateFavoritesPanelVisibility();

        // Adding does NOT auto-grow the panel anymore (2026-07-24): every
        // growth shifted the entire tree under the cursor, and adding several
        // favorites in a row became misclicks on rows that had just moved.
        // The new entry slides in at the bottom instead - scrolled into view,
        // older entries rolling up out of sight - and the panel keeps
        // whatever height it has (the divider double-click still fits it on
        // demand, as does removing). Only the very first favorite sizes the
        // panel: it just appeared, so there is no height to disturb yet.
        if (firstFavorite)
        {
            FitFavoritesPanel();
        }
        else
        {
            // One dispatcher hop so the ListBox has generated the new row
            // before being asked to bring it on screen.
            Dispatcher.BeginInvoke(() => FavoritesList.ScrollIntoView(entry),
                System.Windows.Threading.DispatcherPriority.Loaded);
        }
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

    // Press only ARMS the click now - navigation happens on release (see
    // FavoritesList_PreviewMouseLeftButtonUp), because a press that turns into
    // a drag is a reorder, not a click, and navigating on the way into a drag
    // would jump the tree every time the list is rearranged.
    private void FavoriteListBoxItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: FavoriteEntry entry })
        {
            _favoriteDragEntry = entry;
            _favoriteDragStart = e.GetPosition(FavoritesList);
        }
    }

    // Single click navigates (requirement a). The double-click workaround this
    // used to require is gone: capping every folder at
    // FileSystemItem.DisplayCap keeps the tree light enough that the reveal
    // walk realizes the target's container reliably on the first click, even
    // below a huge folder (NavigateToPath re-caps overflow first).
    private void NavigateToFavorite(FavoriteEntry entry)
    {
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
            //
            // "Nothing left to do" is about the WALK, not the scroll: the row
            // being selected and expanded says nothing about where it is on
            // screen. Wheel-scrolling away from a favorite leaves exactly this
            // state, so returning outright swallowed the click completely -
            // the one thing it was for (put that folder back at the top) did
            // nothing at all, however far the view had drifted. Re-pin instead,
            // which is the cheap half of the walk and the half that was
            // actually missing; if the row scrolled far enough for its
            // container to be virtualized away, fall through and let the full
            // walk realize it again.
            if (ExplorerTree.SelectedItem is FileSystemItem { IsExpanded: true } selected &&
                string.Equals(selected.FullPath.TrimEnd('\\'), entry.Path.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) &&
                FindRealizedContainer(selected) is { } selectedContainer)
            {
                PinRowToTop(selectedContainer);
                return;
            }

            // The walk is asynchronous (RevealChainStep defers via the
            // dispatcher), so the "navigating from a favorite" guard is set and
            // cleared inside NavigateToPath / the walk itself, not around this
            // synchronous call - clearing it here would drop the guard while the
            // walk is still running and let an intermediate selection change
            // clear the favorite we just clicked.
            NavigateToPath(entry.Path, source: "favorite");
        }
    }

    // ----- 즐겨찾기 드래그 재정렬 ------------------------------------------
    //
    // No drag handle: the row itself is the target, with click and drag split
    // by movement (the same rule the tree's drag-out uses). A handle would add
    // permanent visual noise to a narrow row and make the same action a
    // smaller target; "move up/down" menu items were considered and dropped as
    // half a feature. Order is the settings list's own order, so nothing about
    // the saved format changes.
    //
    // Mouse capture rather than DragDrop.DoDragDrop: this never leaves the
    // list, and the OLE drag loop is the one that has cost this app real bugs
    // (stuck captures, phantom self-drops). Capture is released on button-up
    // AND on LostMouseCapture, with the app's own capture watchdog as the
    // backstop underneath both.

    private System.Windows.Point? _favoriteDragStart;
    private FavoriteEntry? _favoriteDragEntry;
    private bool _favoriteDragActive;
    private int _favoriteDropIndex = -1;

    private void FavoritesList_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed ||
            _favoriteDragEntry is null ||
            _favoriteDragStart is not { } start)
        {
            return;
        }

        var current = e.GetPosition(FavoritesList);
        if (!_favoriteDragActive)
        {
            // Below the system's own drag threshold this is still a click -
            // a few pixels of travel while pressing is normal, especially on a
            // high-DPI screen with a fast mouse.
            if (Math.Abs(current.X - start.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(current.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }
            if (_settings.Favorites.Count < 2)
            {
                return;
            }

            _favoriteDragActive = true;
            FavoritesList.CaptureMouse();
        }

        // Dragged off the list entirely (onto the tree below, most often):
        // that cancels, it does not drop at the nearest end. The cursor being
        // past the last row means "the end of the list" only while it is still
        // over the list - outside it, the gesture has left, and dumping the
        // row at the bottom on release is a move nobody asked for.
        bool overList =
            current.X >= 0 && current.X <= FavoritesList.ActualWidth &&
            current.Y >= 0 && current.Y <= FavoritesList.ActualHeight;

        UpdateFavoriteDropIndicator(overList ? ComputeFavoriteDropIndex(current) : -1);

        // The ListBox must not see this move. It reads mouse movement while it
        // holds capture as its own drag-selection - and the capture here is
        // OURS, taken for the reorder - so dragging a row down past the list
        // was quietly selecting whichever item was nearest the cursor (the
        // last one). Handling the preview event suppresses the bubbling
        // MouseMove entirely, which is what Selector acts on.
        e.Handled = true;
    }

    private void FavoritesList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var entry = _favoriteDragEntry;
        bool dragged = _favoriteDragActive;
        int dropIndex = _favoriteDropIndex;

        EndFavoriteDrag();

        if (entry is null)
        {
            return;
        }

        // A press that never became a drag is the click it always was.
        if (dragged)
        {
            CommitFavoriteReorder(entry, dropIndex);
        }
        else
        {
            NavigateToFavorite(entry);
        }
    }

    // Capture can also be taken away (another window activating, a system
    // dialog): the drag ends where it stands rather than reordering on a
    // release that never came.
    private void FavoritesList_LostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e) => EndFavoriteDrag();

    private void EndFavoriteDrag()
    {
        bool wasDragging = _favoriteDragActive;

        // The selection belongs to the row that was picked up, whatever the
        // list may have decided while the cursor was travelling over it.
        if (wasDragging && _favoriteDragEntry is { } dragged)
        {
            FavoritesList.SelectedItem = dragged;
        }

        _favoriteDragActive = false;
        _favoriteDragEntry = null;
        _favoriteDragStart = null;
        _favoriteDropIndex = -1;
        FavoriteDropIndicator.Visibility = Visibility.Collapsed;

        // Checked first: releasing capture raises LostMouseCapture, which
        // lands right back here.
        if (wasDragging && FavoritesList.IsMouseCaptured)
        {
            FavoritesList.ReleaseMouseCapture();
        }
    }

    // Where the dragged row would be inserted: the first row whose middle the
    // cursor is above, or the end of the list. Using the midpoint is what
    // makes the indicator only move as a row boundary is actually crossed, so
    // jitter within one row never changes anything.
    private int ComputeFavoriteDropIndex(System.Windows.Point pointInList)
    {
        int count = _settings.Favorites.Count;
        for (int i = 0; i < count; i++)
        {
            if (FavoritesList.ItemContainerGenerator.ContainerFromIndex(i) is not ListBoxItem container)
            {
                continue;
            }

            double top = container.TransformToAncestor(FavoritesList).Transform(new System.Windows.Point(0, 0)).Y;
            if (pointInList.Y < top + container.ActualHeight / 2)
            {
                return i;
            }
        }
        return count;
    }

    private void UpdateFavoriteDropIndicator(int dropIndex)
    {
        if (dropIndex == _favoriteDropIndex)
        {
            return;
        }
        _favoriteDropIndex = dropIndex;

        // -1 is "nowhere to drop" (the cursor has left the list): no line, and
        // CommitFavoriteReorder leaves the order alone when it sees it.
        if (dropIndex < 0 || FavoriteDropIndicatorOffset(dropIndex) is not { } offset)
        {
            FavoriteDropIndicator.Visibility = Visibility.Collapsed;
            return;
        }

        // Centred on the boundary rather than hanging below it.
        FavoriteDropIndicator.Margin = new Thickness(0, Math.Max(0, offset - 1), 0, 0);
        FavoriteDropIndicator.Visibility = Visibility.Visible;
    }

    private double? FavoriteDropIndicatorOffset(int dropIndex)
    {
        int count = _settings.Favorites.Count;
        if (count == 0)
        {
            return 0;
        }

        if (dropIndex < count)
        {
            return FavoritesList.ItemContainerGenerator.ContainerFromIndex(dropIndex) is ListBoxItem container
                ? container.TransformToAncestor(FavoritesList).Transform(new System.Windows.Point(0, 0)).Y
                : null;
        }

        return FavoritesList.ItemContainerGenerator.ContainerFromIndex(count - 1) is ListBoxItem last
            ? last.TransformToAncestor(FavoritesList).Transform(new System.Windows.Point(0, 0)).Y + last.ActualHeight
            : null;
    }

    private void CommitFavoriteReorder(FavoriteEntry entry, int dropIndex)
    {
        var favorites = _settings.Favorites;
        int from = favorites.IndexOf(entry);
        if (from < 0 || dropIndex < 0)
        {
            return;
        }

        // The insertion index was measured with the dragged row still in
        // place, so removing it first shifts everything after it up one.
        int to = dropIndex > from ? dropIndex - 1 : dropIndex;
        if (to == from)
        {
            return;
        }

        favorites.RemoveAt(from);
        favorites.Insert(to, entry);

        // The list is a plain List<T> with no change notification of its own
        // (same as everywhere else it's bound), so the view is told outright.
        FavoritesList.Items.Refresh();
        FavoritesList.SelectedItem = entry;

        // Saved immediately, same reasoning as bookmarks: a deliberate
        // arrangement whose whole point is that it persists.
        _settingsService.Save(_settings);
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
    // current behavior unless it opts out.
    //
    // Files used to be an exception: they pinned their PARENT folder and were
    // selected below it, so a search hit landed in its folder's context. That
    // is gone (2026-08-02, user's call) - every jump now puts its own target at
    // the top, whatever it is. The context it was buying stopped being worth a
    // rule of its own once every row gained a full-path tooltip, and the
    // exception cost more than it paid in a folder of hundreds, where the
    // parent at the top left the file itself far below the bottom edge.
    private void NavigateToPath(string targetPath, bool pinToTop = true, string source = "nav")
    {
        // A jump moving the tree a long way is CORRECT here - that is what a
        // bookmark/favorite click is for. The scroll-jump instrument has no way
        // to tell that apart from the tree moving on its own, so it is told
        // (see scrolljump.log's nav= field); without this every deliberate jump
        // would read as a suspect.
        NoteNavigationForScrollWatch(source, targetPath);

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

        // Before the walk, not during it: a hidden folder on the way down has
        // no row at all, so the walk would stop at its parent and the jump
        // would look like it had gone to the wrong place.
        RevealHiddenFoldersOnPathTo(targetPath);

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

        RevealChain(chain, myToken, pinToTop);
    }

    // Walks the loaded tree returning every fully-revealed ("더 보기" expanded)
    // folder to its capped state. Recaps a folder before recursing into it, so
    // a folder holding thousands of revealed rows is trimmed to ~25 first and
    // the recursion only ever visits those - it never iterates the full list.
    // Both loops walk a SNAPSHOT, and that is not defensive habit - this walk
    // crashed the app (2026-08-02, exit.log "UNHANDLED (ui thread):
    // InvalidOperationException: Collection was modified" under JumpToBookmark
    // -> NavigateToPath -> here). The re-entrancy is entirely single-threaded:
    //
    //   RecollapseOverflow() drops the overflow rows -> if the SELECTED row was
    //   one of them, WPF moves TreeView.SelectedItem there and then ->
    //   ExplorerTree_SelectedItemChanged runs SYNCHRONOUSLY ->
    //   ReHideFoldersLeftBehind does parent.Children.Remove(...) ->
    //   the collection this method is mid-foreach over is now modified.
    //
    // It needs hidden folders to bite, which is why it took until someone had
    // 65 of them: NavigateToPath calls RevealHiddenFoldersOnPathTo a few lines
    // earlier, so TemporarilyVisiblePaths is freshly non-empty and the re-hide
    // actually removes rows instead of returning at its first line.
    //
    // Recapping an item that has since left the tree is harmless (it only
    // touches its own Children), so a snapshot loses nothing.
    private void RecapAllOverflow()
    {
        foreach (var root in _roots.ToList())
        {
            RecapOverflowRecursive(root);
        }
    }

    private static void RecapOverflowRecursive(FileSystemItem item)
    {
        item.RecollapseOverflow();
        foreach (var child in item.Children.ToList())
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
    private void RevealChain(List<FileSystemItem> chain, int token, bool pinToTop = true)
    {
        // Overflow re-capping already ran up-front in NavigateToPath, so the
        // walk starts over a light tree; this only expands the path down to
        // the target and doesn't touch any other folder's expanded state.
        RevealChainStep(chain, 0, ExplorerTree, token, pinToTop: pinToTop);
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
    // settled says the walk has so far found every container it needed right
    // away. Once any step has had to yield and retry, the tree is still
    // realizing rows and nothing measured at the end of the walk can be
    // trusted yet - see FinishReveal, which is what actually reads it.
    private void RevealChainStep(List<FileSystemItem> chain, int index, ItemsControl container, int token, int attempt = 0, bool pinToTop = true, bool settled = true)
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
                new Action(() => RevealChainStep(chain, index, container, token, attempt + 1, pinToTop, settled: false)));
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
            FinishReveal(treeViewItem, token, pinToTop, settled);
        }
        else
        {
            RevealChainStep(chain, index + 1, treeViewItem, token, pinToTop: pinToTop, settled: settled);
        }
    }

    // `selected` is the row that gets selected, focused and - when pinToTop is
    // set - pinned to the top of the viewport. One row for all three: the
    // separate anchor this used to take, so a file could be selected while its
    // parent folder held the top, is gone (see NavigateToPath).
    private void FinishReveal(TreeViewItem selected, int token, bool pinToTop = true, bool settled = true)
    {
        // Still guarded while this fires: setting IsSelected raises
        // SelectedItemChanged synchronously, and the guard keeps that from
        // re-syncing (and possibly clearing) the favorites list. Cleared right
        // after, so subsequent user-driven selections sync normally again.
        selected.IsSelected = true;

        // Only when nothing is about to be pinned. A pin computes its own final
        // offset, and this call scrolling somewhere else first is exactly what
        // drew the intermediate frame reported as "이동할 때 화면이 한 번 번쩍임"
        // (2026-07-28): the two scrolls used to be a whole rendered frame apart.
        if (!pinToTop)
        {
            selected.BringIntoView();
        }

        // Taking focus dismisses whatever menu the navigation was started from
        // - which is how a bookmark picked out of the 북마크 목록 submenu shut
        // that list every time, defeating the point of a list you check several
        // entries against. Selection plus BringIntoView already shows where the
        // tree went; focus can wait until the menu is out of the way. Stated as
        // a rule rather than a flag for that one caller: nothing should pull
        // focus out from under an open menu.
        if (!IsMenuOrDialogOpen)
        {
            selected.Focus();
        }

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
        // somewhere on screen. Done HERE, in the pass that revealed the chain,
        // rather than deferred to the next one - a deferred scroll is a second
        // scroll, and the frame drawn between the two is the flash. The
        // dispatcher pass below stays as a correction, not as the scroll.
        //
        // Unless the walk had to wait on a container somewhere: then rows above
        // the target are still being realized and this measurement would be
        // wrong, so pinning here scrolls somewhere arbitrary and the correction
        // below has to haul the view back - which is what F5 did, since it
        // rebuilds the whole tree before restoring the selection through this
        // same machinery (reported 2026-08-02, "F5만 한번 다른 데 갔다가 오네요").
        // Skipping straight to the deferred pin there is the pre-existing
        // behaviour, and it was never the flashing case.
        if (settled)
        {
            ExplorerTree.UpdateLayout();
            PinRowToTop(selected);
        }

        // The walk's last step can leave layout still settling (the target's own
        // expand loads its children), and anything that lands after the pin
        // moves the row out from under it. Re-running the same routine settles
        // that; it scrolls only if the target has actually moved, so in the
        // ordinary case this pass does nothing at all and nothing is redrawn.
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
        {
            if (token != _navigationToken)
            {
                return;
            }
            PinRowToTop(selected);
        }));
    }

    // Shared by the end of a reveal walk and by re-clicking a favorite that is
    // already selected (which skips the walk entirely).
    private void PinRowToTop(TreeViewItem anchor)
    {
        if (FindTreeScrollViewer() is { } scrollViewer)
        {
            PinRowToTop(anchor, scrollViewer);
        }
    }

    // Scrolled to a computed offset rather than by asking BringIntoView for a
    // viewport-tall rectangle. Two reasons: BringIntoView is a request that WPF
    // may satisfy on a later dispatcher pass (another frame, another flash),
    // and it only ever scrolls as far as it has to - so a second call for a row
    // already at the top has to be a no-op, which is what lets the correction
    // pass above be free. The offset itself is exact because the tree scrolls
    // in PIXELS (VirtualizingPanel.ScrollUnit="Pixel", see MainWindow.xaml);
    // under the default Item unit this arithmetic would be meaningless.
    private void PinRowToTop(TreeViewItem anchor, ScrollViewer scrollViewer)
    {
        if (ContentTopOf(anchor, scrollViewer) is not { } top)
        {
            return;
        }

        // Near the end of the tree there is nothing left below the row to
        // scroll into view, so ScrollToVerticalOffset clamps and the row stops
        // partway down - the jump looks like it landed somewhere arbitrary.
        // Make the room instead of giving up on the pin.
        double shortfall = top - scrollViewer.ScrollableHeight;
        if (shortfall > 0.5)
        {
            SetBottomGap(_bottomGapSize + shortfall, scrollViewer);
        }

        if (Math.Abs(scrollViewer.VerticalOffset - top) > 0.5)
        {
            scrollViewer.ScrollToVerticalOffset(top);
        }
    }

    // ----- 트리 끝의 아래 여백 ----------------------------------------------
    //
    // Extra scrollable room past the last row, so a row near the end can still
    // be pinned to the top. It is a bottom MARGIN on the last root's container,
    // which lands after that root's entire rendered block - i.e. at the very
    // bottom of the content - and grows the extent without touching the
    // viewport. Measured 2026-08-02 in a standalone WPF spike: with every row
    // realized, a 300px margin bought exactly 300px of extra range and removing
    // it returned the range to the byte.
    //
    // The same spike is why this hangs off the ROOT and not off some row deeper
    // down. With 200 rows in one virtualizing panel the 300px margin bought
    // 1034px instead: VirtualizingStackPanel estimates the height of everything
    // it hasn't realized from the average of what it has, and one 316px-tall
    // item skews that average. The root level is the one place immune to it -
    // there are only ever a handful of drives and the tree caches 1000 items,
    // so every root is realized and the extent there is a real sum, not an
    // estimate.
    //
    // Only while a jump needs it (the user's call, 2026-07-30, over always
    // keeping a gap the way a code editor does): a drive tree is not a file,
    // and permanent empty space at the end is a permanent cost for something
    // that matters at the moment of a jump. The gap leaves on its own - see
    // TreeScrollViewer_ScrollChanged.
    private TreeViewItem? _bottomGapHost;

    private double _bottomGapSize;

    private void SetBottomGap(double gap, ScrollViewer scrollViewer)
    {
        gap = Math.Max(0, gap);

        // Same file as the scroll-jump watch so the two line up by timestamp -
        // a jump with a "gap cleared" beside it points straight at this code.
        if (Math.Abs(gap - _bottomGapSize) > 0.5)
        {
            LogScrollLine(
                $"gap    {(gap > 0.5 ? $"set {gap:F0}" : "cleared")}  (was {_bottomGapSize:F0})  " +
                $"offset {scrollViewer.VerticalOffset:F0}  extent {scrollViewer.ExtentHeight:F0}");
        }

        // Whatever carried the last gap gives it up first: the last root can
        // change under us (a drive appearing, a refresh regenerating
        // containers), and a margin left behind on a row that is no longer last
        // is a gap in the MIDDLE of the tree. ClearValue rather than a zero
        // Thickness so the item style's own margin, if it ever gains one, comes
        // back instead of being overwritten with a hardcoded default.
        if (_bottomGapHost is { } previous)
        {
            previous.ClearValue(MarginProperty);
            _bottomGapHost = null;
        }

        _bottomGapSize = 0;

        if (gap > 0.5 && LastRootContainer() is { } host)
        {
            host.Margin = new Thickness(0, 0, 0, gap);
            _bottomGapHost = host;
            _bottomGapSize = gap;

            scrollViewer.ScrollChanged -= TreeScrollViewer_ScrollChanged;
            scrollViewer.ScrollChanged += TreeScrollViewer_ScrollChanged;
        }

        // The new range has to exist before the caller scrolls into it.
        ExplorerTree.UpdateLayout();
    }

    private TreeViewItem? LastRootContainer()
        => _roots.Count == 0
            ? null
            : ExplorerTree.ItemContainerGenerator.ContainerFromItem(_roots[^1]) as TreeViewItem;

    // The gap is taken back the moment it is no longer on screen - scrolling up
    // past it, in practice. Removing it then is invisible: the space being
    // reclaimed is below the viewport, so nothing on screen moves, and the
    // offset stays inside the smaller range by construction. Waiting for that
    // instead of removing it on a timer or on the next click is what keeps the
    // gap from ever vanishing under the user while they are looking at it.
    private void TreeScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        // The tree was rebuilt and took the gap's host with it - the margin is
        // already gone with the container, so just forget it.
        if (_bottomGapHost is null || !ReferenceEquals(_bottomGapHost, LastRootContainer()))
        {
            _bottomGapHost = null;
            _bottomGapSize = 0;
            scrollViewer.ScrollChanged -= TreeScrollViewer_ScrollChanged;
            return;
        }

        double gapTop = scrollViewer.ExtentHeight - _bottomGapSize;
        if (scrollViewer.VerticalOffset + scrollViewer.ViewportHeight <= gapTop + 0.5)
        {
            scrollViewer.ScrollChanged -= TreeScrollViewer_ScrollChanged;
            SetBottomGap(0, scrollViewer);
        }
    }

    // Where a realized row sits inside the scrolled content: its position
    // relative to the viewport, plus how far the viewport has already been
    // scrolled. Null when the row isn't in the ScrollViewer's visual tree
    // (virtualized away between the walk and this call).
    //
    // Measured against the ScrollViewer itself, which matches the viewport's
    // top edge because the tree's padding is 4,0,4,4 - no top inset. A top
    // padding would have to be subtracted here.
    private static double? ContentTopOf(TreeViewItem row, ScrollViewer scrollViewer)
    {
        if (!row.IsVisible)
        {
            return null;
        }

        try
        {
            double fromViewportTop = row.TransformToAncestor(scrollViewer)
                .Transform(default(System.Windows.Point)).Y;
            return scrollViewer.VerticalOffset + fromViewportTop;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    // The realized container for an item, walked down from the root one level
    // at a time - null the moment any level along the way is virtualized away,
    // which is the caller's cue that only a full reveal walk can get there.
    // Deliberately does NOT try to force realization (that's RevealChainStep's
    // job, retries and all - see the favorites-navigation history).
    private TreeViewItem? FindRealizedContainer(FileSystemItem item)
    {
        var chain = new List<FileSystemItem>();
        for (var current = item; current is not null; current = current.Parent)
        {
            chain.Insert(0, current);
        }

        ItemsControl container = ExplorerTree;
        foreach (var step in chain)
        {
            if (container.ItemContainerGenerator.ContainerFromItem(step) is not TreeViewItem next)
            {
                return null;
            }
            container = next;
        }

        return container as TreeViewItem;
    }

    private ScrollViewer? FindTreeScrollViewer()
    {
        ExplorerTree.ApplyTemplate();
        return ExplorerTree.Template.FindName("PART_TreeScrollViewer", ExplorerTree) as ScrollViewer;
    }

    // ----- 스크롤 점프 계측기 (Debug 전용) ------------------------------------
    //
    // 2026-08-02 신고: 폴더 하나를 펼쳤을 뿐인데 트리가 최상단(C:)으로 확
    // 뛰어버림. 재현이 안 되고, 기존 로그는 스크롤 위치를 한 줄도 남기지 않아
    // 판정할 근거 자체가 없었음 (click.log는 클릭과 토글만 안다).
    //
    // 원인 후보를 하나씩 막는 대신 현상 자체를 잡는다: ScrollViewer에 상시
    // 붙어 있다가, 한 번의 변화로 화면 절반 이상 움직였거나 전체 높이가 한
    // 화면 넘게 흔들린 순간만 남긴다. 어느 경로가 범인이든 - 가상화 높이 추정
    // 붕괴, 트리 끝 여백 회수, 아직 생각하지 못한 것 - 전부 여기를 지나간다.
    // 평소 휠 스크롤(노치당 ~48px)은 문턱을 못 넘으므로 조용하다.
    //
    // 다음에 한 번만 재현되면 아래 규칙으로 판정이 끝난다:
    //  · extent가 viewport 근처까지 폭삭 줄었다 → 가상화 높이 추정이 무너진 것
    //    (2026-08-02 실측: 200행 패널에서 300px 여백이 1034px로 계산됐음)
    //  · extent는 그대로인데 offset만 0         → 오프셋을 리셋한 범인이 따로 있음
    //  · 같은 시각에 gap 줄이 있다              → 트리 끝 여백 회수(v1.4.0 신규 코드)
    //  · sinceGesture가 "-"                     → 직전 클릭과 무관한 경로에서 온 것
    private bool _scrollWatchAttached;

    // 직전 트리 클릭이 무엇이었는지 - 점프 줄에 붙여, 그 펼치기가 방아쇠였는지
    // 아니면 아무 입력 없이 혼자 뛴 것인지 한 줄에서 읽히게 한다.
    private string _lastTreePressLabel = "-";

    // 직전 "의도된 이동"(북마크·즐겨찾기·검색 결과·복원)이 무엇이었는지.
    // 이것이 붙어 있는 점프는 정상이고, `nav=-`인 점프만 설명이 필요하다.
    private string _lastNavLabel = "-";
    private long _lastNavTicks = long.MinValue / 2;

    [System.Diagnostics.Conditional("DEBUG")]
    private void NoteNavigationForScrollWatch(string source, string targetPath)
    {
        _lastNavLabel = $"{source}:{targetPath}";
        _lastNavTicks = Environment.TickCount64;
    }

    [System.Diagnostics.Conditional("DEBUG")]
    private void AttachScrollJumpWatch()
    {
        if (_scrollWatchAttached || FindTreeScrollViewer() is not { } scrollViewer)
        {
            return;
        }

        _scrollWatchAttached = true;

        // 여백 회수용 TreeScrollViewer_ScrollChanged와는 다른 핸들러라 서로
        // 붙었다 떨어졌다 하는 그쪽 구독에 영향을 주지 않는다. 이쪽은 앱이
        // 사는 동안 한 번 붙고 끝.
        scrollViewer.ScrollChanged += ScrollJumpWatch_ScrollChanged;
    }

    private void ScrollJumpWatch_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        double viewport = e.ViewportHeight;
        bool bigJump = Math.Abs(e.VerticalChange) > Math.Max(viewport / 2, 200);
        bool bigExtentSwing = Math.Abs(e.ExtentHeightChange) > Math.Max(viewport, 200);
        if (!bigJump && !bigExtentSwing)
        {
            return;
        }

        double offsetBefore = e.VerticalOffset - e.VerticalChange;
        double extentBefore = e.ExtentHeight - e.ExtentHeightChange;
        long sinceGesture = Environment.TickCount64 - _lastTreeUserInputTicks;

        LogScrollLine(
            $"{(bigJump ? "JUMP  " : "EXTENT")}  " +
            $"offset {offsetBefore:F0} -> {e.VerticalOffset:F0} ({e.VerticalChange:+0;-0;0})  " +
            $"extent {extentBefore:F0} -> {e.ExtentHeight:F0} ({e.ExtentHeightChange:+0;-0;0})  " +
            $"viewport {viewport:F0}  " +
            $"toZero={(e.VerticalOffset <= 0.5 ? "yes" : "no")}  " +
            $"wasAtEnd={(offsetBefore + viewport >= extentBefore - 1 ? "yes" : "no")}  " +
            $"sinceGesture={(sinceGesture is < 0 or > 60000 ? "-" : sinceGesture + "ms")}  " +
            $"nav={(Environment.TickCount64 - _lastNavTicks is < 0 or > 3000 ? "-" : _lastNavLabel)}  " +
            $"lastPress={_lastTreePressLabel}  " +
            $"gap={_bottomGapSize:F0}  " +
            $"selected={(ExplorerTree.SelectedItem as FileSystemItem)?.FullPath ?? "-"}");
    }

    [System.Diagnostics.Conditional("DEBUG")]
    private static void LogScrollLine(string line)
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Edgetree");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, "scrolljump.log"),
                $"{DateTime.Now:HH:mm:ss.fff}  {line}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    // Ctrl+wheel: accelerated scrolling, about five times the ordinary wheel
    // step (~240px per notch vs the default ~48). Deep trees mean a LOT of
    // plain-wheel notches ("스크롤 피곤도", 2026-07-24), and Ctrl+wheel was
    // unassigned - the font zoom lives on Ctrl +/- only. A constant factor
    // rather than progressive velocity: predictable beats clever for a
    // positioning gesture. The offset is in pixels here (the root panel
    // scrolls with ScrollUnit=Pixel).
    private void ExplorerTree_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            return;
        }

        if (FindTreeScrollViewer() is { } scrollViewer)
        {
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta * 2.0);
            e.Handled = true;
        }
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
    {
        // The outcome side of the click instrument (see LogTreeToggle): a press
        // with no matching line here asked for a toggle that never happened.
        if (e.OriginalSource is TreeViewItem { DataContext: FileSystemItem item })
        {
            LogTreeToggle(item, e.RoutedEvent == TreeViewItem.ExpandedEvent);
        }

        UpdateCollapseAllButtonState();
    }

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

    // Which control started the current close, for the exit log - see
    // MainWindow_Closing.
    private string? _closeReason;

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _closeReason = "header close button";
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

        // MenuItems declared in a resource dictionary don't get auto-generated
        // code-behind fields, so they have to be found at runtime. This used to
        // match them POSITIONALLY - one long list pattern over the menu's items
        // - and adding a single row to the XAML made the whole pattern fail,
        // which left every toggle unchecked and the steppers dead: the menu
        // simply "stopped working" (2026-08-02, adding 숨긴 폴더). That is the
        // second time a positional menu pattern has broken this way; the row
        // context menu was converted after the first (v1.3.4, where it SHIPPED
        // broken). Rows are addressed by name now, and a new row can be dropped
        // anywhere in the XAML without touching this.
        //
        // AutomationId rather than Tag: Tag is already spoken for in this menu -
        // the MenuItem template reads it to reserve the check column
        // ("reserve-check-column"), so identity had to live somewhere else.
        if (sender is ContextMenu menu &&
            FindMenuItem(menu, "collapseAllExpanded") is { } collapseAllExpanded &&
            FindMenuItem(menu, "generalSettings") is { } generalSettings &&
            FindMenuItem(menu, "bookmarkList") is { } bookmarkList &&
            FindMenuItem(menu, "fontSizeRow") is { } fontSizeRow &&
            FindMenuItem(menu, "maxItemsRow") is { } maxItemsRow &&
            FindMenuItem(menu, "tabSpacingRow") is { } tabSpacingRow &&
            FindMenuItem(menu, "rowSpacingRow") is { } rowSpacingRow &&
            FindMenuItem(menu, "autoHideSliverWidthRow") is { } autoHideSliverWidthRow &&
            FindMenuItem(menu, "scrollBarThicknessRow") is { } scrollBarThicknessRow &&
            FindMenuItem(menu, "sidePanel") is { } sidePanel &&
            FindMenuItem(menu, "fontWeight") is { } fontWeight &&
            FindMenuItem(menu, "sortMenu") is { } sortMenu &&
            FindMenuItem(menu, "iconStyleMenu") is { } iconStyleMenu &&
            FindMenuItem(menu, "languageMenu") is { } languageMenu)
        {
            // Nothing expanded means nothing to collapse - grey it out rather
            // than offering a confirmation prompt that would do nothing.
            collapseAllExpanded.IsEnabled = CollectAllExpandedPaths().Count > 0;

            _optionsMenu = sender as ContextMenu;
            _bookmarkListMenuItem = bookmarkList;
            bookmarkList.SubmenuOpened -= BookmarkSubmenu_Opened;
            bookmarkList.SubmenuOpened += BookmarkSubmenu_Opened;
            RebuildBookmarkListMenu();

            // FindMenuItem only looks at direct children, so anything living
            // in a submenu (기본 설정's toggles here, 패널 표시's rows below)
            // is looked up on that submenu rather than on the menu itself.
            // Leaving a moved id in the outer chain would make the whole
            // chain fail to match and take every setting down silently: the
            // exact shape of the v1.3.4/v1.4.0 menu breakages.
            if (FindMenuItem(generalSettings, "alwaysOnTop") is { } alwaysOnTop &&
                FindMenuItem(generalSettings, "dockOnRight") is { } dockOnRight &&
                FindMenuItem(generalSettings, "startWithWindows") is { } startWithWindows &&
                FindMenuItem(generalSettings, "trayIcon") is { } trayIcon &&
                FindMenuItem(generalSettings, "showFolderIcons") is { } showFolderIcons &&
                FindMenuItem(generalSettings, "showFileIcons") is { } showFileIcons &&
                FindMenuItem(generalSettings, "hideTitleBarTitle") is { } hideTitleBarTitle &&
                FindMenuItem(generalSettings, "autoCollapse") is { } autoCollapse &&
                FindMenuItem(generalSettings, "autoHideCloseOnLeave") is { } autoHideCloseOnLeave &&
                FindMenuItem(generalSettings, "autoHideUseHandle") is { } autoHideUseHandle &&
                FindMenuItem(generalSettings, "autoHideSlide") is { } autoHideSlide)
            {
                alwaysOnTop.IsChecked = _settings.AlwaysOnTop;
                dockOnRight.IsChecked = _settings.DockOnRight;
                startWithWindows.IsChecked = _settings.StartWithWindows;
                trayIcon.IsChecked = _settings.AlwaysShowTrayIcon;
                showFolderIcons.IsChecked = _settings.ShowFolderIcons;
                showFileIcons.IsChecked = _settings.ShowFileIcons;
                hideTitleBarTitle.IsChecked = _settings.HideTitleBarTitle;
                autoCollapse.IsChecked = _settings.AutoCollapseFolders;
                autoHideCloseOnLeave.IsChecked = _settings.AutoHideCloseOnMouseLeave;
                autoHideUseHandle.IsChecked = _settings.AutoHideUseHandle;
                autoHideSlide.IsChecked = _settings.AutoHideSlide;
            }
            else
            {
                LogClickLine("options menu: a 기본 설정 row is missing");
            }

            if (FindMenuItem(sidePanel, "sidePanelFavorites") is { } panelFavorites &&
                FindMenuItem(sidePanel, "sidePanelBookmarks") is { } panelBookmarks &&
                FindMenuItem(sidePanel, "sidePanelNone") is { } panelNone &&
                FindMenuItem(sidePanel, "favoritesAtBottom") is { } favoritesAtBottom)
            {
                panelBookmarks.IsChecked = IsBookmarkPanelMode;
                panelNone.IsChecked = IsPanelHiddenMode;
                panelFavorites.IsChecked = !IsBookmarkPanelMode && !IsPanelHiddenMode;

                // Nothing to place while the panel is off.
                favoritesAtBottom.IsChecked = _settings.FavoritesAtBottom;
                favoritesAtBottom.IsEnabled = !IsPanelHiddenMode;
            }
            else
            {
                LogClickLine("options menu: a 패널 표시 row is missing");
            }

            if (FindMenuItem(fontWeight, "fontWeightNormal") is { } weightNormal &&
                FindMenuItem(fontWeight, "fontWeightBold") is { } weightBold &&
                FindMenuItem(fontWeight, "fontWeightFolders") is { } weightFolders &&
                FindMenuItem(fontWeight, "fontWeightFiles") is { } weightFiles)
            {
                bool isBold = string.Equals(_settings.TreeFontWeight, "bold", StringComparison.OrdinalIgnoreCase);
                bool isFolders = string.Equals(_settings.TreeFontWeight, "folders", StringComparison.OrdinalIgnoreCase);
                bool isFiles = string.Equals(_settings.TreeFontWeight, "files", StringComparison.OrdinalIgnoreCase);
                weightBold.IsChecked = isBold;
                weightFolders.IsChecked = isFolders;
                weightFiles.IsChecked = isFiles;
                weightNormal.IsChecked = !isBold && !isFolders && !isFiles;
            }
            else
            {
                LogClickLine("options menu: a 글꼴 굵기 row is missing");
            }

            if (sortMenu.Items is [MenuItem byName, MenuItem byDate, MenuItem byType, MenuItem bySize, _, MenuItem ascending, MenuItem descending])
            {
                CheckSortFieldItems(ReadSortField(_settings.SortField, _settings.SortByDate),
                    byName, byDate, byType, bySize);
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
        else
        {
            // An id was renamed or dropped in the XAML. Debug builds say so
            // rather than leaving the whole menu quietly unconfigured, which is
            // what the positional version did silently.
            LogClickLine("options menu: a named item is missing - menu unconfigured");
        }
    }

    // By AutomationId, the options menu's counterpart to the row menus'
    // FindTaggedMenuElement. Direct children only: every id here belongs to a
    // top-level row, and searching deeper would let a submenu's own row answer
    // for its parent.
    // Two resources rather than one, so "folders only" needs no special case
    // anywhere: folder rows inherit FolderNameFontWeight from the TreeViewItem
    // style and file rows override it with FileNameFontWeight in the same
    // trigger that already gives them their own colour. The favorites and
    // bookmark panel rows read the same pair.
    private void ApplyTreeFontWeight()
    {
        bool bold = string.Equals(_settings.TreeFontWeight, "bold", StringComparison.OrdinalIgnoreCase);
        bool foldersOnly = string.Equals(_settings.TreeFontWeight, "folders", StringComparison.OrdinalIgnoreCase);
        bool filesOnly = string.Equals(_settings.TreeFontWeight, "files", StringComparison.OrdinalIgnoreCase);

        Resources["FolderNameFontWeight"] = bold || foldersOnly ? FontWeights.Bold : FontWeights.Normal;
        Resources["FileNameFontWeight"] = bold || filesOnly ? FontWeights.Bold : FontWeights.Normal;
    }

    // Shows the chevron for whichever direction still has menu rows in it.
    // Driven from code rather than a trigger because the question - "is the
    // offset short of the end" - compares two properties, and a Trigger can
    // only test one against a constant.
    //
    // Runs on every scroll of every menu, so it stays arithmetic and property
    // writes only. The half-pixel slack absorbs the fractional offsets that
    // fall out of row heights at non-integer font scales; without it the
    // bottom chevron can survive a scroll all the way down by a rounding
    // error, which reads as the menu lying about having more.
    private void MenuScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer viewer)
        {
            return;
        }

        bool scrolls = viewer.ScrollableHeight > 0.5;

        if (viewer.Template.FindName("MoreAboveGlyph", viewer) is UIElement above)
        {
            above.Visibility = scrolls && viewer.VerticalOffset > 0.5
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        if (viewer.Template.FindName("MoreBelowGlyph", viewer) is UIElement below)
        {
            below.Visibility = scrolls && viewer.VerticalOffset < viewer.ScrollableHeight - 0.5
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    // ----- 파일 종류 필터 -----------------------------------------------------
    //
    // "전체" is not a category, it is the empty selection - so it behaves like a
    // radio against the rest (picking it clears them; picking any of them
    // clears it) while the others multi-select freely. That asymmetry is the
    // user's own specification, and it falls out of the storage rather than
    // being enforced on top of it: an empty list means no filter.
    private void ApplyFileFilter()
    {
        FileTypeFilter.SelectedCategories.Clear();
        foreach (string category in _settings.FileFilterCategories)
        {
            FileTypeFilter.SelectedCategories.Add(category);
        }

        _settingsService.Save(_settings);
        UpdateFileFilterIndicator();

        // Every folder already on screen re-reads, the same way a sort or a
        // per-folder cap change does - a filter that only took effect on
        // folders opened afterwards would be worse than no filter.
        //
        // Quietly, though: a filter is a display change like the hidden-folder
        // toggle, so it has no business moving the view. The footer toggles put
        // it under the hand, and having the tree jump to the top on every click
        // reads as the app losing the user's place rather than filtering.
        RefreshAllLoadedFolders(pinSelectionToTop: false);
    }

    // The empty-space menu's 북마크 submenu. Deliberately NOT the row menu's:
    // that one leads with 북마크 추가/해제, which acts on the row under the
    // cursor, and out in the empty space there is no row - so the toggle is
    // left out entirely rather than shown greyed. What remains still works from
    // nowhere in particular: the two jumps, and the list.
    private void EmptySpaceBookmarkSubmenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem submenu)
        {
            return;
        }

        submenu.Items.Clear();

        bool hasBookmarks = _settings.BookmarkPaths.Count > 0;
        var prev = FollowMenuFont(new MenuItem
        {
            Header = Strings.BookmarkShortcutPrev,
            InputGestureText = "Ctrl+Alt+J",
            IsEnabled = hasBookmarks,
        });
        prev.Click += (_, _) => JumpToBookmark(-1);
        submenu.Items.Add(prev);

        var next = FollowMenuFont(new MenuItem
        {
            Header = Strings.BookmarkShortcutNext,
            InputGestureText = "Ctrl+Alt+L",
            IsEnabled = hasBookmarks,
        });
        next.Click += (_, _) => JumpToBookmark(1);
        submenu.Items.Add(next);

        // The same list the row menu and the options menu build.
        AppendBookmarkListTo(submenu);
    }

    // Category per row, carried on the row itself. Empty string = 전체, which is
    // the absence of a filter rather than a category - see AppSettings.
    private const string FileFilterAllTag = "";

    private static readonly (string Category, Func<string> Label)[] FileFilterRows =
    {
        (FileTypeFilter.Code, () => Strings.MenuFileFilterCode),
        (FileTypeFilter.Image, () => Strings.MenuFileFilterImage),
        (FileTypeFilter.Document, () => Strings.MenuFileFilterDocument),
        (FileTypeFilter.Media, () => Strings.MenuFileFilterMedia),
        (FileTypeFilter.Archive, () => Strings.MenuFileFilterArchive),
        (FileTypeFilter.Executable, () => Strings.MenuFileFilterExecutable),
        (FileTypeFilter.Other, () => Strings.MenuFileFilterOther),
    };

    // Built in code, not declared three times in XAML. This submenu now hangs
    // off the options menu, the row menu AND the empty-space menu, and three
    // copies of fifty lines is three places for them to drift apart - the same
    // reason the bookmark and hidden-folder lists are built rather than
    // declared. The labels are read at build time so a language switch lands.
    private void FileFilterSubmenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem submenu)
        {
            return;
        }

        submenu.Items.Clear();
        submenu.Items.Add(BuildFileFilterRow(Strings.MenuFileFilterAll, FileFilterAllTag));
        submenu.Items.Add(new Separator());
        foreach (var (category, label) in FileFilterRows)
        {
            submenu.Items.Add(BuildFileFilterRow(label(), category));
        }

        // The user's own kind, and the row that edits it. Two rows rather than
        // one that does both: the seven above are switches, and a switch that
        // also opens a window when you happen to hit it in the wrong state is
        // the kind of row people stop trusting. The list is only offered once
        // it holds something - there is nothing to select otherwise.
        submenu.Items.Add(new Separator());
        if (FileTypeFilter.HasCustomExtensions)
        {
            submenu.Items.Add(BuildFileFilterRow(
                FileTypeFilter.DescribeExtensions(_settings.FileFilterCustomExtensions),
                FileTypeFilter.Custom));
        }

        var edit = FollowMenuFont(new MenuItem { Header = Strings.MenuFileFilterCustomEdit });
        edit.Click += (_, args) =>
        {
            args.Handled = true;
            EditCustomFileFilter();
        };
        submenu.Items.Add(edit);
    }

    // Asks for the extensions, then applies whatever comes back. Selecting the
    // custom kind on the way out is deliberate: someone who has just typed
    // "psd, ai" is asking to see those files, and leaving them to find the row
    // and tick it would make the window they just filled in do nothing visible.
    private void EditCustomFileFilter()
    {
        var window = new FilterExtensionsWindow(_settings.FileFilterCustomExtensions) { Owner = this };
        PositionNearOptionsButton(window);
        if (window.ShowDialog() != true || window.Result is not { } extensions)
        {
            return;
        }

        bool wasSelected = _settings.FileFilterCategories.Contains(FileTypeFilter.Custom);
        _settings.FileFilterCustomExtensions = extensions;
        FileTypeFilter.SetCustomExtensions(extensions);

        if (extensions.Length > 0 && !wasSelected)
        {
            _settings.FileFilterCategories.Add(FileTypeFilter.Custom);
        }

        DropCustomFilterIfEmpty();

        // The chip carries the extensions as its label, so it is a different
        // chip now - and it appears or disappears with the list itself.
        RebuildFooterFilterChips();
        ApplyFileFilter();
    }

    // An empty custom list claims no file, so leaving it selected would filter
    // everything away - and with 전체 unlit the footer would insist a filter is
    // on while naming nothing. Clearing the extensions IS the way to remove
    // this kind, so the selection has to go with them.
    private void DropCustomFilterIfEmpty()
    {
        if (!FileTypeFilter.HasCustomExtensions)
        {
            _settings.FileFilterCategories.Remove(FileTypeFilter.Custom);
            FileTypeFilter.SelectedCategories.Remove(FileTypeFilter.Custom);
        }
    }

    private MenuItem BuildFileFilterRow(string header, string category)
    {
        var row = FollowMenuFont(new MenuItem
        {
            Header = header,
            Tag = category,
            IsCheckable = true,
            // Picking three kinds should be three clicks, not three trips back
            // into the menu.
            StaysOpenOnClick = true,
        });
        row.IsChecked = IsFileFilterRowChecked(category);

        row.Click += (_, args) =>
        {
            args.Handled = true;

            if (category.Length == 0)
            {
                _settings.FileFilterCategories.Clear();
            }
            else if (row.IsChecked)
            {
                if (!_settings.FileFilterCategories.Contains(category))
                {
                    _settings.FileFilterCategories.Add(category);
                }
            }
            else
            {
                _settings.FileFilterCategories.Remove(category);
            }

            ApplyFileFilter();

            // The whole group is re-marked, not just the row clicked: turning
            // the last category off falls back to 전체, and 전체 turns the rest
            // off. Found from the row's own host, since this submenu lives in
            // three different menus.
            if (ItemsControl.ItemsControlFromItemContainer(row) is MenuItem host)
            {
                SyncFileFilterMenu(host);
            }
        };

        return row;
    }

    private bool IsFileFilterRowChecked(string category)
        => category.Length == 0
            ? _settings.FileFilterCategories.Count == 0
            : _settings.FileFilterCategories.Contains(category);

    private void SyncFileFilterMenu(MenuItem submenu)
    {
        foreach (var row in submenu.Items.OfType<MenuItem>())
        {
            if (row.Tag is string category)
            {
                row.IsChecked = IsFileFilterRowChecked(category);
            }
        }
    }

    // A filter that hides files SILENTLY is a filter you forget you turned on,
    // and then the app looks like it lost your files. 숨긴 폴더 answers this
    // with a list you can see and clear; this answers it in the footer, which
    // is already on screen, costs no layout, and says nothing at all while
    // nothing is filtered.
    // The footer's filter chips. Built once, then only their IsChecked moves -
    // rebuilding a row of eight on every toggle would take the pressed one out
    // from under the cursor mid-click.
    private readonly List<ToggleButton> _footerFilterChips = new();

    // Carried by every chip rather than by the panel around them: once the row
    // wraps, the only thing between the two lines is what the chips themselves
    // bring (user, 2026-08-06 - the two lines were touching). It goes on their
    // TOP, so the panel's own bottom margin still holds the strip off the
    // window's edge and the air above and below comes out even.
    private const double FooterChipRowGap = 2;

    private void BuildFooterFilterChips()
    {
        // 전체 first and set apart by a wider gap: it is not one of the kinds,
        // it is the empty selection - the same asymmetry the menu draws with a
        // separator, which a single row has no room for.
        AddFooterFilterChip(Strings.MenuFileFilterAll, FileFilterAllTag, new Thickness(0, FooterChipRowGap, 8, 0));
        foreach (var (category, label) in FileFilterRows)
        {
            AddFooterFilterChip(
                category == FileTypeFilter.Executable ? Strings.FilterChipExecutable : label(),
                category,
                new Thickness(0, FooterChipRowGap, 2, 0));
        }

        // Last, and only once it holds something. It carries the extensions
        // themselves rather than the words 사용자 지정 - the strip's job is to
        // say what is being hidden, and "사용자 지정" answers that with a
        // question. Long lists are cut short with the full one on hover; the
        // chips already wrap to a second line, but one chip should not be able
        // to take the whole of it.
        if (FileTypeFilter.HasCustomExtensions)
        {
            string described = FileTypeFilter.DescribeExtensions(_settings.FileFilterCustomExtensions);
            AddFooterFilterChip(
                Shorten(described, 14),
                FileTypeFilter.Custom,
                new Thickness(0, 0, 2, 0),
                described);
        }

        UpdateFileFilterIndicator();
    }

    private static string Shorten(string text, int limit)
        => text.Length <= limit ? text : text[..limit].TrimEnd(',', ' ') + "…";

    // The chips are built once and then only their IsChecked moves - except
    // when the custom list is edited, which changes what the chips ARE.
    private void RebuildFooterFilterChips()
    {
        foreach (var chip in _footerFilterChips)
        {
            VersionFooterPanel.Children.Remove(chip);
        }

        _footerFilterChips.Clear();
        BuildFooterFilterChips();
    }

    private void AddFooterFilterChip(string label, string category, Thickness margin, string? tooltip = null)
    {
        var chip = new ToggleButton
        {
            Content = label,
            Tag = category,
            Style = (Style)FindResource("FooterFilterChipStyle"),
            Margin = margin,
            ToolTip = tooltip,
        };

        chip.Click += (_, _) =>
        {
            if (category.Length == 0)
            {
                _settings.FileFilterCategories.Clear();
            }
            else if (chip.IsChecked == true)
            {
                if (!_settings.FileFilterCategories.Contains(category))
                {
                    _settings.FileFilterCategories.Add(category);
                }
            }
            else
            {
                _settings.FileFilterCategories.Remove(category);
            }

            ApplyFileFilter();
        };

        _footerFilterChips.Add(chip);
        VersionFooterPanel.Children.Add(chip);
    }

    // A filter that hides files SILENTLY is a filter you forget you turned on,
    // and then the app looks like it lost your files. The footer answers that,
    // and since 2026-08-02 it also FIXES it: the strip where a filter announces
    // itself is where the hand already is when it needs changing, so each kind
    // is a toggle rather than a word. 전체 is lit exactly when nothing else is.
    //
    // The app name and version used to sit at the head of this strip and were
    // dropped when the toggles arrived (user's call): a row of controls is not
    // a place for a label nobody acts on, and the version is still in 앱 정보,
    // which is where someone actually goes looking for it.
    private void UpdateFileFilterIndicator()
    {
        foreach (var chip in _footerFilterChips)
        {
            chip.IsChecked = chip.Tag is string category && IsFileFilterRowChecked(category);
        }
    }

    private void FontWeightMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item)
        {
            return;
        }

        string weight = System.Windows.Automation.AutomationProperties.GetAutomationId(item) switch
        {
            "fontWeightBold" => "bold",
            "fontWeightFolders" => "folders",
            "fontWeightFiles" => "files",
            _ => "normal",
        };

        // A checkable row toggles itself on click, so re-picking the current
        // one would uncheck it and leave the group reading as "none".
        item.IsChecked = true;

        if (string.Equals(_settings.TreeFontWeight, weight, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _settings.TreeFontWeight = weight;
        _settingsService.Save(_settings);
        ApplyTreeFontWeight();
    }

    private void SidePanelModeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item)
        {
            return;
        }

        string mode = System.Windows.Automation.AutomationProperties.GetAutomationId(item) switch
        {
            "sidePanelBookmarks" => "bookmarks",
            "sidePanelNone" => "none",
            _ => "favorites",
        };

        // A checkable row toggles itself on click, so clicking the mode that is
        // already current would UNCHECK it and leave the group saying nothing
        // is on for the moment before the menu closes. The re-check on open
        // fixes it next time; this fixes it now.
        item.IsChecked = true;

        if (string.Equals(_settings.SidePanelMode, mode, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _settings.SidePanelMode = mode;
        _settingsService.Save(_settings);
        ApplySidePanelMode();
    }

    private static MenuItem? FindMenuItem(ItemsControl menu, string id)
        => menu.Items.OfType<MenuItem>()
            .FirstOrDefault(item => System.Windows.Automation.AutomationProperties.GetAutomationId(item) == id);

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
            QueueMaxItemsRefresh();
        }

        UpdateStepperRow(sender, value, 1, 50);
    }

    // RefreshAllLoadedFolders is the heaviest operation in the app - it drops
    // every item instance, re-reads every expanded folder from disk, replays
    // the expansion path by path and then re-reveals the selection. Running it
    // per CLICK meant walking 20 -> 50 paid for it thirty times over, which is
    // simply unusable once a few folders are open (reported 2026-07-25).
    // The cap itself (FileSystemItem.DisplayCap) is applied on the spot, so
    // anything loaded from here on already honours the new value; only the
    // re-cap of already-loaded folders waits for the stepper to settle.
    private System.Windows.Threading.DispatcherTimer? _maxItemsRefreshTimer;

    private void QueueMaxItemsRefresh()
    {
        _maxItemsRefreshTimer ??= new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(350)
        };
        _maxItemsRefreshTimer.Tick -= MaxItemsRefreshTimer_Tick;
        _maxItemsRefreshTimer.Tick += MaxItemsRefreshTimer_Tick;

        // Restarted, not merely started: each further click pushes the single
        // refresh back until the clicking stops.
        _maxItemsRefreshTimer.Stop();
        _maxItemsRefreshTimer.Start();
    }

    private void MaxItemsRefreshTimer_Tick(object? sender, EventArgs e)
    {
        _maxItemsRefreshTimer!.Stop();

        // Same reasoning as the file-kind filter: how many rows a folder shows
        // is a display change, and the stepper is held down while watching the
        // tree - the one moment the view must not slide out from under it.
        RefreshAllLoadedFolders(pinSelectionToTop: false);
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

        var parsed = FileSystemService.ParseSortField(field);
        _settings.SortField = FileSystemService.FormatSortFieldName(parsed);
        // Kept in step for older builds reading this file - see AppSettings.
        _settings.SortByDate = parsed == FileSortField.Date;
        FileSystemService.SortField = parsed;
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
        SetFolderSortOverride(item, FileSystemService.ParseSortField(field), sortDescending);
    }

    private void FolderSortDirectionMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string direction } ||
            ExplorerTree.SelectedItem is not FileSystemItem { IsPlaceholder: false, IsDirectory: true } item)
        {
            return;
        }

        var (currentField, _) = GetEffectiveFolderSort(item);
        SetFolderSortOverride(item, currentField, sortDescending: direction == "desc");
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
    private (FileSortField Field, bool SortDescending) GetEffectiveFolderSort(FileSystemItem item)
    {
        var entry = _settings.FolderSortOverrides.FirstOrDefault(o =>
            string.Equals(o.Path, item.FullPath, StringComparison.OrdinalIgnoreCase));
        return entry is not null
            ? (ReadSortField(entry.SortField, entry.SortByDate), entry.SortDescending)
            : (ReadSortField(_settings.SortField, _settings.SortByDate), _settings.SortDescending);
    }

    // Settings written before 유형/크기 existed carry only the SortByDate
    // boolean, so an empty field name falls back to it rather than silently
    // resetting somebody's saved order to 이름.
    private static FileSortField ReadSortField(string? fieldName, bool legacySortByDate)
        => string.IsNullOrWhiteSpace(fieldName)
            ? (legacySortByDate ? FileSortField.Date : FileSortField.Name)
            : FileSystemService.ParseSortField(fieldName);

    // Sets (or updates) this folder's own remembered sort: persists it,
    // mirrors it into FileSystemService.SortOverrides so LoadChildren picks
    // it up on every future (re)load of this exact path, flips the live
    // instance's icon on immediately, and re-sorts it right now. Uses the
    // state-preserving refresh (like a background external-change refresh),
    // not the plain RefreshFolder_Click F5 uses - resorting a folder
    // shouldn't silently collapse whatever subfolders the user had expanded
    // further down inside it.
    private void SetFolderSortOverride(FileSystemItem item, FileSortField field, bool sortDescending)
    {
        var entry = _settings.FolderSortOverrides.FirstOrDefault(o =>
            string.Equals(o.Path, item.FullPath, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            entry = new FolderSortOverrideEntry { Path = item.FullPath };
            _settings.FolderSortOverrides.Add(entry);
        }
        entry.SortField = FileSystemService.FormatSortFieldName(field);
        // Still written for a settings file that may be read by an older
        // build - it only knows name/date, and 유형/크기 land on 이름 there.
        entry.SortByDate = field == FileSortField.Date;
        entry.SortDescending = sortDescending;

        FileSystemService.SortOverrides[FileSystemService.NormalizeSortOverridePath(item.FullPath)] =
            new FolderSortOverride(field, sortDescending);
        item.HasSortOverride = true;
        item.SortOverrideIconGeometry = FileSystemService.SortOverrideGeometry(sortDescending);
        item.SortOverrideTooltip = FileSystemService.FormatSortTooltip(field, sortDescending);

        if (item.ChildrenLoaded)
        {
            RefreshFolderPreservingState(item);
        }
    }

    // Clicking a folder's sort icon opens that folder's sort menu, anchored to
    // the icon. It used to rotate through the states instead - 전역 따름 ->
    // 이름↑ -> 이름↓ -> 날짜↑ -> 날짜↓ -> 전역 따름 - which was already four
    // clicks to reach the last state and would have been eight with 유형 and
    // 크기 in it. A rotation only works while there are three or four states;
    // past that it stops being a control and becomes a guessing game, so the
    // icon's job shrank to showing whether this folder sorts its own way (and
    // which direction), with the menu naming the rest in words.
    // Shared by both places the folder's sort is shown - the right-click
    // menu's "정렬 기준" submenu and the flat menu the row's icon opens. Same
    // items in the same order, so one routine can tick them: the folder's own
    // sort if it has one, otherwise the app-wide default, which is exactly
    // what that folder would sort by if it were reloaded right now.
    private void ApplyFolderSortMenuState(ItemCollection items, bool isFolder)
    {
        if (items is not [MenuItem byName, MenuItem byDate, MenuItem byType, MenuItem bySize, _,
                MenuItem ascending, MenuItem descending, _, MenuItem followGlobal])
        {
            return;
        }

        bool hasOverride = isFolder && ExplorerTree.SelectedItem is FileSystemItem { HasSortOverride: true };
        var (field, sortDescending) = isFolder && ExplorerTree.SelectedItem is FileSystemItem folderItem
            ? GetEffectiveFolderSort(folderItem)
            : (ReadSortField(_settings.SortField, _settings.SortByDate), _settings.SortDescending);

        CheckSortFieldItems(field, byName, byDate, byType, bySize);
        ascending.IsChecked = !sortDescending;
        descending.IsChecked = sortDescending;
        followGlobal.IsEnabled = hasOverride;
    }

    private static void CheckSortFieldItems(FileSortField field, MenuItem byName, MenuItem byDate,
        MenuItem byType, MenuItem bySize)
    {
        byName.IsChecked = field == FileSortField.Name;
        byDate.IsChecked = field == FileSortField.Date;
        byType.IsChecked = field == FileSortField.Type;
        bySize.IsChecked = field == FileSortField.Size;
    }

    private void FolderSortContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        AnyMenu_Opened(sender, e);

        if (sender is ContextMenu menu)
        {
            ApplyFolderSortMenuState(menu.Items,
                isFolder: ExplorerTree.SelectedItem is FileSystemItem { IsPlaceholder: false, IsDirectory: true });
        }
    }

    private void OpenFolderSortMenu(FileSystemItem item, UIElement anchor)
    {
        if (TryFindResource("FolderSortContextMenu") is not ContextMenu menu)
        {
            return;
        }

        // Every handler in that menu works off the tree's current selection,
        // exactly as the right-click menu's own submenu does.
        if (FindRealizedContainer(item) is { } container)
        {
            container.IsSelected = true;
        }

        menu.PlacementTarget = anchor;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    // Drops this folder's own remembered sort so it goes back to picking up
    // the app-wide default like any other folder - see SetFolderSortOverride.
    private void ClearFolderSortOverride(FileSystemItem item)
    {
        _settings.FolderSortOverrides.RemoveAll(o =>
            string.Equals(o.Path, item.FullPath, StringComparison.OrdinalIgnoreCase));
        FileSystemService.SortOverrides.Remove(FileSystemService.NormalizeSortOverridePath(item.FullPath));
        item.HasSortOverride = false;
        // Back to the neutral "follows the app-wide default" glyph.
        item.SortOverrideIconGeometry = FileSystemService.FollowsGlobalSortGeometry;
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
    // pinSelectionToTop: the restored selection is normally put at the top, the
    // way a jump does. A DISPLAY change (the hidden-folder toggle) should not
    // move the view at all, so it asks for the quiet restore instead.
    private void RefreshAllLoadedFolders(bool pinSelectionToTop = true, bool reloadRoots = false)
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
            if (root.IsExpanded)
            {
                // A root's own expanded state is not collected by the helper
                // below (it only looks at an item's children), and it has to be
                // when the roots themselves are about to be replaced.
                expandedPaths.Add(root.FullPath);
            }
            CollectExpandedPaths(root, expandedPaths, showingAllPaths);
        }
        string? selectedPath = (ExplorerTree.SelectedItem as FileSystemItem)?.FullPath;

        if (reloadRoots)
        {
            // The drive rows themselves are what changed - a hidden drive
            // leaving or coming back. Re-reading each root's CHILDREN, which is
            // all this used to do, could never add or remove a root, so
            // toggling "숨긴 폴더 표시" left hidden drives off the tree
            // (reported 2026-08-02).
            ReloadRoots();
        }
        else
        {
            foreach (var root in _roots)
            {
                if (root.ChildrenLoaded)
                {
                    root.RefreshChildren();
                }
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
            NavigateToPath(selectedPath, pinToTop: pinSelectionToTop, source: "refresh");
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
            ExitLog.Record("restart requested by the user (language/settings change)");
            System.Diagnostics.Process.Start(Environment.ProcessPath!);
            Application.Current.Shutdown();
        }
    }

    // The recovery lever: when something feels off - and the fault may be
    // another program's, not ours (the user's own framing, 2026-08-09) - one
    // click brings the app back clean. Same restart the language change
    // performs, minus its prompt: clicking 다시 시작 IS the intent. State is
    // flushed first (SaveCurrentWidth also persists expanded folders,
    // selection and the settings file) for the same reason the language path
    // saves eagerly - the new instance starts reading the file immediately,
    // possibly before the old instance's normal on-close flush would have
    // run.
    private void RestartMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentWidth();
        ExitLog.Record("restart requested by the user (restart menu)");
        System.Diagnostics.Process.Start(Environment.ProcessPath!);
        Application.Current.Shutdown();
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
            ExitLog.Record("restart requested by the user (language/settings change)");
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

        ExitLog.Record("restart after settings reset");
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

            // Auto-hide REQUIRES topmost whatever this preference says - the
            // sliver is the only way back in, and one behind another window
            // can never be reached by the mouse again. ApplyTopmostState owns
            // that rule (and the writing-through problem) for every caller.
            ApplyTopmostState("always-on-top toggle");
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
                // Total width unchanged - the window jumps edges whole, and
                // the panel swings to the new screen-interior side (part of
                // the ride-through experiment, see Undock).
                PositionToWorkArea();
                ApplyViewerSide();
            }
        }
    }

    // Applied live, unlike the thickness stepper next to it: this one changes
    // where the reveal target IS, and someone who just turned it on needs to
    // see where their handle went. The menu is open over the revealed sidebar
    // at this moment, so the shape only actually changes when it re-hides -
    // PositionToWorkArea is still called so the case of toggling it while
    // already collapsed (possible via the tray menu) lands correctly too.
    private void AutoHideUseHandleMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
        {
            _settings.AutoHideUseHandle = menuItem.IsChecked;
            _settingsService.Save(_settings);

            if (_isDocked && _settings.IsAutoHidden && !_isAutoHideRevealed)
            {
                Width = CollapsedWidth;
            }

            PositionToWorkArea();
        }
    }

    private void AutoHideSlideMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
        {
            _settings.AutoHideSlide = menuItem.IsChecked;
            _settingsService.Save(_settings);
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
    // driven by "들여쓰기 간격" (AppSettings.TabSpacing, user-adjustable 4~24 from
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

        // The bookmark panel row's leading number. A step below the row's own
        // font, which is how this app marks something as secondary - fading it
        // is not done here (a dimmed label went wrong twice on the bookmark
        // menu). The column is wide enough for two digits at that size so the
        // names below ten and above it still share one left edge; a third digit
        // simply pushes the column, which is rarer than it is worth reserving
        // room for.
        double panelNumberFontSize = Math.Max(9.0, Math.Round(ExplorerTree.FontSize) - 2);
        Resources["PanelNumberFontSize"] = panelNumberFontSize;

        // Two digits' worth, so rows below ten and above it share one left
        // edge. Not three: nobody keeps a thousand bookmarks, and reserving for
        // a case that will not happen pushes every row right for nothing
        // (user's point, 2026-08-02). A third digit simply widens the column
        // when it does turn up.
        Resources["PanelNumberWidth"] = Math.Round(panelNumberFontSize * 1.2);

        // The marker's lane: the 3px bar plus a hair of air. Deliberately not
        // the tree's indent width - see the row template's own comment. Fixed
        // rather than scaled: the bar itself is 3px at every font size, so the
        // gap beside it should be too.
        Resources["PanelMarkerColumnWidth"] = new GridLength(5.0);

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

        // The bookmark marker follows the font zoom the same grow-only way
        // the sort icon does (9px at the default 12pt - 11 read a touch loud).
        double bookmarkMarkerHeight = Math.Round(9.0 * growOnlyScale);
        Resources["BookmarkMarkerHeight"] = bookmarkMarkerHeight;

        // ONE vertical line for both right-edge marks (user's call,
        // 2026-08-02). A row carrying only a sort icon and a row carrying only
        // a bookmark used to sit 4.5px apart - the icon centred 9.5px in from
        // the right edge, the ribbon 5px - which reads as a wobble down the
        // edge rather than as two different marks.
        //
        // The shared line is NOT simply where the ribbon already was. The
        // ribbon does not take clicks; the sort icon does, and the overlay
        // scrollbar hit-tests a 5px strip along that edge even at Opacity 0
        // (see MinimalScrollBarStyle). Centring the icon at 5px would put the
        // middle of its target inside that strip - aim at the icon, hit the
        // scrollbar. So the line sits just outside the strip and the RIBBON
        // comes in to meet it, which costs nothing because nothing aims at the
        // ribbon. It gives back a little of the "tucked against the edge" the
        // marker was given on 2026-07-28; one line for both was judged worth
        // those two pixels.
        double glyphCenterFromRight = Math.Round(8.0 * growOnlyScale);

        // The ribbon's own aspect is 480:672 in its native grid.
        double ribbonWidth = Math.Round(bookmarkMarkerHeight * 0.714);
        // Left is only the gap to whatever sits before it; the RIGHT margin is
        // what decides where the ribbon actually lands.
        Resources["BookmarkMarkerMargin"] = new Thickness(
            3, 0, Math.Max(0, glyphCenterFromRight - ribbonWidth / 2), 0);

        // Padding here is click area, not spacing, and the left half is the
        // side the cursor arrives from - so what gives to reach the shared line
        // is the right margin, not the target.
        const double SortIconPaddingRight = 2;
        double sortIconWidth = Math.Round(9.0 * growOnlyScale);
        Resources["SortOverrideIconMargin"] = new Thickness(
            4, 0, Math.Max(0, glyphCenterFromRight - sortIconWidth / 2 - SortIconPaddingRight), 0);
        Resources["SortOverrideIconPadding"] = new Thickness(10, 0, SortIconPaddingRight, 0);

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
        // Plateau lowered 6 -> 5 (2026-07-25): on a low-resolution laptop at
        // 14pt, the menu - now carrying the bookmark row too - was pushing
        // 800px tall with a thumbnail showing. Squeezed together with the
        // separator margin (6 -> 5 in the XAML) and the thumbnail's growth
        // cap below.
        //
        // Tried at 7 and then 10 on 2026-08-02 and REVERTED to 5 by the user:
        // the cramped feeling turned out not to be the row rhythm at all - it
        // was the content sitting flush against the new scrollbar (the
        // thumbnail's hover edge touching the thumb). That is fixed where it
        // actually is, in MenuScrollViewerStyle's gutter padding, so this
        // number goes back to the value two rounds of feedback settled on.
        double menuVerticalPadding = Math.Min(5.0, Math.Round(8.0 * menuVerticalScale));

        var appResources = Application.Current.Resources;
        appResources["MenuFontSize"] = ExplorerTree.FontSize;
        // The "해제" chip on a list row (MenuRowActionButtonStyle). Follows the
        // zoom like everything else, one step down and floored so it stays
        // legible at the smallest tree font.
        appResources["MenuChipFontSize"] = Math.Max(9.0, ExplorerTree.FontSize - 2.0);
        appResources["MenuGestureFontSize"] = Math.Max(8.0, Math.Round(11.0 * scale));
        // The dialogs (색상 설정, 앱 정보) live in their own windows, so nothing
        // of the tree's zoom reached them - they stayed at a hardcoded 12pt in a
        // 300px frame however large the rest of the app had been made. Someone
        // who raised the font did so to be able to read, and the settings window
        // is exactly where they then go. App-level rather than window-level
        // resources: another Window can only see these here.
        appResources["DialogFontSize"] = ExplorerTree.FontSize;
        // Widened from 240 when every colour row gained a hex field: at the old
        // width the labels and the fields fought over the same pixels and the
        // Korean ones lost (2026-08-02). The user gave up the width knowingly -
        // this window is opened rarely and read carefully.
        appResources["DialogWidth"] = Math.Round(330.0 * scale);
        // The colour window alone outgrew the shared width when its top
        // gained the two theme zones with dice and its bottom row a fourth
        // button - 랜덤 전으로 was clipping at 330 (user, 2026-08-09, with
        // the widening their own suggestion). The other dialogs stay at 330.
        appResources["ColorDialogWidth"] = Math.Round(390.0 * scale);
        // The colour window's hex box and the line under it. Wide enough for
        // "#RRGGBB" with room to spare, and the hint a step smaller - set apart
        // by size, never by fading.
        appResources["DialogHexInputWidth"] = Math.Round(70.0 * scale);
        appResources["DialogHintFontSize"] = Math.Max(9.0, ExplorerTree.FontSize - 2.0);
        // Lines the title up with the body below it, which sits at 16 - it had
        // been at 10, close enough to look like a near-miss rather than a
        // choice.
        appResources["DialogTitleIndent"] = new Thickness(16, 0, 0, 0);
        appResources["DialogButtonWidth"] = Math.Round(70.0 * scale);
        appResources["DialogSwatchWidth"] = Math.Round(32.0 * scale);
        appResources["DialogSwatchHeight"] = Math.Round(20.0 * scale);
        // Grows at roughly half the font's rate. Straight proportional spacing
        // read as airy once zoomed: the rows themselves are already taller, so
        // the gap between them doesn't need the same again.
        double dialogRowGap = Math.Max(6.0, Math.Round(10.0 + (scale - 1.0) * 4.0));
        appResources["DialogRowSpacing"] = new Thickness(0, dialogRowGap, 0, 0);
        // Even above and below. The row under a divider carries no spacing of
        // its own, so the divider owes it that gap - without it the line sat
        // against the next label.
        appResources["DialogDividerMargin"] = new Thickness(0, dialogRowGap + 2, 0, dialogRowGap + 2);
        // Grow-only: 28px is comfortable at the default font and only becomes
        // cramped once the title inside it grows. A GridLength, not a double -
        // RowDefinition.Height takes nothing else, and handing it a number
        // throws while the window is being parsed rather than at build time.
        appResources["DialogTitleBarHeight"] =
            new GridLength(Math.Max(28.0, Math.Round(28.0 * scale)));
        appResources["DialogTitleFontSize"] = ExplorerTree.FontSize + 2.0;
        appResources["DialogTitleIconSize"] = Math.Round(24.0 * scale);
        // The stepper buttons carry a "+"/"−" that follows the menu font, so
        // their box has to follow it too. Left at a fixed 20px they stopped
        // fitting once the font was zoomed up - the glyph's line box outgrew
        // the button and the symbol sat on its bottom edge (reported
        // 2026-07-28 at 16pt). Grow-only, like the sort and bookmark glyphs:
        // a smaller font has no trouble fitting, and shrinking the buttons
        // would only make them harder to hit.
        appResources["MenuStepperButtonSize"] = Math.Max(20.0, Math.Round(20.0 * scale));
        // Centring the glyph centres its LINE box, and a line box carries
        // descender room that "+" and "−" never use - so the ink they do draw
        // lands below the middle, by more the larger the font gets. A bottom
        // margin proportional to the font pushes it back up.
        // Rounded UP, not to nearest: the correction only lands on whole
        // pixels, and rounding down left 16-18pt a pixel short - the drift
        // returns as soon as the fraction is discarded. Half a pixel high is
        // not something the eye reports; half a pixel low is what it did.
        appResources["MenuStepperGlyphMargin"] =
            new Thickness(0, 0, 0, Math.Ceiling(ExplorerTree.FontSize * 0.13));
        // The thumbnail's info/date lines: one zoom step (1pt) below the menu
        // text - it's metadata under a picture, not a menu item.
        appResources["MenuThumbnailInfoFontSize"] = Math.Max(8.0, ExplorerTree.FontSize - 1.0);
        // The context menu's image-thumbnail slot (see UpdateThumbnailRow):
        // 4:3, sized to roughly fill the menu's own width at any font zoom.
        // The MAX matters as much as the min: the slot's Image reports the
        // picture's natural width during measure, so without a ceiling a wide
        // screenshot dragged the whole MENU out to its own width.
        // The thumbnail SHRINKS with a smaller font (a small-font user is
        // squeezing for space) but no longer GROWS with a bigger one
        // (2026-07-25): picture size has no reason to follow text zoom, and
        // its growth was the single biggest contributor to the menu nearing
        // screen height at 14pt on a low-resolution laptop.
        double thumbnailScale = Math.Min(1.0, scale);
        appResources["MenuThumbnailWidth"] = Math.Round(160.0 * thumbnailScale);
        appResources["MenuThumbnailMaxWidth"] = Math.Round(240.0 * thumbnailScale);
        appResources["MenuThumbnailHeight"] = Math.Round(120.0 * thumbnailScale);
        appResources["MenuItemPadding"] = new Thickness(
            Math.Round(15.0 * scale), menuVerticalPadding,
            Math.Round(15.0 * scale), menuVerticalPadding);
        // The stepper rows were the one place the menu's row rhythm hiccuped:
        // their +/- buttons stand taller than a line of text, so under the
        // shared padding those rows came out a few pixels taller than every
        // row around them (user, 2026-08-08). Their padding hands back the
        // buttons' overshoot instead - half above, half below - so a stepper
        // row measures the same as a plain row. Floored at zero: below ~11pt
        // the 20px button floor (see MenuStepperButtonSize above - shrinking
        // the buttons was already tried and rejected) is taller than a fully
        // unpadded text row, and a pixel or two of leftover height there is
        // the acceptable end of it.
        double menuLineHeight = ExplorerTree.FontFamily.LineSpacing * ExplorerTree.FontSize;
        double stepperOvershoot = Math.Max(0.0,
            (double)appResources["MenuStepperButtonSize"] - menuLineHeight);
        double stepperVerticalPadding = Math.Max(0.0, menuVerticalPadding - stepperOvershoot / 2.0);
        appResources["MenuStepperRowPadding"] = new Thickness(
            Math.Round(15.0 * scale), stepperVerticalPadding,
            Math.Round(15.0 * scale), stepperVerticalPadding);
        double menuHorizontalPadding = Math.Round(5.0 * scale);
        appResources["MenuPadding"] = new Thickness(
            menuHorizontalPadding, menuVerticalPadding,
            menuHorizontalPadding, menuVerticalPadding);

        // What a SCROLLING menu keeps clear around its rows - applied only
        // while the bar is showing (see MenuScrollViewerStyle), so a menu that
        // fits is untouched.
        //
        // Right: the same value as the menu's own horizontal padding above, on
        // purpose. That padding is what already sits to the RIGHT of the thumb
        // (plus the thumb's own 1px margin), so reusing it puts equal air on
        // both sides of the bar rather than a wider gap on one side.
        //
        // Top and bottom: room for the "more this way" chevrons, which were
        // otherwise drawn straight over the first and last row's text.
        // Reserved on BOTH edges the whole time the menu scrolls, not only on
        // the side currently showing an arrow - sizing it per-arrow would shift
        // every row down the moment you scrolled off the top.
        //
        // Small on purpose. It was 12, and that made a SCROLLING menu visibly
        // taller-topped than every menu that fits, which reads as two different
        // kinds of menu (2026-08-02). At 4 the chevron still clears the text,
        // because it also has the row's own vertical padding to sit in, and the
        // scrolling menu now looks like the rest. The honest fix if this is
        // still off is to move the chevrons out of the scroll area entirely,
        // into the blank the menu's own padding already leaves - which means
        // teaching both menu templates about them rather than one scroll
        // template.
        double menuScrollBand = Math.Round(4.0 * scale);
        appResources["MenuScrollContentPadding"] = new Thickness(
            0, menuScrollBand, menuHorizontalPadding, menuScrollBand);

        // Padding just changed, and the cap subtracts it.
        ApplyMenuMaxHeight();
    }

    // How tall a menu may grow before it scrolls instead. A menu lives in its
    // own popup, so nothing bounds it the way the window bounds the tree: a
    // list with 65 rows in it grew past the screen and had its tail CUT OFF
    // with no way to reach it (reported 2026-08-02). Both menu templates now
    // host their items in a ScrollViewer - but a ScrollViewer given unbounded
    // height never scrolls, so this is the number that makes it work.
    //
    // Deliberately NOT the full work area (user's call): a menu that fills the
    // screen edge to edge reads as broken, and the gap left above and below is
    // also the cue that the list continues past what is shown.
    //
    // The fraction is generous on purpose. Hitting the cap is not free - the
    // scrollbar takes a lane of its own out of the content (see
    // MinimalScrollViewerTemplate), so an ORDINARY right-click menu that only
    // just crosses the line loses visible width for nothing, which is exactly
    // what got reported at 0.8 ("폭이 좀 많이 줄어든 느낌", 2026-08-02). The cap
    // is here for the runaway list of 65 rows, not for menus that merely happen
    // to be long.
    private void ApplyMenuMaxHeight()
    {
        double workAreaHeight = GetCurrentMonitorWorkArea().Height;

        // What the popup wraps around the scrollable part and therefore does
        // not get to use: the shadow-bleed margin on both sides, the border,
        // and the menu's own padding.
        double chrome = 2 * 10 + 2;
        if (Application.Current.Resources["MenuPadding"] is Thickness menuPadding)
        {
            chrome += menuPadding.Top + menuPadding.Bottom;
        }

        // The floor matters on a short work area (a low-resolution laptop with
        // a large taskbar): a cap small enough to show two rows would be worse
        // than the clipping this replaces.
        double cap = Math.Max(240.0, workAreaHeight * 0.9 - chrome);
        Application.Current.Resources["MenuMaxHeight"] = cap;
        LogScrollLine($"menu   cap {cap:F0}  (work area {workAreaHeight:F0}, chrome {chrome:F0})");
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
        var window = new AboutWindow(UpdateAvailableVersion) { Owner = this };
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

        // Nothing under a drive that isn't answering can be opened - the read
        // would only time out and the rows behind it are stale anyway. Fold it
        // straight back and leave the drive row saying so.
        if (item.IsNetworkDriveOffline)
        {
            item.IsExpanded = false;
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
            case Key.X when Keyboard.Modifiers == ModifierKeys.Control:
                CutItem_Click(sender, e);
                e.Handled = true;
                break;
            // Esc calls off a pending cut as well as the multi-selection -
            // Explorer's own way out of "I didn't mean to cut that".
            case Key.Escape when _multiSelection.Count > 0 || FileSystemService.CutPaths.Count > 0:
                ClearMultiSelection();
                ClearCutMarks("esc");
                e.Handled = true;
                break;
            case Key.V when Keyboard.Modifiers == ModifierKeys.Control:
                PasteItem_Click(sender, e);
                e.Handled = true;
                break;
            // F7 = new folder, the binding Total Commander (and the file
            // managers that copied it) has used for decades. Explorer has no
            // key for this at all, so there's nothing to conflict with.
            case Key.F7:
                NewFolder_Click(sender, e);
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

        if (index == chain.Count - 1)
        {
            treeViewItem.IsSelected = true;
            treeViewItem.BringIntoView();
            treeViewItem.Focus();
            return;
        }

        // An ancestor is scrolled into view ONLY when the next level's
        // container doesn't exist yet - forcing realization is the one thing
        // that scroll is for. Bringing every ancestor in unconditionally (as
        // this walk originally did) sent the viewport UP to the folder and
        // then back DOWN to the target, and that return trip parks the target
        // on the viewport's bottom edge every time - so each carousel step
        // pinned the selected row to the bottom of the tree even when the row
        // it stepped to was already comfortably on screen (user report,
        // 2026-08-09). With the skip, a step between two visible rows moves
        // the viewport not at all, and a step past the edge scrolls the one
        // row the ↓ key would.
        treeViewItem.UpdateLayout();
        if (treeViewItem.ItemContainerGenerator.ContainerFromItem(chain[index + 1]) is null)
        {
            treeViewItem.BringIntoView();
            container.UpdateLayout();
        }
        SelectVisibleItemStep(chain, index + 1, treeViewItem);
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
            Key.F7 => () => NewFolder_Click(sender, e),
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

    // A press on the tree's own background - below the last row, or in the
    // strip left of the scrollbar - lands on no TreeViewItem, so none of the
    // row handlers run and keyboard focus has nowhere to move. With an inline
    // rename open that read as the click being swallowed: the name is
    // committed by RenameTextBox_LostFocus, and LostFocus cannot fire while
    // focus stays put. So the commit is made here instead, which is the same
    // "click away to confirm" that clicking another row already gives.
    //
    // Deliberately nothing else. Clearing the selection here would look
    // tidier and cost more than it looks: it is what the next launch restores
    // to (SaveCurrentWidth), and it is what keeps a temporarily-visible
    // hidden folder on screen (ReHideFoldersLeftBehind).
    private void ExplorerTree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;

        // The scrollbar is part of the tree's own template, and dragging it is
        // not clicking away from anything - the row being renamed is still
        // there, just moved.
        if (source?.FindAncestor<TreeViewItem>() is not null ||
            source?.FindAncestor<System.Windows.Controls.Primitives.ScrollBar>() is not null)
        {
            return;
        }

        FinishOpenInlineRename();
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

        LogClickLine("tree double-click");

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

        if (IsUnreachableNetworkItem(item))
        {
            e.Handled = true;
            return;
        }

        ShellFileService.OpenWithDefaultApp(item.FullPath);
        e.Handled = true;
    }

    // Rows on a network drive that isn't answering are a listing from before
    // it went away - fine to look at (that is why they stay on screen, dimmed
    // and red-dotted), but handing one to the shell means the SHELL puts up
    // its own "네트워크 오류 - 액세스할 수 없습니다" box, which this app can
    // neither prevent (SetErrorMode only covers our own IO) nor dismiss.
    // Reported 2026-07-26: clicking a file under a rebooting NAS. So opening
    // is simply declined while the drive is out; the row's own dimming already
    // says why, and nothing is lost by waiting for the dot to go green.
    // Also consults the service's own cache, not just the row's flag: the flag
    // is pushed down by a poll every couple of seconds, while the cache is
    // written the instant a read times out - and the click that matters most
    // is the one right after the drive goes quiet.
    private static bool IsUnreachableNetworkItem(FileSystemItem item)
        => item.IsOnNetworkDrive &&
           (item.IsNetworkDriveOffline || FileSystemService.IsNetworkPathUnreachable(item.FullPath));

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

    // Where the row's own content starts, measured from the row's left edge -
    // everything before it is indent. The icon when it is showing, the name
    // otherwise (icons can be turned off, and then the name IS the left edge).
    // Null means "could not tell", and the caller treats that as "not indent"
    // so an unanswerable case never silently disables the toggle.
    private static double? RowContentLeftEdge(TreeViewItem row)
    {
        var anchor = FindRowPart(row, "RowIcon") is { IsVisible: true, ActualWidth: > 0 } icon
            ? icon
            : FindRowPart(row, "NameText");

        if (anchor is not { IsVisible: true })
        {
            return null;
        }

        try
        {
            // Fully qualified: WinForms is referenced here too and brings its
            // own Point.
            return anchor.TransformToAncestor(row).Transform(new System.Windows.Point(0, 0)).X;
        }
        catch (InvalidOperationException)
        {
            // Not connected to the row's visual tree at this instant.
            return null;
        }
    }

    // Stops at any nested TreeViewItem: without that, a collapsed-but-realized
    // child row's icon could answer for its parent and put the boundary far to
    // the right, which would turn most of the parent row into "indent".
    private static FrameworkElement? FindRowPart(DependencyObject root, string name)
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is TreeViewItem)
            {
                continue;
            }
            if (child is FrameworkElement element && element.Name == name)
            {
                return element;
            }
            if (FindRowPart(child, name) is { } found)
            {
                return found;
            }
        }

        return null;
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

        // The bookmark ribbon releases the bookmark instead of selecting the
        // row - matched by name for the same reason as the sort icon below.
        // Handled, so the press never reaches the row: a click that both
        // unmarks and selects would leave the user unsure which one they
        // asked for.
        if ((e.OriginalSource as DependencyObject)?.FindAncestor<Border>() is { Name: "BookmarkMarkerBorder" } &&
            treeViewItem.DataContext is FileSystemItem { IsPlaceholder: false, IsShowMore: false } bookmarkedItem)
        {
            ToggleBookmark(bookmarkedItem);

            // Same stale-drag reset as the sort icon's early return below.
            _itemDragStart = null;
            _itemDragCandidate = null;
            e.Handled = true;
            return;
        }

        // The small sort icon (see FileSystemItem.HasSortOverride) opens this
        // folder's sort menu instead of the row's normal click behavior
        // (select + toggle expand/collapse) below - matched by name rather
        // than type, since other Border ancestors exist further up the same
        // row (RowBorder's hover/selection highlight) that must NOT match.
        if ((e.OriginalSource as DependencyObject)?.FindAncestor<Border>() is { Name: "SortOverrideIconBorder" } iconBorder &&
            treeViewItem.DataContext is FileSystemItem { IsPlaceholder: false, IsDirectory: true } overrideItem)
        {
            OpenFolderSortMenu(overrideItem, iconBorder);

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

        // Logged here rather than at the top of this handler: only the row the
        // press actually landed on is interesting, and by this point we know
        // whether it hit the arrow, what the row's state was BEFORE the toggle
        // below, and how long it has been since the previous press.
        LogTreeClick(treeViewItem.DataContext as FileSystemItem, clickedOnExpander, e.ClickCount);

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

        // ClickCount == 1 is what stops a folder from ignoring every other
        // click. Two clicks closer together than Windows' double-click time
        // (500ms by default - 400ms feels like two separate clicks to a person,
        // and the reports said as much) arrive as ONE press with ClickCount 2,
        // and WPF's TreeViewItem toggles IsExpanded on that by itself. Toggling
        // here as well ran the row through two toggles in the same press and
        // left it exactly where it started, which is precisely the reported
        // "click didn't take" (measured 2026-07-30: every ClickCount 2 press
        // logged a collapse and an expand one millisecond apart). Stepping
        // aside on the second press leaves the built-in behaviour to do the one
        // toggle, so each click still registers.
        // A click in the INDENT - the guide-line strip to the left of the row's
        // icon - selects the row but no longer toggles it (2026-08-02). That
        // strip is empty space as far as the eye is concerned, and with the
        // guides set narrow and faint it is easy to land in by accident; the
        // cost of doing so was not small. A misclick on a DRIVE ROOT collapsed
        // the whole drive, every other root jumped up to fill the space, and
        // the selection went with it - which is almost certainly the "tree
        // suddenly flew to the top at C:" report that scrolljump.log was added
        // to catch. The user worked that out from the gesture, not the log.
        //
        // Only the indent is excluded, not the rest of the row: clicking a
        // folder's name to open it is how this tree has always worked, and
        // VS Code's fixed, non-clickable indent is exactly the shape this now
        // borrows.
        bool clickedInIndent = RowContentLeftEdge(treeViewItem) is { } contentLeft
            && e.GetPosition(treeViewItem).X < contentLeft;

        if (!clickedOnExpander && !clickedInIndent && e.ClickCount == 1 &&
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
        // The activation grace keeps the click that brings the WINDOW back to
        // the foreground from doubling as a rename gesture - it lands on
        // whatever is under the cursor, often the very row that was left
        // selected (see _lastActivatedTicks).
        if (!clickedOnExpander && e.ClickCount == 1 && treeViewItem.IsSelected &&
            Environment.TickCount64 - _lastActivatedTicks > ActivationClickGraceMs &&
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
        // Last line of defence for the drop-target mark. Drop, DragLeave and
        // the drag-out's own finally all clear it, but a drag that STARTED in
        // another application and was cancelled with Esc while the cursor sat
        // over the tree gives this app no event at all - nothing tells us the
        // drag is over. Plain mouse movement can't happen during a drag (the
        // OLE loop raises Drag* events instead of Mouse*), so the first move
        // after one is proof it has ended.
        SetDropTarget(null);

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

            // Wherever this drag ended, it ended - so nothing is "about to be
            // dropped here" any more. TreeViewItem_Drop and ExplorerTree_
            // DragLeave each clear the mark on their own path; this covers the
            // rest, including a drag abandoned over the tree itself.
            SetDropTarget(null);
        }
    }

    // Which folder a drop on this row lands in: the row itself when it's a
    // folder, otherwise the folder the file sits in. The file fallback is the
    // same one 붙여넣기 has always used - a drop and a paste are the same "put
    // these here" gesture, and only one of them used to accept a file row.
    private static FileSystemItem? ResolveDropTargetFolder(FileSystemItem row)
        => row switch
        {
            { IsPlaceholder: true } or { IsShowMore: true } => null,
            { IsDirectory: true } => row,
            _ => row.Parent
        };

    // DragEnter/DragOver/Drop all bubble, and every TreeViewItem has its own
    // handler for them (see ExplorerTreeViewItemStyle) - e.Handled = true
    // here stops that same bubble from also reaching (and re-selecting) every
    // ancestor folder above whatever's actually under the cursor.
    private void TreeViewItem_DragOver(object sender, DragEventArgs e)
    {
        e.Handled = true;

        if (sender is not TreeViewItem { DataContext: FileSystemItem row } ||
            !e.Data.GetDataPresent(DataFormats.FileDrop) ||
            ResolveDropTargetFolder(row) is not { } target)
        {
            e.Effects = DragDropEffects.None;
            SetDropTarget(null);
            return;
        }

        e.Effects = DragDropEffects.Copy;

        // Marked with the same brushes the selection uses, so there's no
        // ambiguity about which folder a drop would land in - but as a state of
        // its own, NOT by moving the selection there (see
        // FileSystemItem.IsDropTarget for what that cost). Over a FILE row the
        // mark goes on the row ABOVE the cursor - its parent folder, the one
        // actually receiving the files - rather than the file being hovered,
        // which would read as "something happens to this file".
        SetDropTarget(target);
    }

    // Row-to-row movement inside the tree raises DragLeave too (it bubbles up
    // from the row being left), and clearing on that would drop the mark for
    // the instant before the next DragOver puts it back - a flicker on every
    // row crossed. So the mark only comes off when the cursor is genuinely
    // outside the tree, which is the case this handler exists for: a drag that
    // wanders off to another window, or out of the app entirely.
    private void ExplorerTree_DragLeave(object sender, DragEventArgs e)
    {
        var position = e.GetPosition(ExplorerTree);
        bool stillInside =
            position.X >= 0 && position.X <= ExplorerTree.ActualWidth &&
            position.Y >= 0 && position.Y <= ExplorerTree.ActualHeight;

        if (!stillInside)
        {
            SetDropTarget(null);
        }
    }

    // The one row currently marked as "a drop lands here". Held as a field
    // rather than searched for on each change because the mark has to come off
    // reliably: the row can be scrolled out and de-realized mid-drag, and a
    // mark left behind after the drag ends is a row that claims to be selected
    // and answers nothing.
    private FileSystemItem? _dropTargetItem;

    private void SetDropTarget(FileSystemItem? target)
    {
        if (ReferenceEquals(_dropTargetItem, target))
        {
            return;
        }

        if (_dropTargetItem is not null)
        {
            _dropTargetItem.IsDropTarget = false;
        }

        _dropTargetItem = target;

        if (target is not null)
        {
            target.IsDropTarget = true;
        }
    }

    private void TreeViewItem_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        SetDropTarget(null);

        if (sender is not TreeViewItem { DataContext: FileSystemItem row } ||
            e.Data.GetData(DataFormats.FileDrop) is not string[] droppedPaths ||
            ResolveDropTargetFolder(row) is not { } item)
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

        // Merge: a drop adds rows to the target folder, which may well have
        // expanded folders of its own on screen.
        RefreshFolderPreservingState(item);
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

        ReHideFoldersLeftBehind(e.NewValue as FileSystemItem);

        // Landing on a ROOT is the signature of the 2026-08-02 report ("C: went
        // to the top and got selected"): nothing the user does down in a subtree
        // should ever move the selection up to a drive row. Rare enough to log
        // unconditionally, and it shares scrolljump.log so it lines up with the
        // jump it came with.
        if (e.NewValue is FileSystemItem { Parent: null } root)
        {
            LogScrollLine(
                $"select root  {root.FullPath}  " +
                $"from={(e.OldValue as FileSystemItem)?.FullPath ?? "-"}  " +
                $"lastPress={_lastTreePressLabel}");
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

        // The viewer panel follows the selection (debounced - see the
        // method). A no-op while the panel is closed.
        ScheduleViewerPreview();
        // Not debounced: this one only appears or disappears, and waiting
        // 120ms to show a button the cursor may already be heading for reads
        // as lag rather than as smoothing.
        UpdateViewerExpandButton();

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

        // NOT behind the same guard: a bookmark jump runs the whole reveal walk
        // with that flag set, and clearing it raises no further selection
        // change - so gating this the way the favorites sync is gated would
        // leave the panel blank after every Ctrl+Alt+L, which is the one case
        // it most needs to answer. Re-marking the row it is already on costs
        // nothing.
        SyncBookmarkPanelToSelection();
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
        // visual tree do, so they are found by their Tag instead.
        //
        // This used to be one positional list pattern over the whole menu, and
        // it cost a shipped release: adding 잘라내기 to the menu shifted every
        // row after it, the pattern stopped matching, and the entire block below
        // went silently dead - no greyed 이름 바꾸기, no "N개 항목 선택됨", no
        // 압축 풀기 row, nothing (found 2026-07-30, in v1.3.4). Tags don't care
        // where a row sits or how many rows join it.
        if (sender is ContextMenu menu)
        {
            var thumbnailItem = FindTaggedMenuElement<MenuItem>(menu, "thumbnail");
            var thumbnailSeparator = FindTaggedMenuElement<Separator>(menu, "thumbnailSep");
            var multiInfoItem = FindTaggedMenuElement<MenuItem>(menu, "multiInfo");
            var multiInfoSeparator = FindTaggedMenuElement<Separator>(menu, "multiInfoSep");
            var addFavoriteItem = FindTaggedMenuElement<MenuItem>(menu, "addFavorite");
            var newFolderItem = FindTaggedMenuElement<MenuItem>(menu, "newFolder");
            var refreshItem = FindTaggedMenuElement<MenuItem>(menu, "refresh");
            var searchInFolderItem = FindTaggedMenuElement<MenuItem>(menu, "searchInFolder");
            var sortMenu = FindTaggedMenuElement<MenuItem>(menu, "sort");
            var openWithItem = FindTaggedMenuElement<MenuItem>(menu, "openWith");
            var compressItem = FindTaggedMenuElement<MenuItem>(menu, "compress");
            var extractItem = FindTaggedMenuElement<MenuItem>(menu, "extract");
            var renameItem = FindTaggedMenuElement<MenuItem>(menu, "rename");
            var copyPathItem = FindTaggedMenuElement<MenuItem>(menu, "copyPath");
            var openWithCodeItem = FindTaggedMenuElement<MenuItem>(menu, "openWithCode");

            if (thumbnailItem is null || thumbnailSeparator is null || multiInfoItem is null ||
                multiInfoSeparator is null || addFavoriteItem is null || newFolderItem is null ||
                refreshItem is null || searchInFolderItem is null || sortMenu is null ||
                openWithItem is null || compressItem is null || extractItem is null ||
                renameItem is null || copyPathItem is null || openWithCodeItem is null)
            {
                // A tag was renamed or dropped in the XAML. Debug builds say so
                // rather than leaving the menu quietly half-configured.
                LogClickLine("row menu: a tagged item is missing - menu half-configured");
                return;
            }

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

            // The bookmark submenu configures itself when it opens (see
            // BookmarkRowSubmenu_Opened) - its label depends on the row and is
            // read at the moment it is shown.

            // The zip lands next to the right-clicked row, so a drive root -
            // the one kind of row with no parent folder - has nowhere to put
            // it and is the only case this greys out.
            compressItem.IsEnabled =
                ExplorerTree.SelectedItem is FileSystemItem { IsPlaceholder: false, IsShowMore: false, Parent: not null };

            // Shown only on an actual .zip row, and never on a multi-selection
            // (unpacking several archives at once isn't offered).
            extractItem.Visibility =
                _multiSelection.Count <= 1 &&
                ExplorerTree.SelectedItem is FileSystemItem { IsPlaceholder: false, IsDirectory: false, Parent: not null } zipRow &&
                ArchiveService.IsZipPath(zipRow.FullPath)
                    ? Visibility.Visible
                    : Visibility.Collapsed;

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

            // Drive roots included: they hide exactly the way a folder does
            // (2026-08-02) - an unused drive is the noisiest thing a tree of
            // whole drives can carry, and the list is the way back for both.
            // Works on a MULTI-selection too since 2026-08-02 - it used to be
            // greyed out there. The way this feature actually gets used is
            // "clear away everything I never open", and one right-click per
            // folder was the entire cost of doing that. Files in the selection
            // are skipped, so the row only needs SOME folder to act on, and the
            // label says which it is - "이 폴더" is a lie with five rows picked.
            if (FindTaggedMenuElement<MenuItem>(menu, "hideFolder") is { } hideItem)
            {
                bool anyFolderSelected = _multiSelection
                    .Any(i => i is { IsPlaceholder: false, IsShowMore: false, IsDirectory: true });
                hideItem.IsEnabled = isMultiSelection ? anyFolderSelected : isFolder;
                hideItem.Header = isMultiSelection
                    ? Strings.MenuHideSelectedFolders
                    : Strings.MenuHideFolder;
            }

            // Deliberately NOT greyed out when nothing is hidden. It was, and a
            // disabled row that still shows its submenu arrow reads as broken
            // rather than as unavailable (user, 2026-08-02). There is also
            // something in there either way now: "숨긴 폴더 표시" leads the
            // submenu, and an empty list says so in words.

            // Only makes sense to reach for while looking at a folder. Shows
            // that folder's own override if it has one (GetEffectiveFolderSort),
            // otherwise the app-wide default - either way this submenu always
            // reflects what THIS folder would sort by if (re)loaded right now.
            sortMenu.IsEnabled = isFolder;
            ApplyFolderSortMenuState(sortMenu.Items, isFolder);

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

    // Menu rows are addressed by Tag rather than by position - see the note in
    // ExplorerItemContextMenu_Opened for what position-addressing cost.
    private static T? FindTaggedMenuElement<T>(ItemsControl menu, string tag) where T : FrameworkElement
        => menu.Items.OfType<T>().FirstOrDefault(item => (item.Tag as string) == tag);

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
    // Clicking the picture in the row menu opens it in the VIEWER rather than
    // handing it to whatever app owns the extension. The thumbnail IS the
    // image, so a click on it reads as "bigger, here", and the default app is
    // still one Enter or double-click away on the row itself. It replaced a
    // "뷰어에서 보기" row that said the same thing in words directly under the
    // picture (user, 2026-08-09) - and which had nothing left to do whenever
    // the panel was already open, since right-clicking a row selects it and
    // the viewer follows the selection.
    //
    // Only for what the viewer can actually draw: a video or a PDF can carry a
    // shell thumbnail while the panel would manage no more than its icon, so
    // those keep opening the way they always have.
    private void ThumbnailMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (ExplorerTree.SelectedItem is FileSystemItem { IsPlaceholder: false, IsDirectory: false } file &&
            ThumbnailExtensions.Contains(Path.GetExtension(file.FullPath)))
        {
            // Already open means the panel is already showing this row - the
            // selection took it there - so there is nothing further to do.
            OpenViewer();
            return;
        }

        OpenItem_Click(sender, e);
    }

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

    // The empty-space menu's 새로고침: the GLOBAL one, drive list included (a
    // plugged USB stick is exactly the kind of change you reach for a global
    // refresh over). Heaviest operation in the app, which is fine for an
    // explicit click.
    //
    // The viewport is put back by OFFSET, not left to the selection restore:
    // reloadRoots replaces the root instances, which throws the scroll state
    // away outright, and the selection reveal walk then parks the selected
    // row at the viewport's bottom edge - reported as the whole tree
    // "jumping to the bottom" on every refresh (2026-08-09). The tree
    // scrolls in pixels (ScrollUnit="Pixel"), so the saved offset is exact;
    // ContextIdle runs only after the reveal walk's Background-priority
    // chain has fully drained, so nothing scrolls after the restore.
    private void RefreshAll_Click(object sender, RoutedEventArgs e)
    {
        var scrollViewer = FindTreeScrollViewer();
        double offset = scrollViewer?.VerticalOffset ?? 0;

        RefreshAllLoadedFolders(pinSelectionToTop: false, reloadRoots: true);

        if (scrollViewer is not null)
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle,
                new Action(() => scrollViewer.ScrollToVerticalOffset(offset)));
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
            // Already watched - this runs again whenever the set of roots
            // changes (a drive coming back from the hidden list), and a second
            // watcher on the same drive would double every event.
            if (_driveWatchers.Any(w => string.Equals(w.Path, root.FullPath, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

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

        Dispatcher.BeginInvoke(() =>
        {
            QueueExternalRefresh(folderPath);
            NoteSearchScopeChanged(folderPath);
        });
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

    // ----- 북마크 (책갈피) --------------------------------------------------
    //
    // A lightweight "mark this row" - deliberately NOT navigation the way
    // favorites are: the marker (BookmarkMarker in the row template) makes a
    // row recognizable mid-scroll, persists until toggled off, and survives
    // restarts (AppSettings.BookmarkPaths, mirrored into the static
    // FileSystemService.BookmarkedPaths that item constructors consult, so
    // refresh-rebuilt rows come back already flagged). Ctrl+Alt+K toggles,
    // Ctrl+Alt+L/J cycle-jump - the VS Code Bookmarks extension's bindings,
    // chosen because that muscle memory is widespread.

    // ----- 네트워크 드라이브 연결 상태 -------------------------------------
    //
    // The badge on a network drive's rows is green while it answers and red
    // while it doesn't. Asking is a disk call - instant when a mapping is
    // disconnected, but seconds when the server is up and wedged - so it
    // happens on a timer, off the UI thread, once per DRIVE rather than per
    // row, and never while an earlier check is still out.
    private System.Windows.Threading.DispatcherTimer? _networkStatusTimer;
    private bool _networkStatusCheckRunning;

    private void StartNetworkRootStatusWatch()
    {
        _networkStatusTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _networkStatusTimer.Tick += (_, _) => UpdateNetworkRootStatus();
        _networkStatusTimer.Start();
        UpdateNetworkRootStatus();
    }

    private async void UpdateNetworkRootStatus()
    {
        if (_networkStatusCheckRunning)
        {
            return;
        }

        var networkRoots = _roots.Where(r => r.IsOnNetworkDrive).ToList();
        if (networkRoots.Count == 0)
        {
            return;
        }

        _networkStatusCheckRunning = true;
        try
        {
            var paths = networkRoots.Select(r => r.FullPath).ToList();
            var offline = await Task.Run(() => paths
                .Where(FileSystemService.RefreshNetworkRootState)
                .ToHashSet(StringComparer.OrdinalIgnoreCase));

            foreach (var root in networkRoots)
            {
                bool isOffline = offline.Contains(root.FullPath);
                if (root.IsNetworkDriveOffline != isOffline)
                {
                    // Only on a CHANGE, so the walk below costs nothing while
                    // a drive simply stays connected.
                    ApplyNetworkOfflineState(root, isOffline);
                }
            }
        }
        finally
        {
            _networkStatusCheckRunning = false;
        }
    }

    // The whole loaded subtree carries the drive's state, not just its root
    // row: rows deep in a scroll are exactly where "is this still live?" gets
    // asked, which is why the badge exists on them at all.
    //
    // Going offline also COLLAPSES the subtree. What was on screen was a
    // half-updated listing - rows that could no longer be re-read, gaps where
    // a merge had got partway - and every one of them was still a click that
    // handed the shell a dead path. Folding it up leaves one grey, red-dotted
    // drive row that says exactly what is true: this is not reachable right
    // now. The rows are not discarded, only closed, so re-expanding after the
    // drive returns costs nothing (user's call, 2026-07-26).
    private static void ApplyNetworkOfflineState(FileSystemItem item, bool isOffline)
    {
        item.IsNetworkDriveOffline = isOffline;
        if (isOffline)
        {
            item.IsExpanded = false;
        }
        if (!item.ChildrenLoaded)
        {
            return;
        }

        foreach (var child in item.Children)
        {
            if (child is { IsPlaceholder: false, IsShowMore: false })
            {
                // FILES take the flag too, not just folders - they are the
                // rows that most need it, since a click on one is what hands
                // the shell a dead path (see IsUnreachableNetworkItem). An
                // earlier version walked directories only, which left every
                // file looking live and still openable.
                child.IsNetworkDriveOffline = isOffline;
                if (child.IsDirectory)
                {
                    ApplyNetworkOfflineState(child, isOffline);
                }
            }
        }
    }

    // The 북마크 목록 submenu, picked out of the options menu on open - a
    // MenuItem living in a resource dictionary has no code-behind field of
    // its own. Null until that menu has been opened once.
    private MenuItem? _bookmarkListMenuItem;
    private ContextMenu? _optionsMenu;

    // Everything in that submenu whose width has to match the options menu
    // above it, filled while the rows are built and applied once the parent has
    // measured itself (BookmarkSubmenu_Opened).
    private readonly List<FrameworkElement> _bookmarkWidthTargets = new();

    // Where the Ctrl+Alt+L/J cycle currently stands in BookmarkPaths.
    private int _bookmarkCycleIndex = -1;

    // ----- 북마크 패널 (SidePanelMode == "bookmarks") -------------------------
    private readonly System.Collections.ObjectModel.ObservableCollection<BookmarkPanelRow> _bookmarkPanelRows = new();

    // Rebuilt whole rather than patched. The numbers ARE positions, so removing
    // one row renumbers every row under it regardless, and the list is small
    // enough that there is nothing to save by being clever about it.
    private void RebuildBookmarkPanelRows()
    {
        long startedAt = Environment.TickCount64;
        _bookmarkPanelRows.Clear();

        // ONE walk of the loaded tree for the whole list. It used to be one per
        // row (ResolveBookmarkPanelRow called EnumerateLoadedItems itself), and
        // that walk visits EVERY loaded item - so a rebuild cost bookmarks ×
        // loaded rows, on the UI thread, and a rebuild runs on every add and
        // every remove. On a deeply expanded tree with a handful of bookmarks
        // that is seconds of frozen window.
        var loadedKinds = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in EnumerateLoadedItems(_roots))
        {
            if (item is { IsPlaceholder: false, IsShowMore: false })
            {
                loadedKinds[item.FullPath] = item.IsDirectory;
            }
        }

        int number = 1;
        foreach (string path in _settings.BookmarkPaths)
        {
            var row = new BookmarkPanelRow(number++, path, BookmarkLeafName(path));
            _bookmarkPanelRows.Add(row);

            if (loadedKinds.TryGetValue(path, out bool isDirectory))
            {
                ApplyBookmarkPanelRowKind(row, isDirectory);
            }
            else
            {
                ResolveBookmarkPanelRowFromDisk(row);
            }
        }

        SyncBookmarkPanelToSelection();
        LogPanelLine($"bookmark panel rebuilt: {_bookmarkPanelRows.Count} rows, " +
            $"{loadedKinds.Count} loaded items walked, {Environment.TickCount64 - startedAt}ms");
    }

    // Debug-only. The window was killed by Windows as "not responding" during a
    // bookmark round on 2026-08-02 with no exception recorded anywhere, which
    // means the UI thread was busy rather than broken - so what is wanted next
    // time is how long this took and how big the walk was, not a stack.
    [System.Diagnostics.Conditional("DEBUG")]
    private static void LogPanelLine(string line)
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Edgetree");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, "panel.log"),
                $"{DateTime.Now:HH:mm:ss.fff}  {line}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    // revealPath: a bookmark that was just ADDED, which should end up on screen
    // even if the panel is shorter than the list. Same treatment favorites get
    // (see AddFavorite_Click) and for the same reasons - the very first one
    // sizes the panel because there is no height yet to disturb, and every one
    // after that slides in at the bottom instead of growing the panel, since
    // growing it shifts the whole tree under the cursor mid-click.
    private void RefreshBookmarkPanelIfShowing(string? revealPath = null)
    {
        if (!IsBookmarkPanelMode)
        {
            return;
        }

        bool wasEmpty = _bookmarkPanelRows.Count == 0;
        RebuildBookmarkPanelRows();

        // The row count is what the panel's height is sized against, and a
        // bookmark going or arriving changes it.
        UpdateFavoritesPanelVisibility();

        if (revealPath is null)
        {
            return;
        }

        var added = _bookmarkPanelRows.FirstOrDefault(r =>
            string.Equals(r.Path, revealPath, StringComparison.OrdinalIgnoreCase));
        if (added is null)
        {
            return;
        }

        if (wasEmpty)
        {
            FitFavoritesPanel();
            return;
        }

        // One dispatcher hop so the ListBox has generated the new row before
        // being asked to bring it on screen.
        Dispatcher.BeginInvoke(() => BookmarkPanelList.ScrollIntoView(added),
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    // Only for rows the tree could not answer for (see RebuildBookmarkPanelRows,
    // which asks it once for the whole list). Off the UI thread, because a
    // bookmark can sit on a network drive that has gone to sleep.
    private async void ResolveBookmarkPanelRowFromDisk(BookmarkPanelRow row)
    {
        bool? isDirectory = await Task.Run<bool?>(() =>
            FileSystemService.ProbeExists(row.Path, out bool directory) ? directory : null);

        // The list can have been rebuilt out from under this while the probe
        // ran - a toggle, a prune, a mode switch. Writing into an orphaned row
        // would be harmless but pointless; writing into a REPLACED one would
        // show nothing, which is the case actually worth guarding.
        if (!_bookmarkPanelRows.Contains(row))
        {
            return;
        }

        if (isDirectory is { } known)
        {
            ApplyBookmarkPanelRowKind(row, known);
            return;
        }

        // Nothing answered - the path is gone, or its drive is asleep. Either
        // way the row still needs an icon: leaving it blank is what made a
        // deleted file's leftover bookmark read as a rendering fault rather
        // than as an entry (2026-08-02). Guessed from the name, since that is
        // all there is to go on, and NOT pruned here - the rule for dropping a
        // bookmark stays "only when the volume testifies", which a silent probe
        // failure is not (see PruneMissingBookmarks, which does it properly at
        // startup).
        ApplyBookmarkPanelRowKind(row, isDirectory: !Path.HasExtension(row.Name));
    }

    private void ApplyBookmarkPanelRowKind(BookmarkPanelRow row, bool isDirectory)
    {
        row.IsDirectory = isDirectory;

        // Follows the same two toggles the tree does - someone who turned icons
        // off asked for that everywhere. The slot goes with the icon (the
        // template collapses on a null source), so names sit where they would
        // in the tree rather than behind an empty gap.
        if (!(isDirectory ? _settings.ShowFolderIcons : _settings.ShowFileIcons))
        {
            row.Icon = null;
            return;
        }

        if (isDirectory)
        {
            row.Icon = ShellIconService.GetFolderIcon(row.Name, isExpanded: false);
            return;
        }

        // Generic icon for the extension at once, the file's own (an .exe's, a
        // shortcut's) in the background - the callback is how that later
        // arrival gets picked up, and the re-read passes none of its own so it
        // can't queue another round.
        row.Icon = ShellIconService.GetFileIcon(row.Name, row.Path,
            () => row.Icon = ShellIconService.GetFileIcon(row.Name, row.Path, null));
    }

    // Marks the row the TREE is standing on, and selects it - the same contract
    // the favorites list has always had, which is the one that reads right.
    //
    // It was the cycle's own index at first, and that was wrong in both
    // directions (2026-08-02): selecting an ordinary file left the mark sitting
    // on a bookmark the tree was nowhere near, and selecting a bookmarked row
    // in the tree lit nothing, because the cycle had not moved. The cycle index
    // still exists - it is what Ctrl+Alt+L/J counts from - but it is not what
    // the panel shows.
    private void SyncBookmarkPanelToSelection()
    {
        if (!IsBookmarkPanelMode)
        {
            return;
        }

        string? selectedPath = (ExplorerTree.SelectedItem as FileSystemItem)?.FullPath.TrimEnd('\\');
        var match = selectedPath is null
            ? null
            : _bookmarkPanelRows.FirstOrDefault(r =>
                string.Equals(r.Path.TrimEnd('\\'), selectedPath, StringComparison.OrdinalIgnoreCase));

        foreach (var row in _bookmarkPanelRows)
        {
            row.IsCurrent = ReferenceEquals(row, match);
        }

        BookmarkPanelList.SelectedItem = match;
    }

    private void BookmarkPanelList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (ItemsControl.ContainerFromElement(BookmarkPanelList, (DependencyObject)e.OriginalSource)
            is not ListBoxItem { Content: BookmarkPanelRow row })
        {
            return;
        }

        // The cycle moves to the row that was clicked, so the next Ctrl+Alt+L
        // carries on from where the eye is instead of from wherever the cycle
        // last landed on its own. The MARK is not set here - the jump below
        // selects the row in the tree, and the mark follows that.
        _bookmarkCycleIndex = row.Number - 1;
        JumpToBookmarkPath(row.Path);
    }

    // So the context menu acts on the row under the cursor rather than on
    // whatever was selected before - same reason the favorites list does it.
    private void BookmarkPanelItem_PreviewMouseRightButtonDown(object sender, RoutedEventArgs e)
    {
        if (sender is ListBoxItem item)
        {
            item.IsSelected = true;
        }
    }

    private void RemoveBookmarkFromPanel_Click(object sender, RoutedEventArgs e)
    {
        if (BookmarkPanelList.SelectedItem is not BookmarkPanelRow row)
        {
            return;
        }

        RemoveBookmark(row.Path);
        RefreshBookmarkPanelIfShowing();
    }

    // 없는 경로 정리. A bookmark on a path that no longer exists is dead weight:
    // the cycle skips it silently (see JumpToBookmark), so it never announces
    // itself, it just makes Ctrl+Alt+L feel like it missed a press.
    //
    // The rule for removing one is deliberately narrow: ONLY when the volume
    // holding it can testify right now that it is gone. A sleeping NAS, an
    // unplugged drive or a disconnected mapping says nothing at all about
    // whether the folder still exists, and a bookmark deleted on that evidence
    // is gone for good - the exact reason bookmarks were left alone until now
    // (and the same principle as "read failure is not an empty folder", the
    // 2026-07-23 NAS incident).
    //
    // Runs off the UI thread: File.Exists against a path on a sleeping network
    // drive blocks for seconds, and this runs at startup where that would be a
    // visible freeze. Quiet by design - no prompt, no report - because the
    // only thing it ever removes is a bookmark that could not be reached in
    // the first place; debug builds record what went, in bookmarks.log.
    private async void PruneMissingBookmarks()
    {
        var candidates = _settings.BookmarkPaths.ToList();
        if (candidates.Count == 0)
        {
            return;
        }

        var missing = await Task.Run(() => candidates.Where(IsBookmarkConfirmedMissing).ToList());
        if (missing.Count == 0)
        {
            return;
        }

        foreach (string path in missing)
        {
            _settings.BookmarkPaths.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            FileSystemService.BookmarkedPaths.Remove(path);
        }

        // The cycle's position indexes into the list that just changed.
        _bookmarkCycleIndex = -1;
        _settingsService.Save(_settings);
        RefreshBookmarkPanelIfShowing();
        LogBookmarkPrune(missing);
    }

    private static bool IsBookmarkConfirmedMissing(string path)
    {
        try
        {
            if (File.Exists(path) || Directory.Exists(path))
            {
                return false;
            }

            // The evidence required is the PARENT FOLDER's own listing: the
            // entry is gone only if the folder that should hold it can be read
            // right now and doesn't. Asking the drive instead is not good
            // enough - DriveInfo.IsReady answers true for a mapped network
            // drive whose server is only half up (a NAS mid-reboot), and a
            // bookmark deleted on that answer is gone for good.
            string? parent = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(parent))
            {
                return false;
            }

            // The parent itself may be what was deleted - walk up to the first
            // level that still exists and let that one testify. Stops at the
            // root: a root that doesn't answer proves nothing at all.
            while (!Directory.Exists(parent))
            {
                string? grandParent = Path.GetDirectoryName(parent);
                if (string.IsNullOrEmpty(grandParent))
                {
                    return false;
                }
                parent = grandParent;
            }

            // Enumerating is the actual test: Directory.Exists can answer from
            // a cached handle, while listing has to reach the volume. Any
            // failure here means "couldn't tell", which keeps the bookmark.
            Directory.EnumerateFileSystemEntries(parent).Take(1).ToList();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException
                                       or NotSupportedException)
        {
            // Every uncertain answer keeps the bookmark. Only a folder that
            // answered, and did not contain it, removes one.
            return false;
        }
    }

    [System.Diagnostics.Conditional("DEBUG")]
    private static void LogBookmarkPrune(List<string> removed)
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Edgetree");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, "bookmarks.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  pruned {removed.Count} missing: {string.Join(" | ", removed)}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    // Built fresh every time the options menu opens: the marker on a row says
    // "this one", but nothing said how many there were or where the rest had
    // got to, and the Ctrl+Alt+L/J cycle only reveals them one at a time.
    //
    // Deliberately reads nothing from disk. Names come out of the path string
    // alone, so opening the menu can't stall on a sleeping network drive -
    // the same rule the jump cycle had to learn (see JumpToBookmark).
    private void RebuildBookmarkListMenu()
    {
        if (_bookmarkListMenuItem is not { } menu)
        {
            return;
        }

        menu.Items.Clear();
        _bookmarkWidthTargets.Clear();

        if (_settings.BookmarkPaths.Count == 0)
        {
            menu.Items.Add(FollowMenuFont(new MenuItem
            {
                Header = Strings.MenuBookmarkListEmpty,
                IsEnabled = false,
            }));
            return;
        }

        foreach (string path in _settings.BookmarkPaths.ToList())
        {
            menu.Items.Add(BuildBookmarkListRow(path));
        }

        // This list carried a three-line shortcut hint (added 2026-07-30 for
        // whoever had never made a bookmark, then trimmed twice) until the
        // user removed it outright (2026-08-08): the row context menu's 북마크
        // submenu already states the same gestures full-size, and a
        // small-print block here read as a different menu's furniture.
        menu.Items.Add(new Separator());

        menu.Items.Add(BuildListClearAllButton(
            Strings.MenuBookmarkClearAll,
            () => ClearAllBookmarks_Click(this, new RoutedEventArgs()),
            reserveMarkerColumn: true));
    }

    // 전체 해제 as a BUTTON rather than another row in the list.
    //
    // It never was one: every other entry in these menus is a place to go or a
    // single thing to release, and this is a one-shot that empties the whole
    // list. As a row it also sat in the very column the cursor travels down
    // while looking for one bookmark to drop (user, 2026-08-04). It takes the
    // chip style the per-row 해제 already uses, so a list has one button family
    // rather than two, and it keeps to the right end away from that column.
    //
    // The menu is closed BEFORE the confirmation appears. A menu holds the
    // mouse for as long as it is up, and standing a modal dialog on top of
    // that is the exact shape this app keeps a capture watchdog for - same
    // close-then-run order the context menu's keyboard shortcuts use.
    // reserveMarkerColumn: a bookmark row ends in the blue ribbon, not in its
    // 해제 chip, so the chips stop one glyph short of the right edge. The
    // ribbon's width follows the font (BookmarkMarkerHeight, stretched
    // uniformly), which is why a button simply pinned to the right edge sat
    // level at one zoom and drifted at the next. An invisible copy of the very
    // same glyph holds the very same space, so the two stay level at every
    // step without a number anywhere. The hidden-folder list has no such
    // column and asks for none.
    private MenuItem BuildListClearAllButton(string text, Action clear, bool reserveMarkerColumn = false)
    {
        var button = new Button
        {
            Content = text,
            Style = (Style)FindResource("MenuRowActionButtonStyle"),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new Thickness(0, 4, 0, 4),
        };

        button.Click += (_, _) =>
        {
            foreach (var open in _openMenus.ToList())
            {
                open.IsOpen = false;
            }

            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, clear);
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(button, 1);
        grid.Children.Add(button);

        if (reserveMarkerColumn)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Hidden, not Collapsed: the point of it is the space it takes.
            var spacer = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("F1 M240-144v-672h480v672l-240-96-240 96Z"),
                Stretch = Stretch.Uniform,
                Margin = new Thickness(8, 0, 0, 0),
                Visibility = Visibility.Hidden,
            };
            spacer.SetResourceReference(HeightProperty, "BookmarkMarkerHeight");
            Grid.SetColumn(spacer, 2);
            grid.Children.Add(spacer);
        }

        // Carried in a row of its own (MenuButtonRowStyle) rather than handed
        // to the menu bare: WPF would wrap it in an ordinary MenuItem, and
        // that row highlights under the pointer as though all of it were the
        // target. The row also supplies the menu's padding, which is what puts
        // this column on the same grid as the rows above it.
        return new MenuItem
        {
            Header = grid,
            Style = (Style)FindResource("MenuButtonRowStyle"),
        };
    }

    // The submenu's rows are made to span the options menu's own width, so the
    // popup lines up with the menu it hangs off rather than sizing itself to
    // whatever names happen to be bookmarked. Done here rather than at build
    // time because the parent hasn't measured itself yet when its Opened fires.
    private void BookmarkSubmenu_Opened(object sender, RoutedEventArgs e)
    {
        if (_optionsMenu is not { ActualWidth: > 0 } parent)
        {
            return;
        }

        // Applied to the ITEMS, not to what's inside them: a popup is as wide
        // as its widest item plus the menu's own padding and border, so an item
        // sized to the parent's width less that same trim produces a submenu
        // exactly as wide as the menu it hangs off. Sizing the content instead
        // left each row short by its own padding.
        // Both templates frame their visible border with a transparent 10px
        // margin for the drop shadow to render into (see the ContextMenu
        // template and PART_Popup in MainWindow.xaml). ActualWidth counts that
        // room, the eye doesn't - so it comes off first, or the submenu ends up
        // exactly one shadow-frame wider than the menu it hangs off.
        const double ShadowBleed = 10;

        double inset = 2 * ShadowBleed + 2;
        if (Application.Current.Resources["MenuPadding"] is Thickness menuPadding)
        {
            inset += menuPadding.Left + menuPadding.Right;
        }

        double width = Math.Max(80, parent.ActualWidth - inset);
        foreach (var target in _bookmarkWidthTargets)
        {
            target.Width = width;
        }
    }

    // Rows declared in XAML inherit the menu's font from DarkContextMenuStyle,
    // which is where the Ctrl +/- zoom reaches menus at all. Ones built in code
    // sit in a submenu popup that inheritance doesn't cross, so they came up in
    // the system's default menu font - visibly larger than everything around
    // them. A resource reference rather than a copied value, so they keep
    // following the zoom while the menu is open.
    private static MenuItem FollowMenuFont(MenuItem item)
    {
        item.SetResourceReference(FontSizeProperty, "MenuFontSize");
        return item;
    }

    // `[아이콘] 이름 … [−] [책갈피]`, at the options menu's own width (applied by
    // BookmarkSubmenu_Opened). Content-fit was tried first and is worse here:
    // removing one long name visibly shrinks the whole popup, so the list
    // appears to move under the cursor mid-cleanup. The full path is the
    // tooltip.
    // trackWidth applies only to the options menu's copy, whose rows are
    // stretched to that menu's width when it opens. The tree row menu's copy
    // sizes to its own content - it hangs off a submenu with nothing to line up
    // with, and the names are capped anyway.
    private MenuItem BuildBookmarkListRow(string path, bool trackWidth = true)
    {
        var row = FollowMenuFont(new MenuItem
        {
            ToolTip = path,
            // Neither the remove button nor a jump closes this menu: WPF would
            // shut it on any click inside, and the point of the list is to keep
            // looking. Clicking outside still dismisses it as usual.
            StaysOpenOnClick = true,
        });

        if (trackWidth)
        {
            _bookmarkWidthTargets.Add(row);
        }

        var grid = new Grid();
        // icon | name | remove | bookmark glyph
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Same size the tree's own rows use, so the two read as one thing at
        // every zoom step. Empty until the row's kind is known - see
        // ResolveBookmarkRow - and the slot is held either way, so nothing
        // shifts sideways when an icon does arrive.
        var icon = new System.Windows.Controls.Image
        {
            Stretch = Stretch.Uniform,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
            SnapsToDevicePixels = true,
        };
        icon.SetResourceReference(WidthProperty, "IconSize");
        icon.SetResourceReference(HeightProperty, "IconSize");
        Grid.SetColumn(icon, 0);
        grid.Children.Add(icon);

        // The name alone. A parent folder used to sit alongside it, to tell two
        // same-named rows apart - built, tried, and dropped (2026-07-28): it
        // read as clutter rather than as a distinction, and the tooltip already
        // answers "which one is this" on demand. Trimmed to whatever the fixed
        // row width leaves it.
        var name = new TextBlock
        {
            Text = BookmarkLeafName(path),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
        };
        // Readable from the first frame, before the row's kind is known and the
        // folder/file colour replaces this. Without it the row would render in
        // TextBlock's own default - black, i.e. invisible here.
        name.SetResourceReference(TextBlock.ForegroundProperty, "MenuForeground");
        ResolveBookmarkRow(icon, name, path);
        Grid.SetColumn(name, 1);
        grid.Children.Add(name);

        var remove = new Button
        {
            Content = Strings.MenuListRowRemove,
            Style = (Style)FindResource("MenuRowActionButtonStyle"),
            ToolTip = Strings.MenuBookmarkRemove,
            Margin = new Thickness(14, 0, 0, 0),
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
        };
        remove.Click += (_, args) =>
        {
            // Consumed so the row underneath doesn't also run and jump to the
            // bookmark being removed. With StaysOpenOnClick the list stays up,
            // which is the point - several can go in one visit.
            args.Handled = true;
            RemoveBookmark(path);
            DropBookmarkListRow(row);
        };
        Grid.SetColumn(remove, 2);
        grid.Children.Add(remove);

        // The same ribbon that marks the row in the tree, repeated on every
        // line. Redundant by design: the list is reached through a menu that
        // says "북마크 목록" and then never says it again, and one glance at a
        // familiar glyph settles what these rows are faster than reading does.
        // Last in the row, so it sits where the eye already looks for it - the
        // tree keeps its marker hard against the right edge too.
        var marker = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("F1 M240-144v-672h480v672l-240-96-240 96Z"),
            Fill = new SolidColorBrush(Color.FromRgb(0x4A, 0x90, 0xE2)),
            Stretch = Stretch.Uniform,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
        };
        marker.SetResourceReference(HeightProperty, "BookmarkMarkerHeight");
        Grid.SetColumn(marker, 3);
        grid.Children.Add(marker);

        row.Header = grid;
        row.Click += (_, args) =>
        {
            // Deliberately leaves the menu open. The tree moves behind it, so
            // the next bookmark is one more click away rather than four -
            // checking several in a row is the reason to open this list at all.
            args.Handled = true;
            JumpToBookmarkPath(path);
        };
        return row;
    }

    // ----- 폴더 숨기기 -------------------------------------------------------
    //
    // Takes a folder out of the TREE only. The file search still indexes and
    // finds what is inside it, because a search is a deliberate act of looking
    // and "분명 있는데 검색이 안 된다" is the worse surprise (decided
    // 2026-08-02, with the user). The list below is not a nicety either: hiding
    // a folder removes the only row you could have right-clicked to get it
    // back, so the two ship together or not at all.
    // Hides everything selected, not just the row that was right-clicked. The
    // way this feature is actually used is "hide all the folders I never open
    // and keep the few I do" (2026-08-02), and doing that one right-click at a
    // time is the whole cost of it - a tree ends up with dozens hidden. Every
    // other row operation already works off GetEffectiveSelection; this was the
    // only one still reading the single native selection.
    private void HideFolder_Click(object sender, RoutedEventArgs e)
    {
        var folders = GetEffectiveSelection()
            .Where(i => i is { IsPlaceholder: false, IsShowMore: false, IsDirectory: true })
            .ToList();

        // A folder whose ancestor is going as well would only add a list entry
        // that hides nothing - its rows are already leaving with the ancestor,
        // and the user would then have to clear two entries to get one folder
        // back.
        var covered = folders
            .Where(f => folders.Any(other => !ReferenceEquals(other, f) && IsSelfOrDescendant(f, other)))
            .ToHashSet();

        foreach (var folder in folders)
        {
            if (!covered.Contains(folder))
            {
                HideFolder(folder, save: false);
            }
        }

        // Once, not per folder: hiding 30 rows wrote settings.json 30 times.
        _settingsService.Save(_settings);

        // The set refers to rows that are gone now, so leaving it would keep a
        // selection nobody can see - and the next operation would run on it.
        ClearMultiSelection();
    }

    // Rebuilds the drive rows, keeping whatever was expanded and selected -
    // used when a hidden DRIVE comes back, where there is no parent listing to
    // merge into the way an ordinary folder has. Roots are few and cheap to
    // re-read; everything below them is replayed by path, the same two-pass
    // trick RefreshAllLoadedFolders uses for the same reason.
    private void ReloadRoots()
    {
        _roots.Clear();
        foreach (var root in FileSystemService.GetDriveRoots())
        {
            _roots.Add(root);
        }

        // A drive that was hidden had no watcher (they are made per ROOT), so
        // its live updates would otherwise stay dead until the next launch.
        StartDriveWatchers();
    }

    // save: false lets a batch (HideFolder_Click) write settings once at the end
    // instead of once per folder.
    private void HideFolder(FileSystemItem folder, bool save = true)
    {
        string path = FileSystemService.NormalizeHiddenPath(folder.FullPath);
        if (!FileSystemService.HiddenPaths.Add(path))
        {
            return;
        }

        _settings.HiddenFolderPaths.Add(path);

        // A folder hidden while it is the current selection would leave the
        // tree selected on a row that no longer exists - and the favorites
        // panel syncing to it. Move up to its parent, which is where the eye
        // already is once the row goes.
        bool selectionWasInside =
            ExplorerTree.SelectedItem is FileSystemItem selected && IsSelfOrDescendant(selected, folder);

        if (folder.Parent is { } parent)
        {
            if (selectionWasInside)
            {
                parent.IsSelected = true;
            }

            // Removed in place rather than by re-reading the parent from disk:
            // the listing is already correct apart from this one row, and a
            // reload would collapse whatever else is open under that parent
            // (the defect found 2026-07-25 when every file operation still
            // rebuilt).
            parent.Children.Remove(folder);
        }
        else
        {
            // A drive root: no parent listing to take it out of, so it leaves
            // the roots collection instead. Observable, so the tree follows on
            // its own. Selection moves to whatever drive is left rather than
            // staying on a row that no longer exists.
            _roots.Remove(folder);
            if (selectionWasInside && _roots.FirstOrDefault() is { } firstRoot)
            {
                firstRoot.IsSelected = true;
            }
        }

        if (save)
        {
            _settingsService.Save(_settings);
        }
    }

    private void UnhideFolder(string path)
    {
        path = FileSystemService.NormalizeHiddenPath(path);
        FileSystemService.HiddenPaths.Remove(path);
        FileSystemService.TemporarilyVisiblePaths.Remove(path);
        _settings.HiddenFolderPaths.RemoveAll(
            p => string.Equals(FileSystemService.NormalizeHiddenPath(p), path, StringComparison.OrdinalIgnoreCase));
        _settingsService.Save(_settings);

        // The row may already be on screen - "숨긴 폴더 표시" is on, or a jump is
        // passing through it - and in that case it is an EXISTING instance that
        // read its flag in its constructor. Merges reuse instances by name, so
        // nothing would ever clear it and the folder went on looking hidden
        // after being released (reported 2026-08-02, with everything unhidden
        // and one row still italic). Same reason RemoveBookmark walks the tree.
        foreach (var item in EnumerateLoadedItems(_roots))
        {
            if (string.Equals(FileSystemService.NormalizeHiddenPath(item.FullPath), path, StringComparison.OrdinalIgnoreCase))
            {
                item.IsHiddenFolderShown = false;
                return;
            }
        }

        // Not on screen at all, so the row has to come back from disk. Merge
        // rather than rebuild, so the rest of the parent's open subtree stays.
        if (System.IO.Path.GetDirectoryName(path) is { } parentPath)
        {
            if (FindItemForPath(parentPath) is { ChildrenLoaded: true } parent)
            {
                RefreshFolderPreservingState(parent);
            }

            return;
        }

        // No parent path at all means a drive root - it belongs to the roots
        // collection, so the whole set is rebuilt and everything that was open
        // is replayed onto it.
        RefreshAllLoadedFolders(pinSelectionToTop: false, reloadRoots: true);
    }

    // A jump has to be able to land inside a folder the user hid: a search
    // result, bookmark or favorite in there is still reachable by design (the
    // search deliberately does not filter), and a click that silently went
    // nowhere would be the worst of both. So the hidden folders along the way
    // are put back on screen for the trip, and they leave again on their own
    // once the selection is no longer inside them - "숨긴 폴더는 지금 그 안에
    // 있는 동안만 보인다", one rule covering every jump route rather than a
    // decision per caller (agreed 2026-08-02).
    private void RevealHiddenFoldersOnPathTo(string targetPath)
    {
        if (FileSystemService.HiddenPaths.Count == 0)
        {
            return;
        }

        // Shallowest first: a hidden folder's row can only be brought back once
        // its own parent is listed, and one hidden folder can sit inside
        // another.
        var chain = new List<string>();
        for (var current = FileSystemService.NormalizeHiddenPath(targetPath);
             !string.IsNullOrEmpty(current);
             current = System.IO.Path.GetDirectoryName(current) ?? string.Empty)
        {
            chain.Insert(0, current);
        }

        foreach (string path in chain)
        {
            if (!FileSystemService.HiddenPaths.Contains(path) ||
                !FileSystemService.TemporarilyVisiblePaths.Add(path))
            {
                continue;
            }

            // The row was never built - it has to come back from disk. Merge,
            // so nothing else open under that parent collapses.
            if (System.IO.Path.GetDirectoryName(path) is { } parentPath &&
                FindItemForPath(parentPath) is { ChildrenLoaded: true } parent)
            {
                RefreshFolderPreservingState(parent);
            }
        }
    }

    private void ReHideFoldersLeftBehind(FileSystemItem? selected)
    {
        if (FileSystemService.TemporarilyVisiblePaths.Count == 0)
        {
            return;
        }

        string? selectedPath = selected is null
            ? null
            : FileSystemService.NormalizeHiddenPath(selected.FullPath);

        foreach (string path in FileSystemService.TemporarilyVisiblePaths.ToList())
        {
            // Still in there (the folder itself, or anything under it) - leave
            // it alone. Compared as paths rather than by walking parents,
            // because the row for a temporarily-visible folder can be replaced
            // by a refresh while the user is inside it.
            if (selectedPath is not null &&
                (selectedPath.Equals(path, StringComparison.OrdinalIgnoreCase) ||
                 selectedPath.StartsWith(path + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            FileSystemService.TemporarilyVisiblePaths.Remove(path);

            if (FindItemForPath(path) is { } item && item.Parent is { } parent)
            {
                parent.Children.Remove(item);
            }
        }
    }

    // Built fresh on every open, like the bookmark list - the contents are the
    // user's own hidden folders, so there is nothing to declare in XAML. The
    // same handler serves the copy in the row menu and the copy in the options
    // menu; `sender` says which one is asking.
    private void HiddenFolderSubmenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem submenu)
        {
            AppendHiddenFolderListTo(submenu);
        }
    }

    private void AppendHiddenFolderListTo(MenuItem submenu)
    {
        submenu.Items.Clear();

        // A "숨긴 폴더 표시" toggle led this list for a while and was taken out
        // again (2026-08-02, user's call): "숨겼는데 또 별도로 보여준다"의
        // 개념이 애매하다 - hidden is hidden, and a persistent switch that
        // un-hides everything without unhiding it is a third state to hold in
        // your head. If it comes back it should be a LIVE peek - held down to
        // reveal, gone on release - which is a different thing from a setting.
        // "어디로 갔지"는 이 목록이 답한다.
        //
        // Keeping SOMETHING in here matters beyond looks: a MenuItem whose
        // Items go to zero reports HasItems=false, WPF stops opening a popup
        // for it at all, and SubmenuOpened - the only thing that could refill
        // it - never fires again, so the row is dead until the app restarts.
        // Releasing the last hidden folder used to leave exactly that.
        if (_settings.HiddenFolderPaths.Count == 0)
        {
            submenu.Items.Add(FollowMenuFont(new MenuItem
            {
                Header = Strings.MenuHiddenFolderListEmpty,
                IsEnabled = false,
            }));
            return;
        }

        foreach (string path in _settings.HiddenFolderPaths.ToList())
        {
            submenu.Items.Add(BuildHiddenFolderRow(path));
        }

        // Same closing row the bookmark list has, for the same reason: releasing
        // a dozen folders one at a time is the case a list is supposed to spare
        // you. Right-aligned and asked about (with the count) exactly like that
        // one - a per-row 해제 undoes something you are looking straight at,
        // while this throws away a list that may have taken a while to build.
        submenu.Items.Add(new Separator());
        submenu.Items.Add(BuildListClearAllButton(
            Strings.MenuBookmarkClearAll, () => ClearAllHiddenFolders_Click(this, new RoutedEventArgs())));
    }

    private void ClearAllHiddenFolders_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            this,
            string.Format(Strings.HiddenClearAllConfirmBody, _settings.HiddenFolderPaths.Count),
            Strings.HiddenClearAllConfirmTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        // One at a time through the same path a single 해제 takes, so each
        // folder's row comes back the same way and nothing needs a second
        // implementation of "put it back".
        foreach (string path in _settings.HiddenFolderPaths.ToList())
        {
            UnhideFolder(path);
        }
    }

    // `[폴더 아이콘] 이름 … [−]`, the bookmark list's row minus the jump and the
    // ribbon. No disk probe for the icon either: every row here is a folder by
    // construction, where a bookmark could be either and had to ask.
    private MenuItem BuildHiddenFolderRow(string path)
    {
        var row = FollowMenuFont(new MenuItem
        {
            ToolTip = path,
            // Nothing to run by clicking the row itself - the folder is hidden,
            // so there is nowhere to jump to. StaysOpenOnClick keeps a stray
            // click from closing the list mid-cleanup all the same.
            StaysOpenOnClick = true,
        });

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = new System.Windows.Controls.Image
        {
            Source = Resources["FavoriteFolderIconSource"] as ImageSource,
            Stretch = Stretch.Uniform,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
            SnapsToDevicePixels = true,
        };
        icon.SetResourceReference(WidthProperty, "IconSize");
        icon.SetResourceReference(HeightProperty, "IconSize");
        Grid.SetColumn(icon, 0);
        grid.Children.Add(icon);

        var name = new TextBlock
        {
            Text = BookmarkLeafName(path),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
        };
        name.SetResourceReference(TextBlock.ForegroundProperty, "FolderNameForeground");
        Grid.SetColumn(name, 1);
        grid.Children.Add(name);

        var unhide = new Button
        {
            Content = Strings.MenuListRowRemove,
            Style = (Style)FindResource("MenuRowActionButtonStyle"),
            ToolTip = Strings.MenuUnhideFolder,
            Margin = new Thickness(14, 0, 0, 0),
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
        };
        unhide.Click += (_, args) =>
        {
            args.Handled = true;
            UnhideFolder(path);

            // From whichever menu this row actually belongs to - the list is
            // built into two different menus, and a hardcoded host quietly did
            // nothing in the other one when the bookmark list first learned
            // this (2026-07-31).
            if (ItemsControl.ItemsControlFromItemContainer(row) is MenuItem host)
            {
                // Rebuilt rather than just dropping the row: taking the last one
                // out would leave the submenu empty, which is a state it never
                // comes back from (see AppendHiddenFolderListTo).
                AppendHiddenFolderListTo(host);
            }
        };
        Grid.SetColumn(unhide, 2);
        grid.Children.Add(unhide);

        row.Header = grid;
        return row;
    }

    private static bool IsSelfOrDescendant(FileSystemItem item, FileSystemItem ancestor)
    {
        for (var current = item; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    // Icon and name colour both hang on one question - is this a folder or a
    // file - and that question costs a disk call, which this menu makes a point
    // of not making (see RebuildBookmarkListMenu).
    //
    // So rows already present in the tree answer it instantly and for free,
    // and only the rest are asked in the background. Those show no icon and
    // the inherited menu colour until the answer arrives: immediate on a live
    // drive, never at all on a sleeping one - and in neither case does the
    // menu wait.
    private void ResolveBookmarkRow(System.Windows.Controls.Image icon, TextBlock name, string path)
    {
        foreach (var item in EnumerateLoadedItems(_roots))
        {
            if (string.Equals(item.FullPath, path, StringComparison.OrdinalIgnoreCase))
            {
                ApplyBookmarkRowKind(icon, name, path, item.IsDirectory);
                return;
            }
        }

        ResolveBookmarkRowFromDisk(icon, name, path);
    }

    private async void ResolveBookmarkRowFromDisk(System.Windows.Controls.Image icon, TextBlock name, string path)
    {
        // ProbeExists carries the cycle's own guard against a dead network
        // root - the first wait is remembered so the rest give up at once.
        bool? isDirectory = await Task.Run<bool?>(() =>
            FileSystemService.ProbeExists(path, out bool directory) ? directory : null);

        if (isDirectory is { } known)
        {
            ApplyBookmarkRowKind(icon, name, path, known);
        }
    }

    private void ApplyBookmarkRowKind(System.Windows.Controls.Image icon, TextBlock name, string path, bool isDirectory)
    {
        // The tree's own name colours rather than the menu's grey: that grey
        // was picked against the tree's background and sits too close to the
        // menu's darker surface to read.
        name.SetResourceReference(TextBlock.ForegroundProperty,
            isDirectory ? "FolderNameForeground" : "FileNameForeground");

        // Follows the same two toggles the tree does. Someone who turned icons
        // off asked for that everywhere, not just in the tree - and the slot
        // goes with them, so the names sit where they would in the tree rather
        // than behind an empty gap.
        if (!(isDirectory ? _settings.ShowFolderIcons : _settings.ShowFileIcons))
        {
            icon.Visibility = Visibility.Collapsed;
            return;
        }

        string leaf = BookmarkLeafName(path);
        if (isDirectory)
        {
            icon.Source = ShellIconService.GetFolderIcon(leaf, isExpanded: false);
            return;
        }

        // Returns a generic icon for the extension at once and fetches the
        // file's own (an .exe's, a shortcut's) in the background - the callback
        // is how that later arrival gets picked up. The re-read passes no
        // callback of its own, so it can't queue another round.
        icon.Source = ShellIconService.GetFileIcon(leaf, path,
            () => icon.Source = ShellIconService.GetFileIcon(leaf, path, null));
    }

    private void DropBookmarkListRow(MenuItem row)
    {
        // Whichever menu this row actually belongs to - the list is built into
        // the options menu AND into the tree row menu's 북마크 submenu, and
        // removing it from a hardcoded host quietly did nothing in the other
        // one: the bookmark went, the row stayed, and "−" looked broken
        // (2026-07-31).
        if (ItemsControl.ItemsControlFromItemContainer(row) is { } host)
        {
            host.Items.Remove(row);
        }
        else
        {
            _bookmarkListMenuItem?.Items.Remove(row);
        }

        // Nothing left but the separator and "전체 해제", which now have
        // nothing to act on.
        if (_settings.BookmarkPaths.Count == 0)
        {
            RebuildBookmarkListMenu();
            if (_bookmarkRowSubmenu is { } submenu)
            {
                // Strips the separator this list left behind - it takes the
                // tagged rows out and adds nothing back while the list is empty.
                AppendBookmarkListTo(submenu);
            }
        }
    }

    private static string BookmarkLeafName(string path)
    {
        string trimmed = path.TrimEnd('\\');
        string name = Path.GetFileName(trimmed);
        // A drive root has no file name of its own - show the root itself.
        return string.IsNullOrEmpty(name) ? path : name;
    }

    // Same destination as a Ctrl+Alt+L landing, minus the search for which one
    // is next. Whether the target is a folder decides the route, and that
    // question costs a disk call - off the UI thread, since a bookmark can
    // point at a network drive that has gone to sleep.
    private async void JumpToBookmarkPath(string path)
    {
        bool isDirectory = await Task.Run(() =>
        {
            try
            {
                return Directory.Exists(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException
                                           or NotSupportedException)
            {
                return false;
            }
        });

        SetSearchViewActive(false);
        NavigateToPath(path, source: "bookmark-list");
    }

    private void RemoveBookmark(string path)
    {
        _settings.BookmarkPaths.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        FileSystemService.BookmarkedPaths.Remove(path);

        foreach (var item in EnumerateLoadedItems(_roots))
        {
            if (string.Equals(item.FullPath, path, StringComparison.OrdinalIgnoreCase))
            {
                item.IsBookmarked = false;
                break;
            }
        }

        // The cycle's position indexes into the list that just changed.
        _bookmarkCycleIndex = -1;
        _settingsService.Save(_settings);
        RefreshBookmarkPanelIfShowing();
    }

    private void ClearAllBookmarks_Click(object sender, RoutedEventArgs e)
    {
        // Asked about, unlike the per-row "−" beside it. That one drops a
        // single bookmark the user is looking straight at and can put back in
        // a click; this one throws away a list they may have built over weeks,
        // with nothing to undo it. The count is in the question because it is
        // the part worth knowing before answering. Same treatment as "모든
        // 펼친 폴더 접기", the other one-shot in this menu.
        var result = MessageBox.Show(
            this,
            string.Format(Strings.BookmarkClearAllConfirmBody, _settings.BookmarkPaths.Count),
            Strings.BookmarkClearAllConfirmTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        foreach (var item in EnumerateLoadedItems(_roots))
        {
            item.IsBookmarked = false;
        }

        _settings.BookmarkPaths.Clear();
        FileSystemService.BookmarkedPaths.Clear();
        _bookmarkCycleIndex = -1;
        _settingsService.Save(_settings);
        RefreshBookmarkPanelIfShowing();
    }

    // Rows that already exist in the tree, and only those - no lazy loading.
    // FindItemForPath would walk down to a path, reading each level from disk
    // on the way, which is the last thing a bookmark pointing at an absent
    // network drive should trigger.
    private static IEnumerable<FileSystemItem> EnumerateLoadedItems(IEnumerable<FileSystemItem> items)
    {
        foreach (var item in items)
        {
            yield return item;
            foreach (var child in EnumerateLoadedItems(item.Children))
            {
                yield return child;
            }
        }
    }

    // The 북마크 submenu, set up as it opens: the first row says which way the
    // toggle would go for the row under the cursor, and the two jumps only need
    // a bookmark to exist somewhere - so they stay live even on a
    // multi-selection, where the toggle greys out like rename/경로 복사 do.
    //
    // Reading the state HERE is the point. The same code ran from the context
    // menu's own Opened handler when this was a single row, and it silently
    // stopped once the row became a submenu - the label stayed on 북마크 추가 for
    // rows that were already bookmarked (2026-07-30).
    private void BookmarkRowSubmenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem submenu)
        {
            return;
        }

        // By tag, not by position - the list appended below adds rows to this
        // very submenu, so a positional match would break the moment a bookmark
        // exists. (Addressing menu rows by position is what killed the whole
        // context-menu setup block a day earlier.)
        var toggle = FindTaggedMenuElement<MenuItem>(submenu, "bookmarkToggle");
        var prev = FindTaggedMenuElement<MenuItem>(submenu, "bookmarkPrev");
        var next = FindTaggedMenuElement<MenuItem>(submenu, "bookmarkNext");
        if (toggle is null || prev is null || next is null)
        {
            LogClickLine("bookmark submenu: a tagged item is missing");
            return;
        }

        bool isMultiSelection = _multiSelection.Count > 1;
        var selected = ExplorerTree.SelectedItem as FileSystemItem;

        toggle.IsEnabled = !isMultiSelection && selected is { IsPlaceholder: false, IsShowMore: false };
        toggle.Header = selected is { IsBookmarked: true }
            ? Strings.MenuBookmarkRemove
            : Strings.MenuBookmarkAdd;

        bool hasBookmarks = _settings.BookmarkPaths.Count > 0;
        prev.IsEnabled = hasBookmarks;
        next.IsEnabled = hasBookmarks;

        _bookmarkRowSubmenu = submenu;
        AppendBookmarkListTo(submenu);
    }

    // The tree row menu's 북마크 submenu, once it has been opened - so removing
    // the last bookmark from it can also take away the separator that list left
    // behind. Null until then.
    private MenuItem? _bookmarkRowSubmenu;

    // The list, inline under the three actions rather than behind a further
    // submenu of its own: a third level means crossing two popup boundaries
    // with the mouse, and this list is meant to be kept open while several
    // entries are checked in a row. The MAIN context menu doesn't grow from
    // this - only the submenu someone deliberately opened does.
    private void AppendBookmarkListTo(MenuItem submenu)
    {
        // Last time's list, identified by its tag rather than by counting from
        // the end - so adding a fourth action row later can't start deleting
        // actions instead.
        for (int i = submenu.Items.Count - 1; i >= 0; i--)
        {
            if (submenu.Items[i] is FrameworkElement { Tag: BookmarkListRowTag })
            {
                submenu.Items.RemoveAt(i);
            }
        }

        if (_settings.BookmarkPaths.Count == 0)
        {
            return;
        }

        submenu.Items.Add(new Separator { Tag = BookmarkListRowTag });
        foreach (string path in _settings.BookmarkPaths.ToList())
        {
            var row = BuildBookmarkListRow(path, trackWidth: false);
            row.Tag = BookmarkListRowTag;
            submenu.Items.Add(row);
        }

        // The same 전체 해제 the options menu's copy of this list carries. It
        // was missing here alone, which made the two routes to the same list
        // disagree about what could be done with it - and the hidden-folder
        // list, whose one builder serves both menus, never had that problem
        // (user, 2026-08-04). Tagged like the rows so the next open clears it
        // instead of stacking a second one.
        var clearAll = BuildListClearAllButton(
            Strings.MenuBookmarkClearAll,
            () => ClearAllBookmarks_Click(this, new RoutedEventArgs()),
            reserveMarkerColumn: true);
        clearAll.Tag = BookmarkListRowTag;
        submenu.Items.Add(clearAll);
    }

    private const string BookmarkListRowTag = "bookmarkListRow";

    // The two jumps, from the context menu's 북마크 submenu - the same calls
    // Ctrl+Alt+L / Ctrl+Alt+J make, so both routes behave identically.
    private void BookmarkNext_Click(object sender, RoutedEventArgs e)
        => JumpToBookmark(+1);

    private void BookmarkPrev_Click(object sender, RoutedEventArgs e)
        => JumpToBookmark(-1);

    private void BookmarkMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (ExplorerTree.SelectedItem is FileSystemItem { IsPlaceholder: false, IsShowMore: false } item)
        {
            ToggleBookmark(item);
        }
    }

    private void ToggleBookmark(FileSystemItem item)
    {
        bool added;
        if (FileSystemService.BookmarkedPaths.Remove(item.FullPath))
        {
            item.IsBookmarked = false;
            _settings.BookmarkPaths.RemoveAll(p =>
                string.Equals(p, item.FullPath, StringComparison.OrdinalIgnoreCase));
            added = false;
        }
        else
        {
            FileSystemService.BookmarkedPaths.Add(item.FullPath);
            item.IsBookmarked = true;
            _settings.BookmarkPaths.Add(item.FullPath);
            added = true;
        }

        // Saved immediately, same reasoning as the color settings: a bookmark
        // is a deliberate act whose whole point is persisting.
        _settingsService.Save(_settings);
        RefreshBookmarkPanelIfShowing(added ? item.FullPath : null);
    }

    // +1 = next (Ctrl+Alt+L), -1 = previous (Ctrl+Alt+J), cycling in the
    // order bookmarks were added. An entry whose path doesn't answer right
    // now is skipped but NOT removed - it may live on a network drive that's
    // merely asleep (the same read-failure-isn't-emptiness rule as the tree's).
    // The search for the next reachable bookmark happens off the UI thread.
    // Testing a path is a disk call, and against a network drive that has gone
    // away each one sits in the SMB timeout for seconds - times however many
    // bookmarks the cycle walks past. Pressing Ctrl+Alt+L with a NAS switched
    // off froze the window and then did nothing, since every path had answered
    // "no" (reported 2026-07-26). ProbeExists also remembers a root that made
    // it wait, so the rest of the cycle skips it immediately.
    private async void JumpToBookmark(int direction)
    {
        var paths = _settings.BookmarkPaths.ToList();
        if (paths.Count == 0)
        {
            return;
        }

        int startIndex = _bookmarkCycleIndex;
        var target = await Task.Run(() => FindNextReachableBookmark(paths, startIndex, direction));
        if (target is not { } found)
        {
            return;
        }

        // The list can have changed while the search ran (a toggle, a prune) -
        // the index is only meaningful against the list it was found in.
        _bookmarkCycleIndex = _settings.BookmarkPaths.FindIndex(p =>
            string.Equals(p, found.Path, StringComparison.OrdinalIgnoreCase));


        SetSearchViewActive(false);

        // Same route a search-result click takes for files (handles rows past
        // a folder's "더 보기" cap); folders take the favorites-style walk with
        // its usual pin-to-top.
        NavigateToPath(found.Path, source: "bookmark");
    }

    private static (string Path, bool IsDirectory)? FindNextReachableBookmark(List<string> paths, int startIndex,
        int direction)
    {
        int index = startIndex;
        for (int attempts = 0; attempts < paths.Count; attempts++)
        {
            index = ((index + direction) % paths.Count + paths.Count) % paths.Count;
            if (FileSystemService.ProbeExists(paths[index], out bool isDirectory))
            {
                return (paths[index], isDirectory);
            }
        }
        return null;
    }

    // Ctrl+Alt+K/L/J work anywhere in the window. With Alt held WPF reports
    // Key.System and hides the real key in SystemKey. Skipped while a TextBox
    // has focus (inline rename, the search box) so typing never triggers a
    // toggle or jump.
    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (Keyboard.Modifiers != (ModifierKeys.Control | ModifierKeys.Alt) ||
            Keyboard.FocusedElement is TextBox)
        {
            return;
        }

        switch (e.Key == Key.System ? e.SystemKey : e.Key)
        {
            case Key.K:
                if (ExplorerTree.SelectedItem is FileSystemItem { IsPlaceholder: false, IsShowMore: false } item)
                {
                    ToggleBookmark(item);
                }
                e.Handled = true;
                break;
            case Key.L:
                JumpToBookmark(+1);
                e.Handled = true;
                break;
            case Key.J:
                JumpToBookmark(-1);
                e.Handled = true;
                break;
        }
    }

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
        else if (!IsUnreachableNetworkItem(item))
        {
            ShellFileService.OpenWithDefaultApp(item.FullPath);
        }
    }

    private void OpenWithPicker_Click(object sender, RoutedEventArgs e)
    {
        if (ExplorerTree.SelectedItem is FileSystemItem { IsPlaceholder: false, IsDirectory: false } item &&
            !IsUnreachableNetworkItem(item))
        {
            ShellFileService.OpenWithPicker(item.FullPath);
        }
    }

    private void CopyItem_Click(object sender, RoutedEventArgs e)
    {
        var items = GetEffectiveSelection();
        if (items.Count > 0)
        {
            // A copy replaces the clipboard outright, so whatever was cut
            // before is no longer going anywhere - drop its markers with it.
            ClearCutMarks();
            FileOperationService.CopyToClipboard(items.Select(i => i.FullPath));
        }
    }

    private void CutItem_Click(object sender, RoutedEventArgs e)
    {
        var items = GetEffectiveSelection();
        if (items.Count == 0)
        {
            return;
        }

        // Mark only after the clipboard actually took it: a failed write would
        // otherwise leave rows faded with nothing to paste.
        if (FileOperationService.CutToClipboard(items.Select(i => i.FullPath)))
        {
            MarkCutPaths(items.Select(i => i.FullPath));
        }
    }

    // The cut markers live in FileSystemService.CutPaths (so rows rebuilt by a
    // merge come back marked) and on whatever rows are realized right now, in
    // both views.
    private void MarkCutPaths(IEnumerable<string> paths)
    {
        FileSystemService.CutPaths.Clear();
        foreach (string path in paths)
        {
            FileSystemService.CutPaths.Add(path);
        }
        ApplyCutMarks("cut");
    }

    // The reason is only ever written to the log, but that is the whole point:
    // the first round of this feature shipped without it and the answer to
    // "why is this row still faded" turned out to be a clear that never ran.
    private void ClearCutMarks(string reason = "clear")
    {
        if (FileSystemService.CutPaths.Count == 0)
        {
            return;
        }
        FileSystemService.CutPaths.Clear();
        ApplyCutMarks(reason);
    }

    private void ApplyCutMarks(string reason)
    {
        foreach (var item in EnumerateLoadedItems(_roots))
        {
            item.IsCut = FileSystemService.CutPaths.Count > 0 &&
                FileSystemService.CutPaths.Contains(item.FullPath);
        }

        int markedRows = 0;
        foreach (var row in _searchRows)
        {
            row.IsCut = row.Entry is { } entry &&
                FileSystemService.CutPaths.Count > 0 &&
                FileSystemService.CutPaths.Contains(entry.FullPath);
            if (row.IsCut)
            {
                markedRows++;
            }
        }

        LogCutState(reason, markedRows);
    }

    // A search result faded by a cut was reported still faded after the paste
    // (2026-07-28), and the code path that would explain it isn't visible from
    // reading alone - every clear walks the very list the results are bound to.
    // So: record what the clear actually saw. A line with cut=0 rows=N and a
    // still-faded row on screen means the model was cleared and the VIEW kept
    // the fade; cut=0 rows=0 means the results list had already been replaced
    // out from under the walk.
    [System.Diagnostics.Conditional("DEBUG")]
    private void LogCutState(string reason, int markedRows)
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Edgetree");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, "cut.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {reason}: cut={FileSystemService.CutPaths.Count} " +
                $"searchRows={_searchRows.Count} markedRows={markedRows}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    // The search index is a snapshot of the last scan, so a move leaves it
    // pointing at paths the file has left - the row stays listed, and clicking
    // it now opens nothing. Drop those entries (files moved directly, and
    // everything indexed beneath a moved FOLDER) and re-filter, so the results
    // stop showing a place the file no longer is. Its new home comes back on
    // the next scan rather than being patched in here: the destination may sit
    // outside the searched scope entirely.
    private void DropMovedSearchEntries(IReadOnlyList<string> movedPaths)
    {
        if (_searchEntries.Count == 0 || movedPaths.Count == 0)
        {
            return;
        }

        var moved = movedPaths
            .Select(p => p.TrimEnd(Path.DirectorySeparatorChar))
            .ToList();

        int removed = _searchEntries.RemoveAll(entry =>
            moved.Any(m =>
                string.Equals(entry.FullPath, m, StringComparison.OrdinalIgnoreCase) ||
                entry.FullPath.StartsWith(m + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)));

        if (removed > 0)
        {
            RunSearchFilter();
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

        if (!FileOperationService.TryPaste(destinationFolder, out var outcome, out var error))
        {
            return; // Clipboard has nothing pasteable.
        }
        if (error is not null)
        {
            MessageBox.Show(this, error, Strings.PasteFailedTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // Diff-merge rather than rebuild: pasting adds rows to a folder the
        // user is looking at, and a clear-and-refill collapses every expanded
        // subtree beside them (and drops the scroll position with it). Same
        // reasoning as the watcher paths - see RefreshFolderPreservingState.
        var pasteTarget = item.IsDirectory ? item : item.Parent;
        if (pasteTarget is not null)
        {
            RefreshFolderPreservingState(pasteTarget);
        }

        if (outcome.WasMove)
        {
            // A move empties the folders it came from, which the paste target
            // refresh above knows nothing about. The watcher would catch up on
            // its own, but only for folders it's watching and only after its
            // debounce - the rows the user just moved should be gone by the
            // time they look. Sources cut in Explorer are covered too, since
            // this walks the clipboard's own list rather than our markers.
            ClearCutMarks("paste");
            DropMovedSearchEntries(outcome.SourcePaths);
            foreach (string sourceParent in outcome.SourcePaths
                .Select(p => Path.GetDirectoryName(p.TrimEnd(Path.DirectorySeparatorChar)) ?? string.Empty)
                .Where(p => p.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (FindLoadedItemForPath(sourceParent) is { ChildrenLoaded: true } sourceFolder &&
                    !ReferenceEquals(sourceFolder, pasteTarget))
                {
                    RefreshFolderPreservingState(sourceFolder);
                }
            }
        }
    }

    // Right-click on empty tree space (see ExplorerEmptySpaceContextMenu) has
    // no clicked-on item to anchor to, unlike every other file operation here.
    // Falls back through: selected folder -> selected file's parent -> the
    // multi-selection's last row -> the first drive root, so it's never simply
    // a no-op.
    private void NewFolder_Click(object sender, RoutedEventArgs e)
    {
        // Before anything is created, not just before the new row goes into
        // edit mode: if a name really was typed into the open box, it has to
        // land - and its parent finish merging - while this folder's own name
        // is still only a name on disk. Doing it afterwards would have the
        // merge run over the row this is about to hand to BeginInlineRename.
        FinishOpenInlineRename();

        FileSystemItem? target = ExplorerTree.SelectedItem switch
        {
            FileSystemItem { IsPlaceholder: false, IsDirectory: true } folder => folder,
            FileSystemItem { IsPlaceholder: false, IsDirectory: false } file => file.Parent,

            // Ctrl+clicking the natively-selected row back off drops the
            // native selection on purpose while the set keeps its rows painted
            // as selected (see ExplorerTree_SelectedItemChanged). Creating in
            // the first drive root at that moment would contradict what the
            // tree is plainly showing, so the set is asked before the roots.
            _ => _multiSelection.LastOrDefault() switch
            {
                { IsPlaceholder: false, IsDirectory: true } folder => folder,
                { IsPlaceholder: false, IsDirectory: false } file => file.Parent,
                _ => null
            } ?? _roots.FirstOrDefault()
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

        // Merge when the folder is already loaded (so its expanded children
        // survive the new row appearing); only a folder that has never been
        // read needs the full load, which is what RefreshChildren does for a
        // never-loaded item - MergeChildrenFromDisk deliberately no-ops there.
        if (target.ChildrenLoaded)
        {
            RefreshFolderPreservingState(target);
        }
        else
        {
            target.RefreshChildren();
        }
        target.IsExpanded = true;

        // The new folder is looked up by name rather than through any prior
        // reference (a rebuild would have replaced every instance, and a merge
        // creates a fresh instance for a row that wasn't there before) - then
        // dropped straight into inline rename, matching Explorer/VS Code's
        // "type the name right away" new-folder flow.
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

    // The row currently in inline edit - tracked so leaving the WINDOW can
    // revert it: LostFocus only fires when focus moves within the app, never
    // on whole-window deactivation, so without this an edit box survived an
    // Alt-Tab and was still sitting there (or reappearing on hover) when the
    // user came back (2026-07-23 03:51 report).
    private FileSystemItem? _inlineRenameItem;

    // When this window last became active. The click that ACTIVATES the
    // window must not double as a rename gesture: it lands on whatever row
    // happens to be under the cursor - often the still-selected row the user
    // left off on - and "already-selected row clicked" is exactly the
    // slow-double-click rename trigger.
    private long _lastActivatedTicks = long.MinValue / 2;

    private const long ActivationClickGraceMs = 300;

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        _lastActivatedTicks = Environment.TickCount64;
        UpdateSelectionBrushForActivation();

        // Both halves of the claim, re-checked at the one moment the marks are
        // about to be looked at again. The listener alone wasn't enough: an app
        // that writes the clipboard while we're in the background can leave the
        // notification unread or unreadable (another process still holding it),
        // and then nothing ever revisits the question (2026-07-28, reported as
        // "브라우저에서 복사해도 그대로 남아 있음").
        DropCutMarksIfClipboardMovedOn();
        DropCutMarksForVanishedPaths();
    }

    // The clipboard listener covers a cut that was consumed or replaced, but
    // not an app that moves the files and leaves the clipboard as it found it.
    // So the second half of the same claim gets checked on the way back in:
    // something that isn't there any more cannot be waiting to move.
    //
    // Off the UI thread, and "gone" means the PARENT answered and the item
    // wasn't in it - a whole drive being asleep answers nothing and must not
    // be read as a deletion (the 2026-07-23 NAS rule, same as the bookmark
    // prune).
    private async void DropCutMarksForVanishedPaths()
    {
        if (FileSystemService.CutPaths.Count == 0)
        {
            return;
        }

        var candidates = FileSystemService.CutPaths.ToList();
        var gone = await Task.Run(() => candidates.Where(IsCutPathConfirmedGone).ToList());
        if (gone.Count == 0)
        {
            return;
        }

        foreach (string path in gone)
        {
            FileSystemService.CutPaths.Remove(path);
        }
        ApplyCutMarks("vanished");

        // The same knowledge the marks just acted on applies to the results
        // list: a cut item confirmed gone from its old place is a search row
        // pointing at nothing. This is the other half of the paste path's own
        // cleanup, for the case where the move happened in another app.
        DropMovedSearchEntries(gone);
    }

    private static bool IsCutPathConfirmedGone(string path)
    {
        if (File.Exists(path) || Directory.Exists(path))
        {
            return false;
        }

        string? parent = Path.GetDirectoryName(path.TrimEnd(Path.DirectorySeparatorChar));
        return parent is { Length: > 0 } && Directory.Exists(parent);
    }

    // Leaving the app resolves rename state Total Commander-style: a pending
    // slow-double-click timer is disarmed (its edit would otherwise pop up in
    // a BACKGROUND window ~0.6s after the user already switched away - the
    // "came back and found the file in edit mode" report), and an edit box
    // already open reverts without renaming. Committing on app-switch was
    // deliberately rejected: applying a half-typed name the user never
    // confirmed is worse than asking them to start over.
    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);

        CancelPendingRename();
        if (_inlineRenameItem is { IsEditing: true } editing)
        {
            editing.IsEditing = false;
        }
        _inlineRenameItem = null;

        // One hop late on purpose - see UpdateSelectionBrushForActivation.
        Dispatcher.BeginInvoke(() => UpdateSelectionBrushForActivation(),
            System.Windows.Threading.DispatcherPriority.Input);
    }

    // Whatever edit box is currently open is finished off. Nothing used to do
    // this: BeginInlineRename simply overwrote _inlineRenameItem, leaving the
    // previous row with IsEditing = true and nothing tracking it - so making
    // three folders in a row with F7, without renaming any of them, left three
    // edit boxes open at once (reported 2026-08-02).
    //
    // Committed rather than reverted, which is what clicking another row inside
    // the app already does (leaving the app is the case that reverts - see
    // OnDeactivated). A new folder whose default name was never touched commits
    // to itself, so the F7-F7-F7 flow above is a no-op either way.
    private void FinishOpenInlineRename(FileSystemItem? except = null)
    {
        if (_inlineRenameItem is { IsEditing: true } open && !ReferenceEquals(open, except))
        {
            CommitInlineRename(open);
        }
    }

    private void BeginInlineRename(FileSystemItem item)
    {
        FinishOpenInlineRename(except: item);

        item.EditingName = item.Name;
        item.IsEditing = true;
        _inlineRenameItem = item;
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
        // than hardcoding a guess, plus a margin so a double-click landing right
        // at that limit still cancels this in time.
        //
        // The margin is the only part of this that is ours to spend, and it
        // was tried at 30 on 2026-08-09 and REVERTED the same minute: 530ms
        // made the rename box feel like it was opening on clicks that weren't
        // meant for it ("더 리네임이 자주 되는것 같이 느껴지고"). The wait is
        // not the complaint's real subject - the system double-click time is
        // 500 of it - so shortening the margin buys a feeling of misfires
        // rather than a feeling of speed. Leave it at 100.
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
        // edit already started some other way, the button is still held - a
        // drag, or a press that hasn't become a click yet - or the window is
        // no longer active (the user clicked and switched apps within the
        // timer's ~0.6s; OnDeactivated also disarms this timer, so this is
        // the belt to that braces for a deactivation racing the tick).
        if (!ReferenceEquals(ExplorerTree.SelectedItem, item) || item.IsEditing ||
            Mouse.LeftButton == MouseButtonState.Pressed || !IsActive)
        {
            return;
        }

        BeginInlineRename(item);
    }

    // Every rename box gets the overtype guard as it is created - one per tree
    // row, and a virtualized row that scrolls away and comes back is a new box
    // (see OvertypeGuard for what a bare Insert does to a Korean composition).
    private void RenameTextBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox box)
        {
            OvertypeGuard.Disable(box);
        }
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
            _inlineRenameItem = null;
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
        _inlineRenameItem = null;

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

        // Merge, not rebuild - a rename changes one row's name, and the rest
        // of the folder (including anything expanded under it) has no business
        // being torn down for it.
        if (item.Parent is { } renameParent)
        {
            RefreshFolderPreservingState(renameParent);
        }
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

        // Where the selection lands AFTER the delete: the nearest surviving
        // sibling below the selected row, else the nearest above - what
        // Explorer does, and what walking a folder of pictures in the viewer
        // needs (delete = "next picture, this one gone"). Left to itself, WPF
        // moves the selection to the PARENT folder when the selected row is
        // removed, which yanked the viewer from the picture to a folder icon
        // (user report, 2026-08-09). Computed before anything is deleted -
        // the positions only exist while the rows are all still there.
        FileSystemItem? successor = null;
        var goingAway = new HashSet<FileSystemItem>(items);
        if (ExplorerTree.SelectedItem is FileSystemItem { IsPlaceholder: false, IsShowMore: false } anchor &&
            anchor.Parent is { } anchorParent)
        {
            var rows = anchorParent.Children
                .Where(c => !c.IsPlaceholder && !c.IsShowMore)
                .ToList();
            int anchorIndex = rows.IndexOf(anchor);
            if (anchorIndex >= 0)
            {
                successor = rows.Skip(anchorIndex + 1).FirstOrDefault(r => !goingAway.Contains(r))
                    ?? rows.Take(anchorIndex).LastOrDefault(r => !goingAway.Contains(r));
            }
        }

        // Every failure is collected and shown once at the end rather than
        // popping a box per item mid-loop. A selection can also contain both a
        // folder and something inside it - deleting the folder first makes the
        // child's delete a silent no-op (TryDeleteToRecycleBin succeeds on an
        // already-gone path), which is the right outcome.
        var failures = new List<string>();
        var parentsToRefresh = new HashSet<FileSystemItem>();
        bool anchorDeleted = false;
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

            if (ReferenceEquals(item, ExplorerTree.SelectedItem))
            {
                anchorDeleted = true;
            }
            if (item.Parent is { } parent)
            {
                parentsToRefresh.Add(parent);
            }
            RemoveFavoritesUnder(item.FullPath);
            RemoveBookmarksUnder(item.FullPath);
        }

        // Refresh each affected folder once, however many of its children
        // were deleted. The set members are about to be discarded by these
        // rebuilds anyway, so the multi-selection ends here too.
        ClearMultiSelection();
        foreach (var parent in parentsToRefresh)
        {
            // Merge drops exactly the deleted rows and leaves every surviving
            // sibling - and whatever was expanded under them - untouched.
            RefreshFolderPreservingState(parent);
        }

        // Only when the selected row actually went away (a failed delete
        // leaves it standing, and moving the selection off a row that still
        // exists would be its own surprise). The merge above reuses surviving
        // item instances, so the successor captured pre-delete is still the
        // live row. No successor (the folder emptied out) falls through to
        // WPF's own parent selection, which is right there.
        if (anchorDeleted && successor is not null)
        {
            SelectVisibleItem(successor);
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

    // The bookmark half of the above, which was simply missing: deleting a
    // bookmarked file left its bookmark behind, and the panel then drew it as a
    // row with no icon at all (the kind probe has nothing to answer with), which
    // reads as a rendering fault rather than as a dead entry. Reported
    // 2026-08-02; favorites never showed it because they had this from the
    // start.
    //
    // Removing on OUR OWN delete does not weaken the rule that a bookmark is
    // only dropped when the volume can testify the path is gone (see
    // PruneMissingBookmarks): this app just deleted it. That is the strongest
    // testimony there is, not a guess about an unreachable drive.
    private void RemoveBookmarksUnder(string deletedPath)
    {
        string trimmed = deletedPath.TrimEnd('\\');
        string prefix = trimmed + '\\';

        var stale = _settings.BookmarkPaths.Where(p =>
            string.Equals(p.TrimEnd('\\'), trimmed, StringComparison.OrdinalIgnoreCase) ||
            p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stale.Count == 0)
        {
            return;
        }

        // Deliberately NOT a loop over RemoveBookmark: that one walks the whole
        // loaded tree to clear a row's ribbon, and rebuilds the panel, on every
        // call - so deleting a folder holding several bookmarks would pay both
        // costs once per bookmark, on the UI thread. One pass over the tree and
        // one rebuild for the batch instead.
        var staleSet = new HashSet<string>(stale, StringComparer.OrdinalIgnoreCase);
        foreach (string path in stale)
        {
            _settings.BookmarkPaths.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            FileSystemService.BookmarkedPaths.Remove(path);
        }

        foreach (var item in EnumerateLoadedItems(_roots))
        {
            if (staleSet.Contains(item.FullPath))
            {
                item.IsBookmarked = false;
            }
        }

        // The cycle's position indexes into the list that just changed.
        _bookmarkCycleIndex = -1;
        _settingsService.Save(_settings);
        RefreshBookmarkPanelIfShowing();
    }

    // Packs the current selection into one zip beside the right-clicked row,
    // named after that row with its extension dropped ("photo.jpg" ->
    // "photo.zip") - the naming Explorer already taught the user. The work
    // runs off the UI thread because a large folder takes long enough to
    // freeze the window, and nothing appears in the tree until it finishes
    // (see ArchiveService's hidden-temp-then-rename note).
    private async void CompressItem_Click(object sender, RoutedEventArgs e)
    {
        var items = GetEffectiveSelection();
        if (items.Count == 0 ||
            ExplorerTree.SelectedItem is not FileSystemItem { IsPlaceholder: false, IsShowMore: false } anchor ||
            anchor.Parent is not { } destination)
        {
            return;
        }

        // Captured before the await: the selection can move, and the folder's
        // rows get rebuilt underneath us while the archive is being written.
        var sourcePaths = items.Select(i => i.FullPath).ToList();
        string destinationFolder = destination.FullPath;
        string baseName = anchor.IsDirectory ? anchor.Name : Path.GetFileNameWithoutExtension(anchor.Name);

        var result = await Task.Run(() => ArchiveService.CreateZip(sourcePaths, destinationFolder, baseName));

        // Diff-merge, NOT RefreshChildren: the new zip is one added row in a
        // folder the user is looking at, and a clear-and-refill would take
        // every expanded subtree beside it down with it (see
        // RefreshFolderPreservingState's three-designs note).
        RefreshFolderPreservingState(destination);

        if (!result.Success)
        {
            MessageBox.Show(this, result.Error, Strings.CompressFailedTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // A locked or unreadable file doesn't throw the archive away, but the
        // user still has to be told it isn't in there.
        if (result.SkippedCount > 0)
        {
            MessageBox.Show(this, string.Format(Strings.CompressSkippedBody, result.SkippedCount),
                Strings.CompressFailedTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // Unpacks the right-clicked .zip into a folder of the same name beside it.
    // Only reachable from a .zip row (the menu item is Collapsed otherwise).
    private async void ExtractItem_Click(object sender, RoutedEventArgs e)
    {
        if (ExplorerTree.SelectedItem is not FileSystemItem { IsPlaceholder: false, IsDirectory: false } item ||
            item.Parent is not { } destination ||
            !ArchiveService.IsZipPath(item.FullPath))
        {
            return;
        }

        string zipPath = item.FullPath;
        var result = await Task.Run(() => ArchiveService.ExtractZip(zipPath));

        // Same reason as 압축: merge, don't rebuild.
        RefreshFolderPreservingState(destination);

        if (!result.Success)
        {
            MessageBox.Show(this, result.Error, Strings.ExtractFailedTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CopyPath_Click(object sender, RoutedEventArgs e)
    {
        if (ExplorerTree.SelectedItem is FileSystemItem { IsPlaceholder: false } item)
        {
            ClearCutMarks();
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
        // No pre-clamp here: ClampExpandedWidth bounds the TREE's share, and
        // with the viewer open the window total legitimately exceeds it -
        // SetExpandedWidthAnchored does all the bounding per policy.
        double rawDelta = _settings.DockOnRight ? -e.HorizontalChange : e.HorizontalChange;

        // The same grids as the band edges, on the WINDOW's whole width -
        // which is what the eye reads off the screen, and which puts the outer
        // edge on the same grid lines from either screen side (docked left the
        // window starts at the work area's left, docked right it ends at its
        // right). The monitor is only queried while a modifier is actually
        // held, so an ordinary drag costs nothing new.
        double target = Width + rawDelta;
        if (DockedSnapDivisions is not null)
        {
            target = SnapToGrid(target, 0, GetCurrentMonitorWorkArea().Width);
        }

        SetExpandedWidthAnchored(target);
    }

    // The docked window's top and bottom edges. Dragging the top moves where the
    // sidebar starts as well as how tall it is; dragging the bottom only changes
    // the height. Between them that is position and size out of one gesture,
    // which is how any other window behaves.
    //
    // Neither gesture touches window geometry AT ALL. While docked and
    // expanded, the window is parked covering the whole work-area edge
    // (PositionToWorkArea); the band the user sees is RootContent's top and
    // bottom margins plus the window clip region, and a drag just moves those
    // - pure content changes, which WPF renders atomically. This shape
    // survived a day of instrumented alternatives (2026-08-07, resize.log):
    // every way of actually resizing the window during a drag - one
    // SetWindowPos per event, the OS's own sizing loop, WM_NCCALCSIZE
    // WVR_VALIDRECTS (honored by the window manager, irrelevant to WPF's
    // D3D-presented surface), a one-off expand/commit at the gesture's ends -
    // left a visible artifact somewhere, because WPF presents frames to the
    // compositor asynchronously and ANY geometry change can be composed a
    // beat before the frame that matches it (a floating window's native top
    // border tears identically with no custom code involved - the control
    // test that closed the question). Moving the origin shifts every late
    // frame by the drag delta; growing the window exposes uninitialized
    // surface, seen as handle-colored garbage past the old edge. No geometry,
    // no artifact class.
    private bool _inTopBandDrag;
    private bool _inBottomBandDrag;

    // Gesture state captured at DragStarted, in DIPs: the window's fixed
    // extents, the band edge that must not move, where inside the grip the
    // cursor grabbed, and the band's height floor.
    private double _bandWorkTopDip;        // window top (= work area top)
    private double _bandWindowBottomDip;   // window bottom (= work area bottom)
    private double _bandAnchorBottomDip;   // band bottom, fixed during a top drag
    private double _bandAnchorTopDip;      // band top, fixed during a bottom drag
    private double _bandGrabOffsetDip;
    private double _bandMinHeightDip;

    // Absolute cursor position, not accumulated DragDelta: these grips are
    // anchored to the very edge they move, so every event relocates the
    // Thumb's own coordinate space and accumulated deltas drift (2026-08-06).
    private double CursorDipY()
        => System.Windows.Forms.Cursor.Position.Y / VisualTreeHelper.GetDpi(this).DpiScaleY;

    // Back to the full edge - the escape hatch for a sidebar dragged down to a
    // stub, the same convention the width thumb's double-click uses. Read off
    // ClickCount in the Preview handlers because neither grip's press bubbles
    // far enough for a MouseDoubleClick to be raised.
    private bool HandleVerticalThumbDoubleClick(MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2)
        {
            return false;
        }

        e.Handled = true;
        _settings.DockedHeightRatio = 1.0;
        _settings.DockedTopRatio = 0.0;
        PositionToWorkArea();
        return true;
    }

    private void TopResizeThumb_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!CanResizeWidth)
        {
            return;
        }

        // A single press is deliberately NOT handled: it falls through to the
        // Thumb, which captures and raises the DragStarted below.
        HandleVerticalThumbDoubleClick(e);
    }

    private void TopResizeThumb_DragStarted(object sender, DragStartedEventArgs e)
    {
        if (!CanResizeWidth)
        {
            return;
        }

        var workArea = GetCurrentMonitorWorkArea();
        _bandWorkTopDip = Top;
        _bandAnchorBottomDip = Top + Height - RootContent.Margin.Bottom;
        _bandGrabOffsetDip = CursorDipY() - (Top + RootContent.Margin.Top);
        _bandMinHeightDip = Math.Min(MinDockedHeight, workArea.Height);
        _snapGridOriginDip = workArea.Top;
        _snapGridExtentDip = workArea.Height;
        _inTopBandDrag = true;
        BeginResizeLog("top-band");
    }

    private void TopResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (!_inTopBandDrag)
        {
            return;
        }

        // Snapped BEFORE the clamp, so the floors still have the last word: a
        // grid line that would leave the band shorter than its minimum simply
        // doesn't survive the clamp below.
        double top = Math.Clamp(
            SnapToGrid(CursorDipY() - _bandGrabOffsetDip, _snapGridOriginDip, _snapGridExtentDip),
            _bandWorkTopDip, Math.Max(_bandWorkTopDip, _bandAnchorBottomDip - _bandMinHeightDip));
        RootContent.Margin = new Thickness(
            0, top - _bandWorkTopDip, 0, RootContent.Margin.Bottom);
        ApplyWindowClipRegion();
        LogBandDrag(top);
    }

    private void TopResizeThumb_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (!_inTopBandDrag)
        {
            return;
        }

        _inTopBandDrag = false;
        StoreDockedBandRatios(GetCurrentMonitorWorkArea());
        FlushResizeLog();
    }

    private void BottomResizeThumb_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!CanResizeWidth)
        {
            return;
        }

        // A single press is deliberately NOT handled: it falls through to the
        // Thumb, which captures and raises the DragStarted below.
        HandleVerticalThumbDoubleClick(e);
    }

    private void BottomResizeThumb_DragStarted(object sender, DragStartedEventArgs e)
    {
        if (!CanResizeWidth)
        {
            return;
        }

        var workArea = GetCurrentMonitorWorkArea();
        _bandWindowBottomDip = Top + Height;
        _bandAnchorTopDip = Top + RootContent.Margin.Top;
        _bandGrabOffsetDip = CursorDipY() - (Top + Height - RootContent.Margin.Bottom);
        _bandMinHeightDip = Math.Min(MinDockedHeight, workArea.Height);
        _snapGridOriginDip = workArea.Top;
        _snapGridExtentDip = workArea.Height;
        _inBottomBandDrag = true;
        BeginResizeLog("bottom-band");
    }

    private void BottomResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (!_inBottomBandDrag)
        {
            return;
        }

        double bottom = Math.Clamp(
            SnapToGrid(CursorDipY() - _bandGrabOffsetDip, _snapGridOriginDip, _snapGridExtentDip),
            Math.Min(_bandAnchorTopDip + _bandMinHeightDip, _bandWindowBottomDip),
            _bandWindowBottomDip);
        RootContent.Margin = new Thickness(
            0, RootContent.Margin.Top, 0, _bandWindowBottomDip - bottom);
        ApplyWindowClipRegion();
        LogBandDrag(bottom);
    }

    private void BottomResizeThumb_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (!_inBottomBandDrag)
        {
            return;
        }

        _inBottomBandDrag = false;
        StoreDockedBandRatios(GetCurrentMonitorWorkArea());
        FlushResizeLog();
    }

    // Stored as they will be read back - a fraction of the work area, and a
    // fraction of whatever space that leaves (see AppSettings). The slack guard
    // is not a formality: at full height it is zero, and the division would put
    // a NaN into the settings file.
    //
    // The band, not the window: the expanded docked window intentionally
    // reaches above the band to the work area's top, with the margin covering
    // the difference (see PositionToWorkArea).
    private void StoreDockedBandRatios(Rect workArea)
    {
        double bandTop = Top + RootContent.Margin.Top;
        double bandHeight = Height - RootContent.Margin.Top - RootContent.Margin.Bottom;
        double slack = workArea.Height - bandHeight;
        _settings.DockedHeightRatio = Math.Clamp(bandHeight / workArea.Height, 0, 1);
        _settings.DockedTopRatio = slack > 0.5
            ? Math.Clamp((bandTop - workArea.Top) / slack, 0, 1)
            : 0;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct WindowPos
    {
        public IntPtr Hwnd;
        public IntPtr HwndInsertAfter;
        public int X, Y, Cx, Cy;
        public uint Flags;
    }

    // ----- 세로 리사이즈 계측기 (Debug 전용) --------------------------------
    //
    // Kept in memory for the length of one drag and written once when the button
    // comes up: a file append per event would itself be a source of hitching,
    // which is the last thing to add to something being investigated for shaking.
    //
    // What the lines answer: `band` is where each event put the visible edge,
    // `applied` is any real window-geometry change the window manager performed
    // while the drag ran (see the WM_WINDOWPOSCHANGED hook). The gestures have
    // no reason to move the window, so an `applied` line that is not the clip
    // region's own no-move echo names the culprit directly.
    private readonly List<string> _resizeLog = new();

    [System.Diagnostics.Conditional("DEBUG")]
    private void BeginResizeLog(string edge)
    {
        _resizeLog.Clear();
        var dpi = VisualTreeHelper.GetDpi(this);
        _resizeLog.Add($"{DateTime.Now:HH:mm:ss.fff}  drag {edge} start  " +
            $"dpi={dpi.DpiScaleY:F3} Top={Top:F2} Height={Height:F2} " +
            $"margin={RootContent.Margin.Top:F2}/{RootContent.Margin.Bottom:F2}");
    }

    // Where each event put the visible edge (the dragged one; the other is in
    // the start line's margins).
    [System.Diagnostics.Conditional("DEBUG")]
    private void LogBandDrag(double edgeDip)
        => _resizeLog.Add($"  band    edge={edgeDip:F2}");

    [System.Diagnostics.Conditional("DEBUG")]
    private void LogWindowPosChanged(IntPtr lParam)
    {
        var pos = System.Runtime.InteropServices.Marshal.PtrToStructure<WindowPos>(lParam);
        _resizeLog.Add($"  applied y={pos.Y} cy={pos.Cy} bot={pos.Y + pos.Cy} flags=0x{pos.Flags:X4}");
    }

    [System.Diagnostics.Conditional("DEBUG")]
    private void FlushResizeLog()
    {
        if (_resizeLog.Count == 0)
        {
            return;
        }

        _resizeLog.Add($"  end    Top={Top:F2} Height={Height:F2} " +
            $"ratio={_settings.DockedHeightRatio:F4}/{_settings.DockedTopRatio:F4}");
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Edgetree");
            Directory.CreateDirectory(dir);
            File.AppendAllLines(Path.Combine(dir, "resize.log"), _resizeLog);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        _resizeLog.Clear();
    }

    // Shared anchoring logic between manual drag-resize and the fit/restore
    // double-click below - right-docked, the window has to slide left by
    // however much it's growing/shrinking to keep its right edge pinned to
    // the screen edge, since Width alone only grows/shrinks rightward.
    private void SetExpandedWidthAnchored(double newWidth)
    {
        // newWidth is the whole WINDOW's target. With the viewer panel open,
        // the outer edge belongs to the VIEWER alone (user's calls,
        // 2026-08-08, two refinements the same hour): the tree's share never
        // moves from here - the middle divider is the one and only way to
        // resize the tree - and the drag simply stops at the viewer's own
        // bounds instead of cascading into the tree. ExpandedWidth is left
        // untouched on that branch for the same reason: the tree didn't
        // move, and its remembered width shouldn't either.
        double viewerWidth = CurrentViewerPanelWidth;
        if (_viewerOpen)
        {
            // The held share, not Width minus the STORED panel width: those
            // two part company the moment the window has squeezed or grown
            // the panel away from what settings remember, and the difference
            // used to leak straight into the tree.
            double treeShare = _viewerTreeShare ?? (Width - viewerWidth);
            viewerWidth = Math.Clamp(newWidth - treeShare, MinViewerWidth, MaxViewerWidth);
            newWidth = treeShare + viewerWidth;

            _settings.ViewerWidth = viewerWidth;
            if (_viewerOnLeft)
            {
                ViewerColumnLeft.Width = new GridLength(viewerWidth);
            }
            else
            {
                ViewerColumnRight.Width = new GridLength(viewerWidth);
            }
        }
        else
        {
            newWidth = ClampExpandedWidth(newWidth);
            _settings.ExpandedWidth = newWidth;
        }

        if (_settings.DockOnRight)
        {
            Left -= newWidth - Width;
        }
        Width = newWidth;
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
        // A deliberate no-op while the viewer is open (user, 2026-08-08): the
        // outer edge then belongs to the viewer (see SetExpandedWidthAnchored),
        // and a "fit the tree" gesture on the viewer's edge fits nothing the
        // user is looking at.
        if (e.ChangedButton != MouseButton.Left || !CanResizeWidth || _viewerOpen)
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
        // Viewer-open never reaches here (the guard above) - fitWidth is the
        // tree's content and the tree is the whole window.
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
            // Minus the viewer panel: ExpandedWidth is the TREE's width alone
            // (see the viewer region below). With the panel open the tree's
            // share may legitimately sit below the WINDOW floor (the split
            // floor is smaller), and the standing rule is that nothing but
            // the middle divider moves the stored tree width - so this keeps
            // the split-floor clamp, not the window one.
            _settings.ExpandedWidth = _viewerOpen
                ? Math.Clamp(Width - CurrentViewerPanelWidth, MinTreeSplitWidth, MaxExpandedWidth)
                : ClampExpandedWidth(Width);
        }

        _settings.ExpandedFolderPaths = CollectAllExpandedPaths();
        _settings.LastSelectedPath = (ExplorerTree.SelectedItem as FileSystemItem)?.FullPath;

        _settingsService.Save(_settings);
    }

    // ===================================================================
    // Image viewer panel (round 1: open/close geometry + selection-follow
    // preview; zoom/pan and the image context menu are later rounds).
    //
    // The panel is one of the content grid's two outer columns; opening it
    // widens the WINDOW by the panel's width, so the tree keeps every pixel
    // it had and _settings.ExpandedWidth stays the TREE's width alone -
    // every place that persists a width subtracts the panel again
    // (SaveCurrentWidth, SetExpandedWidthAnchored). Docked on the right
    // edge the extra width grows toward the screen's interior (Left shifts
    // left by the same amount); docked left or floating it grows rightward.
    //
    // Deliberately kept OUT of every risky geometry path: dock, undock,
    // dock-side change and auto-hide all close the panel first (auto-hide
    // folding it is the user's call, 2026-08-08), so PositionToWorkArea,
    // the reveal slide and the band clip never meet a widened window.

    private bool _viewerOpen;
    private bool _viewerOnLeft;
    // The tree's width, held across every window resize that isn't the middle
    // divider (the standing rule: the divider is the ONE gesture that moves
    // it). Kept by the app rather than read back off the window, because the
    // WPF properties don't survive the trip: instrumented 2026-08-08, Width
    // is 3854 the moment a maximize starts while ActualWidth still reads the
    // old 1246, and they swap roles again on the restore - so anything
    // derived from them mid-transition is a coin toss. Null until the panel
    // opens; then ClampViewerColumnToWindow hands the panel every pixel the
    // window gains or loses and the tree stays where the user put it.
    private double? _viewerTreeShare;
    private string? _pendingViewerPath;

    // Zoom state (round 2). NULL is the rest state, "fit inside the panel" -
    // deliberately not a number, because fit has no fixed value: it moves with
    // every divider drag and window resize, and storing the number it happened
    // to have would silently stop fitting the moment the panel changed width.
    // Non-null is a DISPLAY scale where 1.0 means the bitmap's own pixels.
    private double? _viewerZoom;
    // Which rest state a NEW picture arrives in: fit (false) or 1:1 (true).
    // Set only by the two chips and the double-click that toggles them, so
    // picking 1:1 once carries down a folder - the wheel and +/- stay a
    // one-off zoom on the picture in front of you and are deliberately NOT
    // remembered (user, 2026-08-09). Session-only; nothing is persisted.
    private bool _viewerRestAtActualSize;
    // Which file the zoom above belongs to. The panel reloads the SAME file
    // whenever a width drag settles (to re-decode at the new size), and
    // "arriving picture resets to fit" read that as a new picture - so any
    // resize of the window threw away the zoom (reported 2026-08-08). The
    // reset is keyed on the path changing, not on a load happening.
    private string? _viewerZoomPath;
    // The original file's pixel size (not the decode's - DecodePixelWidth
    // rewrites the bitmap's own PixelWidth), which is what every scale here is
    // measured against.
    private int _viewerPixelWidth;
    private int _viewerPixelHeight;
    // Width the bitmap on screen was actually decoded at, so zooming past it
    // can ask for the full-resolution one exactly once.
    private int _viewerDecodedWidth;
    private bool _viewerFullResPending;
    // Whether the picture on screen came out of WIC's own decode - as opposed
    // to a shell preview (video frame, PSD, PDF page) riding in the same Image
    // element, or the icon fallback. The context menu's 배경 설정 row is only
    // offered on the former: the shell preview is a downsized stand-in for a
    // file Windows can't use as a wallpaper anyway.
    private bool _viewerShowingDecodedImage;
    // True from the first frame of a window resize until it settles - see
    // ViewerImageHost_SizeChanged.
    private bool _viewerResizing;
    private System.Windows.Threading.DispatcherTimer? _viewerSettleTimer;
    private bool _viewerPanning;
    // The temporary full cover (middle-click on the picture, Esc to leave).
    // Deliberately NOT the same thing as the 0px divider position tried and
    // rolled back on 2026-08-08 ("아무리 봐도 어색") - that was proposed as the
    // resting split, where a strip of tree always belongs; this is a mode
    // entered on purpose and left by the gesture that entered it.
    private bool _viewerFullscreen;
    private WindowState? _viewerFullscreenPreviousState;
    private System.Windows.Point _viewerPanOrigin;
    private double _viewerPanOriginX;
    private double _viewerPanOriginY;

    // Percentages the stepper and the wheel both land on. Fit is not one of
    // them - it is a ratio, and it moves - so the 맞춤 chip is how you get
    // back to it. Nor is it the floor any more: it was, and that left a large
    // picture unable to zoom out at all past the size it rested at, which no
    // other viewer does (user, 2026-08-08). The ladder simply runs down to 5%.
    private static readonly double[] ViewerZoomSteps =
        { 0.05, 0.1, 0.17, 0.25, 0.33, 0.5, 0.67, 1.0, 1.5, 2.0, 3.0, 4.0, 8.0 };
    private System.Windows.Threading.DispatcherTimer? _viewerPreviewTimer;

    private const double MinViewerWidth = 240;
    // 800 → 1600 → 3200 → 3720 the day the split drag landed (user kept
    // hitting it: the panel is where the pixels should go). 3720 is the
    // user's own 4K arithmetic - 3840 minus the tree's 120 floor - chosen so
    // a near-fullscreen window can give everything but the tree strip to the
    // panel. The split can only grow within the current window anyway - the
    // real bound is the window minus MinTreeSplitWidth - so this cap mostly
    // bounds what a saved width can re-widen the window by on the next open.
    private const double MaxViewerWidth = 3720;
    // The tree column's floor under the SPLIT drag - deliberately smaller
    // than MinExpandedWidth: that one is a WINDOW floor (seven header
    // buttons must fit), and the header spans all three columns, so the
    // tree column itself can go much narrower before anything breaks.
    private const double MinTreeSplitWidth = 120;

    private double ViewerPanelWidth => Math.Clamp(_settings.ViewerWidth, MinViewerWidth, MaxViewerWidth);

    // What the window's Width currently carries on top of the tree - the
    // number every width-persisting path subtracts.
    private double CurrentViewerPanelWidth => _viewerOpen ? ViewerPanelWidth : 0;

    private void ViewerButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewerOpen)
        {
            CloseViewer();
        }
        else
        {
            OpenViewer();
        }
    }

    private void ViewerCloseButton_Click(object sender, RoutedEventArgs e) => CloseViewer();

    // Same thing as the panel's own X, put where the hand already is: the
    // divider, rather than the far corner of the window (user, 2026-08-09).
    private void ViewerCollapseButton_Click(object sender, RoutedEventArgs e) => CloseViewer();

    private void ViewerExpandButton_Click(object sender, RoutedEventArgs e) => OpenViewer();

    // The collapse chevron's mirror, shown only while the panel is CLOSED and
    // an image row is selected. Both conditions matter: the edge it sits on is
    // also the tree's scrollbar and, docked, the width grip, so it has to earn
    // its place each time rather than stand there permanently. Hidden while
    // auto-hidden and collapsed too, since OpenViewer declines from a sliver
    // and a control that does nothing is worse than no control.
    private void UpdateViewerExpandButton()
    {
        bool show =
            !_viewerOpen &&
            !(_settings.IsAutoHidden && !_isAutoHideRevealed) &&
            _selectedItem is { IsPlaceholder: false, IsShowMore: false, IsDirectory: false } item &&
            ThumbnailExtensions.Contains(Path.GetExtension(item.FullPath));

        ViewerExpandButton.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (!show)
        {
            return;
        }

        // Points where the panel will come FROM, which is the side it opens on
        // - the mirror of the collapse chevron, which points at the tree.
        bool opensOnLeft = _isDocked && _settings.DockOnRight;
        Grid.SetColumn(ViewerExpandButton, 1);
        ViewerExpandButton.HorizontalAlignment = opensOnLeft
            ? System.Windows.HorizontalAlignment.Left
            : System.Windows.HorizontalAlignment.Right;
        ViewerExpandGlyph.Data = Geometry.Parse(
            opensOnLeft ? "M6,0 L2,5 L6,10" : "M2,0 L6,5 L2,10");
    }

    private void OpenViewer()
    {
        // Collapsed-to-sliver is the only state that declines: there is no
        // window to widen. A REVEALED auto-hide peek is geometrically the
        // same as pinned-open, and blocking it too made the eye button dead
        // until the user pinned once (reported 2026-08-08) - the ride
        // through the next hide/reveal already works either way.
        if (_viewerOpen || (_settings.IsAutoHidden && !_isAutoHideRevealed))
        {
            return;
        }

        _viewerOpen = true;

        bool onLeft = _isDocked && _settings.DockOnRight;
        double panelWidth = ViewerPanelWidth;
        if (onLeft)
        {
            Left -= panelWidth;
        }
        Width += panelWidth;
        if (!_isDocked)
        {
            // A floating window near the screen's right edge would otherwise
            // grow past it.
            var workArea = GetCurrentMonitorWorkArea();
            if (Left + Width > workArea.Right)
            {
                Left = Math.Max(workArea.Left, workArea.Right - Width);
            }
        }

        ApplyViewerSide();
        ViewerPanel.Visibility = Visibility.Visible;
        ViewerSplitThumb.Visibility = Visibility.Visible;
        ViewerCollapseButton.Visibility = Visibility.Visible;
        UpdateViewerExpandButton();

        // The band clip's state tuple carries no width (see _appliedClip), so
        // force the next pass to re-derive it against the widened window.
        _appliedClip = (ClipUnknown, 0, 0);
        ApplyWindowClipRegion();

        if (!_settings.ViewerOpen)
        {
            _settings.ViewerOpen = true;
            _settingsService.Save(_settings);
        }

        UpdateViewerPreview();
    }

    private void CloseViewer()
    {
        if (!_viewerOpen)
        {
            return;
        }

        SetViewerFullscreen(false);

        _viewerOpen = false;
        _pendingViewerPath = null;
        _viewerTreeShare = null;

        double panelWidth = ViewerPanelWidth;
        ViewerColumnLeft.Width = new GridLength(0);
        ViewerColumnRight.Width = new GridLength(0);
        ViewerPanel.Visibility = Visibility.Collapsed;
        ViewerSplitThumb.Visibility = Visibility.Collapsed;
        ViewerCollapseButton.Visibility = Visibility.Collapsed;
        UpdateViewerExpandButton();
        StopViewerGif();
        ViewerImage.Source = null;
        ViewerIconImage.Source = null;

        Width = Math.Max(MinExpandedWidth, Width - panelWidth);
        if (_viewerOnLeft)
        {
            Left += panelWidth;
        }

        _appliedClip = (ClipUnknown, 0, 0);
        ApplyWindowClipRegion();

        if (_settings.ViewerOpen)
        {
            _settings.ViewerOpen = false;
            _settingsService.Save(_settings);
        }
    }

    // Selection changes debounce through a short timer: arrow-keying down a
    // folder of images fires SelectedItemChanged per row, and decoding every
    // intermediate file would stutter exactly the browsing this panel is
    // for. 120ms is below "feels laggy" and above key-repeat.
    private void ScheduleViewerPreview()
    {
        if (!_viewerOpen)
        {
            return;
        }

        if (_viewerPreviewTimer is null)
        {
            _viewerPreviewTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(120),
            };
            _viewerPreviewTimer.Tick += (_, _) =>
            {
                _viewerPreviewTimer!.Stop();
                UpdateViewerPreview();
            };
        }

        _viewerPreviewTimer.Stop();
        _viewerPreviewTimer.Start();
    }

    private void UpdateViewerPreview()
    {
        if (!_viewerOpen)
        {
            return;
        }

        var item = _selectedItem;
        if (item is null || item.IsPlaceholder || item.IsShowMore)
        {
            _pendingViewerPath = null;
            StopViewerGif();
            ViewerImage.Source = null;
            _viewerShowingDecodedImage = false;
            ViewerIconImage.Source = null;
            ViewerFileName.Text = string.Empty;
            ViewerFileInfo.Text = string.Empty;
            ClearViewerZoom();
            UpdateViewerCarousel();
            return;
        }

        string path = item.FullPath;
        if (string.Equals(_pendingViewerPath, path, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        _pendingViewerPath = path;
        // A new file always ends the old animation; the same-path early
        // return above is what lets a playing GIF survive its row being
        // re-selected.
        StopViewerGif();

        ViewerFileName.Text = item.Name;
        ViewerFileInfo.Text = string.Empty;
        UpdateViewerCarousel();

        bool isImage = !item.IsDirectory && ThumbnailExtensions.Contains(Path.GetExtension(path));
        if (isImage)
        {
            if (string.Equals(Path.GetExtension(path), ".gif", StringComparison.OrdinalIgnoreCase))
            {
                // Animated: its own loader, which falls back to the still
                // path for single-frame and broken files.
                LoadViewerGif(path);
            }
            else
            {
                int decodeWidth = ViewerDecodeWidth();
                Task.Run(() => LoadViewerImage(path, decodeWidth));
            }
        }
        else
        {
            ShowViewerIcon(path);
        }
    }

    // Decode at the size of the slot it lands in, not full size: an 8K
    // wallpaper decoded whole is ~100MB of pixels for a ~400px slot, and
    // nothing is drawn bigger than the slot until someone zooms (which asks
    // for the full-resolution pass separately).
    //
    // The slot is the HOST, not the remembered panel width, whenever the two
    // differ - full screen the panel width is still the little number from
    // the windowed split, so every picture arrived decoded for a 960px slot
    // and sat there soft on a 4K screen (reported 2026-08-08). Getting it
    // right here means the second pass usually isn't needed at all.
    private int ViewerDecodeWidth()
    {
        double slotWidth = ViewerImageHost.ActualWidth > 0
            ? Math.Max(ViewerImageHost.ActualWidth, ViewerPanelWidth)
            : ViewerPanelWidth;
        return (int)Math.Ceiling(
            Math.Max(200, slotWidth) * VisualTreeHelper.GetDpi(this).DpiScaleX);
    }

    // Background thread. Header first (original dimensions - DecodePixelWidth
    // rewrites the decoded bitmap's own), then the scaled decode; only a
    // frozen bitmap crosses back to the UI thread.
    //
    // fullResolution marks the second, on-demand pass a zoom past the panel
    // width asks for: it swaps a sharper bitmap into a picture the user is
    // already looking at, so it must not disturb the zoom, the pan, or - if
    // the decode fails this time - the perfectly good image on screen.
    private void LoadViewerImage(string path, int decodeWidth, bool fullResolution = false)
    {
        BitmapImage? bitmap = null;
        int pixelWidth = 0, pixelHeight = 0;
        long fileLength = 0;
        DateTime modified = default;
        try
        {
            var fileInfo = new FileInfo(path);
            fileLength = fileInfo.Length;
            modified = fileInfo.LastWriteTime;

            // ONE open for both the header and the decode: rewind the same
            // stream rather than handing the decoder a Uri, which opened the
            // file a second time (two open/read passes per picture, and the
            // browsing hot path pays it on every arrow key - worst on a NAS).
            //
            // The Uri also had to be right, and a path is not a URL: a file
            // named "photo#1.jpg" was cut off at the '#' and one named
            // "a%20b.png" went looking for "a b.png", so a perfectly good
            // picture failed to decode and fell back to its file-type icon
            // with nothing said (2026-08-09 review). A stream has no such
            // grammar. The GIF path already read its bytes this way.
            //
            // CacheOption.OnLoad is what makes closing the stream right after
            // EndInit safe - the pixels are already in the bitmap.
            using (var stream = File.OpenRead(path))
            {
                var frame = BitmapDecoder.Create(stream,
                    BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];
                pixelWidth = frame.PixelWidth;
                pixelHeight = frame.PixelHeight;

                stream.Position = 0;
                bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = stream;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                if (pixelWidth > decodeWidth)
                {
                    bitmap.DecodePixelWidth = decodeWidth;
                }
                bitmap.EndInit();
                bitmap.Freeze();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException
                                       or FileFormatException or ArgumentException or UriFormatException)
        {
            // No WIC codec (webp/heic without the store extension), unreadable
            // or vanished file - fall through to the shell icon below.
            bitmap = null;
        }

        Dispatcher.BeginInvoke(() =>
        {
            if (!_viewerOpen || !string.Equals(_pendingViewerPath, path, StringComparison.OrdinalIgnoreCase))
            {
                if (fullResolution)
                {
                    _viewerFullResPending = false;
                }
                return;
            }

            if (fullResolution)
            {
                _viewerFullResPending = false;
                // A failed second pass leaves the first one alone: the picture
                // on screen is still correct, just softer than it could be.
                if (bitmap is null)
                {
                    return;
                }

                _viewerDecodedWidth = bitmap.PixelWidth;
                ViewerImage.Source = bitmap;
                return;
            }

            if (bitmap is null)
            {
                ShowViewerIcon(path);
                return;
            }

            ViewerIconImage.Visibility = Visibility.Collapsed;
            ViewerIconImage.Source = null;
            ViewerImage.Visibility = Visibility.Visible;
            ViewerImage.Source = bitmap;
            _viewerShowingDecodedImage = true;
            ViewerFileInfo.Text =
                $"{pixelWidth} × {pixelHeight}  ·  {FormatFileSize(fileLength)}  ·  {modified:yyyy-MM-dd HH:mm}";

            _viewerPixelWidth = pixelWidth;
            _viewerPixelHeight = pixelHeight;
            _viewerDecodedWidth = bitmap.PixelWidth;

            // A new picture arrives fitted (user's call, round 2): a zoom
            // carried over from the last file lands somewhere arbitrary in
            // this one, and arrow-keying a folder would show a different
            // corner of every image. A RE-load of the same file is not that -
            // the width drags deliberately reload to re-decode at the settled
            // size, and resetting there threw the zoom away on every resize.
            if (!string.Equals(_viewerZoomPath, path, StringComparison.OrdinalIgnoreCase))
            {
                _viewerZoomPath = path;
                // The REMEMBERED rest state, not always fit: picking 1:1 once
                // is meant to carry down a folder.
                _viewerZoom = ViewerRestZoom;
                ViewerZoomPan.X = 0;
                ViewerZoomPan.Y = 0;
            }

            ApplyViewerZoom();
        });
    }

    // ----- GIF 재생 --------------------------------------------------------
    //
    // WPF's Image draws a GIF's first frame and stops, so the panel plays one
    // by hand with the codec Windows already ships (GifBitmapDecoder) - no
    // dependency taken. What runs per TICK, at the GIF's own frame rate and
    // only while one is showing: decode the next frame (OnDemand, from the
    // in-memory copy of the file), blend its rectangle into a byte canvas,
    // one WritePixels. Ticks stand down while a window resize is in flight -
    // the resize-frame rule stays two doubles.
    //
    // Composition covers the spec's living parts: per-frame rectangle
    // (left/top), transparency (alpha-0 pixels leave the canvas as it was),
    // disposal 2 (clear the rectangle back to transparent). Disposal 3
    // (restore-previous) is approximated as clear - it is practically
    // extinct. The loop count is ignored: the panel loops forever, like
    // every viewer. Zoom, pan, navigator and full screen ride on top
    // unchanged, because the Source is ONE WriteableBitmap whose pixels
    // change; _viewerDecodedWidth is set to the full width so the
    // full-resolution second pass never tries to reload a playing GIF.

    private System.Windows.Threading.DispatcherTimer? _viewerGifTimer;
    private GifBitmapDecoder? _viewerGifDecoder;
    private MemoryStream? _viewerGifStream;
    private WriteableBitmap? _viewerGifCanvas;
    private byte[]? _viewerGifPixels;
    private List<ViewerGifFrame>? _viewerGifFrames;
    private int _viewerGifNextFrame;

    private readonly record struct ViewerGifFrame(int Left, int Top, int DelayMs, int Disposal);

    private void LoadViewerGif(string path)
    {
        Task.Run(() =>
        {
            byte[] bytes;
            DateTime modified;
            try
            {
                bytes = File.ReadAllBytes(path);
                modified = File.GetLastWriteTime(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    if (_viewerOpen && string.Equals(_pendingViewerPath, path, StringComparison.OrdinalIgnoreCase))
                    {
                        ShowViewerIcon(path);
                    }
                });
                return;
            }

            Dispatcher.BeginInvoke(() => SetUpViewerGif(path, bytes, modified));
        });
    }

    // UI thread on purpose: BitmapDecoder is a DispatcherObject, and the
    // per-tick decode has to touch it from here anyway. Creation itself is
    // cheap - DelayCreation/OnDemand defers the actual pixel work.
    private void SetUpViewerGif(string path, byte[] bytes, DateTime modified)
    {
        if (!_viewerOpen || !string.Equals(_pendingViewerPath, path, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var stream = new MemoryStream(bytes);
        GifBitmapDecoder decoder;
        try
        {
            decoder = new GifBitmapDecoder(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.OnDemand);
            if (decoder.Frames.Count <= 1)
            {
                throw new FileFormatException();
            }
        }
        catch (Exception ex) when (ex is IOException or FileFormatException or NotSupportedException
                                       or ArgumentException or InvalidOperationException)
        {
            // Single-frame or undecodable - the still path knows what to do
            // with both.
            stream.Dispose();
            int decodeWidth = ViewerDecodeWidth();
            Task.Run(() => LoadViewerImage(path, decodeWidth));
            return;
        }

        StopViewerGif();
        _viewerGifStream = stream;
        _viewerGifDecoder = decoder;

        // The logical screen from the header - a frame is allowed to be
        // smaller than the screen it plays on, so frame 0 is only a fallback.
        int width = 0, height = 0;
        try
        {
            if (decoder.Metadata?.GetQuery("/logscrdesc/Width") is ushort w)
            {
                width = w;
            }
            if (decoder.Metadata?.GetQuery("/logscrdesc/Height") is ushort h)
            {
                height = h;
            }
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or InvalidOperationException
                                       or System.Runtime.InteropServices.COMException)
        {
        }
        if (width <= 0 || height <= 0)
        {
            width = decoder.Frames[0].PixelWidth;
            height = decoder.Frames[0].PixelHeight;
        }

        var frames = new List<ViewerGifFrame>(decoder.Frames.Count);
        foreach (var frame in decoder.Frames)
        {
            int left = 0, top = 0, delay = 10, disposal = 0;
            try
            {
                if (frame.Metadata is BitmapMetadata meta)
                {
                    if (meta.GetQuery("/imgdesc/Left") is ushort l)
                    {
                        left = l;
                    }
                    if (meta.GetQuery("/imgdesc/Top") is ushort t)
                    {
                        top = t;
                    }
                    if (meta.GetQuery("/grctlext/Delay") is ushort d)
                    {
                        delay = d;
                    }
                    if (meta.GetQuery("/grctlext/Disposal") is byte dp)
                    {
                        disposal = dp;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or NotSupportedException or InvalidOperationException
                                           or System.Runtime.InteropServices.COMException)
            {
            }
            // Delay is in 1/100s; 0 means "as fast as possible", which every
            // real viewer reads as the de-facto 100ms.
            frames.Add(new ViewerGifFrame(left, top, delay == 0 ? 100 : delay * 10, disposal));
        }
        _viewerGifFrames = frames;

        _viewerGifCanvas = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
        _viewerGifPixels = new byte[width * height * 4];
        _viewerGifNextFrame = 0;

        // Mirror of LoadViewerImage's arrival block.
        ViewerIconImage.Visibility = Visibility.Collapsed;
        ViewerIconImage.Source = null;
        ViewerImage.Visibility = Visibility.Visible;
        ViewerImage.Source = _viewerGifCanvas;
        _viewerShowingDecodedImage = true;
        ViewerFileInfo.Text =
            $"{width} × {height}  ·  {FormatFileSize(bytes.LongLength)}  ·  {modified:yyyy-MM-dd HH:mm}";

        _viewerPixelWidth = width;
        _viewerPixelHeight = height;
        _viewerDecodedWidth = width;

        if (!string.Equals(_viewerZoomPath, path, StringComparison.OrdinalIgnoreCase))
        {
            _viewerZoomPath = path;
            _viewerZoom = ViewerRestZoom;
            ViewerZoomPan.X = 0;
            ViewerZoomPan.Y = 0;
        }
        ApplyViewerZoom();

        AdvanceViewerGifFrame();
    }

    private void AdvanceViewerGifFrame()
    {
        if (_viewerGifDecoder is null || _viewerGifCanvas is null ||
            _viewerGifPixels is null || _viewerGifFrames is null)
        {
            return;
        }

        int index = _viewerGifNextFrame;
        var info = _viewerGifFrames[index];
        int canvasWidth = _viewerGifCanvas.PixelWidth;
        int canvasHeight = _viewerGifCanvas.PixelHeight;

        try
        {
            var frame = _viewerGifDecoder.Frames[index];
            var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
            int frameWidth = converted.PixelWidth;
            int frameHeight = converted.PixelHeight;
            var framePixels = new byte[frameWidth * 4 * frameHeight];
            converted.CopyPixels(framePixels, frameWidth * 4, 0);

            for (int y = 0; y < frameHeight; y++)
            {
                int canvasY = info.Top + y;
                if (canvasY < 0 || canvasY >= canvasHeight)
                {
                    continue;
                }
                for (int x = 0; x < frameWidth; x++)
                {
                    int canvasX = info.Left + x;
                    if (canvasX < 0 || canvasX >= canvasWidth)
                    {
                        continue;
                    }
                    int src = (y * frameWidth + x) * 4;
                    if (framePixels[src + 3] == 0)
                    {
                        continue;
                    }
                    int dst = (canvasY * canvasWidth + canvasX) * 4;
                    _viewerGifPixels[dst] = framePixels[src];
                    _viewerGifPixels[dst + 1] = framePixels[src + 1];
                    _viewerGifPixels[dst + 2] = framePixels[src + 2];
                    _viewerGifPixels[dst + 3] = framePixels[src + 3];
                }
            }

            _viewerGifCanvas.WritePixels(new Int32Rect(0, 0, canvasWidth, canvasHeight),
                _viewerGifPixels, canvasWidth * 4, 0);

            // Disposal prepares the BUFFER for the next frame, after this one
            // has been shown.
            if (info.Disposal is 2 or 3)
            {
                int clearLeft = Math.Max(0, info.Left);
                int clearRight = Math.Min(canvasWidth, info.Left + frameWidth);
                if (clearRight > clearLeft)
                {
                    for (int y = 0; y < frameHeight; y++)
                    {
                        int canvasY = info.Top + y;
                        if (canvasY < 0 || canvasY >= canvasHeight)
                        {
                            continue;
                        }
                        Array.Clear(_viewerGifPixels,
                            (canvasY * canvasWidth + clearLeft) * 4,
                            (clearRight - clearLeft) * 4);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or FileFormatException or NotSupportedException
                                       or ArgumentException or InvalidOperationException)
        {
            // A frame that won't decode ends the animation on whatever is
            // showing - the keep-the-good-image rule, animated edition.
            StopViewerGif();
            return;
        }

        _viewerGifNextFrame = (index + 1) % _viewerGifFrames.Count;
        ScheduleViewerGifTick(info.DelayMs);
    }

    private void ScheduleViewerGifTick(int delayMs)
    {
        if (_viewerGifTimer is null)
        {
            _viewerGifTimer = new System.Windows.Threading.DispatcherTimer();
            _viewerGifTimer.Tick += (_, _) =>
            {
                _viewerGifTimer!.Stop();
                if (_viewerGifDecoder is null)
                {
                    return;
                }
                // Stand down while a resize is in flight; try again once the
                // settle window has passed.
                if (_viewerResizing)
                {
                    _viewerGifTimer.Interval = TimeSpan.FromMilliseconds(160);
                    _viewerGifTimer.Start();
                    return;
                }
                AdvanceViewerGifFrame();
            };
        }

        _viewerGifTimer.Stop();
        _viewerGifTimer.Interval = TimeSpan.FromMilliseconds(delayMs);
        _viewerGifTimer.Start();
    }

    // Safe to call at any time; the Source keeps showing the last composed
    // frame until whatever replaces it lands.
    private void StopViewerGif()
    {
        _viewerGifTimer?.Stop();
        _viewerGifDecoder = null;
        _viewerGifFrames = null;
        _viewerGifCanvas = null;
        _viewerGifPixels = null;
        _viewerGifNextFrame = 0;
        _viewerGifStream?.Dispose();
        _viewerGifStream = null;
    }

    // Fit is a ratio, so it moves with the panel - and a zoom held at, say,
    // 200% has to STAY 200% while the window resizes, which means recomputing
    // the transform (it is stored relative to fit) on every frame of it.
    // That part is two doubles. Everything else waits for the size to settle.
    private void ViewerImageHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_viewerOpen || _viewerPixelWidth <= 0)
        {
            return;
        }

        // HighQuality resampling of a large bitmap, every frame, is the other
        // half of the cost - and it only gets worse once the full-resolution
        // decode has been swapped in. Bilinear while the size is moving; the
        // sharp one comes back with everything else at the end.
        _viewerResizing = true;
        RenderOptions.SetBitmapScalingMode(ViewerImage, BitmapScalingMode.LowQuality);

        ApplyViewerZoomTransform();
        ScheduleViewerSettle();
    }

    // Fires once the window has stopped changing size. Short enough that the
    // sharp image and the readout feel like they belong to the gesture, long
    // enough that a drag never reaches it mid-flight.
    private void ScheduleViewerSettle()
    {
        if (_viewerSettleTimer is null)
        {
            _viewerSettleTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(160),
            };
            _viewerSettleTimer.Tick += (_, _) =>
            {
                _viewerSettleTimer!.Stop();
                _viewerResizing = false;
                RenderOptions.SetBitmapScalingMode(ViewerImage, BitmapScalingMode.HighQuality);
                if (_viewerOpen && _viewerPixelWidth > 0)
                {
                    ApplyViewerZoom();
                }
            };
        }

        _viewerSettleTimer.Stop();
        _viewerSettleTimer.Start();
    }

    // The scale the Uniform stretch is already drawing at - the same
    // arithmetic WPF does, repeated here because the number is needed for the
    // readout and for every zoom step, and the Image element's ActualWidth
    // reports the SLOT it fills, not the picture inside it.
    private double ViewerFitScale
    {
        get
        {
            if (_viewerPixelWidth <= 0 || _viewerPixelHeight <= 0)
            {
                return 1;
            }

            var margin = ViewerImage.Margin;
            double availableWidth = ViewerImageHost.ActualWidth - margin.Left - margin.Right;
            double availableHeight = ViewerImageHost.ActualHeight - margin.Top - margin.Bottom;
            if (availableWidth <= 0 || availableHeight <= 0)
            {
                return 1;
            }

            return Math.Min(availableWidth / _viewerPixelWidth, availableHeight / _viewerPixelHeight);
        }
    }

    // The scale the picture RESTS at. In the panel that is plain fit, ceiling
    // and all - the round-1 call was that a small picture may scale up to fill
    // the slot. Full screen it stops at 100%: "작은건 작게 큰건 맞추면" (user,
    // 2026-08-08) - big pictures fit the monitor, small ones stay their own
    // size rather than being blown up across a 4K screen. That ceiling is also
    // what makes a 3840 picture on a 3840 screen land at exactly 1:1: the raw
    // fit works out a hair over 1.0, because a maximized WPF window is a few
    // pixels wider than the screen it covers, and the ceiling swallows it.
    private double ViewerRestScale =>
        _viewerFullscreen ? Math.Min(1, ViewerFitScale) : ViewerFitScale;

    private double ViewerDisplayScale => _viewerZoom ?? ViewerRestScale;

    // Only a picture bigger than its panel can be dragged; at or below fit
    // there is nothing off-screen to bring into view, so the cursor stays
    // ordinary and a drag there does nothing at all.
    private bool ViewerCanPan =>
        _viewerPixelWidth > 0 &&
        (_viewerPixelWidth * ViewerDisplayScale > ViewerImageHost.ActualWidth + 0.5 ||
         _viewerPixelHeight * ViewerDisplayScale > ViewerImageHost.ActualHeight + 0.5);

    // The one thing that touches the transform. Everything else decides what
    // the zoom should BE and then calls this - so there is exactly one place
    // where a frame's worth of work happens, and it is two doubles and a
    // clamp. No decode, no measure, no layout pass.
    // The per-frame half, and deliberately nothing else: two doubles and a
    // clamp, whatever the picture's size. A live window resize raises a
    // SizeChanged per frame, and everything the full method below does on top
    // of this - a new string in the zoom readout, four chips re-deciding their
    // checked/enabled state, the cursor, the navigator, the full-resolution
    // decode - was riding every one of those frames, which is what made a
    // floating resize stutter "이미지 사이즈와 관계 없이" (user, 2026-08-09).
    private void ApplyViewerZoomTransform()
    {
        // The RAW fit here, never the rest scale: this converts to the
        // transform, and what the Stretch has already drawn is the raw fit.
        double fit = ViewerFitScale;
        double display = ViewerDisplayScale;

        double relative = fit > 0 ? display / fit : 1;
        ViewerZoomScale.ScaleX = relative;
        ViewerZoomScale.ScaleY = relative;

        ClampViewerPan();
    }

    private void ApplyViewerZoom()
    {
        ApplyViewerZoomTransform();
        // Asked here rather than only from SetViewerZoom, which was the first
        // version and left two ways in uncovered (reported 2026-08-08): going
        // full screen, and a new picture arriving while already full screen,
        // both change how big the bitmap is drawn without any zoom happening -
        // so the panel-width decode stayed on screen, soft, until the wheel
        // was turned. This method is the one thing every one of those paths
        // ends at. It stays cheap: the request guards on already-pending,
        // already-whole, and already-big-enough before it starts anything.
        RequestViewerFullResolution();
        UpdateViewerNavigator();
        UpdateViewerZoomBar();
        ViewerImageHost.Cursor = ViewerCanPan
            ? (_viewerPanning ? System.Windows.Input.Cursors.ScrollAll : System.Windows.Input.Cursors.SizeAll)
            : null;
    }

    // Keeps the picture from being dragged off its own panel: an axis smaller
    // than the panel is pinned centred, a larger one can travel exactly as far
    // as its overhang and no further.
    private void ClampViewerPan()
    {
        double display = ViewerDisplayScale;
        double overhangX = (_viewerPixelWidth * display - ViewerImageHost.ActualWidth) / 2;
        double overhangY = (_viewerPixelHeight * display - ViewerImageHost.ActualHeight) / 2;
        double limitX = Math.Max(0, overhangX);
        double limitY = Math.Max(0, overhangY);
        ViewerZoomPan.X = Math.Clamp(ViewerZoomPan.X, -limitX, limitX);
        ViewerZoomPan.Y = Math.Clamp(ViewerZoomPan.Y, -limitY, limitY);
    }

    private void UpdateViewerZoomBar()
    {
        bool hasImage = _viewerPixelWidth > 0 && ViewerImage.Source is not null;
        ViewerZoomBar.Visibility = hasImage ? Visibility.Visible : Visibility.Collapsed;
        if (!hasImage)
        {
            return;
        }

        double fit = ViewerRestScale;
        double display = _viewerZoom ?? fit;
        // The real percentage even while fitted (a fit at 34% says "34%"),
        // which is the number someone reading the strip actually wants; the
        // lit 맞춤 chip beside it already says WHICH state that number is.
        ViewerZoomText.Text = $"{Math.Round(display * 100)}%";
        ViewerFitChip.IsChecked = _viewerZoom is null;
        ViewerActualChip.IsChecked = _viewerZoom is { } z && Math.Abs(z - 1) < 0.001;
        ViewerZoomOutButton.IsEnabled = display > ViewerZoomSteps[0] + 0.001;
        ViewerZoomInButton.IsEnabled = display < ViewerZoomSteps[^1] - 0.001;
        ViewerNavigatorChip.IsChecked = _settings.ViewerNavigator;
    }

    // Below this the plate would be taking a serious bite out of the picture
    // it is meant to help read - the panel's own floor is 240px wide.
    private const double ViewerNavigatorMinHostWidth = 220;
    private const double ViewerNavigatorMinHostHeight = 160;
    // Proportional to the panel's shorter side, so the same plate that reads
    // right beside a 900px panel doesn't become a postage stamp once the
    // viewer has the whole 4K screen (user, 2026-08-08). The ceiling is what
    // keeps "bigger screen" from turning into "map instead of picture".
    // The FLOOR is relative too: a fixed 56px left a postage stamp exactly
    // where precision is scarcest, the small panel (user, 2026-08-09) - so
    // small panels floor at MinSideShare of the shorter side, capped at
    // MinSide. Note `side` is the plate's LONGER edge: a wide picture's
    // plate height is this divided by the aspect again, which is why the
    // first bump (120) still read "아까랑 비슷한데요" on a 16:9 image - the
    // floor has to be generous to survive that division. The 0.22 ratio
    // takes over past ~773px; large panels are unchanged.
    private const double ViewerNavigatorSideRatio = 0.22;
    private const double ViewerNavigatorMinSide = 170;
    private const double ViewerNavigatorMinSideShare = 0.45;
    private const double ViewerNavigatorMaxSide = 280;

    // The whole picture, small, with a box around the part the panel is
    // showing. Everything it needs was already being computed for the zoom -
    // the visible region in image pixels is the panel's size divided by the
    // scale, centred on the pan - so this adds arithmetic, not state.
    private void UpdateViewerNavigator()
    {
        if (!_settings.ViewerNavigator ||
            _viewerPixelWidth <= 0 ||
            ViewerImage.Source is null ||
            !ViewerCanPan ||
            ViewerImageHost.ActualWidth < ViewerNavigatorMinHostWidth ||
            ViewerImageHost.ActualHeight < ViewerNavigatorMinHostHeight)
        {
            ViewerNavigatorPlate.Visibility = Visibility.Collapsed;
            return;
        }

        // The same bitmap the panel is already showing - a navigator this size
        // has no use for its own decode, at either resolution.
        if (!ReferenceEquals(ViewerNavigatorImage.Source, ViewerImage.Source))
        {
            ViewerNavigatorImage.Source = ViewerImage.Source;
        }

        double shorter = Math.Min(ViewerImageHost.ActualWidth, ViewerImageHost.ActualHeight);
        double side = Math.Clamp(
            shorter * ViewerNavigatorSideRatio,
            Math.Min(shorter * ViewerNavigatorMinSideShare, ViewerNavigatorMinSide),
            ViewerNavigatorMaxSide);
        double plateWidth, plateHeight;
        if (_viewerPixelWidth >= _viewerPixelHeight)
        {
            plateWidth = side;
            plateHeight = Math.Max(16, side * _viewerPixelHeight / _viewerPixelWidth);
        }
        else
        {
            plateHeight = side;
            plateWidth = Math.Max(16, side * _viewerPixelWidth / _viewerPixelHeight);
        }

        ViewerNavigatorPlate.Width = plateWidth;
        ViewerNavigatorPlate.Height = plateHeight;
        ViewerNavigatorPlate.Visibility = Visibility.Visible;

        // The plate's 1px border sits OUTSIDE the content the box lives in,
        // so every map computation runs on the inner size - mapping against
        // the outer one pushed the box past the right/bottom edge by the
        // border's width (user, 2026-08-09: "살짝 벗어나네요").
        double innerWidth = Math.Max(1, plateWidth - 2);
        double innerHeight = Math.Max(1, plateHeight - 2);

        double display = ViewerDisplayScale;
        double mapScale = innerWidth / _viewerPixelWidth;
        double visibleWidth = ViewerImageHost.ActualWidth / display;
        double visibleHeight = ViewerImageHost.ActualHeight / display;
        // Pan moves the picture, so the viewport travels the other way.
        double visibleLeft =
            _viewerPixelWidth / 2.0 - (ViewerZoomPan.X + ViewerImageHost.ActualWidth / 2) / display;
        double visibleTop =
            _viewerPixelHeight / 2.0 - (ViewerZoomPan.Y + ViewerImageHost.ActualHeight / 2) / display;

        double boxWidth = Math.Min(innerWidth, visibleWidth * mapScale);
        double boxHeight = Math.Min(innerHeight, visibleHeight * mapScale);

        // NaN on the first pass, and every comparison against NaN is false -
        // so ask about NaN explicitly or the box never gets its first size.
        if (double.IsNaN(ViewerNavigatorBox.Width) ||
            Math.Abs(ViewerNavigatorBox.Width - boxWidth) > 0.5 ||
            Math.Abs(ViewerNavigatorBox.Height - boxHeight) > 0.5)
        {
            ViewerNavigatorBox.Width = boxWidth;
            ViewerNavigatorBox.Height = boxHeight;
        }

        ViewerNavigatorBoxOffset.X = Math.Clamp(
            visibleLeft * mapScale, 0, Math.Max(0, innerWidth - boxWidth));
        ViewerNavigatorBoxOffset.Y = Math.Clamp(
            visibleTop * mapScale, 0, Math.Max(0, innerHeight - boxHeight));

        // The scrim darkens everything BUT the box - same numbers, so the
        // hole and the border always agree. Geometry Rect writes are
        // render-only, the same per-frame bill as the transform above.
        ViewerNavigatorScrimOuter.Rect = new Rect(0, 0, innerWidth, innerHeight);
        ViewerNavigatorScrimHole.Rect = new Rect(
            ViewerNavigatorBoxOffset.X, ViewerNavigatorBoxOffset.Y, boxWidth, boxHeight);
    }

    private void ViewerNavigatorChip_Click(object sender, RoutedEventArgs e)
    {
        _settings.ViewerNavigator = ViewerNavigatorChip.IsChecked == true;
        _settingsService.Save(_settings);
        UpdateViewerNavigator();
    }

    // The navigator as a MAP (user, 2026-08-09: they kept grabbing the white
    // box and getting a whole-image pan): press anywhere on the plate and
    // the view CENTRES on that point, then follows the drag - absolute per
    // event, never accumulated, for the pan drag's own reason. The
    // arithmetic is UpdateViewerNavigator's run backwards, and collapses
    // nicely: centring the viewport on image point c makes the pan simply
    // (pixelCentre − c) · displayScale, with the standing clamp doing the
    // rest. Per frame this is two doubles, a clamp, and the navigator's own
    // transform update - the same bill as a picture pan frame.
    private bool _viewerNavigatorDragging;

    private void ViewerNavigatorPlate_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!ViewerCanPan)
        {
            return;
        }

        _viewerNavigatorDragging = true;
        ViewerNavigatorPlate.CaptureMouse();
        CenterViewerOnNavigatorPoint(e.GetPosition(ViewerNavigatorPlate));
        e.Handled = true;
    }

    private void ViewerNavigatorPlate_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_viewerNavigatorDragging)
        {
            return;
        }

        CenterViewerOnNavigatorPoint(e.GetPosition(ViewerNavigatorPlate));
    }

    private void ViewerNavigatorPlate_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_viewerNavigatorDragging)
        {
            return;
        }

        _viewerNavigatorDragging = false;
        ViewerNavigatorPlate.ReleaseMouseCapture();
    }

    // SAFETY DEVICE, the pan's own mirrored here: capture can end without a
    // mouse-up (another window, the stuck-capture watchdog), and the flag
    // surviving that would glue the view to every bare mouse move over the
    // plate. What it hides: any path that ends this drag other than the
    // user letting go.
    private void ViewerNavigatorPlate_LostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _viewerNavigatorDragging = false;
    }

    private void CenterViewerOnNavigatorPoint(System.Windows.Point platePoint)
    {
        // Inner size and a 1px inset, exactly as UpdateViewerNavigator maps -
        // the plate's border is outside the content, and one scale serves
        // both axes; disagreeing with the drawn box on either would make the
        // drag drift off the cursor.
        double innerWidth = ViewerNavigatorPlate.ActualWidth - 2;
        if (innerWidth <= 0 || _viewerPixelWidth <= 0)
        {
            return;
        }

        double mapScale = innerWidth / _viewerPixelWidth;
        double display = ViewerDisplayScale;
        ViewerZoomPan.X = (_viewerPixelWidth / 2.0 - (platePoint.X - 1) / mapScale) * display;
        ViewerZoomPan.Y = (_viewerPixelHeight / 2.0 - (platePoint.Y - 1) / mapScale) * display;
        ClampViewerPan();
        UpdateViewerNavigator();
    }

    // zoom == null means fit. The anchor is a point in the host's coordinates
    // that should keep showing the same pixel of the picture across the change
    // - the cursor for a wheel turn, nothing for a button (which zooms about
    // the centre, since there is no cursor position that means anything).
    private void SetViewerZoom(double? zoom, System.Windows.Point? anchor)
    {
        double before = ViewerDisplayScale;

        if (zoom is null)
        {
            _viewerZoom = null;
            ViewerZoomPan.X = 0;
            ViewerZoomPan.Y = 0;
        }
        else
        {
            _viewerZoom = zoom;
            if (anchor is { } point && before > 0)
            {
                // The content point under the anchor sits at (anchor - pan)
                // from the centre, measured in today's scale; after the change
                // it has to sit at the same place on screen, so the pan moves
                // to anchor - (anchor - pan) * (after / before).
                double ratio = zoom.Value / before;
                double fromCentreX = point.X - ViewerImageHost.ActualWidth / 2;
                double fromCentreY = point.Y - ViewerImageHost.ActualHeight / 2;
                ViewerZoomPan.X = fromCentreX - (fromCentreX - ViewerZoomPan.X) * ratio;
                ViewerZoomPan.Y = fromCentreY - (fromCentreY - ViewerZoomPan.Y) * ratio;
            }
        }

        // ApplyViewerZoom is the funnel and asks for the full-resolution pass
        // itself - this used to call it again right here, from before that
        // move, which ran the whole guard chain twice per notch.
        ApplyViewerZoom();
    }

    // One rung of the ladder, with fit as the floor: stepping down past the
    // rung nearest fit lands ON fit rather than somewhere slightly smaller
    // than the panel, and there is nothing below that.
    private void StepViewerZoom(int direction, System.Windows.Point? anchor)
    {
        if (_viewerPixelWidth <= 0 || ViewerImage.Source is null)
        {
            return;
        }

        double fit = ViewerRestScale;
        double current = _viewerZoom ?? fit;

        if (direction > 0)
        {
            foreach (double step in ViewerZoomSteps)
            {
                if (step > current + 0.001)
                {
                    SetViewerZoom(step, anchor);
                    return;
                }
            }
            return;
        }

        for (int i = ViewerZoomSteps.Length - 1; i >= 0; i--)
        {
            if (ViewerZoomSteps[i] < current - 0.001)
            {
                SetViewerZoom(ViewerZoomSteps[i], anchor);
                return;
            }
        }
    }

    // Round 1 decodes at panel width, which is the right size for a picture
    // that never grows past fit and the wrong one the moment it does - an 8K
    // wallpaper decoded for a 900px slot goes soft as soon as it is bigger
    // than the slot. Ask for the full-resolution decode once, the first time
    // the zoom actually needs it, and never for a picture that is already
    // whole.
    private void RequestViewerFullResolution()
    {
        // Never mid-resize: the slot outgrows the current decode on the first
        // frame of a drag, and answering that with a full-resolution decode
        // puts an 8-megapixel bitmap in front of a scaler that then has to
        // rework it every frame for the rest of the drag.
        if (_viewerResizing ||
            _viewerFullResPending ||
            _pendingViewerPath is not { } path ||
            _viewerPixelWidth <= 0 ||
            _viewerDecodedWidth >= _viewerPixelWidth)
        {
            return;
        }

        double onScreenWidth =
            _viewerPixelWidth * ViewerDisplayScale * VisualTreeHelper.GetDpi(this).DpiScaleX;
        if (onScreenWidth <= _viewerDecodedWidth + 1)
        {
            return;
        }

        _viewerFullResPending = true;
        // SAFETY DEVICE: the flag is normally cleared inside LoadViewerImage's
        // own dispatcher callback - but that callback never runs at all if the
        // decode dies from something the loader's catch filter doesn't name.
        // This pass decodes with NO DecodePixelWidth, so OutOfMemoryException
        // on a huge picture is the realistic one. The flag would then stay
        // true for the rest of the session and every later picture would
        // silently stop asking for its sharp decode. What this hides: any
        // failure of the full-resolution pass other than the ones the loader
        // handles - the picture on screen stays the good one either way
        // (2026-08-09 review).
        Task.Run(() =>
        {
            try
            {
                LoadViewerImage(path, int.MaxValue, fullResolution: true);
            }
            catch (Exception ex) when (ex is OutOfMemoryException
                                           or System.Runtime.InteropServices.COMException)
            {
            }
            finally
            {
                Dispatcher.BeginInvoke(() => _viewerFullResPending = false);
            }
        });
    }

    private void ViewerImageHost_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        // The picture never scrolls, so the wheel is free to mean zoom with no
        // modifier - and it has to be Handled either way, or the notch carries
        // on into whatever is behind the panel.
        e.Handled = true;
        StepViewerZoom(Math.Sign(e.Delta), e.GetPosition(ViewerImageHost));
    }

    // Middle-click on the picture toggles a full cover: the window maximizes
    // (floating only) and the viewer takes the tree's column as well. Docked,
    // the window is a parked band with a clip region and margins, and
    // maximizing it would fight every rule the dock geometry runs on - so
    // there the mode is columns only, which is the one resize in this app with
    // no dock interaction at all.
    private void SetViewerFullscreen(bool on)
    {
        if (!_viewerOpen || _viewerFullscreen == on)
        {
            return;
        }

        _viewerFullscreen = on;

        if (on)
        {
            if (!_isDocked && WindowState != WindowState.Maximized)
            {
                _viewerFullscreenPreviousState = WindowState;
                WindowState = WindowState.Maximized;
            }
        }
        else if (_viewerFullscreenPreviousState is { } previous)
        {
            _viewerFullscreenPreviousState = null;
            WindowState = previous;
        }

        // The header goes with it. Without this the mode was just "the window
        // is maximized" (user, 2026-08-08) - and worse, entering it from a
        // window ALREADY maximized by a header double-click changed nothing
        // but the tree, so the same middle-click looked like it was opening
        // and closing the explorer rather than entering a mode. With the
        // header gone, every route in looks the same and looks like a mode.
        // Row 0 is Auto-height, so collapsing both children collapses the row
        // and the viewer rises to the top of the window.
        // EVERY piece of chrome goes, not just the header. Keeping the caption
        // strip and the close button was the first attempt, on the reasoning
        // that the mode would otherwise have nothing to hold on to - but they
        // eat the picture's own space, so a 3840 image on a 3840 screen had
        // nowhere to be 1:1 (user, 2026-08-08: "보통 일반적으로 다른
        // 이미지뷰어들의 1:1은 아무 정보도 표시하지 않습니다"). Margins and
        // the panel's divider line go for the same reason - a 1px border is
        // still 1px the picture doesn't get. The way out is Esc, Enter or
        // middle-click; that is what every other viewer offers too.
        var chrome = on ? Visibility.Collapsed : Visibility.Visible;
        HeaderGrid.Visibility = chrome;
        HeaderUnderline.Visibility = chrome;
        // The row is a FIXED 36, not Auto, so hiding its contents left a 36px
        // band of background across the top of the picture (reported with a
        // screenshot, 2026-08-08). The height has to go too.
        HeaderRow.Height = on ? new GridLength(0) : new GridLength(36);
        ViewerCaptionPanel.Visibility = chrome;
        ViewerCloseButton.Visibility = chrome;
        ViewerImage.Margin = on ? default : new Thickness(10);
        ViewerPanel.BorderThickness = on
            ? default
            : (_viewerOnLeft ? new Thickness(0, 0, 1, 0) : new Thickness(1, 0, 0, 0));

        // Nothing to split while the viewer is the whole window.
        ViewerSplitThumb.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
        ViewerCollapseButton.Visibility = on ? Visibility.Collapsed : Visibility.Visible;

        // Both crossings land at the rest state. Carrying a zoom across meant
        // going full screen after one wheel turn in the panel put you at that
        // magnification on a 4K screen, looking at a corner of the picture
        // rather than the picture (reported 2026-08-08). Leaving resets for the
        // same reason in reverse: a zoom chosen for the whole screen is a
        // strange place to drop someone back into a 900px panel.
        _viewerZoom = ViewerRestZoom;
        ViewerZoomPan.X = 0;
        ViewerZoomPan.Y = 0;

        ClampViewerColumnToWindow();
        // The clamp usually resizes the column and the layout pass that
        // follows re-applies this - but not when the column was already the
        // width it wanted, so do it here too rather than depend on a resize.
        ApplyViewerZoom();
    }

    private void ViewerImageHost_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        SetViewerFullscreen(!_viewerFullscreen);
        e.Handled = true;
    }

    // Gate for the picture's context menu. Its file actions are the row
    // menu's handlers, and those all act on the SELECTION - so the menu may
    // only open while the picture on screen is that selection: something is
    // actually on the picture surface (not the icon fallback or an empty
    // panel) and the selected row's path is the one being shown. An active
    // multi-selection is collapsed to the shown file rather than blocking
    // the menu (user's call, 2026-08-09): the hand that right-clicked one
    // picture means that one, and the rows un-highlighting says so.
    private void ViewerImageHost_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (ViewerImage.Visibility != Visibility.Visible
            || ViewerImage.Source is null
            || ExplorerTree.SelectedItem is not FileSystemItem { IsPlaceholder: false, IsShowMore: false } item
            || !string.Equals(item.FullPath, _pendingViewerPath, StringComparison.OrdinalIgnoreCase))
        {
            e.Handled = true;
            return;
        }

        // Count > 0, not > 1: with any multi-selection alive the handlers act
        // on that list (GetEffectiveSelection), and even a single Ctrl-clicked
        // row in it need not be the row being shown.
        if (_multiSelection.Count > 0)
        {
            ClearMultiSelection();
        }

        ViewerSetWallpaperItem.Visibility = _viewerShowingDecodedImage
            ? Visibility.Visible
            : Visibility.Collapsed;
        // A folder can land here via its shell thumbnail; the picker is a
        // file-only verb (same rule the row menu applies).
        ViewerOpenWithItem.IsEnabled = !item.IsDirectory;
    }

    private void ViewerSetWallpaper_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingViewerPath is not string path)
        {
            return;
        }

        // Off the UI thread: the fallback inside re-encodes the full-size
        // original. Nothing to report either way - the desktop itself is the
        // feedback, and the quiet default is this app's standing rule.
        Task.Run(() => ShellFileService.TrySetDesktopWallpaper(path));
    }

    // The carousel row's world: the folder's image rows in the tree's current
    // sort order and filters - INCLUDING the ones still parked behind
    // "더 보기" (AllLoadedChildren). The count is a claim about the FOLDER:
    // counting only revealed rows made the total grow when 더 보기 was
    // clicked - and not even then, since the counter only recomputes on a
    // selection change - which read as the counter being wrong (user,
    // 2026-08-09). Counting the full in-memory list costs nothing and makes
    // the total independent of reveal state entirely; the chevron pays the
    // reveal only at the moment it actually crosses the boundary (see
    // ViewerCarouselStep). Images only, by extension: a mixed folder's
    // videos and documents still step fine on ↑↓.
    private static bool IsViewerCarouselImage(FileSystemItem item)
        => item is { IsDirectory: false, IsPlaceholder: false, IsShowMore: false }
           && ThumbnailExtensions.Contains(Path.GetExtension(item.FullPath));

    private List<FileSystemItem> GetViewerCarouselImages(FileSystemItem current)
    {
        IEnumerable<FileSystemItem> siblings = current.Parent?.AllLoadedChildren ?? _roots;
        return siblings.Where(IsViewerCarouselImage).ToList();
    }

    // Recomputed on every selection change rather than cached: the list is one
    // Where() over a folder's realized rows, and a cache would go stale on
    // every rename/delete/sort for nothing.
    private void UpdateViewerCarousel()
    {
        if (!_viewerOpen
            || _selectedItem is not { } current
            || !IsViewerCarouselImage(current))
        {
            ViewerCarouselBar.Visibility = Visibility.Collapsed;
            return;
        }

        var images = GetViewerCarouselImages(current);
        int index = images.IndexOf(current);
        if (index < 0)
        {
            ViewerCarouselBar.Visibility = Visibility.Collapsed;
            return;
        }

        ViewerCarouselBar.Visibility = Visibility.Visible;
        ViewerCarouselText.Text = $"{index + 1} / {images.Count}";
        // The invisible twin reserves the folder's widest possible string so
        // the chevron buttons hold still while the number walks.
        ViewerCarouselMaxText.Text = $"{images.Count} / {images.Count}";
        ViewerPrevButton.IsEnabled = index > 0;
        ViewerNextButton.IsEnabled = index < images.Count - 1;
    }

    // No wrap-around: a disabled chevron at either end says "you are at the
    // edge" more honestly than silently jumping 257 → 1 would.
    private void ViewerPrevButton_Click(object sender, RoutedEventArgs e) => ViewerCarouselStep(-1);

    private void ViewerNextButton_Click(object sender, RoutedEventArgs e) => ViewerCarouselStep(+1);

    private void ViewerCarouselStep(int direction)
    {
        if (_selectedItem is not { } current || !IsViewerCarouselImage(current))
        {
            return;
        }

        var images = GetViewerCarouselImages(current);
        int index = images.IndexOf(current);
        int next = index + direction;
        if (index < 0 || next < 0 || next >= images.Count)
        {
            return;
        }

        // Crossing the reveal boundary: the counter promised this picture,
        // so the chevron performs the same reveal the "더 보기" row would
        // and keeps going - stopping at the cap would make the total a lie.
        // Only > can get here (overflow rows only ever hide at the BOTTOM),
        // and the cost is exactly a hand-click on 더 보기.
        var target = images[next];
        if (current.Parent is { } parent && !parent.Children.Contains(target))
        {
            parent.ShowAllChildren();
        }

        // Selects, scrolls to and focuses the tree row; selection-follow then
        // brings the picture, so the chevrons never grow a second idea of
        // "next" to keep in sync (same reasoning as the Space key's).
        SelectVisibleItem(target);
    }

    private void ViewerImageHost_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // The gesture every viewer has: fit and 1:1 with nothing to aim
            // at. It lands on the same two states the chips do, so it moves
            // the remembered rest state with them.
            // At fit it goes to 1:1; from anywhere else - including a wheel
            // zoom - it comes back to fit, which is the behaviour it had.
            SetViewerRest(_viewerZoom is null);
            e.Handled = true;
            return;
        }

        if (!ViewerCanPan)
        {
            return;
        }

        _viewerPanning = true;
        _viewerPanOrigin = e.GetPosition(ViewerImageHost);
        _viewerPanOriginX = ViewerZoomPan.X;
        _viewerPanOriginY = ViewerZoomPan.Y;
        ViewerImageHost.CaptureMouse();
        ViewerImageHost.Cursor = System.Windows.Input.Cursors.ScrollAll;
        e.Handled = true;
    }

    private void ViewerImageHost_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_viewerPanning)
        {
            return;
        }

        // Absolute from where the drag started, not accumulated per event: an
        // accumulating pan drifts away from the cursor every time a move is
        // coalesced or a clamp bites.
        var point = e.GetPosition(ViewerImageHost);
        ViewerZoomPan.X = _viewerPanOriginX + (point.X - _viewerPanOrigin.X);
        ViewerZoomPan.Y = _viewerPanOriginY + (point.Y - _viewerPanOrigin.Y);
        ClampViewerPan();
        // The navigator box has to track the pan frame by frame - that IS its
        // job - and it is two transform values when it is on, nothing when it
        // is off. The rest of the settle work has no business here.
        UpdateViewerNavigator();
    }

    private void ViewerImageHost_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_viewerPanning)
        {
            return;
        }

        _viewerPanning = false;
        ViewerImageHost.ReleaseMouseCapture();
        ViewerImageHost.Cursor = ViewerCanPan ? System.Windows.Input.Cursors.SizeAll : null;
    }

    // SAFETY DEVICE: capture can end without a mouse-up ever arriving - another
    // window taking it, a system event, or the app's own stuck-capture watchdog
    // reclaiming a leak. The pan flag would survive all three, and then a bare
    // mouse move with no button held would drag the picture around. What this
    // hides: any path that ends a pan other than the user letting go.
    private void ViewerImageHost_LostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_viewerPanning)
        {
            return;
        }

        _viewerPanning = false;
        ViewerImageHost.Cursor = ViewerCanPan ? System.Windows.Input.Cursors.SizeAll : null;
    }

    // Nothing zoomable on screen: back to rest, and the strip goes away rather
    // than sitting there reading "100%" over a shell icon.
    private void ClearViewerZoom()
    {
        _viewerZoom = null;
        _viewerZoomPath = null;
        _viewerPixelWidth = 0;
        _viewerPixelHeight = 0;
        _viewerDecodedWidth = 0;
        _viewerPanning = false;
        ViewerZoomScale.ScaleX = 1;
        ViewerZoomScale.ScaleY = 1;
        ViewerZoomPan.X = 0;
        ViewerZoomPan.Y = 0;
        ViewerImageHost.Cursor = null;
        ViewerNavigatorImage.Source = null;
        UpdateViewerNavigator();
        UpdateViewerZoomBar();
    }

    // The two chips are the ONLY things that move the remembered rest state -
    // see _viewerRestAtActualSize.
    private void ViewerFitChip_Click(object sender, RoutedEventArgs e) => SetViewerRest(false);

    private void ViewerActualChip_Click(object sender, RoutedEventArgs e) => SetViewerRest(true);

    private void SetViewerRest(bool atActualSize)
    {
        _viewerRestAtActualSize = atActualSize;
        SetViewerZoom(ViewerRestZoom, null);
    }

    // null means fit, which is how the zoom stores it (fit is a ratio that
    // moves, so it has no number of its own).
    private double? ViewerRestZoom => _viewerRestAtActualSize ? 1 : null;

    private void ViewerZoomInButton_Click(object sender, RoutedEventArgs e) => StepViewerZoom(+1, null);

    private void ViewerZoomOutButton_Click(object sender, RoutedEventArgs e) => StepViewerZoom(-1, null);

    // Re-aims the panel at the side the current mode wants - screen-interior
    // (left column) when docked on the right edge, the right column
    // otherwise - and sizes the columns to match, clamped so the tree keeps
    // its split floor. One place instead of inline setup in OpenViewer,
    // because the panel now SURVIVES dock/undock/side changes (experiment,
    // 2026-08-08) and each of those has to re-aim it.
    private void ApplyViewerSide()
    {
        if (!_viewerOpen)
        {
            return;
        }

        // Dock, undock and side changes rebuild the split from scratch, and
        // the full cover has no place in one - leave it here, in the single
        // place all three pass through, so the columns below are the real ones.
        SetViewerFullscreen(false);

        _viewerOnLeft = _isDocked && _settings.DockOnRight;
        double panelWidth = Math.Min(ViewerPanelWidth, Math.Max(0, Width - MinTreeSplitWidth));

        ViewerColumnLeft.Width = new GridLength(_viewerOnLeft ? panelWidth : 0);
        ViewerColumnRight.Width = new GridLength(_viewerOnLeft ? 0 : panelWidth);

        // Re-anchor the tree here and only here among the app's own width
        // writes: every caller (open, dock, undock, side change) has already
        // set Width to the total it wants, so the remainder IS the tree share
        // being asked for. A window too narrow to hold the remembered panel
        // plus a tree floor is a SQUEEZE, not a new split - it must not
        // rewrite the anchor, or an auto-hide collapse to an 8px sliver would
        // record a 120px tree and the reveal would never grow back.
        if (!double.IsNaN(Width) && Width - ViewerPanelWidth >= MinTreeSplitWidth)
        {
            _viewerTreeShare = Width - ViewerPanelWidth;
        }
        Grid.SetColumn(ViewerPanel, _viewerOnLeft ? 0 : 2);
        // The divider faces the tree.
        ViewerPanel.BorderThickness = _viewerOnLeft
            ? new Thickness(0, 0, 1, 0)
            : new Thickness(1, 0, 0, 0);

        // The split grab zone rides the panel's tree-side edge.
        Grid.SetColumn(ViewerSplitThumb, _viewerOnLeft ? 0 : 2);
        ViewerSplitThumb.HorizontalAlignment = _viewerOnLeft
            ? System.Windows.HorizontalAlignment.Right
            : System.Windows.HorizontalAlignment.Left;
        ViewerSplitThumb.Margin = _viewerOnLeft
            ? new Thickness(0, 0, -4, 0)
            : new Thickness(-4, 0, 0, 0);

        // The collapse chevron rides the same edge, and points at the tree -
        // which is also the way the window's outer edge travels when the panel
        // closes (CloseViewer narrows toward whichever side the tree is on).
        Grid.SetColumn(ViewerCollapseButton, _viewerOnLeft ? 0 : 2);
        ViewerCollapseButton.HorizontalAlignment = _viewerOnLeft
            ? System.Windows.HorizontalAlignment.Right
            : System.Windows.HorizontalAlignment.Left;
        ViewerCollapseButton.Margin = _viewerOnLeft
            ? new Thickness(0, 0, -7, 0)
            : new Thickness(-7, 0, 0, 0);
        ViewerCollapseGlyph.Data = Geometry.Parse(
            _viewerOnLeft ? "M2,0 L6,5 L2,10" : "M6,0 L2,5 L6,10");

        UpdateViewerExpandButton();
    }

    // The panel column always fits the window, whatever resized it. The
    // app's own grips enforce the floors themselves, but the OS border
    // resize on a floating window bypasses them entirely - reported
    // 2026-08-08 as the viewer swallowing the tree AND pushing the header's
    // buttons out past the window edge (the fixed column had made the whole
    // grid wider than the window). Runs on every SizeChanged: the panel
    // yields until the tree keeps its split floor, and grows back toward
    // the REMEMBERED width when the window does - _settings.ViewerWidth is
    // deliberately never written here, so a squeeze is temporary.
    private void ClampViewerColumnToWindow()
    {
        if (!_viewerOpen)
        {
            return;
        }

        double windowWidth = ActualWidth > 0 ? ActualWidth : Width;

        // The full cover takes the whole window, tree floor and viewer cap
        // both stood down - they are rules about a SPLIT, and there isn't one
        // here. _viewerTreeShare is left untouched, so leaving the mode puts
        // the split back exactly as it was.
        if (_viewerFullscreen)
        {
            var covered = _viewerOnLeft ? ViewerColumnLeft : ViewerColumnRight;
            if (Math.Abs(covered.Width.Value - windowWidth) > 0.5)
            {
                covered.Width = new GridLength(windowWidth);
            }
            return;
        }

        double available = Math.Max(0, windowWidth - MinTreeSplitWidth);

        // The tree column is the STAR one, so left alone it absorbs every
        // pixel the window gains or loses - which is what a maximize looked
        // like on a 4K screen (reported 2026-08-08): a wall of tree and a
        // viewer still sitting at the width it had in a 1246px window. The
        // same arithmetic moved the tree on an ordinary border resize, which
        // the standing rule says only the middle divider may do. So the
        // TREE is what gets held here and the panel takes the whole delta,
        // in both directions, clamped so the tree keeps its split floor and
        // the panel its cap. _settings.ViewerWidth is still deliberately
        // never written from this method: a maximize, an auto-hide collapse
        // and a work-area squeeze are all transient, and the remembered
        // width has to survive them intact.
        double target = _viewerTreeShare is { } treeShare
            ? Math.Clamp(windowWidth - treeShare, 0, Math.Min(MaxViewerWidth, available))
            : Math.Min(ViewerPanelWidth, available);

        var column = _viewerOnLeft ? ViewerColumnLeft : ViewerColumnRight;
        if (Math.Abs(column.Width.Value - target) < 0.5)
        {
            return;
        }

        column.Width = new GridLength(target);
    }

    // Dragging the tree/viewer divider re-splits the CURRENT window between
    // the two: only the grid columns move, the window's bounds never do -
    // which is what makes this the one resize in the app with no dock or
    // auto-hide interaction at all. The image refits live through Stretch;
    // the sharp re-decode at the final width happens once, on release.
    private void ViewerSplitThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (!_viewerOpen)
        {
            return;
        }

        // The thumb sits on the panel's tree-side edge, so the sign flips
        // with the panel's side: panel on the right grows by dragging LEFT.
        //
        // Measured from the column the user is actually looking at, not from
        // the stored width: the two part company whenever the window can't
        // hold the remembered panel (a squeeze) or holds more than it (a
        // maximize), and reading the stored one there made the first delta
        // jump the divider to somewhere the cursor wasn't.
        var draggedColumn = _viewerOnLeft ? ViewerColumnLeft : ViewerColumnRight;
        double target = draggedColumn.Width.Value
            + (_viewerOnLeft ? e.HorizontalChange : -e.HorizontalChange);

        // The tree keeps its split floor (see MinTreeSplitWidth - a column
        // floor, not the window one) of the fixed window width. A floating
        // window briefly had a floor of 0 here - divider pushed all the way,
        // viewer covering the whole window - tried at the user's request and
        // rolled back the same hour ("아무리 봐도 어색"): a strip of tree
        // always remains, in both modes. If full-cover comes back, it wants
        // to be a real fullscreen view (round 3), not a 0px column.
        // Max(min, ...) so a window too narrow for both floors can't hand
        // Math.Clamp an inverted range (which throws).
        //
        // ActualWidth, not Width: MAXIMIZED, only ActualWidth is the window
        // the user is dragging inside - clamping against Width capped a
        // fullscreen split at the restore width's remainder, a wall of dead
        // space to the divider's right (reported 2026-08-08, screenshot).
        double windowWidth = ActualWidth > 0 ? ActualWidth : Width;
        double maxAllowed = Math.Max(MinViewerWidth,
            Math.Min(MaxViewerWidth, windowWidth - MinTreeSplitWidth));
        double panelWidth = Math.Clamp(target, MinViewerWidth, maxAllowed);

        // This is the one gesture allowed to move the tree, so it is the one
        // that re-anchors the share every window resize afterwards holds to.
        _viewerTreeShare = Math.Max(MinTreeSplitWidth, windowWidth - panelWidth);

        // Maximized, the width under the cursor belongs to a window that stops
        // existing at the next restore, so it must not become the remembered
        // one - storing it was how a single nudge of the divider erased the
        // width the restored window goes back to. The tree share above already
        // carries the split through the restore; the stored panel width is
        // left for the next open/restart to rebuild the window from.
        if (WindowState != WindowState.Maximized)
        {
            _settings.ViewerWidth = panelWidth;
        }

        if (_viewerOnLeft)
        {
            ViewerColumnLeft.Width = new GridLength(panelWidth);
        }
        else
        {
            ViewerColumnRight.Width = new GridLength(panelWidth);
        }
    }

    private void ViewerSplitThumb_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (!_viewerOpen)
        {
            return;
        }

        // The middle divider is the ONE gesture allowed to move the stored
        // tree width (the standing rule) - so it is also the one that must
        // record it, or the next reveal/restart rebuilds the window from a
        // stale tree share plus the new panel width and the total jumps.
        // Docked only: ExpandedWidth is the DOCKED tree width, and a
        // floating session pushed to full-cover (tree 0) must not leak into
        // it - same containment SaveCurrentWidth applies.
        if (_isDocked)
        {
            _settings.ExpandedWidth = Math.Clamp(
                Width - CurrentViewerPanelWidth, MinTreeSplitWidth, MaxExpandedWidth);
        }
        _settingsService.Save(_settings);

        // A panel widened past its decode width would keep showing the
        // narrow decode upscaled soft - reload once at the settled width.
        // Not while a GIF is playing: it decodes at its own natural size, so
        // the settled width changes nothing, and the reload would restart
        // the animation from frame 0.
        if (_viewerGifDecoder is null)
        {
            _pendingViewerPath = null;
            UpdateViewerPreview();
        }
    }

    // The OUTER edge drag resizes the viewer too now (tree-hold policy in
    // SetExpandedWidthAnchored), so its release needs the same settle work
    // the split thumb's does. Harmless with the viewer closed.
    private void ResizeThumb_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (!_viewerOpen)
        {
            return;
        }

        _settingsService.Save(_settings);
        // Same GIF exemption as the split thumb's settle above.
        if (_viewerGifDecoder is null)
        {
            _pendingViewerPath = null;
            UpdateViewerPreview();
        }
    }

    // Non-image selections (and images whose decode failed). Two steps, in
    // this order:
    //
    // 1. Ask the SHELL for a real thumbnail, at the size of the slot it is
    //    going into. Whatever thumbnail providers this machine has installed
    //    answer here - PSD, AI, PDF's first page, a video's frame, and SVG if
    //    something like PowerToys registered a handler for it. Windows has no
    //    WIC codec for any of those, so this is the only way the panel ever
    //    shows them, and it costs no dependency. Before this the panel showed
    //    a 96px file-type icon marooned in a 900px panel ("포토샵도 아쭈
    //    쬐깐하게 보여서", user 2026-08-09).
    // 2. Only if that fails, the file-type icon at icon size - a .txt has no
    //    thumbnail and never will, and a blown-up icon is worse than a small
    //    one.
    //
    // What comes back is a raster, capped by the shell around 1024px, so this
    // is not the vector answer for SVG - it is the cheap one. A real SVG
    // renderer stays a separate question (a new dependency, see TODO).
    private void ShowViewerIcon(string path)
    {
        // A folder skips the thumbnail ask outright: the shell answers
        // THUMBNAILONLY for a directory with its big folder ICON, and that
        // answer then rides the zoomable-picture branch below - where 맞춤
        // stretches a ~256px icon across the whole panel ("갑자기 아이콘이
        // 굉장히 커졌어요", 2026-08-09). The icon-at-icon-size fallback is
        // the honest rendering for a folder.
        if (Directory.Exists(path))
        {
            ShowViewerFileTypeIcon(path);
            return;
        }

        int slotSize = (int)Math.Ceiling(
            Math.Clamp(ViewerImageHost.ActualWidth, 256, 2048) *
            VisualTreeHelper.GetDpi(this).DpiScaleX);

        ShellThumbnailService.GetThumbnail(path, slotSize, (thumbnail, pixelWidth, pixelHeight) =>
        {
            if (!_viewerOpen || !string.Equals(_pendingViewerPath, path, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (thumbnail is BitmapSource frame)
            {
                ViewerIconImage.Visibility = Visibility.Collapsed;
                ViewerIconImage.Source = null;
                ViewerImage.Visibility = Visibility.Visible;
                ViewerImage.Source = frame;
                _viewerShowingDecodedImage = false;

                // The thumbnail's own pixels are all there is, so they are what
                // the zoom measures against - and marking them as the decoded
                // width too stops RequestViewerFullResolution from trying to
                // re-read a file WIC cannot open in the first place.
                _viewerPixelWidth = frame.PixelWidth;
                _viewerPixelHeight = frame.PixelHeight;
                _viewerDecodedWidth = frame.PixelWidth;
                if (!string.Equals(_viewerZoomPath, path, StringComparison.OrdinalIgnoreCase))
                {
                    _viewerZoomPath = path;
                    _viewerZoom = ViewerRestZoom;
                    ViewerZoomPan.X = 0;
                    ViewerZoomPan.Y = 0;
                }
                ApplyViewerZoom();

                // The ORIGINAL dimensions when the header could be read (see
                // the service); for a PSD or an SVG it can't, and printing the
                // thumbnail's size there would be a plain lie.
                SetViewerFileInfo(path, pixelWidth, pixelHeight);
                return;
            }

            ShowViewerFileTypeIcon(path);
        });
    }

    // The last resort (and a folder's first): the file-type icon at icon
    // size, centered - a .txt has no thumbnail and never will, and a
    // blown-up icon is worse than a small one. Served via an HICON from the
    // system image list (see GetViewerIcon), not GetImage - GetImage's
    // freshly-rendered icon answers sometimes arrive upside down, which is
    // what the intermittent "물구나무서기" folders were (2026-08-09).
    private void ShowViewerFileTypeIcon(string path)
    {
        ShellIconService.GetViewerIcon(path, icon =>
        {
            if (!_viewerOpen || !string.Equals(_pendingViewerPath, path, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            ViewerImage.Visibility = Visibility.Collapsed;
            ViewerImage.Source = null;
            _viewerShowingDecodedImage = false;
            ViewerIconImage.Visibility = Visibility.Visible;
            ViewerIconImage.Source = icon;
            // A file-type icon has nothing to zoom, so the strip goes too.
            ClearViewerZoom();
            SetViewerFileInfo(path, 0, 0);
        });
    }

    // Size · date, with the pixel dimensions in front when they are actually
    // known. Files get size · date; a folder just its date.
    private void SetViewerFileInfo(string path, int pixelWidth, int pixelHeight)
    {
        try
        {
            var fileInfo = new FileInfo(path);
            string date = $"{File.GetLastWriteTime(path):yyyy-MM-dd HH:mm}";
            string body = fileInfo.Exists
                ? $"{FormatFileSize(fileInfo.Length)}  ·  {date}"
                : date;
            ViewerFileInfo.Text = pixelWidth > 0 && pixelHeight > 0
                ? $"{pixelWidth} × {pixelHeight}  ·  {body}"
                : body;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            ViewerFileInfo.Text = string.Empty;
        }
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

        // The other text box in the app (the rename box is handled per row, in
        // RenameTextBox_Loaded) - see OvertypeGuard.
        OvertypeGuard.Disable(SearchBox);

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

    // Shares the tree's sort geometries rather than the PNG pairs this used to
    // carry - a path takes the button's brush, so one asset covers both themes
    // and any future palette. That was the last PNG pair left in the app, and
    // the prerequisite the theme work had been waiting on.
    private void UpdateSearchSortIcon()
    {
        if (_searchSortMode == SearchSortMode.FolderGroup)
        {
            // The neutral "sort" glyph, the same one a folder following the
            // app-wide default shows: no direction is being applied.
            SearchSortIcon.Data = FileSystemService.FollowsGlobalSortGeometry;
            SearchSortButton.ToolTip = string.Format(Strings.SortTooltipFormat, Strings.SortModeFolderGroup);
            return;
        }

        SearchSortIcon.Data = FileSystemService.SortOverrideGeometry(IsSearchSortDescending);
        SearchSortButton.ToolTip = FileSystemService.FormatSortTooltip(SearchSortFieldOf(_searchSortMode),
            IsSearchSortDescending);
    }

    private bool IsSearchSortDescending
        => _searchSortMode is SearchSortMode.NameDesc or SearchSortMode.DateDesc;

    private static FileSortField SearchSortFieldOf(SearchSortMode mode)
        => mode is SearchSortMode.DateAsc or SearchSortMode.DateDesc ? FileSortField.Date : FileSortField.Name;

    // Opens the menu instead of stepping to the next mode. Five states behind
    // one button is a list, not a control - the same conclusion the tree's sort
    // icon reached on 2026-07-26, reached here for the same reason.
    private void SearchSortButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: { } menu } button)
        {
            return;
        }

        menu.PlacementTarget = button;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void SearchSortContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        AnyMenu_Opened(sender, e);

        if (sender is not ContextMenu
            {
                Items: [MenuItem group, MenuItem byName, MenuItem byDate, _, MenuItem ascending, MenuItem descending]
            })
        {
            return;
        }

        bool grouping = _searchSortMode == SearchSortMode.FolderGroup;
        group.IsChecked = grouping;
        byName.IsChecked = !grouping && SearchSortFieldOf(_searchSortMode) == FileSortField.Name;
        byDate.IsChecked = !grouping && SearchSortFieldOf(_searchSortMode) == FileSortField.Date;

        // Grouping has no direction of its own, so the two rows stand down
        // rather than showing a state that isn't in effect.
        ascending.IsEnabled = !grouping;
        descending.IsEnabled = !grouping;
        ascending.IsChecked = !grouping && !IsSearchSortDescending;
        descending.IsChecked = !grouping && IsSearchSortDescending;
    }

    private void SearchSortFieldMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string tag })
        {
            return;
        }

        // Keeps whichever direction was already in effect when switching
        // between 이름 and 날짜 - only the grouping mode discards it, having
        // none.
        bool descending = IsSearchSortDescending;
        ApplySearchSortMode(tag switch
        {
            "name" => descending ? SearchSortMode.NameDesc : SearchSortMode.NameAsc,
            "date" => descending ? SearchSortMode.DateDesc : SearchSortMode.DateAsc,
            _ => SearchSortMode.FolderGroup,
        });
    }

    private void SearchSortDirectionMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string tag } || _searchSortMode == SearchSortMode.FolderGroup)
        {
            return;
        }

        bool descending = tag == "desc";
        ApplySearchSortMode(SearchSortFieldOf(_searchSortMode) == FileSortField.Date
            ? (descending ? SearchSortMode.DateDesc : SearchSortMode.DateAsc)
            : (descending ? SearchSortMode.NameDesc : SearchSortMode.NameAsc));
    }

    private void ApplySearchSortMode(SearchSortMode mode)
    {
        _searchSortMode = mode;
        _settings.SearchSortMode = (int)mode;
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

        // The favorites splitter is a transparent 9px grab strip floating over
        // its neighbours on ZIndex 1, and the search view merely covers rows
        // 1-4 underneath it - so while search is up that strip still answered
        // the mouse ON TOP of the results: a resize cursor over one of the top
        // rows, and a click there grabbing a splitter nobody can see instead of
        // opening the result (2026-07-31, reported as "맨 윗줄은 위아래 아이콘으로
        // 바뀌네요"). Visibility is left to the favorites panel's own logic;
        // only the hit-testing is taken away for the duration.
        FavoritesSplitter.IsHitTestVisible = !active;

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
            // Nothing has been observed changing since this listing arrived -
            // whatever happened while the app was closed is what the age says.
            ClearSearchIndexStale();
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

    // A change was SEEN inside the searched folder since this index was built,
    // so the results can be missing (or still listing) a file. The drive
    // watchers already report every add/remove/rename on every root for the
    // tree's sake, so this costs one path comparison and nothing else.
    //
    // What it does NOT claim: that an unmarked index is current. A watcher that
    // died with a network drive (see the open item) reports nothing, and an
    // index loaded from disk carries changes made while the app was closed -
    // which is what the status line's age is there to say. Blue means "known to
    // have changed", not "the only time refreshing is worth it".
    private bool _searchIndexStale;

    private void NoteSearchScopeChanged(string changedFolderPath)
    {
        if (_searchIndexStale || _searchScanning || _searchEntries.Count == 0 ||
            _searchScopeFolder is not { Length: > 0 } scope)
        {
            return;
        }

        string scopeTrimmed = scope.TrimEnd(Path.DirectorySeparatorChar);
        string changed = changedFolderPath.TrimEnd(Path.DirectorySeparatorChar);
        bool inScope =
            string.Equals(changed, scopeTrimmed, StringComparison.OrdinalIgnoreCase) ||
            changed.StartsWith(scopeTrimmed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        if (!inScope)
        {
            return;
        }

        _searchIndexStale = true;
        UpdateSearchRefreshIndicator();
    }

    private void ClearSearchIndexStale()
    {
        if (!_searchIndexStale)
        {
            return;
        }
        _searchIndexStale = false;
        UpdateSearchRefreshIndicator();
    }

    // The glyph keeps the row's own colour either way; only the dot appears.
    // A whole glyph turning blue reads as a state the button is IN, while a dot
    // reads as a notice attached to it - which is what this is.
    private void UpdateSearchRefreshIndicator()
    {
        SearchRefreshStaleDot.Visibility = _searchIndexStale ? Visibility.Visible : Visibility.Collapsed;
        SearchRefreshButton.ToolTip = _searchIndexStale
            ? Strings.SearchTooltipRefreshStale
            : Strings.SearchTooltipRefresh;
    }

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
        // Cleared as the scan starts rather than when it ends: a change that
        // lands mid-scan may well be one the walk has already passed, and
        // re-marking is the honest answer to that.
        ClearSearchIndexStale();
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
            // Nothing found is exactly when the index's age matters, and it was
            // the one moment the age wasn't on screen: it shows while the box is
            // empty and is replaced by the count as soon as anything is typed.
            // Someone who points the search at a folder whose saved index
            // predates the files they are looking for then reads "결과 없음" as
            // "not in this folder" (2026-07-31 report - a screenshots folder,
            // where new files arrive constantly). Naming the refresh here is the
            // "and says so" half of loading a saved index in the first place.
            SearchStatusText.Text = _searchIndexSavedAtUtc is { } noResultAge
                ? string.Format(Strings.SearchStatusNoResultsCached, FormatSearchIndexAge(noResultAge))
                : Strings.SearchStatusNoResults;
        }
        else if (total > shownFiles)
        {
            SearchStatusText.Text = string.Format(Strings.SearchStatusResultsCapped, shownFiles, total);
        }
        else
        {
            SearchStatusText.Text = string.Format(Strings.SearchStatusResults, total);
        }

        // Results that came out of a saved index carry its age; results from a
        // scan this session carry nothing, because there is nothing to say.
        if (total > 0 && _searchIndexSavedAtUtc is { } age)
        {
            SearchStatusText.Text += string.Format(Strings.SearchStatusIndexAgeSuffix, FormatSearchIndexAge(age));
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
        var container = ItemsControl.ContainerFromElement(SearchResultsList, (DependencyObject)e.OriginalSource) as ListBoxItem;
        LogClick("press", container?.Content as SearchRow);
        if (container is { Content: SearchRow { Entry: { } entry } })
        {
            _searchDragStart = e.GetPosition(SearchResultsList);
            _searchDragCandidate = entry;
        }
    }

    // One timeline for every click-shaped thing the app can see, written at
    // three depths so a click that "didn't take" can be placed:
    //   win press/up  - the raw window message (SingleInstanceWndProc)
    //   press/up      - WPF routed it to a results row
    //   menu          - a context menu opened or closed
    // A click with a window line but no row line was lost inside the app; a
    // click with no line at all never reached this window. That distinction is
    // the whole question behind the open 간헐적 입력 씹힘 item, which has never
    // had a repro until the alternating one found on 2026-07-28.
    // The tree's own row line, so the same timeline covers both views - the
    // swallowed clicks have been reported in ordinary tree use too.
    //
    // Arrival alone turned out not to be enough: on 2026-07-30 every press of a
    // reportedly-swallowed expand/collapse WAS in the log, so the loss is in
    // what the app did with it, not in delivery. Hence the extra fields - the
    // gap to the previous press and the ClickCount say whether Windows folded
    // it into a double-click, and the pre-toggle state plus the expanded/
    // collapsed line below say whether the toggle actually took.
    private long _lastTreePressTicks;

    [System.Diagnostics.Conditional("DEBUG")]
    private void LogTreeClick(FileSystemItem? item, bool onExpander, int clickCount)
    {
        long now = Environment.TickCount64;
        long gap = _lastTreePressTicks == 0 ? -1 : now - _lastTreePressTicks;
        _lastTreePressTicks = now;

        // Carried over to the scroll-jump watch (scrolljump.log), which needs to
        // say WHAT was clicked just before a jump - the reported case was a
        // plain expand on a folder nowhere near the top.
        _lastTreePressLabel =
            $"{item?.Name ?? "(none)"}/{(item?.IsExpanded == true ? "collapse" : "expand")}";

        LogClickLine(
            $"tree press: gap={(gap < 0 ? "-" : gap + "ms")} click={clickCount} " +
            $"expander={(onExpander ? "yes" : "no")} " +
            $"target={item?.Name ?? "(none)"} dir={(item?.IsDirectory == true ? "yes" : "no")} " +
            $"wasExpanded={(item?.IsExpanded == true ? "yes" : "no")} " +
            $"menu={(IsCapturingUiOpen ? "open" : "-")} " +
            $"captured={(Mouse.Captured?.GetType().Name ?? "-")}");
    }

    // The outcome half: did a toggle actually happen, and how long after the
    // press that asked for it.
    [System.Diagnostics.Conditional("DEBUG")]
    private void LogTreeToggle(FileSystemItem item, bool expanded)
    {
        long sincePress = _lastTreePressTicks == 0 ? -1 : Environment.TickCount64 - _lastTreePressTicks;
        LogClickLine(
            $"tree {(expanded ? "expanded" : "collapsed")}: {item.Name} " +
            $"(+{(sincePress < 0 ? "-" : sincePress + "ms")} after press)");
    }

    [System.Diagnostics.Conditional("DEBUG")]
    private void LogClickLine(string line)
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Edgetree");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, "click.log"),
                $"{DateTime.Now:HH:mm:ss.fff}  {line}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    [System.Diagnostics.Conditional("DEBUG")]
    private void LogClick(string stage, SearchRow? row)
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Edgetree");
            Directory.CreateDirectory(dir);
            string target = row is null
                ? string.Empty
                : row.IsHeader ? $" target=header {row.DirectoryPath}"
                : row.IsShowMore ? " target=showmore"
                : $" target={row.FileName}";
            File.AppendAllText(
                Path.Combine(dir, "click.log"),
                $"{DateTime.Now:HH:mm:ss.fff}  {stage}: menu={(IsCapturingUiOpen ? "open" : "-")} " +
                $"captured={(Mouse.Captured?.GetType().Name ?? "-")}{target}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
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
            LogClick("up (no row)", null);
            return;
        }

        LogClick("up", row);

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
            case Key.X when Keyboard.Modifiers == ModifierKeys.Control:
                SearchCut_Click(sender, e);
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
    // already handles files past a folder's "더 보기" cap). The found file goes
    // to the top of the viewport itself, like every other jump.
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
            NavigateToPath(entry.FullPath, source: "search-result");
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

        // By Tag, not by position - this menu gained 잘라내기 on 2026-07-30 and
        // the index this used to count to ("Code로 열기 sits at 10") moved with
        // it, which is the same way the tree menu's whole block went dead.
        if (sender is ContextMenu menu)
        {
            var thumbnailItem = FindTaggedMenuElement<MenuItem>(menu, "thumbnail");
            var thumbnailSeparator = FindTaggedMenuElement<Separator>(menu, "thumbnailSep");
            var openWithCodeItem = FindTaggedMenuElement<MenuItem>(menu, "openWithCode");
            if (thumbnailItem is null || thumbnailSeparator is null || openWithCodeItem is null)
            {
                LogClickLine("search menu: a tagged item is missing - menu half-configured");
                return;
            }

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
            ClearCutMarks();
            FileOperationService.CopyToClipboard(entry.FullPath);
        }
    }

    // A search result can be cut from here even though there's nothing to
    // paste INTO in this view - the paste happens back in the tree, which is
    // exactly the trip this saves (find it by name, then move it).
    private void SearchCut_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSearchResult is { } entry && FileOperationService.CutToClipboard(entry.FullPath))
        {
            MarkCutPaths(new[] { entry.FullPath });
        }
    }

    private void SearchCopyPath_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSearchResult is { } entry)
        {
            ClearCutMarks();
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


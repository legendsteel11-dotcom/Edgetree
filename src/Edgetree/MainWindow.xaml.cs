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
    private const double CollapsedWidth = 44;
    private const double MinExpandedWidth = 180;
    private const double MaxExpandedWidth = 1200;
    private const int ToggleAnimationMs = 200;
    private static readonly double[] TreeFontSizeSteps = { 9, 10, 11, 12, 13, 14, 15, 16 };
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
    private readonly Dictionary<string, System.Windows.Threading.DispatcherTimer> _pendingExternalRefreshes = new();

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
        SystemParameters.StaticPropertyChanged += SystemParameters_StaticPropertyChanged;
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

        Width = _settings.IsAutoHidden ? AutoHideSliverWidth
            : _settings.IsCollapsed ? CollapsedWidth
            : ClampExpandedWidth(_settings.ExpandedWidth);
        SetExpandedContentVisibility(_settings.IsCollapsed ? Visibility.Collapsed : Visibility.Visible);
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

        Topmost = _settings.AlwaysOnTop || _settings.IsAutoHidden;
        if (Application.Current is App app)
        {
            app.IsTrayIconVisible = _settings.AlwaysShowTrayIcon;
        }

        ApplyColorSettings();
        ApplyFolderIconVisibility();
        ApplyFileIconVisibility();

        // Deferred rather than done inline above: restoring possibly many
        // expanded folders plus the last selection means synchronous disk
        // I/O per folder (EnsureChildrenLoaded), which - done inline here -
        // blocked this handler from returning, and with it the window from
        // ever painting its first frame, until all of it finished. Queuing
        // it at Background priority instead lets the window actually show
        // up (drive roots visible, just not yet re-expanded) before that
        // work runs, rather than reading as a startup freeze/flicker.
        Dispatcher.BeginInvoke(RestoreTreeState, System.Windows.Threading.DispatcherPriority.Background);
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
        SetBrushColor("SidebarBackground", _settings.BackgroundColorHex);
        SetBrushColor("FolderNameForeground", _settings.FolderNameColorHex);
        SetBrushColor("FolderNameHighlightForeground", _settings.FolderNameHighlightColorHex);
        SetBrushColor("FileNameForeground", _settings.FileNameColorHex);
        SetBrushColor("FileNameHighlightForeground", _settings.FileNameHighlightColorHex);
        SetBrushColor("TreeRowSelectedActiveBackground", _settings.SelectionColorHex);
        SetBrushColor("FavoritesBackground", _settings.HistoryBackgroundColorHex);
        SetBrushColor("TreeRowHoverBackground", _settings.HoverBackgroundColorHex);
        SetBrushColor("FolderNameHoverForeground", _settings.FolderNameHoverColorHex);
        SetBrushColor("FileNameHoverForeground", _settings.FileNameHoverColorHex);
        SetBrushColor("ShowMoreForeground", _settings.ShowMoreColorHex);
        SetBrushColor("TreeGuideLineBrush", _settings.GuideLineColorHex);
        SetBrushColor("TreeGuideLineActiveBrush", _settings.GuideLineActiveColorHex);
        SetBrushColor("PanelDividerBrush", _settings.PanelDividerColorHex);
        SetBrushColor("HeaderBackground", _settings.HeaderBackgroundColorHex);
    }

    private void SetBrushColor(string resourceKey, string hex)
    {
        if (ColorConverter.ConvertFromString(hex) is Color color)
        {
            Resources[resourceKey] = new SolidColorBrush(color);
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

        foreach (var watcher in _driveWatchers)
        {
            watcher.Dispose();
        }
    }

    private void SystemParameters_StaticPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Only re-snap to the work area while docked - a floating window must not
        // get yanked back to the left edge just because the taskbar moved/resized.
        if (e.PropertyName == nameof(SystemParameters.WorkArea) && _isDocked)
        {
            Dispatcher.Invoke(PositionToWorkArea);
        }
    }

    private void PositionToWorkArea()
    {
        var workArea = GetCurrentMonitorWorkArea();
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
    private Rect GetCurrentMonitorWorkArea()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var screen = hwnd != IntPtr.Zero
            ? System.Windows.Forms.Screen.FromHandle(hwnd)
            : System.Windows.Forms.Screen.PrimaryScreen;
        if (screen is null)
        {
            return SystemParameters.WorkArea;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var working = screen.WorkingArea;
        return new Rect(
            working.Left / dpi.DpiScaleX,
            working.Top / dpi.DpiScaleY,
            working.Width / dpi.DpiScaleX,
            working.Height / dpi.DpiScaleY);
    }

    private static double ClampExpandedWidth(double width)
        => Math.Clamp(width, MinExpandedWidth, MaxExpandedWidth);

    // The app icon is the collapse/expand toggle now (replacing the old
    // separate chevron button entirely) - docked, a click steps through
    // expanded -> icon rail -> auto-hide sliver. Ignored entirely once
    // auto-hidden (see EnterAutoHide's own comment - the pin button, not
    // this, is the only way back out from there). Floating has no rail to
    // collapse to, so this is a no-op there; AppIcon is just branding while
    // floating.
    private void AppIcon_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isDocked || _settings.IsAutoHidden)
        {
            return;
        }

        if (_settings.IsCollapsed)
        {
            EnterAutoHide();
        }
        else
        {
            ToggleCollapsed();
        }
        e.Handled = true;
    }

    private void ToggleCollapsed()
    {
        bool collapsing = !_settings.IsCollapsed;
        double targetWidth = collapsing ? CollapsedWidth : ClampExpandedWidth(_settings.ExpandedWidth);

        // Hide labels immediately when collapsing so they don't visibly
        // squeeze/wrap during the shrink animation.
        if (collapsing)
        {
            SetExpandedContentVisibility(Visibility.Collapsed);
        }
        _settings.IsCollapsed = collapsing;

        AnimateWidth(targetWidth, onCompleted: () =>
        {
            // Only reveal labels once the window has fully expanded.
            if (!collapsing)
            {
                SetExpandedContentVisibility(Visibility.Visible);
            }
        });
    }

    // Animates Width toward targetWidth - and, docked to the right edge,
    // Left in lockstep (identical duration/easing, so they visibly move as
    // one) so the right edge stays anchored to the screen edge instead of
    // the whole window drifting as it grows/shrinks (see PositionToWorkArea/
    // ResizeThumb_DragDelta, which anchor that same edge their own way).
    // Shared by ToggleCollapsed and the auto-hide reveal/re-hide transitions.
    private void AnimateWidth(double targetWidth, Action? onCompleted = null)
    {
        double targetLeft = Left + (Width - targetWidth);
        bool anchorRightEdge = _isDocked && _settings.DockOnRight;

        var widthAnimation = new DoubleAnimation
        {
            To = targetWidth,
            Duration = TimeSpan.FromMilliseconds(ToggleAnimationMs),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        };
        widthAnimation.Completed += (_, _) =>
        {
            // Release the animation clocks' hold so later direct assignments
            // (e.g. from the resize thumb) take effect again.
            BeginAnimation(WidthProperty, null);
            Width = targetWidth;
            if (anchorRightEdge)
            {
                BeginAnimation(LeftProperty, null);
                Left = targetLeft;
            }
            onCompleted?.Invoke();
        };
        BeginAnimation(WidthProperty, widthAnimation);

        if (anchorRightEdge)
        {
            var leftAnimation = new DoubleAnimation
            {
                To = targetLeft,
                Duration = TimeSpan.FromMilliseconds(ToggleAnimationMs),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            BeginAnimation(LeftProperty, leftAnimation);
        }
    }

    // Clamped defensively at the point of use (like MaxItemsPerFolder/
    // TabSpacing elsewhere) rather than trusting a hand-edited settings file.
    private double AutoHideSliverWidth => Math.Clamp(_settings.AutoHideSliverWidth, 3, 8);

    // Entered by clicking the app icon a second time while already collapsed
    // to the 44px icon rail - shrinks further to a bare AutoHideSliverWidth
    // sliver at the screen edge, which MainWindow_MouseEnter/Leave then peek
    // open/closed as the mouse crosses it, the same convention as Windows'
    // own taskbar auto-hide. Forces Topmost on for as long as auto-hide stays
    // engaged (both the sliver and the temporarily-peeked-open states) -
    // otherwise a maximized window would cover the sliver and the mouse could
    // never reach it to reveal it again, regardless of the user's own
    // "항상 위에 표시" preference. ExitAutoHide restores that preference.
    private void EnterAutoHide()
    {
        _settings.IsAutoHidden = true;
        Topmost = true;
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
        _settings.IsCollapsed = false;
        _isAutoHideRevealed = false;
        Topmost = _settings.AlwaysOnTop;
        UpdatePinButtonVisibility();

        // The reveal (MainWindow_MouseEnter) that got us here ran while
        // _settings.IsCollapsed was still true, so it left the resize thumb
        // hidden/non-hit-testable (see UpdateResizeThumbVisibility) - now
        // that IsCollapsed is actually false, it needs to be told to show
        // again, or dragging to resize silently does nothing until some
        // other, unrelated call happens to refresh it (e.g. docking to the
        // right, or an undock/re-dock round trip).
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
        SetExpandedContentVisibility(Visibility.Visible);
        AnimateWidth(ClampExpandedWidth(_settings.ExpandedWidth));
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
        RootPathText.Visibility = visibility;
        ExplorerTree.Visibility = visibility;
        CollapseAllButton.Visibility = visibility;
        OptionsButton.Visibility = visibility;
        MinimizeButton.Visibility = visibility;
        CloseButton.Visibility = visibility;
        FavoritesList.Visibility = visibility;
        FavoritesSplitter.Visibility = visibility;
        VersionFooterBorder.Visibility = visibility;

        // RowDefinition heights don't auto-shrink just because their content is
        // hidden, so without this the favorites row/splitter (and the version
        // footer row) would keep reserving their pixel height as a blank gap
        // in the 44px-wide icon-only rail. Using the visibility parameter
        // directly (rather than _settings.IsCollapsed) avoids a timing issue:
        // on the collapsing path this runs before that flag is actually
        // flipped (see ToggleCollapsed).
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
    // the only way to change width) and expanded (nothing to grab once collapsed
    // to the icon bar) - floating windows get native edge-resize instead.
    // Normal expanded-and-docked, or docked and temporarily peeked out of
    // auto-hide (see MainWindow_MouseEnter) - the icon rail and the hidden
    // auto-hide sliver are both too narrow to make sense of a drag-resize,
    // but a peek shows the same full-width content a normal expanded window
    // does, so it should be resizable the same way while it's up.
    private bool CanResizeWidth
        => _isDocked && (!_settings.IsCollapsed || (_settings.IsAutoHidden && _isAutoHideRevealed));

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
        bool showForFloatingRedock = !_isDocked && !_settings.IsCollapsed;
        bool showForAutoHideReveal = _isDocked && _settings.IsAutoHidden && _isAutoHideRevealed;
        PinButton.Visibility = showForFloatingRedock || showForAutoHideReveal ? Visibility.Visible : Visibility.Collapsed;

        // Which edge re-docking/pinning actually snaps to depends on
        // DockOnRight, so the tooltip can't be one fixed string the way the
        // header's other buttons' tooltips are.
        PinButton.ToolTip = _settings.DockOnRight ? Strings.ToolTipPinRight : Strings.ToolTipPinLeft;
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

        // Collapsing to the icon-only rail (and further, auto-hiding to a
        // sliver) only makes sense docked (both are space-saving tricks for a
        // fixed-height edge strip); a freshly undocked window has no reason
        // to be stuck at either, so expand it.
        if (_settings.IsCollapsed)
        {
            _settings.IsCollapsed = false;
            if (_settings.IsAutoHidden)
            {
                _settings.IsAutoHidden = false;
                _isAutoHideRevealed = false;
                _autoHideRehideTimer?.Stop();
                Topmost = _settings.AlwaysOnTop;
            }
            SetExpandedContentVisibility(Visibility.Visible);
        }
        Width = _floatingWidth ?? ClampExpandedWidth(_settings.ExpandedWidth);

        ResizeMode = ResizeMode.CanResize;
        ChromeSettings.CaptionHeight = HeaderHeight;
        ChromeSettings.ResizeBorderThickness = new Thickness(FloatingResizeBorder);

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

        var hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.MakeToolWindow(hwnd);
        NativeMethods.SetWindowCornerPreference(hwnd, rounded: false);
        ShowInTaskbar = false;

        Width = _settings.IsCollapsed ? CollapsedWidth : ClampExpandedWidth(_settings.ExpandedWidth);
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
    private double FavoriteRowHeight => 24 * (ExplorerTree.FontSize / DefaultTreeFontSize);

    // FavoritesList's own top+bottom Padding (see the XAML - top trimmed from
    // 8 to 6 to match the tree's top gap, per direct pixel measurement).
    private const double FavoritesListChrome = 10;

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

        // The one hairline border FavoritesList draws (see its
        // BorderThickness in the XAML) faces whichever edge is adjacent to
        // the splitter/tree - bottom edge when favorites is on top, top edge
        // when it's on bottom - so it still reads as separating the two
        // panels instead of framing the wrong side.
        FavoritesList.BorderThickness = _settings.FavoritesAtBottom
            ? new Thickness(0, 1, 0, 0)
            : new Thickness(0, 0, 0, 1);

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
        => _settings.Favorites.Count * FavoriteRowHeight + FavoritesListChrome + FavoritesFitBottomPadding;

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
    private void FavoritesSplitter_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        => FitFavoritesPanel();

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
            // Re-clicking a favorite that's already revealed and selected used
            // to still re-run the entire walk: re-collapse every other
            // folder's "more" overflow, re-expand the whole chain level by
            // level, and re-pin the selection to the top of the tree - all for
            // an end state identical to what was already on screen, which read
            // as the whole panel flashing/redrawing. Nothing left to do if the
            // target is already the current selection.
            string? currentPath = (ExplorerTree.SelectedItem as FileSystemItem)?.FullPath.TrimEnd('\\');
            if (currentPath is not null &&
                string.Equals(currentPath, entry.Path.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
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
    // were added on top of.
    private void NavigateToPath(string targetPath)
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

        RevealChain(chain, myToken);
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
    private void RevealChain(List<FileSystemItem> chain, int token)
    {
        // Overflow re-capping already ran up-front in NavigateToPath, so the
        // walk starts over a light tree; this only expands the path down to
        // the target and doesn't touch any other folder's expanded state.
        RevealChainStep(chain, 0, ExplorerTree, token);
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
    private void RevealChainStep(List<FileSystemItem> chain, int index, ItemsControl container, int token, int attempt = 0)
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
            // A cheap safety net for a genuine one-frame layout lag, and
            // nothing more. It is NOT what makes a container appear: a
            // container outside the virtualizing panel's viewport+cache is
            // never going to materialize just because we looked again, no
            // matter how many times. Several rounds of raising this ceiling
            // (10 -> 40) chasing exactly that failure achieved nothing, which
            // in hindsight was the clue that the miss was structural. The
            // actual cause was drive roots falling outside the ROOT panel's
            // cache - fixed by CacheLength on ExplorerTree in the XAML, not
            // here.
            if (attempt >= 5)
            {
                // Given it a real chance - genuinely gone (renamed/deleted), not just slow.
                EndFavoriteNavigation(token);
                return;
            }
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
                new Action(() => RevealChainStep(chain, index, container, token, attempt + 1)));
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
            FinishReveal(treeViewItem, token);
        }
        else
        {
            RevealChainStep(chain, index + 1, treeViewItem, token);
        }
    }

    private void FinishReveal(TreeViewItem treeViewItem, int token)
    {
        // Still guarded while this fires: setting IsSelected raises
        // SelectedItemChanged synchronously, and the guard keeps that from
        // re-syncing (and possibly clearing) the favorites list. Cleared right
        // after, so subsequent user-driven selections sync normally again.
        treeViewItem.IsSelected = true;
        treeViewItem.BringIntoView();
        treeViewItem.Focus();
        EndFavoriteNavigation(token);

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
                treeViewItem.BringIntoView(new Rect(0, 0, treeViewItem.ActualWidth, scrollViewer.ActualHeight));
            }
        }));
    }

    private ScrollViewer? FindTreeScrollViewer()
    {
        ExplorerTree.ApplyTemplate();
        return ExplorerTree.Template.FindName("PART_TreeScrollViewer", ExplorerTree) as ScrollViewer;
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
        // Same reasoning as ExplorerItemContextMenu_Opened: MenuItems declared
        // in a resource dictionary don't get auto-generated code-behind fields.
        // The two discards after the toggle group skip the separator and the
        // "색상 변경" item, neither of which has state to sync here.
        if (sender is ContextMenu
            {
                Items: [MenuItem autoCollapse, MenuItem alwaysOnTop, MenuItem startWithWindows, MenuItem trayIcon, MenuItem showFolderIcons, MenuItem showFileIcons, MenuItem favoritesAtBottom, MenuItem dockOnRight, MenuItem autoHideCloseOnLeave, MenuItem autoHideSliverWidthRow, _, _, MenuItem sortMenu, MenuItem maxItemsRow, MenuItem tabSpacingRow, MenuItem languageMenu, ..]
            })
        {
            autoCollapse.IsChecked = _settings.AutoCollapseFolders;
            alwaysOnTop.IsChecked = _settings.AlwaysOnTop;
            startWithWindows.IsChecked = _settings.StartWithWindows;
            trayIcon.IsChecked = _settings.AlwaysShowTrayIcon;
            showFolderIcons.IsChecked = _settings.ShowFolderIcons;
            showFileIcons.IsChecked = _settings.ShowFileIcons;
            favoritesAtBottom.IsChecked = _settings.FavoritesAtBottom;
            dockOnRight.IsChecked = _settings.DockOnRight;
            autoHideCloseOnLeave.IsChecked = _settings.AutoHideCloseOnMouseLeave;

            if (autoHideSliverWidthRow.Header is StackPanel { Children: [_, _, TextBlock sliverWidthValueText, _] })
            {
                sliverWidthValueText.Text = _settings.AutoHideSliverWidth.ToString();
            }

            if (sortMenu.Items is [MenuItem byName, MenuItem byDate, _, MenuItem ascending, MenuItem descending])
            {
                byName.IsChecked = !_settings.SortByDate;
                byDate.IsChecked = _settings.SortByDate;
                ascending.IsChecked = !_settings.SortDescending;
                descending.IsChecked = _settings.SortDescending;
            }

            if (maxItemsRow.Header is StackPanel { Children: [_, _, TextBlock maxItemsValueText, _] })
            {
                maxItemsValueText.Text = _settings.MaxItemsPerFolder.ToString();
            }

            if (tabSpacingRow.Header is StackPanel { Children: [_, _, TextBlock tabSpacingValueText, _] })
            {
                tabSpacingValueText.Text = _settings.TabSpacing.ToString();
            }

            // languageMenu's first child is the non-interactive restart note
            // (see the XAML) - skipped here via the leading discard.
            if (languageMenu.Items is [_, MenuItem koItem, MenuItem enItem])
            {
                koItem.IsChecked = _settings.Language != "en";
                enItem.IsChecked = _settings.Language == "en";
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

        // sender is whichever stepper Button was clicked; its logical parent
        // is the row's StackPanel (label, -, value, +) - the value TextBlock
        // sits at the same position regardless of which button fired.
        if (sender is Button { Parent: StackPanel { Children: [_, _, TextBlock valueText, _] } })
        {
            valueText.Text = value.ToString();
        }
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

        if (sender is Button { Parent: StackPanel { Children: [_, _, TextBlock valueText, _] } })
        {
            valueText.Text = value.ToString();
        }
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

    // Per-folder right-click menu's own "정렬": sort order/direction is still
    // one global setting (there's no per-folder override to store), so this
    // changes the exact same fields as the options-menu handlers above - but
    // reaching for "정렬" on one specific folder reads as "sort THIS folder
    // now", not "change the app-wide default and re-sort everything I've got
    // open". Refreshing only the folder that was actually right-clicked keeps
    // that scoped as expected; every other already-loaded folder just picks
    // up the new global setting whenever it's next refreshed or re-expanded.
    private void FolderSortFieldMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string field })
        {
            return;
        }

        _settings.SortByDate = field == "date";
        FileSystemService.SortField = _settings.SortByDate ? FileSortField.Date : FileSortField.Name;
        RefreshFolder_Click(sender, e);
    }

    private void FolderSortDirectionMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string direction })
        {
            return;
        }

        _settings.SortDescending = direction == "desc";
        FileSystemService.SortDescending = _settings.SortDescending;
        RefreshFolder_Click(sender, e);
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
        // Snapshot every currently-expanded, already-loaded folder's full
        // path, and the current selection, before refreshing discards the
        // actual instances that know about either - a folder's
        // RefreshChildren always rebuilds its Children as fresh, collapsed,
        // unloaded instances, even for a folder nobody had expanded.
        var expandedPaths = new List<string>();
        foreach (var root in _roots)
        {
            CollectExpandedPaths(root, expandedPaths);
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

    private static void CollectExpandedPaths(FileSystemItem item, List<string> result)
    {
        if (!item.IsDirectory || !item.ChildrenLoaded)
        {
            return;
        }

        foreach (var child in item.Children)
        {
            if (child.IsPlaceholder || child.IsShowMore || !child.IsDirectory)
            {
                continue;
            }
            if (child.IsExpanded)
            {
                result.Add(child.FullPath);
            }
            CollectExpandedPaths(child, result);
        }
    }

    // Same idea as CollectExpandedPaths, but also captures a drive root's OWN
    // expanded state - which that helper can't, since it only ever looks at
    // an item's children. RefreshAllLoadedFolders never needed that (a root's
    // IsExpanded survives RefreshChildren untouched), but a full app restart
    // rebuilds _roots from scratch, so root-level state needs saving too.
    private List<string> CollectAllExpandedPaths()
    {
        var result = new List<string>();
        foreach (var root in _roots)
        {
            if (root.IsExpanded)
            {
                result.Add(root.FullPath);
            }
            CollectExpandedPaths(root, result);
        }
        return result;
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

    // Portable app (no installer, no per-user machine record) - so moving
    // settings to another PC is a manual file, not an automatic sync. Exports
    // the same AppSettings/JSON shape the app already reads/writes at its
    // normal AppData location (see SettingsService), just to a user-chosen
    // path, favorites included.
    private void ExportSettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            FileName = "SidebarExplorer-settings.json",
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
            // Switching away from click-outside mode mid-reveal should stop
            // watching for one immediately rather than leaving it armed under
            // the now-inapplicable setting.
            if (menuItem.IsChecked)
            {
                StopAutoHideOutsideClickWatch();
            }
        }
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

        if (sender is Button { Parent: StackPanel { Children: [_, _, TextBlock valueText, _] } })
        {
            valueText.Text = value.ToString();
        }
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
    private void ApplyLayoutMetrics()
    {
        double scale = ExplorerTree.FontSize / DefaultTreeFontSize;
        Resources["IconSize"] = 16.0 * scale;

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
    }

    private void ColorSettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var window = new ColorSettingsWindow(_settings, ApplyColorSettings) { Owner = this };
        PositionNearOptionsButton(window);
        window.ShowDialog();
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
    private const string RunValueName = "SidebarExplorer";

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
        ApplyAutoCollapse(item);
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

    private void TreeViewItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TreeViewItem treeViewItem)
        {
            return;
        }

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

        // The built-in expand/collapse arrow already toggles IsExpanded on its own
        // Click; skip our row-click handling for that case to avoid a double toggle.
        bool clickedOnExpander =
            (e.OriginalSource as DependencyObject)?.FindAncestor<ToggleButton>() is { } expander
            && ReferenceEquals(expander.FindAncestor<TreeViewItem>(), treeViewItem);

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
        if (_itemDragCandidate is not { } item || _itemDragStart is not { } start ||
            e.LeftButton != MouseButtonState.Pressed)
        {
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

        // Dragging the file out, not renaming it in place.
        CancelPendingRename();

        // FileDrop is the same format Explorer itself puts on the clipboard/
        // drag operation for real files, so any app that accepts a file
        // dropped from Explorer (mail client, another Explorer window, a
        // "drop to open" target, ...) accepts one dropped from here too.
        var data = new DataObject(DataFormats.FileDrop, new[] { item.FullPath });
        DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link);
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

        if (!FileOperationService.TryImportDroppedPaths(droppedPaths, item.FullPath, ConfirmOverwrite, out var error))
        {
            return;
        }
        if (error is not null)
        {
            MessageBox.Show(this, error, Strings.ImportFailedTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        item.RefreshChildren();
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
            treeViewItem.IsSelected = true;
            treeViewItem.Focus();

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
            }
        }
    }

    private void ExplorerTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
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
        // MenuItems declared inside a resource dictionary don't get an
        // auto-generated code-behind field the way named elements in the main
        // visual tree do, so these have to be found by position on the
        // ContextMenu itself instead.
        if (sender is ContextMenu { Items: [MenuItem addFavoriteItem, MenuItem newFolderItem, MenuItem refreshItem, MenuItem sortMenu, _, _, MenuItem openWithItem, ..] })
        {
            bool isFolder = ExplorerTree.SelectedItem is FileSystemItem { IsPlaceholder: false, IsDirectory: true };
            addFavoriteItem.IsEnabled = isFolder;

            // New folder is created as a child of the selected folder - it
            // doesn't make sense (and there's nowhere to put it) off a file.
            newFolderItem.IsEnabled = isFolder;

            // Refresh re-reads the selected folder's own contents from disk -
            // there's nothing to refresh on a plain file.
            refreshItem.IsEnabled = isFolder;

            // Sort applies globally either way (see SortFieldMenuItem_Click),
            // but only makes sense to reach for while looking at a folder.
            sortMenu.IsEnabled = isFolder;
            if (sortMenu.Items is [MenuItem byName, MenuItem byDate, _, MenuItem ascending, MenuItem descending])
            {
                byName.IsChecked = !_settings.SortByDate;
                byDate.IsChecked = _settings.SortByDate;
                ascending.IsChecked = !_settings.SortDescending;
                descending.IsChecked = _settings.SortDescending;
            }

            // "Open with" only makes sense for files - folders don't have a
            // file-association picker.
            openWithItem.IsEnabled =
                ExplorerTree.SelectedItem is FileSystemItem { IsPlaceholder: false, IsDirectory: false };
        }
    }

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
                    // Deliberately no NotifyFilters.LastWrite/no Changed
                    // subscription below - this tree only ever shows a
                    // folder's list of names, and a file being edited in
                    // place (same name) doesn't change what that list looks
                    // like. Watching LastWrite too would mean every
                    // autosave/log write anywhere on the whole drive resets
                    // some folder's debounce for a change nothing here
                    // actually displays differently.
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
                };
                watcher.Created += OnDriveWatcherEvent;
                watcher.Deleted += OnDriveWatcherEvent;
                watcher.Renamed += OnDriveWatcherEvent;
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

    // Debounced per-folder-path - see _pendingExternalRefreshes' own comment.
    private void QueueExternalRefresh(string folderPath)
    {
        if (_pendingExternalRefreshes.TryGetValue(folderPath, out var existing))
        {
            existing.Stop();
            existing.Start();
            return;
        }

        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _pendingExternalRefreshes.Remove(folderPath);
            if (FindLoadedItemForPath(folderPath) is { IsExpanded: true } item)
            {
                RefreshFolderPreservingState(item);
            }
        };
        _pendingExternalRefreshes[folderPath] = timer;
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

    // Same snapshot -> refresh -> replay approach as RefreshAllLoadedFolders,
    // scoped to just this one folder's own subtree - an external change the
    // user didn't ask for should disturb whatever they had expanded/selected
    // as little as possible, unlike a deliberate RefreshFolder_Click/F5 where
    // losing a grandchild's expanded state is a lot less surprising.
    private void RefreshFolderPreservingState(FileSystemItem item)
    {
        var expandedPaths = new List<string>();
        CollectExpandedPaths(item, expandedPaths);
        string? selectedPath = (ExplorerTree.SelectedItem as FileSystemItem)?.FullPath.TrimEnd('\\');

        item.RefreshChildren();

        foreach (var path in expandedPaths.OrderBy(p => p.Length))
        {
            ExpandPathIfPossible(path);
        }

        // RefreshChildren only replaces THIS folder's own children with fresh
        // instances (see its own comment), so the selected item's object
        // reference - and its on-screen position - is completely untouched
        // unless the selection actually lived inside this subtree. Without
        // this check, an unrelated background change anywhere else on the
        // same watched drive (a browser cache write, antivirus scan, cloud
        // sync, ...) would re-run the favorite-style reveal walk below on
        // every keystroke of background disk activity - including its
        // forced "pin selection to the top of the tree" scroll (see
        // FinishReveal) - which is what made the current selection jump to
        // the top line or the tree flicker while the user wasn't doing
        // anything in the app at all.
        string refreshedPath = item.FullPath.TrimEnd('\\');
        bool selectionInsideRefreshedSubtree = selectedPath is not null &&
            (string.Equals(selectedPath, refreshedPath, StringComparison.OrdinalIgnoreCase) ||
             selectedPath.StartsWith(refreshedPath + "\\", StringComparison.OrdinalIgnoreCase));
        if (selectionInsideRefreshedSubtree)
        {
            NavigateToPath(selectedPath!);
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
        if (ExplorerTree.SelectedItem is FileSystemItem { IsPlaceholder: false } item)
        {
            FileOperationService.CopyToClipboard(item.FullPath);
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
        if (ExplorerTree.SelectedItem is not FileSystemItem { IsPlaceholder: false } item)
        {
            return;
        }

        var result = MessageBox.Show(
            this,
            string.Format(Strings.DeleteConfirmBody, item.Name),
            Strings.DeleteConfirmTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        if (!FileOperationService.TryDeleteToRecycleBin(item.FullPath, out var error))
        {
            if (error is not null)
            {
                MessageBox.Show(this, error, Strings.DeleteFailedTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            return;
        }

        item.Parent?.RefreshChildren();
        RemoveFavoritesUnder(item.FullPath);
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
        if (!CanResizeWidth)
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

    private void SaveCurrentWidth()
    {
        // Only capture Width as the docked width while actually docked - a
        // floating window's (possibly much wider) size shouldn't leak into the
        // docked sidebar's remembered width, since every launch starts docked.
        if (_isDocked && !_settings.IsCollapsed)
        {
            _settings.ExpandedWidth = ClampExpandedWidth(Width);
        }

        _settings.ExpandedFolderPaths = CollectAllExpandedPaths();
        _settings.LastSelectedPath = (ExplorerTree.SelectedItem as FileSystemItem)?.FullPath;

        _settingsService.Save(_settings);
    }
}

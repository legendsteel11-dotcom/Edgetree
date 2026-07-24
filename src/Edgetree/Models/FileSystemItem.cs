using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using SidebarExplorer.App.Services;

namespace SidebarExplorer.App.Models;

public class FileSystemItem : INotifyPropertyChanged
{
    private bool _isExpanded;
    private bool _isSelected;
    private bool _isMultiSelected;
    private bool _isAncestorOfSelection;
    private bool _childrenLoaded;
    private bool _isEditing;
    private string _editingName = string.Empty;

    public string Name { get; }
    public string FullPath { get; }
    public bool IsDirectory { get; }
    public bool IsPlaceholder { get; }
    public FileSystemItem? Parent { get; }
    // Mode-aware (PNG set vs. Windows shell icons - see ShellIconService).
    // RefreshIcon is handed in as the change callback: a per-file shell icon
    // (.exe 등) arriving from the background swaps in by re-raising this
    // property, VS the instant generic icon this getter returned meanwhile.
    public ImageSource? Icon => IsShowMore || IsPlaceholder
        ? null
        : IsDirectory
            ? ShellIconService.GetFolderIcon(Name, IsExpanded)
            : ShellIconService.GetFileIcon(Name, FullPath, RefreshIcon);

    // Also called by MainWindow.ApplyIconStyle when the icon mode toggles, so
    // every realized row re-reads Icon under the new mode without a reload.
    public void RefreshIcon() => OnPropertyChanged(nameof(Icon));

    // Only the first DisplayCap children are ever placed in Children at once;
    // the rest wait in _overflow behind a single "더 보기" row. Keeping the
    // virtualizing TreeView from ever holding thousands of realized rows is
    // what makes favorites reveal/navigation land in a single click no matter
    // how many files a folder has (see MainWindow.NavigateToPath), and it
    // keeps expanding a huge folder by hand from lagging the whole tree.
    // Mutable (not const) and user-configurable (옵션 메뉴, 1~50) - set from
    // AppSettings.MaxItemsPerFolder at startup and whenever changed, same
    // pattern as FileSystemService.SortField/SortDescending.
    public static int DisplayCap = 25;
    private readonly List<FileSystemItem> _overflow = new();
    private bool _showingAll;

    // The synthetic "… 더 보기 (N)" row (see CreateShowMore): rendered in place
    // of icon+name by the tree DataTemplate, and clicking it reveals the rest.
    public bool IsShowMore { get; }
    public int RemainingCount { get; }
    public string ShowMoreLabel => string.Format(Strings.ShowMoreFormat, RemainingCount);

    // Drive roots (FileSystemService.GetDriveRoots) are the only items ever
    // constructed with no parent - used to bold their row (C:, D:, ...).
    public bool IsRoot => Parent is null;

    // Whether "더 보기" has been clicked and every overflow row is currently
    // appended to Children - lets a caller that's about to rebuild this
    // folder's Children from scratch (RefreshChildren via a sort-override
    // change or a background disk change) know it needs to re-reveal the rest
    // afterward too, not just re-set IsExpanded (see MainWindow's
    // CollectExpandedPaths/RefreshFolderPreservingState).
    public bool IsShowingAllChildren => _showingAll;

    public ObservableCollection<FileSystemItem> Children { get; } = new();

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetField(ref _isExpanded, value) && IsDirectory)
            {
                OnPropertyChanged(nameof(Icon));
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    // Part of the Ctrl/Shift-click multi-selection (MainWindow._multiSelection).
    // Kept separate from IsSelected because WPF's TreeView only ever has ONE
    // native selection; the additional rows carry this flag instead, and the
    // row style paints both states with the same brushes. Maintained externally
    // by MainWindow, same pattern as IsAncestorOfSelection below.
    public bool IsMultiSelected
    {
        get => _isMultiSelected;
        set => SetField(ref _isMultiSelected, value);
    }

    // True for every ancestor folder of the currently selected item, so its
    // indent guide line can be highlighted VS Code-style to trace the path
    // down to the active selection. Maintained externally (MainWindow reacts
    // to TreeView.SelectedItemChanged) since selection itself is tracked per
    // TreeViewItem, not on this model.
    public bool IsAncestorOfSelection
    {
        get => _isAncestorOfSelection;
        set => SetField(ref _isAncestorOfSelection, value);
    }

    // True when this folder has its own remembered sort override (set via its
    // right-click "정렬", independent of the app-wide default) - drives the
    // small icon next to its name. Computed once at construction from
    // FileSystemService.SortOverrides (same map LoadChildren itself consults
    // for this folder's own path), then flipped directly by MainWindow when
    // the override is set/cleared afterward so the icon updates immediately
    // without a full reload - same externally-maintained pattern as
    // IsAncestorOfSelection above.
    private bool _hasSortOverride;
    public bool HasSortOverride
    {
        get => _hasSortOverride;
        set => SetField(ref _hasSortOverride, value);
    }

    // Which of the sort-icon images this folder's row currently shows: one of
    // the 4 field/direction images (see FileSystemService.FormatSortOverride-
    // IconUri) while it has an override, or the neutral "follows the global
    // sort" one when it doesn't. Kept in sync with HasSortOverride by
    // MainWindow (SetFolderSortOverride/RotateFolderSortOverride/
    // ClearFolderSortOverride), same externally-maintained pattern.
    private string _sortOverrideIconUri = string.Empty;
    public string SortOverrideIconUri
    {
        get => _sortOverrideIconUri;
        set => SetField(ref _sortOverrideIconUri, value);
    }

    // That icon's ToolTip, naming the state it's showing ("정렬: 이름 오름차순
    // (클릭하여 전환)") - always set together with the icon above, since the
    // image alone doesn't say which sort is active. Per-item rather than one
    // static string on the Border, precisely because it differs per folder.
    private string _sortOverrideTooltip = string.Empty;
    public string SortOverrideTooltip
    {
        get => _sortOverrideTooltip;
        set => SetField(ref _sortOverrideTooltip, value);
    }

    // Drives the inline VS Code-style rename UI in the tree row's
    // DataTemplate (a TextBox swapped in for the name TextBlock), rather
    // than a separate popup dialog.
    public bool IsEditing
    {
        get => _isEditing;
        set => SetField(ref _isEditing, value);
    }

    // Backing text for the inline rename TextBox - kept separate from Name
    // (which stays the on-disk name until the rename actually succeeds and
    // RefreshChildren rebuilds this item from disk).
    public string EditingName
    {
        get => _editingName;
        set => SetField(ref _editingName, value);
    }

    // True for a network drive's root and everything beneath it - set on the
    // root by FileSystemService.GetDriveRoots (DriveType.Network) and simply
    // inherited down as children are constructed. Drives the small marker on
    // folder icons so NAS territory is recognizable mid-scroll, the way
    // Explorer badges the network drive icon itself.
    public bool IsOnNetworkDrive { get; set; }

    public FileSystemItem(string name, string fullPath, bool isDirectory, FileSystemItem? parent = null)
    {
        Name = name;
        FullPath = fullPath;
        IsDirectory = isDirectory;
        Parent = parent;
        IsOnNetworkDrive = parent?.IsOnNetworkDrive ?? false;
        if (isDirectory)
        {
            // Always resolves to SOME icon - this folder's own override if it
            // has one, otherwise the neutral one - so the icon shown while
            // merely selected (see MainWindow.xaml's IsSelected trigger) is
            // there to click instead of rendering blank until an override
            // exists.
            if (FileSystemService.SortOverrides.TryGetValue(
                FileSystemService.NormalizeSortOverridePath(fullPath), out var over))
            {
                _hasSortOverride = true;
                _sortOverrideIconUri = FileSystemService.FormatSortOverrideIconUri(over.Field, over.Descending);
                _sortOverrideTooltip = FileSystemService.FormatSortTooltip(over.Field, over.Descending);
            }
            else
            {
                // No override - the neutral icon, which is also the first stop
                // in the click-rotation ("follow the global sort").
                _sortOverrideIconUri = FileSystemService.NoSortOverrideIconUri;
                _sortOverrideTooltip = FileSystemService.NoSortOverrideTooltip;
            }
            Children.Add(new FileSystemItem());
        }
    }

    private FileSystemItem()
    {
        IsPlaceholder = true;
        Name = string.Empty;
        FullPath = string.Empty;
    }

    // The trailing "더 보기" row for an oversized folder; carries the count of
    // items still hidden in the parent's overflow so the row can label itself.
    private FileSystemItem(FileSystemItem parent, int remainingCount)
    {
        IsShowMore = true;
        RemainingCount = remainingCount;
        Parent = parent;
        Name = string.Empty;
        FullPath = string.Empty;
    }

    public static FileSystemItem CreateShowMore(FileSystemItem parent, int remainingCount)
        => new(parent, remainingCount);

    // Lets MainWindow's whole-tree refresh (RefreshAllLoadedFolders) skip
    // folders that were never expanded - nothing loaded means nothing stale to
    // re-read from disk.
    public bool ChildrenLoaded => _childrenLoaded;

    // When Children were last actually read off the disk. The live
    // external-change refresh (MainWindow's QueueExternalRefresh) compares this
    // against the moment a change was reported, to tell "our listing predates
    // that change" from "we already re-read it afterwards, so there's nothing
    // to redo". Environment.TickCount64, not DateTime - monotonic, so a system
    // clock change can't make a fresh load look ancient.
    public long LastLoadedTicks { get; private set; }

    // Set by MainWindow's external-change path when a watcher event lands on
    // this folder while it is loaded but COLLAPSED: the live refresh only
    // patches expanded folders, and EnsureChildrenLoaded caches - so without
    // this flag the next expand would show the stale pre-change listing
    // (2026-07-22: screenshot taken while its folder was auto-collapsed,
    // expand showed the old newest-first top row). Consumed by
    // TreeViewItem_Expanded, cleared by any real re-read.
    public bool PendingExternalRefresh { get; set; }

    public void EnsureChildrenLoaded()
    {
        if (_childrenLoaded || !IsDirectory)
        {
            return;
        }

        // Stamped BEFORE the read, not after: a file created while LoadChildren
        // is enumerating can be missed by the enumeration yet have its watcher
        // event carry an earlier tick than an after-the-read stamp - which made
        // QueueExternalRefresh's "already re-read it afterwards" check skip the
        // refresh and the file never appear. Stamping first errs the other way:
        // worst case one redundant refresh of a folder that did catch the file.
        LastLoadedTicks = Environment.TickCount64;
        var loaded = FileSystemService.LoadChildren(FullPath, this, out bool readFailed);

        // A failed read (sleeping NAS, unplugged drive) is UNKNOWN contents,
        // not empty contents: keep the placeholder so the expander arrow
        // stays, and stay "not loaded" so the next expand simply retries -
        // recording empty here is what left a network drive permanently
        // arrow-less for the session.
        if (readFailed)
        {
            return;
        }

        _childrenLoaded = true;
        PendingExternalRefresh = false;
        PopulateCapped(loaded);
    }

    // Fills Children with at most DisplayCap items; anything beyond that is
    // parked in _overflow behind a single trailing "더 보기" row.
    private void PopulateCapped(List<FileSystemItem> loaded)
    {
        Children.Clear();
        _overflow.Clear();
        _showingAll = false;

        if (loaded.Count <= DisplayCap)
        {
            foreach (var child in loaded)
            {
                Children.Add(child);
            }
            return;
        }

        for (int i = 0; i < DisplayCap; i++)
        {
            Children.Add(loaded[i]);
        }
        _overflow.AddRange(loaded.GetRange(DisplayCap, loaded.Count - DisplayCap));
        Children.Add(CreateShowMore(this, _overflow.Count));
    }

    // "더 보기" clicked: drop that row and append everything held back. Only
    // adds to Children, so the first DisplayCap rows - and any subtrees
    // expanded within them - keep their state.
    public void ShowAllChildren()
    {
        if (_showingAll || _overflow.Count == 0)
        {
            return;
        }
        if (Children.Count > 0 && Children[^1].IsShowMore)
        {
            Children.RemoveAt(Children.Count - 1);
        }
        foreach (var child in _overflow)
        {
            Children.Add(child);
        }
        _showingAll = true;
    }

    // Returns a fully-revealed folder to the capped state (first DisplayCap +
    // a "더 보기" row) so navigating to another favorite never has to walk past
    // a huge realized list (see MainWindow.NavigateToPath). Only removes the
    // overflow rows it previously appended, leaving the first DisplayCap - and
    // anything expanded within them - untouched.
    public void RecollapseOverflow()
    {
        if (!_showingAll || _overflow.Count == 0)
        {
            return;
        }
        while (Children.Count > DisplayCap)
        {
            Children.RemoveAt(Children.Count - 1);
        }
        Children.Add(CreateShowMore(this, _overflow.Count));
        _showingAll = false;
    }

    // Finds a direct child by name for navigation, looking past the cap into
    // the overflow too - and revealing the overflow when the match is hidden
    // there, so RevealChain can realize its container. A path segment is
    // always a directory (they sort ahead of files), so this only ever forces
    // a reveal on a folder with more than DisplayCap subfolders, not on the
    // many-files folders capping exists to keep light.
    public FileSystemItem? FindChildForNavigation(string name)
    {
        var visible = Children.FirstOrDefault(c =>
            !c.IsPlaceholder && !c.IsShowMore &&
            string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        if (visible is not null)
        {
            return visible;
        }

        var hidden = _overflow.FirstOrDefault(c =>
            string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        if (hidden is not null)
        {
            ShowAllChildren();
            return hidden;
        }

        return null;
    }

    // Re-reads this folder from disk and applies the result as a DIFF against
    // the current rows instead of clear-and-refill: entries that survived keep
    // their existing FileSystemItem instance - and with it their loaded
    // children, expanded state, and (crucially) their realized tree container.
    // Only genuinely new/removed/moved rows touch the collection at all, so a
    // change to one file cannot tear down and regenerate the whole expanded
    // subtree beneath this folder.
    //
    // This replaced clear-and-refill for every WATCHER-driven refresh
    // (2026-07-23 02:4x): a project switch in an IDE churns some ancestor
    // folder's listing every few seconds, and rebuilding that ancestor
    // wholesale destroyed every realized row below it - the whole viewport
    // went blank until the user poked the layout, faster than the
    // post-refresh forced redraw could win back (redraw.log showed dozens of
    // external-refresh passes AROUND the blank, proving redraw-after-rebuild
    // was the wrong altitude for the fix). A no-op change (hidden file,
    // attribute flip) now results in zero collection operations.
    public void MergeChildrenFromDisk()
    {
        if (!IsDirectory || !_childrenLoaded)
        {
            return;
        }
        PendingExternalRefresh = false;
        // Same stamped-before-the-read rule as EnsureChildrenLoaded.
        LastLoadedTicks = Environment.TickCount64;

        var fresh = FileSystemService.LoadChildren(FullPath, this, out bool readFailed);

        // A refresh that couldn't actually read the folder keeps what's on
        // screen: stale rows beat wiping a NAS root (and every loaded subtree
        // under it) because the drive blinked during a background refresh.
        // The next successful watcher event or expand re-syncs for real.
        if (readFailed)
        {
            return;
        }

        // Reuse the existing instance wherever the fresh listing has an entry
        // of the same name (and same file-vs-folder kind - a name reused for
        // the other kind gets a fresh instance, its old subtree is meaningless).
        var existingByName = new Dictionary<string, FileSystemItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var child in Children)
        {
            if (!child.IsPlaceholder && !child.IsShowMore)
            {
                existingByName[child.Name] = child;
            }
        }
        foreach (var child in _overflow)
        {
            existingByName[child.Name] = child;
        }

        var target = new List<FileSystemItem>(fresh.Count);
        foreach (var loaded in fresh)
        {
            target.Add(existingByName.TryGetValue(loaded.Name, out var existing) && existing.IsDirectory == loaded.IsDirectory
                ? existing
                : loaded);
        }

        // Same cap semantics as PopulateCapped/ShowAllChildren: overflow only
        // exists while not showing all, and shrinking back under the cap
        // returns the folder to the plain uncapped state.
        _overflow.Clear();
        List<FileSystemItem> visibleTarget = target;
        if (!_showingAll && target.Count > DisplayCap)
        {
            visibleTarget = target.GetRange(0, DisplayCap);
            _overflow.AddRange(target.GetRange(DisplayCap, target.Count - DisplayCap));
        }
        else if (target.Count <= DisplayCap)
        {
            _showingAll = false;
        }

        // Sync Children to visibleTarget with minimal operations. Remove pass
        // first (also drops the old "더 보기" row - re-added below with a
        // fresh count), then walk the target order: after step i the first
        // i+1 rows match the target, so a wanted row is only ever found
        // further right (Move) or absent (Insert).
        var keep = new HashSet<FileSystemItem>(visibleTarget);
        for (int i = Children.Count - 1; i >= 0; i--)
        {
            if (!keep.Contains(Children[i]))
            {
                Children.RemoveAt(i);
            }
        }
        for (int i = 0; i < visibleTarget.Count; i++)
        {
            var wanted = visibleTarget[i];
            if (i < Children.Count && ReferenceEquals(Children[i], wanted))
            {
                continue;
            }
            int currentIndex = Children.IndexOf(wanted);
            if (currentIndex >= 0)
            {
                Children.Move(currentIndex, i);
            }
            else
            {
                Children.Insert(i, wanted);
            }
        }

        if (_overflow.Count > 0)
        {
            Children.Add(CreateShowMore(this, _overflow.Count));
        }
    }

    // Re-reads this folder's contents from disk after an operation that
    // changed them (rename/delete/paste). Note this rebuilds Children with
    // fresh instances, so any previously-expanded descendants collapse -
    // background/watcher refreshes must use MergeChildrenFromDisk instead.
    public void RefreshChildren()
    {
        if (!IsDirectory)
        {
            return;
        }
        _childrenLoaded = false;
        EnsureChildrenLoaded();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged(string? propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

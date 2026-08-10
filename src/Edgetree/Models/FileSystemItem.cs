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
    private bool _isDropTarget;
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

    // Bulk-capable: the three methods that rewrite this wholesale (the initial
    // capped fill, "더 보기", and the re-cap) do it in one notification rather
    // than one per row - see BulkObservableCollection for what that was
    // costing. Everything else still adds and removes normally.
    public BulkObservableCollection<FileSystemItem> Children { get; } = new();

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

    // The folder a drop would land in right now, while a drag is over the
    // tree. It used to be marked by moving the native selection there, which
    // meant a drag started inside the tree pulled the selection off the very
    // row the user had picked up and only handed it back on release - two
    // visible jumps for one gesture (user, 2026-08-05: "부모폴더가 선택되고
    // 클릭을 떼야 다시 선택한 파일로 돌아가서 오해의 소지가 좀 있어서요").
    // A flag of its own lets the drop target and the selection be true at the
    // same time, the way Explorer shows it. Painted with the SAME brushes as
    // selection - a deliberate choice, so the mark reads as one thing and no
    // new colour has to be invented or configured. Maintained externally by
    // MainWindow, same pattern as IsMultiSelected above.
    public bool IsDropTarget
    {
        get => _isDropTarget;
        set => SetField(ref _isDropTarget, value);
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

    // Which glyph this folder's sort icon currently draws: the direction arrow
    // while it has its own sort, or the neutral "follows the app-wide default"
    // one when it doesn't (see FileSystemService's geometries). Vector rather
    // than an image so it takes the row's brush and needs no light/dark pair.
    // Kept in sync with HasSortOverride by MainWindow (SetFolderSortOverride/
    // ClearFolderSortOverride), same externally-maintained pattern.
    private Geometry? _sortOverrideIconGeometry;
    public Geometry? SortOverrideIconGeometry
    {
        get => _sortOverrideIconGeometry;
        set => SetField(ref _sortOverrideIconGeometry, value);
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

    // Whether that network drive is answering right now. Drives the badge's
    // colour (green connected / red not), kept up to date by MainWindow's own
    // poll rather than asked per row - one question per drive, not per folder,
    // and never from the row's own render path where a dead mapping would cost
    // seconds. Set on the root and pushed down its loaded rows on change.
    private bool _isNetworkDriveOffline;
    public bool IsNetworkDriveOffline
    {
        get => _isNetworkDriveOffline;
        set => SetField(ref _isNetworkDriveOffline, value);
    }

    private bool _isBookmarked;

    // The 책갈피 marker (see MainWindow's ToggleBookmark and the row
    // template's bookmark glyph). Initialized from the static
    // FileSystemService.BookmarkedPaths in the constructor so refreshes that
    // rebuild items (and lazy loads that create them for the first time)
    // come up already flagged; toggling flips both this and that set.
    public bool IsBookmarked
    {
        get => _isBookmarked;
        set => SetField(ref _isBookmarked, value);
    }

    private bool _isHiddenFolderShown;

    // A folder the user has hidden that is on screen anyway - either because a
    // jump is passing through it (TemporarilyVisiblePaths) or because "숨긴
    // 폴더 표시" is on. The row marks itself so neither case reads as "this
    // folder came back on its own": italic name plus a recessed row, no dimming
    // (see the tree template). False for every ordinary row, so it costs
    // nothing to nobody who has never hidden anything.
    public bool IsHiddenFolderShown
    {
        get => _isHiddenFolderShown;
        set => SetField(ref _isHiddenFolderShown, value);
    }

    private bool _isCut;

    // Waiting on a Ctrl+X paste. Fades the row's ICON only (see the tree
    // template) - the name keeps its full weight, since dimmed text isn't how
    // this app marks anything. Initialized from FileSystemService.CutPaths in
    // the constructor, same as IsBookmarked above.
    public bool IsCut
    {
        get => _isCut;
        set => SetField(ref _isCut, value);
    }

    public FileSystemItem(string name, string fullPath, bool isDirectory, FileSystemItem? parent = null)
    {
        Name = name;
        FullPath = fullPath;
        IsDirectory = isDirectory;
        Parent = parent;
        IsOnNetworkDrive = parent?.IsOnNetworkDrive ?? false;
        // Inherited so a row built while the drive is out (a merge running on
        // a stale listing, a folder expanded from cache) starts out looking
        // the same as its neighbours instead of alone in full colour.
        _isNetworkDriveOffline = parent?.IsNetworkDriveOffline ?? false;
        _isBookmarked = FileSystemService.BookmarkedPaths.Contains(fullPath);
        _isCut = FileSystemService.CutPaths.Count > 0 && FileSystemService.CutPaths.Contains(fullPath);
        // Built at all while hidden means it is being shown deliberately - the
        // filter in ReadChildrenFromDisk is what keeps the other case out.
        _isHiddenFolderShown = isDirectory &&
            FileSystemService.HiddenPaths.Count > 0 &&
            FileSystemService.HiddenPaths.Contains(FileSystemService.NormalizeHiddenPath(fullPath));
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
                _sortOverrideIconGeometry = FileSystemService.SortOverrideGeometry(over.Descending);
                _sortOverrideTooltip = FileSystemService.FormatSortTooltip(over.Field, over.Descending);
            }
            else
            {
                // No override of its own - the neutral glyph.
                _sortOverrideIconGeometry = FileSystemService.FollowsGlobalSortGeometry;
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
        _overflow.Clear();
        _showingAll = false;

        if (loaded.Count <= DisplayCap)
        {
            Children.ReplaceAll(loaded);
            return;
        }

        _overflow.AddRange(loaded.GetRange(DisplayCap, loaded.Count - DisplayCap));

        var capped = new List<FileSystemItem>(DisplayCap + 1);
        capped.AddRange(loaded.GetRange(0, DisplayCap));
        capped.Add(CreateShowMore(this, _overflow.Count));
        Children.ReplaceAll(capped);
    }

    // The full loaded listing with the reveal state ignored: the revealed
    // rows plus whatever still waits in _overflow behind "더 보기" (which are
    // the SAME instances Children gains when it is clicked - _overflow is
    // never cleared by the reveal, so the two must not be concatenated
    // blindly). The viewer's carousel counts pictures against this, because
    // "35 / 447" is a claim about the FOLDER, and a total that grew when
    // 더 보기 was clicked read as the counter being wrong (user,
    // 2026-08-09). The "더 보기" row itself is not a child and is skipped.
    public IEnumerable<FileSystemItem> AllLoadedChildren
    {
        get
        {
            foreach (var child in Children)
            {
                if (!child.IsShowMore)
                {
                    yield return child;
                }
            }
            if (!_showingAll)
            {
                foreach (var child in _overflow)
                {
                    yield return child;
                }
            }
        }
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
        // Built as one list and applied in a single notification. Appending the
        // overflow row by row is what made a large folder's reveal - and every
        // filter toggle that replays it - repaint itself hundreds of times.
        var revealed = new List<FileSystemItem>(Children.Count + _overflow.Count);
        foreach (var child in Children)
        {
            if (!child.IsShowMore)
            {
                revealed.Add(child);
            }
        }
        revealed.AddRange(_overflow);
        Children.ReplaceAll(revealed);
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
        // The other half of the same cost: NavigateToPath re-caps every
        // revealed folder before it walks, so a jump was paying one removal
        // notification per hidden row on top of the reveal's own adds.
        int keep = Math.Min(DisplayCap, Children.Count);
        var capped = new List<FileSystemItem>(keep + 1);
        for (int i = 0; i < keep; i++)
        {
            capped.Add(Children[i]);
        }
        capped.Add(CreateShowMore(this, _overflow.Count));
        Children.ReplaceAll(capped);
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

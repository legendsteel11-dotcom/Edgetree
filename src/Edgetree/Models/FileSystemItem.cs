using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using SidebarExplorer.App.Services;

namespace SidebarExplorer.App.Models;

public class FileSystemItem : INotifyPropertyChanged
{
    private bool _isExpanded;
    private bool _isSelected;
    private bool _isAncestorOfSelection;
    private bool _childrenLoaded;
    private bool _isEditing;
    private string _editingName = string.Empty;

    public string Name { get; }
    public string FullPath { get; }
    public bool IsDirectory { get; }
    public bool IsPlaceholder { get; }
    public FileSystemItem? Parent { get; }
    public string IconUri => IsShowMore ? string.Empty : IconResolver.Resolve(this);

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

    public ObservableCollection<FileSystemItem> Children { get; } = new();

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetField(ref _isExpanded, value) && IsDirectory)
            {
                OnPropertyChanged(nameof(IconUri));
                if (value)
                {
                    StartWatching();
                }
                else
                {
                    StopWatching();
                }
            }
        }
    }

    // Live-refresh for whatever's currently expanded: fires (on the
    // FileSystemWatcher's own thread pool thread, not the UI thread) when a
    // watched folder's contents change externally - e.g. from Windows
    // Explorer or any other program. MainWindow subscribes once and owns
    // marshaling to the UI thread/debouncing/actually refreshing - see its
    // own remarks for why that lives there instead of here.
    public static event Action<FileSystemItem>? ExternalChange;

    private FileSystemWatcher? _watcher;

    private void StartWatching()
    {
        if (_watcher is not null || !IsDirectory || IsPlaceholder || IsShowMore)
        {
            return;
        }

        try
        {
            var watcher = new FileSystemWatcher(FullPath)
            {
                // Deliberately no NotifyFilters.LastWrite/no Changed
                // subscription below - this tree only ever shows a folder's
                // list of names, and a file being edited in place (same
                // name) doesn't change what that list looks like. Watching
                // LastWrite too would mean every autosave/log write nearby
                // resets the debounce and thrashes a refresh for a change
                // nothing here actually displays differently.
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
            };
            watcher.Created += OnWatcherEvent;
            watcher.Deleted += OnWatcherEvent;
            watcher.Renamed += OnWatcherEvent;
            watcher.EnableRaisingEvents = true;
            _watcher = watcher;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Folder deleted/unavailable/inaccessible right as we tried to
            // watch it - same tolerance as any other filesystem race
            // elsewhere in this app; this folder just doesn't get live
            // updates, nothing else depends on the watcher existing.
        }
    }

    private void StopWatching()
    {
        if (_watcher is not { } watcher)
        {
            return;
        }
        _watcher = null;

        watcher.EnableRaisingEvents = false;
        watcher.Created -= OnWatcherEvent;
        watcher.Deleted -= OnWatcherEvent;
        watcher.Renamed -= OnWatcherEvent;
        watcher.Dispose();
    }

    private void OnWatcherEvent(object sender, FileSystemEventArgs e) => ExternalChange?.Invoke(this);

    // Old children/overflow items about to be discarded (see PopulateCapped)
    // never have IsExpanded set back to false the normal way - nothing sets
    // it, they're just dropped - which would otherwise leak their
    // FileSystemWatchers (and everything expanded further down inside them)
    // indefinitely.
    private static void StopWatchingRecursive(FileSystemItem item)
    {
        item.StopWatching();
        foreach (var child in item.Children)
        {
            StopWatchingRecursive(child);
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
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

    public FileSystemItem(string name, string fullPath, bool isDirectory, FileSystemItem? parent = null)
    {
        Name = name;
        FullPath = fullPath;
        IsDirectory = isDirectory;
        Parent = parent;
        if (isDirectory)
        {
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

    public void EnsureChildrenLoaded()
    {
        if (_childrenLoaded || !IsDirectory)
        {
            return;
        }
        _childrenLoaded = true;

        PopulateCapped(FileSystemService.LoadChildren(FullPath, this));
    }

    // Fills Children with at most DisplayCap items; anything beyond that is
    // parked in _overflow behind a single trailing "더 보기" row.
    private void PopulateCapped(List<FileSystemItem> loaded)
    {
        foreach (var child in Children)
        {
            StopWatchingRecursive(child);
        }
        foreach (var child in _overflow)
        {
            StopWatchingRecursive(child);
        }

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

    // Re-reads this folder's contents from disk after an operation that
    // changed them (rename/delete/paste). Note this rebuilds Children with
    // fresh instances, so any previously-expanded descendants collapse.
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

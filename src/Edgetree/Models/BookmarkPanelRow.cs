using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace SidebarExplorer.App.Models;

// One row of the side panel while it is showing bookmarks (see AppSettings.
// SidePanelMode). Favorites bind straight to their stored FavoriteEntry, but a
// bookmark row carries three things the stored path does not: its position in
// the Ctrl+Alt+L/J cycle, whether the cycle is standing on it right now, and an
// icon that can only be known once someone has asked the disk whether the path
// is a folder or a file.
public class BookmarkPanelRow : INotifyPropertyChanged
{
    private ImageSource? _icon;
    private bool _isCurrent;
    private bool _isDirectory = true;
    private bool _isOutOfTree;
    private bool _isHiddenFolder;

    public BookmarkPanelRow(int number, string path, string name)
    {
        Number = number;
        Path = path;
        Name = name;
    }

    // 1-based and counted from the top, so the number a row shows is also the
    // number of times Ctrl+Alt+L would have to be pressed to reach it. The
    // answer to "how do I know what order these are in" - chosen over
    // letting the list be dragged into order (2026-08-02), because the order
    // IS the cycle order and reordering would silently rewire the shortcuts.
    public int Number { get; }

    public string Path { get; }

    public string Name { get; }

    public ImageSource? Icon
    {
        get => _icon;
        set => SetField(ref _icon, value);
    }

    // Drives the name colour, the same folder/file split the tree uses. Starts
    // as a folder because that is the commoner case and because a wrong guess
    // only shows until the probe answers.
    public bool IsDirectory
    {
        get => _isDirectory;
        set => SetField(ref _isDirectory, value);
    }

    // Where the Ctrl+Alt+L/J cycle is standing. Not the same as the row being
    // selected: selection follows what the user last clicked in the panel,
    // while this follows the cycle, and the two are only sometimes the same
    // row.
    public bool IsCurrent
    {
        get => _isCurrent;
        set => SetField(ref _isCurrent, value);
    }

    // The tree is not showing this row's target right now, so the row is drawn
    // muted (2026-09-05, on the author's question, then widened by the author
    // the same day to cover the second reason).
    //
    // TWO REASONS, ONE LOOK, and they cost the click different things:
    //
    //   a file whose kind 표시할 파일 형식 is filtering out - it is never put
    //   into its folder's Children, so the reveal walk breaks at the last
    //   segment and lands on the PARENT FOLDER, silently, which is the part
    //   this is really for;
    //
    //   a folder that 숨기기 has excluded - the click still arrives, because
    //   the walk reveals hidden folders along its path for the length of the
    //   jump, and the row it lands on is the one the tree marks as shown-for-
    //   now.
    //
    // Both are "not in the tree as it stands", which is what the muting says,
    // and neither hides the bookmark: a row disappearing when a chip is pressed
    // or a folder is excluded would be a worse surprise than one that says why
    // it looks different.
    public bool IsOutOfTree
    {
        get => _isOutOfTree;
        set => SetField(ref _isOutOfTree, value);
    }

    // The 숨기기 half of IsOutOfTree, kept apart because the row's right-click
    // menu offers 숨김 해제 on it - and that is an action only the folder case
    // has. A filtered file has no per-row undo: the chips are the undo.
    public bool IsHiddenFolder
    {
        get => _isHiddenFolder;
        set => SetField(ref _isHiddenFolder, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

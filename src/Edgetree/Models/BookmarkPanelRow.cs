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

    public BookmarkPanelRow(int number, string path, string name)
    {
        Number = number;
        Path = path;
        Name = name;
    }

    // 1-based and counted from the top, so the number a row shows is also the
    // number of times Ctrl+Alt+L would have to be pressed to reach it. The
    // user's answer to "how do I know what order these are in" - chosen over
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

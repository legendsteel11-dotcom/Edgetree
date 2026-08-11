using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace SidebarExplorer.App.Models;

// One frame of the viewer's filmstrip: a file the carousel walks, and the
// thumbnail for it once the shell has answered.
//
// The thumbnail is NOT fetched when the cell is made - a folder of 257 films
// would be 257 shell calls for the dozen cells anyone can see. The strip is
// virtualized, so the fetch is kicked off when a cell's container is realized,
// and Requested is what keeps a cell that scrolls in and out of view from
// asking again every time.
//
// The cell holds the tree's own FileSystemItem rather than just a path: a click
// hands that item straight to SelectVisibleItem, which is the same route the
// carousel chevrons take - so the strip never grows a second idea of what
// "select this picture" means.
public class FilmstripCell : INotifyPropertyChanged
{
    private ImageSource? _thumbnail;

    public FilmstripCell(FileSystemItem item, bool isPlayable)
    {
        Item = item;
        IsPlayable = isPlayable;
    }

    public FileSystemItem Item { get; }

    public string Path => Item.FullPath;

    public string Name => Item.Name;

    // Drives the little play mark in the template. A strip of stills gives no
    // other clue which of them will play - a film's thumbnail is just a frame,
    // and a track's is its album art, which says even less about what it is.
    public bool IsPlayable { get; }

    // False until the container has asked once, whether or not the shell had
    // anything to give: a file with no thumbnail must not be re-asked on every
    // scroll past it, since that is exactly the file the call is slowest for.
    public bool Requested { get; set; }

    // The same guard for the cheap speculative ask made ahead of the strip on a
    // network folder, kept SEPARATE from Requested on purpose: that ask reads
    // the file's own header and stops there, so its coming back empty says
    // nothing about whether the shell could make a thumbnail. Sharing one flag
    // would let a speculative miss decide the cell is blank for good.
    public bool AskedAhead { get; set; }

    public ImageSource? Thumbnail
    {
        get => _thumbnail;
        set => SetField(ref _thumbnail, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

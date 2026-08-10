using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace SidebarExplorer.App.Models;

// An ObservableCollection that can be rewritten in ONE notification.
//
// Why it exists: a folder's rows are bound to a virtualizing TreeView, and
// that panel does real work on every CollectionChanged it receives - it
// invalidates measure and may generate or drop containers there and then.
// Adding a "더 보기"-revealed folder's few hundred rows one Add at a time
// therefore costs a few hundred layout invalidations, and WPF paints some of
// the half-arranged states on the way through: the tree was seen with its rows
// drawn on top of each other for about a second after a file-filter toggle
// (reported 2026-08-10, on a folder with hundreds of matching files; a folder
// with a handful never showed it).
//
// One Reset instead makes the panel throw its containers away and rebuild them
// once. That is a bigger hammer per notification and still far cheaper than N
// small ones - and, more to the point, there is no intermediate state left for
// WPF to paint.
//
// Deliberately NOT used for the watcher's incremental merge
// (MergeChildrenFromDisk): that one touches the one or two rows that actually
// changed, and a Reset there would drop every container in the folder - and
// the selection's own container with them - for the sake of a single new file.
public sealed class BulkObservableCollection<T> : ObservableCollection<T>
{
    public void ReplaceAll(IEnumerable<T> items)
    {
        CheckReentrancy();

        // Items is the protected inner list - writing through it is what keeps
        // this silent until the single notification below.
        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(item);
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}

using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
// The WinForms SDK is referenced for the tray icon, so the bare name is
// ambiguous - same reason App.xaml.cs aliases Application.
using Size = System.Windows.Size;

namespace SidebarExplorer.App;

// A wrap layout that VIRTUALIZES, which WPF does not ship. WrapPanel builds a
// container for every item, and the thumbnail bar's own history says what that
// costs here: a folder of 2,402 files, cells that ARE the thumbnail cache, and
// pixels that live outside the GC heap. The tree paid this once already
// (realized rows 2,038 -> 288); a grid of the same folder would pay it again
// and worse.
//
// THE SCROLL UNIT IS A ROW, not a pixel, and that is the load-bearing decision.
// The strip's fetch sweep asks the ScrollViewer what is on screen and reads the
// answer as item numbers - it works today because a VirtualizingStackPanel with
// CanContentScroll scrolls by item. Keeping rows as the unit here means the same
// three lines still answer, with the row multiplied by the column count, and
// everything downstream of them (the pacing, the retain window, the trickle,
// the trimming) never learns that the layout changed.
//
// The footprint of one cell comes in as ItemWidth/ItemHeight rather than being
// measured off a child: the sizes are app resources the cell template already
// binds to, so one place owns them and a size change cannot mean two things.
public class VirtualizingWrapPanel : VirtualizingPanel, IScrollInfo
{
    public static readonly DependencyProperty ItemWidthProperty =
        DependencyProperty.Register(
            nameof(ItemWidth), typeof(double), typeof(VirtualizingWrapPanel),
            new FrameworkPropertyMetadata(64.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty ItemHeightProperty =
        DependencyProperty.Register(
            nameof(ItemHeight), typeof(double), typeof(VirtualizingWrapPanel),
            new FrameworkPropertyMetadata(64.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double ItemWidth
    {
        get => (double)GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    public double ItemHeight
    {
        get => (double)GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    // How many cells fit across. Read by the owner to turn a row offset into
    // item numbers, which is the whole reason the unit is a row.
    public int Columns { get; private set; } = 1;

    private double _offsetRows;
    private int _rowCount;
    private int _visibleRows = 1;
    private Size _viewport;
    private Size _extent;

    protected override Size MeasureOverride(Size availableSize)
    {
        // Touched before the generator is used: this is what brings the
        // generator to life on a panel that has never realized anything.
        var children = InternalChildren;
        var owner = ItemsControl.GetItemsOwner(this);
        int count = owner?.Items.Count ?? 0;

        double cellWidth = Math.Max(1, ItemWidth);
        double cellHeight = Math.Max(1, ItemHeight);

        // An infinite constraint means someone is asking how big this wants to
        // be, not how much room it has. Answering with one row keeps a bar-sized
        // request bar-sized rather than reporting the whole folder's height.
        double width = double.IsInfinity(availableSize.Width) ? cellWidth : availableSize.Width;
        double height = double.IsInfinity(availableSize.Height) ? cellHeight : availableSize.Height;

        Columns = Math.Max(1, (int)(width / cellWidth));
        _rowCount = count == 0 ? 0 : (count + Columns - 1) / Columns;
        _visibleRows = Math.Max(1, (int)(height / cellHeight));

        UpdateScrollInfo(new Size(width, height));

        int firstRow = (int)_offsetRows;
        int firstIndex = firstRow * Columns;

        // ONE ROW PAST THE BOTTOM is realized on purpose: a viewport that is not
        // a whole number of rows shows part of the next one, and a row built
        // only when it is fully on screen appears late every time.
        int lastIndex = Math.Min(count - 1, ((firstRow + _visibleRows + 1) * Columns) - 1);

        if (count > 0 && firstIndex <= lastIndex)
        {
            RealizeRange(firstIndex, lastIndex, new Size(cellWidth, cellHeight));
            CleanUpItems(firstIndex, lastIndex);
        }
        else if (children.Count > 0)
        {
            RemoveInternalChildRange(0, children.Count);
        }

        // Never the whole extent: this reports the room it was GIVEN, so the
        // host's height is the host's business (the grip sets it) and this panel
        // scrolls inside it.
        return new Size(width, double.IsInfinity(availableSize.Height) ? cellHeight : height);
    }

    private void RealizeRange(int firstIndex, int lastIndex, Size cell)
    {
        var generator = ItemContainerGenerator;
        var start = generator.GeneratorPositionFromIndex(firstIndex);
        int childIndex = start.Offset == 0 ? start.Index : start.Index + 1;

        using (generator.StartAt(start, GeneratorDirection.Forward, true))
        {
            for (int index = firstIndex; index <= lastIndex; index++, childIndex++)
            {
                if (generator.GenerateNext(out bool isNew) is not UIElement child)
                {
                    break;
                }

                if (isNew)
                {
                    if (childIndex >= InternalChildren.Count)
                    {
                        AddInternalChild(child);
                    }
                    else
                    {
                        InsertInternalChild(childIndex, child);
                    }

                    generator.PrepareItemContainer(child);
                }

                child.Measure(cell);
            }
        }
    }

    // Everything outside the realized range goes back to the generator. Walked
    // from the end so the indices below the one being removed do not move.
    private void CleanUpItems(int firstIndex, int lastIndex)
    {
        var generator = ItemContainerGenerator;
        for (int i = InternalChildren.Count - 1; i >= 0; i--)
        {
            var position = new GeneratorPosition(i, 0);
            int index = generator.IndexFromGeneratorPosition(position);
            if (index < firstIndex || index > lastIndex)
            {
                generator.Remove(position, 1);
                RemoveInternalChildRange(i, 1);
            }
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var generator = ItemContainerGenerator;
        double cellWidth = Math.Max(1, ItemWidth);
        double cellHeight = Math.Max(1, ItemHeight);
        int firstRow = (int)_offsetRows;

        for (int i = 0; i < InternalChildren.Count; i++)
        {
            var child = InternalChildren[i];
            int index = generator.IndexFromGeneratorPosition(new GeneratorPosition(i, 0));
            if (index < 0)
            {
                continue;
            }

            int row = index / Columns;
            int column = index % Columns;

            // The cell keeps its own size (the template binds it to the same
            // resources this panel is measured from) and sits at the top-left of
            // its footprint, so the gap between cells is the difference between
            // the two - the container's own margin horizontally, and the slack
            // in ItemHeight vertically.
            child.Arrange(new Rect(
                column * cellWidth,
                (row - firstRow) * cellHeight,
                cellWidth,
                cellHeight));
        }

        return finalSize;
    }

    protected override void OnItemsChanged(object sender, ItemsChangedEventArgs args)
    {
        switch (args.Action)
        {
            case NotifyCollectionChangedAction.Remove:
            case NotifyCollectionChangedAction.Replace:
            case NotifyCollectionChangedAction.Move:
                RemoveInternalChildRange(args.Position.Index, args.ItemUICount);
                break;

            case NotifyCollectionChangedAction.Reset:
                // A new folder. Everything realized belongs to the old one, and
                // the offset with it - staying where the last folder was scrolled
                // to would open the new one somewhere in its middle.
                RemoveInternalChildRange(0, InternalChildren.Count);
                _offsetRows = 0;
                break;
        }

        InvalidateMeasure();
    }

    // ScrollIntoView on a virtualizing panel arrives here rather than at
    // MakeVisible, because the container it wants may not exist yet.
    protected override void BringIndexIntoView(int index)
    {
        if (index < 0 || Columns <= 0)
        {
            return;
        }

        ScrollRowIntoView(index / Columns);
    }

    private void ScrollRowIntoView(int row)
    {
        int first = (int)_offsetRows;
        if (row < first)
        {
            SetVerticalOffset(row);
        }
        else if (row > first + _visibleRows - 1)
        {
            SetVerticalOffset(row - _visibleRows + 1);
        }
    }

    private void UpdateScrollInfo(Size viewport)
    {
        var extent = new Size(viewport.Width, _rowCount);
        bool changed = extent != _extent || viewport.Width != _viewport.Width ||
            _visibleRows != (int)_viewport.Height;

        _extent = extent;
        _viewport = new Size(viewport.Width, _visibleRows);

        double maxOffset = Math.Max(0, _rowCount - _visibleRows);
        if (_offsetRows > maxOffset)
        {
            _offsetRows = maxOffset;
            changed = true;
        }

        if (changed)
        {
            ScrollOwner?.InvalidateScrollInfo();
        }
    }

    // ----- IScrollInfo, in rows ------------------------------------------------

    public bool CanHorizontallyScroll { get; set; }

    public bool CanVerticallyScroll { get; set; }

    public double ExtentHeight => _extent.Height;

    public double ExtentWidth => _extent.Width;

    public double HorizontalOffset => 0;

    public double VerticalOffset => _offsetRows;

    public double ViewportHeight => _viewport.Height;

    public double ViewportWidth => _viewport.Width;

    public ScrollViewer? ScrollOwner { get; set; }

    public void LineUp() => SetVerticalOffset(_offsetRows - 1);

    public void LineDown() => SetVerticalOffset(_offsetRows + 1);

    public void PageUp() => SetVerticalOffset(_offsetRows - _visibleRows);

    public void PageDown() => SetVerticalOffset(_offsetRows + _visibleRows);

    // Three rows a notch, the same as Windows' own default for a list. A grid
    // scrolled one row per notch reads as sluggish next to every other list on
    // the screen.
    public void MouseWheelUp() => SetVerticalOffset(_offsetRows - 3);

    public void MouseWheelDown() => SetVerticalOffset(_offsetRows + 3);

    public void SetVerticalOffset(double offset)
    {
        double maxOffset = Math.Max(0, _rowCount - _visibleRows);
        offset = Math.Clamp(Math.Round(offset), 0, maxOffset);
        if (Math.Abs(offset - _offsetRows) < 0.001)
        {
            return;
        }

        _offsetRows = offset;
        ScrollOwner?.InvalidateScrollInfo();
        InvalidateMeasure();
    }

    // Nothing scrolls sideways: the columns are fitted to the width, so there is
    // never anything off the right edge to reach.
    public void LineLeft()
    {
    }

    public void LineRight()
    {
    }

    public void PageLeft()
    {
    }

    public void PageRight()
    {
    }

    public void MouseWheelLeft()
    {
    }

    public void MouseWheelRight()
    {
    }

    public void SetHorizontalOffset(double offset)
    {
    }

    public Rect MakeVisible(Visual visual, Rect rectangle)
    {
        for (int i = 0; i < InternalChildren.Count; i++)
        {
            if (!ReferenceEquals(InternalChildren[i], visual))
            {
                continue;
            }

            int index = ItemContainerGenerator.IndexFromGeneratorPosition(new GeneratorPosition(i, 0));
            if (index >= 0 && Columns > 0)
            {
                ScrollRowIntoView(index / Columns);
            }

            break;
        }

        return rectangle;
    }
}

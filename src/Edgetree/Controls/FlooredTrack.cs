using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace SidebarExplorer.App;

// A Track that will not draw its thumb shorter than MinThumbLength.
//
// WHY THIS EXISTS - measured 2026-08-12, after five rounds of raising a number
// that could never have worked. The floor used to be set as MinWidth/MinHeight
// on the Thumb inside MinimalScrollBarStyle, and the app read it back as
// applied: Thumb.ActualWidth 96px. The pixels on screen were 8. Both readings
// were right.
//
//   strip: thumb=96px  slot=8x17  clip=8x17  trackVp=5.00  desired=96x2
//   tree : thumb=96px  slot=17x49 clip=17x49
//
// A Track sizes its thumb as trackLength x viewport / extent and arranges it
// into a slot exactly that size. It consults neither the thumb's MinWidth nor
// its DesiredSize; the only minimum it applies is a small internal one, around
// eight pixels. MinWidth still makes the ELEMENT 96 wide, and WPF clips an
// element that overflows its layout slot - so the floor produced a 96px thumb
// with 8px of it visible, and raising the floor to 400 changed nothing at all.
// The tree's vertical bar had the same fault, asking 96 and receiving 49; it
// read as milder only because 49px is still catchable.
//
// So the floor has to be applied where the slot is decided. Base arranges
// first and this stretches the thumb afterwards, which keeps every direction
// rule WPF already has: which end the decrease button sits at, and how
// IsDirectionReversed flips it, are read back out of where base put the
// pieces rather than reimplemented here.
//
// ValueFromDistance has to move with it. A drag maps pixels to value across
// the distance the thumb can TRAVEL - the track less the thumb - so growing
// the thumb without re-deriving that mapping would make the content run ahead
// of the cursor by the difference, which on a 2,400-item strip is most of the
// bar.
public class FlooredTrack : Track
{
    public static readonly DependencyProperty MinThumbLengthProperty =
        DependencyProperty.Register(
            nameof(MinThumbLength),
            typeof(double),
            typeof(FlooredTrack),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsArrange));

    public double MinThumbLength
    {
        get => (double)GetValue(MinThumbLengthProperty);
        set => SetValue(MinThumbLengthProperty, value);
    }

    // Value per pixel of drag, for the geometry actually drawn. NaN means the
    // thumb was left where base put it and base's own mapping still holds.
    private double _valuePerPixel = double.NaN;

    // System.Drawing is in scope through UseWindowsForms, so Size is qualified.
    protected override System.Windows.Size ArrangeOverride(System.Windows.Size arrangeSize)
    {
        System.Windows.Size arranged = base.ArrangeOverride(arrangeSize);
        _valuePerPixel = double.NaN;

        if (Thumb is not { } thumb || MinThumbLength <= 0)
        {
            return arranged;
        }

        // Track.Orientation shadows the enum's name, so the enum is qualified.
        bool vertical = Orientation == System.Windows.Controls.Orientation.Vertical;
        double trackLength = vertical ? arrangeSize.Height : arrangeSize.Width;
        double floor = MinThumbLength;
        if (trackLength <= floor)
        {
            // Nothing to floor against - a bar shorter than the floor would
            // give the thumb the whole track and no way to move it.
            return arranged;
        }

        Rect placed = LayoutInformation.GetLayoutSlot(thumb);
        double length = vertical ? placed.Height : placed.Width;
        if (length <= 0 || length >= floor)
        {
            return arranged;
        }

        // Where along the track base put it, as a fraction of the travel it
        // had. Read rather than recomputed, so a reversed direction needs no
        // special case here.
        double start = vertical ? placed.Y : placed.X;
        double travelBefore = trackLength - length;
        double fraction = travelBefore > 0 ? Math.Clamp(start / travelBefore, 0, 1) : 0;

        double travelAfter = trackLength - floor;
        double grownStart = travelAfter * fraction;

        // Which repeat button base placed before the thumb, again read rather
        // than assumed: for a vertical scrollbar the decrease button is the
        // one at the top, and IsDirectionReversed swaps them.
        RepeatButton? decrease = DecreaseRepeatButton;
        RepeatButton? increase = IncreaseRepeatButton;
        RepeatButton? leading = decrease;
        RepeatButton? trailing = increase;
        if (decrease is not null && increase is not null)
        {
            Rect decreaseSlot = LayoutInformation.GetLayoutSlot(decrease);
            Rect increaseSlot = LayoutInformation.GetLayoutSlot(increase);
            double decreaseStart = vertical ? decreaseSlot.Y : decreaseSlot.X;
            double increaseStart = vertical ? increaseSlot.Y : increaseSlot.X;
            if (increaseStart < decreaseStart)
            {
                leading = increase;
                trailing = decrease;
            }
        }

        double cross = vertical ? arrangeSize.Width : arrangeSize.Height;

        void Place(UIElement? element, double axisStart, double axisLength)
        {
            if (element is null)
            {
                return;
            }

            axisLength = Math.Max(0, axisLength);
            element.Arrange(vertical
                ? new Rect(0, axisStart, cross, axisLength)
                : new Rect(axisStart, 0, axisLength, cross));
        }

        Place(leading, 0, grownStart);
        Place(thumb, grownStart, floor);
        Place(trailing, grownStart + floor, trackLength - grownStart - floor);

        _valuePerPixel = travelAfter > 0 ? (Maximum - Minimum) / travelAfter : double.NaN;
        return arranged;
    }

    // Mirrors Track's own sign convention exactly - a vertical track carries an
    // extra inversion because screen Y grows downward - and differs from it
    // only in the distance the thumb is understood to travel.
    public override double ValueFromDistance(double horizontal, double vertical)
    {
        if (double.IsNaN(_valuePerPixel))
        {
            return base.ValueFromDistance(horizontal, vertical);
        }

        double scale = IsDirectionReversed ? -1 : 1;
        return Orientation == System.Windows.Controls.Orientation.Horizontal
            ? scale * horizontal * _valuePerPixel
            : -1 * scale * vertical * _valuePerPixel;
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SidebarExplorer.App.Models;
using SidebarExplorer.App.Services;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using Rect = System.Windows.Rect;
using Orientation = System.Windows.Controls.Orientation;
// System.Drawing is in scope through the implicit usings and has a Brush of its
// own; every brush here is WPF's.
using Brush = System.Windows.Media.Brush;

namespace SidebarExplorer.App;

// F1. The document itself is Services/HelpContent; this only draws it.
//
// Built in code because the content is DATA - sections of two-column rows, in
// two languages - and every part of the drawing is the same for every row. In
// XAML it would be the whole document written twice with the markup repeated
// sixty times, and the two languages would drift apart the first time a row was
// added in a hurry.
//
// NO TEXTBLOCK HERE SETS ITS OWN FontSize, and that is the fix for the one way
// building a document in code goes wrong: `FindResource` READS a number, so
// every row was cast in the size the window happened to open at, and Ctrl+/−
// moved the app around a page that had frozen (reported 2026-08-11 - the title
// bar, which is XAML and a DynamicResource, shrank while the document did not
// budge). The window's own FontSize is the dynamic reference and FontSize
// inherits, so leaving it unset is what makes every line follow. The one size
// that differs - the section titles - asks for itself with
// SetResourceReference, never with a read.
public partial class HelpWindow : Window
{
    private readonly AppSettings _settings;
    private readonly Action _onSave;

    public HelpWindow(AppSettings settings, Action onSave)
    {
        InitializeComponent();
        _settings = settings;
        _onSave = onSave;

        var work = SystemParameters.WorkArea;
        MaxHeight = Math.Round(work.Height * 0.92);

        // NOT sized to the content, which is what it did first and which opened
        // a window as tall as the monitor - the document is several screens long
        // and the ScrollViewer is there precisely so it does not have to fit.
        // Half the screen, or 640, whichever is less: 640 is a comfortable read
        // on a tall monitor, and half is what keeps it sensible on a short one.
        Width = _settings.HelpWindowWidth > 0
            ? _settings.HelpWindowWidth
            : (double)FindResource("HelpDialogWidth");
        Height = _settings.HelpWindowHeight > 0
            ? _settings.HelpWindowHeight
            : Math.Min(640, Math.Round(work.Height * 0.5));

        BuildSections();
    }

    private void BuildSections()
    {
        SectionHost.Children.Add(BuildTipBox());

        bool first = true;
        foreach (var section in HelpContent.Build())
        {
            // A rule between sections, not just more air. Air alone left the
            // page reading as one long list with the type occasionally getting
            // bigger; a line says where one subject stops.
            if (!first)
            {
                SectionHost.Children.Add(new Border
                {
                    Height = 1,
                    Background = (Brush)FindResource("SeparatorBrush"),
                    Margin = new Thickness(0, 22, 0, 0),
                });
            }

            SectionHost.Children.Add(BuildSectionTitle(section.Title, first));
            first = false;

            foreach (var group in section.Groups)
            {
                if (!string.IsNullOrEmpty(group.SubTitle))
                {
                    SectionHost.Children.Add(new TextBlock
                    {
                        Text = group.SubTitle,
                        FontWeight = FontWeights.Bold,
                        Foreground = (Brush)FindResource("DialogForeground"),
                        Margin = new Thickness(0, 14, 0, 2),
                    });
                }

                SectionHost.Children.Add(BuildRows(group.Rows));
            }
        }
    }

    // A box, and boxed on purpose: everything below it answers "how do I do X",
    // which only helps once you know what X is worth doing. This is the other
    // half, so it has to read as a different KIND of thing rather than as the
    // first section of the same list - hence a plate and a border where the
    // rest of the page has neither.
    private UIElement BuildTipBox()
    {
        var accent = (Brush)FindResource("AccentForeground");

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = HelpContent.TipsTitle,
            FontWeight = FontWeights.Bold,
            Foreground = accent,
            Margin = new Thickness(0, 0, 0, 8),
        });

        var tips = HelpContent.Tips();
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        for (int i = 0; i < tips.Count; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // The number is its own column so a tip that wraps lines up under
            // its own text rather than under the digit.
            var number = new TextBlock
            {
                Text = (i + 1).ToString() + ".",
                Foreground = accent,
                Margin = new Thickness(0, 2, 8, 2),
            };
            Grid.SetRow(number, i);
            Grid.SetColumn(number, 0);
            grid.Children.Add(number);

            var text = new TextBlock
            {
                Text = tips[i],
                Foreground = (Brush)FindResource("DialogForeground"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 2),
            };
            Grid.SetRow(text, i);
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);
        }

        stack.Children.Add(grid);

        return new Border
        {
            Background = (Brush)FindResource("ControlBackground"),
            BorderBrush = (Brush)FindResource("ControlBorder"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(14, 12, 14, 12),
            // The same air every other section gets before its title. Between
            // two sections that is 22 above the rule and 18 below it; this box
            // draws its own edge instead of a rule, so it takes the sum. At 8
            // the first title sat tight under the box while every title after
            // it had room, and the page read as starting in the wrong place.
            Margin = new Thickness(0, 0, 0, 40),
            Child = stack,
        };
    }

    // Section titles take the shared accent, and a dot leads each one. The
    // colour is safe here where it was not on the gesture column: a heading is
    // not something anyone tries to click, and one accent thing per screenful
    // is what makes the accent mean "look here" at all.
    private UIElement BuildSectionTitle(string title, bool first)
    {
        var accent = (Brush)FindResource("AccentForeground");

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, first ? 0 : 18, 0, 4),
        };

        row.Children.Add(new System.Windows.Shapes.Ellipse
        {
            Width = 5,
            Height = 5,
            Fill = accent,
            VerticalAlignment = VerticalAlignment.Center,
            // Optically centred on the letters rather than on the line box,
            // which carries descender room the title never uses.
            Margin = new Thickness(1, 1, 8, 0),
        });

        var text = new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.Bold,
            Foreground = accent,
            VerticalAlignment = VerticalAlignment.Center,
        };
        // The only size in the document that is not the inherited one, so it is
        // the only one that has to ask for itself. A reference, not a read:
        // Ctrl+/− has to reach a window that is already open.
        text.SetResourceReference(TextBlock.FontSizeProperty, "HelpTitleFontSize");
        row.Children.Add(text);

        return row;
    }

    // The gesture column takes a THIRD of the width and wraps rather than
    // pushing the meaning off the edge: "전체 화면에서 아래쪽에 마우스" is a
    // sentence, and a column sized for "F5" would put it on one line at 4px.
    private UIElement BuildRows(IReadOnlyList<HelpContent.Row> rows)
    {
        var grid = new Grid { Margin = new Thickness(0, 4, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });

        // NOT the accent colour, which it was for a day. In this app accent
        // means "this is a link, click it" - every other place it appears is
        // underlined and opens something - so a whole column of blue gestures
        // read as a page of dead links. Secondary is what the menus already use
        // for a shortcut hint beside the thing it triggers, which is exactly
        // the relationship these two columns are in.
        var gestureBrush = (Brush)FindResource("SecondaryForeground");
        var meaningBrush = (Brush)FindResource("DialogForeground");

        for (int i = 0; i < rows.Count; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var gesture = new TextBlock
            {
                Text = rows[i].Gesture,
                Foreground = gestureBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 3, 12, 3),
            };
            Grid.SetRow(gesture, i);
            Grid.SetColumn(gesture, 0);
            grid.Children.Add(gesture);

            var meaning = new TextBlock
            {
                Text = rows[i].Meaning,
                Foreground = meaningBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 3, 0, 3),
            };
            Grid.SetRow(meaning, i);
            Grid.SetColumn(meaning, 1);
            grid.Children.Add(meaning);
        }

        return grid;
    }

    // F1 opens it and F1 closes it. A key that only opens leaves the window as
    // something to be got rid of by other means, which is the opposite of what
    // a help key is for.
    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is System.Windows.Input.Key.F1 or System.Windows.Input.Key.Escape)
        {
            e.Handled = true;
            Close();
            return;
        }

        base.OnKeyDown(e);
    }

    private void HeaderGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    // On the way out rather than on every drag: a resize raises a dozen events
    // a second and this writes a file. RestoreBounds rather than Width/Height,
    // so closing it while maximized stores the size it will actually come back
    // at instead of the whole screen.
    protected override void OnClosed(EventArgs e)
    {
        var bounds = WindowState == WindowState.Normal
            ? new Rect(Left, Top, ActualWidth, ActualHeight)
            : RestoreBounds;
        if (bounds.Width > 0 && bounds.Height > 0)
        {
            _settings.HelpWindowWidth = Math.Round(bounds.Width);
            _settings.HelpWindowHeight = Math.Round(bounds.Height);
            _onSave();
        }

        base.OnClosed(e);
    }
}

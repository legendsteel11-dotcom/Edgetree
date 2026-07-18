using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using SidebarExplorer.App.Models;

namespace SidebarExplorer.App;

// Renders a search result filename with the matched substring drawn in the
// highlight color, by building the TextBlock's Inlines from a bound SearchRow
// (rather than a plain Text binding). Attached so the results DataTemplate can
// declare it inline; re-runs whenever the row rebinds (including on virtualized
// container recycling), so the highlight always tracks the current row.
public static class SearchHighlightBehavior
{
    public static readonly DependencyProperty RowProperty = DependencyProperty.RegisterAttached(
        "Row",
        typeof(SearchRow),
        typeof(SearchHighlightBehavior),
        new PropertyMetadata(null, OnRowChanged));

    public static void SetRow(DependencyObject target, SearchRow? value) => target.SetValue(RowProperty, value);

    public static SearchRow? GetRow(DependencyObject target) => (SearchRow?)target.GetValue(RowProperty);

    private static void OnRowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock textBlock)
        {
            return;
        }

        textBlock.Inlines.Clear();

        if (e.NewValue is not SearchRow row)
        {
            return;
        }

        string name = row.FileName;

        // No literal match to point at (header row / wildcard query / stale
        // range): plain text, inheriting the TextBlock's normal foreground.
        if (row.MatchStart < 0 || row.MatchLength <= 0 || row.MatchStart + row.MatchLength > name.Length)
        {
            textBlock.Inlines.Add(new Run(name));
            return;
        }

        if (row.MatchStart > 0)
        {
            textBlock.Inlines.Add(new Run(name[..row.MatchStart]));
        }

        var match = new Run(name.Substring(row.MatchStart, row.MatchLength));
        // Tracks the theme (light/dark) like every other themed brush.
        match.SetResourceReference(TextElement.ForegroundProperty, "FileNameHighlightForeground");
        textBlock.Inlines.Add(match);

        int afterIndex = row.MatchStart + row.MatchLength;
        if (afterIndex < name.Length)
        {
            textBlock.Inlines.Add(new Run(name[afterIndex..]));
        }
    }
}

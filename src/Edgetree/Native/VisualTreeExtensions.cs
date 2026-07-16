using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace SidebarExplorer.App.Native;

internal static class VisualTreeExtensions
{
    public static T? FindAncestor<T>(this DependencyObject? source) where T : DependencyObject
    {
        var current = source;
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }
            current = current is Visual or Visual3D ? VisualTreeHelper.GetParent(current) : null;
        }
        return null;
    }
}

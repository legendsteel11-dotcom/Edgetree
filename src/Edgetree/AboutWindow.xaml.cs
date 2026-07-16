using System.IO;
using System.Reflection;
using System.Windows;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;

namespace SidebarExplorer.App;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        var assembly = Assembly.GetExecutingAssembly();
        VersionText.Text = assembly.GetName().Version?.ToString(3) ?? "1.0.0";

        // No build-time step embeds a real build date (or git commit) yet, so
        // this reads the running exe's own last-write time as a reasonable
        // stand-in - accurate as of whenever this was last built. Assembly.Location
        // is empty for a PublishSingleFile build (the release exe this app is
        // actually distributed as), so this uses Environment.ProcessPath instead
        // - the same property SetStartWithWindows already relies on for exactly
        // this reason.
        string? exePath = Environment.ProcessPath;
        DateText.Text = exePath is not null
            ? File.GetLastWriteTime(exePath).ToString("yyyy-MM-dd")
            : string.Empty;
    }

    private void HeaderGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

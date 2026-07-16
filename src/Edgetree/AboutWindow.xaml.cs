using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using Color = System.Windows.Media.Color;

namespace SidebarExplorer.App;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        Deactivated += Window_Deactivated;

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

    // Nudges attention back to this dialog if the user clicks outside the
    // whole app while it's open (ShowDialog only blocks its owner, not other
    // applications, so that's still possible).
    private void Window_Deactivated(object? sender, EventArgs e)
    {
        var flashBrush = new SolidColorBrush(((SolidColorBrush)RootBorder.BorderBrush).Color);
        RootBorder.BorderBrush = flashBrush;
        flashBrush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation
        {
            To = Color.FromRgb(0x4F, 0xA8, 0xFF),
            Duration = TimeSpan.FromMilliseconds(150),
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(2)
        });
    }

    private void GithubLink_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://github.com/legendsteel11-dotcom/Edgetree",
            UseShellExecute = true
        });
    }
}

using System.Diagnostics;
using System.IO;

namespace SidebarExplorer.App.Services;

// Debug-only record of how the app started and, more to the point, how it
// ended. Written because "the app was gone and I don't remember closing it"
// is otherwise unanswerable: a graceful exit leaves no Windows event, so the
// absence of a crash record proves only that it did NOT crash - never who or
// what closed it. Every deliberate exit path stamps its own reason here, so
// next time the question has an answer instead of a theory.
//
// Compiled out of Release builds entirely (Conditional), like the watcher and
// auto-collapse instruments.
internal static class ExitLog
{
    [Conditional("DEBUG")]
    public static void Record(string reason)
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Edgetree");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, "exit.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {reason}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}

using System.Diagnostics;
using System.IO;
using System.Threading;

namespace SidebarExplorer.App.Services;

// Debug-only record of how the app started and, more to the point, how it
// ended. Written because "the app was gone and I don't remember closing it"
// is otherwise unanswerable: a graceful exit leaves no Windows event, so the
// absence of a crash record proves only that it did NOT crash - never who or
// what closed it. Every deliberate exit path stamps its own reason here, so
// next time the question has an answer instead of a theory.
//
// The original version stopped there, on the theory that "a forced kill
// leaves nothing, which is itself informative". It wasn't: builds kill the
// running debug app with Stop-Process every time, so a build kill and a
// genuine disappearance produced the exact same silence. Two incidents came
// and went unanswered (2026-07-26 first, 2026-07-27 second). The instrument
// was blind in precisely the case it was built for.
//
// So there are now three parts:
//   1. A heartbeat file, rewritten every minute, holding this session's pid,
//      start time and last-alive time. It exists only while a session is
//      running - a clean exit deletes it.
//   2. A post-mortem at startup. If the heartbeat file survived, the previous
//      session never reached OnExit, and this stamps how long it had been up
//      and when it was last known alive - so the death has a time, not just a
//      gap between two log lines.
//   3. A signature for our own kills. tools/kill-edgetree.ps1 stamps the log
//      before killing, and the post-mortem reads that stamp back. Anything
//      that vanishes WITHOUT a signature is the real thing.
//
// Compiled out of Release builds entirely (Conditional), like the watcher and
// auto-collapse instruments.
internal static class ExitLog
{
    // Written by tools/kill-edgetree.ps1 just before it kills the process.
    // Keep the two in sync.
    private const string BuildKillMarker = "build kill requested";

    // Fully qualified: WinForms brings its own Timer into scope here.
    private static System.Threading.Timer? _heartbeat;
    private static string _sessionStart = string.Empty;

    private static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Edgetree");

    private static string LogPath => Path.Combine(Dir, "exit.log");

    private static string HeartbeatPath => Path.Combine(Dir, "heartbeat");

    [Conditional("DEBUG")]
    public static void Record(string reason)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.AppendAllText(
                LogPath,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {reason}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    // Call once at startup, before the "--- started" line, so the post-mortem
    // of the previous session reads above this session's own entries.
    [Conditional("DEBUG")]
    public static void BeginSession()
    {
        PostMortem();

        _sessionStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        WriteHeartbeat();

        // A minute is fine: it bounds the time of death closely enough to line
        // it up against a build, a sleep/resume, or nothing at all - and one
        // small overwrite per minute costs nothing.
        _heartbeat = new System.Threading.Timer(_ => WriteHeartbeat(), null,
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    // Call from OnExit. Removing the file is what tells the next launch that
    // this session ended on purpose - so it must happen on every clean path,
    // which is why it lives in OnExit rather than in any one exit handler.
    [Conditional("DEBUG")]
    public static void EndSession()
    {
        try
        {
            _heartbeat?.Dispose();
            _heartbeat = null;
            File.Delete(HeartbeatPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void WriteHeartbeat()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(
                HeartbeatPath,
                $"{Environment.ProcessId}|{_sessionStart}|{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void PostMortem()
    {
        try
        {
            if (!File.Exists(HeartbeatPath))
            {
                return;
            }

            string[] parts = File.ReadAllText(HeartbeatPath).Split('|');
            File.Delete(HeartbeatPath);

            // A kill mid-write can leave the file short; say so rather than
            // throwing away the fact that a session died without exiting.
            if (parts.Length < 3
                || !DateTime.TryParse(parts[1], out DateTime start)
                || !DateTime.TryParse(parts[2], out DateTime alive))
            {
                Record("previous session ended without a record (heartbeat unreadable)");
                return;
            }

            string detail = $"pid {parts[0]}, last alive {alive:yyyy-MM-dd HH:mm:ss}, "
                + $"up {(int)(alive - start).TotalMinutes}m";

            Record(PreviousSessionEnding() switch
            {
                Ending.BuildKill => $"previous session: killed for a build, signed - {detail}",
                Ending.Crash => $"previous session: crashed, exception above - {detail}",
                _ => $"previous session: VANISHED with no record - {detail}",
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private enum Ending
    {
        Unexplained,
        BuildKill,
        Crash,
    }

    // How the previous session ended, read out of its own stretch of the log.
    //
    // This used to look at the LAST LINE only, and that was wrong in exactly
    // the case the instrument exists for: an unhandled exception writes
    // "UNHANDLED ..." and then its stack trace, dozens of lines of it, so the
    // last line is always a stack frame and never the word being searched for.
    // Every crash there has ever been was therefore filed as VANISHED - which
    // is the one verdict that says "nobody can explain this", printed over the
    // top of a full explanation sitting a few lines above. Two of the recorded
    // disappearances are provably this (2026-07-28's XamlParseException,
    // 2026-08-02's collection-modified), and it was caught only because a
    // crash was deliberately caused on 2026-08-04 and mislabelled in front of
    // us.
    //
    // So the whole of the previous session is read instead: backwards to its
    // "--- started" line, taking the latest marker found. A safeguard that
    // reports the wrong cause is worse than one that reports nothing, because
    // the wrong cause gets believed.
    private static Ending PreviousSessionEnding()
    {
        try
        {
            if (!File.Exists(LogPath))
            {
                return Ending.Unexplained;
            }

            // A few KB, read once per launch.
            string[] lines = File.ReadAllLines(LogPath);
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                string line = lines[i];
                if (line.Contains(BuildKillMarker))
                {
                    return Ending.BuildKill;
                }
                if (line.Contains("UNHANDLED"))
                {
                    return Ending.Crash;
                }
                // The previous session's own opening line: anything above it
                // belongs to a session that has already been accounted for.
                if (line.Contains("--- started"))
                {
                    return Ending.Unexplained;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        return Ending.Unexplained;
    }
}

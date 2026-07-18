using System.Collections.Concurrent;
using System.IO;
using System.Text.RegularExpressions;

namespace SidebarExplorer.App.Services;

// In-memory, session-only file index over one or more chosen roots (a picked
// folder, or every fixed drive for a whole-PC search - see the 2026-07-18
// decision). ScanAsync walks the roots once into a flat list the UI holds;
// filtering that list on every keystroke is what the user experiences as "the
// index". No persistence, no staleness across sessions - the "새로고침" button
// re-scans instead.
public static class FileSearchService
{
    // One matched file. Stores the folder path (shared, see ScanAsync's dedupe)
    // + name + write time; FullPath is composed on demand rather than stored, so
    // a whole-PC index of ~900k files doesn't also carry a full path string per
    // file on top of the folder path it already shares with its siblings.
    // LastWriteTime comes from the FileInfo the enumeration already yields (no
    // extra stat call) and drives the results date sort.
    public sealed record SearchEntry(string DirectoryPath, string FileName, DateTime LastWriteTime)
    {
        public string FullPath => Path.Combine(DirectoryPath, FileName);
    }

    // Reported to the caller in chunks rather than one entry at a time so a
    // big walk streams into the UI in a handful of marshaled hops instead of
    // tens of thousands - see MainWindow's IProgress consumer, which appends
    // each batch on the UI thread (so the list it owns is never touched from
    // the scan thread).
    private const int BatchSize = 1024;

    // How many folders are enumerated at once (see ScanAsync's level-by-level
    // walk). A network share's scan time is dominated by per-folder round-trip
    // latency rather than bandwidth or file count - measured on a NAS at ~6ms
    // per folder, which is round-trip territory, not throughput - so issuing
    // several listings concurrently is close to a straight division of the
    // wall-clock time. On a local disk the latency being overlapped is tiny, so
    // this neither helps nor hurts much there. Kept modest deliberately: the
    // gain flattens out once enough requests are in flight to cover the
    // latency, and a large worker count on a spinning disk would just add seek
    // contention.
    private const int MaxParallelDirectories = 8;

    // Enumerate every file under each root, reporting them to `onBatch` in
    // BatchSize chunks. IgnoreInaccessible walks past an access-denied folder
    // (e.g. "System Volume Information") instead of throwing; AttributesToSkip
    // drops Hidden/System (matching Explorer) and ReparsePoint so a junction/
    // symlink can't loop the walk back on itself or wander onto another volume.
    // Honors ct so a scope change / re-scan cancels a walk already in flight.
    //
    // The recursion is done here rather than by RecurseSubdirectories, so that
    // folders can be enumerated MaxParallelDirectories at a time - see that
    // constant for why concurrency is what matters on a network share. The walk
    // goes level by level (enumerate every folder at this depth, collect their
    // subfolders, repeat) rather than handing a shared work queue to a pool of
    // workers: the termination condition is then simply "this level produced no
    // subfolders", with no need to track whether an idle worker might still be
    // handed more work by a busy one. A level that happens to hold one enormous
    // folder serializes on it, which is the accepted cost of that simplicity.
    public static Task ScanAsync(
        IReadOnlyList<string> roots,
        IProgress<IReadOnlyList<SearchEntry>> onBatch,
        CancellationToken ct)
    {
        return Task.Run(() =>
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = false,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint
            };

            var currentLevel = new List<DirectoryInfo>();
            foreach (var root in roots)
            {
                try
                {
                    var rootInfo = new DirectoryInfo(root);
                    if (rootInfo.Exists)
                    {
                        currentLevel.Add(rootInfo);
                    }
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
                {
                }
            }

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = MaxParallelDirectories,
                CancellationToken = ct
            };

            while (currentLevel.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                var nextLevel = new ConcurrentBag<DirectoryInfo>();

                // Each worker accumulates across the folders it handles and
                // flushes at BatchSize, rather than reporting once per folder -
                // otherwise a tree of tens of thousands of small folders would
                // marshal a tiny batch to the UI thread for every one of them.
                // Progress<T> posts to the captured UI context, so reporting
                // from several threads at once is safe.
                Parallel.ForEach(
                    currentLevel,
                    parallelOptions,
                    () => new List<SearchEntry>(BatchSize),
                    (dir, _, batch) =>
                    {
                        // Materialized once per folder and handed to every entry
                        // in it, so the files of one folder share a single path
                        // string. The old walk needed a dictionary to deduplicate
                        // FileInfo.DirectoryName for this; enumerating a folder
                        // explicitly means we already hold the string.
                        string dirPath = dir.FullName;

                        try
                        {
                            foreach (var file in dir.EnumerateFiles("*", options))
                            {
                                ct.ThrowIfCancellationRequested();

                                batch.Add(new SearchEntry(dirPath, file.Name, file.LastWriteTime));
                                if (batch.Count >= BatchSize)
                                {
                                    onBatch.Report(batch);
                                    batch = new List<SearchEntry>(BatchSize);
                                }
                            }
                        }
                        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                        {
                        }

                        try
                        {
                            foreach (var sub in dir.EnumerateDirectories("*", options))
                            {
                                nextLevel.Add(sub);
                            }
                        }
                        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                        {
                        }

                        return batch;
                    },
                    batch =>
                    {
                        if (batch.Count > 0)
                        {
                            onBatch.Report(batch);
                        }
                    });

                currentLevel = nextLevel.ToList();
            }
        }, ct);
    }

    // Builds the per-query predicate matched against a filename. A query
    // containing '*' or '?' is treated as an anchored wildcard pattern (shell
    // semantics: "*.txt" ends with .txt, "report*" starts with report);
    // anything else is a plain case-insensitive substring match (부분일치).
    // Empty query matches nothing - the results list is blank until the user
    // actually types something. No regex mode by design (this searches names,
    // not contents).
    public static Func<string, bool> BuildMatcher(string query)
    {
        query = query.Trim();
        if (query.Length == 0)
        {
            return static _ => false;
        }

        if (query.Contains('*') || query.Contains('?'))
        {
            // Escape everything, then re-open just the two wildcard chars back
            // into their regex equivalents, anchored so the pattern matches the
            // whole name rather than any substring of it.
            string pattern = "^" + Regex.Escape(query)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";
            var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return name => regex.IsMatch(name);
        }

        return name => name.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}

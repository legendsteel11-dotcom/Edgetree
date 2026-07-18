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

    // Enumerate every file under each root recursively, reporting them to
    // `onBatch` in BatchSize chunks. RecurseSubdirectories + IgnoreInaccessible
    // walks the whole subtree without throwing on the first access-denied folder
    // (e.g. "System Volume Information"); AttributesToSkip drops Hidden/System
    // (matching Explorer) and ReparsePoint so a junction/symlink can't loop a
    // whole-drive walk back on itself or wander onto another volume. Honors ct
    // so a scope change / re-scan cancels a walk already in flight.
    public static Task ScanAsync(
        IReadOnlyList<string> roots,
        IProgress<IReadOnlyList<SearchEntry>> onBatch,
        CancellationToken ct)
    {
        return Task.Run(() =>
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint
            };

            // Every file in one folder shares a single DirectoryPath instance
            // instead of each FileInfo.DirectoryName allocating its own copy -
            // the bulk of what keeps a whole-PC (900k-file) index's memory in
            // check. Lives only for the scan; the shared strings outlive it via
            // the entries that reference them.
            var dirCache = new Dictionary<string, string>(StringComparer.Ordinal);
            var batch = new List<SearchEntry>(BatchSize);

            foreach (var root in roots)
            {
                DirectoryInfo rootInfo;
                try
                {
                    rootInfo = new DirectoryInfo(root);
                    if (!rootInfo.Exists)
                    {
                        continue;
                    }
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    continue;
                }

                foreach (var file in rootInfo.EnumerateFiles("*", options))
                {
                    ct.ThrowIfCancellationRequested();

                    string dir = file.DirectoryName ?? string.Empty;
                    if (!dirCache.TryGetValue(dir, out var sharedDir))
                    {
                        sharedDir = dir;
                        dirCache[dir] = dir;
                    }

                    batch.Add(new SearchEntry(sharedDir, file.Name, file.LastWriteTime));

                    if (batch.Count >= BatchSize)
                    {
                        onBatch.Report(batch);
                        batch = new List<SearchEntry>(BatchSize);
                    }
                }
            }

            if (batch.Count > 0)
            {
                onBatch.Report(batch);
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

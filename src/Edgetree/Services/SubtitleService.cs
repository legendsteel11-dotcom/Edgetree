using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace SidebarExplorer.App.Services;

// Subtitles that sit BESIDE the film as their own file - .smi and .srt.
//
// Deliberately the only kind this app can do at all, and worth stating plainly:
// subtitles carried INSIDE an .mkv belong to the decoder, which hands this app
// finished video frames and nothing else, and subtitles burned into the picture
// (the "KORSUB" rips) are already pixels. So a film with no .smi/.srt next to it
// shows no subtitle line here, and that is the feature working rather than
// failing.
//
// Everything here runs on a worker: a library lives on a NAS, and both the
// directory read that finds the file and the read that parses it are the kind
// of call that has frozen this app before.
public static class SubtitleService
{
    // Start/End in seconds from the film's beginning, and the text with its own
    // line breaks already resolved.
    public sealed record Cue(double Start, double End, string Text);

    // Language tags people actually put on these, in the order worth preferring.
    // Only used to CHOOSE between several candidates; a lone subtitle file wins
    // whatever it is tagged.
    private static readonly string[] PreferredTags = { "ko", "kor", "korean", "ko-kr" };

    private static readonly string[] Extensions = { ".smi", ".srt" };

    // Beside the film and named after it: "movie.mkv" takes "movie.smi", and
    // also "movie.kor.srt" - the language tag is the one variation common enough
    // that leaving it out would miss half a real library.
    //
    // Subfolders (a Subs/ directory) are NOT searched, by the user's call
    // (2026-08-10): it is a second directory read on a cold network share for
    // every selection, paid by everyone to serve a layout most files do not use.
    public static string? Find(string videoPath)
    {
        try
        {
            string? directory = Path.GetDirectoryName(videoPath);
            string baseName = Path.GetFileNameWithoutExtension(videoPath);
            if (string.IsNullOrEmpty(directory) || baseName.Length == 0)
            {
                return null;
            }

            // ONE directory read, filtered here rather than several patterned
            // reads - on a sleeping share each of those is its own wait.
            var candidates = Directory
                .EnumerateFiles(directory, baseName + ".*")
                .Where(file => Extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                .ToList();
            if (candidates.Count == 0)
            {
                return null;
            }

            // Exact name first ("movie.srt"), then a preferred language tag,
            // then whatever is left - so a folder holding movie.eng.srt and
            // movie.kor.srt does not come down to alphabetical order.
            return candidates.FirstOrDefault(file =>
                       string.Equals(Path.GetFileNameWithoutExtension(file), baseName,
                           StringComparison.OrdinalIgnoreCase))
                   ?? candidates.FirstOrDefault(file => PreferredTags.Contains(
                       Path.GetExtension(Path.GetFileNameWithoutExtension(file)).TrimStart('.'),
                       StringComparer.OrdinalIgnoreCase))
                   ?? candidates[0];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    public static List<Cue> Load(string subtitlePath)
    {
        try
        {
            string text = ReadAllText(subtitlePath);
            var cues = Path.GetExtension(subtitlePath).Equals(".smi", StringComparison.OrdinalIgnoreCase)
                ? ParseSami(text, Strings.IsEnglish)
                : ParseSubRip(text);
            cues.RemoveAll(cue => cue.Text.Length == 0 || cue.End <= cue.Start);
            cues.Sort((a, b) => a.Start.CompareTo(b.Start));
            return cues;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException
                                      or ArgumentException or RegexMatchTimeoutException)
        {
            return new List<Cue>();
        }
    }

    // Korean .smi files are overwhelmingly CP949, and .NET Core does not carry
    // that code page: Encoding.GetEncoding(949) throws unless a provider is
    // registered, and the provider is a NuGet package this project does not
    // have - it has none at all, and ships as one exe. So the conversion goes
    // through Win32, which is already how this app reaches the shell and GDI.
    //
    // The order matters. A BOM is believed outright. Without one, UTF-8 is tried
    // STRICTLY - invalid sequences throw rather than turning into replacement
    // characters - because a CP949 file will nearly always contain a byte pair
    // that is not valid UTF-8, while a real UTF-8 file always passes. Only then
    // does it fall to the code page, which accepts anything and would therefore
    // never have rejected a UTF-8 file if it were asked first.
    private static string ReadAllText(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        try
        {
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (ArgumentException)
        {
            return FromCodePage(bytes, 949);
        }
    }

    private static string FromCodePage(byte[] bytes, int codePage)
    {
        if (bytes.Length == 0)
        {
            return string.Empty;
        }

        int count = MultiByteToWideChar(codePage, 0, bytes, bytes.Length, null, 0);
        if (count <= 0)
        {
            // Nothing left to try that could be better than mojibake with the
            // right shape: Latin-1 maps every byte, so the timings at least
            // survive and the file is visibly wrong rather than empty.
            return Encoding.Latin1.GetString(bytes);
        }

        var buffer = new char[count];
        MultiByteToWideChar(codePage, 0, bytes, bytes.Length, buffer, count);
        return new string(buffer);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int MultiByteToWideChar(
        int codePage, uint flags, byte[] bytes, int byteCount, char[]? wide, int wideCount);

    // 00:00:20,000 --> 00:00:24,400 (a dot is accepted too - plenty of files in
    // the wild use one, and rejecting them would be pedantry).
    private static readonly Regex SubRipTiming = new(
        @"(\d{1,2}):(\d{2}):(\d{2})[,.](\d{1,3})\s*-->\s*(\d{1,2}):(\d{2}):(\d{2})[,.](\d{1,3})",
        RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    private static List<Cue> ParseSubRip(string text)
    {
        var cues = new List<Cue>();
        // Split on blank lines - the format's own record separator. \r is
        // normalised first so a file written on either platform lands the same.
        foreach (string block in text.Replace("\r\n", "\n").Replace('\r', '\n')
                     .Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            var match = SubRipTiming.Match(block);
            if (!match.Success)
            {
                continue;
            }

            double start = Seconds(match, 1);
            double end = Seconds(match, 5);
            // Everything after the timing line is the text, index line and all -
            // the index sits BEFORE the timing, so taking what follows the match
            // drops it without having to recognise it.
            string body = block[(match.Index + match.Length)..].Trim('\n', ' ', '\t');
            cues.Add(new Cue(start, end, StripTags(body)));
        }
        return cues;

        static double Seconds(Match m, int group)
            => int.Parse(m.Groups[group].Value, CultureInfo.InvariantCulture) * 3600
               + int.Parse(m.Groups[group + 1].Value, CultureInfo.InvariantCulture) * 60
               + int.Parse(m.Groups[group + 2].Value, CultureInfo.InvariantCulture)
               + int.Parse(m.Groups[group + 3].Value.PadRight(3, '0'), CultureInfo.InvariantCulture) / 1000.0;
    }

    private static readonly Regex SamiSync = new(
        @"<sync\s+start\s*=\s*""?(-?\d+)""?[^>]*>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));

    // The class that follows a marker - <P Class=KRCC>. SAMI's own answer to
    // "which language is this line", and the thing that makes a bilingual file
    // readable instead of a mess.
    private static readonly Regex SamiClass = new(
        @"^\s*<p[^>]*\bclass\s*=\s*""?([A-Za-z0-9_-]+)""?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));

    // Class-name prefixes, not ISO codes: real files write KRCC and ENCC, and
    // the Korean one says "kr" where the standard says "ko". Both are accepted
    // rather than picking a side.
    private static readonly string[] KoreanClassPrefixes = { "kr", "ko" };
    private static readonly string[] EnglishClassPrefixes = { "en" };

    // SAMI is HTML in a trench coat: a flat run of <SYNC Start=ms> markers, each
    // one both the start of its own line and the end of the one before it. A
    // marker whose body is empty (&nbsp; is the convention) is a CLEAR, and
    // exists only to end the previous line - which is why the cues are built by
    // pairing each marker with the next rather than by reading an end time.
    //
    // AND A FILE CAN HOLD SEVERAL TRACKS. Not interleaved, either: the Korean
    // track runs to the end of the film and then the English one starts over
    // from zero, in the same flat list of markers (a real file, 2026-08-10 -
    // 2396 Korean markers followed by 3058 English ones). Read as one track and
    // sorted by time, that plays both at once, alternating language line by
    // line - which is exactly what it did.
    //
    // So the markers are grouped by their class first, and ONE group becomes
    // the subtitle. Pairing happens inside a group, which also fixes the
    // boundary for free: the last Korean line no longer takes its end time from
    // the first English one, three hours earlier.
    private static List<Cue> ParseSami(string text, bool preferEnglish)
    {
        var matches = SamiSync.Matches(text);
        var byClass = new Dictionary<string, List<(double Start, string Body)>>(
            StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < matches.Count; i++)
        {
            double start = long.Parse(matches[i].Groups[1].Value, CultureInfo.InvariantCulture) / 1000.0;
            int bodyStart = matches[i].Index + matches[i].Length;
            int bodyEnd = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;
            string body = text[bodyStart..bodyEnd];

            // A file with no classes at all lands everything under "", which is
            // one group and therefore behaves exactly as it did before.
            var classMatch = SamiClass.Match(body);
            string track = classMatch.Success ? classMatch.Groups[1].Value : string.Empty;

            if (!byClass.TryGetValue(track, out var list))
            {
                list = new List<(double, string)>();
                byClass[track] = list;
            }
            list.Add((start, body));
        }

        if (byClass.Count == 0)
        {
            return new List<Cue>();
        }

        var chosen = ChooseTrack(byClass, preferEnglish);
        var cues = new List<Cue>(chosen.Count);
        for (int i = 0; i < chosen.Count; i++)
        {
            // The last line has no marker to end it; five seconds is what every
            // player assumes and nobody notices.
            double end = i + 1 < chosen.Count ? chosen[i + 1].Start : chosen[i].Start + 5;
            cues.Add(new Cue(chosen[i].Start, end, StripTags(chosen[i].Body)));
        }
        return cues;
    }

    // The app's own language decides, because nothing else in the file does: a
    // bilingual SAMI declares both tracks as equals and leaves the choice to
    // the player. Falling back to the LARGEST group rather than the first keeps
    // a file whose only track is tagged something unexpected from coming back
    // empty.
    //
    // (Switching tracks by hand is not offered - the panel previews a film, it
    // does not become a player - but the grouping above is what a switch would
    // need if that line ever moves.)
    private static List<(double Start, string Body)> ChooseTrack(
        Dictionary<string, List<(double Start, string Body)>> byClass, bool preferEnglish)
    {
        string[] wanted = preferEnglish ? EnglishClassPrefixes : KoreanClassPrefixes;
        var match = byClass.FirstOrDefault(pair => wanted.Any(prefix =>
            pair.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));
        if (match.Value is { Count: > 0 })
        {
            return match.Value;
        }

        return byClass.OrderByDescending(pair => pair.Value.Count).First().Value;
    }

    private static readonly Regex LineBreakTag = new(
        @"<br\s*/?>", RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
    private static readonly Regex AnyTag = new(
        @"<[^>]*>", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    // Both formats carry markup this panel does not render - SAMI's <P Class>
    // and SubRip's <i>/<b>/{\an8} - so it is taken out rather than shown as
    // literal angle brackets. <br> is the one tag that MEANS something here and
    // becomes the line break it stands for.
    private static string StripTags(string raw)
    {
        string text = LineBreakTag.Replace(raw, "\n");
        text = AnyTag.Replace(text, string.Empty);
        text = text
            .Replace("&nbsp;", " ", StringComparison.OrdinalIgnoreCase)
            .Replace("&lt;", "<", StringComparison.OrdinalIgnoreCase)
            .Replace("&gt;", ">", StringComparison.OrdinalIgnoreCase)
            .Replace("&quot;", "\"", StringComparison.OrdinalIgnoreCase)
            .Replace("&apos;", "'", StringComparison.OrdinalIgnoreCase)
            // Last, or it would turn the others' own ampersands back into
            // entities halfway through.
            .Replace("&amp;", "&", StringComparison.OrdinalIgnoreCase);

        // Per-line trim, then drop the blank ones: a cleared SAMI marker leaves
        // a run of whitespace and newlines that would otherwise draw as an empty
        // box on screen.
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0);
        return string.Join("\n", lines);
    }
}

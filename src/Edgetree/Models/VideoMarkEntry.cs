namespace SidebarExplorer.App.Models;

// One film's playback marks (see AppSettings.VideoMarks).
//
// Seconds rather than a formatted time: the readout's format is a display
// decision that has already changed once, and a stored string would have
// frozen it.
public class VideoMarkEntry
{
    public string Path { get; set; } = string.Empty;

    // Ascending, and de-duplicated within a second of each other - two marks a
    // frame apart would draw as one tick and read as a bug in the drawing.
    public List<double> Seconds { get; set; } = new();

    // Where playback last left off, written without being asked. It exists
    // because ↑↓ walk the folder while a film is playing (the user's own call)
    // and one mis-hit therefore throws away where you were - "잘못 누르면 참
    // 곤란한 상황" (2026-08-10). With this, the way back is to select the row
    // again.
    //
    // 0 means nothing to resume, which is also what the very start means, so
    // the two need not be told apart. Deliberately not written for a film that
    // barely began or that ran to the end - both would resume somewhere nobody
    // asked to be.
    public double Resume { get; set; }

    // Seconds to shift this film's subtitle by, + for later. PER FILM and not a
    // global setting, because that is what the number is: a subtitle file drifts
    // against the release it was timed for, so an offset that fixed one film
    // would be wrong on the next.
    //
    // It lives in this entry rather than in one of its own because the KEY is
    // the same - the video's path - and a second list keyed identically would
    // be two things to prune, two things to cap, and two chances to disagree.
    public double SubtitleOffset { get; set; }
}

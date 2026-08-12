using System.IO;
using System.Text.Json;
using SidebarExplorer.App.Models;

namespace SidebarExplorer.App.Services;

public class SettingsService
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Edgetree");

    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    // Pre-rebrand location (app was named SidebarExplorer) - kept here only so
    // Load() can pull an existing install's settings forward the first time
    // it runs under the new folder name, instead of that install looking like
    // its favorites/colors/overrides all got reset.
    private static readonly string OldSettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SidebarExplorer");

    private static readonly string OldSettingsPath = Path.Combine(OldSettingsDir, "settings.json");

    // ----- 손으로 고친 파일을 읽을 때 -------------------------------------------
    //
    // This file is meant to be openable in an editor - that is why every choice
    // in it is a word rather than a number - so it is read on terms that
    // forgive what hand-editing actually produces:
    //
    //   TRAILING COMMAS and COMMENTS, because both are what someone does while
    //   trying something out, and neither changes what the file says. Without
    //   these two, a comma left behind after deleting a line is not a mistake in
    //   one setting - it makes the WHOLE file unreadable.
    //
    //   CASE-INSENSITIVE names, because the failure otherwise is silent:
    //   "backgroundColorHex" is not an error, it is simply a property this build
    //   has never heard of, so the line is dropped and the colour quietly goes
    //   back to its default with nothing said.
    //
    // What is deliberately NOT forgiven is a value of the wrong TYPE. That is a
    // JsonException and lands in the recovery below, where the file is kept.
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true,
    };

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath) && File.Exists(OldSettingsPath))
            {
                Directory.CreateDirectory(SettingsDir);
                File.Copy(OldSettingsPath, SettingsPath);
            }

            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, ReadOptions);
                if (settings is not null)
                {
                    // Everything that arrives from outside goes through this -
                    // see AppSettings.Normalize for what it does and what it
                    // deliberately leaves to the use sites.
                    settings.Normalize();
                    return settings;
                }
            }
            else
            {
                // No settings file at either location: nobody has ever run this
                // app on this machine. That is the one case allowed to differ
                // from the plain defaults - see AppSettings.ForFirstRun.
                //
                // Deliberately NOT the fall-through below. A file that exists
                // but cannot be read or parsed lands there instead, and that is
                // an existing install having a bad day: it should come back
                // looking like the app it was, not like a new one.
                return AppSettings.ForFirstRun();
            }
        }
        catch (IOException) { }
        catch (JsonException)
        {
            // THE FILE IS KEPT BEFORE ANYTHING ELSE HAPPENS. Returning defaults
            // is not the loss - the loss is the first Save afterwards, which
            // writes those defaults straight over a file that still held every
            // favorite, colour, bookmark and mark the user ever made. Nothing
            // asks before that save; settings here are written the moment they
            // are clicked.
            //
            // So the unreadable file is copied aside first, and it keeps its
            // own name with the time on it: whatever went wrong, the data is
            // still on disk and can be put back a line at a time.
            KeepUnreadableFile();
        }

        return new AppSettings();
    }

    private static void KeepUnreadableFile()
    {
        try
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            File.Copy(SettingsPath, Path.Combine(SettingsDir, $"settings.broken-{stamp}.json"));
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    // WRITTEN BESIDE, THEN SWAPPED IN. File.WriteAllText truncates the real file
    // and then fills it, so a crash or a power cut in that window leaves a
    // half-written settings.json - which is the same total loss as a bad edit,
    // arrived at without anyone touching anything. This app writes settings on
    // every click, so that window is open often.
    //
    // File.Replace is the swap, and it keeps the previous contents as
    // settings.bak on the way through: one save back is recoverable for free.
    // Falls back to the plain write where Replace cannot work (a fresh install
    // has nothing to replace).
    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });

            if (!File.Exists(SettingsPath))
            {
                File.WriteAllText(SettingsPath, json);
                return;
            }

            string temp = SettingsPath + ".tmp";
            File.WriteAllText(temp, json);
            File.Replace(temp, SettingsPath, SettingsPath + ".bak", ignoreMetadataErrors: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

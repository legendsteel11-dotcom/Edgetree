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
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
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
        catch (JsonException) { }

        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch (IOException) { }
    }
}

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
                    // 한 번만 도는 것이라 Normalize 안이 아니라 그 뒤다 - 표식은
                    // 이 다음 저장에 실려 나간다.
                    settings.MergeFavoritesIntoBookmarks();
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
    // ----- 저장이 실패했을 때 ---------------------------------------------------
    //
    // 삼키기만 하던 자리다. 설정은 누를 때마다 저장되므로, 파일이 읽기 전용이거나
    // 다른 프로그램이 잡고 있으면 화면은 계속 바뀌는데 재시작하면 전부 돌아온다 -
    // 그 사이 아무 표시도 없어서, 앱이 설정을 안 지키는 것으로 보인다.
    //
    // 알리는 쪽은 여기가 아니다. 이 클래스는 창을 모르고, 무엇보다 저장은 클릭마다
    // 도는 것이라 실패도 클릭마다 난다 - 여기서 상자를 띄우면 상자가 쏟아진다.
    // 그래서 사실만 알리고, 한 번만 말할지 로그만 남길지는 받는 쪽이 정한다.
    public event Action<Exception>? SaveFailed;

    // 실패한 뒤 다시 성공한 그 순간에만 오른다. 성공할 때마다 알리면 클릭마다
    // 도는 이벤트가 하나 더 생기는 것이고, 받는 쪽이 알고 싶은 것은 성공이
    // 아니라 "막혀 있던 것이 풀렸다"는 전환뿐이다.
    public event Action? SaveRecovered;

    private bool _lastSaveFailed;

    // true면 디스크까지 갔다. 반환값을 안 받는 호출부가 대부분이고 그래도 되지만,
    // 저장을 확인해야 하는 자리(내보내기, 종료 직전)를 위해 남겨 둔다.
    public bool Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });

            if (!File.Exists(SettingsPath))
            {
                File.WriteAllText(SettingsPath, json);
                return NoteSaved();
            }

            string temp = SettingsPath + ".tmp";
            File.WriteAllText(temp, json);
            File.Replace(temp, SettingsPath, SettingsPath + ".bak", ignoreMetadataErrors: true);
            return NoteSaved();
        }
        catch (IOException ex)
        {
            return NoteFailed(ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            return NoteFailed(ex);
        }
    }

    private bool NoteSaved()
    {
        if (_lastSaveFailed)
        {
            _lastSaveFailed = false;
            SaveRecovered?.Invoke();
        }

        return true;
    }

    private bool NoteFailed(Exception error)
    {
        _lastSaveFailed = true;
        SaveFailed?.Invoke(error);
        return false;
    }

    // 어디에 쓰려고 했는지 말해 줄 수 있어야 한다 - 안내문에서 이 경로가 사실상
    // 유일하게 실행 가능한 정보다(잡고 있는 프로그램을 닫든, 읽기 전용을 풀든).
    public static string PathForMessages => SettingsPath;
}

using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using SidebarExplorer.App.Native;

namespace SidebarExplorer.App.Services;

public static class ShellFileService
{
    public static void OpenWithDefaultApp(string path)
    {
        NativeMethods.AllowNextWindowToActivate();

        // Goes through the same IContextMenu shortcut-menu-handler negotiation
        // Explorer's own double-click uses, instead of Process.Start's plain
        // ShellExecuteEx - some apps' single-instance handling (e.g. reusing an
        // already-open window) only responds correctly to that path.
        if (NativeMethods.TryOpenWithShellVerb(path))
        {
            return;
        }

        // No app is associated with this extension (common for developer file
        // types like .cs/.md), or the call otherwise failed. Explorer would show
        // the "How do you want to open this file?" picker in that case; do the
        // same instead of silently doing nothing.
        OpenWithPicker(path);
    }

    public static void OpenWithPicker(string path)
    {
        NativeMethods.AllowNextWindowToActivate();

        // "openas" is Explorer's own documented shell verb for the "How do you
        // want to open this?" picker, resolved through the same ShellExecuteEx
        // path as ShowProperties below - more reliable than the older
        // rundll32 shell32.dll,OpenAs_RunDLL trick, which several Windows 10/11
        // builds silently no-op depending on shell extension state.
        NativeMethods.TryOpenWithShellVerb(path, "openas");
    }

    public static void ShowProperties(string path)
    {
        NativeMethods.AllowNextWindowToActivate();
        NativeMethods.TryOpenWithShellVerb(path, "properties");
    }

    public static void OpenTerminal(string folderPath)
    {
        NativeMethods.AllowNextWindowToActivate();
        try
        {
            // Windows Terminal, if installed, is the nicer default; cmd.exe
            // always exists so it's a safe fallback if wt.exe isn't found.
            Process.Start(new ProcessStartInfo("wt.exe", $"-d \"{folderPath}\"") { UseShellExecute = true });
            return;
        }
        catch (Win32Exception) { }

        try
        {
            Process.Start(new ProcessStartInfo("cmd.exe") { UseShellExecute = true, WorkingDirectory = folderPath });
        }
        catch (Win32Exception) { }
    }

    public static void RevealInExplorer(string path)
    {
        NativeMethods.AllowNextWindowToActivate();
        try
        {
            // /select opens the *parent* folder with the item highlighted -
            // which is what "show me where this is" means for a file, but for a
            // folder it lands on the parent instead of opening the folder
            // itself. Passing a folder's own path plainly opens its contents.
            string arguments = Directory.Exists(path)
                ? $"\"{path}\""
                : $"/select,\"{path}\"";

            Process.Start(new ProcessStartInfo("explorer.exe", arguments) { UseShellExecute = true });
        }
        catch (Win32Exception) { }
    }
}

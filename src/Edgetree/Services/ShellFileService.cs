using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using SidebarExplorer.App.Native;

namespace SidebarExplorer.App.Services;

public static class ShellFileService
{
    // The classic Directory\shell\VSCode verb only exists for installs that
    // used the legacy installer's "Add to context menu" checkbox. Newer VS
    // Code builds instead register their Explorer "Open with Code" entry as
    // a modern sparse package (Microsoft.VisualStudioCode, no such registry
    // verb at all) - so neither install path reliably shows up under
    // Directory\shell. App Paths is the one registration VS Code's installer
    // writes unconditionally (Windows' standard "how do I run Code.exe"
    // lookup, used by ShellExecute("Code.exe") and the Run dialog alike),
    // so it's used here as the actual "is Code installed" signal instead.
    private const string CodeAppPathsKey = @"Software\Microsoft\Windows\CurrentVersion\App Paths\Code.exe";

    private static string? ResolveCodeExecutablePath()
    {
        string? path = Registry.CurrentUser.OpenSubKey(CodeAppPathsKey)?.GetValue(null) as string;
        path ??= Registry.LocalMachine.OpenSubKey(CodeAppPathsKey)?.GetValue(null) as string;
        return !string.IsNullOrEmpty(path) && File.Exists(path) ? path : null;
    }

    public static bool IsCodeRegistered() => ResolveCodeExecutablePath() is not null;

    public static void OpenWithCode(string path)
    {
        string? exePath = ResolveCodeExecutablePath();
        if (exePath is null)
        {
            return;
        }

        NativeMethods.AllowNextWindowToActivate();

        // UseShellExecute=false + ArgumentList so we can (a) hand the path to
        // Code.exe with correct quoting and (b) scrub ELECTRON_RUN_AS_NODE from
        // the child's environment. If Edgetree itself was started from a shell
        // that had that variable set, Code.exe would otherwise inherit it and
        // run as a bare Node interpreter on the path - failing to launch the
        // editor at all (opening nothing) rather than opening the file/folder.
        var startInfo = new ProcessStartInfo(exePath) { UseShellExecute = false };
        startInfo.ArgumentList.Add(path);
        startInfo.Environment.Remove("ELECTRON_RUN_AS_NODE");

        try
        {
            Process.Start(startInfo);
        }
        catch (Win32Exception) { }
    }


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

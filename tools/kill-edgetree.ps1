# Ends the running debug app so a build can replace its exe.
#
# Asks it to CLOSE first and only kills what refuses to go.
#
# That distinction used to be the difference between keeping and losing a day's
# settings: everything from the options menu was held in memory and written only
# on the way out, so a forced kill rolled it all back. It happened on 2026-07-23
# with colours and again on 2026-07-28 with option-menu settings, both times
# because a build killed the app mid-day. Since 2026-08-12 every options-menu
# toggle writes as it is clicked, so what a forced kill can still cost is the
# window's own geometry and the expanded/selected state - which MainWindow_Closing
# is the only thing that writes. Worth keeping the polite close for that alone.
#
# It also stamps exit.log either way, so a build's ending is never mistaken for
# the app disappearing on its own - which is the whole point of that log.

$dir = Join-Path $env:APPDATA 'Edgetree'
$log = Join-Path $dir 'exit.log'

function Write-ExitLog($text) {
    if (Test-Path $dir) {
        $stamp = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
        # AppendAllText, not Add-Content: PowerShell 5.1 would prepend a UTF-8
        # BOM into the middle of a file the app writes without one.
        [System.IO.File]::AppendAllText($log, "$stamp  $text`r`n")
    }
}

# Wildcard, not the exact name: release copies get renamed and would otherwise
# report as "not running" while holding the build's output file locked.
#
# The installer is the exception, and it had to be carved out by hand: it is
# called Edgetree-v1.5.0-win-x64-setup.exe, so the wildcard above matched it and
# a routine pre-build close went after Setup itself while it was mid-run
# (2026-08-06). Windows refused - it runs elevated - but the attempt is the
# problem: this script must never be able to interrupt an install, and it has no
# business touching anything but the app. Anything with "setup" in its name is
# not the app.
$isApp = { $_.ProcessName -like '*dgetree*' -and $_.ProcessName -notlike '*setup*' }
$procs = @(Get-Process | Where-Object $isApp)

if ($procs.Count -eq 0) {
    'not running'
    return
}

$ids = ($procs | ForEach-Object { $_.Id }) -join ', '
Write-ExitLog "build close requested (assistant) - pid $ids"

Add-Type -Name Win -Namespace KillEdgetree -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
[DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr p);
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
public delegate bool EnumProc(IntPtr h, IntPtr p);
'@ -ErrorAction SilentlyContinue

# WM_CLOSE to every top-level window the process owns. The main window is
# ShowInTaskbar=false, so MainWindowHandle reads as zero - enumerating is the
# only way to find it.
$targets = @($procs | ForEach-Object { $_.Id })
$windows = New-Object System.Collections.ArrayList
$callback = [KillEdgetree.Win+EnumProc] {
    param($hwnd, $lparam)
    $owner = 0
    [void][KillEdgetree.Win]::GetWindowThreadProcessId($hwnd, [ref]$owner)
    if ($targets -contains $owner) { [void]$windows.Add($hwnd) }
    return $true
}
[void][KillEdgetree.Win]::EnumWindows($callback, [IntPtr]::Zero)

foreach ($hwnd in $windows) {
    [void][KillEdgetree.Win]::PostMessage($hwnd, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero)
}

# Four seconds is generous for an app whose exit path is a settings write.
$deadline = 40
while ($deadline -gt 0 -and @(Get-Process | Where-Object $isApp).Count -gt 0) {
    Start-Sleep -Milliseconds 100
    $deadline--
}

$left = @(Get-Process | Where-Object $isApp)
if ($left.Count -eq 0) {
    "closed cleanly: pid $ids"
    return
}

# Only now, and said out loud: anything that got here didn't save its settings.
$stubborn = ($left | ForEach-Object { $_.Id }) -join ', '
Write-ExitLog "build kill forced (assistant) - pid $stubborn (did not close in time)"
$left | Stop-Process -Force
Start-Sleep -Milliseconds 700
"FORCED (settings since launch are lost): pid $stubborn"

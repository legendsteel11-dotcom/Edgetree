# Kills the running debug app the way a build needs to - but stamps exit.log
# first, so that kill is distinguishable from the app disappearing on its own.
#
# Without the stamp the two look identical (a forced kill runs no exit handler
# and writes nothing), which is why two "it was gone and I didn't close it"
# incidents went unanswered. Use this instead of Stop-Process before building.
#
# It also reports whether anything was actually running: "not running" is
# itself the answer on the morning after.

$dir = Join-Path $env:APPDATA 'Edgetree'
$log = Join-Path $dir 'exit.log'

# Wildcard, not the exact name: release copies get renamed and would otherwise
# report as "not running" while holding the build's output file locked.
$procs = @(Get-Process | Where-Object { $_.ProcessName -like '*dgetree*' })

if ($procs.Count -eq 0) {
    'not running'
    return
}

$ids = ($procs | ForEach-Object { $_.Id }) -join ', '

if (Test-Path $dir) {
    $stamp = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
    $line = "$stamp  build kill requested (assistant) - pid $ids`r`n"
    # AppendAllText, not Add-Content: PowerShell 5.1 would prepend a UTF-8 BOM
    # into the middle of a file the app writes without one.
    [System.IO.File]::AppendAllText($log, $line)
}

$procs | Stop-Process -Force
Start-Sleep -Milliseconds 700
"killed: pid $ids"

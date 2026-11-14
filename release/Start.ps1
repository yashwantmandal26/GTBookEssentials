$ErrorActionPreference = 'Stop'

function Send-ShowSignal {
    try {
        $signal = [Threading.EventWaitHandle]::OpenExisting('Local\GTBookEssentialsShow')
        try {
            [void]$signal.Set()
            return $true
        } finally {
            $signal.Dispose()
        }
    } catch {
        return $false
    }
}

# 1. If an instance is already running and ready, signal it and exit
if (Send-ShowSignal) { exit 0 }

# 2. Try starting via the scheduled task (for elevated background execution)
$startedViaTask = $false
try {
    $scheduler = New-Object -ComObject Schedule.Service
    $scheduler.Connect()
    $folder = $scheduler.GetFolder('\')
    $task = $folder.GetTask('GTBookStable')
    $expected = Join-Path $PSScriptRoot 'GTBook.exe'
    if ($task.Enabled -and [string]::Equals($task.Definition.Actions.Item(1).Path, $expected, [StringComparison]::OrdinalIgnoreCase)) {
        $taskHandle = $task.Run($null)
        $startedViaTask = $true
    }
} catch {} finally {
    foreach ($item in @($taskHandle, $task, $folder, $scheduler)) {
        if ($item -and [Runtime.InteropServices.Marshal]::IsComObject($item)) {
            [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($item)
        }
    }
}

if ($startedViaTask) {
    # Bounded handshake: poll for IPC readiness up to 3000ms
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.ElapsedMilliseconds -lt 3000) {
        if (Send-ShowSignal) { exit 0 }
        Start-Sleep -Milliseconds 50
    }
}

# 3. Fallback: launch GTBook.exe directly in foreground if task did not deliver
$exe = Join-Path $PSScriptRoot 'GTBook.exe'
if (Test-Path -LiteralPath $exe) {
    Start-Process -FilePath $exe -WorkingDirectory $PSScriptRoot
}

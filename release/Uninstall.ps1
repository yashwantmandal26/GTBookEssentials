$ErrorActionPreference='Stop'
$appRoot=Join-Path $env:ProgramFiles 'GTBOOK Essentials'
$stateFile=Join-Path $env:ProgramData 'GTBOOK Essentials\install-state.json'
$admin=([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if(-not $admin){$run=Start-Process powershell.exe -Verb RunAs -WindowStyle Hidden -ArgumentList ('-NoProfile -ExecutionPolicy Bypass -File "'+$PSCommandPath+'"') -Wait -PassThru;exit $run.ExitCode}
Add-Type -AssemblyName System.Windows.Forms
$installGate=$null;$gateOwned=$false
try {
    if([Windows.Forms.MessageBox]::Show('Remove GTBOOK Essentials and restore the previous OEM startup setting? Personal preferences and diagnostics will be retained.','GTBOOK Essentials','YesNo','Question') -ne 'Yes'){exit 0}
    # Verify the exact named Program Files target before any recursive removal.
    $resolved=[IO.Path]::GetFullPath($appRoot);$expected=[IO.Path]::GetFullPath((Join-Path $env:ProgramFiles 'GTBOOK Essentials'))
    if($resolved -ne $expected -or -not $resolved.StartsWith(([IO.Path]::GetFullPath($env:ProgramFiles)+'\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected uninstall directory.'}
    foreach($directory in @($resolved,(Join-Path $resolved 'assets'))){if(Test-Path -LiteralPath $directory){if((Get-Item -LiteralPath $directory -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Uninstall will not traverse a linked installation directory.'}}}
    $installGate=New-Object Threading.Mutex($false,'Global\GTBookEssentialsInstall');try{$gateOwned=$installGate.WaitOne(0)}catch [Threading.AbandonedMutexException]{$gateOwned=$true}
    if(-not $gateOwned){throw 'Another Essentials installer/uninstaller is already running.'}
    $state=$null;if(Test-Path -LiteralPath $stateFile){try{$state=Get-Content -LiteralPath $stateFile -Raw | ConvertFrom-Json}catch{}}
    try{$signal=[Threading.EventWaitHandle]::OpenExisting('Local\GTBookEssentialsExit');[void]$signal.Set();$signal.Dispose()}catch{}
    $deadline=[DateTime]::UtcNow.AddSeconds(15)
    do{$owned=@(Get-Process GTBook -ErrorAction SilentlyContinue | Where-Object {try{[string]::Equals($_.MainModule.FileName,(Join-Path $appRoot 'GTBook.exe'),[StringComparison]::OrdinalIgnoreCase)}catch{$false}});if($owned.Count -eq 0){break};Start-Sleep -Milliseconds 150}while([DateTime]::UtcNow -lt $deadline)
    if($owned.Count -gt 0){throw 'Exit Essentials from the tray and retry uninstall.'}
    $scheduler=New-Object -ComObject Schedule.Service;$scheduler.Connect();$folder=$scheduler.GetFolder('\')
    try{$task=$folder.GetTask('GTBookStable');if([string]::Equals($task.Definition.Actions.Item(1).Path,(Join-Path $appRoot 'GTBook.exe'),[StringComparison]::OrdinalIgnoreCase)){$folder.DeleteTask('GTBookStable',0)}}catch{}
    if($state){try{$original=$folder.GetTask('ControlCenter');if([string]::Equals($original.Definition.Actions.Item(1).Path,$state.OriginalPath,[StringComparison]::OrdinalIgnoreCase)){$original.Enabled=[bool]$state.OriginalEnabled}}catch{}}
    $menu=Join-Path ([Environment]::GetFolderPath('CommonPrograms')) 'GTBOOK Essentials'
    foreach($name in @('GTBOOK Essentials.lnk','Uninstall GTBOOK Essentials.lnk')){if(Test-Path -LiteralPath (Join-Path $menu $name)){Remove-Item -LiteralPath (Join-Path $menu $name) -Force}}
    if(Test-Path -LiteralPath $menu){if(@(Get-ChildItem -LiteralPath $menu -Force).Count -eq 0){Remove-Item -LiteralPath $menu}}
    Remove-Item -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\GTBOOK Essentials' -ErrorAction SilentlyContinue
    Set-Location -LiteralPath $env:SystemRoot;[Environment]::CurrentDirectory=$env:SystemRoot
    # Remove only files installed by this package; retain any user-added files.
    foreach($relative in @('GTBook.exe','Start.bat','Start.ps1','Uninstall.ps1','assets\gtbook-essentials.ico')){$file=Join-Path $resolved $relative;if(Test-Path -LiteralPath $file){Remove-Item -LiteralPath $file -Force}}
    foreach($directory in @((Join-Path $resolved 'assets'),$resolved)){if(Test-Path -LiteralPath $directory){if(@(Get-ChildItem -LiteralPath $directory -Force).Count -eq 0){Remove-Item -LiteralPath $directory}}}
    if(Test-Path -LiteralPath $stateFile){Remove-Item -LiteralPath $stateFile -Force}
    [Windows.Forms.MessageBox]::Show('Essentials removed. Personal settings and diagnostics were kept.','GTBOOK Essentials') | Out-Null
}catch{[Windows.Forms.MessageBox]::Show($_.Exception.Message,'GTBOOK Essentials','OK','Error') | Out-Null;exit 1}
finally{if($installGate){if($gateOwned){$installGate.ReleaseMutex()};$installGate.Dispose()};foreach($item in @($original,$task,$folder,$scheduler)){if($item -and [Runtime.InteropServices.Marshal]::IsComObject($item)){[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($item)}}}

param([switch]$VerifyGpu,[switch]$VerifyUsb,[switch]$CheckOnly,[string]$OriginalSid)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$appRoot=Join-Path $env:ProgramFiles 'GTBOOK Essentials'
$stateRoot=Join-Path $env:ProgramData 'GTBOOK Essentials'
$stateFile=Join-Path $stateRoot 'install-state.json'
$taskName='GTBookStable'
$currentSid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$installGate=$null;$gateOwned=$false
if(-not $OriginalSid){$OriginalSid=$currentSid}
if($OriginalSid -notmatch '^S-1-5-21-[0-9-]+$'){throw 'Invalid interactive user identity.'}
function Show-Result([string]$message,[bool]$failure=$false){Add-Type -AssemblyName System.Windows.Forms;[Windows.Forms.MessageBox]::Show($message,'GTBOOK Essentials','OK',$(if($failure){'Error'}else{'Information'})) | Out-Null}
function Release-Com($value){if($null -ne $value -and [Runtime.InteropServices.Marshal]::IsComObject($value)){[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($value)}}
function Write-Atomic([string]$target,[byte[]]$bytes){
    $temporary=$target+'.'+[guid]::NewGuid().ToString('N')+'.tmp';$previous=$target+'.replaced-'+[guid]::NewGuid().ToString('N')+'.bak'
    try{ $stream=[IO.File]::Open($temporary,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None);try{$stream.Write($bytes,0,$bytes.Length);$stream.Flush($true)}finally{$stream.Dispose()};if(Test-Path -LiteralPath $target){[IO.File]::Replace($temporary,$target,$previous,$true)}else{[IO.File]::Move($temporary,$target)} }
    finally{foreach($file in @($temporary,$previous)){if(Test-Path -LiteralPath $file){Remove-Item -LiteralPath $file -Force}}}
}
function Stop-Essentials {
    try {$signal=[Threading.EventWaitHandle]::OpenExisting('Local\GTBookEssentialsExit');[void]$signal.Set();$signal.Dispose()}catch{}
    $deadline=[DateTime]::UtcNow.AddSeconds(15)
    do {
        $owned=@(Get-Process -Name GTBook -ErrorAction SilentlyContinue | Where-Object {try{$_.MainModule.FileVersionInfo.ProductName -eq 'GTBOOK Essentials'}catch{$false}})
        if($owned.Count -eq 0){return}
        foreach($process in $owned){if($process.MainWindowHandle -ne 0){[void]$process.CloseMainWindow()}}
        Start-Sleep -Milliseconds 150
    }while([DateTime]::UtcNow -lt $deadline)
    throw 'Essentials is still busy. Exit it from the tray, then run Install again. No process was force-terminated.'
}
try {
    if(-not [Environment]::Is64BitOperatingSystem){throw 'This build requires 64-bit Windows.'}
    $framework=(Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -Name Release).Release
    if($framework -lt 528040){throw '.NET Framework 4.8 or newer is required.'}
    $computer=Get-CimInstance Win32_ComputerSystem;$bios=Get-CimInstance Win32_BIOS
    $supported=$computer.Manufacturer -match 'INFINIX' -and $computer.Model -ieq 'GL613' -and $bios.SMBIOSBIOSVersion.Trim() -eq '1.00'
    if($CheckOnly){[pscustomobject]@{Model=$computer.Model;Bios=$bios.SMBIOSBIOSVersion;NativeControlsReviewed=$supported;InstallDirectory=$appRoot;Startup='Interactive sign-in, no artificial delay, battery allowed'} | ConvertTo-Json;exit 0}
    $admin=([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if(-not $admin){
        $arguments='-NoProfile -ExecutionPolicy Bypass -File "'+$PSCommandPath+'" -OriginalSid "'+$OriginalSid+'"'+$(if($VerifyGpu){' -VerifyGpu'}else{''})+$(if($VerifyUsb){' -VerifyUsb'}else{''})
        $elevated=Start-Process -FilePath powershell.exe -Verb RunAs -WindowStyle Hidden -ArgumentList $arguments -Wait -PassThru;exit $elevated.ExitCode
    }
    if($OriginalSid -ne $currentSid){throw 'Install using the same Windows account that will run Essentials. Administrator consent may be approved, but switching to a different administrator account is not supported by this per-user setup.'}
    $installGate=New-Object Threading.Mutex($false,'Global\GTBookEssentialsInstall')
    try{$gateOwned=$installGate.WaitOne(0)}catch [Threading.AbandonedMutexException]{$gateOwned=$true}
    if(-not $gateOwned){throw 'Another Essentials installer/uninstaller is already running.'}
    $manifestFile=Join-Path $PSScriptRoot 'SHA256SUMS.json'
    if(-not (Test-Path -LiteralPath $manifestFile)){throw 'Release manifest missing. Extract the complete release ZIP before installing.'}
    $manifest=Get-Content -LiteralPath $manifestFile -Raw | ConvertFrom-Json
    foreach($entry in $manifest.PSObject.Properties){
        $path=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot $entry.Name))
        if(-not $path.StartsWith(([IO.Path]::GetFullPath($PSScriptRoot)+[IO.Path]::DirectorySeparatorChar),[StringComparison]::OrdinalIgnoreCase)){throw 'Invalid release manifest path.'}
        if((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.Value){throw ('Release checksum mismatch: '+$entry.Name)}
    }
    . (Join-Path $PSScriptRoot 'DeviceSupport.ps1')
    $deviceSupport=Resolve-GTBookDeviceSupport $supported
    if($supported -and -not $deviceSupport.Ready){
        Add-Type -AssemblyName System.Windows.Forms
        $message="Device controls are not ready ($($deviceSupport.Status)). Windows device discovery could not restore the required firmware interface.`n`nYes: open official driver support and stop setup.`nNo: install Essentials with device controls unavailable.`nCancel: stop setup."
        $choice=[Windows.Forms.MessageBox]::Show($message,'GTBOOK Essentials - device support','YesNoCancel','Warning','Button2')
        if($choice -eq 'Yes'){Start-Process 'https://www.carlcare.com/global/drivers-download/';exit 2}
        if($choice -eq 'Cancel'){exit 2}
    }
    Stop-Essentials
    New-Item -ItemType Directory -Path $stateRoot -Force | Out-Null
    $backup=Join-Path $stateRoot ('backup-'+(Get-Date -Format yyyyMMdd-HHmmss)+'-'+[guid]::NewGuid().ToString('N').Substring(0,6));New-Item -ItemType Directory -Path $backup | Out-Null
    $service=New-Object -ComObject Schedule.Service;$service.Connect();$folder=$service.GetFolder('\')
    $oldTask=$null;$original=$null;$oldXml=$null;$oldOriginalEnabled=$false
    try{$oldTask=$folder.GetTask($taskName);$oldXml=$oldTask.Xml}catch{}
    try{$original=$folder.GetTask('ControlCenter');$oldOriginalEnabled=$original.Enabled}catch{}
    $state=[ordered]@{InstallDirectory=$appRoot;UserSid=$OriginalSid;OriginalPath=$null;OriginalEnabled=$oldOriginalEnabled;InstalledAt=(Get-Date).ToString('o')}
    if($original){$state.OriginalPath=$original.Definition.Actions.Item(1).Path}
    $stateExisted=Test-Path -LiteralPath $stateFile
    if($stateExisted){
        $prior=Get-Content -LiteralPath $stateFile -Raw | ConvertFrom-Json
        $state.OriginalEnabled=$prior.OriginalEnabled
        $state.OriginalPath=$prior.OriginalPath
        Copy-Item -LiteralPath $stateFile -Destination (Join-Path $backup 'install-state.json') -Force
    }
    if($oldXml){[IO.File]::WriteAllText((Join-Path $backup 'previous-task.xml'),$oldXml)}
    $files=@('GTBook.exe','Start.ps1','Start.bat','Uninstall.ps1','assets\gtbook-essentials.ico')
    $existed=@{}
    foreach($relative in $files){$target=Join-Path $appRoot $relative;$existed[$relative]=Test-Path -LiteralPath $target;if($existed[$relative]){$copy=Join-Path $backup $relative;New-Item -ItemType Directory -Path (Split-Path $copy -Parent) -Force | Out-Null;Copy-Item -LiteralPath $target -Destination $copy}}
    $menu=Join-Path ([Environment]::GetFolderPath('CommonPrograms')) 'GTBOOK Essentials'
    $menuExisted=Test-Path -LiteralPath $menu
    $shortcutMain=Join-Path $menu 'GTBOOK Essentials.lnk'
    $shortcutMainExisted=Test-Path -LiteralPath $shortcutMain
    if($shortcutMainExisted){Copy-Item -LiteralPath $shortcutMain -Destination (Join-Path $backup 'GTBOOK Essentials.lnk') -Force}
    $shortcutUninst=Join-Path $menu 'Uninstall GTBOOK Essentials.lnk'
    $shortcutUninstExisted=Test-Path -LiteralPath $shortcutUninst
    if($shortcutUninstExisted){Copy-Item -LiteralPath $shortcutUninst -Destination (Join-Path $backup 'Uninstall GTBOOK Essentials.lnk') -Force}
    $key='HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\GTBOOK Essentials'
    $regKeyExisted=Test-Path -Path $key
    $regBackup=@{}
    if($regKeyExisted){
        try{
            $regItem=Get-ItemProperty -Path $key
            foreach($p in @('DisplayName','DisplayVersion','DisplayIcon','UninstallString','NoModify')){
                if($regItem.PSObject.Properties[$p]){$regBackup[$p]=$regItem.$p}
            }
        }catch{}
    }
    try {
        foreach($relative in $files){$target=Join-Path $appRoot $relative;New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null;Write-Atomic $target ([IO.File]::ReadAllBytes((Join-Path $PSScriptRoot $relative)))}
        $exe=Join-Path $appRoot 'GTBook.exe'
        if($VerifyGpu){$test=Start-Process -FilePath $exe -ArgumentList '--verify-gpu' -WindowStyle Hidden -Wait -PassThru;if($test.ExitCode -ne 0){throw 'GPU request verification failed. The original request was restored where possible. See LocalAppData\GTBookStable\gpu-verification.json.'}}
        if($VerifyUsb){$test=Start-Process -FilePath $exe -ArgumentList '--verify-usb' -WindowStyle Hidden -Wait -PassThru;if($test.ExitCode -ne 0){throw 'USB charging read-back verification failed. See LocalAppData\GTBookStable\usb-verification.json for the original-setting restoration result.'}}
        $setup=Start-Process -FilePath $exe -ArgumentList '--configure-startup','--no-run' -WindowStyle Hidden -Wait -PassThru
        if($setup.ExitCode -ne 0){throw 'Windows startup registration failed. Previous files and startup state will be restored.'}
        $registered=$folder.GetTask($taskName)
        if(-not $registered.Enabled -or $registered.Definition.Actions.Item(1).Path -ne $exe){throw 'Startup task read-back failed.'}
        Release-Com $registered
        $state.Version=(Get-Item -LiteralPath $exe).VersionInfo.FileVersion
        Write-Atomic $stateFile ([Text.Encoding]::UTF8.GetBytes(($state | ConvertTo-Json)))
        New-Item -ItemType Directory -Path $menu -Force | Out-Null
        $shell=New-Object -ComObject WScript.Shell
        $shortcut=$shell.CreateShortcut($shortcutMain);$shortcut.TargetPath=Join-Path $appRoot 'Start.bat';$shortcut.WorkingDirectory=$appRoot;$shortcut.IconLocation=Join-Path $appRoot 'assets\gtbook-essentials.ico';$shortcut.WindowStyle=7;$shortcut.Save();Release-Com $shortcut
        $shortcut=$shell.CreateShortcut($shortcutUninst);$shortcut.TargetPath=Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe';$shortcut.Arguments='-NoProfile -ExecutionPolicy Bypass -File "'+(Join-Path $appRoot 'Uninstall.ps1')+'"';$shortcut.WindowStyle=7;$shortcut.Save();Release-Com $shortcut;Release-Com $shell
        New-Item -Path $key -Force | Out-Null
        New-ItemProperty -Path $key -Name DisplayName -Value 'GTBOOK Essentials' -PropertyType String -Force | Out-Null
        New-ItemProperty -Path $key -Name DisplayVersion -Value $state.Version -PropertyType String -Force | Out-Null
        New-ItemProperty -Path $key -Name DisplayIcon -Value $exe -PropertyType String -Force | Out-Null
        New-ItemProperty -Path $key -Name UninstallString -Value ('powershell.exe -NoProfile -ExecutionPolicy Bypass -File "'+(Join-Path $appRoot 'Uninstall.ps1')+'"') -PropertyType String -Force | Out-Null
        New-ItemProperty -Path $key -Name NoModify -Value 1 -PropertyType DWord -Force | Out-Null
        $run=$folder.GetTask($taskName);$started=$run.Run($null);Release-Com $started;Release-Com $run
    }catch {
        foreach($relative in $files){
            try{
                $target=Join-Path $appRoot $relative
                if($existed[$relative]){Write-Atomic $target ([IO.File]::ReadAllBytes((Join-Path $backup $relative)))}
                else{if(Test-Path -LiteralPath $target){Remove-Item -LiteralPath $target -Force}}
            }catch{}
        }
        try{
            if($stateExisted){Write-Atomic $stateFile ([IO.File]::ReadAllBytes((Join-Path $backup 'install-state.json')))}
            else{if(Test-Path -LiteralPath $stateFile){Remove-Item -LiteralPath $stateFile -Force}}
        }catch{}
        try{
            if($shortcutMainExisted){Copy-Item -LiteralPath (Join-Path $backup 'GTBOOK Essentials.lnk') -Destination $shortcutMain -Force}
            else{if(Test-Path -LiteralPath $shortcutMain){Remove-Item -LiteralPath $shortcutMain -Force}}
            if($shortcutUninstExisted){Copy-Item -LiteralPath (Join-Path $backup 'Uninstall GTBOOK Essentials.lnk') -Destination $shortcutUninst -Force}
            else{if(Test-Path -LiteralPath $shortcutUninst){Remove-Item -LiteralPath $shortcutUninst -Force}}
            if(-not $menuExisted -and (Test-Path -LiteralPath $menu)){
                if(@(Get-ChildItem -LiteralPath $menu).Count -eq 0){Remove-Item -LiteralPath $menu -Force}
            }
        }catch{}
        try{
            if($regKeyExisted){
                foreach($p in $regBackup.Keys){New-ItemProperty -Path $key -Name $p -Value $regBackup[$p] -Force | Out-Null}
            }else{
                if(Test-Path -Path $key){Remove-Item -Path $key -Recurse -Force}
            }
        }catch{}
        try{
            if($oldXml){$restored=$folder.RegisterTask($taskName,$oldXml,6,$OriginalSid,$null,3,$null);Release-Com $restored}
            else{try{$folder.DeleteTask($taskName,0)}catch{}}
        }catch{}
        try{
            if($original){$original.Enabled=$oldOriginalEnabled}
        }catch{}
        throw
    }finally{Release-Com $oldTask;Release-Com $original;Release-Com $folder;Release-Com $service}
    Show-Result ('Installed GTBOOK Essentials '+$state.Version+'. It starts in the tray automatically at Windows sign-in.'+$(if($supported -and -not $deviceSupport.Ready){"`nDevice controls are unavailable. See GitHub docs/DRIVER_SUPPORT.md for recovery steps."}elseif(-not $supported){"`nThis model/BIOS is not yet reviewed; hardware writes stay disabled."}else{''}))
}catch{if($CheckOnly){Write-Error $_;exit 1};Show-Result $_.Exception.Message $true;exit 1}
finally{if($installGate){if($gateOwned){$installGate.ReleaseMutex()};$installGate.Dispose()}}

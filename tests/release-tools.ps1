$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$scripts=@(Get-ChildItem -LiteralPath (Join-Path $root 'release') -Filter '*.ps1')+@(Get-Item -LiteralPath (Join-Path $root 'Build-Release.ps1'))
foreach($script in $scripts){$tokens=$null;$issues=$null;[void][Management.Automation.Language.Parser]::ParseFile($script.FullName,[ref]$tokens,[ref]$issues);if($issues.Count){throw ($issues.Message -join '; ')}}
$tokens=$null;$issues=$null;$ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $root 'release\Install.ps1'),[ref]$tokens,[ref]$issues)
$atomic=$ast.Find({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Write-Atomic'},$true)
if(-not $atomic){throw 'Installer atomic writer not found.'}
. ([ScriptBlock]::Create($atomic.Extent.Text))
$fixtureRoot=Join-Path $PSScriptRoot ('installer-fixtures\'+[guid]::NewGuid().ToString('N'));New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null
$fixture=Join-Path $fixtureRoot 'binary.bin'
Write-Atomic $fixture ([Text.Encoding]::UTF8.GetBytes('first'))
Write-Atomic $fixture ([Text.Encoding]::UTF8.GetBytes('second'))
if([IO.File]::ReadAllText($fixture) -ne 'second'){throw 'Installer failed to atomically replace an existing file.'}
if(@(Get-ChildItem -LiteralPath $fixtureRoot -File).Count -ne 1){throw 'Installer left transaction files behind.'}
$gate=New-Object Threading.Mutex($false,('Local\GTBookInstallerTest-'+[guid]::NewGuid().ToString('N')))
try{if(-not $gate.WaitOne(0)){throw 'Installer gate failed.'};$gate.ReleaseMutex()}finally{$gate.Dispose()}
. (Join-Path $root 'release\DeviceSupport.ps1')
$script:supportChecks=0;$script:supportScans=0
$ready=Resolve-GTBookDeviceSupport $true -Probe {param($supported) [pscustomobject]@{Ready=$true;Status='Ready'}} -Scan {$script:supportScans++}
if(-not $ready.Ready -or $script:supportScans -ne 0){throw 'Healthy devices must not be rescanned.'}
$fixed=Resolve-GTBookDeviceSupport $true -Probe {param($supported) $script:supportChecks++;[pscustomobject]@{Ready=($script:supportChecks -gt 1);Status='ProviderUnavailable'}} -Scan {$script:supportScans++}
if(-not $fixed.Ready -or $script:supportScans -ne 1){throw 'Missing interface must be rechecked after one rescan.'}
foreach($status in @('UnsupportedProfile','AccessDenied','InterfaceMismatch')){
    $notReady=Resolve-GTBookDeviceSupport ($status -ne 'UnsupportedProfile') -Probe {param($supported) [pscustomobject]@{Ready=$false;Status=$status}} -Scan {$script:supportScans++}
    if($notReady.Ready -or $script:supportScans -ne 1){throw 'Unsupported/mismatched/denied devices must not be repaired blindly.'}
}
$failed=Resolve-GTBookDeviceSupport $true -Probe {param($supported) [pscustomobject]@{Ready=$false;Status='ProviderUnavailable'}} -Scan {throw 'simulated scan failure'}
if($failed.Ready){throw 'Failed recovery must not claim working device controls.'}
'Release tooling passed: script parsing, atomic writes, installer gate and device-support recovery states. No drivers were changed by tests.'

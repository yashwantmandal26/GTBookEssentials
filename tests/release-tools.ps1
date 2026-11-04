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
'Release tooling passed: script parsing, new/upgrade atomic writes, transaction cleanup, installer gate.'

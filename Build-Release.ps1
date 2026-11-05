param([switch]$SkipTests)
$ErrorActionPreference='Stop'
$version='2.0.0-rc.13'
if(-not $SkipTests){& (Join-Path $PSScriptRoot 'tests\run.ps1');if(-not $?){throw 'Release checks failed.'}}
& (Join-Path $PSScriptRoot 'tests\release-tools.ps1')
$out=Join-Path $PSScriptRoot 'out';New-Item -ItemType Directory -Path $out -Force | Out-Null
$stage=Join-Path $out ('stage-'+[guid]::NewGuid().ToString('N'));New-Item -ItemType Directory -Path $stage | Out-Null
$name='GTBOOK-Essentials-'+$version+'-win-x64';$binary=Join-Path $stage $name;New-Item -ItemType Directory -Path $binary | Out-Null
& (Join-Path $PSScriptRoot 'build.ps1') -OutputPath (Join-Path $binary 'GTBook.exe')
if(-not $?){throw 'Release compilation failed.'}
foreach($file in @('Install.ps1','DeviceSupport.ps1','Install.bat','Install_With_GPU_Check.bat','Start.ps1','Start.bat','Uninstall.ps1')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('release\'+$file)) -Destination $binary}
New-Item -ItemType Directory -Path (Join-Path $binary 'assets') | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'assets\gtbook-essentials.ico') -Destination (Join-Path $binary 'assets')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs\RELEASE_README.md') -Destination (Join-Path $binary 'README.md')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs\VALIDATION.md') -Destination $binary
$manifest=[ordered]@{}
Get-ChildItem -LiteralPath $binary -File -Recurse | Sort-Object FullName | ForEach-Object {$relative=$_.FullName.Substring($binary.Length+1).Replace('\','/');$manifest[$relative]=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $binary 'SHA256SUMS.json') -Encoding UTF8
$zip=Join-Path $out ($name+'.zip');Compress-Archive -LiteralPath $binary -DestinationPath $zip -Force
$source=Join-Path $stage ('GTBOOK-Essentials-'+$version+'-source');New-Item -ItemType Directory -Path $source | Out-Null
foreach($directory in @('src','assets','release','docs','.github')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $directory) -Destination $source -Recurse}
New-Item -ItemType Directory -Path (Join-Path $source 'tests') | Out-Null
foreach($testScript in @('run.ps1','release-tools.ps1')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('tests\'+$testScript)) -Destination (Join-Path $source 'tests')}
foreach($file in @('build.ps1','Build-Release.ps1','Build-Setup.ps1','app.manifest','.gitignore')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $source}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs\RELEASE_README.md') -Destination (Join-Path $source 'README.md')
$sourceZip=Join-Path $out ('GTBOOK-Essentials-'+$version+'-source.zip');Compress-Archive -LiteralPath $source -DestinationPath $sourceZip -Force
$setup=Join-Path $out 'GTBOOK-Essentials-Setup.exe'
& (Join-Path $PSScriptRoot 'Build-Setup.ps1') -PackageZip $zip -OutputPath $setup
if(-not $?){throw 'Setup compilation failed.'}
foreach($artifact in @($zip,$sourceZip,$setup)){((Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash+'  '+[IO.Path]::GetFileName($artifact)) | Set-Content -LiteralPath ($artifact+'.sha256') -Encoding ASCII}
[pscustomobject]@{Version=$version;InstallerDirectory=$binary;BinaryZip=$zip;SourceZip=$sourceZip;SetupExe=$setup} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $out 'latest.json') -Encoding UTF8
Get-Content -LiteralPath (Join-Path $out 'latest.json')

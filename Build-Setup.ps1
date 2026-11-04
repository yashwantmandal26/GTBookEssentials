param([Parameter(Mandatory=$true)][string]$PackageZip,[Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$package=[IO.Path]::GetFullPath($PackageZip)
$output=[IO.Path]::GetFullPath($OutputPath)
if(-not (Test-Path -LiteralPath $package)){throw 'Build the complete win-x64 ZIP first.'}
& $compiler /nologo /target:winexe /platform:x64 /optimize+ "/out:$output" "/win32icon:$PSScriptRoot\assets\gtbook-essentials.ico" "/resource:$package,GTBook.Package" /reference:System.Windows.Forms.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll (Join-Path $PSScriptRoot 'release\SetupWrapper.cs')
if($LASTEXITCODE -ne 0){throw 'Setup compilation failed.'}
Write-Host "Built $output"

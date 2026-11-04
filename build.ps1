param([string]$OutputPath)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.x (64-bit) compiler was not found.' }
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | Where-Object Name -ne 'Tests.cs' | ForEach-Object FullName)
$output = if($OutputPath){[IO.Path]::GetFullPath($OutputPath)}else{Join-Path $PSScriptRoot 'GTBook.Next.exe'}
New-Item -ItemType Directory -Path (Split-Path $output -Parent) -Force | Out-Null
& $compiler /nologo /target:winexe /platform:x64 /optimize+ "/out:$output" "/win32manifest:$PSScriptRoot\app.manifest" "/win32icon:$PSScriptRoot\assets\gtbook-essentials.ico" "/resource:$PSScriptRoot\assets\gtbook-essentials.ico,GTBookEssentials.Icon" "/resource:$PSScriptRoot\assets\gtbook-essentials-256.png,GTBookEssentials.Logo" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Management.dll /reference:System.Web.Extensions.dll /reference:Microsoft.CSharp.dll $sources
if ($LASTEXITCODE -ne 0) { throw 'GTBook build failed.' }
if(-not $OutputPath){try { Move-Item -LiteralPath $output -Destination (Join-Path $PSScriptRoot 'GTBook.exe') -Force; $output = Join-Path $PSScriptRoot 'GTBook.exe' } catch { Write-Host 'The open app is using GTBook.exe. Close it, then launch again to install this update.' }}
Write-Host "Built $output"

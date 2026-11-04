$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object FullName)
$testExe = Join-Path $PSScriptRoot 'GTBook.Tests.exe'
& $compiler /nologo /define:TESTING /target:exe /platform:x64 "/out:$testExe" "/win32icon:$root\assets\gtbook-essentials.ico" "/resource:$root\assets\gtbook-essentials.ico,GTBookEssentials.Icon" "/resource:$root\assets\gtbook-essentials-256.png,GTBookEssentials.Logo" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Management.dll /reference:System.Web.Extensions.dll /reference:Microsoft.CSharp.dll $sources
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
& $testExe --self-test (Join-Path $PSScriptRoot 'results.txt')
if ($LASTEXITCODE -ne 0) { Get-Content (Join-Path $PSScriptRoot 'results.txt'); throw 'Tests failed.' }
Get-Content (Join-Path $PSScriptRoot 'results.txt')

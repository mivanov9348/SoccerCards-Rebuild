<#
  Builds the desktop version of Dugout Deck for Windows.
  Result: dist\DugoutDeck-win-x64\DugoutDeck.exe (folder) and dist\DugoutDeck-win-x64.zip
  The folder is what you upload to Steam / itch.io. It contains the .NET runtime, so players need nothing else
  (the WebView2 runtime is already part of Windows 10/11).

  Usage (from the repository root):  powershell -ExecutionPolicy Bypass -File scripts\publish-desktop.ps1
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root 'dist\DugoutDeck-win-x64'
$zip = Join-Path $root 'dist\DugoutDeck-win-x64.zip'

Write-Host '1/3 Building the React client...' -ForegroundColor Cyan
Push-Location (Join-Path $root 'client')
npm install --no-audit --no-fund
if ($LASTEXITCODE -ne 0) { throw 'npm install failed' }
npm run build
if ($LASTEXITCODE -ne 0) { throw 'client build failed' }
Pop-Location

Write-Host '2/3 Publishing the desktop app...' -ForegroundColor Cyan
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
dotnet publish (Join-Path $root 'server\DugoutDeck.Desktop') -c Release -r win-x64 --self-contained true -p:DebugType=None -o $out
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

Write-Host '3/3 Creating the zip...' -ForegroundColor Cyan
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip

$size = [Math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host "Done: $out\DugoutDeck.exe  (zip: $size MB)" -ForegroundColor Green

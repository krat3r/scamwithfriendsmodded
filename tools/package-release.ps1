# Builds the mod and packs it, with BepInEx, into a single Windows installer:
#   dist\Hotline-Setup.exe              one-click installer (everything embedded)
#   dist\Hotline-<ver>.zip    the plugin folder, for manual installs
# Needs the .NET 8 SDK and the game installed (the mod compiles against its DLLs).
# Usage: tools\package-release.ps1 [-GameDir "D:\SteamLibrary\steamapps\common\Scam With Your Friends"]
param([string]$GameDir)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if ($GameDir) { $env:SWYF_GAME_DIR = $GameDir }

$bepinexVersion = '5.4.23.5'
$bepinexUrl = "https://github.com/BepInEx/BepInEx/releases/download/v$bepinexVersion/BepInEx_win_x64_$bepinexVersion.zip"
$bepinexSha256 = '82F9878551030F54657792C0740D9D51A09500EEAE1FBA21106B0C441E6732C4'

$payload = Join-Path $root 'installer\payload'
$dist = Join-Path $root 'dist'
$modOut = Join-Path $root 'src\Hotline\bin\Release'
$version = ([xml](Get-Content (Join-Path $root 'src\Hotline\Hotline.csproj'))).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1

Write-Host "Building mod $version..."
if (Test-Path $modOut) { Remove-Item $modOut -Recurse -Force }
dotnet build (Join-Path $root 'src\Hotline') -c Release -p:DeployToGame=false -nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Mod build failed.' }

New-Item -ItemType Directory -Force -Path $payload, $dist | Out-Null
$modZip = Join-Path $payload 'mod.zip'
if (Test-Path $modZip) { Remove-Item $modZip }
Compress-Archive -Path (Join-Path $modOut 'Hotline.dll'), (Join-Path $modOut 'Hotline.pdb'), (Join-Path $modOut 'ScamPacks') -DestinationPath $modZip
Set-Content -Path (Join-Path $payload 'version.txt') -Value $version -NoNewline

$bepinexZip = Join-Path $payload 'BepInEx.zip'
if (-not (Test-Path $bepinexZip) -or (Get-FileHash $bepinexZip -Algorithm SHA256).Hash -ne $bepinexSha256) {
    Write-Host "Downloading BepInEx $bepinexVersion..."
    Invoke-WebRequest -Uri $bepinexUrl -OutFile $bepinexZip
}
if ((Get-FileHash $bepinexZip -Algorithm SHA256).Hash -ne $bepinexSha256) { throw 'BepInEx download hash mismatch.' }

Write-Host 'Publishing installer...'
$publish = Join-Path $root 'installer\bin\publish'
dotnet publish (Join-Path $root 'installer') -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true -p:PublishTrimmed=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $publish -nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Installer publish failed.' }
Copy-Item (Join-Path $publish 'Hotline-Setup.exe') $dist -Force
Copy-Item $modZip (Join-Path $dist "Hotline-$version.zip") -Force

Write-Host "Done:"
Get-ChildItem $dist

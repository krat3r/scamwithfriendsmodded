# Decompiles the game's code into ./decompiled (gitignored) so you can find classes to hook.
# Usage: tools\decompile.ps1 [-GameDir "D:\SteamLibrary\steamapps\common\Scam With Your Friends"]
# Needs the .NET 8 SDK (ilspycmd is pinned to a version that runs on it).
param([string]$GameDir = $env:SWYF_GAME_DIR)
$ErrorActionPreference = 'Stop'
if (-not $GameDir) { $GameDir = "${env:ProgramFiles(x86)}\Steam\steamapps\common\Scam With Your Friends" }
$managed = Join-Path $GameDir 'Scam With Your Friends_Data\Managed'
if (-not (Test-Path (Join-Path $managed 'Assembly-CSharp.dll'))) { throw "Assembly-CSharp.dll not found in '$managed'. Pass -GameDir." }
if (-not (Get-Command ilspycmd -ErrorAction SilentlyContinue)) { dotnet tool install --global ilspycmd --version 9.1.0.7988 }
$out = Join-Path $PSScriptRoot '..\decompiled'
foreach ($asm in 'Assembly-CSharp', 'ScriptsAssDef') {
    $dll = Join-Path $managed "$asm.dll"
    if (Test-Path $dll) {
        Write-Host "Decompiling $asm..."
        ilspycmd -p -o (Join-Path $out $asm) -r $managed $dll
    }
}
Write-Host "Done: $out"

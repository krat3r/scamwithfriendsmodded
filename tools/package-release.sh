#!/usr/bin/env bash
# Builds the mod and packs it, with BepInEx, into a single Windows installer:
#   dist/Hotline-Setup.exe              one-click installer (everything embedded)
#   dist/Hotline-<ver>.zip    the plugin folder, for manual installs
# Needs the .NET 8 SDK and the game installed (the mod compiles against its DLLs).
# Usage: tools/package-release.sh [game-dir]   (or set SWYF_GAME_DIR)
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
if [ $# -ge 1 ]; then export SWYF_GAME_DIR="$1"; fi

BEPINEX_VERSION="5.4.23.5"
BEPINEX_URL="https://github.com/BepInEx/BepInEx/releases/download/v${BEPINEX_VERSION}/BepInEx_win_x64_${BEPINEX_VERSION}.zip"
BEPINEX_SHA256="82f9878551030f54657792c0740d9d51a09500eeae1fba21106b0c441e6732c4"

PAYLOAD="$ROOT/installer/payload"
DIST="$ROOT/dist"
MOD_OUT="$ROOT/src/Hotline/bin/Release"
VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$ROOT/src/Hotline/Hotline.csproj" | head -1)"

echo "Building mod $VERSION..."
rm -rf "$MOD_OUT"
dotnet build "$ROOT/src/Hotline" -c Release -p:DeployToGame=false -nologo -v quiet

mkdir -p "$PAYLOAD" "$DIST"
rm -f "$PAYLOAD/mod.zip"
(cd "$MOD_OUT" && zip -qr "$PAYLOAD/mod.zip" Hotline.dll Hotline.pdb ScamPacks)
echo "$VERSION" > "$PAYLOAD/version.txt"

if [ ! -f "$PAYLOAD/BepInEx.zip" ] || ! echo "$BEPINEX_SHA256  $PAYLOAD/BepInEx.zip" | sha256sum -c --quiet 2>/dev/null; then
  echo "Downloading BepInEx $BEPINEX_VERSION..."
  curl -fsSL -o "$PAYLOAD/BepInEx.zip" "$BEPINEX_URL"
fi
echo "$BEPINEX_SHA256  $PAYLOAD/BepInEx.zip" | sha256sum -c --quiet || { echo "BepInEx download hash mismatch" >&2; exit 1; }

echo "Publishing installer..."
dotnet publish "$ROOT/installer" -c Release -r win-x64 --self-contained \
  -p:PublishSingleFile=true -p:PublishTrimmed=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -o "$ROOT/installer/bin/publish" -nologo -v quiet
cp "$ROOT/installer/bin/publish/Hotline-Setup.exe" "$DIST/"
cp "$PAYLOAD/mod.zip" "$DIST/Hotline-$VERSION.zip"

echo "Done:"
ls -lh "$DIST"

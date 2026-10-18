#!/usr/bin/env bash
# Decompiles the game's code into ./decompiled (gitignored) so you can find classes to hook.
# Usage: tools/decompile.sh [game-dir]   (or set SWYF_GAME_DIR)
# Needs the .NET 8 SDK (ilspycmd is pinned to a version that runs on it).
set -euo pipefail
GAME_DIR="${1:-${SWYF_GAME_DIR:-$HOME/.steam/steam/steamapps/common/Scam With Your Friends}}"
MANAGED="$GAME_DIR/Scam With Your Friends_Data/Managed"
[ -f "$MANAGED/Assembly-CSharp.dll" ] || { echo "Assembly-CSharp.dll not found in '$MANAGED'" >&2; exit 1; }
command -v ilspycmd >/dev/null || { dotnet tool install --global ilspycmd --version 9.1.0.7988; export PATH="$PATH:$HOME/.dotnet/tools"; }
OUT="$(cd "$(dirname "$0")/.." && pwd)/decompiled"
for asm in Assembly-CSharp ScriptsAssDef; do
  if [ -f "$MANAGED/$asm.dll" ]; then
    echo "Decompiling $asm..."
    ilspycmd -p -o "$OUT/$asm" -r "$MANAGED" "$MANAGED/$asm.dll"
  fi
done
echo "Done: $OUT"

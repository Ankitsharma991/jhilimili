#!/usr/bin/env bash
# The creator pack tool (Phase 11 step 4). Builds itself from the game's Core code and tools/creator/*.cs with the Mono
# that ships inside Unity (no editor needed), then runs. Usage: tools/creator/pack.sh help
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
D="${UNITY_DATA:-$HOME/Unity/Hub/Editor/$(sed -n 's/m_EditorVersion: //p' "$ROOT/ProjectSettings/ProjectVersion.txt")/Editor/Data}"
MONO="$D/MonoBleedingEdge/bin/mono"
[ -x "$MONO" ] || { echo "Unity's Mono not found under $D (set UNITY_DATA to the editor's Data folder)"; exit 2; }
LIB="$D/MonoBleedingEdge/lib/mono/4.5"
OUT="$ROOT/Temp/creator"; EXE="$OUT/pack.exe"
mkdir -p "$OUT"
SOURCES=$(find "$ROOT/Assets/_Game/Scripts/Core" -name "*.cs" -not -path "*/Core/Runtime/*"; ls "$HERE"/*.cs)
if [ ! -f "$EXE" ] || [ -n "$(find $SOURCES -newer "$EXE" 2>/dev/null | head -1)" ]; then
  "$MONO" "$LIB/csc.exe" -nologo -langversion:9 -nowarn:1591 -out:"$EXE" -r:"$LIB/System.IO.Compression.dll" -r:"$LIB/System.IO.Compression.FileSystem.dll" $SOURCES >/dev/null \
    || { echo "the pack tool didn't build"; "$MONO" "$LIB/csc.exe" -nologo -langversion:9 -out:"$EXE" -r:"$LIB/System.IO.Compression.dll" -r:"$LIB/System.IO.Compression.FileSystem.dll" $SOURCES | grep error | head; exit 2; }
fi
CREATOR_HOME="$HERE" exec "$MONO" "$EXE" "$@"

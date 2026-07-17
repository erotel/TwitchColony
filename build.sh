#!/usr/bin/env bash
# One-command build for the Twitch Colony mod.
#   - compiles the single project against the game DLLs in ./lib
#   - assembles a ready-to-copy mod folder in ./dist/TwitchColony
# No wine. The game DLLs are only referenced, never bundled; PLib is ILRepacked in (see
# src/ILRepack.targets), so the output is still a single TwitchColony.dll.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$ROOT"

if [ -z "$(ls -A lib/*.dll 2>/dev/null)" ]; then
  echo "ERROR: ./lib is empty. Copy the game's Managed\\ DLLs into ./lib first." >&2
  echo "  (…/steamapps/common/OxygenNotIncluded/OxygenNotIncluded_Data/Managed/)" >&2
  exit 1
fi

echo ">> Building (Release)…"
dotnet build src/TwitchColony.csproj -c Release -v minimal

DLL="src/bin/Release/TwitchColony.dll"
if [ ! -f "$DLL" ]; then
  echo "ERROR: build did not produce $DLL" >&2
  exit 1
fi

echo ">> Assembling dist/TwitchColony …"
OUT="dist/TwitchColony"
rm -rf "$OUT"
mkdir -p "$OUT"
cp "$DLL" "$OUT/"
cp mod_info.yaml mod.yaml "$OUT/"

echo ""
echo "Done. Mod folder: $OUT"
echo "Copy its CONTENTS into:"
echo "  …/Documents/Klei/OxygenNotIncluded/mods/Local/TwitchColony/"
echo "(mod_info.yaml must sit directly in that folder.)"

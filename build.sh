#!/usr/bin/env bash
# One-command build for the Twitch Colony mod.
#   - compiles the mod against the game DLLs in ./lib   -> dist/TwitchColony/  (what players install)
#   - compiles the modding merge-lib                    -> dist/api/           (what add-on authors use)
# No wine. The game DLLs are only referenced, never bundled; PLib is ILRepacked in (see
# src/ILRepack.targets), so the mod still ships as a single TwitchColony.dll.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$ROOT"

if [ -z "$(ls -A lib/*.dll 2>/dev/null)" ]; then
  echo "ERROR: ./lib is empty. Copy the game's Managed\\ DLLs into ./lib first." >&2
  echo "  (…/steamapps/common/OxygenNotIncluded/OxygenNotIncluded_Data/Managed/)" >&2
  exit 1
fi

echo ">> Building the mod (Release)…"
dotnet build src/TwitchColony.csproj -c Release -v minimal

DLL="src/bin/Release/TwitchColony.dll"
if [ ! -f "$DLL" ]; then
  echo "ERROR: build did not produce $DLL" >&2
  exit 1
fi

# Built for net48 and netstandard2.1: add-on authors use both, and a netstandard2.1 project can't
# cleanly reference a net48 library.
echo ">> Building the modding API (Release, net48 + netstandard2.1)…"
dotnet build Api/TwitchColony.Api.csproj -c Release -v minimal

for tfm in net48 netstandard2.1; do
  if [ ! -f "Api/bin/Release/$tfm/TwitchColony.Api.dll" ]; then
    echo "ERROR: build did not produce Api/bin/Release/$tfm/TwitchColony.Api.dll" >&2
    exit 1
  fi
done

# Built after the API, since it merges it in — and it's how we test that the API actually works
# from a separate mod, which is the only way to test it short of writing one.
echo ">> Building the example add-on (Release)…"
dotnet build examples/ExampleAddon/ExampleAddon.csproj -c Release -v minimal

echo ">> Assembling dist/ …"
OUT="dist/TwitchColony"
API_OUT="dist/api"
EXAMPLE_OUT="dist/example-addon/TwitchColonyExampleAddon"
rm -rf dist
mkdir -p "$OUT" "$API_OUT" "$EXAMPLE_OUT"
cp "$DLL" "$OUT/"
cp mod_info.yaml mod.yaml "$OUT/"
for tfm in net48 netstandard2.1; do
  mkdir -p "$API_OUT/$tfm"
  cp "Api/bin/Release/$tfm/TwitchColony.Api.dll" "$API_OUT/$tfm/"
  [ -f "Api/bin/Release/$tfm/TwitchColony.Api.xml" ] && cp "Api/bin/Release/$tfm/TwitchColony.Api.xml" "$API_OUT/$tfm/"
done
cp examples/ExampleAddon/bin/Release/TwitchColonyExampleAddon.dll "$EXAMPLE_OUT/"
cp examples/ExampleAddon/mod_info.yaml examples/ExampleAddon/mod.yaml "$EXAMPLE_OUT/"

echo ""
echo "Done."
echo "Mod folder: $OUT"
echo "  Copy its CONTENTS into:"
echo "    …/Documents/Klei/OxygenNotIncluded/mods/Local/TwitchColony/"
echo "  (mod_info.yaml must sit directly in that folder.)"
echo "Modding API: $API_OUT/{net48,netstandard2.1}/TwitchColony.Api.dll  (for add-on authors — see MODDING.md)"
echo "Example add-on: $EXAMPLE_OUT/  (optional; install like the mod, into mods/Local/)"

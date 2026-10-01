#!/bin/sh
# Builds the mod and copies the DLL into the game. Usage: sh tools/deploy.sh ["<game folder>"]
set -e
GAME="${1:-D:/!stem/steamapps/common/ULTRAKILL}"
cd "$(dirname "$0")/.."
dotnet build UKCraft.csproj -c Release -p:GamePath="$GAME" | tail -3
mkdir -p "$GAME/BepInEx/plugins/UKCraft"
cp bin/Release/netstandard2.1/UKCraft.dll "$GAME/BepInEx/plugins/UKCraft/"
echo "deployed to $GAME/BepInEx/plugins/UKCraft"

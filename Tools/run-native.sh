#!/usr/bin/env bash
set -euo pipefail

game_dir=${STACKISLAND_NATIVE_DIR:-$PWD/Builds/Linux}
if [[ ! -f "$game_dir/StackIsland.x86_64" || ! -d "$game_dir/StackIsland_Data" ]]; then
    echo "Native build missing at $game_dir. Run Tools/build-linux.sh first, or set STACKISLAND_NATIVE_DIR." >&2
    exit 1
fi
cd -- "$game_dir"
exec ./StackIsland.x86_64 "$@"

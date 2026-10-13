#!/usr/bin/env bash
set -euo pipefail

project=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
version=$(sed -n 's/^m_EditorVersion: //p' "$project/ProjectSettings/ProjectVersion.txt")
editor=${UNITY_EDITOR:-}
if [[ -z "$editor" ]]; then
    for candidate in "$HOME/Unity/Hub/Editor/$version/Editor/Unity" "/opt/unity/Editor/Unity"; do
        if [[ -x "$candidate" ]]; then editor=$candidate; break; fi
    done
fi
if [[ -z "$editor" || ! -x "$editor" ]]; then
    echo "Install licensed Unity $version with Linux Build Support, then set UNITY_EDITOR to its Editor/Unity executable." >&2
    exit 1
fi
mkdir -p "$project/Logs"
method=StackIsland.EditorTools.BuildScript.BuildLinux
if [[ "${1:-}" == --zip ]]; then
    method=StackIsland.EditorTools.BuildScript.BuildLinuxZip
    shift
fi
if [[ $# -ne 0 ]]; then echo "Usage: $0 [--zip]" >&2; exit 2; fi
"$editor" -batchmode -nographics -quit -buildTarget Linux64 \
    -projectPath "$project" -executeMethod "$method" -logFile "$project/Logs/build-linux.log"
test -f "$project/Builds/Linux/StackIsland.x86_64"
chmod +x "$project/Builds/Linux/StackIsland.x86_64"
echo "Built $project/Builds/Linux/StackIsland.x86_64"

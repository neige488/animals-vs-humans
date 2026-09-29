#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
unity="${UNITY_EDITOR_ROOT:-/Applications/Unity/Hub/Editor/6000.6.3f1/Unity.app/Contents}"
mono="$unity/Resources/Scripting/MonoBleedingEdge"
out="$(mktemp -d)"
trap 'rm -rf "$out"' EXIT
references=("/r:$mono/lib/mono/4.5/Facades/netstandard.dll")
for module in "$unity"/Resources/Scripting/Managed/UnityEngine/*.dll; do references+=("/r:$module"); done
"$mono/bin/mono" "$mono/lib/mono/4.5/csc.exe" /nologo /target:library /out:"$out/AvH.dll" "${references[@]}" game/Assets/AvH/Core/*.cs game/Assets/AvH/Runtime/*.cs
printf '%s\n' 'Runtime C# compiled against installed Unity assemblies; this does not execute Unity.'

#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
unity="${UNITY_EDITOR_ROOT:-/Applications/Unity/Hub/Editor/6000.6.3f1/Unity.app/Contents}"
mono="$unity/Resources/Scripting/MonoBleedingEdge"
nunit="$unity/Resources/PackageManager/BuiltInPackages/com.unity.ext.nunit/net472/unity-custom/nunit.framework.dll"
out="$(mktemp -d)"
trap 'rm -rf "$out"' EXIT
cp "$nunit" "$out/"
"$mono/bin/mono" "$mono/lib/mono/4.5/csc.exe" /nologo /target:exe /out:"$out/tests.exe" /r:"$nunit" scripts/CoreTestRunner.cs game/Assets/AvH/Core/*.cs game/Assets/AvH/Tests/EditMode/*.cs
"$mono/bin/mono" "$out/tests.exe"

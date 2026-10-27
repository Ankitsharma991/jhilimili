#!/usr/bin/env bash
# Licence-free compile check. Builds every project assembly with the installed editor's own Roslyn,
# against the real UnityEngine/UnityEditor modules, uGUI and Input System (compiled from package
# source) and a thin URP stub (UrpStub.cs, members copied from URP 17.3). Then runs the pure-C# Core
# tests under the editor's Mono. A real Unity import remains the source of truth; when a script uses
# a new URP member, add it to UrpStub.cs with the signature from the URP source.
# Usage: tools/compile-check/check.sh [path-to-editor-Data]
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
HERE="$ROOT/tools/compile-check"
D="${1:-$HOME/Unity/Hub/Editor/$(sed -n 's/m_EditorVersion: //p' "$ROOT/ProjectSettings/ProjectVersion.txt")/Editor/Data}"
[ -d "$D" ] || { echo "Editor Data folder not found: $D"; exit 1; }
OUT="$ROOT/Temp/compile-check"; rm -rf "$OUT"; mkdir -p "$OUT"; cd "$OUT"
PKG="$D/Resources/PackageManager"
CSC=("$D/NetCoreRuntime/dotnet" "$D/DotNetSdkRoslyn/csc.dll" -nologo -nostdlib -noconfig -langversion:9 -nowarn:1701,1702)
PKGWARN="-nowarn:0618,0649,0169,0414,0067"

{ echo "-r:$D/NetStandard/ref/2.1.0/netstandard.dll"
  for f in "$D"/NetStandard/compat/2.1.0/shims/netstandard/*.dll; do echo "-r:$f"; done
  for f in "$D"/Managed/UnityEngine/UnityEngine*.dll; do echo "-r:$f"; done; } > engine.rsp
for f in "$D"/Managed/UnityEngine/UnityEditor*.dll; do echo "-r:$f"; done > editor.rsp

V="UNITY_6000_3_OR_NEWER;UNITY_6000_2_OR_NEWER;UNITY_6000_1_OR_NEWER;UNITY_6000_0_OR_NEWER"
for y in 2023_3 2023_2 2023_1 2022_3 2022_2 2022_1 2021_3 2021_2 2021_1 2020_3 2020_2 2020_1 2019_4 2019_3 2019_2 2019_1 2018_4 2018_3 2018_2 2018_1 2017_4 2017_3 2017_2 2017_1 5_6 5_5 5_4 5_3; do V="$V;UNITY_${y}_OR_NEWER"; done
RUNTIME="$V;UNITY_ANDROID;UNITY_64;ENABLE_INPUT_SYSTEM;NET_STANDARD_2_1;NET_STANDARD;CSHARP_7_3_OR_NEWER;UNITY_INCLUDE_TESTS;DEBUG;TRACE"
EDITOR="$RUNTIME;UNITY_EDITOR;UNITY_EDITOR_64;UNITY_EDITOR_LINUX"

step() { echo "== $1"; }
quote_list() { while IFS= read -r f; do printf '"%s"\n' "$f"; done; }
# Package compiles: print errors only, fail on any.
pkg_compile() { local log; log="$("${CSC[@]}" $PKGWARN "$@" 2>&1)" || { echo "$log" | grep error | head -20; exit 1; }; }

step "uGUI (package source)"
UGUI="$PKG/BuiltInPackages/com.unity.ugui/Runtime"
find "$UGUI/InternalBridge" -name '*.cs' | quote_list > bridge.rsp
pkg_compile -target:library -out:Unity.InternalAPIEngineBridge.004.dll @engine.rsp -define:"$RUNTIME" @bridge.rsp
find "$UGUI/UGUI" -name '*.cs' | quote_list > ugui.rsp
pkg_compile -target:library -out:UnityEngine.UI.dll @engine.rsp -r:Unity.InternalAPIEngineBridge.004.dll \
  -define:"$RUNTIME;PACKAGE_PHYSICS;PACKAGE_ANIMATION;PACKAGE_UITOOLKIT" @ugui.rsp

step "Input System (package source)"
IS_TGZ="$(ls "$PKG"/Editor/com.unity.inputsystem-*.tgz | head -1)"
mkdir -p is && tar xzf "$IS_TGZ" -C is
find is/package/InputSystem -name '*.cs' -not -path '*/Editor/*' -not -path '*/Plugins/InputForUI/*' | quote_list > is.rsp
ISDEF="UNITY_INPUT_SYSTEM_ENABLE_UI;UNITY_INPUT_SYSTEM_ENABLE_PHYSICS;UNITY_INPUT_SYSTEM_PLATFORM_SCROLL_DELTA;UNITY_INPUT_SYSTEM_INPUT_MODULE_SCROLL_DELTA;UNITY_INPUT_SYSTEM_INPUT_MODULE_NAVIGATION_DEVICE_TYPE;UNITY_INPUT_SYSTEM_SENDPOINTERHOVERTOPARENT;UNITY_INPUT_SYSTEM_PLATFORM_POLLING_FREQUENCY;DEVELOPMENT_BUILD"
pkg_compile -target:library -unsafe -out:Unity.InputSystem.dll @engine.rsp -r:UnityEngine.UI.dll -define:"$RUNTIME;$ISDEF" @is.rsp

step "URP and TextMeshPro stubs"
"${CSC[@]}" -target:library -out:UrpStub.dll @engine.rsp "$HERE/UrpStub.cs"
"${CSC[@]}" -target:library -out:Unity.TextMeshPro.dll @engine.rsp -r:UnityEngine.UI.dll "$HERE/TmpStub.cs"

A="$ROOT/Assets/_Game"
DEPS=(-r:UnityEngine.UI.dll -r:Unity.InputSystem.dll -r:UrpStub.dll -r:Unity.TextMeshPro.dll)
step "UtsavNights.Core"
"${CSC[@]}" -warnaserror+ -target:library -out:UtsavNights.Core.dll -r:"$D/NetStandard/ref/2.1.0/netstandard.dll" $(find "$A/Scripts/Core" -name "*.cs" -not -path "*/Core/Runtime/*")
step "UtsavNights.Core.Runtime"
"${CSC[@]}" -warnaserror+ -target:library -out:UtsavNights.Core.Runtime.dll @engine.rsp -r:UtsavNights.Core.dll -define:"$RUNTIME" "$A"/Scripts/Core/Runtime/*.cs
step "UtsavNights.Content"
"${CSC[@]}" -warnaserror+ -target:library -out:UtsavNights.Content.dll @engine.rsp -r:UtsavNights.Core.dll -define:"$RUNTIME" "$A"/Scripts/Content/*.cs
step "UtsavNights.Effects"
"${CSC[@]}" -warnaserror+ -target:library -out:UtsavNights.Effects.dll @engine.rsp -r:UtsavNights.Core.dll -r:UtsavNights.Core.Runtime.dll -r:UtsavNights.Content.dll -define:"$RUNTIME" "$A"/Scripts/Effects/*.cs
step "UtsavNights.Audio"
"${CSC[@]}" -warnaserror+ -target:library -out:UtsavNights.Audio.dll @engine.rsp -r:UtsavNights.Core.dll -r:UtsavNights.Core.Runtime.dll -r:UtsavNights.Content.dll -define:"$RUNTIME" "$A"/Scripts/Audio/*.cs
step "UtsavNights.Haptics (player defines include the Android JNI backend)"
"${CSC[@]}" -warnaserror+ -target:library -out:UtsavNights.Haptics.dll @engine.rsp -r:UtsavNights.Core.dll -r:UtsavNights.Core.Runtime.dll -r:UtsavNights.Content.dll -define:"$RUNTIME" "$A"/Scripts/Haptics/*.cs
"${CSC[@]}" -warnaserror+ -target:library -out:Haptics.editor-variant.dll @engine.rsp -r:UtsavNights.Core.dll -r:UtsavNights.Core.Runtime.dll -r:UtsavNights.Content.dll -define:"$EDITOR" "$A"/Scripts/Haptics/*.cs
step "UtsavNights.Gameplay"
"${CSC[@]}" -warnaserror+ -target:library -out:UtsavNights.Gameplay.dll @engine.rsp -r:Unity.InputSystem.dll -r:UnityEngine.UI.dll -r:UrpStub.dll -r:UtsavNights.Core.dll -r:UtsavNights.Core.Runtime.dll -r:UtsavNights.Content.dll -r:UtsavNights.Effects.dll -r:UtsavNights.Audio.dll -r:UtsavNights.Haptics.dll -define:"$RUNTIME" $(find "$A/Scripts/Gameplay" -name "*.cs")
"${CSC[@]}" -warnaserror+ -target:library -out:Gameplay.editor-variant.dll @engine.rsp -r:Unity.InputSystem.dll -r:UnityEngine.UI.dll -r:UrpStub.dll -r:UtsavNights.Core.dll -r:UtsavNights.Core.Runtime.dll -r:UtsavNights.Content.dll -r:UtsavNights.Effects.dll -r:UtsavNights.Audio.dll -r:Haptics.editor-variant.dll -define:"$EDITOR" $(find "$A/Scripts/Gameplay" -name "*.cs")
step "UtsavNights.UI"
"${CSC[@]}" -warnaserror+ -target:library -out:UtsavNights.UI.dll @engine.rsp "${DEPS[@]}" -r:UtsavNights.Core.dll -r:UtsavNights.Core.Runtime.dll -r:UtsavNights.Content.dll -r:UtsavNights.Gameplay.dll -r:UtsavNights.Haptics.dll -r:UtsavNights.Effects.dll -r:UtsavNights.Audio.dll -define:"$RUNTIME" $(find "$A/Scripts/UI" -name "*.cs")
step "UtsavNights.Editor"
"${CSC[@]}" -warnaserror+ -target:library -out:UtsavNights.Editor.dll @engine.rsp @editor.rsp "${DEPS[@]}" -r:UtsavNights.Core.dll -r:UtsavNights.Core.Runtime.dll -r:UtsavNights.Content.dll -r:UtsavNights.Effects.dll -r:UtsavNights.Audio.dll -r:Haptics.editor-variant.dll -r:Gameplay.editor-variant.dll -r:UtsavNights.UI.dll -define:"$EDITOR" $(find "$A/Scripts/Editor" -name "*.cs")
step "UtsavNights.Tests.EditMode"
NUNIT="$PKG/BuiltInPackages/com.unity.ext.nunit/net40/unity-custom/nunit.framework.dll"
"${CSC[@]}" -warnaserror+ -target:library -out:UtsavNights.Tests.EditMode.dll @engine.rsp @editor.rsp -r:"$D/NetStandard/compat/2.1.0/shims/netfx/mscorlib.dll" \
  -r:"$NUNIT" "${DEPS[@]}" -r:UtsavNights.Core.dll -r:UtsavNights.Core.Runtime.dll -r:UtsavNights.Content.dll -r:UtsavNights.Effects.dll -r:UtsavNights.Audio.dll -r:UtsavNights.Haptics.dll -r:UtsavNights.Gameplay.dll -r:UtsavNights.UI.dll -r:UtsavNights.Editor.dll -define:"$EDITOR" "$A"/Tests/EditMode/*.cs

step "Archived spikes (only with UTSAV_SPIKES; player and editor defines)"
"${CSC[@]}" -warnaserror+ -target:library -out:UtsavNights.Spikes.dll @engine.rsp "${DEPS[@]}" -r:UtsavNights.Core.dll -define:"$RUNTIME;UTSAV_SPIKES" "$A"/Spikes/Scripts/*.cs
"${CSC[@]}" -warnaserror+ -target:library -out:Spikes.editor-variant.dll @engine.rsp "${DEPS[@]}" -r:UtsavNights.Core.dll -define:"$EDITOR;UTSAV_SPIKES" "$A"/Spikes/Scripts/*.cs
"${CSC[@]}" -warnaserror+ -target:library -out:UtsavNights.Spikes.Editor.dll @engine.rsp @editor.rsp "${DEPS[@]}" -r:UtsavNights.Core.dll -r:UtsavNights.Spikes.dll -r:UtsavNights.Editor.dll -define:"$EDITOR;UTSAV_SPIKES" "$A"/Spikes/Editor/*.cs
"${CSC[@]}" -warnaserror+ -target:library -out:UtsavNights.Spikes.Tests.dll @engine.rsp @editor.rsp -r:"$D/NetStandard/compat/2.1.0/shims/netfx/mscorlib.dll" -r:"$NUNIT" -r:UtsavNights.Spikes.dll -define:"$EDITOR;UTSAV_SPIKES" "$A"/Spikes/Tests/*.cs

step "Core tests under Mono (Unity-free subset)"
MONO="$D/MonoBleedingEdge/bin/mono"; MCSC=("$MONO" "$D/MonoBleedingEdge/lib/mono/4.5/csc.exe" -nologo -langversion:9)
mkdir -p mono && cp "$NUNIT" mono/
"${MCSC[@]}" -target:library -out:mono/Core.dll $(find "$A/Scripts/Core" -name "*.cs" -not -path "*/Core/Runtime/*")
"${MCSC[@]}" -out:mono/CoreTests.exe -r:mono/Core.dll -r:mono/nunit.framework.dll "$HERE/CoreTestRunner.cs" \
  "$A"/Tests/EditMode/{RollingStatsTests,IgnitionHoldTrackerTests,SpikeMathTests,ProceduralSoundsTests,CrackerStateMachineTests,CoreServicesTests,EffectsMathTests,AudioMathTests,HapticWaveformTests,LighterModelTests,CameraTimingTests,QualityGovernorTests,PlaytestSessionTests,GameSettingsTests,PlacementRulesTests,StageMathTests,ScreenFitTests,FinaleScheduleTests,BlindSoundTestTests,AdPolicyTests,PlayerProgressTests}.cs
"$MONO" mono/CoreTests.exe
echo "All project assemblies compiled."

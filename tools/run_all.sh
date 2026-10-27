#!/usr/bin/env bash
# Local pipeline: rebuild generated scenes, run EditMode and PlayMode tests, then build the release APK
# and install it on the connected device, only when every test passed.
# Usage: tools/run_all.sh [--no-install | --tests-only]
cd "$(dirname "$0")/.."
U="$HOME/Unity/Hub/Editor/$(sed -n 's/m_EditorVersion: //p' ProjectSettings/ProjectVersion.txt)/Editor/Unity"
rm -f Logs/editmode-results.xml Logs/playmode-results.xml
timeout 2400 "$U" -batchmode -projectPath . -executeMethod UtsavNights.EditorTools.Main.MainSceneBuilder.BuildBatch -logFile Logs/main_build.log; echo "scenes=$?"
grep -E "error CS" Logs/main_build.log | sort -u | head -5
timeout 1500 "$U" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults Logs/editmode-results.xml -logFile Logs/tests.log; echo "editmode=$?"
timeout 1500 "$U" -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults Logs/playmode-results.xml -logFile Logs/playmode.log; echo "playmode=$?"
ok=1
for f in Logs/editmode-results.xml Logs/playmode-results.xml; do
  run=$(grep -o '<test-run[^>]*' "$f" 2>/dev/null)
  echo "$f: $(echo "$run" | grep -oE '(total|passed|failed)="[0-9]+"' | tr '\n' ' ')"
  echo "$run" | grep -q 'failed="0"' || ok=0
done
python3 - <<'PY'
import re
for f in ['Logs/editmode-results.xml', 'Logs/playmode-results.xml']:
    try:
        s = open(f).read()
    except OSError:
        continue
    for m in re.finditer(r'<test-case [^>]*name="([^"]+)"[^>]*result="Failed".*?<message><!\[CDATA\[(.*?)\]\]>', s, re.S):
        print("FAILED", f, m.group(1), m.group(2)[:500])
PY
if [ "$ok" != 1 ]; then echo "tests failed: APK not built"; exit 1; fi
if [ "${1:-}" = "--tests-only" ]; then exit 0; fi
timeout 3000 "$U" -batchmode -nographics -projectPath . -buildTarget Android -executeMethod UtsavNights.EditorTools.BuildScript.CiReleaseApk -logFile Logs/build_apk.log; echo "apk=$?"
grep -a "UTSAV_BUILD" Logs/build_apk.log | tail -1 | cut -c1-160
grep -aE "\[Ads\]|FAILURE|What went wrong" Logs/build_apk.log | head -5
if [ "${1:-}" != "--no-install" ]; then timeout 120 adb install -r Builds/Android/UtsavNights-release.apk 2>&1 | tail -1; fi

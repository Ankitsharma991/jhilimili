#!/usr/bin/env bash
# Copies spike_results.json and the tagged log lines from the phone into captures/<device>-<time>/.
set -euo pipefail
PKG="${1:-com.ankitsharma.utsavnights}"
MODEL="$(adb shell getprop ro.product.model | tr -d '\r' | tr ' ' '_')"
OUT="captures/${MODEL}-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$OUT"

REMOTE="/sdcard/Android/data/${PKG}/files/spike_results.json"
if ! adb pull "$REMOTE" "$OUT/spike_results.json" 2>/dev/null; then
  # Development builds are debuggable, so run-as can read the file when direct pull is blocked.
  adb exec-out run-as "$PKG" cat "files/spike_results.json" > "$OUT/spike_results.json" 2>/dev/null \
    || echo "Could not pull the JSON file; using log lines only."
fi
adb logcat -d -s Unity | grep -E 'UTSAV_SPIKE|UTSAV_BUILD' > "$OUT/spike_log.txt" || true
adb shell dumpsys battery | grep -i temperature > "$OUT/battery_temp.txt" || true
echo "Saved to $OUT"

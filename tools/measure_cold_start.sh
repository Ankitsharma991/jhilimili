#!/usr/bin/env bash
# Gate C: cold start to first frame on a USB-connected phone.
# Usage: tools/measure_cold_start.sh [runs] [package]
set -euo pipefail
RUNS="${1:-5}"
PKG="${2:-com.ankitsharma.utsavnights}"

command -v adb >/dev/null || { echo "adb not found"; exit 1; }
adb get-state >/dev/null || { echo "No device connected"; exit 1; }

COMPONENT="$(adb shell cmd package resolve-activity --brief "$PKG" | tail -n 1 | tr -d '\r')"
if [[ "$COMPONENT" != */* ]]; then
  echo "Could not resolve launcher activity for $PKG. Is the app installed?"; exit 1
fi

echo "Device: $(adb shell getprop ro.product.model | tr -d '\r'), Android $(adb shell getprop ro.build.version.release | tr -d '\r')"
echo "Activity: $COMPONENT"
total=0
for i in $(seq 1 "$RUNS"); do
  adb shell am force-stop "$PKG"
  sleep 2
  adb logcat -c
  ms="$(adb shell am start -W -n "$COMPONENT" | awk -F': ' '/TotalTime/ {print $2}' | tr -d '\r')"
  sleep 8
  ready="$(adb logcat -d -s Unity | grep -m1 'scene_ready_since_engine_start' | sed -E 's/.*=([0-9.]+) ms.*/\1/' || true)"
  echo "run $i: activity TotalTime=${ms} ms, Unity scene_ready=${ready:-?} ms after engine start"
  total=$((total + ms))
done
echo "average activity TotalTime: $((total / RUNS)) ms"
echo "Cold start to interactive is roughly TotalTime + scene_ready. Gate C target: under 5000 ms."

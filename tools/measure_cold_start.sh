#!/usr/bin/env bash
# PRD 14 cold start to interactive on a USB-connected phone (Phase 5 step 3).
# Usage: tools/measure_cold_start.sh [runs] [package]
# Each run force-stops the app, starts it, and reads the [UTSAV_STARTUP] line the game logs once the
# scene takes taps. interactive_ms counts from the real process start (android.os.Process), so it is
# what a player waits. Later marks (consent, ads_sdk, store) show the SDKs finishing in the background.
# Targets: 6000 ms low tier, 4000 ms mid, 3000 ms high.
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
total=0; n=0
for i in $(seq 1 "$RUNS"); do
  adb shell am force-stop "$PKG"
  sleep 2
  adb logcat -c
  first="$(adb shell am start -W -n "$COMPONENT" | awk -F': ' '/TotalTime/ {print $2}' | tr -d '\r')"
  sleep 10
  line="$(adb logcat -d -s Unity | grep 'UTSAV_STARTUP' | tail -1 | sed -E 's/.*\[UTSAV_STARTUP\] //' | tr -d '\r' || true)"
  ms="$(echo "$line" | grep -oE 'interactive_ms=[0-9]+' | cut -d= -f2 || true)"
  echo "run $i: first frame ${first} ms (Android); ${line:-no [UTSAV_STARTUP] line}"
  if [ -n "$ms" ]; then total=$((total + ms)); n=$((n + 1)); fi
done
if [ "$n" -gt 0 ]; then
  echo "average cold start to interactive: $((total / n)) ms over $n runs (targets: low 6000, mid 4000, high 3000)"
fi
adb shell am force-stop "$PKG"

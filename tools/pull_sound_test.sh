#!/usr/bin/env bash
# Pulls the blind sound test results (Phase 3 step 13, F03) from the phone and prints the summary.
# Usage: tools/pull_sound_test.sh   (set PKG to override the package)
set -euo pipefail
PKG="${PKG:-com.ankitsharma.utsavnights}"
MODEL="$(adb shell getprop ro.product.model | tr -d '\r' | tr ' ' '_')"
OUT="captures/soundtest-${MODEL}-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$OUT"
adb pull "/sdcard/Android/data/${PKG}/files/blind_sound_test.jsonl" "$OUT/blind_sound_test.jsonl" >/dev/null
echo "Saved $OUT/blind_sound_test.jsonl"
python3 "$(dirname "$0")/phase3_report.py" --sound "$OUT"

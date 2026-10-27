#!/usr/bin/env bash
# Pulls the playtest log from the phone and prints one summary row per session.
# Usage: tools/pull_playtest.sh [label]   (label, e.g. tester-03, names the capture folder)
# Package: set PKG to override com.ankitsharma.utsavnights.
set -euo pipefail
PKG="${PKG:-com.ankitsharma.utsavnights}"
LABEL="${1:-}"
MODEL="$(adb shell getprop ro.product.model | tr -d '\r' | tr ' ' '_')"
OUT="captures/playtest-${MODEL}-$(date +%Y%m%d-%H%M%S)${LABEL:+-$LABEL}"
mkdir -p "$OUT"
adb pull "/sdcard/Android/data/${PKG}/files/playtest_sessions.jsonl" "$OUT/playtest_sessions.jsonl" >/dev/null
echo "Saved $OUT/playtest_sessions.jsonl"
python3 "$(dirname "$0")/phase3_report.py" --sessions "$OUT"

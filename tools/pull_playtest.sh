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
F="/sdcard/Android/data/${PKG}/files/playtest_sessions.jsonl"
adb pull "$F" "$OUT/current.jsonl" >/dev/null
# Past 4 MB the app moves the log to .old (LocalLog); keep the order oldest first.
adb pull "$F.old" "$OUT/old.jsonl" >/dev/null 2>&1 || true
cat "$OUT"/old.jsonl "$OUT/current.jsonl" 2>/dev/null > "$OUT/playtest_sessions.jsonl"
rm -f "$OUT/old.jsonl" "$OUT/current.jsonl"
echo "Saved $OUT/playtest_sessions.jsonl"
python3 "$(dirname "$0")/phase3_report.py" --sessions "$OUT"

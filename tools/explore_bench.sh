#!/usr/bin/env bash
# Phase 12 step 8: runs the Explore bench on a USB-connected phone and pulls the results.
# Usage: tools/explore_bench.sh [runs] [package]
# Each run force-stops the app, starts it with the extra utsav_bench=explore (the bench waits 8 s, then tours the
# courtyard, the bazaar on foot, by the stalls, on the scooter and the chowk's show, about 90 s), waits for the
# "[UTSAV_BENCH] done" line, and saves the log lines, explore_bench.jsonl, memory (dumpsys meminfo) and the battery
# temperature into captures/bench-<device>-<time>/. Not for store builds (they have no bench).
# Budgets (ExploreBudget): 30 fps or more, under 5% of frames over 66.7 ms, 4 KB of garbage a frame, memory
# 700 / 900 / 1200 MB by tier. A release APK can't measure garbage (gc_kb_avg -1); a development APK can.
set -euo pipefail
RUNS="${1:-1}"
PKG="${2:-com.ankitsharma.utsavnights}"

command -v adb >/dev/null || { echo "adb not found"; exit 1; }
adb get-state >/dev/null || { echo "No device connected"; exit 1; }
COMPONENT="$(adb shell cmd package resolve-activity --brief "$PKG" | tail -n 1 | tr -d '\r')"
[[ "$COMPONENT" == */* ]] || { echo "Could not resolve the launcher activity for $PKG. Is the app installed?"; exit 1; }

MODEL="$(adb shell getprop ro.product.model | tr -d '\r' | tr ' ' '_')"
OUT="captures/bench-${MODEL}-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$OUT"
echo "Device: $MODEL, Android $(adb shell getprop ro.build.version.release | tr -d '\r'), RAM $(adb shell cat /proc/meminfo | awk '/MemTotal/ {printf "%d MB", $2/1024}')"

for i in $(seq 1 "$RUNS"); do
  adb shell am force-stop "$PKG"
  sleep 2
  adb logcat -c
  adb shell am start -n "$COMPONENT" --es utsav_bench explore >/dev/null
  echo "run $i: started; touring (about 100 s)..."
  peak=0
  for _ in $(seq 1 60); do
    sleep 3
    pss="$(adb shell dumpsys meminfo "$PKG" 2>/dev/null | awk '/TOTAL PSS:/ {print $3; exit} /^ *TOTAL / {print $2; exit}' | tr -d '\r')"
    if [[ "$pss" =~ ^[0-9]+$ ]] && [ "$pss" -gt "$peak" ]; then peak=$pss; fi
    if adb logcat -d -s Unity | grep -q 'UTSAV_BENCH\] done'; then break; fi
  done
  adb logcat -d -s Unity | grep -E 'UTSAV_BENCH|UTSAV_BUILD|Quality\]' > "$OUT/run${i}_log.txt" || true
  echo "run $i: peak PSS $((peak / 1024)) MB" | tee -a "$OUT/memory.txt"
  python3 - "$OUT/run${i}_log.txt" <<'PY2' || true
import json, sys
for line in open(sys.argv[1], errors="replace"):
    if "[UTSAV_BENCH] {" not in line:
        continue
    r = json.loads(line[line.index("{"):])
    if "leg" in r:
        print(f"  {r['leg']:13} {r['avg_fps']:5.1f} fps  p95 {r['p95_ms']:5.1f} ms  max {r['max_ms']:6.1f} ms  spikes {r['spike_pct']:4.1f}%"
              f"  mem {r['memory_mb']:6.0f} MB  crackers {r['crackers']} refused {r['refused']}  {'ok' if r['ok'] else 'MISSED: ' + '; '.join(r['misses'])}")
    elif r.get("done"):
        print(f"  all budgets held: {r['ok']}{'  error: ' + r['error'] if r.get('error') else ''}")
PY2
done

adb exec-out run-as "$PKG" cat files/explore_bench.jsonl > "$OUT/explore_bench.jsonl" 2>/dev/null \
  || adb pull "/sdcard/Android/data/${PKG}/files/explore_bench.jsonl" "$OUT/explore_bench.jsonl" 2>/dev/null \
  || echo "Could not pull explore_bench.jsonl (a release build); the log lines have the same results."
adb shell dumpsys battery | grep -i temperature > "$OUT/battery_temp.txt" || true
adb shell am force-stop "$PKG"
echo "Saved to $OUT"

#!/usr/bin/env bash
# Pulls the playtest log from the phone and prints one summary row per session.
# Usage: tools/pull_playtest.sh [package]
set -euo pipefail
PKG="${1:-com.ankitsharma.utsavnights}"
MODEL="$(adb shell getprop ro.product.model | tr -d '\r' | tr ' ' '_')"
OUT="captures/playtest-${MODEL}-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$OUT"
adb pull "/sdcard/Android/data/${PKG}/files/playtest_sessions.jsonl" "$OUT/playtest_sessions.jsonl" >/dev/null
echo "Saved $OUT/playtest_sessions.jsonl"
python3 - "$OUT/playtest_sessions.jsonl" <<'PY'
import json, sys
from collections import defaultdict
sessions = defaultdict(list)
for line in open(sys.argv[1]):
    line = line.strip()
    if line:
        e = json.loads(line)
        sessions[e["session"]].append(e)
print(f"{'session':17} {'device':14} {'first_ign_s':>11} {'ignitions':>9} {'payoffs':>7} {'flame_outs':>10} {'avg_fps':>7} {'min_fps':>7} {'tier':>4}")
for sid, events in sessions.items():
    start = next((e for e in events if e["type"] == "session_start"), {})
    ign = [e for e in events if e["type"] == "cracker_ignited"]
    first = next((e["time_to_first_ignition_s"] for e in ign if "time_to_first_ignition_s" in e), None)
    perf = [e for e in events if e["type"] == "perf_sample"]
    fps = [p["avg_fps"] for p in perf]
    tier = perf[-1].get("tier", "?") if perf else "?"
    print(f"{sid:17} {start.get('device','?')[:14]:14} {first if first is not None else '-':>11} {len(ign):>9} "
          f"{sum(1 for e in events if e['type']=='payoff_complete'):>7} {sum(1 for e in events if e['type']=='flame_out'):>10} "
          f"{(sum(fps)/len(fps)) if fps else 0:>7.1f} {min(fps) if fps else 0:>7.1f} {tier:>4}")
PY

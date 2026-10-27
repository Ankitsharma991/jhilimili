#!/usr/bin/env bash
# Pulls the development analytics and crash logs (Phase 4 step 6, F12) and prints a small dashboard:
# events per type, PRD 17 funnel numbers and crash-free sessions. Stand-in until Firebase is connected.
# Usage: tools/pull_analytics.sh [--clear]   (set PKG to override the package)
set -euo pipefail
PKG="${PKG:-com.ankitsharma.utsavnights}"
MODEL="$(adb shell getprop ro.product.model | tr -d '\r' | tr ' ' '_')"
OUT="captures/analytics-${MODEL}-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$OUT"
DIR="/sdcard/Android/data/${PKG}/files"
adb pull "$DIR/analytics_events.jsonl" "$OUT/" >/dev/null 2>&1 || echo "No analytics events on the phone yet."
adb pull "$DIR/crash_reports.jsonl" "$OUT/" >/dev/null 2>&1 || true
# Past 4 MB the app moves a log to .old (LocalLog); put those lines first.
for f in analytics_events crash_reports; do
  if adb pull "$DIR/$f.jsonl.old" "$OUT/$f.old" >/dev/null 2>&1; then
    cat "$OUT/$f.old" "$OUT/$f.jsonl" 2>/dev/null > "$OUT/$f.tmp"; mv "$OUT/$f.tmp" "$OUT/$f.jsonl"; rm -f "$OUT/$f.old"
  fi
done
if [ "${1:-}" = "--clear" ]; then
  adb shell rm -f "$DIR/analytics_events.jsonl" "$DIR/crash_reports.jsonl" "$DIR/analytics_events.jsonl.old" "$DIR/crash_reports.jsonl.old"
fi
echo "Saved to $OUT"
python3 - "$OUT" <<'PY'
import json, os, sys
from collections import Counter
out = sys.argv[1]
path = os.path.join(out, "analytics_events.jsonl")
events = [json.loads(l) for l in open(path)] if os.path.exists(path) else []
counts = Counter(e["event"] for e in events)
print("Events by type:")
for name, n in counts.most_common():
    print(f"  {name:18} {n}")
sessions = counts["session_end"]
ign = [e for e in events if e["event"] == "session_end"]
if ign:
    print(f"Ignitions per session: {sum(e.get('ignitions', 0) for e in ign) / len(ign):.1f} (target 8+)")
ignited = {e.get("id") for e in events if e["event"] == "cracker_ignited"}
paid = {e.get("id") for e in events if e["event"] == "payoff_complete"}
if ignited:
    print(f"Crackers ignited: {len(ignited)} kinds; reached payoff: {len(paid & ignited)} kinds")
shown = [e for e in events if e["event"] == "ad_shown"]
early = [e for e in shown if 0 <= e.get("ms_since_payoff", -1) < 10000]
print(f"Ads shown: {len(shown)}; within 10 s of a payoff: {len(early)} (must be 0 for interstitials)")
crash_path = os.path.join(out, "crash_reports.jsonl")
crashes = [json.loads(l) for l in open(crash_path)] if os.path.exists(crash_path) else []
fatal = [c for c in crashes if c.get("kind") == "exception"]
if sessions:
    print(f"Sessions: {sessions}; exceptions logged: {len(fatal)}; non-fatals: {len(crashes) - len(fatal)}")
PY

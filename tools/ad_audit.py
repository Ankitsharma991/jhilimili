#!/usr/bin/env python3
"""Phase 4 ad placement audit on real device sessions (definition of done: zero ads at forbidden
moments over 50 test sessions).

Reads playtest_sessions.jsonl files (from tools/pull_playtest.sh) under the given folders, replays each
session and checks every ad_shown against PRD 15.1, on its own, without using the game's code:
no ad during a fuse, payoff or launch (between cracker_ignited and payoff_complete), while placing
(tray_select until cracker_placed or tray_cancel), or during a finale; interstitials only 4+ minutes
after launch or the last ad, after 6+ ignitions, and 10+ s after a payoff. (Taps are not logged on
devices; that rule is covered by the simulated audit in AdAuditTests.)
Usage: tools/ad_audit.py [DIR ...]   (default: captures)
"""
import glob
import json
import os
import sys
from collections import defaultdict


def audit(events):
    violations = []
    active = 0
    placing = False
    finale = False
    last_payoff = float("-inf")
    last_ad_or_start = 0.0
    ignitions = 0
    shown = 0
    for e in sorted(events, key=lambda x: x.get("t", 0)):
        t = e.get("t", 0)
        kind = e["type"]
        if kind == "session_start":
            last_ad_or_start = t
            ignitions = 0
        elif kind == "cracker_ignited":
            active += 1
            ignitions += 1
        elif kind == "payoff_complete":
            active = max(0, active - 1)
            last_payoff = t
        elif kind == "tray_select":
            placing = True
        elif kind in ("cracker_placed", "tray_cancel"):
            placing = False
        elif kind == "finale_start":
            finale = True
        elif kind == "finale_end":
            finale = False
        elif kind == "ad_shown":
            shown += 1
            ad = e.get("kind", "?")
            at = f"{ad} ad at {t:.1f} s ({e.get('placement', '?')}): "
            if active > 0:
                violations.append(at + "during a fuse, payoff or launch")
            if placing:
                violations.append(at + "while placing a cracker")
            if finale:
                violations.append(at + "during a finale")
            if ad == "Interstitial":
                if t - last_payoff < 10:
                    violations.append(at + "within 10 s of a payoff")
                if t - last_ad_or_start < 240:
                    violations.append(at + "within 4 minutes of launch or the last ad")
                if ignitions < 6:
                    violations.append(at + f"after only {ignitions} ignitions")
            last_ad_or_start = t
            ignitions = 0
    return shown, violations


def main():
    dirs = sys.argv[1:] or ["captures"]
    files = sorted({f for d in dirs for f in glob.glob(os.path.join(d, "**", "playtest_sessions.jsonl"), recursive=True)})
    sessions = defaultdict(list)
    seen = set()
    for path in files:
        for line in open(path, encoding="utf-8"):
            line = line.strip()
            if line and line not in seen:
                seen.add(line)
                e = json.loads(line)
                sessions[e["session"]].append(e)
    if not sessions:
        print("No playtest sessions found. Pull them with tools/pull_playtest.sh first.")
        return 1
    total_ads = 0
    bad = 0
    for sid, events in sorted(sessions.items()):
        shown, violations = audit(events)
        total_ads += shown
        if violations:
            bad += 1
            print(f"{sid}: {len(violations)} violation(s)")
            for v in violations:
                print("   " + v)
    with_ads = sum(1 for ev in sessions.values() if any(e["type"] == "ad_shown" for e in ev))
    print(f"{len(sessions)} sessions ({with_ads} with ads), {total_ads} ads shown, {bad} session(s) with violations")
    done = len(sessions) >= 50 and bad == 0
    print("Definition of done (50+ sessions, zero violations): " + ("PASS" if done else "not yet" if bad == 0 else "FAIL"))
    return 0 if bad == 0 else 2


if __name__ == "__main__":
    sys.exit(main())

#!/usr/bin/env python3
"""Phase 3 measurement report (Docs/Phase3/MEASURE.md).

Reads every playtest_sessions.jsonl and blind_sound_test.jsonl under the given folders (default:
captures/), removes duplicates (the same session pulled twice), and prints:
  - free sessions: one row per session plus the exit gate (average 8 or more ignitions);
  - blind sound test: one row per tester, per-cracker hit rate, confusions, and the F03 check
    (each tester names 6 of 8).
Usage: tools/phase3_report.py [--sessions DIR ...] [--sound DIR ...] [--min-minutes N] [DIR ...]
"""
import argparse
import glob
import json
import os
import sys
from collections import Counter, defaultdict

IGNITION_GATE = 8.0
SOUND_PASS = 6


def read_lines(paths):
    seen = set()
    for path in paths:
        with open(path, encoding="utf-8") as f:
            for raw in f:
                raw = raw.strip()
                if not raw or raw in seen:
                    continue
                seen.add(raw)
                try:
                    yield json.loads(raw)
                except json.JSONDecodeError:
                    print(f"skipped a broken line in {path}", file=sys.stderr)


def find(dirs, name):
    files = []
    for d in dirs:
        files += glob.glob(os.path.join(d, "**", name), recursive=True)
    return sorted(set(files))


def sessions_report(dirs, min_minutes):
    files = find(dirs, "playtest_sessions.jsonl")
    if not files:
        print("No playtest logs found.")
        return
    sessions = defaultdict(list)
    for e in read_lines(files):
        sessions[e["session"]].append(e)

    print(f"Free sessions ({len(files)} file(s))")
    header = f"{'session':17} {'device':14} {'min':>5} {'ign':>4} {'kinds':>5} {'sel':>4} {'placed':>6} {'reject':>6} {'cancel':>6} {'finale':>6} {'first_s':>7} {'avg_fps':>7} {'min_fps':>7}"
    print(header)
    counted = []
    for sid, events in sorted(sessions.items()):
        types = Counter(e["type"] for e in events)
        start = next((e for e in events if e["type"] == "session_start"), {})
        ign = [e for e in events if e["type"] == "cracker_ignited"]
        first = next((e["time_to_first_ignition_s"] for e in ign if "time_to_first_ignition_s" in e), None)
        perf = [e["avg_fps"] for e in events if e["type"] == "perf_sample"]
        minutes = max((e.get("t", 0) for e in events), default=0) / 60.0
        row = {
            "ign": len(ign),
            "kinds": len({e.get("id") for e in ign}),
            "minutes": minutes,
        }
        print(f"{sid:17} {start.get('device', '?')[:14]:14} {minutes:5.1f} {row['ign']:4} {row['kinds']:5} "
              f"{types['tray_select']:4} {types['cracker_placed']:6} {types['placement_rejected']:6} {types['tray_cancel']:6} "
              f"{types['finale_start']:6} {first if first is not None else '-':>7} "
              f"{(sum(perf) / len(perf)) if perf else 0:7.1f} {min(perf) if perf else 0:7.1f}")
        if minutes >= min_minutes:
            counted.append(row)

    reasons = Counter(e.get("reason", "") for evs in sessions.values() for e in evs if e["type"] == "placement_rejected")
    if reasons:
        print("Placement rejections by reason: " + ", ".join(f"{r or '?'} x{n}" for r, n in reasons.most_common()))
    if counted:
        avg = sum(r["ign"] for r in counted) / len(counted)
        verdict = "PASS" if avg >= IGNITION_GATE and len(counted) >= 10 else ("needs 10 testers" if avg >= IGNITION_GATE else "FAIL")
        print(f"Exit gate: {len(counted)} session(s) of {min_minutes:g}+ min, average ignitions {avg:.1f} "
              f"(target {IGNITION_GATE:g}) -> {verdict}")
    print()


def sound_report(dirs):
    files = find(dirs, "blind_sound_test.jsonl")
    if not files:
        print("No blind sound test results found.")
        return
    tests = defaultdict(list)
    for e in read_lines(files):
        tests[e["test"]].append(e)

    print(f"Blind sound test ({len(files)} file(s))")
    print(f"{'test':17} {'tester':>6} {'correct':>7} {'replays':>7} {'result':>8}")
    per_cracker = defaultdict(lambda: [0, 0])
    confusions = Counter()
    placeholder = False
    complete = []
    for tid, events in sorted(tests.items()):
        answers = [e for e in events if e["type"] == "answer"]
        done = next((e for e in events if e["type"] == "test_complete"), None)
        for a in answers:
            per_cracker[a["played"]][1] += 1
            if a["correct"]:
                per_cracker[a["played"]][0] += 1
            else:
                confusions[(a["played"], a["guess"])] += 1
            if "own-003" in a.get("audio_credits", ""):
                placeholder = True
        correct = sum(1 for a in answers if a["correct"])
        replays = sum(a.get("replays", 0) for a in answers)
        tester = int(events[0].get("tester", 0))
        if done:
            complete.append(done["passed"])
            result = "pass" if done["passed"] else "fail"
        else:
            result = "unfinished"
        print(f"{tid:17} {tester:6} {correct:4}/{len(answers):<2} {replays:7} {result:>8}")

    print("Per cracker (named correctly / heard):")
    for cid, (hit, n) in sorted(per_cracker.items(), key=lambda kv: kv[1][0] / max(1, kv[1][1])):
        print(f"  {cid:18} {hit}/{n}  {100 * hit / max(1, n):3.0f}%")
    if confusions:
        print("Most common mix-ups (heard -> named): " +
              ", ".join(f"{p} -> {g} x{n}" for (p, g), n in confusions.most_common(5)))
    if complete:
        passed = sum(complete)
        print(f"F03: {passed} of {len(complete)} tester(s) named {SOUND_PASS} of 8 or more")
    if placeholder:
        print("Note: some sounds were synthesised placeholders (own-003); record this run as provisional.")
    print()


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("dirs", nargs="*", help="folders to read for both reports (default: captures)")
    p.add_argument("--sessions", nargs="+", default=[], help="folders for the free-session report only")
    p.add_argument("--sound", nargs="+", default=[], help="folders for the sound test report only")
    p.add_argument("--min-minutes", type=float, default=0.0, help="ignore shorter sessions in the gate (e.g. 5)")
    args = p.parse_args()
    if not (args.dirs or args.sessions or args.sound):
        args.dirs = ["captures"]
    if args.dirs or args.sessions:
        sessions_report(args.dirs + args.sessions, args.min_minutes)
    if args.dirs or args.sound:
        sound_report(args.dirs + args.sound)


if __name__ == "__main__":
    main()

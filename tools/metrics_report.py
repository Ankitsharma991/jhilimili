#!/usr/bin/env python3
"""PRD 17 metrics report for the closed test and launch reviews (Phase 5 step 6, Docs/Phase5/CLOSED_TEST.md).

Input: events.csv from the BigQuery query in Docs/Phase5/metrics_query.sql (Firebase's free BigQuery
export). One row per event, with the columns user, ts, event, id, is_first, duration_s, ignitions,
ms_since_launch, placement, ms_since_payoff, sku, key, value, avg_fps, tier, downgrades.

Usage:
  tools/metrics_report.py events.csv --since 2026-11-01 --days 7 [--crashes N] [--installs installs.csv]
                          [--ecpm 0.50] [--out report.md]
  tools/metrics_report.py --self-test

--crashes: crashes in the period, from Crashlytics or Play Console Android vitals; crash-free sessions are
1 - crashes / (session_end events + crashes). --installs: Play Console's installs CSV export (UTF-16).
--ecpm: your measured AdMob eCPM in USD, for the PRD 15.3 revenue check.
"""
import argparse
import csv
import io
import os
import statistics
import sys
from collections import Counter, defaultdict
from datetime import date, datetime, timedelta, timezone

TARGETS = {
    "time_to_first_ignition_s": 30.0,
    "first_cracker_completion": 0.80,
    "ignitions_per_session": 8.0,
    "d1_retention": 0.20,
    "crash_free_sessions": 0.99,
}

# AdPolicy (Core/Monetisation): an interstitial needs 10 s since the last payoff. Rewarded ads are asked for.
INTERSTITIAL_PAYOFF_QUIET_MS = 10_000


def parse_ts(text):
    text = text.strip()
    if text.isdigit():  # BigQuery event_timestamp in microseconds
        return datetime.fromtimestamp(int(text) / 1_000_000, tz=timezone.utc)
    text = text.replace(" UTC", "+00:00").replace("Z", "+00:00")
    ts = datetime.fromisoformat(text)
    return ts if ts.tzinfo else ts.replace(tzinfo=timezone.utc)


def num(row, key):
    value = (row.get(key) or "").strip()
    if value == "":
        return None
    try:
        return float(value)
    except ValueError:
        return None


def load_events(path_or_rows, since, days):
    rows = path_or_rows if isinstance(path_or_rows, list) else list(csv.DictReader(open(path_or_rows, newline="")))
    start = datetime.combine(since, datetime.min.time(), tzinfo=timezone.utc)
    end = start + timedelta(days=days)
    events = []
    for row in rows:
        ts = parse_ts(row["ts"])
        if start <= ts < end:
            row = dict(row)
            row["_ts"] = ts
            events.append(row)
    events.sort(key=lambda r: r["_ts"])
    return events


def compute(events, crashes=None, ecpm=None):
    by_user = defaultdict(list)
    for e in events:
        by_user[e["user"]].append(e)

    m = {"users": len(by_user)}
    new_users = [u for u, evs in by_user.items() if any(e["event"] == "app_first_open" for e in evs)]
    m["new_users"] = len(new_users)

    # Time to first ignition: launch (scene_ready time minus its ms_since_launch) to the user's first ignition.
    ttfi = []
    for u in new_users:
        evs = by_user[u]
        ready = next((e for e in evs if e["event"] == "scene_ready"), None)
        first = next((e for e in evs if e["event"] == "cracker_ignited"), None)
        if ready and first and first["_ts"] >= ready["_ts"]:
            launch = ready["_ts"] - timedelta(milliseconds=num(ready, "ms_since_launch") or 0)
            ttfi.append((first["_ts"] - launch).total_seconds())
    m["new_users_who_ignited"] = sum(1 for u in new_users if any(e["event"] == "cracker_ignited" for e in by_user[u]))
    m["time_to_first_ignition_s"] = statistics.median(ttfi) if ttfi else None
    m["first_cracker_completion"] = (
        sum(1 for u in new_users if any(e["event"] == "payoff_complete" for e in by_user[u])) / len(new_users) if new_users else None)

    ends = [e for e in events if e["event"] == "session_end"]
    m["sessions"] = len(ends)
    ign = [num(e, "ignitions") for e in ends if num(e, "ignitions") is not None]
    dur = [num(e, "duration_s") for e in ends if num(e, "duration_s") is not None]
    m["ignitions_per_session"] = statistics.mean(ign) if ign else None
    m["median_session_s"] = statistics.median(dur) if dur else None
    m["sessions_per_user"] = len(ends) / len(by_user) if by_user else None

    # Retention: users first seen on day d who come back on d+1 (or d+7), for cohorts whose return day is in the data.
    last_day = max(e["_ts"].date() for e in events) if events else None
    first_day = {u: evs[0]["_ts"].date() for u, evs in by_user.items()}
    active = defaultdict(set)
    for e in events:
        active[e["user"]].add(e["_ts"].date())
    for n in (1, 7):
        cohort = [u for u, d in first_day.items() if last_day and d + timedelta(days=n) <= last_day]
        back = sum(1 for u in cohort if first_day[u] + timedelta(days=n) in active[u])
        m[f"d{n}_cohort"] = len(cohort)
        m[f"d{n}_retention"] = back / len(cohort) if cohort else None

    m["crashes"] = crashes
    m["crash_free_sessions"] = (1 - crashes / (len(ends) + crashes)) if crashes is not None and (len(ends) + crashes) > 0 else None

    shown = [e for e in events if e["event"] == "ad_shown"]
    m["ads_shown"] = len(shown)
    m["ad_rewards"] = sum(1 for e in events if e["event"] == "ad_reward")
    m["ad_failures"] = sum(1 for e in events if e["event"] == "ad_failed")
    m["ads_per_session"] = len(shown) / len(ends) if ends else None
    m["ad_safety_violations"] = sum(
        1 for e in shown
        if e.get("placement") == "natural_break" and (num(e, "ms_since_payoff") or 0) >= 0
        and (num(e, "ms_since_payoff") or 0) < INTERSTITIAL_PAYOFF_QUIET_MS)

    m["purchase_started_users"] = len({e["user"] for e in events if e["event"] == "purchase_started"})
    m["purchase_done_users"] = len({e["user"] for e in events if e["event"] == "purchase_done"})
    m["purchase_conversion"] = m["purchase_done_users"] / len(by_user) if by_user else None

    quiet = {e["user"] for e in events if e["event"] == "settings_changed" and e.get("key") == "quiet_mode" and e.get("value") == "on"}
    m["quiet_mode_users"] = len(quiet)

    fps = defaultdict(list)
    for e in events:
        if e["event"] == "perf_sample" and num(e, "avg_fps") is not None:
            fps[e.get("tier") or "?"].append(num(e, "avg_fps"))
    m["fps_by_tier"] = {t: (statistics.median(v), len(v)) for t, v in sorted(fps.items())}
    m["downgrade_samples"] = sum(1 for e in events if e["event"] == "perf_sample" and (num(e, "downgrades") or 0) > 0)

    # R1 (Phase 6) exit gate: do players keep an avatar? Share of sessions that end with one.
    with_flag = [e for e in ends if num(e, "has_avatar") is not None]
    m["sessions_with_avatar"] = sum(1 for e in with_flag if num(e, "has_avatar") >= 1) / len(with_flag) if with_flag else None
    m["creator_opens"] = sum(1 for e in events if e["event"] == "avatar_creator_opened")
    m["creator_first_opens"] = sum(1 for e in events if e["event"] == "avatar_creator_opened" and (num(e, "first") or 0) >= 1)
    saves = [e for e in events if e["event"] == "avatar_saved"]
    m["avatar_saves"] = len(saves)
    m["avatar_skips"] = sum(1 for e in events if e["event"] == "avatar_skipped")
    m["avatar_save_rate"] = len(saves) / (len(saves) + m["avatar_skips"]) if (saves or m["avatar_skips"]) else None
    m["avatar_from_preset"] = sum(1 for e in saves if (num(e, "from_preset") or 0) >= 1) / len(saves) if saves else None
    worn = Counter()
    for e in saves:
        for key in ("top", "bottom", "full_length", "full", "headwear"):
            v = (e.get(key) or "").strip()
            if v and v != "none":
                worn[v] += 1
    m["top_garments"] = worn.most_common(8)

    # R1 (Phase 7) exit gate: decoration use per user and the return rate. Decorators are users who opened decoration
    # mode; their D1 and D7 return is compared with everyone else's.
    decorators = {e["user"] for e in events if e["event"] == "decor_mode_opened"}
    placed = [e for e in events if e["event"] == "decoration_placed"]
    m["decorators"] = len(decorators)
    m["decorator_share"] = len(decorators) / len(by_user) if by_user else None
    m["placements_per_decorator"] = len(placed) / len(decorators) if decorators else None
    m["decor_visits_per_decorator"] = sum(1 for e in events if e["event"] == "decor_mode_opened") / len(decorators) if decorators else None
    closes = [e for e in events if e["event"] == "decor_mode_closed"]
    m["decor_visits_saved"] = sum(1 for e in closes if (num(e, "saved") or 0) >= 1) / len(closes) if closes else None
    with_count = [e for e in ends if num(e, "decorations") is not None]
    m["sessions_with_decorations"] = sum(1 for e in with_count if num(e, "decorations") >= 1) / len(with_count) if with_count else None
    m["house_restores"] = sum(1 for e in events if e["event"] == "house_restored")
    m["layout_saves"] = sum(1 for e in events if e["event"] == "layout_saved")
    for n in (1, 7):
        for group, users in (("decorators", decorators), ("others", set(by_user) - decorators)):
            cohort = [u for u in users if last_day and first_day[u] + timedelta(days=n) <= last_day]
            back = sum(1 for u in cohort if first_day[u] + timedelta(days=n) in active[u])
            m[f"d{n}_retention_{group}"] = back / len(cohort) if cohort else None
    m["top_decorations"] = Counter(e.get("id") for e in placed if e.get("id")).most_common(8)
    m["top_modules"] = Counter(e.get("module") for e in events if e["event"] == "house_module_changed" and e.get("module")).most_common(6)

    # R2 (Phase 8) exit gate: session length and retention improve against the R0 cohort. Walkers (users who went for a
    # walk) are compared with everyone else in the same window; an earlier R0 export gives the R0 cohort itself.
    walkers = {e["user"] for e in events if e["event"] == "walk_started"}
    walks = [e for e in events if e["event"] == "walk_ended"]
    m["walkers"] = len(walkers)
    m["walker_share"] = len(walkers) / len(by_user) if by_user else None
    m["walks_per_walker"] = sum(1 for e in events if e["event"] == "walk_started") / len(walkers) if walkers else None
    secs = [num(e, "seconds") for e in walks if num(e, "seconds") is not None]
    m["median_walk_s"] = statistics.median(secs) if secs else None
    for group, users in (("walkers", walkers), ("others", set(by_user) - walkers)):
        d = [num(e, "duration_s") for e in ends if e["user"] in users and num(e, "duration_s") is not None]
        m[f"median_session_s_{group}"] = statistics.median(d) if d else None
        for n in (1, 7):
            cohort = [u for u in users if last_day and first_day[u] + timedelta(days=n) <= last_day]
            back = sum(1 for u in cohort if first_day[u] + timedelta(days=n) in active[u])
            m[f"d{n}_retention_{group}"] = back / len(cohort) if cohort else None
    m["spot_kinds"] = Counter(e.get("kind") for e in events if e["event"] == "spot_chosen" and e.get("kind")).most_common(9)
    m["environments"] = Counter(e.get("environment") for e in ends if e.get("environment")).most_common(5)
    m["fast_travels"] = sum(1 for e in events if e["event"] == "fast_travel")

    # PRD 15.3: sessions per user x impressions per session x eCPM / 1000.
    if ecpm is not None and m["sessions_per_user"] is not None and m["ads_per_session"] is not None:
        m["ad_revenue_per_user_usd"] = m["sessions_per_user"] * m["ads_per_session"] * ecpm / 1000
    else:
        m["ad_revenue_per_user_usd"] = None
    return m


def load_installs(path):
    raw = open(path, "rb").read()
    for encoding in ("utf-16", "utf-8-sig"):
        try:
            text = raw.decode(encoding)
            break
        except UnicodeDecodeError:
            continue
    rows = list(csv.DictReader(io.StringIO(text)))
    def col(row, *names):
        for name in names:
            for k in row:
                if k and k.strip().lower() == name.lower():
                    try:
                        return int(float(row[k] or 0))
                    except ValueError:
                        return 0
        return 0
    installs = sum(col(r, "Daily User Installs", "Daily Device Installs") for r in rows)
    uninstalls = sum(col(r, "Daily User Uninstalls", "Daily Device Uninstalls") for r in rows)
    return {"installs": installs, "uninstalls": uninstalls, "days": len(rows)}


def fmt(value, kind):
    if value is None:
        return "no data"
    if kind == "pct":
        return f"{value * 100:.1f}%"
    if kind == "s":
        return f"{value:.0f} s"
    if kind == "usd":
        return f"${value:.4f}"
    return f"{value:.1f}" if isinstance(value, float) else str(value)


def verdict(value, target, higher_is_better=True):
    if value is None:
        return "–"
    ok = value >= target if higher_is_better else value <= target
    return "✅" if ok else "❌"


def report(m, since, days, installs=None):
    lines = [f"# Metrics report: {since.isoformat()} + {days} day(s)", ""]
    lines += [f"Users {m['users']} (new {m['new_users']}), sessions {m['sessions']}, "
              f"sessions per user {fmt(m['sessions_per_user'], 'n')}.", ""]
    if installs:
        lines += [f"Play Console: {installs['installs']} installs, {installs['uninstalls']} uninstalls over {installs['days']} day(s).", ""]
    lines += ["## PRD 17 targets", "", "| Metric | Value | Target | |", "|---|---|---|---|"]
    lines.append(f"| Time to first ignition (median, new users) | {fmt(m['time_to_first_ignition_s'], 's')} | 30 s or less | {verdict(m['time_to_first_ignition_s'], 30, False)} |")
    lines.append(f"| First-cracker completion | {fmt(m['first_cracker_completion'], 'pct')} | 80% or more | {verdict(m['first_cracker_completion'], 0.8)} |")
    lines.append(f"| Ignitions per session | {fmt(m['ignitions_per_session'], 'n')} | 8 or more | {verdict(m['ignitions_per_session'], 8)} |")
    lines.append(f"| D1 retention (cohort {m['d1_cohort']}) | {fmt(m['d1_retention'], 'pct')} | about 20% | {verdict(m['d1_retention'], 0.2)} |")
    lines.append(f"| D7 retention (cohort {m['d7_cohort']}) | {fmt(m['d7_retention'], 'pct')} | secondary | |")
    lines.append(f"| Crash-free sessions | {fmt(m['crash_free_sessions'], 'pct')} | 99% or more | {verdict(m['crash_free_sessions'], 0.99)} |")
    lines += ["", "## Play and ads", "", "| Metric | Value |", "|---|---|"]
    lines.append(f"| Median session length | {fmt(m['median_session_s'], 's')} |")
    lines.append(f"| Ads shown / rewarded / failed | {m['ads_shown']} / {m['ad_rewards']} / {m['ad_failures']} |")
    lines.append(f"| Ads per session | {fmt(m['ads_per_session'], 'n')} |")
    lines.append(f"| Interstitials within 10 s of a payoff (must be 0) | {m['ad_safety_violations']} {'✅' if m['ad_safety_violations'] == 0 else '❌'} |")
    lines.append(f"| Purchase started / done (users) | {m['purchase_started_users']} / {m['purchase_done_users']} ({fmt(m['purchase_conversion'], 'pct')} of users) |")
    lines.append(f"| Users who turned on Quiet mode | {m['quiet_mode_users']} |")
    lines.append(f"| Perf samples with a quality downgrade | {m['downgrade_samples']} |")
    for tier, (median_fps, n) in m["fps_by_tier"].items():
        lines.append(f"| Median fps, tier {tier} ({n} samples) | {median_fps:.0f} |")
    lines.append(f"| Ad revenue per user (PRD 15.3) | {fmt(m['ad_revenue_per_user_usd'], 'usd')} |")
    lines += ["", "## Avatar (R1, Phase 6 exit gate)", "", "| Metric | Value |", "|---|---|"]
    lines.append(f"| Sessions ending with an avatar | {fmt(m['sessions_with_avatar'], 'pct')} |")
    lines.append(f"| Creator opened (first time) | {m['creator_opens']} ({m['creator_first_opens']}) |")
    lines.append(f"| Saved / skipped | {m['avatar_saves']} / {m['avatar_skips']} ({fmt(m['avatar_save_rate'], 'pct')} saved) |")
    lines.append(f"| Saves that started from a preset | {fmt(m['avatar_from_preset'], 'pct')} |")
    if m["top_garments"]:
        lines.append(f"| Most worn | {', '.join(f'{g} ({n})' for g, n in m['top_garments'])} |")
    lines += ["", "## House and decorations (R1, Phase 7 exit gate)", "", "| Metric | Value |", "|---|---|"]
    lines.append(f"| Users who decorated | {m['decorators']} ({fmt(m['decorator_share'], 'pct')} of users) |")
    lines.append(f"| Decorations placed per decorator | {fmt(m['placements_per_decorator'], 'n')} |")
    lines.append(f"| Decoration visits per decorator | {fmt(m['decor_visits_per_decorator'], 'n')} ({fmt(m['decor_visits_saved'], 'pct')} saved) |")
    lines.append(f"| Sessions ending with a decorated courtyard | {fmt(m['sessions_with_decorations'], 'pct')} |")
    lines.append(f"| D1 return, decorators / others | {fmt(m['d1_retention_decorators'], 'pct')} / {fmt(m['d1_retention_others'], 'pct')} |")
    lines.append(f"| D7 return, decorators / others | {fmt(m['d7_retention_decorators'], 'pct')} / {fmt(m['d7_retention_others'], 'pct')} |")
    lines.append(f"| Layouts saved / interrupted edits restored | {m['layout_saves']} / {m['house_restores']} |")
    if m["top_decorations"]:
        lines.append(f"| Most placed | {', '.join(f'{g} ({n})' for g, n in m['top_decorations'])} |")
    if m["top_modules"]:
        lines.append(f"| Most chosen house parts | {', '.join(f'{g} ({n})' for g, n in m['top_modules'])} |")
    lines += ["", "## Neighbourhood (R2, Phase 8 exit gate)", "", "| Metric | Value |", "|---|---|"]
    lines.append(f"| Users who walked | {m['walkers']} ({fmt(m['walker_share'], 'pct')} of users) |")
    lines.append(f"| Walks per walker / median walk | {fmt(m['walks_per_walker'], 'n')} / {fmt(m['median_walk_s'], 's')} |")
    lines.append(f"| Median session, walkers / others | {fmt(m['median_session_s_walkers'], 's')} / {fmt(m['median_session_s_others'], 's')} |")
    lines.append(f"| D1 return, walkers / others | {fmt(m['d1_retention_walkers'], 'pct')} / {fmt(m['d1_retention_others'], 'pct')} |")
    lines.append(f"| D7 return, walkers / others | {fmt(m['d7_retention_walkers'], 'pct')} / {fmt(m['d7_retention_others'], 'pct')} |")
    lines.append(f"| Fast travels | {m['fast_travels']} |")
    if m["spot_kinds"]:
        lines.append(f"| Celebration spots chosen | {', '.join(f'{k} ({n})' for k, n in m['spot_kinds'])} |")
    if m["environments"]:
        lines.append(f"| Environments at session end | {', '.join(f'{k} ({n})' for k, n in m['environments'])} |")
    lines.append("| Against the R0 cohort | Compare the median session and D1/D7 above with an R0-only export of the same length |")
    lines += ["", "Small tests (20 to 50 users) can judge feel, time to ignition, completion and crashes. "
              "Retention, conversion and ad revenue need hundreds to thousands of users (PRD 17)."]
    return "\n".join(lines)


def self_test():
    rows = []
    t0 = datetime(2026, 11, 1, 18, 0, tzinfo=timezone.utc)

    def add(user, at, event, **params):
        row = {"user": user, "ts": at.isoformat(), "event": event}
        row.update({k: str(v) for k, v in params.items()})
        rows.append(row)

    # Four new users on day 0. u1 and u2 come back on day 1; u1 also on day 7.
    for i, user in enumerate(["u1", "u2", "u3", "u4"]):
        start = t0 + timedelta(minutes=i)
        add(user, start, "app_first_open", tier="mid", locale="en_IN")
        add(user, start + timedelta(seconds=2), "scene_ready", ms_since_launch=2000)
        if user != "u4":  # u4 never lights anything
            add(user, start + timedelta(seconds=10 + 10 * i), "cracker_ignited", id="flowerpot_gold", is_first=1)
            add(user, start + timedelta(seconds=20 + 10 * i), "payoff_complete", id="flowerpot_gold")
        add(user, start + timedelta(minutes=5), "session_end", duration_s=300, ignitions=10 if user != "u4" else 0)
        add(user, start + timedelta(minutes=1), "perf_sample", avg_fps=58.5, tier="mid", downgrades=0)
    for user, day in (("u1", 1), ("u2", 1), ("u1", 7)):
        at = t0 + timedelta(days=day)
        add(user, at, "scene_ready", ms_since_launch=1500)
        add(user, at + timedelta(minutes=4), "session_end", duration_s=240, ignitions=12)
    add("u1", t0 + timedelta(minutes=2), "ad_shown", placement="unlock_peacock_fountain", ms_since_payoff=4000)
    add("u1", t0 + timedelta(minutes=2, seconds=30), "ad_reward", placement="unlock_peacock_fountain", ms_since_payoff=34000)
    add("u2", t0 + timedelta(minutes=3), "ad_shown", placement="natural_break", ms_since_payoff=5000)  # a violation
    add("u3", t0 + timedelta(minutes=3), "purchase_started", sku="remove_ads")
    add("u3", t0 + timedelta(minutes=3, seconds=20), "purchase_done", sku="remove_ads")
    add("u2", t0 + timedelta(minutes=4), "settings_changed", key="quiet_mode", value="on")
    add("u9", t0 + timedelta(days=30), "session_end", duration_s=1, ignitions=1)  # outside the window
    add("u1", t0 + timedelta(minutes=1), "avatar_creator_opened", first=1)
    add("u1", t0 + timedelta(minutes=1, seconds=40), "avatar_saved", from_preset=1, garments=5, top="kurta", bottom="dhoti", full_length="none", headwear="turban")
    add("u2", t0 + timedelta(minutes=1), "avatar_creator_opened", first=1)
    add("u2", t0 + timedelta(minutes=1, seconds=20), "avatar_skipped")
    # u1 decorates twice (two visits, three placements); u3 once. u1 comes back on day 1, u3 does not.
    add("u1", t0 + timedelta(minutes=2), "decor_mode_opened", decorations=0)
    add("u1", t0 + timedelta(minutes=2, seconds=10), "decoration_placed", id="diya_clay", category="lamps")
    add("u1", t0 + timedelta(minutes=2, seconds=20), "decoration_placed", id="diya_clay", category="lamps")
    add("u1", t0 + timedelta(minutes=2, seconds=30), "house_module_changed", point="door", module="door_carved_arch")
    add("u1", t0 + timedelta(minutes=2, seconds=40), "decor_mode_closed", edits=3, saved=1, decorations=2)
    add("u1", t0 + timedelta(days=1, minutes=1), "decor_mode_opened", decorations=2)
    add("u1", t0 + timedelta(days=1, minutes=1, seconds=5), "decoration_placed", id="rangoli_lotus", category="rangoli")
    add("u1", t0 + timedelta(days=1, minutes=1, seconds=9), "decor_mode_closed", edits=1, saved=0, decorations=3)
    add("u3", t0 + timedelta(minutes=4), "decor_mode_opened", decorations=0)
    add("u3", t0 + timedelta(minutes=4, seconds=10), "house_restored", lines=4)
    # u1 walks twice and chooses the chowk; u2 walks once.
    add("u1", t0 + timedelta(minutes=3), "walk_started", environment="town_lane")
    add("u1", t0 + timedelta(minutes=3, seconds=30), "fast_travel", spot="open")
    add("u1", t0 + timedelta(minutes=3, seconds=40), "spot_chosen", spot="open", kind="square", environment="town_lane")
    add("u1", t0 + timedelta(minutes=3, seconds=41), "walk_ended", seconds=41, metres=30)
    add("u1", t0 + timedelta(days=1, minutes=2), "walk_started", environment="town_lane")
    add("u1", t0 + timedelta(days=1, minutes=3), "walk_ended", seconds=60, metres=55)
    add("u2", t0 + timedelta(minutes=2), "walk_started", environment="town_lane")
    add("u2", t0 + timedelta(minutes=2, seconds=20), "walk_ended", seconds=20, metres=12)

    events = load_events(rows, t0.date(), 8)
    m = compute(events, crashes=1, ecpm=2.0)
    checks = {
        "users": (m["users"], 4),
        "new_users": (m["new_users"], 4),
        # Launch is 0 s, scene ready 2 s; ignitions at 10, 20, 30 s after the user's start: median 20 s.
        "time_to_first_ignition_s": (m["time_to_first_ignition_s"], 20.0),
        "first_cracker_completion": (m["first_cracker_completion"], 0.75),
        "sessions": (m["sessions"], 7),
        "ignitions_per_session": (round(m["ignitions_per_session"], 4), round((30 + 0 + 36) / 7, 4)),
        "d1_retention": (m["d1_retention"], 0.5),
        "d7_retention": (m["d7_retention"], 0.25),
        "crash_free_sessions": (m["crash_free_sessions"], 1 - 1 / 8),
        "ads_shown": (m["ads_shown"], 2),
        "ad_safety_violations": (m["ad_safety_violations"], 1),
        "purchase_done_users": (m["purchase_done_users"], 1),
        "quiet_mode_users": (m["quiet_mode_users"], 1),
        "avatar_save_rate": (m["avatar_save_rate"], 0.5),
        "avatar_from_preset": (m["avatar_from_preset"], 1.0),
        "creator_first_opens": (m["creator_first_opens"], 2),
        "top_garment": (m["top_garments"][0][0] if m["top_garments"] else None, "kurta"),
        "decorators": (m["decorators"], 2),
        "decorator_share": (m["decorator_share"], 0.5),
        "placements_per_decorator": (m["placements_per_decorator"], 1.5),
        "decor_visits_saved": (m["decor_visits_saved"], 0.5),
        "d1_retention_decorators": (m["d1_retention_decorators"], 0.5),
        "d1_retention_others": (m["d1_retention_others"], 0.5),
        "top_decoration": (m["top_decorations"][0][0] if m["top_decorations"] else None, "diya_clay"),
        "house_restores": (m["house_restores"], 1),
        "walkers": (m["walkers"], 2),
        "walks_per_walker": (m["walks_per_walker"], 1.5),
        "median_walk_s": (m["median_walk_s"], 41),
        "d1_retention_walkers": (m["d1_retention_walkers"], 1.0),
        "d1_retention_others": (m["d1_retention_others"], 0.0),
        "top_spot_kind": (m["spot_kinds"][0][0] if m["spot_kinds"] else None, "square"),
        # 7 sessions / 4 users x 2 ads / 7 sessions x $2 / 1000.
        "ad_revenue_per_user_usd": (round(m["ad_revenue_per_user_usd"], 6), round(7 / 4 * 2 / 7 * 2 / 1000, 6)),
    }
    failed = [f"{k}: got {got}, expected {want}" for k, (got, want) in checks.items() if got != want]
    report(m, t0.date(), 8)
    if failed:
        print("SELF-TEST FAILED\n" + "\n".join(failed))
        return 1
    print(f"self-test passed ({len(checks)} checks)")
    return 0


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("events", nargs="?")
    parser.add_argument("--since", type=date.fromisoformat)
    parser.add_argument("--days", type=int, default=7)
    parser.add_argument("--crashes", type=int)
    parser.add_argument("--installs")
    parser.add_argument("--ecpm", type=float)
    parser.add_argument("--out")
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    if args.self_test:
        return self_test()
    if not args.events or not args.since:
        parser.error("events.csv and --since are required")
    m = compute(load_events(args.events, args.since, args.days), args.crashes, args.ecpm)
    text = report(m, args.since, args.days, load_installs(args.installs) if args.installs else None)
    if args.out:
        os.makedirs(os.path.dirname(args.out) or ".", exist_ok=True)
        open(args.out, "w").write(text + "\n")
        print(f"Saved {args.out}")
    print(text)
    return 0


if __name__ == "__main__":
    sys.exit(main())

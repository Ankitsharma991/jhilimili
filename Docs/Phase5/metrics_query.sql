-- PRD 17 events from Firebase's BigQuery export, one row per event, for tools/metrics_report.py.
-- Phase 5 step 6, Docs/Phase5/CLOSED_TEST.md.
-- 1. Firebase console → Project settings → Integrations → BigQuery → Link (the free sandbox is enough;
--    the daily export starts the day after linking).
-- 2. Replace PROJECT and analytics_000000000 with your dataset (BigQuery console, left panel), and the dates.
-- 3. Run it, then Save results → CSV (local file) as events.csv.
SELECT
  user_pseudo_id AS user,
  FORMAT_TIMESTAMP('%Y-%m-%dT%H:%M:%E3SZ', TIMESTAMP_MICROS(event_timestamp)) AS ts,
  event_name AS event,
  (SELECT value.string_value FROM UNNEST(event_params) WHERE key = 'id') AS id,
  (SELECT COALESCE(value.int_value, CAST(value.double_value AS INT64)) FROM UNNEST(event_params) WHERE key = 'is_first') AS is_first,
  (SELECT COALESCE(value.double_value, value.int_value) FROM UNNEST(event_params) WHERE key = 'duration_s') AS duration_s,
  (SELECT COALESCE(value.int_value, CAST(value.double_value AS INT64)) FROM UNNEST(event_params) WHERE key = 'ignitions') AS ignitions,
  (SELECT COALESCE(value.int_value, CAST(value.double_value AS INT64)) FROM UNNEST(event_params) WHERE key = 'ms_since_launch') AS ms_since_launch,
  (SELECT value.string_value FROM UNNEST(event_params) WHERE key = 'placement') AS placement,
  (SELECT COALESCE(value.int_value, CAST(value.double_value AS INT64)) FROM UNNEST(event_params) WHERE key = 'ms_since_payoff') AS ms_since_payoff,
  (SELECT value.string_value FROM UNNEST(event_params) WHERE key = 'sku') AS sku,
  (SELECT value.string_value FROM UNNEST(event_params) WHERE key = 'key') AS key,
  (SELECT value.string_value FROM UNNEST(event_params) WHERE key = 'value') AS value,
  (SELECT COALESCE(value.double_value, value.int_value) FROM UNNEST(event_params) WHERE key = 'avg_fps') AS avg_fps,
  (SELECT value.string_value FROM UNNEST(event_params) WHERE key = 'tier') AS tier,
  (SELECT COALESCE(value.int_value, CAST(value.double_value AS INT64)) FROM UNNEST(event_params) WHERE key = 'downgrades') AS downgrades
FROM `PROJECT.analytics_000000000.events_*`
WHERE _TABLE_SUFFIX BETWEEN '20261101' AND '20261131'
  AND event_name IN ('app_first_open', 'scene_ready', 'cracker_placed', 'cracker_ignited', 'payoff_complete',
                     'session_end', 'settings_changed', 'ad_shown', 'ad_failed', 'ad_reward',
                     'purchase_started', 'purchase_done', 'clip_shared', 'perf_sample')
ORDER BY user, ts

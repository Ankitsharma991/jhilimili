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
  (SELECT COALESCE(value.int_value, CAST(value.double_value AS INT64)) FROM UNNEST(event_params) WHERE key = 'downgrades') AS downgrades,
  -- R1 (Phase 6) avatar events
  (SELECT COALESCE(value.int_value, CAST(value.double_value AS INT64)) FROM UNNEST(event_params) WHERE key = 'first') AS first,
  (SELECT COALESCE(value.int_value, CAST(value.double_value AS INT64)) FROM UNNEST(event_params) WHERE key = 'from_preset') AS from_preset,
  (SELECT COALESCE(value.int_value, CAST(value.double_value AS INT64)) FROM UNNEST(event_params) WHERE key = 'garments') AS garments,
  (SELECT value.string_value FROM UNNEST(event_params) WHERE key = 'top') AS top,
  (SELECT value.string_value FROM UNNEST(event_params) WHERE key = 'bottom') AS bottom,
  (SELECT value.string_value FROM UNNEST(event_params) WHERE key = 'full') AS full_length,
  (SELECT value.string_value FROM UNNEST(event_params) WHERE key = 'headwear') AS headwear,
  (SELECT COALESCE(value.int_value, CAST(value.double_value AS INT64)) FROM UNNEST(event_params) WHERE key = 'has_avatar') AS has_avatar,
  -- R1 (Phase 7) house and decoration events
  (SELECT COALESCE(value.int_value, CAST(value.double_value AS INT64)) FROM UNNEST(event_params) WHERE key = 'decorations') AS decorations,
  (SELECT COALESCE(value.int_value, CAST(value.double_value AS INT64)) FROM UNNEST(event_params) WHERE key = 'edits') AS edits,
  (SELECT COALESCE(value.int_value, CAST(value.double_value AS INT64)) FROM UNNEST(event_params) WHERE key = 'saved') AS saved,
  (SELECT value.string_value FROM UNNEST(event_params) WHERE key = 'category') AS category,
  (SELECT value.string_value FROM UNNEST(event_params) WHERE key = 'point') AS point,
  (SELECT value.string_value FROM UNNEST(event_params) WHERE key = 'module') AS module,
  (SELECT COALESCE(value.int_value, CAST(value.double_value AS INT64)) FROM UNNEST(event_params) WHERE key = 'slot') AS slot,
  (SELECT COALESCE(value.int_value, CAST(value.double_value AS INT64)) FROM UNNEST(event_params) WHERE key = 'lines') AS lines
FROM `PROJECT.analytics_000000000.events_*`
WHERE _TABLE_SUFFIX BETWEEN '20261101' AND '20261131'
  AND event_name IN ('app_first_open', 'scene_ready', 'cracker_placed', 'cracker_ignited', 'payoff_complete',
                     'session_end', 'settings_changed', 'ad_shown', 'ad_failed', 'ad_reward',
                     'purchase_started', 'purchase_done', 'clip_shared', 'perf_sample',
                     'avatar_creator_opened', 'avatar_saved', 'avatar_skipped',
                     'decor_mode_opened', 'decor_mode_closed', 'decoration_placed', 'house_module_changed',
                     'layout_saved', 'layout_loaded', 'house_restored')
ORDER BY user, ts

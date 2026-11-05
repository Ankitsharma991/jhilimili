using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Unity.Profiling;
using UnityEngine;
using UtsavNights.Core.Diagnostics;
using UtsavNights.Core.District;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;

namespace UtsavNights.UI
{
    /// <summary>
    /// The Explore bench (Phase 12 step 8): a scripted tour of the open world, the same every time, for measuring on
    /// phones. Five legs: the courtyard as a baseline, sprinting down the bazaar road, standing by the stalls turning the
    /// camera, riding the scooter through the traffic, and the district's show on the chowk with the crowd watching. Each
    /// leg records frame times (average, p50, p95, p99, the largest, spikes), garbage a frame, the most memory, crackers
    /// on the scene and any the limit turned away, people and vehicles, and checks them against
    /// <see cref="ExploreBudget"/>. Results go to persistentDataPath/explore_bench.jsonl and logcat with the tag
    /// [UTSAV_BENCH]. Started from the developer overlay ("Explore bench") or by launching the app with the extra
    /// utsav_bench=explore (tools/explore_bench.sh). Not in store builds.
    /// </summary>
    public sealed class ExploreBench : MonoBehaviour
    {
        public const string LogTag = "[UTSAV_BENCH]";
        public const string FileName = "explore_bench.jsonl";
        public const string IntentExtra = "utsav_bench";
        public const int MaxFrames = 8192;

        public static readonly string[] Legs = { "courtyard", "bazaar_walk", "bazaar_stand", "bazaar_ride", "chowk_show" };

        private readonly float[] _frameMs = new float[MaxFrames];
        private readonly List<BenchLeg> _results = new List<BenchLeg>();
        private ProfilerRecorder _gc;
        private ProfilerRecorder _memory;
        private string _run = string.Empty;
        private int _tier;

        /// <summary>Shortens every leg (the Play Mode test runs it at a fraction).</summary>
        public float Scale { get; set; } = 1f;

        public bool Running { get; private set; }

        public IReadOnlyList<BenchLeg> Results => _results;

        /// <summary>Whether every leg held to its budgets (only meaningful once a run is done).</summary>
        public bool AllOk { get; private set; }

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        private void Start()
        {
            if (BuildFlavor.IsStore)
            {
                enabled = false;
                return;
            }

            DevOverlay overlay = FindFirstObjectByType<DevOverlay>();
            overlay?.AddTool("Explore bench", () => Run());
            if (LaunchExtra() == "explore")
            {
                StartCoroutine(AutoStart());
            }
        }

        private void OnDestroy()
        {
            _gc.Dispose();
            _memory.Dispose();
        }

        /// <summary>Starts the tour. False when one is running, or in a store build.</summary>
        public bool Run()
        {
            if (Running || BuildFlavor.IsStore)
            {
                return false;
            }

            StartCoroutine(RunAll());
            return true;
        }

        private IEnumerator AutoStart()
        {
            // Let the scene settle (first-run screens, the SDKs starting) before measuring.
            float end = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
            }

            Run();
        }

        private IEnumerator RunAll()
        {
            Running = true;
            AllOk = false;
            _results.Clear();
            _run = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            if (!_gc.Valid)
            {
                _gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            }

            if (!_memory.Valid)
            {
                _memory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory", 1);
            }

            ExploreMode mode = FindFirstObjectByType<ExploreMode>();
            QualityManager quality = FindFirstObjectByType<QualityManager>();
            _tier = quality != null ? quality.Tier : 0;
            Debug.Log($"{LogTag} start run={_run} device={SystemInfo.deviceModel} tier={_tier} ram_mb={SystemInfo.systemMemorySize} gpu={SystemInfo.graphicsDeviceName}");
            if (mode == null)
            {
                Finish("no explore mode");
                yield break;
            }

            yield return Leg(Legs[0], 10f, null);
            if (!mode.Open())
            {
                Finish("explore did not open");
                yield break;
            }

            Game.Services.TryGet(out DistrictView district);
            Game.Services.TryGet(out TownFireworks town);
            ExploreController c = mode.Controller;

            // Sprinting east down the bazaar road: tiles streaming, the crowd and stalls going by.
            yield return Settle(c, district, new Vector3(60f, 0f, 6.8f), 90f);
            mode.SetSprint(true);
            mode.Stick.Set(new Vector2(0f, 1f));
            yield return Leg(Legs[1], 14f, null);
            mode.Stick.Set(Vector2.zero);
            mode.SetSprint(false);

            // Standing by the stalls, turning the camera round: the most of the town in view.
            yield return Settle(c, district, new Vector3(60f, 0f, 9.8f), 90f);
            yield return Leg(Legs[2], 10f, () => c.Look(new Vector2(240f * Time.unscaledDeltaTime, 0f)));

            // On the scooter at full speed through the traffic.
            yield return Settle(c, district, new Vector3(48f, 0f, 7f), 90f);
            if (c.Mount())
            {
                mode.Stick.Set(new Vector2(0f, 1f));
                yield return Leg(Legs[3], 12f, null);
                mode.Stick.Set(Vector2.zero);
                c.Dismount();
            }

            // The district's show on the chowk, the crowd gathering to watch.
            TownShowPlace chowk = TownShows.Places(DistrictGenerator.Bazaar)[1];
            yield return Settle(c, district, new Vector3(chowk.WatchX - 22f, 0f, chowk.WatchZ - 20f), 45f);
            int refused = town != null ? town.Refused : 0;
            if (town != null && town.StartShow(1))
            {
                BenchLeg show = null;
                yield return Leg(Legs[4], TownShows.Length, null, l => show = l);
                if (show != null)
                {
                    show.Refused = town.Refused - refused;
                }
            }

            mode.Exit();
            Finish(string.Empty);
        }

        /// <summary>Puts the player somewhere and lets the district stream round them for a moment.</summary>
        private IEnumerator Settle(ExploreController c, DistrictView district, Vector3 at, float facing)
        {
            c.Teleport(at, facing);
            district?.Refresh();
            float end = Time.realtimeSinceStartup + Mathf.Max(0.3f, 1.5f * Scale);
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
        }

        private IEnumerator Leg(string name, float seconds, Action everyFrame, Action<BenchLeg> done = null)
        {
            seconds *= Scale;
            var leg = new BenchLeg { Name = name };
            int frames = 0;
            double gcSum = 0;
            float gcMax = 0f, memMax = 0f;
            bool gcOk = _gc.Valid, memOk = _memory.Valid;
            float start = Time.realtimeSinceStartup;
            yield return null;
            while (Time.realtimeSinceStartup - start < seconds && frames < MaxFrames)
            {
                everyFrame?.Invoke();
                yield return null;
                _frameMs[frames++] = Time.unscaledDeltaTime * 1000f;
                if (gcOk)
                {
                    float kb = _gc.LastValue / 1024f;
                    gcSum += kb;
                    gcMax = Mathf.Max(gcMax, kb);
                }

                if (memOk)
                {
                    memMax = Mathf.Max(memMax, _memory.LastValue / (1024f * 1024f));
                }

                if (Game.Services.TryGet(out CrackerSpawner spawner))
                {
                    leg.Crackers = Mathf.Max(leg.Crackers, spawner.ActiveCount);
                }
            }

            leg.Seconds = Time.realtimeSinceStartup - start;
            ExploreBudget.FillFrames(leg, _frameMs, frames);
            if (gcOk && frames > 0)
            {
                leg.GcKbAvg = (float)(gcSum / frames);
                leg.GcKbMax = gcMax;
            }

            if (memOk && memMax > 0f)
            {
                leg.MemoryMb = memMax;
            }

            if (Game.Services.TryGet(out CrowdView crowd) && crowd.Sim != null && crowd.isActiveAndEnabled)
            {
                leg.People = crowd.Sim.People.Length;
                leg.PeopleDrawn = crowd.Drawn;
            }

            TrafficView traffic = FindFirstObjectByType<TrafficView>();
            if (traffic != null && traffic.isActiveAndEnabled)
            {
                leg.Vehicles = traffic.Drawn;
            }

            _results.Add(leg);
            done?.Invoke(leg);
            Write(ExploreBudget.ToJson(leg, _run, SystemInfo.deviceModel, _tier));
        }

        private void Finish(string error)
        {
            bool ok = string.IsNullOrEmpty(error) && _results.Count == Legs.Length;
            foreach (BenchLeg leg in _results)
            {
                ok &= ExploreBudget.Misses(leg, _tier).Count == 0;
            }

            AllOk = ok;
            string line = $"{{\"run\":\"{_run}\",\"done\":true,\"legs\":{_results.Count},\"ok\":{(ok ? "true" : "false")},\"error\":\"{error}\"}}";
            Write(line);
            Debug.Log($"{LogTag} done ok={(ok ? "true" : "false")} legs={_results.Count}{(string.IsNullOrEmpty(error) ? string.Empty : " error=" + error)}");
            Running = false;
        }

        private static void Write(string line)
        {
            Debug.Log($"{LogTag} {line}");
            try
            {
                LocalLog.Append(FilePath, line);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{LogTag} could not write results: {e.Message}");
            }
        }

        /// <summary>The utsav_bench extra the app was launched with (adb shell am start ... --es utsav_bench explore), or null.</summary>
        private static string LaunchExtra()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using AndroidJavaObject intent = activity.Call<AndroidJavaObject>("getIntent");
                return intent?.Call<string>("getStringExtra", IntentExtra);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{LogTag} could not read the launch extra: {e.Message}");
                return null;
            }
#else
            return null;
#endif
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UtsavNights.Content;
using UtsavNights.Core.Neighbourhood;
using UtsavNights.Core.Placement;
using UtsavNights.Core.Runtime;
using UtsavNights.Core.Services;
using UtsavNights.Core.Shows;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Plays a show at the current celebration spot (Phase 9 step 4, F22): each cracker is set on its pad a second before
    /// its cue and lit on the cue, and the same show can be replayed. Also keeps the saved shows (shows.json).
    /// </summary>
    [DefaultExecutionOrder(-110)]
    public sealed class ShowPlayer : MonoBehaviour, ITickable
    {
        public enum PlayerState
        {
            Idle,
            Running,
        }

        [SerializeField] private CrackerCatalog catalog;

        private readonly List<ShowCue> _order = new List<ShowCue>();
        private readonly List<CrackerRuntime> _spawned = new List<CrackerRuntime>();
        private readonly Dictionary<string, CrackerTiming?> _timings = new Dictionary<string, CrackerTiming?>();
        private CrackerSpawner _spawner;
        private ShowDef _show;
        private float _elapsed;
        private int _nextSpawn;
        private int _nextLight;
        private bool _registered;

        public event Action<PlayerState> StateChanged;

        /// <summary>A cue was lit: the cue, its cracker (null when it couldn't be placed) and the show clock.</summary>
        public event Action<ShowCue, CrackerRuntime, float> CueLit;

        public PlayerState State { get; private set; } = PlayerState.Idle;

        public ShowStore Store { get; private set; }

        public ShowLibrary Library { get; private set; }

        /// <summary>The show playing, or the last one played (what Replay plays).</summary>
        public ShowDef Last => _show;

        public float Elapsed => _elapsed;

        public int Lit => _nextLight;

        /// <summary>Crackers that failed to go on their pad (the scene was full); zero in a valid show.</summary>
        public int Missed { get; private set; }

        public bool CanReplay => State == PlayerState.Idle && _show != null;

        public void Configure(CrackerCatalog crackerCatalog) => catalog = crackerCatalog;

        /// <summary>Points the saved shows at another file (tests).</summary>
        public void UseStore(ShowStore store)
        {
            Store = store;
            Library = store.Load();
        }

        private void Awake()
        {
            if (!Game.Services.IsRegistered<ShowPlayer>())
            {
                Game.Services.Register(this);
                _registered = true;
            }

            UseStore(new ShowStore());
            JsonPackReader.ShowsCheck = CheckPackShows;
        }

        private void OnEnable() => Game.Loop.Add(this);

        private void OnDisable() => Game.Loop.Remove(this);

        private void OnDestroy()
        {
            if (_registered)
            {
                Game.Services.Unregister<ShowPlayer>();
            }
        }

        /// <summary>Starting points for a new show: the built-in templates, then the shows from installed shows packs.</summary>
        public List<ShowDef> Templates()
        {
            var list = new List<ShowDef>(ShowTemplates.All);
            if (!Game.Services.TryGet(out PackService packs))
            {
                return list;
            }

            foreach (Core.Packs.PackRequirement installed in packs.Manager.Installed)
            {
                Core.Packs.PackManifest m = packs.Manager.Manifest(installed.pack);
                if (m == null || m.type != Core.Packs.PackTypes.Shows)
                {
                    continue;
                }

                try
                {
                    ShowLibrary pack = JsonUtility.FromJson<ShowLibrary>(JsonPackReader.Text(packs.Manager.Files(installed.pack).Read(Core.Packs.PackLint.ShowsFile)));
                    list.AddRange(pack?.shows ?? new List<ShowDef>());
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Shows] pack {installed.pack} unreadable: {e.Message}");
                }
            }

            return list;
        }

        /// <summary>The fuse, effect length and flight of a cracker in the catalog, or null for an unknown id.</summary>
        public CrackerTiming? Timing(string id)
        {
            if (_timings.TryGetValue(id ?? string.Empty, out CrackerTiming? t))
            {
                return t;
            }

            CrackerDefinition d = catalog != null ? catalog.FindById(id) : null;
            t = null;
            if (d != null)
            {
                float stop = 0f;
                try
                {
                    stop = d.BuildTimeline().StopSeconds;
                }
                catch (ArgumentException)
                {
                    // An invalid timeline is caught by the content lint; count the pad as busy for the cooldown only.
                }

                t = new CrackerTiming(d.FuseSeconds, stop + d.CooldownSeconds, PlacementValidator.IsAerial(d));
            }

            _timings[id ?? string.Empty] = t;
            return t;
        }

        /// <summary>The spot the show would play at: the chosen celebration spot, or the courtyard's area when there is none.</summary>
        public static SpotDef CurrentSpot() =>
            Game.Services.TryGet(out CelebrationService celebration) && celebration.Current != null
                ? celebration.Current
                : new SpotDef { id = CelebrationService.Courtyard, x = 0f, y = 0f, z = 0.8f, radius = 2.3f, aerials = Aerials.Open };

        /// <summary>Every reason the show can't play at the current spot on this phone; empty when it can.</summary>
        public List<string> Check(ShowDef show)
        {
            SpotDef spot = CurrentSpot();
            ResolveSpawner();
            int limit = _spawner != null ? _spawner.ItemLimit : PlacementRules.ItemLimit(1);
            return ShowRules.Check(show, Timing, limit, spot.radius, CelebrationRules.RuleOf(spot) != AerialRule.None);
        }

        /// <summary>World position of a show pad at a spot: dx to the camera's right, dz away from it.</summary>
        public static Vector3 PadPosition(SpotDef spot, ShowPad pad)
        {
            Quaternion yaw = Quaternion.Euler(0f, spot.camYaw, 0f);
            return new Vector3(spot.x, spot.y, spot.z) + yaw * new Vector3(pad.dx, 0f, pad.dz);
        }

        public bool Play(ShowDef show)
        {
            if (State != PlayerState.Idle || Check(show).Count > 0)
            {
                return false;
            }

            ResolveSpawner();
            if (_spawner == null)
            {
                return false;
            }

            if (Game.Services.TryGet(out PlacementController placement))
            {
                placement.Cancel();
            }

            // The show owns the stage: crackers left from free play would count against the scene's limit.
            _spawner.ClearAll();
            _show = show.Clone();
            _order.Clear();
            _order.AddRange(ShowRules.Order(_show));
            _spawned.Clear();
            _elapsed = Math.Min(0f, _order[0].at - ShowRules.Lead);
            _nextSpawn = 0;
            _nextLight = 0;
            Missed = 0;
            SetState(PlayerState.Running);
            Tick(0f);
            return true;
        }

        public bool Replay() => CanReplay && Play(_show);

        /// <summary>Ends the show now: unlit crackers are taken away, burning ones finish.</summary>
        public void Stop()
        {
            if (State != PlayerState.Running)
            {
                return;
            }

            foreach (CrackerRuntime c in _spawned)
            {
                if (c != null && c.CanIgnite)
                {
                    _spawner.Remove(c);
                }
            }

            SetState(PlayerState.Idle);
        }

        public void Tick(float deltaSeconds)
        {
            if (State != PlayerState.Running)
            {
                return;
            }

            _elapsed += deltaSeconds;
            SpotDef spot = CurrentSpot();
            while (_nextSpawn < _order.Count && _order[_nextSpawn].at - ShowRules.Lead <= _elapsed)
            {
                _spawned.Add(SpawnCue(spot, _order[_nextSpawn]));
                _nextSpawn++;
            }

            while (_nextLight < _nextSpawn && _order[_nextLight].at <= _elapsed)
            {
                CrackerRuntime c = _spawned[_nextLight];
                if (c != null && c.CanIgnite)
                {
                    c.Ignite();
                }

                CueLit?.Invoke(_order[_nextLight], c, _elapsed);
                _nextLight++;
            }

            if (_nextLight < _order.Count)
            {
                return;
            }

            // Over when every cracker has been cleared, so a replay starts on an empty stage.
            foreach (CrackerRuntime c in _spawned)
            {
                if (c != null && c.isActiveAndEnabled && !c.CanIgnite)
                {
                    return;
                }
            }

            SetState(PlayerState.Idle);
        }

        /// <summary>Saves the show (a copy) to shows.json. False when the library is full, the show is invalid in itself, or the write fails.</summary>
        public bool Save(ShowDef show)
        {
            if (show == null || string.IsNullOrWhiteSpace(show.name) || !Library.Put(show))
            {
                return false;
            }

            return Store.Save(Library);
        }

        public bool Delete(string id) => Library.Remove(id) && Store.Save(Library);

        private CrackerRuntime SpawnCue(SpotDef spot, ShowCue cue)
        {
            CrackerDefinition d = catalog != null ? catalog.FindById(cue.cracker) : null;
            Vector3 p = PadPosition(spot, _show.pads[cue.pad]);
            if (d != null && PlacementValidator.IsAerial(d) && CelebrationRules.RuleOf(spot) == AerialRule.Pads)
            {
                p = NearestPad(spot, p);
            }

            CrackerRuntime c = d != null ? _spawner.Spawn(d, p, Quaternion.Euler(0f, spot.camYaw + 180f, 0f)) : null;
            if (c == null)
            {
                Missed++;
            }

            return c;
        }

        /// <summary>Where the spot's own launch pads require it, aerials go up from the nearest one.</summary>
        private static Vector3 NearestPad(SpotDef spot, Vector3 p)
        {
            Vector3 best = p;
            float bestD = float.MaxValue;
            foreach (LaunchPad pad in spot.pads)
            {
                var q = new Vector3(pad.x, pad.y, pad.z);
                float d = (q - p).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = q;
                }
            }

            return best;
        }

        /// <summary>A shows pack's file: a library of shows, each valid in the largest area with aerials allowed (the spot is checked again on play).</summary>
        private List<string> CheckPackShows(string json)
        {
            var problems = new List<string>();
            ShowLibrary library;
            try
            {
                library = JsonUtility.FromJson<ShowLibrary>(json);
            }
            catch (Exception e)
            {
                problems.Add("shows file unreadable: " + e.Message);
                return problems;
            }

            if (library == null || library.shows.Count == 0)
            {
                problems.Add("a shows pack needs at least one show");
                return problems;
            }

            foreach (ShowDef s in library.shows)
            {
                foreach (string p in ShowRules.Check(s, Timing, PlacementRules.ItemLimit(2), 3.5f, true))
                {
                    problems.Add($"{s.name}: {p}");
                }
            }

            return problems;
        }

        private void SetState(PlayerState state)
        {
            State = state;
            StateChanged?.Invoke(state);
        }

        private void ResolveSpawner()
        {
            if (_spawner == null)
            {
                Game.Services.TryGet(out _spawner);
            }
        }
    }
}

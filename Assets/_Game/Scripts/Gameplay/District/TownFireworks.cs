using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UtsavNights.Content;
using UtsavNights.Core.District;
using UtsavNights.Core.Runtime;

namespace UtsavNights.Gameplay
{
    /// <summary>A note for the player while exploring (a show coming up, a cracker that can't go there).</summary>
    public readonly struct TownNoticeEvent
    {
        public TownNoticeEvent(string text) => Text = text;

        public string Text { get; }
    }

    /// <summary>The player lit a cracker in the town (its id), or a district show began (the place's id: ghat, chowk).</summary>
    public readonly struct TownFireworkEvent
    {
        public TownFireworkEvent(string crackerId, string showPlace, bool near = false)
        {
            CrackerId = crackerId;
            ShowPlace = showPlace;
            Near = near;
        }

        public string CrackerId { get; }

        /// <summary>Empty for the player's own cracker.</summary>
        public string ShowPlace { get; }

        /// <summary>For a show: whether the player was close enough to watch it when it began.</summary>
        public bool Near { get; }
    }

    /// <summary>The player tried to set a cracker down in the town: Ok, or why not.</summary>
    public readonly struct TownPlacedEvent
    {
        public TownPlacedEvent(string crackerId, TownVerdict verdict)
        {
            CrackerId = crackerId;
            Verdict = verdict;
        }

        public string CrackerId { get; }

        public TownVerdict Verdict { get; }
    }

    /// <summary>
    /// Fireworks in the town (Phase 12 step 7). The player sets a cracker down in front of them wherever
    /// <see cref="TownSafety"/> allows, walks up and lights the fuse, and the crowd gathers round at a safe distance; the
    /// district puts on its own shows at the ghat and the chowk on a timetable (<see cref="TownShows"/>), announced on
    /// the phone; the rooftop job's finale lights real rockets on its racks. The crackers are the game's own
    /// (<see cref="CrackerSpawner"/>), so they look, sound and react as in the courtyard.
    /// </summary>
    [DefaultExecutionOrder(-85)]
    public sealed class TownFireworks : MonoBehaviour
    {
        public const float PlaceAhead = 1.8f;
        public const float LightReach = 1.7f;
        public const float GatherReach = 60f;

        /// <summary>A show began with the player this close to its watch point: they could see it.</summary>
        public const float NearShow = 90f;

        public static readonly string[] ShowCrackers = { "skyshot_green", "rocket_basic", "skyshot_gold_crown" };

        private readonly List<CrackerRuntime> _placed = new List<CrackerRuntime>();
        private readonly List<CrackerRuntime> _ours = new List<CrackerRuntime>();
        private readonly List<CrackerDefinition> _available = new List<CrackerDefinition>();
        private List<TrafficArm> _arms;
        private List<TownShowPlace> _places;
        private ExploreController _explore;
        private DistrictView _district;
        private CrackerCatalog _catalog;
        private Coroutine _show;
        private bool _registered;
        private bool _noticed;
        private int _nextShow;
        private float _nextShowAt;

        /// <summary>Seconds explored with the bazaar open (the shows' clock).</summary>
        public float Clock { get; private set; }

        public IReadOnlyList<CrackerRuntime> Placed => _placed;

        public bool ShowOn => _show != null;

        public int ShowsHeld { get; private set; }

        public int CuesFired { get; private set; }

        /// <summary>Show crackers the scene's limit turned away (Phase 12 step 8 measures this on phones).</summary>
        public int Refused { get; private set; }

        /// <summary>When the next district show starts, on <see cref="Clock"/>, and where.</summary>
        public (float at, string place) NextShow => (_nextShowAt, _places != null && _places.Count > 0 ? _places[TownShows.PlaceOf(_nextShow, _places.Count)].Name : string.Empty);

        /// <summary>The crackers the player can use in the town: the tray's unlocked ones, sparklers aside (they're held).</summary>
        public IReadOnlyList<CrackerDefinition> Available
        {
            get
            {
                _available.Clear();
                CrackerCatalog catalog = Catalog();
                if (catalog == null)
                {
                    return _available;
                }

                Game.Services.TryGet(out ProgressService progress);
                foreach (CrackerCatalog.Entry e in catalog.Entries)
                {
                    if (e.definition != null && e.definition.Category != CrackerCategory.Sparkler
                        && (!e.locked || (progress != null && progress.IsUnlocked(e.definition.Id, true))))
                    {
                        _available.Add(e.definition);
                    }
                }

                return _available;
            }
        }

        public static bool IsAerial(CrackerDefinition d) =>
            d.Category == CrackerCategory.Rocket || d.Category == CrackerCategory.Skyshot || d.Category == CrackerCategory.MultiShotCake;

        /// <summary>Where a cracker would go now: in front of the player, on the ground (or roof) they stand on.</summary>
        public Vector3 SpotAhead()
        {
            Transform p = _explore.Player;
            float yaw = _explore.State.Facing * Mathf.Deg2Rad;
            return p.position + new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw)) * PlaceAhead;
        }

        /// <summary>Whether a cracker could go down at a spot, and why not.</summary>
        public TownVerdict Check(CrackerDefinition d, Vector3 at)
        {
            Game.Services.TryGet(out CrowdView crowd);
            return TownSafety.Check(DistrictGenerator.Bazaar, Arms(), at.x, at.y, at.z, (int)d.Intensity, IsAerial(d), crowd?.Sim?.People, _placed.Count);
        }

        /// <summary>Sets a cracker down in front of the player. Ok, or why not (also said on screen).</summary>
        public TownVerdict Place(CrackerDefinition d)
        {
            if (d == null || _explore == null || !_explore.IsOpen || _explore.Riding)
            {
                return TownVerdict.NotOpenGround;
            }

            Vector3 at = SpotAhead();
            at.y = _explore.Player.position.y > 1f ? _explore.Player.position.y : 0f;
            TownVerdict v = Check(d, at);
            if (v == TownVerdict.Ok)
            {
                CrackerRuntime c = Game.Services.TryGet(out CrackerSpawner spawner) ? spawner.Spawn(d, at, Quaternion.Euler(0f, _explore.State.Facing, 0f)) : null;
                if (c == null)
                {
                    v = TownVerdict.TooMany;
                }
                else
                {
                    _placed.Add(c);
                    _ours.Add(c);
                    Game.Events.Publish(new TownPlacedEvent(d.Id, v));
                    Game.Events.Publish(new TownNoticeEvent("Set down. Walk up to it and light the fuse."));
                    return v;
                }
            }

            Game.Events.Publish(new TownPlacedEvent(d.Id, v));
            Game.Events.Publish(new TownNoticeEvent(TownSafety.Explain(v)));
            return v;
        }

        /// <summary>Picks up the crackers set down and not lit.</summary>
        public void PickUp()
        {
            if (Game.Services.TryGet(out CrackerSpawner spawner))
            {
                foreach (CrackerRuntime c in _placed)
                {
                    spawner.Remove(c);
                    _ours.Remove(c);
                }
            }

            _placed.Clear();
        }

        /// <summary>The player's cracker within reach to light, or null.</summary>
        public CrackerRuntime LightableAt(Vector3 at)
        {
            CrackerRuntime best = null;
            float bestD = LightReach;
            for (int i = 0; i < _placed.Count; i++)
            {
                CrackerRuntime c = _placed[i];
                if (c == null || !c.CanIgnite)
                {
                    continue;
                }

                Vector3 p = c.transform.position;
                float d = new Vector2(p.x - at.x, p.z - at.z).magnitude;
                if (d < bestD && Mathf.Abs(p.y - at.y) < 1.2f)
                {
                    bestD = d;
                    best = c;
                }
            }

            return best;
        }

        /// <summary>Lights the cracker within reach: people gather round at a safe distance. False when there's none.</summary>
        public bool LightAt(Vector3 at)
        {
            CrackerRuntime c = LightableAt(at);
            if (c == null)
            {
                return false;
            }

            _placed.Remove(c);
            c.Ignite();
            Vector3 p = c.transform.position;
            if (Game.Services.TryGet(out CrowdView crowd) && crowd.Sim != null)
            {
                float clear = TownSafety.PeopleClear((int)c.Definition.Intensity, IsAerial(c.Definition));
                crowd.Sim.GatherAround(p.x, p.z, GatherReach, 25f, clear + 3f);
            }

            Game.Events.Publish(new TownFireworkEvent(c.Definition.Id, string.Empty));
            Game.Events.Publish(new TownNoticeEvent("Step back and enjoy it!"));
            return true;
        }

        /// <summary>A cracker for a show (the district's or the rooftop job's): set down and lit at once. False when it couldn't be.</summary>
        public bool Fire(string crackerId, Vector3 at, float yaw = 0f)
        {
            CrackerDefinition d = Catalog()?.FindById(crackerId);
            if (d == null || !Game.Services.TryGet(out CrackerSpawner spawner))
            {
                return false;
            }

            CrackerRuntime c = spawner.Spawn(d, at, Quaternion.Euler(0f, yaw, 0f), byPlayer: false);
            if (c == null)
            {
                Refused++;
                return false;
            }

            _ours.Add(c);
            c.Ignite(byPlayer: false);
            CuesFired++;
            return true;
        }

        /// <summary>Starts a district show now (the timetable does this; tests too). False when one is on.</summary>
        public bool StartShow(int number)
        {
            if (_show != null || Places().Count == 0)
            {
                return false;
            }

            _show = StartCoroutine(RunShow(number));
            return true;
        }

        private void Awake()
        {
            if (!Game.Services.IsRegistered<TownFireworks>())
            {
                Game.Services.Register(this);
                _registered = true;
            }
        }

        private void OnDestroy()
        {
            if (_registered)
            {
                Game.Services.Unregister<TownFireworks>();
            }
        }

        private void Update()
        {
            if (_explore == null)
            {
                _explore = FindFirstObjectByType<ExploreController>();
            }

            if (_district == null)
            {
                Game.Services.TryGet(out _district);
            }

            bool on = _explore != null && _explore.IsOpen && _district != null && _district.Active;
            if (!on)
            {
                if (_ours.Count > 0 || _show != null)
                {
                    PutAway();
                }

                return;
            }

            _ours.RemoveAll(c => c == null || !c.isActiveAndEnabled);
            Clock += Time.deltaTime;
            (int number, float at) = TownShows.Next(Clock);
            if (number != _nextShow || at != _nextShowAt)
            {
                _nextShow = number;
                _nextShowAt = at;
                _noticed = false;
            }

            if (!_noticed && Clock >= at - TownShows.NoticeBefore)
            {
                _noticed = true;
                string place = Places()[TownShows.PlaceOf(number, Places().Count)].Name;
                string text = $"A fireworks show at {place} in under a minute! Head over to watch.";
                Game.Events.Publish(new TownNoticeEvent(text));
                if (Game.Services.TryGet(out JobService jobs))
                {
                    jobs.AddMessage("The bazaar committee", text);
                }
            }

            if (_show == null && Clock >= at && Clock < at + 1f)
            {
                StartShow(number);
            }
        }

        private IEnumerator RunShow(int number)
        {
            TownShowPlace place = Places()[TownShows.PlaceOf(number, Places().Count)];
            ShowsHeld++;
            if (Game.Services.TryGet(out CrowdView crowd) && crowd.Sim != null)
            {
                crowd.Sim.GatherAround(place.WatchX, place.WatchZ, 140f, TownShows.Length + 20f, 14f);
            }

            bool near = _explore != null && _explore.Player != null
                        && new Vector2(_explore.Player.position.x - place.WatchX, _explore.Player.position.z - place.WatchZ).magnitude < NearShow;
            Game.Events.Publish(new TownFireworkEvent(string.Empty, place.Id, near));
            List<TownCue> program = TownShows.Program(place.Pads.Count, 31 + number);
            float start = Time.time;
            foreach (TownCue cue in program)
            {
                while (Time.time - start < cue.At)
                {
                    yield return null;
                }

                (float x, float z) = place.Pads[cue.Pad];
                Fire(ShowCrackers[Mathf.Clamp(cue.Size, 0, ShowCrackers.Length - 1)], new Vector3(x, 0f, z));
            }

            while (Time.time - start < TownShows.Length)
            {
                yield return null;
            }

            _show = null;
        }

        /// <summary>Leaving the bazaar: crackers set down are picked up, a show stops, the player's lit ones go out.</summary>
        private void PutAway()
        {
            if (_show != null)
            {
                StopCoroutine(_show);
                _show = null;
            }

            if (Game.Services.TryGet(out CrackerSpawner spawner))
            {
                foreach (CrackerRuntime c in _ours)
                {
                    if (c != null)
                    {
                        spawner.Remove(c);
                    }
                }
            }

            _ours.Clear();
            _placed.Clear();
        }

        private List<TrafficArm> Arms() => _arms ??= TrafficSim.BuildArms(DistrictGenerator.Bazaar);

        private List<TownShowPlace> Places() => _places ??= TownShows.Places(DistrictGenerator.Bazaar);

        private CrackerCatalog Catalog()
        {
            if (_catalog == null)
            {
                ShowPlayer shows = FindFirstObjectByType<ShowPlayer>(FindObjectsInactive.Include);
                _catalog = shows != null ? shows.Catalog : null;
            }

            return _catalog;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UtsavNights.Audio;
using UtsavNights.Core;
using UtsavNights.Core.District;
using UtsavNights.Core.Explore;
using UtsavNights.Core.Home;
using UtsavNights.Core.Runtime;

namespace UtsavNights.Gameplay
{
    /// <summary>A message on the player's phone: a job offered, thanks for one done, or news.</summary>
    public sealed class PhoneMessage
    {
        public string From;
        public string Text;

        /// <summary>The job it offers or thanks for, or empty.</summary>
        public string JobId = string.Empty;

        public bool Unread = true;
    }

    /// <summary>An app opened on the explore phone: map, messages, jobs, camera, home or settings.</summary>
    public readonly struct PhoneAppEvent
    {
        public PhoneAppEvent(string app) => App = app;

        public string App { get; }
    }

    /// <summary>A job was taken, finished, failed or given up (counts and ids only).</summary>
    public readonly struct JobEvent
    {
        public JobEvent(string jobId, JobStatus status, int seconds)
        {
            JobId = jobId;
            Status = status;
            Seconds = seconds;
        }

        public string JobId { get; }

        public JobStatus Status { get; }

        public int Seconds { get; }
    }

    /// <summary>
    /// The town's jobs while exploring (Phase 12 step 6): offers them on the phone as messages, runs the one taken
    /// (<see cref="JobRun"/>) from the player's position and the Interact button, shows beacons at what's left to do and
    /// the lane's diyas, pays in coins and reputation through <see cref="PlayerService"/>, and sets off the rooftop show at
    /// the end of that job. One job at a time; leaving Explore gives it up.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public sealed class JobService : MonoBehaviour
    {
        public const int MaxBeacons = 8;
        public const float ShowSeconds = 22f;

        private readonly List<PhoneMessage> _messages = new List<PhoneMessage>();
        private readonly HashSet<string> _offered = new HashSet<string>();
        private readonly GameObject[] _beacons = new GameObject[MaxBeacons];
        private readonly List<GameObject> _diyas = new List<GameObject>();
        private readonly List<bool> _diyaLit = new List<bool>();
        private Mesh _beaconMesh;
        private Mesh _diyaMesh;
        private Mesh _litMesh;
        private HomeMaterials _materials;
        private GameObject _root;
        private ExploreController _explore;
        private bool _registered;
        private bool _wasOpen;

        public IReadOnlyList<JobDef> Jobs { get; private set; }

        public JobRun Active { get; private set; }

        public IReadOnlyList<PhoneMessage> Messages => _messages;

        public int Unread
        {
            get
            {
                int n = 0;
                foreach (PhoneMessage m in _messages)
                {
                    n += m.Unread ? 1 : 0;
                }

                return n;
            }
        }

        /// <summary>A place the player picked on the map, when no job step is the way to go.</summary>
        public Vector3? Waypoint { get; set; }

        /// <summary>Where the route leads now: the nearest step of the job, else the map's waypoint.</summary>
        public bool TryTarget(Vector3 from, out Vector3 target)
        {
            if (Active != null && Active.Status == JobStatus.Active)
            {
                int i = Active.NearestOpen(from.x, from.z);
                if (i >= 0)
                {
                    JobStep s = Active.Def.Steps[i];
                    target = new Vector3(s.X, s.Y, s.Z);
                    return true;
                }
            }

            target = Waypoint ?? default;
            return Waypoint.HasValue;
        }

        /// <summary>The rooftop show is on.</summary>
        public bool ShowPlaying { get; private set; }

        /// <summary>Bursts the rooftop shows have set off this session.</summary>
        public int ShowBursts { get; private set; }

        public event Action Changed;

        public bool IsAvailable(JobDef job) => !Game.Services.TryGet(out PlayerService player) || player.Level >= job.MinLevel;

        public int TimesDone(string jobId) => Game.Services.TryGet(out PlayerService player) ? player.Current.TimesDone(jobId) : 0;

        public JobDef Find(string id)
        {
            foreach (JobDef j in Jobs)
            {
                if (j.Id == id)
                {
                    return j;
                }
            }

            return null;
        }

        /// <summary>Takes a job (giving up any other). False when it isn't offered yet.</summary>
        public bool Accept(string jobId)
        {
            JobDef job = Find(jobId);
            if (job == null || !IsAvailable(job))
            {
                return false;
            }

            Active?.Abandon();
            Active = new JobRun(job);
            Waypoint = null;
            Game.Events.Publish(new JobEvent(job.Id, JobStatus.Active, 0));
            RefreshProps();
            Changed?.Invoke();
            return true;
        }

        public void Abandon()
        {
            if (Active == null || Active.Status != JobStatus.Active)
            {
                return;
            }

            Active.Abandon();
            Game.Events.Publish(new JobEvent(Active.Def.Id, JobStatus.Abandoned, (int)Active.Elapsed));
            Active = null;
            RefreshProps();
            Changed?.Invoke();
        }

        /// <summary>A message from someone in the town (news, a show coming up).</summary>
        public void AddMessage(string from, string text)
        {
            _messages.Insert(0, new PhoneMessage { From = from, Text = text });
            Changed?.Invoke();
        }

        public void MarkRead(PhoneMessage m)
        {
            if (m.Unread)
            {
                m.Unread = false;
                Changed?.Invoke();
            }
        }

        /// <summary>What the Interact button would do for the job here, if anything.</summary>
        public bool PromptAt(Vector3 at, out string label, out string id)
        {
            label = id = string.Empty;
            if (Active == null || Active.Status != JobStatus.Active)
            {
                return false;
            }

            int step = Active.InteractableAt(at.x, at.y, at.z);
            if (step < 0)
            {
                return false;
            }

            label = Active.Def.Steps[step].Prompt;
            id = Active.Def.Steps[step].Id;
            return true;
        }

        /// <summary>Does the job's step here (from Interact). False when there's nothing to do here.</summary>
        public bool Interact(Vector3 at)
        {
            if (Active == null)
            {
                return false;
            }

            int step = Active.InteractableAt(at.x, at.y, at.z);
            JobProgress p = Active.Interact(step, at.x, at.y, at.z);
            if (p == JobProgress.None)
            {
                return false;
            }

            JobStep s = Active.Def.Steps[step];
            if (s.MovesPlayer && _explore != null)
            {
                if (_explore.Riding)
                {
                    _explore.Dismount();
                }

                _explore.Teleport(new Vector3(s.ToX, s.ToY, s.ToZ), s.ToFacing);
            }

            Handle(p);
            return true;
        }

        private void Awake()
        {
            if (!Game.Services.IsRegistered<JobService>())
            {
                Game.Services.Register(this);
                _registered = true;
            }

            Jobs = JobCatalog.Build(DistrictGenerator.Bazaar);
            Game.Events.Subscribe<PlayerRewardEvent>(OnReward);
        }

        private void OnDestroy()
        {
            Game.Events.Unsubscribe<PlayerRewardEvent>(OnReward);
            ClearProps();
            if (_registered)
            {
                Game.Services.Unregister<JobService>();
            }
        }

        private void Update()
        {
            if (_explore == null)
            {
                _explore = FindFirstObjectByType<ExploreController>();
                if (_explore == null)
                {
                    return;
                }
            }

            bool open = _explore.IsOpen && _explore.Player != null;
            if (open != _wasOpen)
            {
                _wasOpen = open;
                if (open)
                {
                    OfferJobs();
                    BuildProps();
                }
                else
                {
                    Abandon();
                    Waypoint = null;
                    ClearProps();
                }
            }

            if (!open || Active == null)
            {
                return;
            }

            Vector3 at = _explore.Player.position;
            JobProgress p = Active.Tick(Time.deltaTime, at.x, at.y, at.z);
            if (p != JobProgress.None)
            {
                Handle(p);
            }

            PlaceBeacons(at);
        }

        private void Handle(JobProgress p)
        {
            JobRun run = Active;
            if (run == null)
            {
                return;
            }

            if (run.FinaleDue)
            {
                run.FinaleDue = false;
                StartCoroutine(RooftopShow(run.Def));
            }

            if (p == JobProgress.Completed)
            {
                (long coins, int rep) = JobPay.For(run.Def, TimesDone(run.Def.Id));
                if (Game.Services.TryGet(out PlayerService player))
                {
                    player.FinishJob(run.Def.Id);
                    player.Reward(coins, rep, "job");
                }

                _messages.Insert(0, new PhoneMessage { From = run.Def.Giver, Text = run.Def.Thanks, JobId = run.Def.Id });
                Game.Events.Publish(new JobEvent(run.Def.Id, JobStatus.Completed, (int)run.Elapsed));
                Active = null;
            }
            else if (p == JobProgress.Failed)
            {
                _messages.Insert(0, new PhoneMessage
                {
                    From = run.Def.Giver,
                    Text = "Never mind, the shops have closed for now. Try again whenever you like: it's in Jobs on your phone.",
                    JobId = run.Def.Id,
                });
                Game.Events.Publish(new JobEvent(run.Def.Id, JobStatus.Failed, (int)run.Elapsed));
                Active = null;
            }

            RefreshProps();
            Changed?.Invoke();
        }

        /// <summary>A message for each job on offer that hasn't been offered this session.</summary>
        private void OfferJobs()
        {
            bool any = false;
            foreach (JobDef job in Jobs)
            {
                if (IsAvailable(job) && _offered.Add(job.Id))
                {
                    string again = TimesDone(job.Id) > 0 ? " (again, if you're free)" : string.Empty;
                    _messages.Insert(0, new PhoneMessage { From = job.Giver, Text = job.Offer + again, JobId = job.Id });
                    any = true;
                }
            }

            if (any)
            {
                Changed?.Invoke();
            }
        }

        private void OnReward(PlayerRewardEvent e)
        {
            if (e.LevelUp > 0 && _wasOpen)
            {
                OfferJobs();
            }
        }

        /// <summary>The lane's diyas (lit once the lane has been lit) and the beacons, made once while exploring.</summary>
        private void BuildProps()
        {
            ClearProps();
            _materials = new HomeMaterials();
            _root = new GameObject("JobProps");
            _root.transform.SetParent(transform, false);
            _beaconMesh = Make(JobKit.Beacon(), "JobBeacon", out Material[] beacon);
            _diyaMesh = Make(JobKit.Diya(false), "LaneDiya", out Material[] diya);
            _litMesh = Make(JobKit.Diya(true), "LaneDiyaLit", out Material[] lit);
            for (int i = 0; i < MaxBeacons; i++)
            {
                _beacons[i] = Draw("Beacon" + i, _beaconMesh, beacon);
                _beacons[i].SetActive(false);
            }

            JobDef lane = Find(JobCatalog.Lane);
            bool litBefore = TimesDone(JobCatalog.Lane) > 0;
            foreach (JobStep s in lane.Steps)
            {
                GameObject go = Draw("LaneDiya_" + s.Id, litBefore ? _litMesh : _diyaMesh, litBefore ? lit : diya);
                go.transform.position = new Vector3(s.X, 0f, s.Z);
                go.SetActive(litBefore);
                _diyas.Add(go);
                _diyaLit.Add(litBefore);
            }

            _diyaMaterials = (diya, lit);
            RefreshProps();
        }

        private (Material[] unlit, Material[] lit) _diyaMaterials;

        /// <summary>The lane's diyas: out (unlit) while the lane job is on, lit as each is done; lit for good once done.</summary>
        private void RefreshProps()
        {
            if (_root == null)
            {
                return;
            }

            JobDef lane = Find(JobCatalog.Lane);
            bool laneOn = Active != null && Active.Def.Id == JobCatalog.Lane;
            bool litForGood = TimesDone(JobCatalog.Lane) > 0;
            for (int i = 0; i < _diyas.Count; i++)
            {
                bool lit = litForGood || (laneOn && Active.IsDone(i));
                _diyas[i].SetActive(lit || laneOn);
                if (lit != _diyaLit[i])
                {
                    _diyaLit[i] = lit;
                    _diyas[i].GetComponent<MeshFilter>().sharedMesh = lit ? _litMesh : _diyaMesh;
                    _diyas[i].GetComponent<MeshRenderer>().sharedMaterials = lit ? _diyaMaterials.lit : _diyaMaterials.unlit;
                }
            }

            if (Active == null)
            {
                foreach (GameObject b in _beacons)
                {
                    if (b != null)
                    {
                        b.SetActive(false);
                    }
                }
            }
        }

        /// <summary>A beacon at each step open now (up to eight); none during the show.</summary>
        private void PlaceBeacons(Vector3 player)
        {
            int n = 0;
            if (Active != null && !ShowPlaying)
            {
                for (int i = 0; i < Active.Def.Steps.Count && n < MaxBeacons; i++)
                {
                    if (!Active.IsOpen(i))
                    {
                        continue;
                    }

                    JobStep s = Active.Def.Steps[i];
                    GameObject b = _beacons[n++];
                    if (!b.activeSelf)
                    {
                        b.SetActive(true);
                    }

                    b.transform.position = new Vector3(s.X, s.Y, s.Z);
                }
            }

            for (; n < MaxBeacons; n++)
            {
                if (_beacons[n] != null && _beacons[n].activeSelf)
                {
                    _beacons[n].SetActive(false);
                }
            }
        }

        private void ClearProps()
        {
            if (_root != null)
            {
                Destroy(_root);
            }

            HouseMeshes.Destroy(_beaconMesh);
            HouseMeshes.Destroy(_diyaMesh);
            HouseMeshes.Destroy(_litMesh);
            _materials?.Clear();
            _materials = null;
            _root = null;
            _diyas.Clear();
            _diyaLit.Clear();
            Array.Clear(_beacons, 0, _beacons.Length);
        }

        private Mesh Make(HouseMesh data, string name, out Material[] materials)
        {
            Mesh mesh = HouseMeshes.ToMesh(data, name, out HousePaint[] paints);
            materials = new Material[paints.Length];
            for (int i = 0; i < paints.Length; i++)
            {
                materials[i] = _materials.Get(paints[i], DistrictPalette.Colour(paints[i]));
            }

            return mesh;
        }

        private GameObject Draw(string name, Mesh mesh, Material[] materials)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = materials;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>
        /// The rooftop show: for about twenty seconds, rockets and sky shots go up from the roof's three racks and burst
        /// over the chowk, building to a finale; the square fills with people who cheer at each burst. They're the game's
        /// own crackers (<see cref="TownFireworks.Fire"/>); if one can't be set down (the scene is full), a sky burst
        /// with a synthesised bang stands in for it.
        /// </summary>
        private IEnumerator RooftopShow(JobDef job)
        {
            ShowPlaying = true;
            Changed?.Invoke();
            DistrictPlan plan = DistrictGenerator.Bazaar;
            DistrictBuilding house = JobCatalog.RooftopHouse(plan);
            Vector3 over = new Vector3(plan.Square.CentreX, 0f, plan.Square.CentreZ);
            if (Game.Services.TryGet(out CrowdView crowd) && crowd.Sim != null)
            {
                crowd.Sim.Gather(over.x, over.z, 90f, ShowSeconds + 20f);
            }

            Game.Services.TryGet(out TownFireworks town);
            AmbientLife sky = FindFirstObjectByType<AmbientLife>();
            Game.Services.TryGet(out AudioService audio);
            AudioClip bang = null;
            var random = new System.Random(job.Id.GetHashCode());
            float end = Time.time + ShowSeconds;
            int shots = 0;
            while (Time.time < end)
            {
                JobStep rack = job.Steps[2 + random.Next(3)];
                var from = new Vector3(rack.X, house.Height, rack.Z);
                bool finale = Time.time > end - 4f;
                string cracker = TownFireworks.ShowCrackers[finale ? 2 : shots % 3 == 2 ? 1 : 0];
                if (town == null || !town.Fire(cracker, from))
                {
                    // The stand-in: a burst in the sky over the square.
                    var burst = Vector3.Lerp(from, over, 0.6f) + new Vector3((float)random.NextDouble() * 10f - 5f, 28f + (float)random.NextDouble() * 12f, (float)random.NextDouble() * 10f - 5f);
                    sky?.Burst(burst, finale ? 2.2f : 1.4f, shots);
                    bang ??= Clip(ProceduralSounds.Kind.Bang, 12);
                    audio?.Play(bang, 0.5f, SoundPriority.Payoff);
                    Game.Events.Publish(new PayoffStartedEvent("rooftop_show", burst, loud: false));
                }

                ShowBursts++;
                shots++;
                yield return new WaitForSeconds(finale ? 0.35f : 0.9f + (float)random.NextDouble() * 0.7f);
            }

            ShowPlaying = false;
            Game.Events.Publish(new ShowPlayedEvent(shots, (int)ShowSeconds, false));
            Changed?.Invoke();
        }

        private static AudioClip Clip(ProceduralSounds.Kind kind, int seed)
        {
            const int rate = 44100;
            float[] data = ProceduralSounds.Generate(kind, rate, seed);
            AudioClip clip = AudioClip.Create("show_" + kind, data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}

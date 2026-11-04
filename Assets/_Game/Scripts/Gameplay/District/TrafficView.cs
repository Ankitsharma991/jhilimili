using UnityEngine;
using UtsavNights.Audio;
using UtsavNights.Core.District;
using UtsavNights.Core.Explore;
using UtsavNights.Core.Home;
using UtsavNights.Core.Runtime;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// The bazaar's traffic (Phase 12 step 5), while the district is part of the world: the traffic simulation
    /// (<see cref="TrafficSim"/>) with every vehicle drawn near the player as a box-built scooter, auto or bicycle with its
    /// rider, a body the player bumps into, and a horn when the player stays in the way. It shares the crowd with the
    /// people (vehicles stop for anyone crossing; walkers step aside for vehicles). Built when the district opens, then only
    /// moved and switched, so it allocates nothing as it runs.
    /// </summary>
    [DefaultExecutionOrder(-105)]
    public sealed class TrafficView : MonoBehaviour
    {
        public const float DrawRange = 100f;
        public const float HornRange = 40f;
        public const int Seed = 4410;

        private GameObject[] _vehicles;
        private Mesh[] _meshes;
        private HomeMaterials _materials;
        private DistrictView _district;
        private CrowdView _crowd;
        private ExploreController _explore;
        private AudioClip _horn;
        private bool _registered;

        public TrafficSim Sim { get; private set; }

        /// <summary>Vehicles drawn now.</summary>
        public int Drawn { get; private set; }

        public GameObject VehicleObject(int i) => _vehicles != null ? _vehicles[i] : null;

        private void Awake()
        {
            if (!Game.Services.IsRegistered<TrafficView>())
            {
                Game.Services.Register(this);
                _registered = true;
            }
        }

        private void OnDestroy()
        {
            Unload();
            if (_registered)
            {
                Game.Services.Unregister<TrafficView>();
            }
        }

        private void Update()
        {
            if (_district == null && !Game.Services.TryGet(out _district))
            {
                return;
            }

            if (_explore == null)
            {
                _explore = FindFirstObjectByType<ExploreController>();
            }

            bool active = _district.Active && _explore != null && _explore.Player != null;
            if (active != (Sim != null))
            {
                if (active)
                {
                    Load();
                }
                else
                {
                    Unload();
                }
            }

            if (Sim == null)
            {
                return;
            }

            if (_crowd == null)
            {
                Game.Services.TryGet(out _crowd);
            }

            CrowdSim crowd = _crowd != null ? _crowd.Sim : null;
            if (crowd != null)
            {
                crowd.Vehicles = Sim.Vehicles;
            }

            Vector3 player = _explore.Player.position;
            Sim.Tick(Time.deltaTime, player.x, player.z, _explore.Riding ? 0.6f : 0.3f, crowd?.People);
            Draw(player);
            for (int h = 0; h < Sim.Honks; h++)
            {
                (float x, float z) = Sim.Honk(h);
                float d = new Vector2(x - player.x, z - player.z).magnitude;
                if (d < HornRange && Game.Services.TryGet(out AudioService audio))
                {
                    VoiceHandle voice = audio.Play(Horn(), 0.32f * (1f - d / HornRange), SoundPriority.Effect);
                    audio.SetPitch(voice, 0.85f + 0.1f * (h % 3));
                }
            }
        }

        private void Draw(Vector3 player)
        {
            Vehicle[] vehicles = Sim.Vehicles;
            int drawn = 0;
            for (int i = 0; i < vehicles.Length; i++)
            {
                ref Vehicle v = ref vehicles[i];
                float dx = v.X - player.x, dz = v.Z - player.z;
                bool show = dx * dx + dz * dz < DrawRange * DrawRange;
                GameObject go = _vehicles[i];
                if (go.activeSelf != show)
                {
                    go.SetActive(show);
                }

                if (show)
                {
                    drawn++;
                    go.transform.SetPositionAndRotation(new Vector3(v.X, 0f, v.Z), Quaternion.Euler(0f, v.Heading, 0f));
                }
            }

            Drawn = drawn;
        }

        private void Load()
        {
            Sim = new TrafficSim(_district.Plan, Seed);
            Horn(); // made now, not at the first honk mid-ride
            _materials ??= new HomeMaterials();
            int kinds = System.Enum.GetValues(typeof(VehicleKind)).Length;
            _meshes = new Mesh[kinds * VehicleKit.Variants];
            var shared = new Material[_meshes.Length][];
            _vehicles = new GameObject[Sim.Vehicles.Length];
            for (int i = 0; i < Sim.Vehicles.Length; i++)
            {
                Vehicle v = Sim.Vehicles[i];
                int m = (int)v.Kind * VehicleKit.Variants + v.Variant % VehicleKit.Variants;
                if (_meshes[m] == null)
                {
                    _meshes[m] = HouseMeshes.ToMesh(VehicleKit.Build(v.Kind, v.Variant), $"Vehicle_{v.Kind}_{v.Variant}", out HousePaint[] paints);
                    shared[m] = new Material[paints.Length];
                    for (int p = 0; p < paints.Length; p++)
                    {
                        shared[m][p] = _materials.Get(paints[p], DistrictPalette.Colour(paints[p]));
                    }
                }

                var go = new GameObject($"Vehicle{i}_{v.Kind}");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = _meshes[m];
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = shared[m];
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var body = go.AddComponent<BoxCollider>();
                body.center = new Vector3(0f, 0.8f, 0f);
                body.size = new Vector3(TrafficSim.HalfWidth(v.Kind) * 2f, 1.6f, TrafficSim.HalfLength(v.Kind) * 2f);
                go.SetActive(false);
                _vehicles[i] = go;
            }
        }

        private void Unload()
        {
            if (_vehicles != null)
            {
                foreach (GameObject go in _vehicles)
                {
                    Destroy(go);
                }
            }

            if (_meshes != null)
            {
                foreach (Mesh m in _meshes)
                {
                    HouseMeshes.Destroy(m);
                }
            }

            if (_crowd != null && _crowd.Sim != null)
            {
                _crowd.Sim.Vehicles = null;
            }

            _materials?.Clear();
            _materials = null;
            _vehicles = null;
            _meshes = null;
            Sim = null;
            Drawn = 0;
        }

        private AudioClip Horn()
        {
            if (_horn == null)
            {
                const int rate = 44100;
                float[] data = VehicleSounds.Horn(rate);
                _horn = AudioClip.Create("traffic_horn", data.Length, 1, rate, false);
                _horn.SetData(data, 0);
            }

            return _horn;
        }
    }
}

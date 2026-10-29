using System;
using System.Collections.Generic;
using UnityEngine;
using UtsavNights.Core.Art;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Home;
using UtsavNights.Core.Neighbourhood;
using UtsavNights.Core.Runtime;
using UtsavNights.Core.Settings;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// The neighbourhood's ambient life (Phase 8 step 5, PRD 10): street lamps and strings of lights along the street (always,
    /// emissive only), neighbours' distant bursts beyond the settlement (always), looped neighbours in their courtyards (mid
    /// and high tiers), a crowd on the open space (high tier), all of the moving life off in Quiet mode, and the Night
    /// Timeline: dusk to late night over the session, darkening the sky and ambient light and shaping the bursts and people.
    /// Re-plans once a second; bursts are emitted from one pooled particle system without allocation.
    /// </summary>
    public sealed class AmbientLife : MonoBehaviour
    {
        public const float PlanInterval = 1f;
        public const int SparksPerBurst = 70;

        private static readonly Color[] Palette =
        {
            new Color(1f, 0.62f, 0.2f), new Color(1f, 0.85f, 0.35f), new Color(0.95f, 0.3f, 0.6f), new Color(0.4f, 0.7f, 1f),
            new Color(0.5f, 1f, 0.5f), new Color(1f, 0.95f, 0.9f),
        };

        private readonly List<DistantBurst> _due = new List<DistantBurst>();
        private readonly List<(AmbientAnchor anchor, AvatarView view, AvatarAnimator animator, float nextCheer)> _people =
            new List<(AmbientAnchor, AvatarView, AvatarAnimator, float)>();

        private SettlementView _settlement;
        private Material _spark;
        private Func<int> _tier = () => 1;
        private DistantBursts _bursts = new DistantBursts(2026);
        private ParticleSystem _particles;
        private GameObject _street;
        private List<AmbientAnchor> _anchors = new List<AmbientAnchor>();
        private float _startedAt;
        private float _clock;
        private bool _quiet;
        private bool _lowMemory;
        private float? _minutesOverride;
        private Material _sky;
        private float _skyBase = 1f;
        private Color _ambientBase;
        private int _cheerSeed = 1;

        public NightState Night { get; private set; } = NightTimeline.At(0f);

        public AmbientPlan Plan { get; private set; }

        public int ActiveNeighbours { get; private set; }

        public int ActiveCrowd { get; private set; }

        public int BurstsEmitted { get; private set; }

        public int StreetLamps { get; private set; }

        public void Configure(SettlementView settlement, Material spark, Func<int> tier)
        {
            _settlement = settlement;
            _spark = spark;
            _tier = tier ?? (() => 1);
            _settlement.Loaded += Rebuild;
            Rebuild();
        }

        /// <summary>Puts the night at a given minute (tests and the screenshot tool); null follows the session's clock.</summary>
        public void SetMinutes(float? minutes)
        {
            _minutesOverride = minutes;
            Replan();
        }

        /// <summary>Runs the distant bursts' schedule forward (tests): emits whatever falls due in that time.</summary>
        public void AdvanceBursts(float seconds)
        {
            _due.Clear();
            _bursts.Tick(seconds, Plan.BurstsPerMinute, _due);
            for (int i = 0; i < _due.Count; i++)
            {
                Emit(_due[i]);
            }
        }

        public Material SparkMaterial => _spark;

        /// <summary>Quiet mode, as the settings report it.</summary>
        public void SetQuiet(bool quiet)
        {
            _quiet = quiet;
            Replan();
        }

        private void Awake()
        {
            _startedAt = Time.unscaledTime;
            _ambientBase = RenderSettings.ambientLight;
            if (RenderSettings.skybox != null)
            {
                // An instance, so the night's darkening never edits the sky material asset.
                _sky = new Material(RenderSettings.skybox);
                _skyBase = _sky.HasProperty("_Exposure") ? _sky.GetFloat("_Exposure") : 1f;
                RenderSettings.skybox = _sky;
            }
        }

        private void OnEnable()
        {
            Game.Events.Subscribe<SettingsChangedEvent>(OnSettings);
            Application.lowMemory += OnLowMemory;
            if (Game.Services.TryGet(out SettingsService settings))
            {
                _quiet = settings.Current.quietMode;
            }
        }

        private void OnDisable()
        {
            Game.Events.Unsubscribe<SettingsChangedEvent>(OnSettings);
            Application.lowMemory -= OnLowMemory;
        }

        private void OnDestroy()
        {
            Clear();
            if (_sky != null)
            {
                Destroy(_sky);
            }
        }

        private void OnSettings(SettingsChangedEvent e)
        {
            if (e.Settings.quietMode != _quiet)
            {
                SetQuiet(e.Settings.quietMode);
            }
        }

        private void OnLowMemory()
        {
            _lowMemory = true;
            Replan();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _clock += Time.unscaledDeltaTime;
            if (_clock >= PlanInterval)
            {
                _clock = 0f;
                Replan();
            }

            _due.Clear();
            _bursts.Tick(dt, Plan.BurstsPerMinute, _due);
            for (int i = 0; i < _due.Count; i++)
            {
                Emit(_due[i]);
            }

            float now = Time.unscaledTime;
            for (int i = 0; i < _people.Count; i++)
            {
                var p = _people[i];
                if (p.view != null && p.view.gameObject.activeSelf && now >= p.nextCheer)
                {
                    p.animator.Play(AvatarClip.Celebrate);
                    p.nextCheer = now + 7f + Hash(i + _cheerSeed++) * 9f;
                    _people[i] = p;
                }
            }
        }

        /// <summary>Re-reads the night and the plan, then shows exactly that many people.</summary>
        private void Replan()
        {
            float minutes = _minutesOverride ?? (Time.unscaledTime - _startedAt) / 60f;
            Night = NightTimeline.At(minutes);
            Plan = AmbientPlan.For(_tier(), _quiet, Night, _lowMemory);
            RenderSettings.ambientLight = _ambientBase * Night.Ambient;
            if (_sky != null && _sky.HasProperty("_Exposure"))
            {
                _sky.SetFloat("_Exposure", _skyBase * Night.SkyExposure);
            }

            int neighbours = 0, crowd = 0;
            for (int i = 0; i < _people.Count; i++)
            {
                var p = _people[i];
                bool on = p.anchor.Crowd ? crowd++ < Plan.Crowd : neighbours++ < Plan.Neighbours;
                if (p.view != null && p.view.gameObject.activeSelf != on)
                {
                    p.view.gameObject.SetActive(on);
                }
            }

            ActiveNeighbours = Math.Min(neighbours, Plan.Neighbours);
            ActiveCrowd = Math.Min(crowd, Plan.Crowd);
            EnsurePeople();
        }

        /// <summary>Creates the avatars the plan needs (once; then they are only shown and hidden).</summary>
        private void EnsurePeople()
        {
            int wantNeighbours = Plan.Neighbours, wantCrowd = Plan.Crowd;
            int haveNeighbours = 0, haveCrowd = 0;
            foreach (var p in _people)
            {
                if (p.anchor.Crowd)
                {
                    haveCrowd++;
                }
                else
                {
                    haveNeighbours++;
                }
            }

            if (haveNeighbours >= wantNeighbours && haveCrowd >= wantCrowd)
            {
                return;
            }

            IReadOnlyList<AvatarProfile> presets = AvatarPresets.All;
            foreach (AmbientAnchor a in _anchors)
            {
                bool need = a.Crowd ? haveCrowd < wantCrowd : haveNeighbours < wantNeighbours;
                if (!need || _people.Exists(p => p.anchor.Position.X == a.Position.X && p.anchor.Position.Z == a.Position.Z))
                {
                    continue;
                }

                AvatarProfile profile = presets[(_people.Count * 5 + 3) % presets.Count];
                AvatarView view = AvatarView.Create(transform, profile, a.Crowd ? "Crowd" : "Neighbour");
                view.transform.SetPositionAndRotation(new Vector3(a.Position.X, a.Position.Y, a.Position.Z), Quaternion.Euler(0f, a.Yaw, 0f));
                AvatarAnimator animator = view.gameObject.AddComponent<AvatarAnimator>();
                _people.Add((a, view, animator, Time.unscaledTime + 2f + Hash(_people.Count) * 8f));
                if (a.Crowd)
                {
                    haveCrowd++;
                    ActiveCrowd = Math.Min(haveCrowd, wantCrowd);
                }
                else
                {
                    haveNeighbours++;
                    ActiveNeighbours = Math.Min(haveNeighbours, wantNeighbours);
                }
            }
        }

        private void Emit(DistantBurst b)
        {
            if (_particles == null)
            {
                return;
            }

            Color c = Palette[b.Colour % Palette.Length];
            var emit = new ParticleSystem.EmitParams { startColor = c, startSize = 0.6f, startLifetime = 1.8f };
            uint seed = (uint)(BurstsEmitted * 2654435761u + 1u);
            for (int i = 0; i < SparksPerBurst; i++)
            {
                // Directions spread evenly over a sphere (a golden-angle spiral), jittered by a cheap hash.
                float t = (i + 0.5f) / SparksPerBurst;
                float y = 1f - 2f * t;
                float r = Mathf.Sqrt(1f - y * y);
                float a = i * 2.39996f + (seed % 628u) * 0.01f;
                var dir = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                emit.position = new Vector3(b.Position.X, b.Position.Y, b.Position.Z);
                emit.velocity = dir * b.Size;
                _particles.Emit(emit, 1);
            }

            BurstsEmitted++;
        }

        private void Rebuild()
        {
            Clear();
            _anchors = AmbientAnchors.For(_settlement.Layout);
            _bursts = new DistantBursts(_settlement.Layout.id.Length * 97 + 2026);
            BuildStreet();
            BuildParticles();
            Replan();
        }

        /// <summary>Lamp posts along the open-space edge of the street, with strings of lights hung between them.</summary>
        private void BuildStreet()
        {
            SettlementLayout layout = _settlement.Layout;
            _street = new GameObject("StreetLights");
            _street.transform.SetParent(transform, false);
            var posts = new HouseMesh();
            float z = layout.street.z1 - 0.15f;
            var tops = new List<V3>();
            StreetLamps = 0;
            for (float x = layout.street.x0 + SettlementLibrary.LotSpacing / 2f; x <= layout.street.x1 - 1f; x += SettlementLibrary.LotSpacing)
            {
                posts.Box(x - 0.05f, 0f, z - 0.05f, x + 0.05f, 2.5f, z + 0.05f, HousePaint.Iron, Faces.Bottom);
                posts.Box(x - 0.1f, 2.5f, z - 0.1f, x + 0.1f, 2.72f, z + 0.1f, HousePaint.Glass);
                posts.Box(x - 0.14f, 2.72f, z - 0.14f, x + 0.14f, 2.78f, z + 0.14f, HousePaint.Iron);
                tops.Add(new V3(x, 2.45f, z));
                StreetLamps++;
            }

            Shared(posts, "StreetLamps", null, 0);
            for (int i = 0; i + 1 < tops.Count; i++)
            {
                string item = i % 2 == 0 ? "string_lights_colour" : "string_lights_warm";
                HouseMesh strand = DecorGenerator.Path(item, tops[i], tops[i + 1], 0.25f);
                Shared(strand, "StreetString" + i, item, 0);
            }
        }

        private void Shared(HouseMesh data, string name, string item, int variant)
        {
            if (data == null)
            {
                return;
            }

            Mesh mesh = HouseMeshes.ToMesh(data, name, out HousePaint[] paints);
            var go = new GameObject(name);
            go.transform.SetParent(_street.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var materials = new Material[paints.Length];
            for (int i = 0; i < paints.Length; i++)
            {
                Rgb c = item != null ? HousePalette.Decor(paints[i], item, variant) : HousePalette.Decor(paints[i], "lamp_post_small", 0);
                materials[i] = _settlement.Shared.Materials.Get(paints[i], c);
            }

            r.sharedMaterials = materials;
        }

        private void BuildParticles()
        {
            var go = new GameObject("DistantBursts");
            go.transform.SetParent(transform, false);
            _particles = go.AddComponent<ParticleSystem>();
            _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = _particles.main;
            // Looping with emission off: the system keeps simulating the sparks that bursts emit by hand.
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = SparksPerBurst * 24;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.15f;
            ParticleSystem.EmissionModule emission = _particles.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = _particles.shape;
            shape.enabled = false;
            ParticleSystem.ColorOverLifetimeModule fade = _particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            ParticleSystem.LimitVelocityOverLifetimeModule drag = _particles.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 1.2f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = _spark;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _particles.Play();
        }

        private void Clear()
        {
            foreach (var p in _people)
            {
                if (p.view != null)
                {
                    HouseMeshes.Destroy(p.view.gameObject);
                }
            }

            _people.Clear();
            if (_street != null)
            {
                foreach (MeshFilter f in _street.GetComponentsInChildren<MeshFilter>())
                {
                    HouseMeshes.Destroy(f.sharedMesh);
                }

                HouseMeshes.Destroy(_street);
            }

            if (_particles != null)
            {
                HouseMeshes.Destroy(_particles.gameObject);
            }
        }

        private static float Hash(int i)
        {
            uint h = (uint)i * 2654435761u;
            return (h >> 8) / (float)(1 << 24);
        }
    }
}

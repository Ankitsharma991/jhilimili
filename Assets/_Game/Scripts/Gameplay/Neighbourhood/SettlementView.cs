using System;
using System.Collections.Generic;
using UnityEngine;
using UtsavNights.Core.Art;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Home;
using UtsavNights.Core.Neighbourhood;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// The neighbourhood in the Main scene (Phase 8 step 2, F19): the street, the open space, the ground, and every
    /// neighbour lot at the detail its chunk is planned at (<see cref="ChunkPlanner"/>). The home lot is the player's own
    /// house (<see cref="HomeInstaller"/>). Meshes and materials are shared across lots; neighbours have no real lights.
    /// Chunks are re-planned four times a second from <see cref="Focus"/> (the walking avatar, or the camera).
    /// </summary>
    public sealed class SettlementView : MonoBehaviour
    {
        public const float PlanInterval = 0.25f;

        private readonly Dictionary<string, LotView> _lots = new Dictionary<string, LotView>();
        private readonly Dictionary<int, List<LotView>> _byChunk = new Dictionary<int, List<LotView>>();
        private HomeShared _shared;
        private Texture2D[] _walls = new Texture2D[0];
        private Texture2D _ground;
        private ChunkPlanner _planner;
        private Func<int> _tier = () => 1;
        private float _clock;
        private bool _lowMemory;
        private GameObject _groundRoot;
        private GameObject _skyline;

        private sealed class LotView
        {
            public LotDef Lot;
            public HouseDesign Design;
            public ChunkDetail Detail;
            public GameObject Root;
        }

        /// <summary>A settlement was built (an environment chosen); celebration spots re-apply their poses.</summary>
        public event Action Loaded;

        public SettlementLayout Layout { get; private set; }

        public EnvironmentInfo Environment { get; private set; }

        /// <summary>Where the player is; chunk detail follows it. Defaults to the main camera.</summary>
        public Func<Vector3> Focus { get; set; }

        public ChunkPlanner Planner => _planner;

        public HomeShared Shared => _shared;

        public static SettlementView Create(Transform parent, string environment, Texture2D[] walls, Texture2D ground, Func<int> tier)
        {
            var go = new GameObject("Settlement");
            go.transform.SetParent(parent, false);
            SettlementView view = go.AddComponent<SettlementView>();
            view._walls = walls ?? new Texture2D[0];
            view._ground = ground;
            view._tier = tier ?? (() => 1);
            view.Load(environment);
            return view;
        }

        /// <summary>Builds an environment's settlement (dropping any other), planned from the current focus.</summary>
        public void Load(string environment)
        {
            Clear();
            Layout = SettlementLibrary.For(environment) ?? SettlementLibrary.For(Environments.TownLane);
            Environment = Environments.Find(Layout.environment);
            _shared = new HomeShared();
            Layout.ChunkRange(out int first, out int last);
            _planner = new ChunkPlanner(first, last, _tier());
            foreach (LotDef lot in Layout.lots)
            {
                if (lot.home)
                {
                    continue;
                }

                var view = new LotView { Lot = lot, Design = SettlementLibrary.DesignFor(Layout, lot, null) };
                _lots[lot.id] = view;
                int chunk = SettlementLayout.ChunkOf(lot.x);
                if (!_byChunk.TryGetValue(chunk, out List<LotView> list))
                {
                    _byChunk[chunk] = list = new List<LotView>();
                }

                list.Add(view);
            }

            BuildGround();
            Plan(force: true);
            Loaded?.Invoke();
        }

        /// <summary>The detail a lot is shown at (tests).</summary>
        public ChunkDetail DetailOf(string lotId) => _lots.TryGetValue(lotId, out LotView v) ? v.Detail : ChunkDetail.Unloaded;

        public int LotCount => _lots.Count;

        private void OnEnable() => Application.lowMemory += OnLowMemory;

        private void OnDisable() => Application.lowMemory -= OnLowMemory;

        private void OnDestroy()
        {
            Clear();
        }

        private void OnLowMemory()
        {
            // PRD 13.3: unload far chunks on memory pressure; the next plan drops them to impostors.
            _lowMemory = true;
            Plan(force: false);
        }

        private void Update()
        {
            _clock += Time.unscaledDeltaTime;
            if (_clock >= PlanInterval)
            {
                _clock = 0f;
                Plan(force: false);
            }
        }

        private void Plan(bool force)
        {
            if (_planner == null)
            {
                return;
            }

            _planner.Tier = _tier();
            Vector3 focus = Focus != null ? Focus() : Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            IReadOnlyList<int> changed = _planner.Update(focus.x, _lowMemory);
            if (force)
            {
                foreach (KeyValuePair<int, List<LotView>> pair in _byChunk)
                {
                    Apply(pair.Key);
                }

                return;
            }

            for (int i = 0; i < changed.Count; i++)
            {
                Apply(changed[i]);
            }
        }

        private void Apply(int chunk)
        {
            if (!_byChunk.TryGetValue(chunk, out List<LotView> lots))
            {
                return;
            }

            ChunkDetail detail = _planner.DetailOf(chunk);
            foreach (LotView lot in lots)
            {
                if (lot.Detail != detail || lot.Root == null)
                {
                    Build(lot, detail);
                }
            }
        }

        private void Build(LotView lot, ChunkDetail detail)
        {
            if (lot.Root != null)
            {
                HouseMeshes.Destroy(lot.Root);
                lot.Root = null;
            }

            lot.Detail = detail;
            if (detail == ChunkDetail.Unloaded)
            {
                return;
            }

            var root = new GameObject("Lot " + lot.Lot.id);
            root.transform.SetParent(transform, false);
            root.transform.SetPositionAndRotation(new Vector3(lot.Lot.x, 0f, lot.Lot.z), Quaternion.Euler(0f, lot.Lot.yaw, 0f));
            lot.Root = root;
            Floor(root.transform, lot);
            if (lot.Design == null)
            {
                return;
            }

            if (detail == ChunkDetail.Impostor)
            {
                Impostor(root.transform, lot);
                return;
            }

            HouseView house = HouseView.Create(root.transform, _walls, "House");
            house.transform.localPosition = Vector3.zero;
            house.Configure(_walls, _shared, lights: false);
            house.Show(lot.Design);
            DecorView decor = DecorView.Create(root.transform, "Decorations");
            decor.transform.localPosition = Vector3.zero;
            decor.Configure(_shared, detail == ChunkDetail.Simple ? ChunkPlanner.KeptAtSimple : (Func<DecorInfo, bool>)null, colliders: detail == ChunkDetail.Full);
            decor.Show(lot.Design.decorations);

            // The house's solid parts block crackers, as at home.
            var solids = new GameObject("Solids").transform;
            solids.SetParent(root.transform, false);
            foreach (Box3 b in DecorPlacement.For(lot.Design, 2).Solids)
            {
                var box = new GameObject("Solid").AddComponent<BoxCollider>();
                box.transform.SetParent(solids, false);
                box.center = new Vector3((b.Min.X + b.Max.X) / 2f, (b.Min.Y + b.Max.Y) / 2f, (b.Min.Z + b.Max.Z) / 2f);
                box.size = new Vector3(b.Max.X - b.Min.X, b.Max.Y - b.Min.Y, b.Max.Z - b.Min.Z);
            }
        }

        private void Impostor(Transform parent, LotView lot)
        {
            Mesh mesh = _shared.Mesh("impostor:" + lot.Lot.id, () => ImpostorBuilder.Build(lot.Design), out HousePaint[] paints);
            var go = new GameObject("Impostor");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var materials = new Material[paints.Length];
            for (int i = 0; i < paints.Length; i++)
            {
                Rgb c = HousePalette.Resolve(paints[i], lot.Design, lot.Design.ModuleAt("door"));
                bool wall = paints[i] == HousePaint.Wall;
                int texture = lot.Design.material;
                materials[i] = _shared.Materials.Get(paints[i], c, wall && texture < _walls.Length ? _walls[texture] : null,
                    wall ? HouseTexturePainter.Metres[Mathf.Clamp(texture, 0, HouseTexturePainter.Metres.Length - 1)] : 1f);
            }

            r.sharedMaterials = materials;
        }

        /// <summary>A lot's courtyard floor (or the bare plot of a vacant lot), in lot space.</summary>
        private void Floor(Transform parent, LotView lot)
        {
            const float inner = HouseGeometry.BoundaryCentre;
            float back = lot.Lot.vacant ? HouseGeometry.BackWallZ : HouseGeometry.BackWallZ - HouseGeometry.BoundaryThickness / 2f;
            float front = lot.Lot.vacant ? -HouseGeometry.Depth : 0f;
            Mesh mesh = _shared.Mesh(lot.Lot.vacant ? "floor:vacant" : "floor:courtyard", () =>
            {
                var m = new HouseMesh();
                m.Quad(new V3(-inner, 0.002f, front), new V3(inner, 0.002f, front), new V3(inner, 0.002f, back), new V3(-inner, 0.002f, back), new V3(0, 1, 0), HousePaint.Stone);
                return m;
            }, out HousePaint[] _);
            var go = new GameObject("Floor");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Rgb tint = lot.Lot.vacant ? Scale(Environment.Ground, 0.85f) : Environment.Ground;
            r.sharedMaterial = _shared.Materials.Get(HousePaint.Stone, tint, _ground, 2.5f);
            if (lot.Lot.vacant)
            {
                // The empty plot is a celebration spot: ground for crackers.
                var box = go.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, -0.05f, (front + back) / 2f);
                box.size = new Vector3(2f * inner, 0.1f, back - front);
                go.AddComponent<PlacementSurfaceMarker>().Configure(Content.PlacementSurface.Ground);
            }
        }

        /// <summary>The street, the open space and the land around the settlement.</summary>
        private void BuildGround()
        {
            _groundRoot = new GameObject("Ground");
            _groundRoot.transform.SetParent(transform, false);
            Area(Layout.street, 0.004f, Scale(Environment.Ground, 1.45f), 2.5f, "Street");
            Area(Layout.open, 0.003f, Scale(Environment.Ground, 1.3f), 4f, "Open");
            Layout.ChunkRange(out int first, out int last);
            // By the river the land stops at the ghat's edge, where the steps go down to the water.
            bool river = Layout.feature == EnvironmentFeatures.Features.Ghat;
            var land = new GroundArea
            {
                kind = "land", x0 = first * SettlementLayout.ChunkSize - 150f, x1 = last * SettlementLayout.ChunkSize + 150f, z0 = -80f,
                z1 = river ? Layout.open.z1 : Layout.open.z1 + 150f,
            };
            Area(land, -0.02f, Scale(Environment.Ground, 0.6f), 6f, "Land");
            BuildFeatures();

            // The scene's far landmarks (temple, water tank) would stand in the river.
            // Found once: GameObject.Find cannot see it again while it is hidden.
            _skyline = _skyline != null ? _skyline : GameObject.Find("Skyline");
            if (_skyline != null)
            {
                _skyline.SetActive(!river);
            }
        }

        /// <summary>The environment's own landmarks (ghat and river, chautari and hills, chowk platform) and their colliders.</summary>
        private void BuildFeatures()
        {
            HouseMesh data = EnvironmentFeatures.Build(Layout);
            if (data.VertexCount == 0)
            {
                return;
            }

            var go = new GameObject("Features");
            go.transform.SetParent(_groundRoot.transform, false);
            Mesh mesh = HouseMeshes.ToMesh(data, "Settlement_Features", out HousePaint[] paints);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var materials = new Material[paints.Length];
            for (int i = 0; i < paints.Length; i++)
            {
                Rgb c = paints[i] == HousePaint.Dark ? Scale(Environment.Ground, 0.12f) : HousePalette.Decor(paints[i], "feature", 0);
                if (paints[i] == HousePaint.Stone)
                {
                    c = Scale(Environment.Ground, 1.3f);
                }

                materials[i] = _shared.Materials.Get(paints[i], c, paints[i] == HousePaint.Stone ? _ground : null, 2.5f);
            }

            r.sharedMaterials = materials;
            foreach (Box3 b in EnvironmentFeatures.Solids(Layout))
            {
                var box = go.AddComponent<BoxCollider>();
                box.center = new Vector3((b.Min.X + b.Max.X) / 2f, (b.Min.Y + b.Max.Y) / 2f, (b.Min.Z + b.Max.Z) / 2f);
                box.size = new Vector3(b.Max.X - b.Min.X, b.Max.Y - b.Min.Y, b.Max.Z - b.Min.Z);
            }
        }

        private void Area(GroundArea a, float y, Rgb colour, float metres, string name)
        {
            var m = new HouseMesh();
            m.Quad(new V3(a.x0, y, a.z0), new V3(a.x1, y, a.z0), new V3(a.x1, y, a.z1), new V3(a.x0, y, a.z1), new V3(0, 1, 0), HousePaint.Stone);
            var go = new GameObject(name);
            go.transform.SetParent(_groundRoot.transform, false);
            Mesh mesh = HouseMeshes.ToMesh(m, "Settlement_" + name, out HousePaint[] _);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.sharedMaterial = _shared.Materials.Get(HousePaint.Stone, colour, _ground, metres);
            if (name != "Land")
            {
                // Walkable, and (step 4) ground for crackers at the spots on it.
                var box = go.AddComponent<BoxCollider>();
                box.center = new Vector3((a.x0 + a.x1) / 2f, y - 0.05f, (a.z0 + a.z1) / 2f);
                box.size = new Vector3(a.x1 - a.x0, 0.1f, a.z1 - a.z0);
                go.AddComponent<PlacementSurfaceMarker>().Configure(Content.PlacementSurface.Ground);
            }
        }

        private void Clear()
        {
            foreach (LotView lot in _lots.Values)
            {
                HouseMeshes.Destroy(lot.Root);
            }

            if (_groundRoot != null)
            {
                foreach (MeshFilter f in _groundRoot.GetComponentsInChildren<MeshFilter>())
                {
                    HouseMeshes.Destroy(f.sharedMesh);
                }

                HouseMeshes.Destroy(_groundRoot);
            }

            _lots.Clear();
            _byChunk.Clear();
            _shared?.Clear();
            _lowMemory = false;
        }

        private static Rgb Scale(Rgb c, float k) => new Rgb(c.R * k, c.G * k, c.B * k);
    }
}

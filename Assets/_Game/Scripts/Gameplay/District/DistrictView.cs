using System.Collections.Generic;
using UnityEngine;
using UtsavNights.Core.District;
using UtsavNights.Core.Home;
using UtsavNights.Core.Neighbourhood;
using UtsavNights.Core.Runtime;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Draws the bazaar district next to the player's lane (Phase 12 step 3), while the player explores: every 50 m tile is
    /// built once when Explore opens (full and simple meshes and the colliders), then streamed by switching: full detail
    /// with colliders near the player, simple farther, nothing beyond. Switching allocates nothing, so walking never
    /// stutters; leaving Explore frees it all. Only with the town lane neighbourhood, and not on the low tier.
    /// </summary>
    [DefaultExecutionOrder(-120)]
    public sealed class DistrictView : MonoBehaviour
    {
        public const float PlanInterval = 0.25f;

        private sealed class Tile
        {
            public TileDetail Detail;
            public GameObject Full;
            public GameObject Simple;
            public GameObject Colliders;
            public Mesh FullMesh;
            public Mesh SimpleMesh;
            public int FullTriangles;
            public int SimpleTriangles;
        }

        private Tile[,] _tiles;
        private HomeMaterials _materials;
        private SettlementView _settlement;
        private ExploreController _explore;
        private float _clock;
        private bool _registered;

        public DistrictPlan Plan { get; private set; }

        /// <summary>Whether the district is part of the world now: exploring, the town lane, a tier above the lowest.</summary>
        public bool Active { get; private set; }

        public int BuiltTiles { get; private set; }

        /// <summary>Triangles drawn now across the district's tiles.</summary>
        public int Triangles { get; private set; }

        /// <summary>Triangles held in memory for the whole district (both details), while it is active.</summary>
        public int HeldTriangles { get; private set; }

        public TileDetail DetailOf(int tx, int tz) => _tiles != null ? _tiles[tx, tz].Detail : TileDetail.Hidden;

        public bool IsWalkable(Vector3 world) => Active && Plan.IsWalkable(world.x, world.z);

        private void Awake()
        {
            Plan = DistrictGenerator.Bazaar;
            if (!Game.Services.IsRegistered<DistrictView>())
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
                Game.Services.Unregister<DistrictView>();
            }
        }

        private void Update()
        {
            _clock -= Time.unscaledDeltaTime;
            if (_clock > 0f)
            {
                return;
            }

            _clock = PlanInterval;
            Refresh();
        }

        /// <summary>Plans now (tests, and after a jump in position).</summary>
        public void Refresh()
        {
            _clock = PlanInterval;
            if (_settlement == null)
            {
                Game.Services.TryGet(out _settlement);
            }

            if (_explore == null)
            {
                _explore = FindFirstObjectByType<ExploreController>();
            }

            int tier = Game.Services.TryGet(out QualityManager quality) ? quality.Tier : 1;
            bool active = _explore != null && _explore.IsOpen && _explore.Player != null && _settlement != null && _settlement.Layout != null
                && _settlement.Layout.environment == Environments.TownLane && tier >= 1;
            if (active != Active)
            {
                Active = active;
                if (active)
                {
                    Load();
                }
                else
                {
                    Unload();
                }
            }

            if (!Active)
            {
                return;
            }

            Vector3 focus = _explore.Player.position;
            int built = 0, tris = 0;
            for (int tx = 0; tx < Plan.TilesX; tx++)
            {
                for (int tz = 0; tz < Plan.TilesZ; tz++)
                {
                    Tile t = _tiles[tx, tz];
                    TileDetail want = TilePlanner.Detail(Plan, tx, tz, focus.x, focus.z, tier);
                    if (want != t.Detail)
                    {
                        t.Detail = want;
                        t.Full.SetActive(want == TileDetail.Full);
                        t.Simple.SetActive(want == TileDetail.Simple);
                        t.Colliders.SetActive(want == TileDetail.Full);
                    }

                    if (want != TileDetail.Hidden)
                    {
                        built++;
                        tris += want == TileDetail.Full ? t.FullTriangles : t.SimpleTriangles;
                    }
                }
            }

            BuiltTiles = built;
            Triangles = tris;
        }

        /// <summary>Builds every tile once (both details and the colliders), all switched off.</summary>
        private void Load()
        {
            _materials ??= new HomeMaterials();
            _tiles = new Tile[Plan.TilesX, Plan.TilesZ];
            HeldTriangles = 0;
            for (int tx = 0; tx < Plan.TilesX; tx++)
            {
                for (int tz = 0; tz < Plan.TilesZ; tz++)
                {
                    var t = new Tile { Detail = TileDetail.Hidden };
                    HouseMesh full = DistrictKit.Tile(Plan, tx, tz, simple: false);
                    HouseMesh simple = DistrictKit.Tile(Plan, tx, tz, simple: true);
                    t.FullTriangles = full.TriangleCount;
                    t.SimpleTriangles = simple.TriangleCount;
                    t.Full = Draw($"DistrictTile_{tx}_{tz}", full, out t.FullMesh);
                    t.Simple = Draw($"DistrictTile_{tx}_{tz}_far", simple, out t.SimpleMesh);
                    t.Colliders = new GameObject($"DistrictSolids_{tx}_{tz}");
                    t.Colliders.transform.SetParent(transform, false);
                    Colliders(t.Colliders.transform, tx, tz);
                    t.Full.SetActive(false);
                    t.Simple.SetActive(false);
                    t.Colliders.SetActive(false);
                    HeldTriangles += t.FullTriangles + t.SimpleTriangles;
                    _tiles[tx, tz] = t;
                }
            }
        }

        private void Unload()
        {
            if (_tiles != null)
            {
                foreach (Tile t in _tiles)
                {
                    Destroy(t.Full);
                    Destroy(t.Simple);
                    Destroy(t.Colliders);
                    HouseMeshes.Destroy(t.FullMesh);
                    HouseMeshes.Destroy(t.SimpleMesh);
                }
            }

            _tiles = null;
            _materials?.Clear();
            _materials = null;
            BuiltTiles = 0;
            Triangles = 0;
            HeldTriangles = 0;
        }

        private GameObject Draw(string name, HouseMesh data, out Mesh mesh)
        {
            mesh = HouseMeshes.ToMesh(data, name, out HousePaint[] paints);
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var materials = new Material[paints.Length];
            for (int i = 0; i < paints.Length; i++)
            {
                materials[i] = _materials.Get(paints[i], DistrictPalette.Colour(paints[i]));
            }

            renderer.sharedMaterials = materials;
            return go;
        }

        /// <summary>The buildings and solid props of a tile as boxes, for the character and the camera.</summary>
        private void Colliders(Transform parent, int tx, int tz)
        {
            foreach (DistrictBuilding b in Plan.Buildings)
            {
                if (Plan.TileOf(b.Footprint) == (tx, tz))
                {
                    Box(parent, b.Footprint, b.Height + 0.5f);
                }
            }

            foreach (DistrictProp p in Plan.Props)
            {
                if (p.Solid && Plan.TileOf(p.Footprint) == (tx, tz))
                {
                    Box(parent, p.Footprint, p.Kind == DistrictProp.Tower ? 12.4f : 2.3f);
                }
            }
        }

        private static void Box(Transform parent, Area a, float height)
        {
            var go = new GameObject("Solid");
            go.transform.SetParent(parent, false);
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(a.CentreX, height / 2f, a.CentreZ);
            box.size = new Vector3(a.Width, height, a.Depth);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Home;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Shows placed decorations (Phase 7 step 3). Free and rangoli items share one mesh per item id, placed by transform
    /// (position, yaw, scale); path items get their own mesh in house space. <see cref="Show"/> touches only items that
    /// were added, removed or changed. The root sits at the house origin, so item coordinates are house space.
    /// </summary>
    public sealed class DecorView : MonoBehaviour
    {
        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();
        private readonly Dictionary<string, (Mesh mesh, HousePaint[] paints)> _shared = new Dictionary<string, (Mesh, HousePaint[])>();
        private HomeMaterials _materials = new HomeMaterials();
        private HomeShared _common;
        private System.Func<DecorInfo, bool> _filter;
        private bool _colliders = true;

        private sealed class Entry
        {
            public GameObject Go;
            public MeshRenderer Renderer;
            public Mesh OwnMesh;
            public HousePaint[] Paints;
            public string Fingerprint;
        }

        public int Count => _entries.Count;

        public int MaterialCount => _materials.Count;

        public static DecorView Create(Transform parent, string name = "Decorations")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0f, HouseGeometry.WorldFrontZ);
            return go.AddComponent<DecorView>();
        }

        /// <summary>
        /// For neighbour lots: the settlement's shared meshes and materials, which items to show (simple detail keeps only
        /// the ones that glow), and whether items get colliders (only where the player can place crackers nearby).
        /// </summary>
        public void Configure(HomeShared common, System.Func<DecorInfo, bool> filter = null, bool colliders = true)
        {
            _common = common;
            _filter = filter;
            _colliders = colliders;
            if (common != null)
            {
                _materials = common.Materials;
            }
        }

        public void Show(IReadOnlyList<PlacedDecor> decorations)
        {
            var keep = new HashSet<int>();
            foreach (PlacedDecor d in decorations)
            {
                DecorInfo info = HomeCatalog.Decoration(d?.item);
                if (info == null || (_filter != null && !_filter(info)))
                {
                    continue;
                }

                keep.Add(d.uid);
                string fp = d.Fingerprint();
                _entries.TryGetValue(d.uid, out Entry entry);
                if (entry != null && entry.Fingerprint == fp)
                {
                    continue;
                }

                if (entry == null || info.Mode == PlacementMode.Path || entry.Go.name != Name(d))
                {
                    Release(entry);
                    entry = Build(d, info);
                    if (entry == null)
                    {
                        _entries.Remove(d.uid);
                        continue;
                    }

                    _entries[d.uid] = entry;
                }

                Place(entry, d, info);
                entry.Fingerprint = fp;
            }

            var gone = new List<int>();
            foreach (int uid in _entries.Keys)
            {
                if (!keep.Contains(uid))
                {
                    gone.Add(uid);
                }
            }

            foreach (int uid in gone)
            {
                Release(_entries[uid]);
                _entries.Remove(uid);
            }
        }

        public GameObject ObjectFor(int uid) => _entries.TryGetValue(uid, out Entry e) ? e.Go : null;

        private void OnDestroy()
        {
            foreach (Entry e in _entries.Values)
            {
                Release(e);
            }

            foreach ((Mesh mesh, HousePaint[] _) in _shared.Values)
            {
                HouseMeshes.Destroy(mesh);
            }

            _entries.Clear();
            _shared.Clear();
            if (_common == null)
            {
                _materials.Clear();
            }
        }

        private Entry Build(PlacedDecor d, DecorInfo info)
        {
            Mesh mesh;
            HousePaint[] paints;
            Mesh own = null;
            if (info.Mode == PlacementMode.Path)
            {
                HouseMesh data = DecorGenerator.Path(d.item, new V3(d.x, d.y, d.z), new V3(d.ex, d.ey, d.ez), d.sag);
                if (data == null)
                {
                    return null;
                }

                mesh = own = HouseMeshes.ToMesh(data, "Decor_" + d.item + "_" + d.uid, out paints);
            }
            else if (_common != null)
            {
                string item = d.item;
                mesh = _common.Mesh("decor:" + item, () => DecorGenerator.Item(item), out paints);
            }
            else if (_shared.TryGetValue(d.item, out (Mesh mesh, HousePaint[] paints) cached))
            {
                (mesh, paints) = cached;
            }
            else
            {
                mesh = HouseMeshes.ToMesh(DecorGenerator.Item(d.item), "Decor_" + d.item, out paints);
                _shared[d.item] = (mesh, paints);
            }

            var go = new GameObject(Name(d));
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            if (info.Mode == PlacementMode.Free && _colliders)
            {
                // Standing and hung items block crackers like any other object; rangoli and strands do not.
                var box = go.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, info.Height / 2f, 0f);
                box.size = new Vector3(info.Width, info.Height, info.Depth);
            }

            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return new Entry { Go = go, Renderer = renderer, OwnMesh = own, Paints = paints };
        }

        private void Place(Entry entry, PlacedDecor d, DecorInfo info)
        {
            Transform t = entry.Go.transform;
            if (info.Mode == PlacementMode.Path)
            {
                t.localPosition = Vector3.zero;
                t.localRotation = Quaternion.identity;
                t.localScale = Vector3.one;
            }
            else
            {
                t.localPosition = new Vector3(d.x, d.y, d.z);
                t.localRotation = Quaternion.Euler(0f, d.yaw, 0f);
                t.localScale = Vector3.one * d.scale;
            }

            // Colour variants are materials only.
            var materials = new Material[entry.Paints.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = _materials.Get(entry.Paints[i], HousePalette.Decor(entry.Paints[i], d.item, d.variant));
            }

            entry.Renderer.sharedMaterials = materials;
        }

        private static string Name(PlacedDecor d) => d.item + "#" + d.uid;

        private static void Release(Entry e)
        {
            if (e == null)
            {
                return;
            }

            HouseMeshes.Destroy(e.OwnMesh);
            HouseMeshes.Destroy(e.Go);
        }
    }
}

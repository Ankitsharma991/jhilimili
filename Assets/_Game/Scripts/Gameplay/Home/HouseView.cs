using System.Collections.Generic;
using UnityEngine;
using UtsavNights.Core.Art;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Home;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Shows a <see cref="HouseDesign"/>: the shell and one child per attachment point, built by <see cref="HouseGenerator"/>
    /// (Phase 7 step 2). <see cref="Show"/> rebuilds only the pieces whose module changed; colours and the wall material
    /// change materials only. Materials are shared per paint and colour, so a house is a handful of SRP batches.
    /// </summary>
    public sealed class HouseView : MonoBehaviour
    {
        [SerializeField] private Texture2D[] wallTextures = new Texture2D[0];

        private readonly Dictionary<string, Piece> _pieces = new Dictionary<string, Piece>();
        private HomeMaterials _materials = new HomeMaterials();
        private HomeShared _shared;
        private bool _withLights = true;
        private bool _gateOpen;
        private Piece _shell;
        private HouseDesign _shown;
        private Light[] _lights;

        private sealed class Piece
        {
            public GameObject Go;
            public MeshRenderer Renderer;
            public Mesh Mesh;
            public string Module;
            public HousePaint[] Paints;
            public bool SharedMesh;
            public bool Open;
        }

        /// <summary>The design on screen (a copy), or null before the first <see cref="Show"/>.</summary>
        public HouseDesign Shown => _shown;

        public int PieceCount => _pieces.Count + (_shell != null ? 1 : 0);

        public static HouseView Create(Transform parent, Texture2D[] wallTextures, string name = "House")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0f, HouseGeometry.WorldFrontZ);
            HouseView view = go.AddComponent<HouseView>();
            view.Configure(wallTextures);
            return view;
        }

        /// <summary>
        /// Wall textures by <see cref="HomeCatalog.Materials"/> index (plaster, brick, stone, wood). Neighbour houses pass the
        /// settlement's <paramref name="shared"/> meshes and materials, and no lights (PRD 10: no real lights in the
        /// neighbourhood; windows glow).
        /// </summary>
        public void Configure(Texture2D[] textures, HomeShared shared = null, bool lights = true)
        {
            wallTextures = textures ?? new Texture2D[0];
            _shared = shared;
            _withLights = lights;
            if (shared != null)
            {
                _materials = shared.Materials;
            }
        }

        public void Show(HouseDesign design)
        {
            if (_withLights)
            {
                EnsureLights();
            }

            bool balconyEmpty = string.IsNullOrEmpty(design.ModuleAt("balcony")?.module);
            bool shellChanged = _shell == null || _shown == null || balconyEmpty != string.IsNullOrEmpty(_shown.ModuleAt("balcony")?.module);
            if (shellChanged)
            {
                Release(_shell);
                _shell = Build("Shell", balconyEmpty ? "juliet" : null, () => HouseGenerator.Shell(balconyEmpty));
            }

            foreach (ModuleChoice choice in design.modules)
            {
                _pieces.TryGetValue(choice.point, out Piece piece);
                bool open = _gateOpen && choice.point == "gate";
                if (piece != null && piece.Module == choice.module && piece.Open == open)
                {
                    continue;
                }

                Release(piece);
                _pieces.Remove(choice.point);
                if (!string.IsNullOrEmpty(choice.module))
                {
                    string point = choice.point, module = choice.module;
                    Piece built = Build(point, open ? module + ":open" : module, () => HouseGenerator.Module(point, module, open));
                    if (built != null)
                    {
                        built.Module = module;
                        built.Open = open;
                        _pieces[point] = built;
                    }
                }
            }

            ApplyMaterials(_shell, design, null);
            foreach (ModuleChoice choice in design.modules)
            {
                if (_pieces.TryGetValue(choice.point, out Piece piece))
                {
                    ApplyMaterials(piece, design, choice);
                }
            }

            _shown = design.Clone();
        }

        /// <summary>Swings the gate's leaves open (the player walks out) or shut.</summary>
        public void SetGateOpen(bool open)
        {
            if (_gateOpen == open)
            {
                return;
            }

            _gateOpen = open;
            if (_shown != null)
            {
                Show(_shown);
            }
        }

        public bool GateOpen => _gateOpen;

        /// <summary>The house's own lights; decoration mode keeps them on, the fireworks view may turn them off.</summary>
        public void SetLightsEnabled(bool on)
        {
            if (!_withLights)
            {
                return;
            }

            EnsureLights();
            foreach (Light l in _lights)
            {
                if (l != null)
                {
                    l.enabled = on;
                }
            }
        }

        /// <summary>The renderer of a point's module (tests and the decoration-mode highlight), or null.</summary>
        public MeshRenderer RendererAt(string pointId) => _pieces.TryGetValue(pointId, out Piece p) ? p.Renderer : null;

        public MeshRenderer ShellRenderer => _shell?.Renderer;

        private void OnDestroy()
        {
            Release(_shell);
            foreach (Piece p in _pieces.Values)
            {
                Release(p);
            }

            _pieces.Clear();
            if (_shared == null)
            {
                _materials.Clear();
            }
        }

        /// <summary>
        /// The moon shines toward +z, so the front would get only ambient light. A warm porch lamp over the door, and a
        /// cool uplight from the far end of the courtyard aimed up at the front (its cone stays above the floor where
        /// crackers stand). Realtime, no shadows: two additional lights, within every tier's per-object limit.
        /// </summary>
        private void EnsureLights()
        {
            if (_lights != null)
            {
                return;
            }

            Light porch = NewLight("PorchLamp", LightType.Point, new Vector3(0f, 2.75f, 0.5f), new Color(1f, 0.7f, 0.42f), 1.6f, 3.6f);
            Light wash = NewLight("FacadeUplight", LightType.Spot, new Vector3(0f, 1.2f, 5.6f), new Color(0.62f, 0.68f, 1f), 3.2f, 11f);
            wash.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 3.6f, 0f) - wash.transform.localPosition);
            wash.spotAngle = 72f;
            wash.innerSpotAngle = 40f;
            _lights = new[] { porch, wash };
        }

        private Light NewLight(string name, LightType type, Vector3 localPosition, Color colour, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPosition;
            Light light = go.AddComponent<Light>();
            light.type = type;
            light.color = colour;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            return light;
        }

        private Piece Build(string name, string module, System.Func<HouseMesh> data)
        {
            Mesh mesh;
            HousePaint[] paints;
            bool shared = _shared != null;
            if (shared)
            {
                mesh = _shared.Mesh(name + ":" + (module ?? "plain"), data, out paints);
            }
            else
            {
                HouseMesh built = data();
                if (built == null)
                {
                    return null;
                }

                mesh = HouseMeshes.ToMesh(built, "House_" + name, out paints);
            }

            if (mesh == null)
            {
                return null;
            }

            var go = new GameObject(module != null && module != "juliet" ? name + ":" + module : name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return new Piece { Go = go, Renderer = renderer, Mesh = mesh, Module = module, Paints = paints, SharedMesh = shared };
        }

        private void ApplyMaterials(Piece piece, HouseDesign design, ModuleChoice choice)
        {
            var materials = new Material[piece.Paints.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                HousePaint paint = piece.Paints[i];
                materials[i] = MaterialFor(paint, HousePalette.Resolve(paint, design, choice), paint == HousePaint.Wall ? design.material : paint == HousePaint.Brick ? 1 : -1);
            }

            piece.Renderer.sharedMaterials = materials;
        }

        private Material MaterialFor(HousePaint paint, Rgb colour, int textureIndex)
        {
            bool textured = textureIndex >= 0 && textureIndex < wallTextures.Length && wallTextures[textureIndex] != null;
            return _materials.Get(paint, colour, textured ? wallTextures[textureIndex] : null, textured ? HouseTexturePainter.Metres[textureIndex] : 1f);
        }

        private static void Release(Piece piece)
        {
            if (piece == null)
            {
                return;
            }

            if (!piece.SharedMesh)
            {
                HouseMeshes.Destroy(piece.Mesh);
            }

            HouseMeshes.Destroy(piece.Go);
        }

    }
}

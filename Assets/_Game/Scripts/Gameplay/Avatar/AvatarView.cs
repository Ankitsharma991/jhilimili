using System.Collections.Generic;
using UnityEngine;
using UtsavNights.Core.Art;
using UtsavNights.Core.Avatar;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// A procedural avatar in the scene (Phase 6 step 2): the shared skeleton, the skinned body with its body-shape blend
    /// shapes, and the head with face, hair, facial hair and glasses on the Head bone. <see cref="Apply"/> sets everything
    /// from an <see cref="AvatarProfile"/>; the body mesh is built once and shared by every avatar.
    /// </summary>
    public sealed class AvatarView : MonoBehaviour
    {
        private static Mesh _sharedBody;
        private static MeshData _bodyData;
        private static readonly Dictionary<string, (Mesh skinned, Mesh head)> SharedGarments = new Dictionary<string, (Mesh, Mesh)>();
        private readonly Dictionary<string, Worn> _worn = new Dictionary<string, Worn>();

        private readonly Transform[] _bones = new Transform[AvatarSkeleton.Count];
        private SkinnedMeshRenderer _body;
        private MeshFilter _headFilter;
        private MeshRenderer _headRenderer;
        private Mesh _bodyInstance;
        private Mesh _headMesh;
        private Material _skin;
        private Material _hair;
        private Material _features;
        private Texture2D _palette;
        private BodyRegion _masked = (BodyRegion)(-1);

        public AvatarProfile Profile { get; private set; }

        public IReadOnlyList<Transform> Bones => _bones;

        public Transform Bone(int index) => _bones[index];

        public SkinnedMeshRenderer Body => _body;

        /// <summary>The hips' rest position after leg length; animation offsets are added to it.</summary>
        public Vector3 HipsBase { get; private set; }

        public Material SkinMaterial => _skin;

        /// <summary>Regions covered by garments this view draws; the skin there is hidden. Set by the garment layer.</summary>
        public BodyRegion DrawnGarmentsCover { get; set; } = BodyRegion.None;

        public static AvatarView Create(Transform parent, AvatarProfile profile, string name = "Avatar")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<AvatarView>();
            view.Build();
            view.Apply(profile);
            return view;
        }

        public void Apply(AvatarProfile profile)
        {
            Profile = profile.Clone();
            Profile.Sanitize(WardrobeCatalog.Default);

            foreach (KeyValuePair<string, float> w in BodyGenerator.Weights(Profile))
            {
                int index = _bodyInstance.GetBlendShapeIndex(w.Key);
                if (index >= 0)
                {
                    _body.SetBlendShapeWeight(index, w.Value);
                }
            }

            Rgb tone = SkinTones.Evaluate(Profile.skinTone);
            _skin.SetColor("_BaseColor", new Color(tone.R, tone.G, tone.B));
            Rgb hair = HairColours.All[Mathf.Clamp(Profile.hairColour, 0, HairColours.All.Length - 1)];
            _hair.SetColor("_BaseColor", new Color(hair.R, hair.G, hair.B));
            PaintPalette(tone);

            // Proportions: height scales everything; leg length stretches the legs and lifts the hips to keep the feet on
            // the floor; head size scales the Head bone and the head built on it.
            float height = AvatarSkeleton.HeightScale(Profile.height);
            transform.localScale = Vector3.one * height;
            float legScale = 1f + 0.08f * Profile.legLength;
            foreach (int leg in new[] { AvatarSkeleton.LeftUpperLeg, AvatarSkeleton.RightUpperLeg })
            {
                _bones[leg].localScale = new Vector3(1f, legScale, 1f);
            }

            Vector3 hips = ToVector(AvatarSkeleton.Rest[AvatarSkeleton.Hips]);
            HipsBase = hips + Vector3.up * (AvatarSkeleton.LegLength * (legScale - 1f));
            _bones[AvatarSkeleton.Hips].localPosition = HipsBase;
            _bones[AvatarSkeleton.Head].localScale = Vector3.one * (AvatarSkeleton.HeadScale * (1f + 0.1f * Profile.headSize));

            Dress();
            SetSkinMask(DrawnGarmentsCover);
            RebuildHead(OutfitRules.HairHidden(Profile, WardrobeCatalog.Default));
        }

        /// <summary>Hides the body triangles under clothing so skin never shows through a garment.</summary>
        public void SetSkinMask(BodyRegion covered)
        {
            if (covered == _masked)
            {
                return;
            }

            _masked = covered;
            _bodyInstance.SetTriangles(_bodyData.TrianglesExcept(BodyGenerator.SkinSubmesh, covered), BodyGenerator.SkinSubmesh);
        }

        /// <summary>
        /// Puts on the profile's outfit: one renderer per garment, sharing the skeleton and the body's blend weights, coloured
        /// by its variant. Garments no longer worn are removed. Then hides the skin they cover.
        /// </summary>
        private void Dress()
        {
            var wanted = new HashSet<string>();
            BodyRegion covered = BodyRegion.None;
            foreach (EquippedItem item in Profile.outfit)
            {
                GarmentInfo info = WardrobeCatalog.Default.Find(item.id);
                if (info == null)
                {
                    continue;
                }

                wanted.Add(item.id);
                if (!_worn.TryGetValue(item.id, out Worn worn))
                {
                    worn = Wear(item.id);
                    _worn.Add(item.id, worn);
                }

                Rgb main = GarmentLooks.Main(item.id, item.variant), trim = GarmentLooks.Trim(item.id, item.variant);
                worn.Main.SetColor("_BaseColor", new Color(main.R, main.G, main.B));
                worn.Trim.SetColor("_BaseColor", new Color(trim.R, trim.G, trim.B));
                if (worn.Skinned != null)
                {
                    foreach (KeyValuePair<string, float> w in BodyGenerator.Weights(Profile))
                    {
                        int index = worn.Skinned.sharedMesh.GetBlendShapeIndex(w.Key);
                        if (index >= 0)
                        {
                            worn.Skinned.SetBlendShapeWeight(index, w.Value);
                        }
                    }
                }

                covered |= info.Covers;
            }

            var remove = new List<string>();
            foreach (KeyValuePair<string, Worn> pair in _worn)
            {
                if (!wanted.Contains(pair.Key))
                {
                    remove.Add(pair.Key);
                }
            }

            foreach (string id in remove)
            {
                _worn[id].Release();
                _worn.Remove(id);
            }

            DrawnGarmentsCover = covered;
        }

        private Worn Wear(string id)
        {
            if (!SharedGarments.TryGetValue(id, out (Mesh skinned, Mesh head) meshes))
            {
                GarmentMesh data = GarmentGenerator.Build(id);
                meshes = (data.Skinned != null ? ToMesh(data.Skinned, "Garment_" + id, skinned: true) : null,
                    data.HeadRigid != null ? ToMesh(data.HeadRigid, "Garment_" + id + "_head", skinned: false, origin: AvatarSkeleton.Rest[AvatarSkeleton.Head]) : null);
                SharedGarments[id] = meshes;
            }

            // Open hems, cuffs and drapes are seen from both sides.
            var worn = new Worn { Main = NewMaterial("Garment_" + id, Color.white, 0.2f), Trim = NewMaterial("Garment_" + id + "_trim", Color.white, 0.45f) };
            worn.Main.SetFloat("_Cull", 0f);
            worn.Trim.SetFloat("_Cull", 0f);
            if (meshes.skinned != null)
            {
                var go = new GameObject("Garment " + id);
                go.transform.SetParent(transform, false);
                worn.Skinned = go.AddComponent<SkinnedMeshRenderer>();
                worn.Skinned.sharedMesh = meshes.skinned;
                worn.Skinned.bones = _bones;
                worn.Skinned.rootBone = _bones[AvatarSkeleton.Hips];
                worn.Skinned.localBounds = _body.localBounds;
                worn.Skinned.sharedMaterials = new[] { worn.Main, worn.Trim };
            }

            if (meshes.head != null)
            {
                var go = new GameObject("Garment " + id + " (head)");
                go.transform.SetParent(_bones[AvatarSkeleton.Head], false);
                go.AddComponent<MeshFilter>().sharedMesh = meshes.head;
                worn.Head = go.AddComponent<MeshRenderer>();
                worn.Head.sharedMaterials = new[] { worn.Main, worn.Trim };
            }

            return worn;
        }

        /// <summary>The renderers and materials of one worn garment.</summary>
        private sealed class Worn
        {
            public SkinnedMeshRenderer Skinned;
            public MeshRenderer Head;
            public Material Main;
            public Material Trim;

            public void Release()
            {
                if (Skinned != null)
                {
                    AvatarView.Release(Skinned.gameObject);
                }

                if (Head != null)
                {
                    AvatarView.Release(Head.gameObject);
                }

                AvatarView.Release(Main);
                AvatarView.Release(Trim);
            }
        }

        /// <summary>Garment renderers by id, for tests and the clipping check.</summary>
        public IEnumerable<Renderer> GarmentRenderers
        {
            get
            {
                foreach (Worn w in _worn.Values)
                {
                    if (w.Skinned != null)
                    {
                        yield return w.Skinned;
                    }

                    if (w.Head != null)
                    {
                        yield return w.Head;
                    }
                }
            }
        }

        private void Build()
        {
            for (int i = 0; i < AvatarSkeleton.Count; i++)
            {
                var bone = new GameObject(AvatarSkeleton.Names[i]).transform;
                int parent = AvatarSkeleton.Parents[i];
                bone.SetParent(parent < 0 ? transform : _bones[parent], false);
                V3 local = parent < 0 ? AvatarSkeleton.Rest[i] : AvatarSkeleton.Rest[i] - AvatarSkeleton.Rest[parent];
                bone.localPosition = ToVector(local);
                _bones[i] = bone;
            }

            if (_sharedBody == null)
            {
                _bodyData = BodyGenerator.Build();
                _sharedBody = ToMesh(_bodyData, "AvatarBody", skinned: true);
            }

            // Each avatar masks its own triangles, so it works on a copy of the shared body.
            _bodyInstance = Instantiate(_sharedBody);
            _bodyInstance.name = "AvatarBody (instance)";

            _skin = NewMaterial("Avatar_Skin", Color.white, 0.25f);
            _hair = NewMaterial("Avatar_Hair", Color.black, 0.18f);
            _hair.SetFloat("_Cull", 0f); // shells are open: draw both sides
            _features = NewMaterial("Avatar_Features", Color.white, 0.55f);
            _palette = new Texture2D(HeadGenerator.PaletteCells, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            _features.SetTexture("_BaseMap", _palette);

            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(transform, false);
            _body = bodyGo.AddComponent<SkinnedMeshRenderer>();
            _body.sharedMesh = _bodyInstance;
            _body.bones = _bones;
            _body.rootBone = _bones[AvatarSkeleton.Hips];
            _body.sharedMaterial = _skin;
            _body.updateWhenOffscreen = false;
            _body.localBounds = new Bounds(new Vector3(0f, -0.1f, 0f), new Vector3(1.2f, 2.2f, 1f));

            var headGo = new GameObject("HeadParts");
            headGo.transform.SetParent(_bones[AvatarSkeleton.Head], false);
            _headFilter = headGo.AddComponent<MeshFilter>();
            _headRenderer = headGo.AddComponent<MeshRenderer>();
            _headRenderer.sharedMaterials = new[] { _skin, _hair, _features };
        }

        private void RebuildHead(bool hideHair)
        {
            Release(_headMesh);

            MeshData data = HeadGenerator.Build(Profile, hideHair);
            _headMesh = ToMesh(data, "AvatarHead", skinned: false, origin: AvatarSkeleton.Rest[AvatarSkeleton.Head]);
            _headFilter.sharedMesh = _headMesh;
        }

        private void PaintPalette(Rgb tone)
        {
            var cells = new Color[HeadGenerator.PaletteCells];
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = Color.magenta;
            }

            cells[HeadGenerator.CellWhite] = new Color(0.95f, 0.94f, 0.9f);
            cells[HeadGenerator.CellBlack] = new Color(0.05f, 0.04f, 0.04f);

            // Lips: the skin tone pulled towards a muted rose, so they suit every tone.
            cells[HeadGenerator.CellLips] = Color.Lerp(new Color(tone.R, tone.G, tone.B) * 0.72f, new Color(0.55f, 0.22f, 0.24f), 0.35f);
            Color[] eyes =
            {
                new Color(0.18f, 0.11f, 0.06f), new Color(0.32f, 0.2f, 0.1f), new Color(0.45f, 0.32f, 0.14f),
                new Color(0.2f, 0.35f, 0.25f), new Color(0.25f, 0.4f, 0.6f), new Color(0.38f, 0.38f, 0.4f),
            };
            for (int i = 0; i < eyes.Length; i++)
            {
                cells[HeadGenerator.CellEyeFirst + i] = eyes[i];
            }

            cells[HeadGenerator.CellLens] = new Color(0.08f, 0.09f, 0.12f);
            _palette.SetPixels(cells);
            _palette.Apply(false);
        }

        private void OnDestroy()
        {
            foreach (Worn w in _worn.Values)
            {
                w.Release();
            }

            Release(_bodyInstance);
            Release(_headMesh);
            Release(_skin);
            Release(_hair);
            Release(_features);
            Release(_palette);
        }

        /// <summary>Destroy in play, DestroyImmediate in the editor (art capture builds avatars outside play mode).</summary>
        private static void Release(Object asset)
        {
            if (asset == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(asset);
            }
            else
            {
                DestroyImmediate(asset);
            }
        }

        private static Mesh ToMesh(MeshData data, string name, bool skinned, V3 origin = default)
        {
            var mesh = new Mesh { name = name };
            var vertices = new Vector3[data.VertexCount];
            var normals = new Vector3[data.VertexCount];
            var uvs = new Vector2[data.VertexCount];
            for (int i = 0; i < data.VertexCount; i++)
            {
                vertices[i] = ToVector(data.Positions[i] - origin);
                normals[i] = ToVector(data.Normals[i]);
                uvs[i] = new Vector2(data.U[i], data.V[i]);
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.subMeshCount = data.Submeshes.Count;
            for (int s = 0; s < data.Submeshes.Count; s++)
            {
                mesh.SetTriangles(data.Submeshes[s], s);
            }

            if (skinned)
            {
                var weights = new BoneWeight[data.VertexCount];
                for (int i = 0; i < data.VertexCount; i++)
                {
                    weights[i] = new BoneWeight
                    {
                        boneIndex0 = data.BoneA[i], weight0 = 1f - data.WeightB[i],
                        boneIndex1 = data.BoneB[i], weight1 = data.WeightB[i],
                    };
                }

                mesh.boneWeights = weights;
                var bindposes = new Matrix4x4[AvatarSkeleton.Count];
                for (int b = 0; b < AvatarSkeleton.Count; b++)
                {
                    bindposes[b] = Matrix4x4.Translate(-ToVector(AvatarSkeleton.Rest[b]));
                }

                mesh.bindposes = bindposes;
                foreach (KeyValuePair<string, V3[]> shape in data.BlendShapes)
                {
                    var deltas = new Vector3[shape.Value.Length];
                    for (int i = 0; i < deltas.Length; i++)
                    {
                        deltas[i] = ToVector(shape.Value[i]);
                    }

                    mesh.AddBlendShapeFrame(shape.Key, 100f, deltas, null, null);
                }
            }

            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material NewMaterial(string name, Color color, float smoothness)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            return m;
        }

        private static Vector3 ToVector(V3 v) => new Vector3(v.X, v.Y, v.Z);
    }
}

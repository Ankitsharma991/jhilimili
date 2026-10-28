using System.Collections.Generic;
using UnityEngine;
using UtsavNights.Core.Art;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Home;

namespace UtsavNights.Gameplay
{
    /// <summary>Turns an engine-free <see cref="HouseMesh"/> into a Unity mesh with one submesh per paint it uses.</summary>
    public static class HouseMeshes
    {
        public static Mesh ToMesh(HouseMesh data, string name, out HousePaint[] paints)
        {
            var mesh = new Mesh { name = name };
            if (data.VertexCount > 65535)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            var vertices = new Vector3[data.VertexCount];
            var normals = new Vector3[data.VertexCount];
            var uvs = new Vector2[data.VertexCount];
            for (int i = 0; i < data.VertexCount; i++)
            {
                vertices[i] = ToVector(data.Positions[i]);
                normals[i] = ToVector(data.Normals[i]);
                uvs[i] = new Vector2(data.U[i], data.V[i]);
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            var used = new List<HousePaint>();
            var submeshes = new List<List<int>>();
            for (int p = 0; p < HouseMesh.PaintCount; p++)
            {
                if (data.Triangles[p].Count > 0)
                {
                    used.Add((HousePaint)p);
                    submeshes.Add(data.Triangles[p]);
                }
            }

            mesh.subMeshCount = submeshes.Count;
            for (int s = 0; s < submeshes.Count; s++)
            {
                mesh.SetTriangles(submeshes[s], s);
            }

            mesh.RecalculateBounds();
            paints = used.ToArray();
            return mesh;
        }

        public static Vector3 ToVector(V3 v) => new Vector3(v.X, v.Y, v.Z);

        public static void Destroy(Object o)
        {
            if (o == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(o);
            }
            else
            {
                Object.DestroyImmediate(o);
            }
        }
    }

    /// <summary>
    /// URP Lit materials shared per paint, colour and texture, so a whole house and its decorations are a handful of SRP
    /// batches. Glowing paints (<see cref="HousePalette.IsEmissive"/>) get emission.
    /// </summary>
    public sealed class HomeMaterials
    {
        private readonly Dictionary<string, Material> _cache = new Dictionary<string, Material>();

        public int Count => _cache.Count;

        public Material Get(HousePaint paint, Rgb colour, Texture2D texture = null, float metresPerRepeat = 1f)
        {
            string key = $"{paint}:{colour.R:0.###},{colour.G:0.###},{colour.B:0.###}:{(texture != null ? texture.GetInstanceID() : 0)}";
            if (_cache.TryGetValue(key, out Material cached))
            {
                return cached;
            }

            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Home_" + key };
            var c = new Color(colour.R, colour.G, colour.B);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", Smoothness(paint));
            if (texture != null)
            {
                float repeat = 1f / metresPerRepeat;
                m.SetTexture("_BaseMap", texture);
                m.SetTextureScale("_BaseMap", new Vector2(repeat, repeat));
            }

            if (HousePalette.IsEmissive(paint))
            {
                // Lit rooms behind glass, flames, paper lanterns and bulbs read as light at night.
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * Glow(paint));
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }

            _cache[key] = m;
            return m;
        }

        public void Clear()
        {
            foreach (Material m in _cache.Values)
            {
                HouseMeshes.Destroy(m);
            }

            _cache.Clear();
        }

        private static float Glow(HousePaint paint) => paint switch
        {
            HousePaint.Glass => 0.85f,
            HousePaint.Paper => 1.1f,
            HousePaint.Flame => 1.3f,
            _ => 2f,
        };

        private static float Smoothness(HousePaint paint) => paint switch
        {
            HousePaint.Glass => 0.8f,
            HousePaint.Iron => 0.45f,
            HousePaint.Brass => 0.65f,
            HousePaint.Accent or HousePaint.Trim => 0.3f,
            HousePaint.Leaf or HousePaint.Marigold or HousePaint.MarigoldAlt => 0.2f,
            _ => 0.12f,
        };
    }
}

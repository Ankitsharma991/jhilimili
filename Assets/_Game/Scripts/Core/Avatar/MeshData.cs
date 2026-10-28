using System;
using System.Collections.Generic;

namespace UtsavNights.Core.Avatar
{
    /// <summary>A small engine-free 3D vector for mesh generation (metres; y up, z forward, x to the avatar's left).</summary>
    public readonly struct V3
    {
        public V3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float X { get; }

        public float Y { get; }

        public float Z { get; }

        public static V3 Zero => new V3(0f, 0f, 0f);

        public static V3 Up => new V3(0f, 1f, 0f);

        public float Length => (float)Math.Sqrt(X * X + Y * Y + Z * Z);

        public V3 Normalized
        {
            get
            {
                float l = Length;
                return l > 1e-6f ? new V3(X / l, Y / l, Z / l) : new V3(0f, 1f, 0f);
            }
        }

        public static V3 operator +(V3 a, V3 b) => new V3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        public static V3 operator -(V3 a, V3 b) => new V3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        public static V3 operator *(V3 a, float k) => new V3(a.X * k, a.Y * k, a.Z * k);

        public static V3 Lerp(V3 a, V3 b, float t) => a + (b - a) * t;

        public static V3 Cross(V3 a, V3 b) => new V3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        public static float Dot(V3 a, V3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public V3 MirrorX() => new V3(-X, Y, Z);

        public override string ToString() => $"({X:0.###}, {Y:0.###}, {Z:0.###})";
    }

    /// <summary>
    /// Engine-free mesh: positions, normals, uvs, up to two bone weights per vertex, triangles grouped by submesh and
    /// by <see cref="BodyRegion"/> (for skin masks), and named blend shapes as position deltas.
    /// </summary>
    public sealed class MeshData
    {
        public readonly List<V3> Positions = new List<V3>();
        public readonly List<V3> Normals = new List<V3>();
        public readonly List<float> U = new List<float>();
        public readonly List<float> V = new List<float>();
        public readonly List<int> BoneA = new List<int>();
        public readonly List<int> BoneB = new List<int>();

        /// <summary>Weight of <see cref="BoneB"/>; bone A has 1 minus this.</summary>
        public readonly List<float> WeightB = new List<float>();

        /// <summary>Triangle index triples per submesh (material).</summary>
        public readonly List<List<int>> Submeshes = new List<List<int>>();

        /// <summary>Region of each triangle, in the same order as the triangles of each submesh.</summary>
        public readonly List<List<BodyRegion>> TriangleRegions = new List<List<BodyRegion>>();

        public readonly List<KeyValuePair<string, V3[]>> BlendShapes = new List<KeyValuePair<string, V3[]>>();

        /// <summary>
        /// Every lofted cross-section: first vertex, vertex count and a tag ("leg", "foot", "hem", "ribbon", ...). The clipping
        /// check (Phase 6 step 7) uses them to find hems, legs and drapes after posing.
        /// </summary>
        public readonly List<Loop> Loops = new List<Loop>();

        public readonly struct Loop
        {
            public Loop(int start, int count, string tag)
            {
                Start = start;
                Count = count;
                Tag = tag;
            }

            public int Start { get; }

            public int Count { get; }

            public string Tag { get; }
        }

        public int VertexCount => Positions.Count;

        public int AddVertex(V3 position, V3 normal, float u, float v, int boneA, int boneB = -1, float weightB = 0f)
        {
            Positions.Add(position);
            Normals.Add(normal.Normalized);
            U.Add(u);
            V.Add(v);
            BoneA.Add(boneA);
            BoneB.Add(boneB < 0 ? boneA : boneB);
            WeightB.Add(boneB < 0 ? 0f : Math.Max(0f, Math.Min(1f, weightB)));
            return Positions.Count - 1;
        }

        public void AddTriangle(int submesh, int a, int b, int c, BodyRegion region)
        {
            while (Submeshes.Count <= submesh)
            {
                Submeshes.Add(new List<int>());
                TriangleRegions.Add(new List<BodyRegion>());
            }

            Submeshes[submesh].Add(a);
            Submeshes[submesh].Add(b);
            Submeshes[submesh].Add(c);
            TriangleRegions[submesh].Add(region);
        }

        public int TriangleCount
        {
            get
            {
                int n = 0;
                foreach (List<int> s in Submeshes)
                {
                    n += s.Count / 3;
                }

                return n;
            }
        }

        /// <summary>Triangles of a submesh without the regions in <paramref name="hidden"/> (the skin mask).</summary>
        public List<int> TrianglesExcept(int submesh, BodyRegion hidden)
        {
            var result = new List<int>();
            if (submesh >= Submeshes.Count)
            {
                return result;
            }

            List<int> tris = Submeshes[submesh];
            List<BodyRegion> regions = TriangleRegions[submesh];
            for (int t = 0; t < regions.Count; t++)
            {
                if ((regions[t] & hidden) == 0)
                {
                    result.Add(tris[t * 3]);
                    result.Add(tris[t * 3 + 1]);
                    result.Add(tris[t * 3 + 2]);
                }
            }

            return result;
        }

        /// <summary>Adds a blend shape as the difference between another generation of the same topology and this one.</summary>
        public void AddBlendShapeFrom(string name, MeshData target)
        {
            if (target.VertexCount != VertexCount)
            {
                throw new ArgumentException($"blend shape {name}: {target.VertexCount} vertices, base has {VertexCount}");
            }

            var deltas = new V3[VertexCount];
            for (int i = 0; i < VertexCount; i++)
            {
                deltas[i] = target.Positions[i] - Positions[i];
            }

            BlendShapes.Add(new KeyValuePair<string, V3[]>(name, deltas));
        }
    }
}

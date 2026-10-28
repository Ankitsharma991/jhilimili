using System;
using System.Collections.Generic;

namespace UtsavNights.Core.Avatar
{
    /// <summary>A 3×3 rotation, row-major, for posing without the engine.</summary>
    public readonly struct Mat3
    {
        private readonly float _m00, _m01, _m02, _m10, _m11, _m12, _m20, _m21, _m22;

        public Mat3(float m00, float m01, float m02, float m10, float m11, float m12, float m20, float m21, float m22)
        {
            _m00 = m00; _m01 = m01; _m02 = m02;
            _m10 = m10; _m11 = m11; _m12 = m12;
            _m20 = m20; _m21 = m21; _m22 = m22;
        }

        public static Mat3 Identity => new Mat3(1, 0, 0, 0, 1, 0, 0, 0, 1);

        /// <summary>Unity's Quaternion.Euler(x, y, z): rotate about Z, then X, then Y (degrees).</summary>
        public static Mat3 Euler(float x, float y, float z)
        {
            double rx = x * Math.PI / 180, ry = y * Math.PI / 180, rz = z * Math.PI / 180;
            float cx = (float)Math.Cos(rx), sx = (float)Math.Sin(rx);
            float cy = (float)Math.Cos(ry), sy = (float)Math.Sin(ry);
            float cz = (float)Math.Cos(rz), sz = (float)Math.Sin(rz);
            var rotX = new Mat3(1, 0, 0, 0, cx, -sx, 0, sx, cx);
            var rotY = new Mat3(cy, 0, sy, 0, 1, 0, -sy, 0, cy);
            var rotZ = new Mat3(cz, -sz, 0, sz, cz, 0, 0, 0, 1);
            return rotY * (rotX * rotZ);
        }

        public static Mat3 operator *(Mat3 a, Mat3 b) => new Mat3(
            a._m00 * b._m00 + a._m01 * b._m10 + a._m02 * b._m20, a._m00 * b._m01 + a._m01 * b._m11 + a._m02 * b._m21, a._m00 * b._m02 + a._m01 * b._m12 + a._m02 * b._m22,
            a._m10 * b._m00 + a._m11 * b._m10 + a._m12 * b._m20, a._m10 * b._m01 + a._m11 * b._m11 + a._m12 * b._m21, a._m10 * b._m02 + a._m11 * b._m12 + a._m12 * b._m22,
            a._m20 * b._m00 + a._m21 * b._m10 + a._m22 * b._m20, a._m20 * b._m01 + a._m21 * b._m11 + a._m22 * b._m21, a._m20 * b._m02 + a._m21 * b._m12 + a._m22 * b._m22);

        public static V3 operator *(Mat3 m, V3 v) => new V3(
            m._m00 * v.X + m._m01 * v.Y + m._m02 * v.Z,
            m._m10 * v.X + m._m11 * v.Y + m._m12 * v.Z,
            m._m20 * v.X + m._m21 * v.Y + m._m22 * v.Z);
    }

    /// <summary>
    /// Poses the procedural avatar without Unity (Phase 6 step 7): bone world transforms from an <see cref="AvatarPose"/>, then
    /// two-bone linear skinning with the body-style blend shapes applied first, exactly as the SkinnedMeshRenderer does it.
    /// The clipping check uses it to measure every garment in every pose.
    /// </summary>
    public sealed class AvatarPoser
    {
        private readonly Mat3[] _rotation = new Mat3[AvatarSkeleton.Count];
        private readonly V3[] _position = new V3[AvatarSkeleton.Count];

        public void SetPose(AvatarPose pose)
        {
            for (int i = 0; i < AvatarSkeleton.Count; i++)
            {
                Mat3 local = Mat3.Euler(pose.X[i], pose.Y[i], pose.Z[i]);
                int parent = AvatarSkeleton.Parents[i];
                if (parent < 0)
                {
                    _rotation[i] = local;
                    _position[i] = AvatarSkeleton.Rest[i] + pose.HipsOffset;
                }
                else
                {
                    _rotation[i] = _rotation[parent] * local;
                    _position[i] = _position[parent] + _rotation[parent] * (AvatarSkeleton.Rest[i] - AvatarSkeleton.Rest[parent]);
                }
            }
        }

        public V3 BonePosition(int bone) => _position[bone];

        /// <summary>Posed positions of a mesh, with blend shape weights (0 to 100) by name.</summary>
        public V3[] Skin(MeshData mesh, IEnumerable<KeyValuePair<string, float>> blendWeights)
        {
            var rest = new V3[mesh.VertexCount];
            for (int i = 0; i < rest.Length; i++)
            {
                rest[i] = mesh.Positions[i];
            }

            foreach (KeyValuePair<string, float> w in blendWeights)
            {
                if (w.Value <= 0f)
                {
                    continue;
                }

                foreach (KeyValuePair<string, V3[]> shape in mesh.BlendShapes)
                {
                    if (shape.Key == w.Key)
                    {
                        for (int i = 0; i < rest.Length; i++)
                        {
                            rest[i] = rest[i] + shape.Value[i] * (w.Value / 100f);
                        }
                    }
                }
            }

            var posed = new V3[rest.Length];
            for (int i = 0; i < rest.Length; i++)
            {
                int a = mesh.BoneA[i], b = mesh.BoneB[i];
                float wb = mesh.WeightB[i];
                V3 pa = _position[a] + _rotation[a] * (rest[i] - AvatarSkeleton.Rest[a]);
                if (wb <= 0f || a == b)
                {
                    posed[i] = pa;
                    continue;
                }

                V3 pb = _position[b] + _rotation[b] * (rest[i] - AvatarSkeleton.Rest[b]);
                posed[i] = pa * (1f - wb) + pb * wb;
            }

            return posed;
        }
    }
}

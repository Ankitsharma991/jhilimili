using System;
using System.Collections.Generic;

namespace UtsavNights.Core.Avatar
{
    /// <summary>
    /// The shared skeleton (PRD 8: one skeleton for every body and garment), with Unity humanoid bone names so a
    /// commissioned rig or Mecanim clips can replace the procedural ones later. Rest pose: standing, arms relaxed
    /// down and slightly out. Medium height, avatar space: metres, y up, facing +z, the avatar's left at -x.
    /// </summary>
    public static class AvatarSkeleton
    {
        public const int Hips = 0, Spine = 1, Chest = 2, Neck = 3, Head = 4;
        public const int LeftShoulder = 5, LeftUpperArm = 6, LeftLowerArm = 7, LeftHand = 8;
        public const int RightShoulder = 9, RightUpperArm = 10, RightLowerArm = 11, RightHand = 12;
        public const int LeftUpperLeg = 13, LeftLowerLeg = 14, LeftFoot = 15;
        public const int RightUpperLeg = 16, RightLowerLeg = 17, RightFoot = 18;
        public const int Count = 19;

        public static readonly string[] Names =
        {
            "Hips", "Spine", "Chest", "Neck", "Head",
            "LeftShoulder", "LeftUpperArm", "LeftLowerArm", "LeftHand",
            "RightShoulder", "RightUpperArm", "RightLowerArm", "RightHand",
            "LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
            "RightUpperLeg", "RightLowerLeg", "RightFoot",
        };

        public static readonly int[] Parents =
        {
            -1, Hips, Spine, Chest, Neck,
            Chest, LeftShoulder, LeftUpperArm, LeftLowerArm,
            Chest, RightShoulder, RightUpperArm, RightLowerArm,
            Hips, LeftUpperLeg, LeftLowerLeg,
            Hips, RightUpperLeg, RightLowerLeg,
        };

        /// <summary>Rest positions in avatar space.</summary>
        public static readonly V3[] Rest =
        {
            new V3(0f, 0.95f, 0f), new V3(0f, 1.06f, 0f), new V3(0f, 1.22f, 0f), new V3(0f, 1.45f, 0f), new V3(0f, 1.54f, 0f),
            new V3(-0.06f, 1.41f, 0f), new V3(-0.17f, 1.39f, 0f), new V3(-0.215f, 1.12f, 0.005f), new V3(-0.24f, 0.88f, 0.02f),
            new V3(0.06f, 1.41f, 0f), new V3(0.17f, 1.39f, 0f), new V3(0.215f, 1.12f, 0.005f), new V3(0.24f, 0.88f, 0.02f),
            new V3(-0.09f, 0.9f, 0f), new V3(-0.095f, 0.49f, 0.005f), new V3(-0.1f, 0.08f, -0.005f),
            new V3(0.09f, 0.9f, 0f), new V3(0.095f, 0.49f, 0.005f), new V3(0.1f, 0.08f, -0.005f),
        };

        /// <summary>Hip height above the floor: what the height presets and leg-length slider scale.</summary>
        public const float LegLength = 0.9f;

        /// <summary>Stylised proportions: the head is drawn a little larger than life so faces read on a phone.</summary>
        public const float HeadScale = 1.15f;

        public static float HeightScale(HeightPreset height) => height == HeightPreset.Short ? 0.92f : height == HeightPreset.Tall ? 1.08f : 1f;
    }

    /// <summary>Inputs that change the body's surface (body style and the width sliders become blend shapes).</summary>
    public struct BodyShape
    {
        /// <summary>0 slim, 1 average, 2 broad.</summary>
        public float Style;

        /// <summary>-1 narrow to 1 wide.</summary>
        public float Shoulders;

        public float Hips;

        public static BodyShape Average => new BodyShape { Style = 1f };
    }

    /// <summary>One cross-section of a lofted part: an ellipse around <see cref="Center"/>, perpendicular to <see cref="Axis"/>.</summary>
    public struct Ring
    {
        public V3 Center;
        public V3 Axis;
        public float Side;
        public float Depth;
        public int BoneA;
        public int BoneB;
        public float WeightB;
        public BodyRegion Region;

        /// <summary>Superellipse power: 2 is an ellipse, higher is boxier.</summary>
        public float Power;
    }

    /// <summary>
    /// The stylised body (Phase 6 step 2). Each part is lofted through rings: torso and neck, arms with mitten hands,
    /// legs and feet. The same generator at body style 0, 1 and 2 (and the width sliders at their limits) gives the same
    /// topology, so <see cref="Build"/> stores them as blend shapes on the average body. Hands are region None: sleeves
    /// end at the wrist, so the skin mask never hides them.
    /// </summary>
    public static class BodyGenerator
    {
        public const int SkinSubmesh = 0;
        public const string BlendSlim = "Slim", BlendBroad = "Broad";
        public const string BlendShouldersWide = "ShouldersWide", BlendShouldersNarrow = "ShouldersNarrow";
        public const string BlendHipsWide = "HipsWide", BlendHipsNarrow = "HipsNarrow";

        public static MeshData Build()
        {
            MeshData mesh = Generate(BodyShape.Average);
            mesh.AddBlendShapeFrom(BlendSlim, Generate(new BodyShape { Style = 0f }));
            mesh.AddBlendShapeFrom(BlendBroad, Generate(new BodyShape { Style = 2f }));
            mesh.AddBlendShapeFrom(BlendShouldersWide, Generate(new BodyShape { Style = 1f, Shoulders = 1f }));
            mesh.AddBlendShapeFrom(BlendShouldersNarrow, Generate(new BodyShape { Style = 1f, Shoulders = -1f }));
            mesh.AddBlendShapeFrom(BlendHipsWide, Generate(new BodyShape { Style = 1f, Hips = 1f }));
            mesh.AddBlendShapeFrom(BlendHipsNarrow, Generate(new BodyShape { Style = 1f, Hips = -1f }));
            return mesh;
        }

        /// <summary>Blend shape weights (0 to 100) for a profile, by blend shape name.</summary>
        public static IEnumerable<KeyValuePair<string, float>> Weights(AvatarProfile p)
        {
            yield return new KeyValuePair<string, float>(BlendSlim, Math.Max(0f, 1f - p.bodyStyle) * 100f);
            yield return new KeyValuePair<string, float>(BlendBroad, Math.Max(0f, p.bodyStyle - 1f) * 100f);
            yield return new KeyValuePair<string, float>(BlendShouldersWide, Math.Max(0f, p.shoulders) * 100f);
            yield return new KeyValuePair<string, float>(BlendShouldersNarrow, Math.Max(0f, -p.shoulders) * 100f);
            yield return new KeyValuePair<string, float>(BlendHipsWide, Math.Max(0f, p.hips) * 100f);
            yield return new KeyValuePair<string, float>(BlendHipsNarrow, Math.Max(0f, -p.hips) * 100f);
        }

        public static MeshData Generate(BodyShape shape)
        {
            var mesh = new MeshData();
            List<List<Ring>> parts = Parts(shape, 0f);
            for (int i = 0; i < parts.Count; i++)
            {
                Loft(mesh, SkinSubmesh, parts[i], Segments(parts[i]), capStart: true, capEnd: true, tag: PartTag(i));
            }

            return mesh;
        }

        /// <summary>
        /// The rings of every body part, pushed out by <paramref name="inflate"/> metres. Garments loft over the same rings
        /// with a positive inflate, which is why they follow the body exactly.
        /// </summary>
        public static List<List<Ring>> Parts(BodyShape shape, float inflate)
        {
            Factors(shape, out float w, out float d, out float belly, out float limb);
            float sh = 1f + 0.12f * shape.Shoulders;
            float hp = 1f + 0.12f * shape.Hips;

            var parts = new List<List<Ring>>();
            var up = V3.Up;
            const int H = AvatarSkeleton.Hips, S = AvatarSkeleton.Spine, C = AvatarSkeleton.Chest, N = AvatarSkeleton.Neck, Hd = AvatarSkeleton.Head;
            parts.Add(new List<Ring>
            {
                R(new V3(0f, 0.84f, 0f), up, 0.15f * w * hp, 0.10f * d, H, H, 0f, BodyRegion.Hips, inflate),
                R(new V3(0f, 0.93f, 0f), up, 0.17f * w * hp, 0.11f * d, H, H, 0f, BodyRegion.Hips, inflate),
                R(new V3(0f, 1.02f, 0.003f), up, 0.145f * w * (1f + 0.05f * shape.Hips), 0.10f * belly, H, S, 0.5f, BodyRegion.Belly, inflate),
                R(new V3(0f, 1.11f, 0.006f), up, 0.145f * w, 0.105f * belly, S, S, 0f, BodyRegion.Belly, inflate),
                R(new V3(0f, 1.2f, 0.004f), up, 0.155f * w, 0.11f * d, S, C, 0.6f, BodyRegion.Chest, inflate),
                R(new V3(0f, 1.3f, 0f), up, 0.165f * w * (1f + 0.06f * shape.Shoulders), 0.11f * d, C, C, 0f, BodyRegion.Chest, inflate),
                R(new V3(0f, 1.38f, -0.005f), up, 0.16f * w * sh, 0.095f * d, C, C, 0f, BodyRegion.Chest, inflate),
                R(new V3(0f, 1.43f, -0.01f), up, 0.115f * w * sh, 0.07f * d, C, N, 0.3f, BodyRegion.Chest, inflate),
                R(new V3(0f, 1.47f, -0.005f), up, 0.052f * limb, 0.052f * limb, N, N, 0f, BodyRegion.Neck, inflate),
                R(new V3(0f, 1.55f, 0f), up, 0.048f * limb, 0.05f * limb, N, Hd, 0.6f, BodyRegion.Neck, inflate),
            });

            foreach (int side in new[] { -1, 1 })
            {
                bool left = side < 0;
                int sh0 = left ? AvatarSkeleton.LeftShoulder : AvatarSkeleton.RightShoulder;
                int ua = left ? AvatarSkeleton.LeftUpperArm : AvatarSkeleton.RightUpperArm;
                int la = left ? AvatarSkeleton.LeftLowerArm : AvatarSkeleton.RightLowerArm;
                int hd = left ? AvatarSkeleton.LeftHand : AvatarSkeleton.RightHand;
                V3 shoulder = AvatarSkeleton.Rest[ua], elbow = AvatarSkeleton.Rest[la], wrist = AvatarSkeleton.Rest[hd];
                float spread = side * 0.012f * shape.Shoulders;
                shoulder += new V3(spread, 0f, 0f);
                V3 upperAxis = (elbow - shoulder).Normalized, lowerAxis = (wrist - elbow).Normalized;
                V3 tip = wrist + lowerAxis * 0.17f;
                parts.Add(new List<Ring>
                {
                    R(shoulder + new V3(-side * 0.02f, 0.01f, 0f), upperAxis, 0.056f * limb, 0.058f * limb, sh0, ua, 0.7f, BodyRegion.UpperArms, inflate),
                    R(V3.Lerp(shoulder, elbow, 0.35f), upperAxis, 0.05f * limb, 0.052f * limb, ua, ua, 0f, BodyRegion.UpperArms, inflate),
                    R(V3.Lerp(shoulder, elbow, 0.75f), upperAxis, 0.043f * limb, 0.045f * limb, ua, ua, 0f, BodyRegion.UpperArms, inflate),
                    R(elbow, lowerAxis, 0.04f * limb, 0.041f * limb, ua, la, 0.5f, BodyRegion.LowerArms, inflate),
                    R(V3.Lerp(elbow, wrist, 0.4f), lowerAxis, 0.04f * limb, 0.038f * limb, la, la, 0f, BodyRegion.LowerArms, inflate),
                    R(wrist - lowerAxis * 0.015f, lowerAxis, 0.029f * limb, 0.026f * limb, la, hd, 0.4f, BodyRegion.LowerArms, inflate),
                });

                // Mitten hand, flattened front to back, palm facing the thigh.
                parts.Add(new List<Ring>
                {
                    R(wrist, lowerAxis, 0.03f, 0.018f, hd, hd, 0f, BodyRegion.None, inflate),
                    R(wrist + lowerAxis * 0.06f, lowerAxis, 0.045f, 0.022f, hd, hd, 0f, BodyRegion.None, inflate),
                    R(wrist + lowerAxis * 0.13f, lowerAxis, 0.04f, 0.018f, hd, hd, 0f, BodyRegion.None, inflate),
                    R(tip, lowerAxis, 0.02f, 0.01f, hd, hd, 0f, BodyRegion.None, inflate),
                });

                int ul = left ? AvatarSkeleton.LeftUpperLeg : AvatarSkeleton.RightUpperLeg;
                int ll = left ? AvatarSkeleton.LeftLowerLeg : AvatarSkeleton.RightLowerLeg;
                int ft = left ? AvatarSkeleton.LeftFoot : AvatarSkeleton.RightFoot;
                V3 hipJoint = AvatarSkeleton.Rest[ul] + new V3(side * 0.008f * shape.Hips, 0f, 0f);
                V3 knee = AvatarSkeleton.Rest[ll], ankle = AvatarSkeleton.Rest[ft];
                V3 thigh = (knee - hipJoint).Normalized, shin = (ankle - knee).Normalized;
                float leg = limb * (shape.Style > 1f ? 1f + 0.08f * (shape.Style - 1f) : 1f);
                parts.Add(new List<Ring>
                {
                    R(hipJoint, thigh, 0.088f * leg * hp, 0.092f * leg, H, ul, 0.7f, BodyRegion.UpperLegs, inflate),
                    R(V3.Lerp(hipJoint, knee, 0.3f), thigh, 0.08f * leg, 0.085f * leg, ul, ul, 0f, BodyRegion.UpperLegs, inflate),
                    R(V3.Lerp(hipJoint, knee, 0.7f), thigh, 0.064f * leg, 0.068f * leg, ul, ul, 0f, BodyRegion.UpperLegs, inflate),
                    R(knee, shin, 0.054f * leg, 0.056f * leg, ul, ll, 0.5f, BodyRegion.LowerLegs, inflate),
                    R(V3.Lerp(knee, ankle, 0.3f), shin, 0.053f * leg, 0.062f * leg, ll, ll, 0f, BodyRegion.LowerLegs, inflate),
                    R(V3.Lerp(knee, ankle, 0.75f), shin, 0.04f * leg, 0.042f * leg, ll, ll, 0f, BodyRegion.LowerLegs, inflate),
                    R(ankle + new V3(0f, 0.02f, 0f), shin, 0.034f, 0.034f, ll, ft, 0.5f, BodyRegion.LowerLegs, inflate),
                });

                // Foot: lofted forward from heel to toe; the "side" of each ring is its width, "depth" its height.
                var forward = new V3(0f, -0.15f, 1f).Normalized;
                float fx = ankle.X;
                parts.Add(new List<Ring>
                {
                    R(new V3(fx, 0.05f, -0.045f), forward, 0.034f, 0.034f, ft, ft, 0f, BodyRegion.Feet, inflate),
                    R(new V3(fx, 0.045f, 0.02f), forward, 0.043f, 0.038f, ft, ft, 0f, BodyRegion.Feet, inflate),
                    R(new V3(fx + side * 0.004f, 0.03f, 0.11f), forward, 0.046f, 0.026f, ft, ft, 0f, BodyRegion.Feet, inflate),
                    R(new V3(fx + side * 0.006f, 0.022f, 0.165f), forward, 0.036f, 0.016f, ft, ft, 0f, BodyRegion.Feet, inflate),
                });
            }

            return parts;
        }

        /// <summary>
        /// Size multipliers for a body style: torso width and depth, belly depth and limb thickness. Garments use the same
        /// numbers, so a skirt or hem made for a broad body is wide enough for broad legs.
        /// </summary>
        public static void Factors(BodyShape shape, out float width, out float depth, out float belly, out float limb)
        {
            width = shape.Style < 1f ? Lerp(0.86f, 1f, shape.Style) : Lerp(1f, 1.32f, shape.Style - 1f);
            depth = shape.Style < 1f ? Lerp(0.85f, 1f, shape.Style) : Lerp(1f, 1.42f, shape.Style - 1f);
            belly = shape.Style < 1f ? depth : Lerp(1f, 1.62f, shape.Style - 1f);
            limb = shape.Style < 1f ? Lerp(0.85f, 1f, shape.Style) : Lerp(1f, 1.3f, shape.Style - 1f);
        }

        /// <summary>Loop tag for a body part: torso, arm, hand, leg or foot.</summary>
        public static string PartTag(int part) => part == PartTorso ? "torso"
            : part == PartLeftArm || part == PartRightArm ? "arm"
            : part == PartLeftHand || part == PartRightHand ? "hand"
            : part == PartLeftLeg || part == PartRightLeg ? "leg"
            : "foot";

        /// <summary>Indices of the parts returned by <see cref="Parts"/>.</summary>
        public const int PartTorso = 0, PartLeftArm = 1, PartLeftHand = 2, PartLeftLeg = 3, PartLeftFoot = 4;
        public const int PartRightArm = 5, PartRightHand = 6, PartRightLeg = 7, PartRightFoot = 8;

        /// <summary>Indices of the torso rings: crotch, hips, waist, belly, lower chest, chest, upper chest, shoulder top, neck, neck top.</summary>
        public const int TorsoCrotch = 0, TorsoHips = 1, TorsoWaist = 2, TorsoBelly = 3, TorsoLowerChest = 4, TorsoChest = 5;
        public const int TorsoUpperChest = 6, TorsoShoulderTop = 7, TorsoNeck = 8, TorsoNeckTop = 9;

        /// <summary>Rings around a part, with caps; ring vertices repeat the first at the seam so uvs wrap cleanly.</summary>
        public static void Loft(MeshData mesh, int submesh, IReadOnlyList<Ring> rings, int segments, bool capStart, bool capEnd, string tag = null)
        {
            int start = mesh.VertexCount;
            for (int r = 0; r < rings.Count; r++)
            {
                if (tag != null)
                {
                    mesh.Loops.Add(new MeshData.Loop(start + r * (segments + 1), segments + 1, tag));
                }

                Ring ring = rings[r];
                Frame(ring.Axis, out V3 side, out V3 front);
                for (int j = 0; j <= segments; j++)
                {
                    double a = 2 * Math.PI * j / segments;
                    float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
                    float p = ring.Power <= 0f ? 2f : ring.Power;
                    float cx = Math.Sign(c) * (float)Math.Pow(Math.Abs(c), 2f / p);
                    float sz = Math.Sign(s) * (float)Math.Pow(Math.Abs(s), 2f / p);
                    V3 offset = side * (cx * ring.Side) + front * (sz * ring.Depth);
                    V3 normal = side * (c / Math.Max(1e-4f, ring.Side)) + front * (s / Math.Max(1e-4f, ring.Depth));
                    mesh.AddVertex(ring.Center + offset, normal, (float)j / segments, (float)r / Math.Max(1, rings.Count - 1), ring.BoneA, ring.BoneB, ring.WeightB);
                }
            }

            int stride = segments + 1;
            for (int r = 0; r < rings.Count - 1; r++)
            {
                BodyRegion region = rings[r + 1].Region;
                for (int j = 0; j < segments; j++)
                {
                    int a = start + r * stride + j, b = a + 1, c = a + stride, e = c + 1;
                    mesh.AddTriangle(submesh, a, c, b, region);
                    mesh.AddTriangle(submesh, b, c, e, region);
                }
            }

            if (capStart)
            {
                Cap(mesh, submesh, rings[0], start, segments, flip: true);
            }

            if (capEnd)
            {
                Cap(mesh, submesh, rings[rings.Count - 1], start + (rings.Count - 1) * stride, segments, flip: false);
            }
        }

        /// <summary>Side and front directions perpendicular to an axis (front is +z for upright parts, up for the feet).</summary>
        public static void Frame(V3 axis, out V3 side, out V3 front)
        {
            V3 a = axis.Normalized;
            V3 reference = Math.Abs(V3.Dot(a, new V3(0f, 0f, 1f))) > 0.85f ? V3.Up : new V3(0f, 0f, 1f);
            side = V3.Cross(a, reference).Normalized;
            front = V3.Cross(side, a).Normalized;
            if (V3.Dot(side, new V3(1f, 0f, 0f)) < 0f && Math.Abs(side.X) > 0.5f)
            {
                side = side * -1f;
                front = front * -1f;
            }
        }

        private static void Cap(MeshData mesh, int submesh, Ring ring, int ringStart, int segments, bool flip)
        {
            V3 normal = flip ? ring.Axis * -1f : ring.Axis;
            int center = mesh.AddVertex(ring.Center + normal * (Math.Min(ring.Side, ring.Depth) * 0.35f), normal, 0.5f, flip ? 0f : 1f, ring.BoneA, ring.BoneB, ring.WeightB);
            for (int j = 0; j < segments; j++)
            {
                if (flip)
                {
                    mesh.AddTriangle(submesh, center, ringStart + j, ringStart + j + 1, ring.Region);
                }
                else
                {
                    mesh.AddTriangle(submesh, center, ringStart + j + 1, ringStart + j, ring.Region);
                }
            }
        }

        private static int Segments(List<Ring> part) => part[0].Region == BodyRegion.Hips ? 16 : part[0].Region == BodyRegion.None ? 8 : 10;

        private static Ring R(V3 center, V3 axis, float side, float depth, int boneA, int boneB, float weightB, BodyRegion region, float inflate) =>
            new Ring
            {
                Center = center, Axis = axis, Side = side + inflate, Depth = depth + inflate, BoneA = boneA, BoneB = boneB, WeightB = weightB, Region = region,
            };

        private static float Lerp(float a, float b, float t) => a + (b - a) * Math.Max(0f, Math.Min(1f, t));
    }
}

using System;
using System.Collections.Generic;

namespace UtsavNights.Core.Avatar
{
    /// <summary>A generated garment: a part skinned to the body's bones, and/or a part fixed to the head (headwear, earrings).</summary>
    public sealed class GarmentMesh
    {
        public MeshData Skinned;
        public MeshData HeadRigid;
    }

    /// <summary>
    /// Builds every catalogue garment from the body's own rings (Phase 6 step 3). Garments loft over the same cross-sections
    /// pushed out by a layer gap (base, mid, outer), and are generated at every body style, so they carry the body's blend
    /// shapes as fit morphs and never clip at rest. Skirts, robes and long hems are flared tubes sized from the same body
    /// factors; drapes (sari pallu, dupatta, shawl ends) are thin ribbons lofted along a path. Two submeshes: <see cref="Main"/>
    /// in the variant colour and <see cref="Trim"/> (borders, buttons, embroidery, soles) in its trim colour.
    /// </summary>
    public static class GarmentGenerator
    {
        public const int Main = 0, Trim = 1;
        public const float BaseGap = 0.008f, MidGap = 0.016f, OuterGap = 0.026f;

        private const int H = AvatarSkeleton.Hips, S = AvatarSkeleton.Spine, C = AvatarSkeleton.Chest;

        public static GarmentMesh Build(string id)
        {
            GarmentMesh garment = Generate(id, BodyShape.Average);
            if (garment.Skinned != null)
            {
                garment.Skinned.AddBlendShapeFrom(BodyGenerator.BlendSlim, Generate(id, new BodyShape { Style = 0f }).Skinned);
                garment.Skinned.AddBlendShapeFrom(BodyGenerator.BlendBroad, Generate(id, new BodyShape { Style = 2f }).Skinned);
                garment.Skinned.AddBlendShapeFrom(BodyGenerator.BlendShouldersWide, Generate(id, new BodyShape { Style = 1f, Shoulders = 1f }).Skinned);
                garment.Skinned.AddBlendShapeFrom(BodyGenerator.BlendShouldersNarrow, Generate(id, new BodyShape { Style = 1f, Shoulders = -1f }).Skinned);
                garment.Skinned.AddBlendShapeFrom(BodyGenerator.BlendHipsWide, Generate(id, new BodyShape { Style = 1f, Hips = 1f }).Skinned);
                garment.Skinned.AddBlendShapeFrom(BodyGenerator.BlendHipsNarrow, Generate(id, new BodyShape { Style = 1f, Hips = -1f }).Skinned);
            }

            return garment;
        }

        public static GarmentMesh Generate(string id, BodyShape s)
        {
            var g = new GarmentMesh();
            var m = new MeshData();
            var head = new MeshData();
            switch (id)
            {
                case "tshirt":
                    Top(m, s, BaseGap, BodyGenerator.TorsoHips, Sleeve.Short, hemY: 0f);
                    break;
                case "shirt":
                    Top(m, s, BaseGap, BodyGenerator.TorsoHips, Sleeve.Long, hemY: 0f);
                    Buttons(m, s, 1.08f, 1.4f, BaseGap, 5);
                    break;
                case "kurta":
                    Top(m, s, BaseGap, BodyGenerator.TorsoHips, Sleeve.Long, hemY: 0.55f, trimHem: true);
                    Neckline(m, s, BaseGap);
                    break;
                case "short_kurta":
                    Top(m, s, BaseGap, BodyGenerator.TorsoHips, Sleeve.ThreeQuarter, hemY: 0.72f, trimHem: true);
                    Neckline(m, s, BaseGap);
                    break;
                case "choli":
                    Top(m, s, BaseGap, BodyGenerator.TorsoLowerChest, Sleeve.Short, hemY: 0f, trimHem: true);
                    break;
                case "daura":
                    Top(m, s, BaseGap, BodyGenerator.TorsoHips, Sleeve.Long, hemY: 0.52f, trimHem: true);
                    Ribbon(m, Trim, new[] { new V3(0.13f, 1.0f, 0.15f), new V3(0.02f, 1.25f, 0.17f), new V3(-0.08f, 1.4f, 0.12f) }, 0.012f, 0.004f, new[] { H, C, C }, s, BaseGap + 0.003f);
                    break;
                case "jeans":
                    Pants(m, s, BaseGap, 6, t => 1.05f);
                    break;
                case "trousers":
                    Pants(m, s, BaseGap, 6, t => 1.12f);
                    break;
                case "churidar":
                    Pants(m, s, BaseGap, 6, t => t > 0.75f ? 1f + 0.12f * (float)Math.Abs(Math.Sin(t * 60f)) : 1f);
                    break;
                case "salwar":
                    Pants(m, s, BaseGap, 6, t => 1f + 0.8f * (float)Math.Sin(Math.PI * Math.Min(1f, t * 1.15f)));
                    break;
                case "suruwal":
                    Pants(m, s, BaseGap, 6, t => t < 0.45f ? 1.55f - 0.55f * t / 0.45f : 1f);
                    break;
                case "dhoti":
                    Pants(m, s, BaseGap, 5, t => 1.75f - 0.3f * t);
                    Ribbon(m, Trim, new[] { new V3(0f, 0.98f, 0.13f), new V3(0f, 0.7f, 0.17f), new V3(0f, 0.42f, 0.16f) }, 0.05f, 0.005f, new[] { H, H, H }, s, BaseGap);
                    break;
                case "lehenga":
                    Skirt(m, s, BaseGap, BodyGenerator.TorsoWaist, 0.04f, 1.95f, trimHem: true);
                    break;
                case "long_skirt":
                    Skirt(m, s, BaseGap, BodyGenerator.TorsoWaist, 0.08f, 1.35f, trimHem: false);
                    break;
                case "shorts":
                    Pants(m, s, BaseGap, 2, t => 1.15f);
                    break;
                case "sari_nivi":
                    Skirt(m, s, BaseGap, BodyGenerator.TorsoWaist, 0.03f, 1.25f, trimHem: true);
                    Ribbon(m, Trim, new[] { new V3(0.02f, 0.98f, 0.14f), new V3(0.02f, 0.5f, 0.17f), new V3(0.02f, 0.05f, 0.2f) }, 0.035f, 0.006f, new[] { H, H, H }, s, BaseGap + 0.004f);
                    Ribbon(m, Main, new[]
                    {
                        new V3(0.13f, 1.0f, 0.15f), new V3(0.02f, 1.24f, 0.17f), new V3(-0.11f, 1.42f, 0.08f), new V3(-0.13f, 1.46f, -0.04f),
                        new V3(-0.12f, 1.35f, -0.13f), new V3(-0.11f, 0.9f, -0.16f),
                    }, 0.11f, 0.004f, new[] { H, S, C, C, C, S }, s, MidGap);
                    break;
                case "sari_seedha":
                    Skirt(m, s, BaseGap, BodyGenerator.TorsoWaist, 0.03f, 1.25f, trimHem: true);
                    Ribbon(m, Main, new[]
                    {
                        new V3(-0.15f, 1.0f, -0.12f), new V3(-0.02f, 1.3f, -0.14f), new V3(0.12f, 1.44f, -0.05f), new V3(0.13f, 1.45f, 0.06f),
                        new V3(0.08f, 1.3f, 0.16f), new V3(0.04f, 0.95f, 0.17f),
                    }, 0.11f, 0.004f, new[] { H, S, C, C, C, S }, s, MidGap);
                    break;
                case "abaya":
                    Top(m, s, BaseGap, BodyGenerator.TorsoHips, Sleeve.Long, hemY: 0.03f, flare: 1.25f);
                    break;
                case "thobe":
                    Top(m, s, BaseGap, BodyGenerator.TorsoHips, Sleeve.Long, hemY: 0.05f, flare: 1.12f);
                    Neckline(m, s, BaseGap);
                    break;
                case "nehru_jacket":
                    Top(m, s, MidGap, BodyGenerator.TorsoHips, Sleeve.None, hemY: 0.84f);
                    Buttons(m, s, 0.9f, 1.38f, MidGap, 6);
                    break;
                case "hoodie":
                    Top(m, s, MidGap, BodyGenerator.TorsoHips, Sleeve.Long, hemY: 0f);
                    HeadGenerator.Blob(m, Main, new V3(0f, 1.47f, -0.1f), new V3(0.12f, 0.07f, 0.06f), 5, 10, -1, C);
                    break;
                case "dupatta":
                    Ribbon(m, Main, new[]
                    {
                        new V3(-0.12f, 0.95f, 0.15f), new V3(-0.11f, 1.3f, 0.13f), new V3(-0.1f, 1.45f, 0.02f), new V3(0f, 1.47f, -0.09f),
                        new V3(0.1f, 1.45f, 0.02f), new V3(0.11f, 1.3f, 0.13f), new V3(0.12f, 0.95f, 0.15f),
                    }, 0.075f, 0.004f, new[] { S, C, C, C, C, C, S }, s, MidGap + 0.004f);
                    break;
                case "blazer":
                    Top(m, s, OuterGap, BodyGenerator.TorsoHips, Sleeve.Long, hemY: 0.8f);
                    Buttons(m, s, 0.95f, 1.15f, OuterGap, 2);
                    break;
                case "sherwani":
                    Top(m, s, OuterGap, BodyGenerator.TorsoHips, Sleeve.Long, hemY: 0.5f, trimHem: true);
                    Buttons(m, s, 0.95f, 1.42f, OuterGap, 7);
                    Neckline(m, s, OuterGap);
                    break;
                case "shawl":
                    Shawl(m, s);
                    break;
                case "dhaka_topi":
                    Topi(head);
                    break;
                case "turban":
                    Turban(head);
                    break;
                case "hijab":
                    Headscarf(head, falls: false);
                    break;
                case "ghutra":
                    Headscarf(head, falls: true);
                    break;
                case "cap":
                    Cap(head);
                    break;
                case "sneakers":
                    Shoes(m, s, 0.012f, sole: 0.012f, toe: 0f);
                    break;
                case "juttis":
                    Shoes(m, s, 0.006f, sole: 0.006f, toe: 0.02f);
                    break;
                case "chappals":
                    Chappals(m, s);
                    break;
                case "formal_shoes":
                    Shoes(m, s, 0.009f, sole: 0.008f, toe: 0f);
                    break;
                case "bangles":
                    Bangles(m, s, left: true, right: true);
                    break;
                case "necklace":
                    Torus(m, Main, new V3(0f, 1.43f, 0.01f), new V3(0f, 0.85f, -0.5f).Normalized, 0.088f, 0.005f, C);
                    HeadGenerator.Blob(m, Main, new V3(0f, 1.34f, 0.13f), new V3(0.016f, 0.02f, 0.008f), 4, 8, -1, C);
                    break;
                case "earrings":
                    foreach (int side in new[] { -1, 1 })
                    {
                        V3 lobe = HeadGenerator.Center + new V3(side * (HeadGenerator.Rx + 0.008f), -0.04f, -0.01f);
                        HeadGenerator.Blob(head, Main, lobe, new V3(0.006f, 0.006f, 0.006f), 3, 6);
                        HeadGenerator.Blob(head, Main, lobe + new V3(0f, -0.016f, 0f), new V3(0.011f, 0.012f, 0.011f), 4, 8);
                    }

                    break;
                case "watch":
                    Bangles(m, s, left: true, right: false, watch: true);
                    break;
                default:
                    throw new ArgumentException("no generator for garment " + id);
            }

            if (FollowsLegs(id))
            {
                FollowLegs(m);
            }

            g.Skinned = m.VertexCount > 0 ? Pad(m) : null;
            g.HeadRigid = head.VertexCount > 0 ? Pad(head) : null;
            return g;
        }

        public enum Sleeve
        {
            None,
            Short,
            ThreeQuarter,
            Long,
        }

        /// <summary>
        /// A top: torso rings from <paramref name="fromRing"/> up to the shoulders (closed at the neck), sleeves, and when
        /// <paramref name="hemY"/> is above 0 a tube continuing below the hips to that height (kurtas, coats, robes), wide
        /// enough for the legs and flared by <paramref name="flare"/> at the hem.
        /// </summary>
        public static void Top(MeshData m, BodyShape s, float gap, int fromRing, Sleeve sleeve, float hemY, float flare = 1.05f, bool trimHem = false)
        {
            List<List<Ring>> parts = BodyGenerator.Parts(s, gap);
            List<Ring> torso = parts[BodyGenerator.PartTorso];
            var rings = new List<Ring>();
            if (hemY > 0f && hemY < torso[BodyGenerator.TorsoCrotch].Center.Y)
            {
                LegExtent(s, out float side, out float depth);
                Ring hips = torso[BodyGenerator.TorsoHips];
                float top = torso[BodyGenerator.TorsoCrotch].Center.Y;
                const int steps = 5;
                for (int i = steps; i >= 1; i--)
                {
                    float t = i / (float)steps;
                    float y = top - (top - hemY) * t;
                    float f = 1f + (flare - 1f) * t;
                    rings.Add(Upright(new V3(0f, y, 0.005f), Math.Max(side, hips.Side) * f + gap, Math.Max(depth, hips.Depth) * f + gap, H));
                }
            }

            for (int r = fromRing; r <= BodyGenerator.TorsoShoulderTop; r++)
            {
                rings.Add(torso[r]);
            }

            BodyGenerator.Loft(m, Main, rings, 16, capStart: false, capEnd: true);
            if (trimHem)
            {
                Ring hem = rings[0];
                BodyGenerator.Loft(m, Trim, new[] { Grow(hem, 0.003f, 0f), Grow(hem, 0.003f, 0.025f) }, 16, false, false);
            }

            // Short sleeves reach as far as the skin mask's upper-arm region (ring 2), so no bare gap shows below them.
            int last = sleeve == Sleeve.Short ? 2 : sleeve == Sleeve.ThreeQuarter ? 4 : sleeve == Sleeve.Long ? 5 : -1;
            if (last >= 0)
            {
                foreach (int arm in new[] { BodyGenerator.PartLeftArm, BodyGenerator.PartRightArm })
                {
                    List<Ring> a = parts[arm];
                    var sleeveRings = new List<Ring>();
                    for (int r = 0; r <= last; r++)
                    {
                        sleeveRings.Add(r == last && sleeve == Sleeve.Short ? Grow(a[r], 0.006f, 0f) : a[r]);
                    }

                    BodyGenerator.Loft(m, Main, sleeveRings, 10, capStart: false, capEnd: false);
                }
            }
        }

        /// <summary>
        /// Trousers: a seat from the crotch to the waist and two legs to ring <paramref name="lastLegRing"/> (6 ankle, 5 mid-calf,
        /// 2 mid-thigh), each ring scaled by <paramref name="loose"/>(t) along the leg (t 0 hip to 1 ankle).
        /// </summary>
        public static void Pants(MeshData m, BodyShape s, float gap, int lastLegRing, Func<float, float> loose)
        {
            List<List<Ring>> parts = BodyGenerator.Parts(s, gap);
            List<Ring> torso = parts[BodyGenerator.PartTorso];
            float seat = Math.Max(1f, loose(0f) * 0.95f);
            BodyGenerator.Loft(m, Main, new[]
            {
                Scale(torso[BodyGenerator.TorsoCrotch], seat), Scale(torso[BodyGenerator.TorsoHips], Math.Max(1f, seat * 0.97f)), torso[BodyGenerator.TorsoWaist],
            }, 16, capStart: true, capEnd: false);
            foreach (int leg in new[] { BodyGenerator.PartLeftLeg, BodyGenerator.PartRightLeg })
            {
                List<Ring> l = parts[leg];
                var rings = new List<Ring>();
                for (int r = 0; r <= lastLegRing; r++)
                {
                    rings.Add(Scale(l[r], loose(r / 6f)));
                }

                BodyGenerator.Loft(m, Main, rings, 10, capStart: false, capEnd: false);
            }
        }

        /// <summary>A skirt from a torso ring down to <paramref name="bottomY"/>, flaring to <paramref name="flare"/> times the leg width.</summary>
        public static void Skirt(MeshData m, BodyShape s, float gap, int fromRing, float bottomY, float flare, bool trimHem)
        {
            List<Ring> torso = BodyGenerator.Parts(s, gap)[BodyGenerator.PartTorso];
            LegExtent(s, out float side, out float depth);
            Ring waist = torso[fromRing];
            Ring hips = torso[BodyGenerator.TorsoHips];
            var rings = new List<Ring>();
            const int steps = 6;
            float top = hips.Center.Y;
            for (int i = steps; i >= 1; i--)
            {
                float t = i / (float)steps;
                float y = top - (top - bottomY) * t;
                float f = 1f + (flare - 1f) * t * t;
                rings.Add(Upright(new V3(0f, y, 0.005f), Math.Max(side, hips.Side) * f + gap, Math.Max(depth, hips.Depth) * f + gap, H));
            }

            rings.Add(Grow(hips, gap * 0.5f, 0f));
            rings.Add(waist);
            BodyGenerator.Loft(m, Main, rings, 18, capStart: false, capEnd: false);
            if (trimHem)
            {
                BodyGenerator.Loft(m, Trim, new[] { Grow(rings[0], 0.003f, 0f), Grow(rings[0], 0.003f, 0.05f) }, 18, false, false);
            }
        }

        /// <summary>A thin strip lofted through points (drapes, borders), each point weighted to its bone.</summary>
        public static void Ribbon(MeshData m, int submesh, V3[] points, float halfWidth, float thickness, int[] bones, BodyShape s, float gap)
        {
            BodyGenerator.Factors(s, out float w, out float d, out float belly, out float _);
            var rings = new List<Ring>();
            for (int i = 0; i < points.Length; i++)
            {
                V3 axis = (points[Math.Min(points.Length - 1, i + 1)] - points[Math.Max(0, i - 1)]).Normalized;

                // Follow the body: push the path out with the torso's width and depth for this body style.
                V3 p = points[i];
                float depthScale = p.Y < 1.18f ? belly : d;
                var fitted = new V3(p.X * w + Math.Sign(p.X) * gap, p.Y, p.Z * depthScale + Math.Sign(p.Z) * gap);
                rings.Add(new Ring { Center = fitted, Axis = axis, Side = halfWidth, Depth = thickness, BoneA = bones[i], BoneB = bones[i], Region = BodyRegion.None });
            }

            BodyGenerator.Loft(m, submesh, rings, 6, capStart: true, capEnd: true);
        }

        private static void Neckline(MeshData m, BodyShape s, float gap)
        {
            Ring top = BodyGenerator.Parts(s, gap)[BodyGenerator.PartTorso][BodyGenerator.TorsoShoulderTop];
            Ring neck = BodyGenerator.Parts(s, gap)[BodyGenerator.PartTorso][BodyGenerator.TorsoNeck];
            var collar = new Ring
            {
                Center = new V3(top.Center.X, top.Center.Y + 0.02f, top.Center.Z + 0.003f), Axis = V3.Up, Side = neck.Side + 0.012f, Depth = neck.Depth + 0.012f,
                BoneA = AvatarSkeleton.Chest, BoneB = AvatarSkeleton.Neck, WeightB = 0.3f,
            };
            BodyGenerator.Loft(m, Trim, new[] { collar, Grow(collar, 0.002f, 0.022f) }, 12, false, false);
        }

        private static void Buttons(MeshData m, BodyShape s, float fromY, float toY, float gap, int count)
        {
            BodyGenerator.Factors(s, out float _, out float d, out float belly, out float _);
            for (int i = 0; i < count; i++)
            {
                float y = fromY + (toY - fromY) * i / Math.Max(1, count - 1);
                float z = (y < 1.18f ? 0.105f * belly : 0.11f * d) + gap + 0.004f;
                HeadGenerator.Blob(m, Trim, new V3(0f, y, z), new V3(0.007f, 0.007f, 0.003f), 3, 6, -1, y < 1.15f ? S : C);
            }
        }

        private static void Shawl(MeshData m, BodyShape s)
        {
            List<Ring> torso = BodyGenerator.Parts(s, OuterGap + 0.012f)[BodyGenerator.PartTorso];
            BodyGenerator.Loft(m, Main, new[]
            {
                Grow(torso[BodyGenerator.TorsoChest], 0.02f, 0f), torso[BodyGenerator.TorsoUpperChest], torso[BodyGenerator.TorsoShoulderTop],
            }, 16, capStart: false, capEnd: true);
            Ribbon(m, Main, new[] { new V3(-0.1f, 1.38f, 0.12f), new V3(-0.11f, 1.1f, 0.14f), new V3(-0.11f, 0.85f, 0.14f) }, 0.06f, 0.005f, new[] { C, S, S }, s, OuterGap);
            Ribbon(m, Main, new[] { new V3(0.1f, 1.38f, 0.12f), new V3(0.11f, 1.1f, 0.14f), new V3(0.11f, 0.85f, 0.14f) }, 0.06f, 0.005f, new[] { C, S, S }, s, OuterGap);
        }

        private static void Shoes(MeshData m, BodyShape s, float gap, float sole, float toe)
        {
            List<List<Ring>> parts = BodyGenerator.Parts(s, gap);
            foreach (int foot in new[] { BodyGenerator.PartLeftFoot, BodyGenerator.PartRightFoot })
            {
                List<Ring> f = parts[foot];
                BodyGenerator.Loft(m, Main, f, 10, capStart: true, capEnd: true);
                int bone = f[0].BoneA;
                float x = f[1].Center.X;
                HeadGenerator.Blob(m, Trim, new V3(x, sole * 0.5f, 0.06f), new V3(0.05f, sole * 0.5f + 0.002f, 0.12f), 3, 10, -1, bone);
                if (toe > 0f)
                {
                    // Juttis: the pointed toe turns up.
                    HeadGenerator.Blob(m, Main, new V3(f[3].Center.X, f[3].Center.Y + toe * 0.6f, f[3].Center.Z + 0.03f), new V3(0.012f, 0.012f, 0.025f), 4, 6, -1, bone);
                }
            }
        }

        private static void Chappals(MeshData m, BodyShape s)
        {
            List<List<Ring>> parts = BodyGenerator.Parts(s, 0f);
            foreach (int foot in new[] { BodyGenerator.PartLeftFoot, BodyGenerator.PartRightFoot })
            {
                List<Ring> f = parts[foot];
                int bone = f[0].BoneA;
                float x = f[1].Center.X;
                HeadGenerator.Blob(m, Main, new V3(x, 0.006f, 0.06f), new V3(0.05f, 0.006f, 0.125f), 3, 10, -1, bone);
                Torus(m, Trim, new V3(x, 0.035f, 0.1f), new V3(0f, 0.3f, 1f).Normalized, 0.04f, 0.004f, bone);
            }
        }

        private static void Bangles(MeshData m, BodyShape s, bool left, bool right, bool watch = false)
        {
            List<List<Ring>> parts = BodyGenerator.Parts(s, 0.004f);
            foreach (int arm in new[] { BodyGenerator.PartLeftArm, BodyGenerator.PartRightArm })
            {
                if ((arm == BodyGenerator.PartLeftArm && !left) || (arm == BodyGenerator.PartRightArm && !right))
                {
                    continue;
                }

                Ring wrist = parts[arm][5];
                int bone = wrist.BoneA;
                if (watch)
                {
                    Torus(m, Main, wrist.Center, wrist.Axis, wrist.Side + 0.004f, 0.006f, bone);
                    BodyGenerator.Frame(wrist.Axis, out V3 side, out V3 _);
                    HeadGenerator.Blob(m, Trim, wrist.Center + side * (wrist.Side + 0.008f), new V3(0.006f, 0.014f, 0.014f), 3, 8, -1, bone);
                    continue;
                }

                for (int k = 0; k < 3; k++)
                {
                    Torus(m, k == 1 ? Trim : Main, wrist.Center + wrist.Axis * (-0.012f * k), wrist.Axis, wrist.Side + 0.008f, 0.004f, bone);
                }
            }
        }

        private static void Topi(MeshData head)
        {
            V3 c = HeadGenerator.Center;
            var rings = new[]
            {
                Upright(new V3(c.X, c.Y + 0.055f, c.Z - 0.005f), HeadGenerator.Rx * 1.0f, HeadGenerator.Rz * 0.98f, AvatarSkeleton.Head),
                Upright(new V3(c.X, c.Y + 0.1f, c.Z - 0.01f), HeadGenerator.Rx * 0.97f, HeadGenerator.Rz * 0.96f, AvatarSkeleton.Head),
                Upright(new V3(c.X, c.Y + 0.14f, c.Z - 0.02f), HeadGenerator.Rx * 0.88f, HeadGenerator.Rz * 0.9f, AvatarSkeleton.Head),
            };
            BodyGenerator.Loft(head, Main, rings, 16, capStart: false, capEnd: true);
            BodyGenerator.Loft(head, Trim, new[] { Grow(rings[0], 0.002f, 0f), Grow(rings[0], 0.002f, 0.015f) }, 16, false, false);
        }

        private static void Turban(MeshData head)
        {
            V3 c = HeadGenerator.Center;
            for (int i = 0; i < 5; i++)
            {
                float y = c.Y + 0.03f + i * 0.028f;
                float k = 1.12f - i * 0.07f;
                Torus(head, i % 2 == 0 ? Main : Trim, new V3(c.X, y, c.Z - 0.01f), new V3(0f, 1f, 0.12f).Normalized, HeadGenerator.Rx * k, 0.028f, AvatarSkeleton.Head);
            }

            HeadGenerator.Blob(head, Main, new V3(c.X, c.Y + 0.1f, c.Z - 0.015f), new V3(HeadGenerator.Rx * 0.95f, 0.07f, HeadGenerator.Rz * 0.95f), 6, 14);
        }

        private static void Headscarf(MeshData head, bool falls)
        {
            // Wraps the head and leaves an oval for the face.
            HeadGenerator.ShellAround(head, Main, 1.12f, n => !(n.Z > 0.45f && Math.Abs(n.X) < 0.62f && n.Y < 0.42f && n.Y > -0.82f), n => 1f + 0.03f * Math.Max(0f, -n.Y));
            V3 c = HeadGenerator.Center;
            float bottom = falls ? 1.33f : 1.4f;
            var drape = new[]
            {
                Upright(new V3(c.X, c.Y - 0.1f, c.Z - 0.02f), 0.09f, 0.1f, AvatarSkeleton.Head),
                Upright(new V3(c.X, 1.48f, c.Z - 0.02f), 0.12f, 0.12f, AvatarSkeleton.Head),
                Upright(new V3(c.X, bottom, c.Z - 0.03f), falls ? 0.22f : 0.19f, 0.15f, AvatarSkeleton.Head),
            };
            BodyGenerator.Loft(head, Main, drape, 16, capStart: false, capEnd: false);
            if (falls)
            {
                // The agal: a dark cord ring on the crown.
                Torus(head, Trim, new V3(c.X, c.Y + 0.06f, c.Z - 0.01f), new V3(0f, 1f, 0.15f).Normalized, HeadGenerator.Rx * 1.08f, 0.009f, AvatarSkeleton.Head);
            }
        }

        private static void Cap(MeshData head)
        {
            HeadGenerator.ShellAround(head, Main, 1.1f, n => n.Y > 0.28f, n => 1f);
            V3 c = HeadGenerator.Center;
            HeadGenerator.Blob(head, Trim, new V3(c.X, c.Y + 0.045f, c.Z + HeadGenerator.Rz * 1.05f), new V3(0.075f, 0.006f, 0.06f), 3, 12);
        }

        /// <summary>A ring of tube around <paramref name="axis"/> (bangles, necklace, turban layers, agal).</summary>
        public static void Torus(MeshData m, int submesh, V3 center, V3 axis, float major, float minor, int bone)
        {
            BodyGenerator.Frame(axis, out V3 e1, out V3 e2);
            const int steps = 18;
            var rings = new List<Ring>();
            for (int i = 0; i <= steps; i++)
            {
                double a = 2 * Math.PI * i / steps;
                V3 radial = e1 * (float)Math.Cos(a) + e2 * (float)Math.Sin(a);
                V3 tangent = e1 * -(float)Math.Sin(a) + e2 * (float)Math.Cos(a);
                rings.Add(new Ring { Center = center + radial * major, Axis = tangent, Side = minor, Depth = minor, BoneA = bone, BoneB = bone, Region = BodyRegion.None });
            }

            BodyGenerator.Loft(m, submesh, rings, 6, capStart: false, capEnd: false);
        }

        /// <summary>Long hems that hang around both legs: skirts, saris, robes and knee-length tops.</summary>
        public static bool FollowsLegs(string id) => id switch
        {
            "kurta" or "short_kurta" or "daura" or "lehenga" or "long_skirt" or "sari_nivi" or "sari_seedha" or "abaya" or "thobe" or "sherwani" => true,
            _ => false,
        };

        /// <summary>
        /// Lets a long hem move with the walking legs (PRD 8: rigid skinning, no cloth chains, on the low tier): below the crotch
        /// each vertex takes a share of the nearer thigh's motion, growing towards the hem and towards the sides, so the fabric
        /// swings with the stride instead of the legs pushing through it.
        /// </summary>
        public static void FollowLegs(MeshData m, float topY = 0.84f, float bottomY = 0.1f)
        {
            for (int i = 0; i < m.VertexCount; i++)
            {
                V3 p = m.Positions[i];
                if (p.Y >= topY || m.BoneA[i] != H)
                {
                    continue;
                }

                float low = Math.Max(0f, Math.Min(1f, (topY - p.Y) / (topY - bottomY)));
                float sideways = Math.Max(0f, Math.Min(1f, Math.Abs(p.X) / 0.09f));
                m.BoneB[i] = p.X < 0f ? AvatarSkeleton.LeftUpperLeg : AvatarSkeleton.RightUpperLeg;
                m.WeightB[i] = 0.8f * (float)Math.Sqrt(low) * sideways;
            }
        }

        /// <summary>How far the legs reach sideways and front to back, plus a margin: hems and skirts never cut through them.</summary>
        public static void LegExtent(BodyShape s, out float side, out float depth)
        {
            BodyGenerator.Factors(s, out float _, out float _, out float _, out float limb);
            float leg = limb * (s.Style > 1f ? 1f + 0.08f * (s.Style - 1f) : 1f);
            float hp = 1f + 0.12f * s.Hips;
            side = 0.09f + 0.008f * Math.Abs(s.Hips) + 0.088f * leg * hp + 0.018f;
            depth = 0.092f * leg + 0.025f;
        }

        private static Ring Upright(V3 center, float side, float depth, int bone) =>
            new Ring { Center = center, Axis = V3.Up, Side = side, Depth = depth, BoneA = bone, BoneB = bone, Region = BodyRegion.None };

        private static Ring Grow(Ring r, float by, float down)
        {
            r.Side += by;
            r.Depth += by;
            r.Center = r.Center - r.Axis * down;
            return r;
        }

        private static Ring Scale(Ring r, float k)
        {
            r.Side *= k;
            r.Depth *= k;
            return r;
        }

        /// <summary>Garments always have both submeshes, so materials line up even when a garment has no trim.</summary>
        private static MeshData Pad(MeshData m)
        {
            while (m.Submeshes.Count < 2)
            {
                m.Submeshes.Add(new List<int>());
                m.TriangleRegions.Add(new List<BodyRegion>());
            }

            return m;
        }
    }
}

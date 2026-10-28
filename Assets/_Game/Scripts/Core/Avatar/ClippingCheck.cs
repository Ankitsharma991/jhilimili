using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace UtsavNights.Core.Avatar
{
    /// <summary>One garment, body style and pose: how far anything pokes through, and how much the hem stretches.</summary>
    public sealed class ClipResult
    {
        public string Garment = string.Empty;
        public string Outfit = string.Empty;
        public float BodyStyle;
        public string Pose = string.Empty;

        /// <summary>Metres a leg, foot, trouser or inner hem pokes out through a long hem.</summary>
        public float HemPenetration;

        /// <summary>Metres a drape (pallu, dupatta, shawl end, tie) sinks into the torso surface under it.</summary>
        public float DrapePenetration;

        /// <summary>Largest posed-to-rest length ratio of a hem's vertical edges.</summary>
        public float Stretch = 1f;

        public string Worst
        {
            get
            {
                if (HemPenetration >= DrapePenetration && HemPenetration > 0f)
                {
                    return $"hem {HemPenetration * 100f:0.0} cm";
                }

                return DrapePenetration > 0f ? $"drape {DrapePenetration * 100f:0.0} cm" : "none";
            }
        }

        public bool Blocker => HemPenetration > ClippingCheck.BlockerMetres || DrapePenetration > ClippingCheck.BlockerMetres || Stretch > ClippingCheck.BlockerStretch;

        public bool Warning => !Blocker && (HemPenetration > ClippingCheck.WarningMetres || DrapePenetration > ClippingCheck.WarningMetres || Stretch > ClippingCheck.WarningStretch);
    }

    /// <summary>
    /// The clipping test matrix (Phase 6 step 7, PRD 19 deliverable): every garment, dressed in a realistic outfit, on slim,
    /// average and broad bodies, in idle, walk (both legs forward), the ignite crouch and a lively celebration. Garments built
    /// on the body's own rings with the same bone weights cannot clip, so the check targets where weights differ: long hems
    /// that follow the thighs (legs, feet, trousers and inner hems must stay inside), drapes pinned to single bones (they must
    /// stay outside the torso under them) and hem stretch. Engine-free, so it runs in the plain test runner.
    /// </summary>
    public static class ClippingCheck
    {
        /// <summary>Penetration a player would notice at phone distance.</summary>
        public const float BlockerMetres = 0.015f;

        public const float WarningMetres = 0.006f;
        public const float BlockerStretch = 2.2f;
        public const float WarningStretch = 1.6f;
        private const float HemTop = 0.8f;
        private const float Band = 0.045f;

        public static readonly (AvatarClip clip, float time, AnimationStyle style, string name)[] Poses =
        {
            (AvatarClip.Idle, 0f, AnimationStyle.Calm, "idle"),
            (AvatarClip.Walk, 0.26f, AnimationStyle.Lively, "walk (left leg forward)"),
            (AvatarClip.Walk, 0.79f, AnimationStyle.Lively, "walk (right leg forward)"),
            (AvatarClip.Ignite, 0.9f, AnimationStyle.Calm, "ignite crouch"),
            (AvatarClip.Celebrate, 0.6f, AnimationStyle.Lively, "celebrate"),
        };

        public static readonly float[] BodyStyles = { 0f, 1f, 2f };

        private static readonly Dictionary<string, MeshData> GarmentCache = new Dictionary<string, MeshData>();
        private static MeshData _body;

        public static List<ClipResult> RunMatrix(WardrobeCatalog catalog = null)
        {
            catalog ??= WardrobeCatalog.Default;
            var results = new List<ClipResult>();
            var poser = new AvatarPoser();
            var pose = new AvatarPose();
            foreach (GarmentInfo g in catalog.All)
            {
                AvatarProfile outfit = OutfitFor(g.Id, catalog);
                foreach (float style in BodyStyles)
                {
                    outfit.bodyStyle = style;
                    foreach (var p in Poses)
                    {
                        AvatarMotion.Evaluate(p.clip, p.time, p.style, pose, AvatarMotion.HasLongHem(outfit));
                        poser.SetPose(pose);
                        ClipResult r = Check(outfit, catalog, poser);
                        r.Garment = g.Id;
                        r.Pose = p.name;
                        results.Add(r);
                    }
                }
            }

            return results;
        }

        /// <summary>The garment worn the way people wear it: the outfit rules fill in the rest, with shoes under long hems.</summary>
        public static AvatarProfile OutfitFor(string id, WardrobeCatalog catalog)
        {
            var p = new AvatarProfile();
            OutfitRules.Equip(p, catalog, id);
            if (catalog.Find(id).Slots != WardrobeSlot.Footwear)
            {
                OutfitRules.Equip(p, catalog, "sneakers");
            }

            p.Sanitize(catalog);
            return p;
        }

        public static ClipResult Check(AvatarProfile outfit, WardrobeCatalog catalog, AvatarPoser poser)
        {
            var weights = BodyGenerator.Weights(outfit).ToList();
            BodyRegion covered = OutfitRules.Covered(outfit, catalog);
            var layers = new List<(GarmentInfo info, MeshData mesh, V3[] posed)>();
            foreach (EquippedItem item in outfit.outfit)
            {
                MeshData mesh = Garment(item.id);
                if (mesh != null)
                {
                    layers.Add((catalog.Find(item.id), mesh, poser.Skin(mesh, weights)));
                }
            }

            _body ??= BodyGenerator.Build();
            V3[] body = poser.Skin(_body, weights);
            var result = new ClipResult { BodyStyle = outfit.bodyStyle, Outfit = string.Join(" + ", outfit.outfit.Select(i => i.id)) };

            foreach (var hem in layers.Where(l => GarmentGenerator.FollowsLegs(l.info.Id)))
            {
                // Everything visible that should stay inside this hem: uncovered legs and feet, trousers, shoes, inner hems.
                var inside = new List<V3>();
                if ((covered & BodyRegion.Legs) == 0)
                {
                    AddLoops(inside, _body, body, "leg");
                }

                if ((covered & BodyRegion.Feet) == 0)
                {
                    AddLoops(inside, _body, body, "foot");
                }

                foreach (var other in layers.Where(l => l.info != hem.info))
                {
                    AddLoops(inside, other.mesh, other.posed, "leg");
                    AddLoops(inside, other.mesh, other.posed, "foot");
                    if (Inner(other.info, hem.info) && GarmentGenerator.FollowsLegs(other.info.Id))
                    {
                        AddHemLoops(inside, other.mesh, other.posed);
                    }
                }

                // Below the hem's lowest ring is open air: feet and shoes showing under a long skirt are not clipping.
                float hemBottom = float.MaxValue;
                foreach (MeshData.Loop loop in HemLoops(hem.mesh))
                {
                    Polygon(hem.posed, loop, out V3[] _, out float y);
                    hemBottom = Math.Min(hemBottom, y);
                }

                foreach (MeshData.Loop loop in HemLoops(hem.mesh))
                {
                    Polygon(hem.posed, loop, out V3[] ring, out float y);
                    foreach (V3 p in inside)
                    {
                        if (Math.Abs(p.Y - y) <= Band && p.Y > hemBottom + 0.01f)
                        {
                            result.HemPenetration = Math.Max(result.HemPenetration, Outside(ring, p));
                        }
                    }
                }

                result.Stretch = Math.Max(result.Stretch, HemStretch(hem.mesh, hem.posed));
            }

            foreach (var drape in layers)
            {
                var ribbon = new List<V3>();
                AddLoops(ribbon, drape.mesh, drape.posed, "ribbon");
                if (ribbon.Count == 0)
                {
                    continue;
                }

                // The torso surface under the drape: the worn top's torso if it covers the chest, otherwise the body's.
                var surfaces = new List<(V3[] ring, float y)>();
                var top = layers.FirstOrDefault(l => l.info != drape.info && Inner(l.info, drape.info) && (l.info.Covers & BodyRegion.Chest) != 0);
                if (top.mesh != null)
                {
                    foreach (MeshData.Loop loop in top.mesh.Loops.Where(l => l.Tag == "top" && RestY(top.mesh, l) >= HemTop + 0.05f))
                    {
                        Polygon(top.posed, loop, out V3[] ring, out float y);
                        surfaces.Add((ring, y));
                    }
                }
                else
                {
                    foreach (MeshData.Loop loop in _body.Loops.Where(l => l.Tag == "torso"))
                    {
                        Polygon(body, loop, out V3[] ring, out float y);
                        surfaces.Add((ring, y));
                    }
                }

                // Compare each drape point with the torso surface at its exact height: the two loops around it, blended. Points
                // above the topmost loop lie over the shoulders (on top, not inside) and are skipped.
                surfaces.Sort((a, b) => a.y.CompareTo(b.y));
                foreach (V3 p in ribbon)
                {
                    for (int k = 0; k + 1 < surfaces.Count; k++)
                    {
                        var lower = surfaces[k];
                        var upper = surfaces[k + 1];
                        if (p.Y < lower.y || p.Y > upper.y || lower.ring.Length != upper.ring.Length)
                        {
                            continue;
                        }

                        float t = upper.y - lower.y > 1e-4f ? (p.Y - lower.y) / (upper.y - lower.y) : 0f;
                        var blended = new V3[lower.ring.Length];
                        for (int i = 0; i < blended.Length; i++)
                        {
                            blended[i] = V3.Lerp(lower.ring[i], upper.ring[i], t);
                        }

                        result.DrapePenetration = Math.Max(result.DrapePenetration, Inside(blended, p));
                        break;
                    }
                }
            }

            return result;
        }

        /// <summary>The report: summary, the worst cases, and the worst result per garment.</summary>
        public static string Report(List<ClipResult> results, string date)
        {
            var sb = new StringBuilder();
            int blockers = results.Count(r => r.Blocker), warnings = results.Count(r => r.Warning);
            sb.AppendLine("# Clipping test report (Phase 6 step 7)").AppendLine();
            sb.AppendLine($"Generated {date} by `ClippingCheck.RunMatrix` (Utsav → Phase 6 → Write Clipping Report).").AppendLine();
            sb.AppendLine($"**{results.Count} combinations:** {results.Select(r => r.Garment).Distinct().Count()} garments × {BodyStyles.Length} body styles " +
                          $"(slim, average, broad) × {Poses.Length} poses ({string.Join(", ", Poses.Select(p => p.name))}).").AppendLine();
            sb.AppendLine($"**Blockers: {blockers}.** Warnings: {warnings}. A blocker is penetration over {BlockerMetres * 100f:0.#} cm or stretch over {BlockerStretch:0.#}×; " +
                          $"a warning is over {WarningMetres * 100f:0.#} cm or {WarningStretch:0.#}×.").AppendLine();
            sb.AppendLine("What is checked: garments built on the body's own rings with the same bone weights cannot clip by construction (tops, sleeves, " +
                          "trousers, shoes). The check covers where weights differ: long hems that follow the thighs (uncovered legs and feet, trousers, " +
                          "shoes and inner hems must stay inside), drapes pinned to single bones (they must stay outside the torso under them), and hem stretch. " +
                          "Skin under a garment is hidden by the skin mask, so masked body parts are not counted.").AppendLine();

            sb.AppendLine("## Worst 15").AppendLine();
            sb.AppendLine("| Garment | Outfit | Body | Pose | Worst | Stretch | |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            foreach (ClipResult r in results.OrderByDescending(r => Math.Max(r.HemPenetration, r.DrapePenetration) + Math.Max(0f, r.Stretch - 1f) * 0.01f).Take(15))
            {
                sb.AppendLine($"| {r.Garment} | {r.Outfit} | {StyleName(r.BodyStyle)} | {r.Pose} | {r.Worst} | {r.Stretch.ToString("0.00", CultureInfo.InvariantCulture)}× | {Mark(r)} |");
            }

            sb.AppendLine().AppendLine("## Every garment (worst case across bodies and poses)").AppendLine();
            sb.AppendLine("| Garment | Worst | Stretch | |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var group in results.GroupBy(r => r.Garment))
            {
                ClipResult worst = group.OrderByDescending(r => Math.Max(r.HemPenetration, r.DrapePenetration)).First();
                float stretch = group.Max(r => r.Stretch);
                bool blocker = group.Any(r => r.Blocker), warning = group.Any(r => r.Warning);
                sb.AppendLine($"| {group.Key} | {worst.Worst} | {stretch.ToString("0.00", CultureInfo.InvariantCulture)}× | {(blocker ? "❌ blocker" : warning ? "⚠ warning" : "✅")} |");
            }

            return sb.ToString();
        }

        private static string Mark(ClipResult r) => r.Blocker ? "❌" : r.Warning ? "⚠" : "✅";

        private static string StyleName(float style) => style < 0.5f ? "slim" : style > 1.5f ? "broad" : "average";

        private static MeshData Garment(string id)
        {
            if (!GarmentCache.TryGetValue(id, out MeshData mesh))
            {
                mesh = GarmentGenerator.Build(id).Skinned;
                GarmentCache[id] = mesh;
            }

            return mesh;
        }

        /// <summary>True when <paramref name="a"/> is worn under <paramref name="b"/> (base under mid under outer).</summary>
        private static bool Inner(GarmentInfo a, GarmentInfo b) => Layer(a) < Layer(b);

        private static int Layer(GarmentInfo g) =>
            (g.Slots & WardrobeSlot.Outer) != 0 ? 3 : (g.Slots & WardrobeSlot.Mid) != 0 ? 2 : (g.Slots & (WardrobeSlot.BaseTop | WardrobeSlot.BaseBottom)) != 0 ? 1 : 0;

        private static IEnumerable<MeshData.Loop> HemLoops(MeshData mesh) =>
            mesh.Loops.Where(l => (l.Tag == "top" || l.Tag == "skirt") && RestY(mesh, l) < HemTop);

        private static void AddHemLoops(List<V3> into, MeshData mesh, V3[] posed)
        {
            foreach (MeshData.Loop loop in HemLoops(mesh))
            {
                for (int i = 0; i < loop.Count; i++)
                {
                    into.Add(posed[loop.Start + i]);
                }
            }
        }

        private static void AddLoops(List<V3> into, MeshData mesh, V3[] posed, string tag)
        {
            foreach (MeshData.Loop loop in mesh.Loops)
            {
                if (loop.Tag != tag)
                {
                    continue;
                }

                for (int i = 0; i < loop.Count; i++)
                {
                    into.Add(posed[loop.Start + i]);
                }
            }
        }

        private static float RestY(MeshData mesh, MeshData.Loop loop)
        {
            float y = 0f;
            for (int i = 0; i < loop.Count; i++)
            {
                y += mesh.Positions[loop.Start + i].Y;
            }

            return y / loop.Count;
        }

        private static void Polygon(V3[] posed, MeshData.Loop loop, out V3[] ring, out float y)
        {
            ring = new V3[loop.Count - 1]; // the last vertex repeats the first at the uv seam
            y = 0f;
            for (int i = 0; i < ring.Length; i++)
            {
                ring[i] = posed[loop.Start + i];
                y += ring[i].Y;
            }

            y /= ring.Length;
        }

        /// <summary>How far point <paramref name="p"/> lies outside the polygon in the horizontal plane (0 when inside).</summary>
        public static float Outside(V3[] ring, V3 p) => Contains(ring, p) ? 0f : EdgeDistance(ring, p);

        /// <summary>How far point <paramref name="p"/> lies inside the polygon in the horizontal plane (0 when outside).</summary>
        public static float Inside(V3[] ring, V3 p) => Contains(ring, p) ? EdgeDistance(ring, p) : 0f;

        private static bool Contains(V3[] ring, V3 p)
        {
            bool inside = false;
            for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
            {
                if ((ring[i].Z > p.Z) != (ring[j].Z > p.Z) &&
                    p.X < (ring[j].X - ring[i].X) * (p.Z - ring[i].Z) / (ring[j].Z - ring[i].Z) + ring[i].X)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private static float EdgeDistance(V3[] ring, V3 p)
        {
            float best = float.MaxValue;
            for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
            {
                float ax = ring[j].X, az = ring[j].Z, bx = ring[i].X, bz = ring[i].Z;
                float dx = bx - ax, dz = bz - az;
                float len = dx * dx + dz * dz;
                float t = len > 0f ? Math.Max(0f, Math.Min(1f, ((p.X - ax) * dx + (p.Z - az) * dz) / len)) : 0f;
                float ex = ax + dx * t - p.X, ez = az + dz * t - p.Z;
                best = Math.Min(best, (float)Math.Sqrt(ex * ex + ez * ez));
            }

            return best;
        }

        private static float HemStretch(MeshData mesh, V3[] posed)
        {
            float worst = 1f;
            var loops = HemLoops(mesh).OrderBy(l => l.Start).ToList();
            for (int k = 0; k + 1 < loops.Count; k++)
            {
                MeshData.Loop a = loops[k], b = loops[k + 1];
                if (b.Start != a.Start + a.Count || a.Count != b.Count)
                {
                    continue; // not neighbouring rings of the same loft
                }

                for (int i = 0; i < a.Count; i++)
                {
                    float rest = (mesh.Positions[b.Start + i] - mesh.Positions[a.Start + i]).Length;
                    float now = (posed[b.Start + i] - posed[a.Start + i]).Length;
                    if (rest > 1e-4f)
                    {
                        worst = Math.Max(worst, now / rest);
                    }
                }
            }

            return worst;
        }
    }
}

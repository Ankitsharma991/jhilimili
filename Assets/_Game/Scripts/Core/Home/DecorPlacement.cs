using System;
using System.Collections.Generic;
using UtsavNights.Core.Avatar;

namespace UtsavNights.Core.Home
{
    /// <summary>
    /// A flat patch of the house or courtyard that decorations can use: the parallelogram Origin + a·U + b·V for a, b in
    /// [0, 1]. A ledge (a rail or wall top) is narrow: items need only their centre on it. <see cref="Outward"/> is the
    /// horizontal direction a hung item faces (away from the wall, toward the courtyard).
    /// </summary>
    public sealed class SurfacePatch
    {
        public SurfacePatch(string name, Surface kind, V3 origin, V3 u, V3 v, V3 outward, bool ledge = false)
        {
            Name = name;
            Kind = kind;
            Origin = origin;
            U = u;
            V = v;
            Outward = outward;
            Ledge = ledge;
            V3 n = V3.Cross(u, v).Normalized;
            V3 hint = kind == Surface.Wall ? outward : new V3(0, 1, 0);
            Normal = V3.Dot(n, hint) < 0f ? n * -1f : n;
        }

        public string Name { get; }

        public Surface Kind { get; }

        public V3 Origin { get; }

        public V3 U { get; }

        public V3 V { get; }

        public V3 Normal { get; }

        public V3 Outward { get; }

        public bool Ledge { get; }

        /// <summary>Degrees between the normal and straight up.</summary>
        public float Tilt => (float)(Math.Acos(Math.Max(-1f, Math.Min(1f, Normal.Y))) * 180.0 / Math.PI);

        /// <summary>The patch coordinates (a, b) of a point on its plane.</summary>
        public void Coordinates(V3 p, out float a, out float b)
        {
            V3 d = p - Origin;
            float uu = V3.Dot(U, U), uv = V3.Dot(U, V), vv = V3.Dot(V, V);
            float du = V3.Dot(d, U), dv = V3.Dot(d, V);
            float det = uu * vv - uv * uv;
            a = (du * vv - dv * uv) / det;
            b = (dv * uu - du * uv) / det;
        }

        /// <summary>True when p lies on the plane (within <paramref name="planeEps"/>) and inside the patch.</summary>
        public bool Holds(V3 p, float planeEps = 0.01f, float edgeEps = 1e-3f)
        {
            if (Math.Abs(V3.Dot(p - Origin, Normal)) > planeEps)
            {
                return false;
            }

            Coordinates(p, out float a, out float b);
            float ea = edgeEps / Math.Max(1e-4f, U.Length), eb = edgeEps / Math.Max(1e-4f, V.Length);
            return a >= -ea && a <= 1f + ea && b >= -eb && b <= 1f + eb;
        }
    }

    public readonly struct SurfaceHit
    {
        public SurfaceHit(SurfacePatch patch, V3 point, float distance)
        {
            Patch = patch;
            Point = point;
            Distance = distance;
        }

        public SurfacePatch Patch { get; }

        public V3 Point { get; }

        public float Distance { get; }
    }

    public enum PlacementProblem
    {
        None,
        UnknownItem,
        NoSurface,
        WrongSurface,
        TooSteep,
        OverEdge,
        HitsHouse,
        Overlaps,
        BlocksDoor,
        BlocksGate,
        TooLow,
        PathTooShort,
        PathTooLong,
        PathBlocked,
        OverBudget,
        CategoryFull,
    }

    public sealed class PlacementResult
    {
        public PlacementResult(PlacedDecor pose, PlacementProblem problem, string reason)
        {
            Pose = pose;
            Problem = problem;
            Reason = reason;
        }

        /// <summary>The snapped pose (also when refused, so the ghost can show where it would go, tinted red).</summary>
        public PlacedDecor Pose { get; }

        public PlacementProblem Problem { get; }

        /// <summary>Plain-language reason for a refusal (PRD 9.1); empty when the placement is fine.</summary>
        public string Reason { get; }

        public bool Ok => Problem == PlacementProblem.None;
    }

    /// <summary>
    /// Everything placement checks against for one house design and quality tier: the surfaces, the solid parts (house body,
    /// shell ledges, balcony, roof parapets, boundary walls, gate) and the clearances kept free in front of the door and the gate.
    /// </summary>
    public sealed class PlacementContext
    {
        public PlacementContext(HouseDesign design, int tier, List<SurfacePatch> surfaces, List<Box3> solids, Box3 door, Box3 gate)
        {
            Design = design;
            Tier = Math.Max(0, Math.Min(DecorBudget.Units.Length - 1, tier));
            Surfaces = surfaces;
            Solids = solids;
            DoorClearance = door;
            GateClearance = gate;
        }

        public HouseDesign Design { get; }

        public int Tier { get; }

        public List<SurfacePatch> Surfaces { get; }

        public List<Box3> Solids { get; }

        public Box3 DoorClearance { get; }

        public Box3 GateClearance { get; }
    }

    /// <summary>
    /// Placement validation (Phase 7 step 4, PRD 9.1): raycast to the house's surfaces, snapping, support within 20 degrees,
    /// footprints inside their surface and clear of the house and other items, door and gate clearance, budgets per tier and
    /// per category, and a plain-language reason for every refusal. Engine-free, so decoration mode and the tests share it.
    /// </summary>
    public static class DecorPlacement
    {
        public const float MaxSupportTilt = 20f;
        public const float Grid = 0.05f;
        public const float YawStep = 15f;
        public const float MinHangHeight = 0.4f;

        /// <summary>Share of the tier's units one category may use, by <see cref="DecorCategory"/>.</summary>
        public static readonly float[] CategoryShare = { 0.5f, 0.5f, 0.4f, 0.35f, 0.3f, 0.25f };

        private static readonly V3 Up = new V3(0, 1, 0);

        public static int CategoryCap(int tier, DecorCategory category) =>
            (int)Math.Floor(DecorBudget.Units[Math.Max(0, Math.Min(DecorBudget.Units.Length - 1, tier))] * CategoryShare[(int)category]);

        /// <summary>True for items that hang from a wall or rail instead of standing on something.</summary>
        public static bool Hangs(DecorInfo info) =>
            info.Mode == PlacementMode.Free && (info.Surfaces & (Surface.Ground | Surface.Step | Surface.Roof)) == 0;

        public static PlacementContext For(HouseDesign design, int tier)
        {
            var surfaces = new List<SurfacePatch>();
            var solids = new List<Box3>(HouseGeometry.ShellSolids);
            const float hw = HouseGeometry.HalfWidth, inner = HouseGeometry.BoundaryCentre - HouseGeometry.BoundaryThickness / 2f;
            const float back = HouseGeometry.BackWallZ - HouseGeometry.BoundaryThickness / 2f;
            float gateL = HouseGeometry.GateX - 0.61f, gateR = HouseGeometry.GateX + 0.61f;

            // Floor, step and plinth.
            surfaces.Add(new SurfacePatch("courtyard floor", Surface.Ground, new V3(-inner, 0f, 0f), new V3(2 * inner, 0, 0), new V3(0, 0, back), Up));
            surfaces.Add(new SurfacePatch("front step", Surface.Step, new V3(-0.85f, 0.15f, 0.1f), new V3(1.7f, 0, 0), new V3(0, 0, 0.4f), Up));
            surfaces.Add(new SurfacePatch("courtyard step", Surface.Step, new V3(-3f, HouseGeometry.CourtyardStepTop, 4.65f), new V3(6f, 0, 0), new V3(0, 0, 1.1f), Up));
            solids.AddRange(HouseGeometry.CourtyardFixtures);
            surfaces.Add(new SurfacePatch("plinth", Surface.Step, new V3(-3.4f, HouseGeometry.Plinth, -0.02f), new V3(6.8f, 0, 0), new V3(0, 0, 0.12f), Up));

            // The house front, below the cornice.
            surfaces.Add(new SurfacePatch("house front", Surface.Wall, new V3(-hw, HouseGeometry.Plinth, 0f), new V3(2 * hw, 0, 0), new V3(0, 6.12f - HouseGeometry.Plinth, 0), new V3(0, 0, 1)));
            solids.Add(new Box3(-hw, 0f, -HouseGeometry.Depth, hw, HouseGeometry.Eaves, 0f));

            // Boundary walls: inner faces, tops (ledges) and their bodies.
            string boundary = design.ModuleAt("boundary")?.module ?? string.Empty;
            bool grill = boundary == "boundary_grill";
            float top = grill ? 1.5f : HouseGeometry.BoundaryHeight + 0.06f;
            float ledge = grill ? 0.05f : HouseGeometry.BoundaryThickness + 0.04f;
            foreach (int side in new[] { 1, -1 })
            {
                var outward = new V3(-side, 0, 0);
                surfaces.Add(new SurfacePatch(side > 0 ? "left wall" : "right wall", Surface.Wall, new V3(side * inner, 0f, 0f), new V3(0, 0, back), new V3(0, HouseGeometry.BoundaryHeight, 0), outward));
                float lx = side * HouseGeometry.BoundaryCentre - ledge / 2f;
                surfaces.Add(new SurfacePatch(side > 0 ? "left wall top" : "right wall top", Surface.Railing, new V3(lx, top, 0f), new V3(0, 0, back), new V3(ledge, 0, 0), outward, ledge: true));
                solids.Add(new Box3(side * inner, 0f, 0f, side * (HouseGeometry.BoundaryCentre + HouseGeometry.BoundaryThickness / 2f), top, back));
            }

            foreach ((float x0, float x1) in new[] { (-inner, gateL), (gateR, inner) })
            {
                var outward = new V3(0, 0, -1);
                surfaces.Add(new SurfacePatch("back wall", Surface.Wall, new V3(x0, 0f, back), new V3(x1 - x0, 0, 0), new V3(0, HouseGeometry.BoundaryHeight, 0), outward));
                float lz = HouseGeometry.BackWallZ - ledge / 2f;
                surfaces.Add(new SurfacePatch("back wall top", Surface.Railing, new V3(x0, top, lz), new V3(x1 - x0, 0, 0), new V3(0, 0, ledge), outward, ledge: true));
                solids.Add(new Box3(x0, 0f, back, x1, top, HouseGeometry.BackWallZ + HouseGeometry.BoundaryThickness / 2f));
            }

            // The gate: its pillars, and its leaves across the opening (open while the avatar walks out).
            solids.Add(new Box3(gateL, 0f, back - 0.03f, gateL + 0.22f, 2.2f, HouseGeometry.BackWallZ + 0.2f));
            solids.Add(new Box3(gateR - 0.22f, 0f, back - 0.03f, gateR, 2.2f, HouseGeometry.BackWallZ + 0.2f));
            solids.Add(HouseGeometry.GateLeaves);

            // Balcony: its rail top is a ledge; the balcony itself is solid.
            string balcony = design.ModuleAt("balcony")?.module ?? string.Empty;
            var front = new V3(0, 0, 1);
            switch (balcony)
            {
                case "balcony_iron_rail":
                    surfaces.Add(new SurfacePatch("balcony rail", Surface.Railing, new V3(-1f, 4.3f, 0.84f), new V3(2f, 0, 0), new V3(0, 0, 0.06f), front, ledge: true));
                    BalconySolids(solids, 4.3f, 0.84f, 0.9f, 0.1f);
                    break;
                case "balcony_jharokha":
                    surfaces.Add(new SurfacePatch("jharokha parapet", Surface.Railing, new V3(-1f, 4.1f, 0.75f), new V3(2f, 0, 0), new V3(0, 0, 0.15f), front, ledge: true));
                    BalconySolids(solids, 4.1f, 0.75f, 0.92f, 0.17f);
                    solids.Add(new Box3(-1.05f, 5.45f, 0f, 1.05f, 5.95f, 1.0f));
                    break;
                case "balcony_wood_lattice":
                    surfaces.Add(new SurfacePatch("balcony rail", Surface.Railing, new V3(-1f, 4.2f, 0.83f), new V3(2f, 0, 0), new V3(0, 0, 0.07f), front, ledge: true));
                    BalconySolids(solids, 4.2f, 0.83f, 0.9f, 0.1f);
                    break;
                default:
                    surfaces.Add(new SurfacePatch("window railing", Surface.Railing, new V3(-0.66f, 4.25f, 0.04f), new V3(1.32f, 0, 0), new V3(0, 0, 0.04f), front, ledge: true));
                    solids.Add(new Box3(-0.66f, 3.3f, 0f, 0.66f, 4.25f, 0.08f));
                    break;
            }

            // Roof: a flat deck and parapet, or slopes too steep to stand on.
            string roof = design.ModuleAt("roof")?.module ?? string.Empty;
            const float eaves = HouseGeometry.Eaves, d = HouseGeometry.Depth;
            if (roof == "roof_tiled_slope")
            {
                surfaces.Add(new SurfacePatch("roof slope", Surface.Roof, new V3(-4f, eaves + 0.08f, 0.4f), new V3(8f, 0, 0), new V3(0, 1.62f, -3.4f), Up));
                surfaces.Add(new SurfacePatch("roof slope", Surface.Roof, new V3(-4f, eaves + 1.7f, -3f), new V3(8f, 0, 0), new V3(0, -1.62f, -3.4f), Up));
                solids.Add(new Box3(-4f, eaves, -d - 0.4f, 4f, eaves + 1.5f, 0f));
            }
            else
            {
                bool terrace = roof == "roof_terrace_canopy";
                float wall = terrace ? 0.45f : 0.7f, railTop = terrace ? 0.97f : 0.78f;
                surfaces.Add(new SurfacePatch("roof terrace", Surface.Roof, new V3(-hw + 0.15f, eaves, -d + 0.15f), new V3(2 * hw - 0.3f, 0, 0), new V3(0, 0, d - 0.3f), Up));
                surfaces.Add(new SurfacePatch("roof parapet", Surface.Railing, new V3(-hw, eaves + railTop, -0.15f), new V3(2 * hw, 0, 0), new V3(0, 0, 0.15f), front, ledge: true));
                solids.Add(new Box3(-hw - 0.05f, eaves, -0.2f, hw + 0.05f, eaves + railTop, 0.05f));
                solids.Add(new Box3(-hw, eaves, -d, -hw + 0.15f, eaves + wall, 0f));
                solids.Add(new Box3(hw - 0.15f, eaves, -d, hw, eaves + wall, 0f));
                solids.Add(new Box3(-hw, eaves, -d, hw, eaves + wall, -d + 0.15f));
                if (terrace)
                {
                    solids.Add(new Box3(1.65f, eaves, -3.25f, 3.35f, eaves + 2.2f, -1.55f));
                }
            }

            var door = new Box3(-0.6f, 0f, 0.5f, 0.6f, 2.4f, 1.4f);
            var gate = new Box3(gateL + 0.2f, 0f, back - 1.0f, gateR - 0.2f, 2.2f, back);
            return new PlacementContext(design, tier, surfaces, solids, door, gate);
        }

        /// <summary>A balcony's slab and its front and side rails (the air above the slab stays free for a celebration spot).</summary>
        private static void BalconySolids(List<Box3> solids, float railTop, float frontZ, float outer, float side)
        {
            const float floor = 3.3f;
            solids.Add(new Box3(-1.05f, 2.97f, 0f, 1.05f, floor, outer));
            solids.Add(new Box3(-1.05f, floor, frontZ, 1.05f, railTop, outer));
            solids.Add(new Box3(-1.05f, floor, 0f, -1.05f + side, railTop, frontZ));
            solids.Add(new Box3(1.05f - side, floor, 0f, 1.05f, railTop, frontZ));
        }

        /// <summary>The nearest surface a ray hits, or null.</summary>
        public static SurfaceHit? Raycast(PlacementContext ctx, V3 origin, V3 direction)
        {
            V3 dir = direction.Normalized;
            SurfaceHit? best = null;
            foreach (SurfacePatch p in ctx.Surfaces)
            {
                float denom = V3.Dot(dir, p.Normal);
                if (Math.Abs(denom) < 1e-6f)
                {
                    continue;
                }

                float t = V3.Dot(p.Origin - origin, p.Normal) / denom;
                if (t <= 0f || (best.HasValue && t >= best.Value.Distance))
                {
                    continue;
                }

                V3 hit = origin + dir * t;
                if (p.Holds(hit, 1e-3f))
                {
                    best = new SurfaceHit(p, hit, t);
                }
            }

            return best;
        }

        /// <summary>
        /// The pose a free or rangoli item takes at a hit, snapped to the grid and to 15 degree turns, and its check. A hung
        /// item hangs from the hit point and faces away from the wall; <paramref name="yaw"/> is ignored for it.
        /// </summary>
        public static PlacementResult PlaceAt(PlacementContext ctx, string itemId, SurfaceHit hit, float yaw, float scale, int variant, int uid = 0)
        {
            DecorInfo info = HomeCatalog.Decoration(itemId);
            if (info == null || info.Mode == PlacementMode.Path)
            {
                return Refuse(null, PlacementProblem.UnknownItem, info);
            }

            float s = Math.Max(HomeCatalog.MinScale, Math.Min(info.MaxScale, scale));
            var pose = new PlacedDecor { uid = uid, item = itemId, variant = variant, scale = s, surface = hit.Patch.Kind };
            V3 at = SnapOnPatch(hit.Patch, hit.Point);
            if ((hit.Patch.Kind & info.Surfaces) == 0)
            {
                pose.x = at.X;
                pose.y = at.Y;
                pose.z = at.Z;
                return Refuse(pose, PlacementProblem.WrongSurface, info);
            }

            if (Hangs(info))
            {
                V3 o = hit.Patch.Outward;
                if (hit.Patch.Ledge)
                {
                    // Hang from the ledge's outer edge, clear of the rail or wall below it.
                    at = at + o * (hit.Patch.V.Length / 2f);
                }

                pose.yaw = HouseDesign.NormaliseYaw((float)(Math.Atan2(o.X, o.Z) * 180.0 / Math.PI));
                V3 basePoint = at + o * (info.Depth * s / 2f) - new V3(0f, info.Height * s, 0f);
                pose.x = basePoint.X;
                pose.y = basePoint.Y;
                pose.z = basePoint.Z;
            }
            else
            {
                pose.yaw = HouseDesign.NormaliseYaw((float)Math.Round(yaw / YawStep) * YawStep);
                pose.x = at.X;
                pose.y = at.Y;
                pose.z = at.Z;
            }

            return Check(ctx, pose);
        }

        /// <summary>A path item from one hit to another, and its check.</summary>
        public static PlacementResult PathBetween(PlacementContext ctx, string itemId, SurfaceHit a, SurfaceHit b, float sag, int variant, int uid = 0)
        {
            V3 pa = SnapOnPatch(a.Patch, a.Point), pb = SnapOnPatch(b.Patch, b.Point);
            var pose = new PlacedDecor
            {
                uid = uid, item = itemId, variant = variant, surface = a.Patch.Kind, x = pa.X, y = pa.Y, z = pa.Z, ex = pb.X, ey = pb.Y, ez = pb.Z,
                sag = Math.Max(0f, Math.Min(1f, sag)),
            };
            return Check(ctx, pose);
        }

        /// <summary>Checks a pose against every rule; the item with the same uid is ignored (moving an item over its old spot).</summary>
        public static PlacementResult Check(PlacementContext ctx, PlacedDecor pose)
        {
            DecorInfo info = HomeCatalog.Decoration(pose?.item);
            if (info == null)
            {
                return Refuse(pose, PlacementProblem.UnknownItem, null);
            }

            PlacementProblem problem = info.Mode == PlacementMode.Path ? CheckPath(ctx, info, pose) : CheckPlaced(ctx, info, pose);
            if (problem == PlacementProblem.None)
            {
                problem = CheckBudget(ctx, info, pose);
            }

            return problem == PlacementProblem.None ? new PlacementResult(pose, problem, string.Empty) : Refuse(pose, problem, info);
        }

        /// <summary>
        /// Every placed item that breaks a rule now, such as pots left on a balcony rail after the balcony was removed.
        /// Decoration mode shows them red with their reason, so the player can move or remove them.
        /// </summary>
        public static List<PlacementResult> Audit(PlacementContext ctx)
        {
            var failing = new List<PlacementResult>();
            foreach (PlacedDecor d in ctx.Design.decorations)
            {
                PlacementResult r = Check(ctx, d);
                if (!r.Ok)
                {
                    failing.Add(r);
                }
            }

            return failing;
        }

        /// <summary>Units the design's decorations use, without the item <paramref name="exceptUid"/>.</summary>
        public static int UnitsUsed(HouseDesign design, int exceptUid = 0, DecorCategory? category = null)
        {
            int units = 0;
            foreach (PlacedDecor d in design.decorations)
            {
                DecorInfo info = HomeCatalog.Decoration(d.item);
                if (info != null && d.uid != exceptUid && (category == null || info.Category == category))
                {
                    units += DecorBudget.CostOf(info, PathLength(d));
                }
            }

            return units;
        }

        public static string Reason(PlacementProblem problem, DecorInfo info)
        {
            string name = info != null ? Lower(info.Name) : "decoration";
            switch (problem)
            {
                case PlacementProblem.None:
                    return string.Empty;
                case PlacementProblem.UnknownItem:
                    return "That decoration isn't available.";
                case PlacementProblem.NoSurface:
                    return $"There's nothing here to put the {name} on.";
                case PlacementProblem.WrongSurface:
                    return $"The {name} can only go {SurfaceWords(info.Surfaces, Hangs(info) || info.Mode == PlacementMode.Path)}.";
                case PlacementProblem.TooSteep:
                    return $"This slope is too steep for the {name}.";
                case PlacementProblem.OverEdge:
                    return $"The {name} would hang over the edge here.";
                case PlacementProblem.HitsHouse:
                    return $"The {name} would run into a wall or step here.";
                case PlacementProblem.Overlaps:
                    return "Another decoration is already here.";
                case PlacementProblem.BlocksDoor:
                    return "Keep the doorway clear so people can come and go.";
                case PlacementProblem.BlocksGate:
                    return "Keep the gate clear so people can come and go.";
                case PlacementProblem.TooLow:
                    return $"Hang the {name} a little higher.";
                case PlacementProblem.PathTooShort:
                    return $"Stretch the {name} a little further.";
                case PlacementProblem.PathTooLong:
                    return $"That's too far. The {name} can stretch up to {DecorBudget.MaxPathLength:0} metres.";
                case PlacementProblem.PathBlocked:
                    return $"The {name} would pass through a wall there.";
                case PlacementProblem.OverBudget:
                    return "Your house has as many decorations as this phone can show. Remove one to add another.";
                case PlacementProblem.CategoryFull:
                    return $"There are as many {CategoryWords(info.Category)} as this phone can show. Remove one to add another.";
                default:
                    return "That can't go here.";
            }
        }

        // ---- Rules ----

        private static PlacementProblem CheckPlaced(PlacementContext ctx, DecorInfo info, PlacedDecor pose)
        {
            float s = pose.scale, w = info.Width * s, dpt = info.Depth * s, h = info.Height * s;
            bool hangs = Hangs(info);
            V3 basePoint = new V3(pose.x, pose.y, pose.z);
            V3 forward = Forward(pose.yaw);
            V3 support = hangs ? basePoint + new V3(0f, h, 0f) - forward * (dpt / 2f) : basePoint;

            SurfacePatch patch = Find(ctx, support, pose.surface);
            if (patch == null)
            {
                SurfacePatch any = Find(ctx, support, null);
                if (any == null)
                {
                    return PlacementProblem.NoSurface;
                }

                patch = any;
            }

            if ((patch.Kind & info.Surfaces) == 0)
            {
                return PlacementProblem.WrongSurface;
            }

            if (hangs)
            {
                if (V3.Dot(forward, patch.Outward) < (float)Math.Cos(MaxSupportTilt * Math.PI / 180.0))
                {
                    return PlacementProblem.TooSteep;
                }
            }
            else if (patch.Tilt > MaxSupportTilt)
            {
                return PlacementProblem.TooSteep;
            }

            if (hangs && basePoint.Y < MinHangHeight)
            {
                return PlacementProblem.TooLow;
            }

            if (info.Mode != PlacementMode.Decal)
            {
                Box3 box = Bounds(basePoint, forward, w, dpt, h, 0.012f);
                foreach (Box3 solid in ctx.Solids)
                {
                    if (box.Overlaps(solid))
                    {
                        return PlacementProblem.HitsHouse;
                    }
                }

                if (box.Overlaps(ctx.DoorClearance))
                {
                    return PlacementProblem.BlocksDoor;
                }

                if (box.Overlaps(ctx.GateClearance))
                {
                    return PlacementProblem.BlocksGate;
                }
            }

            if (!hangs && !FitsOn(patch, basePoint, forward, w, dpt))
            {
                return PlacementProblem.OverEdge;
            }

            foreach (PlacedDecor other in ctx.Design.decorations)
            {
                DecorInfo oi = HomeCatalog.Decoration(other.item);
                if (other.uid == pose.uid || oi == null || oi.Mode == PlacementMode.Path)
                {
                    continue;
                }

                // Items may stand on rangoli; rangoli may not overlap rangoli.
                bool decal = info.Mode == PlacementMode.Decal, otherDecal = oi.Mode == PlacementMode.Decal;
                if (decal != otherDecal)
                {
                    continue;
                }

                float os = other.scale;
                if (!decal && (other.y >= pose.y + h - 1e-3f || pose.y >= other.y + oi.Height * os - 1e-3f))
                {
                    continue;
                }

                if (decal && Math.Abs(other.y - pose.y) > 0.05f)
                {
                    continue;
                }

                if (FootprintsOverlap(basePoint, forward, w, dpt, new V3(other.x, other.y, other.z), Forward(other.yaw), oi.Width * os, oi.Depth * os))
                {
                    return PlacementProblem.Overlaps;
                }
            }

            return PlacementProblem.None;
        }

        private static PlacementProblem CheckPath(PlacementContext ctx, DecorInfo info, PlacedDecor pose)
        {
            var a = new V3(pose.x, pose.y, pose.z);
            var b = new V3(pose.ex, pose.ey, pose.ez);
            foreach (V3 end in new[] { a, b })
            {
                SurfacePatch patch = Find(ctx, end, null, preferred: info.Surfaces);
                if (patch == null)
                {
                    return PlacementProblem.NoSurface;
                }

                if ((patch.Kind & info.Surfaces) == 0)
                {
                    return PlacementProblem.WrongSurface;
                }
            }

            float length = (b - a).Length;
            if (length < DecorBudget.MinPathLength)
            {
                return PlacementProblem.PathTooShort;
            }

            if (length > DecorBudget.MaxPathLength + 1e-4f)
            {
                return PlacementProblem.PathTooLong;
            }

            // Sample the strand (and, for curtains and strands, the bottom of the drops) away from its two hooks.
            const int samples = 24;
            for (int i = 1; i < samples; i++)
            {
                float t = i / (float)samples;
                V3 p = DecorGenerator.PathPoint(a, b, pose.sag, t);
                if ((p - a).Length < 0.08f || (p - b).Length < 0.08f)
                {
                    continue;
                }

                foreach (V3 q in new[] { p, p - new V3(0f, info.Height, 0f) })
                {
                    foreach (Box3 solid in ctx.Solids)
                    {
                        if (Inside(solid, q, 0.02f))
                        {
                            return PlacementProblem.PathBlocked;
                        }
                    }

                    if (Inside(ctx.DoorClearance, q, 0f))
                    {
                        return PlacementProblem.BlocksDoor;
                    }

                    if (Inside(ctx.GateClearance, q, 0f))
                    {
                        return PlacementProblem.BlocksGate;
                    }
                }
            }

            return PlacementProblem.None;
        }

        private static PlacementProblem CheckBudget(PlacementContext ctx, DecorInfo info, PlacedDecor pose)
        {
            int cost = DecorBudget.CostOf(info, PathLength(pose));
            if (UnitsUsed(ctx.Design, pose.uid) + cost > DecorBudget.Units[ctx.Tier])
            {
                return PlacementProblem.OverBudget;
            }

            if (UnitsUsed(ctx.Design, pose.uid, info.Category) + cost > CategoryCap(ctx.Tier, info.Category))
            {
                return PlacementProblem.CategoryFull;
            }

            return PlacementProblem.None;
        }

        // ---- Geometry ----

        private static SurfacePatch Find(PlacementContext ctx, V3 p, Surface? kind, Surface preferred = Surface.None)
        {
            SurfacePatch found = null;
            foreach (SurfacePatch patch in ctx.Surfaces)
            {
                if ((kind == null || patch.Kind == kind) && patch.Holds(p))
                {
                    if ((patch.Kind & preferred) != 0)
                    {
                        return patch;
                    }

                    found ??= patch;
                }
            }

            return found;
        }

        private static bool FitsOn(SurfacePatch patch, V3 centre, V3 forward, float w, float d)
        {
            if (patch.Ledge)
            {
                // On a rail or wall top, the centre must be on it and the item must not run past its ends.
                patch.Coordinates(centre, out float a, out float b);
                float along = Math.Abs(V3.Dot(V3.Cross(forward, Up), patch.U.Normalized)) * w / 2f + Math.Abs(V3.Dot(forward, patch.U.Normalized)) * d / 2f;
                float margin = along / patch.U.Length;
                return b >= -1e-3f && b <= 1.001f && a - margin >= -1e-3f && a + margin <= 1.001f;
            }

            foreach (V3 corner in Corners(centre, forward, w, d))
            {
                V3 onPlane = corner - patch.Normal * V3.Dot(corner - patch.Origin, patch.Normal);
                if (!patch.Holds(onPlane, 0.02f, 0.005f))
                {
                    return false;
                }
            }

            return true;
        }

        private static V3[] Corners(V3 c, V3 forward, float w, float d)
        {
            V3 right = V3.Cross(Up, forward).Normalized * (w / 2f);
            V3 f = forward * (d / 2f);
            return new[] { c + right + f, c - right + f, c - right - f, c + right - f };
        }

        private static Box3 Bounds(V3 basePoint, V3 forward, float w, float d, float h, float shrink)
        {
            float ex = Math.Abs(forward.Z) * w / 2f + Math.Abs(forward.X) * d / 2f - shrink;
            float ez = Math.Abs(forward.X) * w / 2f + Math.Abs(forward.Z) * d / 2f - shrink;
            ex = Math.Max(ex, 0.001f);
            ez = Math.Max(ez, 0.001f);
            return new Box3(basePoint.X - ex, basePoint.Y + shrink, basePoint.Z - ez, basePoint.X + ex, basePoint.Y + Math.Max(h - shrink, shrink + 0.001f), basePoint.Z + ez);
        }

        /// <summary>Separating-axis test of two footprints (rotated rectangles) on the horizontal plane.</summary>
        private static bool FootprintsOverlap(V3 ca, V3 fa, float wa, float da, V3 cb, V3 fb, float wb, float db)
        {
            V3[] a = Corners(ca, fa, wa, da), b = Corners(cb, fb, wb, db);
            foreach (V3 axis in new[] { fa, V3.Cross(Up, fa), fb, V3.Cross(Up, fb) })
            {
                float minA = float.MaxValue, maxA = float.MinValue, minB = float.MaxValue, maxB = float.MinValue;
                foreach (V3 p in a)
                {
                    float v = p.X * axis.X + p.Z * axis.Z;
                    minA = Math.Min(minA, v);
                    maxA = Math.Max(maxA, v);
                }

                foreach (V3 p in b)
                {
                    float v = p.X * axis.X + p.Z * axis.Z;
                    minB = Math.Min(minB, v);
                    maxB = Math.Max(maxB, v);
                }

                if (maxA <= minB + 1e-3f || maxB <= minA + 1e-3f)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Inside(Box3 b, V3 p, float shrink) =>
            p.X > b.Min.X + shrink && p.X < b.Max.X - shrink && p.Y > b.Min.Y + shrink && p.Y < b.Max.Y - shrink && p.Z > b.Min.Z + shrink && p.Z < b.Max.Z - shrink;

        private static V3 Forward(float yawDegrees)
        {
            double r = yawDegrees * Math.PI / 180.0;
            return new V3((float)Math.Sin(r), 0f, (float)Math.Cos(r));
        }

        /// <summary>
        /// Snaps a point to the 5 cm world grid in the patch's plane (the axis the patch faces is solved from its plane), keeps
        /// it on the patch, and puts it on a ledge's centre line.
        /// </summary>
        private static V3 SnapOnPatch(SurfacePatch patch, V3 p)
        {
            V3 n = patch.Normal;
            float ax = Math.Abs(n.X), ay = Math.Abs(n.Y), az = Math.Abs(n.Z);
            float x = Round(p.X), y = Round(p.Y), z = Round(p.Z);
            float k = V3.Dot(patch.Origin, n);
            if (ay >= ax && ay >= az)
            {
                y = (k - n.X * x - n.Z * z) / n.Y;
            }
            else if (ax >= az)
            {
                x = (k - n.Y * y - n.Z * z) / n.X;
            }
            else
            {
                z = (k - n.X * x - n.Y * y) / n.Z;
            }

            patch.Coordinates(new V3(x, y, z), out float a, out float b);
            a = Math.Max(0f, Math.Min(1f, a));
            b = patch.Ledge ? 0.5f : Math.Max(0f, Math.Min(1f, b));
            return patch.Origin + patch.U * a + patch.V * b;
        }

        private static float Round(float v) => (float)Math.Round(v / Grid) * Grid;

        private static float PathLength(PlacedDecor d)
        {
            DecorInfo info = HomeCatalog.Decoration(d.item);
            return info != null && info.Mode == PlacementMode.Path ? (new V3(d.ex, d.ey, d.ez) - new V3(d.x, d.y, d.z)).Length : 0f;
        }

        private static PlacementResult Refuse(PlacedDecor pose, PlacementProblem problem, DecorInfo info) => new PlacementResult(pose, problem, Reason(problem, info));

        private static string Lower(string name) => name.Length > 1 && char.IsUpper(name[0]) && !char.IsUpper(name[1]) ? char.ToLowerInvariant(name[0]) + name.Substring(1) : name;

        private static string SurfaceWords(Surface s, bool hanging)
        {
            var words = new List<string>();
            if ((s & Surface.Ground) != 0)
            {
                words.Add("on the ground");
            }

            if ((s & Surface.Step) != 0)
            {
                words.Add("on the step");
            }

            if ((s & Surface.Wall) != 0)
            {
                words.Add(hanging ? "on a wall" : "against a wall");
            }

            if ((s & Surface.Railing) != 0)
            {
                words.Add(hanging ? "from a railing" : "on a railing or wall top");
            }

            if ((s & Surface.Roof) != 0)
            {
                words.Add("on the roof");
            }

            return words.Count switch
            {
                0 => "nowhere",
                1 => words[0],
                _ => string.Join(", ", words.GetRange(0, words.Count - 1)) + " or " + words[words.Count - 1],
            };
        }

        private static string CategoryWords(DecorCategory c) => c switch
        {
            DecorCategory.Lamps => "lamps",
            DecorCategory.Lights => "lights",
            DecorCategory.Garlands => "garlands",
            DecorCategory.Plants => "plants",
            DecorCategory.Furniture => "pieces of furniture",
            _ => "rangoli",
        };
    }
}

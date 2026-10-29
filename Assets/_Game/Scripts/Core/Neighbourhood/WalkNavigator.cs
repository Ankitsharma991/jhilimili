using System;
using System.Collections.Generic;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Home;

namespace UtsavNights.Core.Neighbourhood
{
    /// <summary>A flat rectangle the avatar may walk on, at height <see cref="Y"/> (the ground, a roof deck, a balcony floor).</summary>
    public readonly struct WalkArea
    {
        public WalkArea(string id, float x0, float z0, float x1, float z1, float y = 0f)
        {
            Id = id;
            X0 = Math.Min(x0, x1);
            Z0 = Math.Min(z0, z1);
            X1 = Math.Max(x0, x1);
            Z1 = Math.Max(z0, z1);
            Y = y;
        }

        public string Id { get; }

        public float X0 { get; }

        public float Z0 { get; }

        public float X1 { get; }

        public float Z1 { get; }

        public float Y { get; }

        public bool Contains(V3 p) => Math.Abs(p.Y - Y) < 0.5f && p.X >= X0 - 1e-4f && p.X <= X1 + 1e-4f && p.Z >= Z0 - 1e-4f && p.Z <= Z1 + 1e-4f;

        public V3 Clamp(V3 p) => new V3(Math.Max(X0, Math.Min(X1, p.X)), Y, Math.Max(Z0, Math.Min(Z1, p.Z)));
    }

    /// <summary>One step of a route: a point to walk to, or (after stairs) a point to appear at behind a short fade.</summary>
    public readonly struct Waypoint
    {
        public Waypoint(V3 position, bool fade)
        {
            Position = position;
            Fade = fade;
        }

        public V3 Position { get; }

        public bool Fade { get; }
    }

    /// <summary>
    /// Walking in the settlement (Phase 8 step 3, PRD 10 "tap-to-move"). Walkable areas are flat rectangles: the home
    /// courtyard, the opening of its gate, the street and open space (one rectangle), the vacant plot, and the home
    /// rooftop and balcony when the house has them (reached by stairs, a fade). A straight line that stays inside the areas
    /// is walked directly; anything else goes along the layout's walk graph (A*), whose lint guarantees no edge crosses a
    /// wall. Every area keeps the avatar's radius clear of the walls around it.
    /// </summary>
    public sealed class WalkNavigator
    {
        public const float Radius = 0.3f;
        public const float Step = 0.2f;

        private readonly SettlementLayout _layout;
        private readonly Dictionary<string, List<(string to, bool stairs)>> _links = new Dictionary<string, List<(string, bool)>>();

        public WalkNavigator(SettlementLayout layout, HouseDesign home)
        {
            _layout = layout;
            Areas = AreasFor(layout, home);
            foreach (WalkEdge e in layout.edges)
            {
                Link(e.a, e.b, e.kind == EdgeKinds.Stairs);
                Link(e.b, e.a, e.kind == EdgeKinds.Stairs);
            }
        }

        public List<WalkArea> Areas { get; }

        public static List<WalkArea> AreasFor(SettlementLayout layout, HouseDesign home)
        {
            const float r = Radius;
            float inner = HouseGeometry.BoundaryCentre - HouseGeometry.BoundaryThickness / 2f - r;
            float back = HouseGeometry.WorldFrontZ + HouseGeometry.BackWallZ - HouseGeometry.BoundaryThickness / 2f - r;
            var areas = new List<WalkArea>
            {
                // The courtyard, clear of the step and plinth at the house front and of the walls.
                new WalkArea("courtyard", -inner, HouseGeometry.WorldFrontZ + 0.5f + r, inner, back),
                // The gate opening, so the courtyard and the street join.
                new WalkArea("gate", HouseGeometry.GateX - 0.39f + r * 0.5f, back - 0.1f, HouseGeometry.GateX + 0.39f - r * 0.5f, layout.street.z0 + r + 0.1f),
                // The street and the open space beyond it.
                new WalkArea("public", layout.street.x0 + r, layout.street.z0 + r, layout.street.x1 - r, layout.open.z1 - r),
            };

            foreach (LotDef lot in layout.lots)
            {
                if (lot.vacant)
                {
                    V3 a = lot.ToWorld(new V3(-HouseGeometry.HalfWidth + r, 0f, -HouseGeometry.Depth + r));
                    areas.Add(new WalkArea("vacant", a.X, a.Z, lot.x + HouseGeometry.HalfWidth - r, layout.street.z0 + r + 0.1f));
                }
            }

            HouseDesign design = home ?? HouseDesign.Default();
            if (HomeTags(design).Contains(SpotTags.HasRooftop))
            {
                float z = HouseGeometry.WorldFrontZ;
                areas.Add(new WalkArea("rooftop", -HouseGeometry.HalfWidth + 0.15f + r, z - HouseGeometry.Depth + 0.15f + r,
                    HouseGeometry.HalfWidth - 0.15f - r, z - 0.2f - r, HouseGeometry.Eaves));
            }

            if (HomeTags(design).Contains(SpotTags.HasBalcony))
            {
                float z = HouseGeometry.WorldFrontZ;
                areas.Add(new WalkArea("balcony", -0.95f + r, z + 0.05f + r * 0.5f, 0.95f - r, z + 0.75f - r * 0.5f, HouseGeometry.UpperFloor));
            }

            return areas;
        }

        /// <summary>The home lot's availability tags from the player's house (PRD 10).</summary>
        public static HashSet<string> HomeTags(HouseDesign home)
        {
            var tags = new HashSet<string> { SpotTags.HasCourtyard };
            string roof = home?.ModuleAt("roof")?.module ?? string.Empty;
            if (roof == "roof_flat_parapet" || roof == "roof_terrace_canopy")
            {
                tags.Add(SpotTags.HasRooftop);
            }

            if (!string.IsNullOrEmpty(home?.ModuleAt("balcony")?.module))
            {
                tags.Add(SpotTags.HasBalcony);
            }

            return tags;
        }

        public bool IsWalkable(V3 p)
        {
            foreach (WalkArea a in Areas)
            {
                if (a.Contains(p))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The walkable point nearest to p on p's level (a tap on a wall lands at its foot).</summary>
        public V3 Nearest(V3 p)
        {
            V3 best = p;
            float bestDistance = float.MaxValue;
            foreach (WalkArea a in Areas)
            {
                V3 c = a.Clamp(p);
                float d = (c - p).Length + Math.Abs(a.Y - p.Y) * 4f;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = c;
                }
            }

            return best;
        }

        /// <summary>True when every point of the straight line from a to b is walkable (same level).</summary>
        public bool Straight(V3 a, V3 b)
        {
            float length = (b - a).Length;
            int steps = Math.Max(1, (int)Math.Ceiling(length / Step));
            for (int i = 0; i <= steps; i++)
            {
                if (!IsWalkable(V3.Lerp(a, b, i / (float)steps)))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// The route from a walkable point to a target (moved to the nearest walkable point first). Empty when already there;
        /// null when no route exists.
        /// </summary>
        public List<Waypoint> Route(V3 from, V3 target)
        {
            V3 to = IsWalkable(target) ? target : Nearest(target);
            var route = new List<Waypoint>();
            if ((to - from).Length < 0.05f)
            {
                return route;
            }

            if (Straight(from, to))
            {
                route.Add(new Waypoint(to, false));
                return route;
            }

            List<string> starts = NodesSeeing(from), goals = NodesSeeing(to);
            if (starts.Count == 0 || goals.Count == 0)
            {
                return null;
            }

            List<(string node, bool stairs)> path = AStar(from, starts, to, goals);
            if (path == null)
            {
                return null;
            }

            foreach ((string node, bool stairs) in path)
            {
                route.Add(new Waypoint(_layout.Node(node).Position, stairs));
            }

            route.Add(new Waypoint(to, false));
            return route;
        }

        /// <summary>The route to a spot's walk node (the map's fast travel uses the same nodes).</summary>
        public List<Waypoint> RouteToSpot(V3 from, string spotId)
        {
            WalkNode n = _layout.Node(spotId);
            return n == null ? null : Route(from, n.Position);
        }

        /// <summary>Joystick movement: p moved by a step, sliding along an area's edge rather than leaving it.</summary>
        public V3 Slide(V3 p, V3 step)
        {
            V3 full = p + step;
            if (IsWalkable(full))
            {
                return full;
            }

            V3 alongX = p + new V3(step.X, 0f, 0f), alongZ = p + new V3(0f, 0f, step.Z);
            if (IsWalkable(alongX))
            {
                return alongX;
            }

            return IsWalkable(alongZ) ? alongZ : p;
        }

        private List<string> NodesSeeing(V3 p)
        {
            var list = new List<string>();
            foreach (WalkNode n in _layout.nodes)
            {
                if (Straight(p, n.Position))
                {
                    list.Add(n.id);
                }
            }

            return list;
        }

        private List<(string, bool)> AStar(V3 from, List<string> starts, V3 to, List<string> goals)
        {
            var cost = new Dictionary<string, float>();
            var came = new Dictionary<string, (string prev, bool stairs)>();
            var open = new List<string>();
            foreach (string s in starts)
            {
                cost[s] = (_layout.Node(s).Position - from).Length;
                came[s] = (null, false);
                open.Add(s);
            }

            var goalSet = new HashSet<string>(goals);
            var closed = new HashSet<string>();
            while (open.Count > 0)
            {
                string best = open[0];
                float bestF = float.MaxValue;
                foreach (string n in open)
                {
                    float f = cost[n] + (_layout.Node(n).Position - to).Length;
                    if (f < bestF)
                    {
                        bestF = f;
                        best = n;
                    }
                }

                if (goalSet.Contains(best))
                {
                    var path = new List<(string, bool)>();
                    for (string at = best; at != null; at = came[at].prev)
                    {
                        path.Add((at, came[at].stairs));
                    }

                    path.Reverse();
                    return path;
                }

                open.Remove(best);
                closed.Add(best);
                if (!_links.TryGetValue(best, out List<(string to, bool stairs)> next))
                {
                    continue;
                }

                foreach ((string n, bool stairs) in next)
                {
                    if (closed.Contains(n) || _layout.Node(n) == null)
                    {
                        continue;
                    }

                    // Stairs cost a little more than their length, so a walk is preferred when both reach.
                    float step = (_layout.Node(n).Position - _layout.Node(best).Position).Length + (stairs ? 3f : 0f);
                    float c = cost[best] + step;
                    if (!cost.TryGetValue(n, out float old) || c < old)
                    {
                        cost[n] = c;
                        came[n] = (best, stairs);
                        if (!open.Contains(n))
                        {
                            open.Add(n);
                        }
                    }
                }
            }

            return null;
        }

        private void Link(string a, string b, bool stairs)
        {
            if (!_links.TryGetValue(a, out List<(string, bool)> list))
            {
                _links[a] = list = new List<(string, bool)>();
            }

            list.Add((b, stairs));
        }
    }

    /// <summary>
    /// Follows a route at walking pace (Phase 8 step 3): the position, the heading, and whether it is walking. A stairs
    /// waypoint is reached at once (the view fades); <see cref="Faded"/> counts them so the view knows when to fade.
    /// </summary>
    public sealed class WalkAgent
    {
        public const float Speed = 1.4f;
        public const float TurnDegreesPerSecond = 540f;

        private readonly List<Waypoint> _route = new List<Waypoint>();

        public WalkAgent(V3 position, float yaw = 0f)
        {
            Position = position;
            Yaw = yaw;
        }

        public V3 Position { get; private set; }

        /// <summary>Degrees; 0 faces +z.</summary>
        public float Yaw { get; private set; }

        public bool Walking => _route.Count > 0;

        public int Faded { get; private set; }

        public V3 Destination => _route.Count > 0 ? _route[_route.Count - 1].Position : Position;

        public void Follow(List<Waypoint> route)
        {
            _route.Clear();
            if (route != null)
            {
                _route.AddRange(route);
            }
        }

        public void Stop() => _route.Clear();

        /// <summary>Jumps to a point (fast travel), stopping any walk.</summary>
        public void Place(V3 position, float yaw)
        {
            _route.Clear();
            Position = position;
            Yaw = yaw;
        }

        /// <summary>Moves directly (joystick), turning to face the movement.</summary>
        public void Nudge(V3 to)
        {
            V3 d = to - Position;
            if (d.Length > 1e-4f)
            {
                Yaw = (float)(Math.Atan2(d.X, d.Z) * 180.0 / Math.PI);
            }

            _route.Clear();
            Position = to;
        }

        public void Tick(float deltaSeconds)
        {
            float budget = Speed * deltaSeconds;
            while (budget > 0f && _route.Count > 0)
            {
                Waypoint next = _route[0];
                if (next.Fade)
                {
                    Position = next.Position;
                    Faded++;
                    _route.RemoveAt(0);
                    continue;
                }

                V3 d = next.Position - Position;
                float distance = d.Length;
                if (distance > 1e-4f)
                {
                    float target = (float)(Math.Atan2(d.X, d.Z) * 180.0 / Math.PI);
                    float turn = DeltaAngle(Yaw, target);
                    float maxTurn = TurnDegreesPerSecond * deltaSeconds;
                    Yaw += Math.Max(-maxTurn, Math.Min(maxTurn, turn));
                }

                if (distance <= budget)
                {
                    Position = next.Position;
                    budget -= distance;
                    _route.RemoveAt(0);
                }
                else
                {
                    Position = Position + d * (budget / distance);
                    budget = 0f;
                }
            }
        }

        private static float DeltaAngle(float from, float to)
        {
            float d = (to - from) % 360f;
            if (d > 180f)
            {
                d -= 360f;
            }
            else if (d < -180f)
            {
                d += 360f;
            }

            return d;
        }
    }
}

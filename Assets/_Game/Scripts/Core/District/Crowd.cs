using System;
using System.Collections.Generic;

namespace UtsavNights.Core.District
{
    /// <summary>
    /// Where people can walk in the district (Phase 12 step 4): nodes where roads cross and every 25 m along them, joined
    /// along each road, with a ring round the square so nobody walks through the clock tower. Each edge knows how far
    /// from its middle the footpath is (the main roads' pavements, the middle of the lanes, the promenade).
    /// </summary>
    public sealed class WalkGraph
    {
        public const float Spacing = 25f;

        /// <summary>A road ending at another's edge still meets it (the lanes reach the promenade this way).</summary>
        public const float Reach = 8f;

        public readonly List<(float x, float z)> Nodes = new List<(float, float)>();
        public readonly List<List<(int to, float offset)>> Links = new List<List<(int, float)>>();

        public int Count => Nodes.Count;

        public static WalkGraph Build(DistrictPlan p)
        {
            var g = new WalkGraph();
            var horizontal = new List<(float z, float x0, float x1, float offset)>();
            var vertical = new List<(float x, float z0, float z1, float offset)>();
            foreach (DistrictRoad r in p.Roads)
            {
                float offset = r.Kind == RoadKind.Main ? Math.Min(r.Area.Width, r.Area.Depth) / 2f - 2.2f : r.Kind == RoadKind.Promenade ? 2.5f : 0.9f;
                if (r.Area.Width >= r.Area.Depth)
                {
                    horizontal.Add((r.Area.CentreZ, r.Area.X0 + 1f, r.Area.X1 - 1f, offset));
                }
                else
                {
                    vertical.Add((r.Area.CentreX, r.Area.Z0 + 1f, r.Area.Z1 - 1f, offset));
                }
            }

            Area sq = p.Square;
            var ring = new[]
            {
                g.Node(sq.X0 + 6f, sq.Z0 + 6f), g.Node(sq.X1 - 6f, sq.Z0 + 6f), g.Node(sq.X1 - 6f, sq.Z1 - 6f), g.Node(sq.X0 + 6f, sq.Z1 - 6f),
            };
            for (int i = 0; i < 4; i++)
            {
                g.Link(ring[i], ring[(i + 1) % 4], 0f);
            }

            // Points along each line: its ends, where it crosses the others, and every 25 m; none inside the square.
            foreach ((float z, float x0, float x1, float offset) in horizontal)
            {
                var xs = new List<float> { x0, x1 };
                foreach ((float x, float vz0, float vz1, float _) in vertical)
                {
                    if (x > x0 && x < x1 && z > vz0 - Reach && z < vz1 + Reach)
                    {
                        xs.Add(x);
                    }
                }

                for (float x = x0 + Spacing; x < x1; x += Spacing)
                {
                    xs.Add(x);
                }

                g.Line(p, xs, x => (x, z), offset, sq, ring);
            }

            foreach ((float x, float z0, float z1, float offset) in vertical)
            {
                var zs = new List<float> { z0, z1 };
                foreach ((float z, float hx0, float hx1, float _) in horizontal)
                {
                    if (z > z0 - Reach && z < z1 + Reach && x > hx0 && x < hx1)
                    {
                        zs.Add(z); // may run a few metres past the road's end, onto the road it meets
                    }
                }

                for (float z = z0 + Spacing; z < z1; z += Spacing)
                {
                    zs.Add(z);
                }

                g.Line(p, zs, z => (x, z), offset, sq, ring);
            }

            return g;
        }

        public int Nearest(float x, float z)
        {
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < Nodes.Count; i++)
            {
                float dx = Nodes[i].x - x, dz = Nodes[i].z - z;
                float d = dx * dx + dz * dz;
                if (d < bestD)
                {
                    bestD = d;
                    best = i;
                }
            }

            return best;
        }

        /// <summary>The shortest way from one node to another, written into <paramref name="path"/>; the count of nodes, or 0 when there's none.</summary>
        public int Path(int from, int to, int[] path, float[] dist, int[] prev, bool[] done)
        {
            int n = Nodes.Count;
            for (int i = 0; i < n; i++)
            {
                dist[i] = float.MaxValue;
                prev[i] = -1;
                done[i] = false;
            }

            dist[from] = 0f;
            for (int k = 0; k < n; k++)
            {
                int u = -1;
                float best = float.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    if (!done[i] && dist[i] < best)
                    {
                        best = dist[i];
                        u = i;
                    }
                }

                if (u < 0 || u == to)
                {
                    break;
                }

                done[u] = true;
                foreach ((int v, float _) in Links[u])
                {
                    float dx = Nodes[u].x - Nodes[v].x, dz = Nodes[u].z - Nodes[v].z;
                    float d = dist[u] + (float)Math.Sqrt(dx * dx + dz * dz);
                    if (d < dist[v])
                    {
                        dist[v] = d;
                        prev[v] = u;
                    }
                }
            }

            if (from != to && prev[to] < 0)
            {
                return 0;
            }

            int count = 0;
            for (int at = to; at >= 0 && count < path.Length; at = prev[at])
            {
                path[count++] = at;
                if (at == from)
                {
                    break;
                }
            }

            Array.Reverse(path, 0, count);
            return count;
        }

        public float OffsetBetween(int a, int b)
        {
            foreach ((int to, float offset) in Links[a])
            {
                if (to == b)
                {
                    return offset;
                }
            }

            return 0f;
        }

        private int Node(float x, float z)
        {
            for (int i = 0; i < Nodes.Count; i++)
            {
                if (Math.Abs(Nodes[i].x - x) < 0.5f && Math.Abs(Nodes[i].z - z) < 0.5f)
                {
                    return i;
                }
            }

            Nodes.Add((x, z));
            Links.Add(new List<(int, float)>());
            return Nodes.Count - 1;
        }

        private void Link(int a, int b, float offset)
        {
            if (a == b || OffsetBetween(a, b) != 0f || Links[a].Exists(l => l.to == b))
            {
                return;
            }

            Links[a].Add((b, offset));
            Links[b].Add((a, offset));
        }

        private void Line(DistrictPlan plan, List<float> along, Func<float, (float x, float z)> at, float offset, Area square, int[] ring)
        {
            along.Sort();
            int previous = -1;
            for (int i = 0; i < along.Count; i++)
            {
                (float x, float z) = at(along[i]);
                if (!plan.IsWalkable(x, z, 0.5f) && !square.Contains(x, z, -1f))
                {
                    continue; // a stall or the tower stands here: the line joins the points either side
                }

                if (square.Contains(x, z, -1f))
                {
                    // Into the square: link to the nearest ring corner and start again on the far side.
                    if (previous >= 0)
                    {
                        Link(previous, NearestOf(ring, Nodes[previous].x, Nodes[previous].z), 0f);
                    }

                    previous = -1;
                    continue;
                }

                int node = Node(x, z);
                if (previous >= 0)
                {
                    Link(previous, node, offset);
                }
                else if (i > 0)
                {
                    Link(node, NearestOf(ring, x, z), 0f);
                }

                previous = node;
            }
        }

        private int NearestOf(int[] nodes, float x, float z)
        {
            int best = nodes[0];
            float bestD = float.MaxValue;
            foreach (int i in nodes)
            {
                float dx = Nodes[i].x - x, dz = Nodes[i].z - z;
                if (dx * dx + dz * dz < bestD)
                {
                    bestD = dx * dx + dz * dz;
                    best = i;
                }
            }

            return best;
        }
    }

    public enum PersonState
    {
        Walking,
        Standing,
        Cheering,
        CoveringEars,
        Waving,
        Watching,
    }

    /// <summary>One person in the district. A struct in an array: the crowd allocates nothing as it moves.</summary>
    public struct Person
    {
        public float X;
        public float Z;
        public float Facing;
        public float Speed;
        public float Side;
        public int HomeNode;
        public int PathCount;
        public int PathIndex;
        public PersonState State;
        public float Timer;
        public float WaveCooldown;
        public int Look;
        public bool Shopkeeper;

        /// <summary>How long to stay on arriving (watching a show), or 0 for an ordinary errand.</summary>
        public float Dwell;

        /// <summary>Metres a second right now (0 when standing), for the animation.</summary>
        public float Moving;
    }

    /// <summary>
    /// The district's people (Phase 12 step 4): residents walking errands between their homes, the square, the stalls and
    /// the ghat on the footpaths, standing a while at each; shopkeepers behind their stalls. They react: a cheer at a burst
    /// nearby, hands over ears at a loud bang close by, a step aside for the player, a wave when the player stops near,
    /// gathering round a show. Pure maths and arrays, the same every run from its seed, and allocation-free per tick.
    /// </summary>
    public sealed class CrowdSim
    {
        public const int MaxPath = 64;
        public const float CheerRange = 32f;
        public const float EarsRange = 9f;
        public const float PersonalSpace = 1.1f;
        public const float WaveRange = 3.2f;
        public const float VehicleSpace = 2.2f;
        public const float HornRange = 8f;

        private readonly DistrictPlan _plan;
        private readonly Random _random;
        private readonly int[] _paths;
        private readonly int[] _scratchPath = new int[MaxPath];
        private readonly float[] _dist;
        private readonly int[] _prev;
        private readonly bool[] _done;
        private readonly List<int> _destinations = new List<int>();
        private float _playerStill;

        public CrowdSim(DistrictPlan plan, int residents, int seed)
        {
            _plan = plan;
            Graph = WalkGraph.Build(plan);
            _random = new Random(seed);
            int n = Graph.Count;
            _dist = new float[n];
            _prev = new int[n];
            _done = new bool[n];
            for (int i = 0; i < n; i++)
            {
                _destinations.Add(i);
            }

            int keepers = 0;
            foreach (DistrictProp p in plan.Props)
            {
                keepers += p.Kind == DistrictProp.Stall ? 1 : 0;
            }

            People = new Person[residents + keepers];
            _paths = new int[People.Length * MaxPath];
            for (int i = 0; i < residents; i++)
            {
                int node = _random.Next(n);
                People[i] = new Person
                {
                    X = Graph.Nodes[node].x,
                    Z = Graph.Nodes[node].z,
                    Speed = 1.05f + (float)_random.NextDouble() * 0.45f,
                    Side = _random.NextDouble() < 0.5 ? -1f : 1f,
                    HomeNode = node,
                    State = PersonState.Standing,
                    Timer = (float)_random.NextDouble() * 6f,
                    Look = _random.Next(),
                };
            }

            int k = residents;
            foreach (DistrictProp p in plan.Props)
            {
                if (p.Kind != DistrictProp.Stall)
                {
                    continue;
                }

                // Behind the counter, facing the road.
                float behind = p.Front == Facing.North ? -1.25f : 1.25f;
                People[k++] = new Person
                {
                    X = p.X,
                    Z = p.Z + behind,
                    Facing = p.Front == Facing.North ? 0f : 180f,
                    State = PersonState.Standing,
                    Timer = float.MaxValue,
                    Shopkeeper = true,
                    Look = _random.Next(),
                };
            }
        }

        public WalkGraph Graph { get; }

        public Person[] People { get; }

        /// <summary>Phase 12 step 5: how much room people give the player (more on the scooter).</summary>
        public float PlayerSpace { get; set; } = PersonalSpace;

        /// <summary>Phase 12 step 5: the traffic, whom walkers also step aside for; may be null.</summary>
        public Vehicle[] Vehicles { get; set; }

        public void Tick(float dt, float playerX, float playerZ, bool playerMoving)
        {
            _playerStill = playerMoving ? 0f : _playerStill + dt;
            for (int i = 0; i < People.Length; i++)
            {
                ref Person p = ref People[i];
                p.WaveCooldown = Math.Max(0f, p.WaveCooldown - dt);
                float pdx = playerX - p.X, pdz = playerZ - p.Z;
                float playerD = (float)Math.Sqrt(pdx * pdx + pdz * pdz);

                // A wave for a player who stops nearby, now and then.
                if (_playerStill > 0.8f && playerD < WaveRange && p.WaveCooldown <= 0f && p.State != PersonState.Cheering && p.State != PersonState.CoveringEars)
                {
                    p.State = PersonState.Waving;
                    p.Timer = 1.6f;
                    p.WaveCooldown = 25f + (float)_random.NextDouble() * 20f;
                    p.Facing = Angle(pdx, pdz);
                }

                if (p.State != PersonState.Walking)
                {
                    p.Moving = 0f;
                    p.Timer -= dt;
                    if (p.Timer <= 0f && !p.Shopkeeper)
                    {
                        NextErrand(i, ref p);
                    }
                    else if (p.Timer <= 0f)
                    {
                        p.State = PersonState.Standing;
                        p.Timer = float.MaxValue;
                    }

                    continue;
                }

                // Walking: towards the next point on the path, on the footpath's side.
                (float tx, float tz) = Waypoint(i, p);
                float dx = tx - p.X, dz = tz - p.Z;
                float d = (float)Math.Sqrt(dx * dx + dz * dz);
                if (d < 0.6f)
                {
                    p.PathIndex++;
                    if (p.PathIndex >= p.PathCount)
                    {
                        Arrive(ref p);
                    }

                    continue;
                }

                float step = Math.Min(d, p.Speed * dt);
                float mx = dx / d * step, mz = dz / d * step;

                // A step aside for the player.
                float room = PlayerSpace * 1.6f;
                if (playerD < room && playerD > 1e-3f)
                {
                    float away = (room - playerD) / room;
                    mx -= pdx / playerD * away * p.Speed * dt * 1.5f;
                    mz -= pdz / playerD * away * p.Speed * dt * 1.5f;
                }

                // And for vehicles going by.
                if (Vehicles != null)
                {
                    for (int k = 0; k < Vehicles.Length; k++)
                    {
                        float vx = Vehicles[k].X - p.X, vz = Vehicles[k].Z - p.Z, vd2 = vx * vx + vz * vz;
                        if (vd2 < VehicleSpace * VehicleSpace && vd2 > 1e-6f)
                        {
                            float vd = (float)Math.Sqrt(vd2), away = (VehicleSpace - vd) / VehicleSpace;
                            mx -= vx / vd * away * p.Speed * dt * 1.5f;
                            mz -= vz / vd * away * p.Speed * dt * 1.5f;
                        }
                    }
                }

                if (!TryStep(ref p, mx, mz))
                {
                    // Round a corner: slide along the wall, or head for the middle of the road; skip the point last.
                    (float cx, float cz) = Graph.Nodes[_paths[i * MaxPath + p.PathIndex]];
                    float rx = cx - p.X, rz = cz - p.Z, rd = (float)Math.Sqrt(rx * rx + rz * rz);
                    if (!TryStep(ref p, mx, 0f) && !TryStep(ref p, 0f, mz) && (rd < 1e-3f || !TryStep(ref p, rx / rd * step, rz / rd * step))
                        && ++p.PathIndex >= p.PathCount)
                    {
                        Arrive(ref p);
                        continue;
                    }
                }

                p.Facing = Angle(dx, dz);
                p.Moving = p.Speed;
            }
        }

        /// <summary>A burst at a point: cheers round it, hands over ears close to a loud one.</summary>
        public void Burst(float x, float z, bool loud)
        {
            for (int i = 0; i < People.Length; i++)
            {
                ref Person p = ref People[i];
                float dx = x - p.X, dz = z - p.Z;
                float d = (float)Math.Sqrt(dx * dx + dz * dz);
                if (d > CheerRange)
                {
                    continue;
                }

                if (loud && d < EarsRange)
                {
                    Pause(ref p, PersonState.CoveringEars, 1.3f, dx, dz);
                }
                else if (_random.NextDouble() < 0.7)
                {
                    Pause(ref p, PersonState.Cheering, 1.7f, dx, dz);
                }
            }
        }

        /// <summary>People within <paramref name="reach"/> come to watch something at a point, for a while.</summary>
        public int Gather(float x, float z, float reach, float seconds)
        {
            int target = Graph.Nearest(x, z), coming = 0;
            for (int i = 0; i < People.Length; i++)
            {
                ref Person p = ref People[i];
                float dx = x - p.X, dz = z - p.Z;
                if (p.Shopkeeper || dx * dx + dz * dz > reach * reach)
                {
                    continue;
                }

                if (Route(i, ref p, target))
                {
                    coming++;
                    p.Dwell = seconds;
                }
            }

            return coming;
        }

        /// <summary>
        /// Phase 12 step 5: the player's horn, heading <paramref name="heading"/>: people ahead in the way step to the side
        /// they're already on, and look round. Returns how many moved.
        /// </summary>
        public int Horn(float x, float z, float heading)
        {
            float fx = (float)Math.Sin(heading * Math.PI / 180.0), fz = (float)Math.Cos(heading * Math.PI / 180.0);
            int moved = 0;
            for (int i = 0; i < People.Length; i++)
            {
                ref Person p = ref People[i];
                float rx = p.X - x, rz = p.Z - z;
                float a = rx * fx + rz * fz, l = rx * fz - rz * fx;
                if (p.Shopkeeper || a <= 0f || a > HornRange || Math.Abs(l) > 2.2f)
                {
                    continue;
                }

                // Sideways, away from the scooter's line: right of it is (fz, -fx).
                float push = Math.Min(1.4f, 2.4f - Math.Abs(l)), side = l >= 0f ? 1f : -1f;
                if (TryStep(ref p, fz * side * push, -fx * side * push) || TryStep(ref p, fz * side * push * 0.5f, -fx * side * push * 0.5f))
                {
                    moved++;
                }

                p.Facing = Angle(-rx, -rz);
            }

            return moved;
        }

        private bool TryStep(ref Person p, float mx, float mz)
        {
            float nx = p.X + mx, nz = p.Z + mz;
            if ((mx == 0f && mz == 0f) || !_plan.IsWalkable(nx, nz, 0.25f))
            {
                return false;
            }

            p.X = nx;
            p.Z = nz;
            return true;
        }

        private void Arrive(ref Person p)
        {
            p.State = p.Dwell > 0f ? PersonState.Watching : PersonState.Standing;
            p.Timer = p.Dwell > 0f ? p.Dwell : 6f + (float)_random.NextDouble() * 30f;
            p.Dwell = 0f;
            p.Moving = 0f;
        }

        private void Pause(ref Person p, PersonState state, float seconds, float dx, float dz)
        {
            p.State = state;
            p.Timer = seconds;
            p.Facing = Angle(dx, dz);
            p.Moving = 0f;
        }

        private void NextErrand(int i, ref Person p)
        {
            // Home now and then; otherwise anywhere on the paths (the square, the stalls and the ghat are all on them).
            int target = _random.NextDouble() < 0.2 ? p.HomeNode : _destinations[_random.Next(_destinations.Count)];
            if (!Route(i, ref p, target))
            {
                p.State = PersonState.Standing;
                p.Timer = 5f;
            }
        }

        private bool Route(int i, ref Person p, int target)
        {
            int from = Graph.Nearest(p.X, p.Z);
            int count = Graph.Path(from, target, _scratchPath, _dist, _prev, _done);
            if (count == 0)
            {
                return false;
            }

            Array.Copy(_scratchPath, 0, _paths, i * MaxPath, count);
            p.PathCount = count;
            p.PathIndex = 0;
            p.State = PersonState.Walking;
            return true;
        }

        /// <summary>The next point on a person's path, moved to their side of the road.</summary>
        private (float x, float z) Waypoint(int i, Person p)
        {
            int node = _paths[i * MaxPath + p.PathIndex];
            (float x, float z) = Graph.Nodes[node];
            int from = p.PathIndex > 0 ? _paths[i * MaxPath + p.PathIndex - 1] : -1;
            if (from < 0)
            {
                return (x, z);
            }

            float offset = Graph.OffsetBetween(from, node);
            (float fx, float fz) = Graph.Nodes[from];
            float ex = x - fx, ez = z - fz;
            float len = (float)Math.Sqrt(ex * ex + ez * ez);
            if (len < 1e-3f || offset <= 0f)
            {
                return (x, z);
            }

            // To the right of the way they walk on their side: both footpaths are used, people keep to theirs.
            return (x + ez / len * offset * p.Side, z - ex / len * offset * p.Side);
        }

        private static float Angle(float dx, float dz) => (float)(Math.Atan2(dx, dz) * 180.0 / Math.PI);
    }
}

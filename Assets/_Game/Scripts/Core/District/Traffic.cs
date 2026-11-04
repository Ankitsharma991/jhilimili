using System;
using System.Collections.Generic;

namespace UtsavNights.Core.District
{
    public enum VehicleKind
    {
        Scooter,
        Auto,
        Cycle,
    }

    /// <summary>
    /// One arm of a main road between the chowk (kept for people) and the road's far end: vehicles keep left along it,
    /// turn round at the end and come back on the other side, a loop measured by distance <c>s</c> along it.
    /// </summary>
    public sealed class TrafficArm
    {
        /// <summary>How far left of the road's middle vehicles keep (India drives on the left).</summary>
        public const float Offset = 1.6f;

        public TrafficArm(string road, bool alongX, float centre, float from, float to)
        {
            Road = road;
            AlongX = alongX;
            Centre = centre;
            From = from;
            To = to;
        }

        public string Road { get; }

        /// <summary>True for an east-west road (x along it), false for north-south.</summary>
        public bool AlongX { get; }

        public float Centre { get; }

        public float From { get; }

        public float To { get; }

        public float Straight => To - From;

        public float Turn => (float)Math.PI * Offset;

        public float Length => 2f * (Straight + Turn);

        /// <summary>The side of the road's middle the outward leg keeps to: north of an east-west road, west of a north-south one.</summary>
        private float Side => AlongX ? 1f : -1f;

        /// <summary>The point, heading (degrees, Unity yaw) and whether it is on a turn, at distance <paramref name="s"/> round the loop.</summary>
        public (float x, float z, float heading, bool turning) At(float s)
        {
            s %= Length;
            if (s < 0f)
            {
                s += Length;
            }

            float along, cross, da, dc;
            bool turning = false;
            if (s < Straight)
            {
                along = From + s;
                cross = Centre + Side * Offset;
                da = 1f;
                dc = 0f;
            }
            else if (s < Straight + Turn)
            {
                float th = (s - Straight) / Offset;
                along = To + Offset * (float)Math.Sin(th);
                cross = Centre + Side * Offset * (float)Math.Cos(th);
                da = (float)Math.Cos(th);
                dc = -Side * (float)Math.Sin(th);
                turning = true;
            }
            else if (s < 2f * Straight + Turn)
            {
                along = To - (s - Straight - Turn);
                cross = Centre - Side * Offset;
                da = -1f;
                dc = 0f;
            }
            else
            {
                float th = (s - 2f * Straight - Turn) / Offset;
                along = From - Offset * (float)Math.Sin(th);
                cross = Centre - Side * Offset * (float)Math.Cos(th);
                da = -(float)Math.Cos(th);
                dc = Side * (float)Math.Sin(th);
                turning = true;
            }

            float x = AlongX ? along : cross, z = AlongX ? cross : along;
            float dx = AlongX ? da : dc, dz = AlongX ? dc : da;
            return (x, z, (float)(Math.Atan2(dx, dz) * 180.0 / Math.PI), turning);
        }

        /// <summary>Metres from <paramref name="s"/> to the start of the next turn (0 while on one).</summary>
        public float ToNextTurn(float s)
        {
            s %= Length;
            if (s < Straight)
            {
                return Straight - s;
            }

            if (s >= Straight + Turn && s < 2f * Straight + Turn)
            {
                return 2f * Straight + Turn - s;
            }

            return 0f;
        }
    }

    public struct Vehicle
    {
        public VehicleKind Kind;
        public int Arm;
        public float S;
        public float Speed;
        public float Cruise;
        public float X;
        public float Z;
        public float Heading;

        /// <summary>Seconds stopped for the player in the way.</summary>
        public float Blocked;

        public float HonkCooldown;

        /// <summary>A paint variant for its body.</summary>
        public int Variant;
    }

    /// <summary>
    /// The district's traffic (Phase 12 step 5): scooters, autos and cyclists on the two main roads, keeping left, turning
    /// round at each end (the chowk itself is for people during the festival). Each keeps a safe gap: it slows for a
    /// turn, follows whoever is ahead and stops for anyone in its path, a person crossing or the player, and gives a short
    /// honk when the player stays in the way. Nobody is ever hit. Arrays of structs, the same every run, allocation-free.
    /// </summary>
    public sealed class TrafficSim
    {
        public const float TurnSpeed = 2.8f;
        public const float Acceleration = 2.5f;
        public const float Braking = 5f;
        public const float StopGap = 2.2f;
        public const float HonkAfter = 1.2f;
        public const float HonkEvery = 4f;
        public const int MaxHonks = 8;

        /// <summary>How far either side of a main road's middle vehicles stop for someone; the footpaths beyond are for walking.</summary>
        public const float Carriageway = 2.5f;

        private readonly float[] _honkX = new float[MaxHonks];
        private readonly float[] _honkZ = new float[MaxHonks];
        private readonly Random _random;

        public TrafficSim(DistrictPlan plan, int seed)
        {
            _random = new Random(seed);
            Arms = BuildArms(plan);
            var list = new List<Vehicle>();
            for (int a = 0; a < Arms.Count; a++)
            {
                // About one vehicle per 50 m of loop.
                int count = Math.Max(2, (int)(Arms[a].Length / 50f));
                for (int k = 0; k < count; k++)
                {
                    double roll = _random.NextDouble();
                    VehicleKind kind = roll < 0.5 ? VehicleKind.Scooter : roll < 0.8 ? VehicleKind.Auto : VehicleKind.Cycle;
                    float cruise = kind == VehicleKind.Scooter ? 7f + (float)_random.NextDouble() * 1.6f
                        : kind == VehicleKind.Auto ? 5.4f + (float)_random.NextDouble() * 1.2f
                        : 3.4f + (float)_random.NextDouble() * 0.8f;
                    var v = new Vehicle
                    {
                        Kind = kind,
                        Arm = a,
                        S = Arms[a].Length * (k + (float)_random.NextDouble() * 0.4f) / count,
                        Cruise = cruise,
                        Speed = cruise * 0.5f,
                        Variant = _random.Next(4),
                    };
                    Place(ref v);
                    list.Add(v);
                }
            }

            Vehicles = list.ToArray();
        }

        public IReadOnlyList<TrafficArm> Arms { get; }

        public Vehicle[] Vehicles { get; }

        /// <summary>Honks this tick (where), for the sound.</summary>
        public int Honks { get; private set; }

        public int TotalHonks { get; private set; }

        public (float x, float z) Honk(int i) => (_honkX[i], _honkZ[i]);

        public static float HalfWidth(VehicleKind k) => k == VehicleKind.Auto ? 0.75f : k == VehicleKind.Scooter ? 0.4f : 0.35f;

        public static float HalfLength(VehicleKind k) => k == VehicleKind.Auto ? 1.35f : 0.9f;

        /// <summary>
        /// Moves the traffic on. <paramref name="playerRadius"/> is how wide the player is (bigger on the scooter);
        /// <paramref name="people"/> may be null.
        /// </summary>
        public void Tick(float dt, float playerX, float playerZ, float playerRadius, Person[] people)
        {
            Honks = 0;
            for (int i = 0; i < Vehicles.Length; i++)
            {
                ref Vehicle v = ref Vehicles[i];
                TrafficArm arm = Arms[v.Arm];
                float fx = (float)Math.Sin(v.Heading * Math.PI / 180.0), fz = (float)Math.Cos(v.Heading * Math.PI / 180.0);
                float look = StopGap + 1.5f + v.Speed * 0.6f + v.Speed * v.Speed / (2f * Braking);
                float half = HalfWidth(v.Kind), front = HalfLength(v.Kind);
                float clear = look;
                bool forPlayer = false;

                float c = OnRoad(arm, playerX, playerZ) ? Ahead(v, fx, fz, playerX, playerZ, half + playerRadius + 0.2f) : float.MaxValue;
                if (c < clear)
                {
                    clear = c;
                    forPlayer = true;
                }

                if (people != null)
                {
                    for (int k = 0; k < people.Length; k++)
                    {
                        if (!OnRoad(arm, people[k].X, people[k].Z))
                        {
                            continue;
                        }

                        c = Ahead(v, fx, fz, people[k].X, people[k].Z, half + 0.5f);
                        if (c < clear)
                        {
                            clear = c;
                            forPlayer = false;
                        }
                    }
                }

                for (int k = 0; k < Vehicles.Length; k++)
                {
                    if (k == i || Vehicles[k].Arm != v.Arm)
                    {
                        continue;
                    }

                    c = Ahead(v, fx, fz, Vehicles[k].X, Vehicles[k].Z, half + HalfWidth(Vehicles[k].Kind) + 0.2f) - HalfLength(Vehicles[k].Kind);
                    if (c < clear)
                    {
                        clear = c;
                        forPlayer = false;
                    }
                }

                // The speed it may have: its own pace, slower into and round a turn, and enough room to stop short.
                float want = v.Cruise;
                float toTurn = arm.ToNextTurn(v.S);
                want = Math.Min(want, (float)Math.Sqrt(TurnSpeed * TurnSpeed + 2f * 3f * toTurn));
                float room = clear - front - StopGap;
                want = Math.Min(want, room <= 0f ? 0f : (float)Math.Sqrt(2f * Braking * room));
                v.Speed = want > v.Speed ? Math.Min(want, v.Speed + Acceleration * dt) : Math.Max(want, v.Speed - Braking * 1.6f * dt);

                // A honk when the player stays in the way.
                v.HonkCooldown -= dt;
                v.Blocked = forPlayer && v.Speed < 0.3f ? v.Blocked + dt : 0f;
                if (v.Blocked > HonkAfter && v.HonkCooldown <= 0f)
                {
                    v.HonkCooldown = HonkEvery;
                    TotalHonks++;
                    if (Honks < MaxHonks)
                    {
                        _honkX[Honks] = v.X;
                        _honkZ[Honks] = v.Z;
                        Honks++;
                    }
                }

                v.S = (v.S + v.Speed * dt) % arm.Length;
                Place(ref v);
            }
        }

        private static bool OnRoad(TrafficArm arm, float x, float z) =>
            Math.Abs((arm.AlongX ? z : x) - arm.Centre) < Carriageway;

        /// <summary>How far ahead along its heading a thing is, if it's in the vehicle's path; else a long way.</summary>
        private static float Ahead(in Vehicle v, float fx, float fz, float ox, float oz, float width)
        {
            float rx = ox - v.X, rz = oz - v.Z;
            float a = rx * fx + rz * fz;
            if (a <= 0f || a > 40f)
            {
                return float.MaxValue;
            }

            float l = Math.Abs(rx * fz - rz * fx);
            return l < width ? a : float.MaxValue;
        }

        private void Place(ref Vehicle v)
        {
            (float x, float z, float heading, bool _) = Arms[v.Arm].At(v.S);
            v.X = x;
            v.Z = z;
            v.Heading = heading;
        }

        /// <summary>The main roads, split at the chowk; each arm stops short of the square and of the road's end.</summary>
        public static List<TrafficArm> BuildArms(DistrictPlan p)
        {
            var arms = new List<TrafficArm>();
            const float margin = 5f;
            foreach (DistrictRoad r in p.Roads)
            {
                if (r.Kind != RoadKind.Main)
                {
                    continue;
                }

                bool alongX = r.Area.Width > r.Area.Depth;
                float centre = alongX ? r.Area.CentreZ : r.Area.CentreX;
                float lo = alongX ? r.Area.X0 : r.Area.Z0, hi = alongX ? r.Area.X1 : r.Area.Z1;
                float sqLo = alongX ? p.Square.X0 : p.Square.Z0, sqHi = alongX ? p.Square.X1 : p.Square.Z1;

                // Not into the player's lane at the west end: turn where the bazaar begins.
                if (alongX && Math.Abs(lo - p.Join.X0) < 1f)
                {
                    lo = p.Join.X1 + 3f;
                }

                arms.Add(new TrafficArm(r.Name, alongX, centre, lo + margin, sqLo - margin));
                arms.Add(new TrafficArm(r.Name, alongX, centre, sqHi + margin, hi - margin));
            }

            return arms;
        }
    }
}

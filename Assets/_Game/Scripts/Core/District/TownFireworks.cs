using System;
using System.Collections.Generic;
using UtsavNights.Core.Neighbourhood;

namespace UtsavNights.Core.District
{
    /// <summary>Why a cracker can or can't go down at a spot in the town.</summary>
    public enum TownVerdict
    {
        Ok,
        NotOpenGround,
        OnTheRoad,
        NearDoor,
        NearStall,
        NearWires,
        NearPeople,
        NoOpenSky,
        TooMany,
    }

    /// <summary>
    /// Where crackers may be lit in the town (Phase 12 step 7): on open ground or a flat roof, never on a main road's
    /// carriageway (the traffic uses it), clear of doors, stalls and their awnings, and the lamp posts' wires, with people
    /// kept back by how loud the cracker is; anything that flies needs open sky (away from walls, the clock tower and the
    /// narrow lanes). The same rules for the player and for the district's own shows.
    /// </summary>
    public static class TownSafety
    {
        public const float DoorClear = 3f;
        public const float StallClear = 3f;
        public const float WireClear = 2.5f;
        public const float WallClear = 1.5f;
        public const float SkyClear = 5f;
        public const float TowerClear = 7f;
        public const float RoadMargin = 0.5f;
        public const int MaxPlaced = 3;

        /// <summary>How far people must be from a cracker by its loudness (0 gentle, 1 medium, 2 loud); aerials need more.</summary>
        public static float PeopleClear(int intensity, bool aerial) =>
            Math.Max(aerial ? 6f : 0f, intensity <= 0 ? 3f : intensity == 1 ? 5f : 8f);

        /// <summary>A verdict as a short id for analytics: ok, on_the_road, near_people and so on.</summary>
        public static string Id(TownVerdict v) => v switch
        {
            TownVerdict.Ok => "ok",
            TownVerdict.NotOpenGround => "not_open_ground",
            TownVerdict.OnTheRoad => "on_the_road",
            TownVerdict.NearDoor => "near_door",
            TownVerdict.NearStall => "near_stall",
            TownVerdict.NearWires => "near_wires",
            TownVerdict.NearPeople => "near_people",
            TownVerdict.NoOpenSky => "no_open_sky",
            TownVerdict.TooMany => "too_many",
            _ => "other",
        };

        public static string Explain(TownVerdict v) => v switch
        {
            TownVerdict.NotOpenGround => "Find open ground to set it down.",
            TownVerdict.OnTheRoad => "Not on the road: the traffic uses it. Try the square, the ghat or a footpath corner.",
            TownVerdict.NearDoor => "Too close to someone's door. Move a few steps away.",
            TownVerdict.NearStall => "Too close to the stalls and their awnings.",
            TownVerdict.NearWires => "Not under the lamp wires. Move away from the post.",
            TownVerdict.NearPeople => "People are too close. Wait for them to step back, or find a clearer spot.",
            TownVerdict.NoOpenSky => "This one flies: it needs open sky, away from walls and the clock tower.",
            TownVerdict.TooMany => "Light the ones you've set down first.",
            _ => string.Empty,
        };

        /// <summary>
        /// Checks a spot. <paramref name="y"/> is the ground's height (above 1 m: a roof). People are the crowd (may be
        /// null), each at (X, Z); <paramref name="placed"/> is how many of the player's crackers are already down.
        /// </summary>
        public static TownVerdict Check(DistrictPlan plan, IReadOnlyList<TrafficArm> arms, float x, float y, float z, int intensity, bool aerial,
            Person[] people, int placed)
        {
            if (placed >= MaxPlaced)
            {
                return TownVerdict.TooMany;
            }

            bool lane = InLaneStreet(plan, x, z);
            if (!lane && !plan.IsWalkableAt(x, y, z, 0.4f))
            {
                return TownVerdict.NotOpenGround;
            }

            bool onRoof = y > 1f && !lane;
            if (!onRoof)
            {
                if (OnCarriageway(arms, x, z))
                {
                    return TownVerdict.OnTheRoad;
                }

                // Stalls and lamp posts first: they stand in front of the shops.
                foreach (DistrictProp p in plan.Props)
                {
                    float d = Distance(x, z, p.X, p.Z);
                    if (p.Kind == DistrictProp.Stall && d < StallClear)
                    {
                        return TownVerdict.NearStall;
                    }

                    if (p.Kind == DistrictProp.Lamp && d < WireClear)
                    {
                        return TownVerdict.NearWires;
                    }
                }

                if (lane)
                {
                    // The player's lane: clear of every gate along it.
                    for (int k = -3; k <= 3; k++)
                    {
                        if (Distance(x, z, k * SettlementLibrary.LotSpacing, SettlementLibrary.StreetZ0) < DoorClear)
                        {
                            return TownVerdict.NearDoor;
                        }
                    }
                }
                else
                {
                    foreach (DistrictBuilding b in plan.Buildings)
                    {
                        Area f = b.Footprint;
                        if (!f.Contains(x, z, SkyClear + 1f))
                        {
                            continue;
                        }

                        (float dx, float dz) = DoorPoint(b);
                        if (Distance(x, z, dx, dz) < DoorClear)
                        {
                            return TownVerdict.NearDoor;
                        }

                        if (f.Contains(x, z, WallClear))
                        {
                            return TownVerdict.NotOpenGround;
                        }
                    }
                }

            }

            if (people != null)
            {
                float clear = PeopleClear(intensity, aerial);
                for (int i = 0; i < people.Length; i++)
                {
                    if (Distance(x, z, people[i].X, people[i].Z) < clear)
                    {
                        return TownVerdict.NearPeople;
                    }
                }
            }

            if (aerial && !OpenSky(plan, x, y, z, lane))
            {
                return TownVerdict.NoOpenSky;
            }

            return TownVerdict.Ok;
        }

        /// <summary>Whether a point is on the player's lane's street (west of the bazaar).</summary>
        public static bool InLaneStreet(DistrictPlan plan, float x, float z) =>
            x < plan.Join.X0 && x > -MapFrame.LaneHalf && z > SettlementLibrary.StreetZ0 + 0.3f && z < SettlementLibrary.StreetZ1 - 0.3f;

        public static bool OnCarriageway(IReadOnlyList<TrafficArm> arms, float x, float z)
        {
            foreach (TrafficArm a in arms)
            {
                float along = a.AlongX ? x : z, cross = a.AlongX ? z : x;
                if (along > a.From - TrafficArm.Offset - 1f && along < a.To + TrafficArm.Offset + 1f
                    && Math.Abs(cross - a.Centre) < TrafficSim.Carriageway + RoadMargin)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>A building's door: the middle of its front.</summary>
        public static (float x, float z) DoorPoint(DistrictBuilding b)
        {
            Area f = b.Footprint;
            return b.Front switch
            {
                Facing.South => (f.CentreX, f.Z0),
                Facing.North => (f.CentreX, f.Z1),
                Facing.West => (f.X0, f.CentreZ),
                _ => (f.X1, f.CentreZ),
            };
        }

        private static bool OpenSky(DistrictPlan plan, float x, float y, float z, bool lane)
        {
            if (lane)
            {
                return true; // the lane's houses are low and set back behind their gates
            }

            if (y > 1f)
            {
                return true; // up on a roof, the sky is open
            }

            foreach (DistrictRoad r in plan.Roads)
            {
                if (r.Kind == RoadKind.Lane && r.Area.Contains(x, z) && !plan.Square.Contains(x, z))
                {
                    return false;
                }
            }

            foreach (DistrictBuilding b in plan.Buildings)
            {
                if (b.Footprint.Contains(x, z, SkyClear))
                {
                    return false;
                }
            }

            foreach (DistrictProp p in plan.Props)
            {
                if (p.Kind == DistrictProp.Tower && Distance(x, z, p.X, p.Z) < TowerClear)
                {
                    return false;
                }
            }

            return true;
        }

        private static float Distance(float ax, float az, float bx, float bz) =>
            (float)Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));
    }

    /// <summary>One cracker in a district show: when (seconds from the start), from which pad, and how big (0 to 2).</summary>
    public readonly struct TownCue
    {
        public TownCue(float at, int pad, int size)
        {
            At = at;
            Pad = pad;
            Size = size;
        }

        public float At { get; }

        public int Pad { get; }

        public int Size { get; }
    }

    /// <summary>A place the district puts on its own shows: where the pads are, and the point people gather round.</summary>
    public sealed class TownShowPlace
    {
        public string Name;

        /// <summary>A short id for analytics: ghat, chowk.</summary>
        public string Id;
        public float WatchX;
        public float WatchZ;
        public readonly List<(float x, float z)> Pads = new List<(float, float)>();
    }

    /// <summary>
    /// The district's own shows (Phase 12 step 7): at the ghat over the river and on the chowk, in turn, every few minutes
    /// of exploring, announced a little before; the crowd gathers round. Each is a fixed program of cues on its pads,
    /// building to a finale, the same every time.
    /// </summary>
    public static class TownShows
    {
        public const float FirstAt = 150f;
        public const float Every = 360f;
        public const float NoticeBefore = 45f;
        public const float Length = 26f;

        public static List<TownShowPlace> Places(DistrictPlan plan)
        {
            var ghat = new TownShowPlace { Name = "the ghat", Id = "ghat", WatchX = plan.Square.CentreX + 40f, WatchZ = plan.Ghat.Z0 - 4f };
            for (int i = 0; i < 5; i++)
            {
                ghat.Pads.Add((ghat.WatchX - 16f + i * 8f, plan.Ghat.CentreZ));
            }

            var chowk = new TownShowPlace { Name = "the chowk", Id = "chowk", WatchX = plan.Square.CentreX, WatchZ = plan.Square.CentreZ };
            foreach ((float dx, float dz) in new[] { (-14f, -14f), (14f, -14f), (-14f, 14f), (14f, 14f) })
            {
                chowk.Pads.Add((plan.Square.CentreX + dx, plan.Square.CentreZ + dz));
            }

            return new List<TownShowPlace> { ghat, chowk };
        }

        /// <summary>Which show is next and when it starts (seconds of exploring), from how long the player has explored.</summary>
        public static (int number, float startsAt) Next(float exploredSeconds)
        {
            if (exploredSeconds < FirstAt + Length)
            {
                return (0, FirstAt);
            }

            int n = (int)Math.Floor((exploredSeconds - FirstAt - Length) / Every) + 1;
            return (n, FirstAt + n * Every);
        }

        /// <summary>The place for a show number: the ghat and the chowk in turn.</summary>
        public static int PlaceOf(int number, int places) => places <= 0 ? 0 : number % places;

        /// <summary>The program: a steady opening, a busier middle, a finale of big ones from every pad.</summary>
        public static List<TownCue> Program(int pads, int seed)
        {
            var random = new Random(seed);
            var cues = new List<TownCue>();
            float t = 0f;
            while (t < Length - 6f)
            {
                cues.Add(new TownCue(t, random.Next(pads), t > Length * 0.5f ? 1 : 0));
                t += t < Length * 0.4f ? 1.6f + (float)random.NextDouble() : 0.8f + (float)random.NextDouble() * 0.6f;
            }

            for (int i = 0; i < pads * 2; i++)
            {
                cues.Add(new TownCue(Length - 5f + i * (4f / (pads * 2)), i % pads, 2));
            }

            return cues;
        }
    }
}

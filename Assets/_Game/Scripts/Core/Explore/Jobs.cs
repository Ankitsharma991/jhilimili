using System;
using System.Collections.Generic;
using UtsavNights.Core.District;
using UtsavNights.Core.Neighbourhood;

namespace UtsavNights.Core.Explore
{
    public enum JobStepKind
    {
        /// <summary>Done on arriving within the step's radius.</summary>
        Reach,

        /// <summary>Done with the Interact button within the step's radius.</summary>
        Interact,
    }

    /// <summary>
    /// One thing to do in a job. Steps in the same <see cref="Group"/> can be done in any order; groups go in turn. An
    /// Interact step may move the player afterwards (up to a roof).
    /// </summary>
    public sealed class JobStep
    {
        public string Id;
        public JobStepKind Kind;

        /// <summary>What the HUD says to do ("Deliver the diyas to the sweet shop").</summary>
        public string Objective;

        /// <summary>What the Interact button says there ("Hand over the diyas").</summary>
        public string Prompt;

        public float X;
        public float Y;
        public float Z;
        public float Radius = 2.2f;
        public int Group;

        /// <summary>Where the player is put after doing it, or none.</summary>
        public bool MovesPlayer;
        public float ToX;
        public float ToY;
        public float ToZ;
        public float ToFacing;

        /// <summary>Doing it sets off the job's finale (the rooftop show).</summary>
        public bool Finale;
    }

    public sealed class JobDef
    {
        public string Id;
        public string Title;

        /// <summary>Who asks ("Meena, at the clay stall").</summary>
        public string Giver;

        /// <summary>The message offering it.</summary>
        public string Offer;

        /// <summary>The message after it's done.</summary>
        public string Thanks;

        public long Coins;
        public int Reputation;

        /// <summary>Seconds to finish in, or 0 for no hurry.</summary>
        public float TimeLimit;

        /// <summary>The reputation level needed before it's offered.</summary>
        public int MinLevel = 1;

        public readonly List<JobStep> Steps = new List<JobStep>();

        public int Groups
        {
            get
            {
                int g = 0;
                foreach (JobStep s in Steps)
                {
                    g = Math.Max(g, s.Group + 1);
                }

                return g;
            }
        }
    }

    public enum JobStatus
    {
        Active,
        Completed,
        Failed,
        Abandoned,
    }

    public enum JobProgress
    {
        None,
        StepDone,
        Completed,
        Failed,
    }

    /// <summary>
    /// A job being done (Phase 12 step 6): which steps are done, the group now open, the clock. The objective text changes
    /// only when progress does, so ticking it allocates nothing.
    /// </summary>
    public sealed class JobRun
    {
        private readonly bool[] _done;
        private readonly int _groups;

        public JobRun(JobDef def)
        {
            Def = def;
            _done = new bool[def.Steps.Count];
            _groups = def.Groups;
            UpdateObjective();
        }

        public JobDef Def { get; }

        public JobStatus Status { get; private set; } = JobStatus.Active;

        public float Elapsed { get; private set; }

        /// <summary>The group of steps open now.</summary>
        public int Group { get; private set; }

        public string Objective { get; private set; } = string.Empty;

        /// <summary>Seconds left, or -1 when there's no time limit.</summary>
        public float TimeLeft => Def.TimeLimit > 0f ? Math.Max(0f, Def.TimeLimit - Elapsed) : -1f;

        /// <summary>The step that set off the finale just now (read once by the game, then cleared).</summary>
        public bool FinaleDue { get; set; }

        public bool IsDone(int step) => _done[step];

        public int DoneCount
        {
            get
            {
                int n = 0;
                foreach (bool d in _done)
                {
                    n += d ? 1 : 0;
                }

                return n;
            }
        }

        /// <summary>Whether a step is open now: in the current group and not done.</summary>
        public bool IsOpen(int step) => Status == JobStatus.Active && !_done[step] && Def.Steps[step].Group == Group;

        /// <summary>The open step nearest a point (the waypoint), or -1.</summary>
        public int NearestOpen(float x, float z)
        {
            int best = -1;
            float bestD = float.MaxValue;
            for (int i = 0; i < _done.Length; i++)
            {
                if (!IsOpen(i))
                {
                    continue;
                }

                JobStep s = Def.Steps[i];
                float d = (s.X - x) * (s.X - x) + (s.Z - z) * (s.Z - z);
                if (d < bestD)
                {
                    bestD = d;
                    best = i;
                }
            }

            return best;
        }

        /// <summary>The open Interact step within reach of the player, or -1.</summary>
        public int InteractableAt(float x, float y, float z)
        {
            for (int i = 0; i < _done.Length; i++)
            {
                JobStep s = Def.Steps[i];
                if (IsOpen(i) && s.Kind == JobStepKind.Interact && Near(s, x, y, z))
                {
                    return i;
                }
            }

            return -1;
        }

        public JobProgress Tick(float dt, float x, float y, float z)
        {
            if (Status != JobStatus.Active)
            {
                return JobProgress.None;
            }

            Elapsed += dt;
            if (Def.TimeLimit > 0f && Elapsed >= Def.TimeLimit)
            {
                Status = JobStatus.Failed;
                Objective = "Out of time";
                return JobProgress.Failed;
            }

            for (int i = 0; i < _done.Length; i++)
            {
                if (IsOpen(i) && Def.Steps[i].Kind == JobStepKind.Reach && Near(Def.Steps[i], x, y, z))
                {
                    return Complete(i);
                }
            }

            return JobProgress.None;
        }

        /// <summary>Does an Interact step (the player must be at it). None when it isn't open or the player isn't there.</summary>
        public JobProgress Interact(int step, float x, float y, float z)
        {
            if (step < 0 || step >= _done.Length || !IsOpen(step) || Def.Steps[step].Kind != JobStepKind.Interact || !Near(Def.Steps[step], x, y, z))
            {
                return JobProgress.None;
            }

            return Complete(step);
        }

        public void Abandon()
        {
            if (Status == JobStatus.Active)
            {
                Status = JobStatus.Abandoned;
            }
        }

        private JobProgress Complete(int step)
        {
            _done[step] = true;
            FinaleDue |= Def.Steps[step].Finale;
            bool groupDone = true;
            for (int i = 0; i < _done.Length; i++)
            {
                groupDone &= _done[i] || Def.Steps[i].Group != Group;
            }

            if (groupDone)
            {
                Group++;
                if (Group >= _groups)
                {
                    Status = JobStatus.Completed;
                    Objective = "Done!";
                    return JobProgress.Completed;
                }
            }

            UpdateObjective();
            return JobProgress.StepDone;
        }

        private void UpdateObjective()
        {
            int open = 0, total = 0, first = -1;
            for (int i = 0; i < _done.Length; i++)
            {
                if (Def.Steps[i].Group != Group)
                {
                    continue;
                }

                total++;
                if (!_done[i])
                {
                    open++;
                    first = first < 0 ? i : first;
                }
            }

            if (first < 0)
            {
                Objective = string.Empty;
                return;
            }

            string text = Def.Steps[first].Objective;
            Objective = total > 1 ? $"{text} ({total - open} of {total})" : text;
        }

        private static bool Near(JobStep s, float x, float y, float z) =>
            (s.X - x) * (s.X - x) + (s.Z - z) * (s.Z - z) <= s.Radius * s.Radius && Math.Abs(s.Y - y) < 1.5f;
    }

    /// <summary>What a job pays: in full the first time, a quarter for doing it again.</summary>
    public static class JobPay
    {
        public const float RepeatShare = 0.25f;

        public static (long coins, int reputation) For(JobDef def, int timesDoneBefore) =>
            timesDoneBefore <= 0 ? (def.Coins, def.Reputation)
                : ((long)Math.Round(def.Coins * RepeatShare), (int)Math.Round(def.Reputation * RepeatShare));
    }

    /// <summary>
    /// The town's jobs (Phase 12 step 6), placed from the district's plan and the player's lane, the same every time:
    /// light the diyas along your lane; carry diyas from the clay stall to three shops across the bazaar against the
    /// clock; and a rooftop show at the chowk (collect the crate, take it up to the roof, set up the racks, light it).
    /// </summary>
    public static class JobCatalog
    {
        public const string Lane = "light_the_lane";
        public const string Diyas = "diyas_for_the_bazaar";
        public const string Rooftop = "rooftop_show";

        /// <summary>How far in front of a shop's door a delivery is made.</summary>
        public const float DoorStep = 1.6f;

        public static List<JobDef> Build(DistrictPlan plan)
        {
            return new List<JobDef> { LaneJob(), DiyaJob(plan), RooftopJob(plan) };
        }

        /// <summary>Eight diyas along the player's lane: in front of the neighbours' gates and across the street.</summary>
        public static JobDef LaneJob()
        {
            var job = new JobDef
            {
                Id = Lane,
                Title = "Light up the lane",
                Giver = "Kamla Aunty, next door",
                Offer = "Beta, the lane is still dark and my knees won't take the bending tonight. Could you light the diyas along it? Eight of them, by the gates and across the street.",
                Thanks = "Look at it glow! The whole lane is talking about you. Here, a little something for your trouble.",
                Coins = 80,
                Reputation = 40,
            };
            int n = 0;
            for (int k = -3; k <= 3; k++)
            {
                if (k != 0)
                {
                    job.Steps.Add(Diya(n++, k * SettlementLibrary.LotSpacing, SettlementLibrary.StreetZ0 + 0.5f));
                }
            }

            job.Steps.Add(Diya(n++, -12f, SettlementLibrary.StreetZ1 - 0.5f));
            job.Steps.Add(Diya(n, 12f, SettlementLibrary.StreetZ1 - 0.5f));
            return job;
        }

        /// <summary>Diyas from the clay stall to three shops: down the bazaar road, down the chowk road and on the promenade.</summary>
        public static JobDef DiyaJob(DistrictPlan plan)
        {
            DistrictProp stall = plan.Props.Find(p => p.Kind == DistrictProp.Stall && p.Front == Facing.North && p.X > 55f);
            var job = new JobDef
            {
                Id = Diyas,
                Title = "Diyas for the bazaar",
                Giver = "Meena, at the clay stall",
                Offer = "Three shops are waiting on my diyas and my nephew has gone to watch the cricket. Take a box to each before the evening rush, in five minutes? The scooter will get you there.",
                Thanks = "All three, and in time! The shopkeepers sent their thanks. Come back any night, there's always work at the stall.",
                Coins = 150,
                Reputation = 60,
                TimeLimit = 300f,
            };
            job.Steps.Add(new JobStep
            {
                Id = "pickup",
                Kind = JobStepKind.Interact,
                Objective = "Pick up the diyas at Meena's stall",
                Prompt = "Pick up the diyas",
                X = stall.X,
                Z = stall.Z + 1.6f,
                Group = 0,
            });

            DistrictBuilding east = plan.Buildings.Find(b => b.Shop && b.Front == Facing.South && b.Footprint.CentreX > 215f && b.Footprint.Z0 < 20f);
            DistrictBuilding south = plan.Buildings.Find(b => b.Shop && b.Front == Facing.East && b.Footprint.X1 < 171f && b.Footprint.CentreZ < -60f);
            DistrictBuilding river = plan.Buildings.Find(b => b.Shop && b.Front == Facing.North && b.Footprint.Z1 > 100f && b.Footprint.CentreX > 90f);
            int n = 0;
            foreach ((DistrictBuilding b, string name) in new[] { (east, "the sweet shop"), (south, "the cloth shop"), (river, "the tea shop by the river") })
            {
                (float x, float z) = DoorOf(b, DoorStep);
                job.Steps.Add(new JobStep
                {
                    Id = "deliver" + n++,
                    Kind = JobStepKind.Interact,
                    Objective = "Deliver the diyas to " + name,
                    Prompt = "Hand over the diyas",
                    X = x,
                    Z = z,
                    Group = 1,
                });
            }

            return job;
        }

        /// <summary>The rooftop show: a crate from the fireworks stall up to a roof on the chowk, three racks, then light it.</summary>
        public static JobDef RooftopJob(DistrictPlan plan)
        {
            DistrictProp stall = plan.Props.Find(p => p.Kind == DistrictProp.Stall && p.Front == Facing.South && p.X > 95f);
            DistrictBuilding host = RooftopHouse(plan);
            Area f = host.Footprint;
            Facing view = SideTowards(f, plan.Square);
            (float dx, float dz) = DoorOf(host, DoorStep);
            float roof = host.Height;
            var job = new JobDef
            {
                Id = Rooftop,
                Title = "A rooftop show",
                Giver = "Joshi-ji, on the chowk",
                Offer = "Everyone says you're the one to ask. I'd like a show from my roof by the chowk tonight. Fetch the crate from the fireworks stall, set up the racks on my roof and light it for the whole square.",
                Thanks = "The whole chowk came out to watch! You've made my Diwali. People will be asking for you now.",
                Coins = 250,
                Reputation = 100,
                MinLevel = 2,
            };
            job.Steps.Add(new JobStep
            {
                Id = "crate",
                Kind = JobStepKind.Interact,
                Objective = "Collect the show crate from the fireworks stall",
                Prompt = "Collect the crate",
                X = stall.X,
                Z = stall.Z - 1.6f,
                Group = 0,
            });
            job.Steps.Add(new JobStep
            {
                Id = "upstairs",
                Kind = JobStepKind.Interact,
                Objective = "Take the crate up to Joshi-ji's roof",
                Prompt = "Take it up to the roof",
                X = dx,
                Z = dz,
                Group = 1,
                MovesPlayer = true,
                ToX = f.CentreX,
                ToY = roof + 0.05f,
                ToZ = f.CentreZ,
                ToFacing = FacingYaw(view),
            });

            // Three racks along the roof's edge over the square: its two corners and the middle.
            float inset = 1.4f;
            (float x, float z)[] racks = RackPoints(host, view, inset);
            for (int i = 0; i < racks.Length; i++)
            {
                job.Steps.Add(new JobStep
                {
                    Id = "rack" + i,
                    Kind = JobStepKind.Interact,
                    Objective = "Set up the rocket racks",
                    Prompt = "Set up a rack",
                    X = racks[i].x,
                    Y = roof,
                    Z = racks[i].z,
                    Radius = 1.6f,
                    Group = 2,
                });
            }

            job.Steps.Add(new JobStep
            {
                Id = "light",
                Kind = JobStepKind.Interact,
                Objective = "Light the show",
                Prompt = "Light the show",
                X = f.CentreX,
                Y = roof,
                Z = f.CentreZ,
                Radius = 2.4f,
                Group = 3,
                Finale = true,
            });
            return job;
        }

        /// <summary>
        /// The house beside the chowk whose roof is the stage: two floors or more, right by the square, its front door clear
        /// to stand at; the lowest such roof, then the closest.
        /// </summary>
        public static DistrictBuilding RooftopHouse(DistrictPlan plan)
        {
            DistrictBuilding best = null;
            float bestGap = float.MaxValue;
            foreach (DistrictBuilding b in plan.Buildings)
            {
                float gap = Gap(b.Footprint, plan.Square);
                (float x, float z) = DoorOf(b, DoorStep);
                if (b.Floors < 2 || gap > 12f || !plan.IsWalkable(x, z, 0.5f))
                {
                    continue;
                }

                if (best == null || b.Floors < best.Floors || (b.Floors == best.Floors && gap < bestGap))
                {
                    best = b;
                    bestGap = gap;
                }
            }

            return best;
        }

        /// <summary>Which side of a building looks towards an area.</summary>
        public static Facing SideTowards(Area f, Area a)
        {
            float east = a.X0 - f.X1, west = f.X0 - a.X1, north = a.Z0 - f.Z1, south = f.Z0 - a.Z1;
            float most = Math.Max(Math.Max(east, west), Math.Max(north, south));
            return most == east ? Facing.East : most == west ? Facing.West : most == north ? Facing.North : Facing.South;
        }

        private static float Gap(Area f, Area a)
        {
            float dx = Math.Max(0f, Math.Max(a.X0 - f.X1, f.X0 - a.X1));
            float dz = Math.Max(0f, Math.Max(a.Z0 - f.Z1, f.Z0 - a.Z1));
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>A point a step out from the middle of a building's front.</summary>
        public static (float x, float z) DoorOf(DistrictBuilding b, float step)
        {
            Area f = b.Footprint;
            return b.Front switch
            {
                Facing.South => (f.CentreX, f.Z0 - step),
                Facing.North => (f.CentreX, f.Z1 + step),
                Facing.West => (f.X0 - step, f.CentreZ),
                _ => (f.X1 + step, f.CentreZ),
            };
        }

        public static float FacingYaw(Facing f) => f switch
        {
            Facing.North => 0f,
            Facing.East => 90f,
            Facing.South => 180f,
            _ => 270f,
        };

        private static (float x, float z)[] RackPoints(DistrictBuilding b, Facing side, float inset)
        {
            Area f = b.Footprint;
            switch (side)
            {
                case Facing.South:
                    return new[] { (f.X0 + inset, f.Z0 + inset), (f.CentreX, f.Z0 + inset), (f.X1 - inset, f.Z0 + inset) };
                case Facing.North:
                    return new[] { (f.X0 + inset, f.Z1 - inset), (f.CentreX, f.Z1 - inset), (f.X1 - inset, f.Z1 - inset) };
                case Facing.West:
                    return new[] { (f.X0 + inset, f.Z0 + inset), (f.X0 + inset, f.CentreZ), (f.X0 + inset, f.Z1 - inset) };
                default:
                    return new[] { (f.X1 - inset, f.Z0 + inset), (f.X1 - inset, f.CentreZ), (f.X1 - inset, f.Z1 - inset) };
            }
        }

        private static JobStep Diya(int n, float x, float z) => new JobStep
        {
            Id = "diya" + n,
            Kind = JobStepKind.Interact,
            Objective = "Light the diyas along the lane",
            Prompt = "Light the diya",
            X = x,
            Z = z,
            Radius = 1.8f,
            Group = 0,
        };
    }
}

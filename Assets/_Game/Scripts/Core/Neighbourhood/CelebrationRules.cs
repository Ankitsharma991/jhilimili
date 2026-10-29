using System;
using System.Collections.Generic;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Home;
using UtsavNights.Core.Placement;

namespace UtsavNights.Core.Neighbourhood
{
    /// <summary>Whether a celebration spot is open now, and if not, why in plain words (PRD 10: greyed with a reason).</summary>
    public readonly struct SpotAvailability
    {
        public SpotAvailability(bool open, string reason)
        {
            Open = open;
            Reason = reason;
        }

        public bool Open { get; }

        public string Reason { get; }
    }

    /// <summary>
    /// Celebration spots (Phase 8 step 4, PRD 10, F20): which spots are open for the player's house and settlement, the
    /// aerials rule at each, and its launch pads. The tags come from the house (has_rooftop, has_balcony; the courtyard
    /// always) and the settlement (square, open_lot).
    /// </summary>
    public static class CelebrationRules
    {
        /// <summary>How close to a launch pad an aerial must stand, in metres.</summary>
        public const float PadReach = 0.45f;

        public static HashSet<string> Tags(SettlementLayout layout, HouseDesign home)
        {
            HashSet<string> tags = WalkNavigator.HomeTags(home);
            if (layout.open.kind == SpotKinds.Square)
            {
                tags.Add(SpotTags.Square);
            }

            foreach (LotDef lot in layout.lots)
            {
                if (lot.vacant)
                {
                    tags.Add(SpotTags.OpenLot);
                }
            }

            return tags;
        }

        public static SpotAvailability Availability(SettlementLayout layout, SpotDef spot, HouseDesign home)
        {
            if (spot == null)
            {
                return new SpotAvailability(false, "That place isn't in this neighbourhood.");
            }

            HashSet<string> tags = Tags(layout, home);
            foreach (string need in spot.requires ?? new string[0])
            {
                if (!tags.Contains(need))
                {
                    return new SpotAvailability(false, Missing(need));
                }
            }

            if (spot.kind == SpotKinds.Balcony && home?.ModuleAt("balcony")?.module == "balcony_jharokha")
            {
                return new SpotAvailability(false, "The jharokha's canopy hangs too low for fireworks. Choose an open balcony in decoration mode.");
            }

            return new SpotAvailability(true, string.Empty);
        }

        public static AerialRule RuleOf(SpotDef spot) => spot?.aerials switch
        {
            Aerials.Pads => AerialRule.Pads,
            Aerials.None => AerialRule.None,
            _ => AerialRule.Open,
        };

        /// <summary>True when the point (world x, z) is within reach of one of the spot's pads.</summary>
        public static bool OnPad(SpotDef spot, float x, float z)
        {
            foreach (LaunchPad pad in spot?.pads ?? new List<LaunchPad>())
            {
                float dx = pad.x - x, dz = pad.z - z;
                if (dx * dx + dz * dz <= PadReach * PadReach)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>True when the point is inside the spot's play area: within its radius, at its height.</summary>
        public static bool InArea(SpotDef spot, V3 p)
        {
            float dx = p.X - spot.x, dz = p.Z - spot.z;
            return Math.Abs(p.Y - spot.y) < 0.6f && dx * dx + dz * dz <= spot.radius * spot.radius;
        }

        /// <summary>
        /// True when strung lights or garlands (the player's path decorations, house space) pass within <paramref name="clearance"/>
        /// of the vertical line above a house-space point: aerials would fly into them (PRD 10: wires).
        /// </summary>
        public static bool StrandOverhead(HouseDesign design, V3 housePoint, float clearance = 0.5f)
        {
            foreach (PlacedDecor d in design?.decorations ?? new List<PlacedDecor>())
            {
                DecorInfo info = HomeCatalog.Decoration(d.item);
                if (info == null || info.Mode != PlacementMode.Path)
                {
                    continue;
                }

                var a = new V3(d.x, d.y, d.z);
                var b = new V3(d.ex, d.ey, d.ez);
                for (int i = 0; i <= 16; i++)
                {
                    V3 p = DecorGenerator.PathPoint(a, b, d.sag, i / 16f);
                    float dx = p.X - housePoint.X, dz = p.Z - housePoint.Z;
                    if (p.Y > housePoint.Y && dx * dx + dz * dz < clearance * clearance)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static string Missing(string tag) => tag switch
        {
            SpotTags.HasRooftop => "Your roof slopes. Choose a flat roof or a roof terrace in decoration mode to celebrate up there.",
            SpotTags.HasBalcony => "Your house has no balcony. Add one in decoration mode.",
            SpotTags.Square => "This neighbourhood has no square.",
            SpotTags.OpenLot => "There's no empty plot here.",
            _ => "Your house has no courtyard.",
        };
    }
}

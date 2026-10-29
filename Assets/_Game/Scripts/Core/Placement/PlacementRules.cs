namespace UtsavNights.Core.Placement
{
    /// <summary>Why a spot is not valid. The UI shows <see cref="PlacementRules.Message"/> next to the red ghost.</summary>
    public enum PlacementBlock
    {
        None = 0,
        NoSurface = 1,
        WrongSurface = 2,
        TooSteep = 3,
        OutOfReach = 4,
        TooManyItems = 5,
        FootprintBlocked = 6,
        TooCloseToItem = 7,
        NoRoomForEffect = 8,

        // Phase 8: celebration spots (PRD 10).
        OutsideArea = 9,
        AerialNotHere = 10,
        AerialPadsOnly = 11,
        NoSky = 12,
    }

    /// <summary>Where aerials may go up at the current celebration spot (PRD 10).</summary>
    public enum AerialRule
    {
        /// <summary>Anywhere in the area with clear sky above.</summary>
        Open = 0,

        /// <summary>Only from the spot's launch pads.</summary>
        Pads = 1,

        /// <summary>Not at all (a balcony).</summary>
        None = 2,
    }

    /// <summary>Facts about one candidate spot, gathered by the Unity-side validator (raycasts, overlaps).</summary>
    public struct PlacementFacts
    {
        public bool HitSurface;

        /// <summary>Bit flags of the surface hit (same values as the definition's PlacementSurface).</summary>
        public int SurfaceFlags;

        /// <summary>Bit flags the cracker allows.</summary>
        public int AllowedSurfaces;

        public float SlopeDegrees;
        public bool InReach;
        public int ItemsPlaced;
        public int ItemLimit;
        public bool FootprintClear;

        /// <summary>Distance to the nearest placed cracker on the ground plane, or a large value when none.</summary>
        public float NearestItemDistance;

        public float MinimumSpacing;
        public bool EffectVolumeClear;

        /// <summary>Outside the current celebration spot's play area.</summary>
        public bool OutsideArea;

        /// <summary>The cracker goes up into the sky (rockets, sky shots, cakes).</summary>
        public bool IsAerial;

        public AerialRule AerialRule;

        /// <summary>Within reach of one of the spot's launch pads.</summary>
        public bool OnPad;

        /// <summary>Nothing overhead: no roof, eave, balcony or strung lights above the point.</summary>
        public bool SkyClear;
    }

    /// <summary>
    /// Placement validation (PRD 6.3, 9.1, 10): checks run in a fixed order so the player sees the most
    /// useful reason first. Engine-free and allocation-free.
    /// </summary>
    public static class PlacementRules
    {
        /// <summary>Support normal must be within about 20 degrees of up (PRD 9.1).</summary>
        public const float MaxSlopeDegrees = 20f;

        public static PlacementBlock Evaluate(in PlacementFacts f)
        {
            if (!f.HitSurface)
            {
                return PlacementBlock.NoSurface;
            }

            if ((f.SurfaceFlags & f.AllowedSurfaces) == 0)
            {
                return PlacementBlock.WrongSurface;
            }

            if (f.SlopeDegrees > MaxSlopeDegrees)
            {
                return PlacementBlock.TooSteep;
            }

            if (!f.InReach)
            {
                return PlacementBlock.OutOfReach;
            }

            if (f.OutsideArea)
            {
                return PlacementBlock.OutsideArea;
            }

            if (f.IsAerial)
            {
                if (f.AerialRule == AerialRule.None)
                {
                    return PlacementBlock.AerialNotHere;
                }

                if (f.AerialRule == AerialRule.Pads && !f.OnPad)
                {
                    return PlacementBlock.AerialPadsOnly;
                }

                if (!f.SkyClear)
                {
                    return PlacementBlock.NoSky;
                }
            }

            if (f.ItemLimit > 0 && f.ItemsPlaced >= f.ItemLimit)
            {
                return PlacementBlock.TooManyItems;
            }

            if (!f.FootprintClear)
            {
                return PlacementBlock.FootprintBlocked;
            }

            if (f.NearestItemDistance < f.MinimumSpacing)
            {
                return PlacementBlock.TooCloseToItem;
            }

            if (!f.EffectVolumeClear)
            {
                return PlacementBlock.NoRoomForEffect;
            }

            return PlacementBlock.None;
        }

        /// <summary>Plain-language reason for the player (English; localisation later).</summary>
        public static string Message(PlacementBlock block)
        {
            switch (block)
            {
                case PlacementBlock.None: return string.Empty;
                case PlacementBlock.NoSurface: return "Place it on the ground";
                case PlacementBlock.WrongSurface: return "This one can't go here";
                case PlacementBlock.TooSteep: return "Find a flat spot";
                case PlacementBlock.OutOfReach: return "Too far away";
                case PlacementBlock.TooManyItems: return "Light some crackers first";
                case PlacementBlock.FootprintBlocked: return "Something is in the way";
                case PlacementBlock.TooCloseToItem: return "Too close to another cracker";
                case PlacementBlock.NoRoomForEffect: return "Not enough room for the sparks";
                case PlacementBlock.OutsideArea: return "Stay inside the celebration area";
                case PlacementBlock.AerialNotHere: return "Rockets and sky shots can't go up from here";
                case PlacementBlock.AerialPadsOnly: return "Rockets go on the launch pads here";
                case PlacementBlock.NoSky: return "Something is overhead. Rockets need open sky";
                default: return "Can't place here";
            }
        }

        /// <summary>Items allowed in the scene at once per tier (Phase 3 plan; tune after device tests).</summary>
        public static int ItemLimit(int tier)
        {
            switch (tier)
            {
                case 0: return 6;
                case 1: return 10;
                default: return 14;
            }
        }
    }
}

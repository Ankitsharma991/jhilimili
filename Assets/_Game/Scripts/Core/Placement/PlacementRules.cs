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

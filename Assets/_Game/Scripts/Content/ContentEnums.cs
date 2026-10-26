using System;

namespace UtsavNights.Content
{
    /// <summary>Interaction class and camera preset family (PRD 7). Ground and aerial items behave differently.</summary>
    public enum CrackerCategory
    {
        Sparkler = 0,
        GroundFountain = 1,
        GroundSpinner = 2,
        ChainCrackers = 3,
        SingleBang = 4,
        Rocket = 5,
        Skyshot = 6,
        MultiShotCake = 7,
    }

    public enum Intensity
    {
        Gentle = 0,
        Medium = 1,
        Loud = 2,
    }

    [Flags]
    public enum PlacementSurface
    {
        None = 0,
        Ground = 1 << 0,
        Rooftop = 1 << 1,
        Hand = 1 << 2,
        LaunchPad = 1 << 3,
    }

    [Flags]
    public enum IgnitionMethod
    {
        None = 0,
        Lighter = 1 << 0,
        Match = 1 << 1,
    }

    /// <summary>The three haptic patterns of PRD 11.</summary>
    public enum HapticPattern
    {
        None = 0,
        Tick = 1,
        Thud = 2,
        Rumble = 3,
    }

    public enum CameraCue
    {
        None = 0,
        PushInSmall = 1,
        AerialFollow = 2,
        Shake = 3,
    }

    /// <summary>Where a stage's emitter starts: the cracker's base (pivot) or its fuse anchor.</summary>
    public enum StageAnchor
    {
        Base = 0,
        Fuse = 1,
    }

    /// <summary>Matches the Low / Mid / High quality levels created by project setup.</summary>
    public enum QualityTier
    {
        Low = 0,
        Mid = 1,
        High = 2,
    }

    public static class CrackerCategoryExtensions
    {
        public static bool IsAerial(this CrackerCategory category)
        {
            return category == CrackerCategory.Rocket || category == CrackerCategory.Skyshot ||
                   category == CrackerCategory.MultiShotCake;
        }
    }
}

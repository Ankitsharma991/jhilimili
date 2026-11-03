using System;

namespace UtsavNights.Core.Explore
{
    /// <summary>
    /// The player's own record (Phase 12 step 2): coins earned in the town, reputation in the neighbourhood, and what
    /// they've done. Kept on the phone in player.json and in backups. The avatar's name stays in the avatar.
    /// </summary>
    [Serializable]
    public sealed class PlayerRecord
    {
        public const int CurrentVersion = 1;

        /// <summary>Caps so a damaged or edited file can't hold absurd numbers.</summary>
        public const long MaxCoins = 9_999_999;
        public const int MaxReputation = 999_999;

        public int version = CurrentVersion;
        public long coins;
        public int reputation;
        public long firstPlayedUnix;
        public int sessions;

        // Stats.
        public float metresExplored;
        public float secondsExplored;
        public int jumps;
        public int crackersLit;
        public int showsPlayed;
        public int midnights;
        public int homesVisited;
        public int jobsDone;

        public PlayerRecord Clone() => (PlayerRecord)MemberwiseClone();

        public bool Migrate()
        {
            if (version >= CurrentVersion)
            {
                return false;
            }

            version = CurrentVersion;
            return true;
        }

        /// <summary>Clamps every number into range (never negative, never past the caps). True when anything changed.</summary>
        public bool Sanitize()
        {
            PlayerRecord before = Clone();
            coins = Math.Max(0, Math.Min(MaxCoins, coins));
            reputation = Math.Max(0, Math.Min(MaxReputation, reputation));
            sessions = Math.Max(0, sessions);
            metresExplored = float.IsNaN(metresExplored) || metresExplored < 0f ? 0f : Math.Min(1e8f, metresExplored);
            secondsExplored = float.IsNaN(secondsExplored) || secondsExplored < 0f ? 0f : Math.Min(1e9f, secondsExplored);
            jumps = Math.Max(0, jumps);
            crackersLit = Math.Max(0, crackersLit);
            showsPlayed = Math.Max(0, showsPlayed);
            midnights = Math.Max(0, midnights);
            homesVisited = Math.Max(0, homesVisited);
            jobsDone = Math.Max(0, jobsDone);
            firstPlayedUnix = Math.Max(0, firstPlayedUnix);
            return coins != before.coins || reputation != before.reputation || sessions != before.sessions || metresExplored != before.metresExplored
                || secondsExplored != before.secondsExplored || jumps != before.jumps || crackersLit != before.crackersLit || showsPlayed != before.showsPlayed
                || midnights != before.midnights || homesVisited != before.homesVisited || jobsDone != before.jobsDone || firstPlayedUnix != before.firstPlayedUnix;
        }

        /// <summary>Earns coins (never past the cap). Returns what was actually added.</summary>
        public long Earn(long amount)
        {
            if (amount <= 0)
            {
                return 0;
            }

            long added = Math.Min(amount, MaxCoins - coins);
            coins += added;
            return added;
        }

        /// <summary>Spends coins when there are enough; false (nothing spent) otherwise.</summary>
        public bool Spend(long amount)
        {
            if (amount < 0 || amount > coins)
            {
                return false;
            }

            coins -= amount;
            return true;
        }

        public void GainReputation(int amount) => reputation = Math.Max(0, Math.Min(MaxReputation, reputation + Math.Max(0, amount)));
    }

    /// <summary>Reputation levels with their titles: how the neighbourhood knows the player.</summary>
    public static class Reputation
    {
        /// <summary>The reputation needed for each level (level 1 at 0).</summary>
        public static readonly int[] Thresholds = { 0, 100, 300, 700, 1500, 3000, 6000, 12000 };

        public static readonly string[] Titles = { "Newcomer", "Neighbour", "Helping hand", "Lamp-lighter", "Show-maker", "Festival favourite", "Pride of the town", "Utsav legend" };

        public static int Level(int reputation)
        {
            int level = 1;
            for (int i = 1; i < Thresholds.Length; i++)
            {
                if (reputation >= Thresholds[i])
                {
                    level = i + 1;
                }
            }

            return level;
        }

        public static string Title(int reputation) => Titles[Level(reputation) - 1];

        /// <summary>0 to 1 of the way to the next level (1 at the top level).</summary>
        public static float Progress(int reputation)
        {
            int level = Level(reputation);
            if (level >= Thresholds.Length)
            {
                return 1f;
            }

            int from = Thresholds[level - 1], to = Thresholds[level];
            return (reputation - from) / (float)(to - from);
        }

        /// <summary>Reputation still needed for the next level (0 at the top).</summary>
        public static int ToNext(int reputation)
        {
            int level = Level(reputation);
            return level >= Thresholds.Length ? 0 : Thresholds[level] - reputation;
        }
    }

    /// <summary>The control settings' ranges (stored in <see cref="Settings.GameSettings"/>).</summary>
    public static class ControlRanges
    {
        public const float MinLook = 0.4f;
        public const float MaxLook = 2.5f;
        public const float MinHud = 0.8f;
        public const float MaxHud = 1.3f;

        /// <summary>A slider's 0 to 1 as a look sensitivity, and back.</summary>
        public static float Look(float slider) => MinLook + Math.Max(0f, Math.Min(1f, slider)) * (MaxLook - MinLook);

        public static float LookSlider(float look) => (Math.Max(MinLook, Math.Min(MaxLook, look)) - MinLook) / (MaxLook - MinLook);

        public static float Hud(float slider) => MinHud + Math.Max(0f, Math.Min(1f, slider)) * (MaxHud - MinHud);

        public static float HudSlider(float hud) => (Math.Max(MinHud, Math.Min(MaxHud, hud)) - MinHud) / (MaxHud - MinHud);
    }
}

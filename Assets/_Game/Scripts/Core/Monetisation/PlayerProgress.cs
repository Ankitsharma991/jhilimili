using System;
using System.Collections.Generic;

namespace UtsavNights.Core.Monetisation
{
    /// <summary>
    /// progress.json (PRD 13.5): purchases cache and unlocks. Public fields for JsonUtility; run
    /// <see cref="Sanitize"/> after loading. Times are Unix seconds (UTC) so unlocks survive restarts.
    /// </summary>
    [Serializable]
    public sealed class PlayerProgress
    {
        public const int CurrentVersion = 1;

        /// <summary>PRD 15.1 rewarded unlock length (owner decision: 24 h).</summary>
        public const long RewardedUnlockSeconds = 24 * 60 * 60;

        public int version = CurrentVersion;

        /// <summary>Remove ads and unlock all (F11); cached from the store, confirmed by Restore.</summary>
        public bool adsRemoved;

        /// <summary>Product id of the confirmed purchase (no order id or token: nothing personal is kept).</summary>
        public string purchasedSku = string.Empty;

        public List<TimedUnlock> unlocks = new List<TimedUnlock>();

        [Serializable]
        public sealed class TimedUnlock
        {
            public string id = string.Empty;
            public long expiresUnix;
        }

        public bool IsUnlocked(string crackerId, long nowUnix)
        {
            if (adsRemoved)
            {
                return true;
            }

            foreach (TimedUnlock unlock in unlocks)
            {
                if (unlock.id == crackerId && unlock.expiresUnix > nowUnix)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Seconds left on a rewarded unlock, 0 when none.</summary>
        public long SecondsLeft(string crackerId, long nowUnix)
        {
            long best = 0;
            foreach (TimedUnlock unlock in unlocks)
            {
                if (unlock.id == crackerId)
                {
                    best = Math.Max(best, unlock.expiresUnix - nowUnix);
                }
            }

            return best;
        }

        /// <summary>Grants (or renews) a 24 h unlock from now. Watching again restarts the 24 h, never stacks.</summary>
        public void GrantRewardedUnlock(string crackerId, long nowUnix)
        {
            if (string.IsNullOrEmpty(crackerId))
            {
                return;
            }

            unlocks.RemoveAll(u => u.id == crackerId);
            unlocks.Add(new TimedUnlock { id = crackerId, expiresUnix = nowUnix + RewardedUnlockSeconds });
        }

        public void SetPurchased(string sku)
        {
            adsRemoved = true;
            purchasedSku = sku ?? string.Empty;
        }

        /// <summary>A refund or a Restore that finds nothing clears the cache.</summary>
        public void ClearPurchase()
        {
            adsRemoved = false;
            purchasedSku = string.Empty;
        }

        public bool Migrate()
        {
            if (version >= CurrentVersion)
            {
                return false;
            }

            version = CurrentVersion;
            return true;
        }

        /// <summary>
        /// Drops expired, empty and duplicate unlocks, and caps any unlock that ends more than 24 h from
        /// now (a clock moved backwards, or an edited file). Returns true if anything changed.
        /// </summary>
        public bool Sanitize(long nowUnix)
        {
            bool changed = false;
            if (unlocks == null)
            {
                unlocks = new List<TimedUnlock>();
                changed = true;
            }

            if (purchasedSku == null)
            {
                purchasedSku = string.Empty;
                changed = true;
            }

            var seen = new HashSet<string>();
            for (int i = unlocks.Count - 1; i >= 0; i--)
            {
                TimedUnlock u = unlocks[i];
                if (u == null || string.IsNullOrEmpty(u.id) || u.expiresUnix <= nowUnix || !seen.Add(u.id))
                {
                    unlocks.RemoveAt(i);
                    changed = true;
                    continue;
                }

                if (u.expiresUnix > nowUnix + RewardedUnlockSeconds)
                {
                    u.expiresUnix = nowUnix + RewardedUnlockSeconds;
                    changed = true;
                }
            }

            return changed;
        }
    }
}

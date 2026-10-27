using System;
using UtsavNights.Core.Monetisation;

namespace UtsavNights.Gameplay
{
    /// <summary>What the player owns: the Remove-ads purchase and rewarded unlocks. Saves on every change.</summary>
    public sealed class ProgressService
    {
        private readonly ProgressStore _store;
        private readonly Func<long> _clock;

        public ProgressService(ProgressStore store, Func<long> clock = null)
        {
            _store = store;
            _clock = clock ?? (() => DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            Current = store != null ? store.Load(NowUnix) : new PlayerProgress();
        }

        /// <summary>Raised after any change (unlock granted, purchase confirmed or cleared).</summary>
        public event Action Changed;

        public PlayerProgress Current { get; }

        public long NowUnix => _clock();

        public bool AdsRemoved => Current.adsRemoved;

        /// <summary>Unlocked when the catalog does not lock it, or by purchase, or by a running rewarded unlock.</summary>
        public bool IsUnlocked(string crackerId, bool lockedInCatalog) => !lockedInCatalog || Current.IsUnlocked(crackerId, NowUnix);

        public long SecondsLeft(string crackerId) => Current.SecondsLeft(crackerId, NowUnix);

        public void GrantRewardedUnlock(string crackerId)
        {
            Current.GrantRewardedUnlock(crackerId, NowUnix);
            Commit();
        }

        public void SetPurchased(string sku)
        {
            if (Current.adsRemoved && Current.purchasedSku == sku)
            {
                return;
            }

            Current.SetPurchased(sku);
            Commit();
        }

        public void ClearPurchase()
        {
            if (!Current.adsRemoved)
            {
                return;
            }

            Current.ClearPurchase();
            Commit();
        }

        /// <summary>Removes every rewarded unlock (support and tests); the purchase is untouched.</summary>
        public void ClearUnlocks()
        {
            Current.unlocks.Clear();
            Commit();
        }

        private void Commit()
        {
            _store?.Save(Current);
            Changed?.Invoke();
        }
    }
}

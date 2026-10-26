using System;

namespace UtsavNights.Core.Effects
{
    /// <summary>
    /// Scene-wide particle allowance (PRD 14 per-tier caps). Each effect reserves its maximum when it
    /// starts and releases it when it ends, so the total never exceeds the cap. When the budget is tight
    /// an effect gets a smaller share instead of failing, down to a minimum below which it is skipped.
    /// Lowering the cap (quality governor) affects new effects only; running ones finish normally.
    /// </summary>
    public sealed class ParticleBudget
    {
        private int _cap;

        public ParticleBudget(int cap)
        {
            Cap = cap;
        }

        public int Cap
        {
            get => _cap;
            set => _cap = Math.Max(0, value);
        }

        public int Reserved { get; private set; }

        public int Remaining => Math.Max(0, _cap - Reserved);

        /// <summary>
        /// Reserves up to <paramref name="desired"/> particles. Returns the granted amount, or 0 when even
        /// <paramref name="minimum"/> does not fit (the caller should skip or simplify the effect).
        /// </summary>
        public int Reserve(int desired, int minimum)
        {
            if (desired <= 0)
            {
                return 0;
            }

            int floor = Math.Max(1, Math.Min(minimum, desired));
            int grant = Math.Min(desired, Remaining);
            if (grant < floor)
            {
                return 0;
            }

            Reserved += grant;
            return grant;
        }

        public void Release(int amount)
        {
            if (amount > 0)
            {
                Reserved = Math.Max(0, Reserved - amount);
            }
        }

        public void ReleaseAll() => Reserved = 0;
    }
}

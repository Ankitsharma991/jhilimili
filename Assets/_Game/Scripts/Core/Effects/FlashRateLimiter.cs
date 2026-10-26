using System;

namespace UtsavNights.Core.Effects
{
    /// <summary>
    /// Allows at most N flashes in any rolling time window (PRD 6.2: cap at 3 flashes per second).
    /// Protects photosensitive users regardless of how many crackers go off at once. No allocations.
    /// </summary>
    public sealed class FlashRateLimiter
    {
        private readonly double[] _times;
        private readonly double _windowSeconds;
        private int _head;
        private int _count;

        public FlashRateLimiter(int maxPerWindow = 3, double windowSeconds = 1.0)
        {
            if (maxPerWindow <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPerWindow));
            }

            if (!(windowSeconds > 0.0))
            {
                throw new ArgumentOutOfRangeException(nameof(windowSeconds));
            }

            _times = new double[maxPerWindow];
            _windowSeconds = windowSeconds;
        }

        public int MaxPerWindow => _times.Length;

        /// <summary>Records and allows a flash at <paramref name="now"/> if the window has room.</summary>
        public bool TryAcquire(double now)
        {
            Evict(now);
            if (_count == _times.Length)
            {
                return false;
            }

            _times[_head] = now;
            _head = (_head + 1) % _times.Length;
            _count++;
            return true;
        }

        /// <summary>Flashes still counted in the window ending at <paramref name="now"/>.</summary>
        public int CountInWindow(double now)
        {
            Evict(now);
            return _count;
        }

        public void Reset()
        {
            _head = 0;
            _count = 0;
        }

        private void Evict(double now)
        {
            while (_count > 0)
            {
                int oldest = (_head - _count + _times.Length) % _times.Length;
                if (now - _times[oldest] < _windowSeconds)
                {
                    break;
                }

                _count--;
            }
        }
    }
}

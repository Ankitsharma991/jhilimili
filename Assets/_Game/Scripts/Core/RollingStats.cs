using System;

namespace UtsavNights.Core
{
    /// <summary>
    /// Fixed-capacity ring buffer of float samples (for example frame times in ms).
    /// Allocates only in the constructor, so it is safe to feed from Update.
    /// </summary>
    public sealed class RollingStats
    {
        private readonly float[] _samples;
        private readonly float[] _scratch;
        private int _count;
        private int _head;
        private double _sum;

        public RollingStats(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");
            }

            _samples = new float[capacity];
            _scratch = new float[capacity];
        }

        public int Capacity => _samples.Length;

        public int Count => _count;

        public float Average => _count == 0 ? 0f : (float)(_sum / _count);

        public float Min
        {
            get
            {
                if (_count == 0)
                {
                    return 0f;
                }

                float min = float.MaxValue;
                for (int i = 0; i < _count; i++)
                {
                    if (_samples[i] < min)
                    {
                        min = _samples[i];
                    }
                }

                return min;
            }
        }

        public float Max
        {
            get
            {
                if (_count == 0)
                {
                    return 0f;
                }

                float max = float.MinValue;
                for (int i = 0; i < _count; i++)
                {
                    if (_samples[i] > max)
                    {
                        max = _samples[i];
                    }
                }

                return max;
            }
        }

        public void Add(float value)
        {
            if (_count == _samples.Length)
            {
                _sum -= _samples[_head];
            }
            else
            {
                _count++;
            }

            _samples[_head] = value;
            _sum += value;
            _head = (_head + 1) % _samples.Length;
        }

        /// <summary>Nearest-rank percentile, p in [0, 100]. Sorts a scratch copy; call at low frequency.</summary>
        public float Percentile(float p)
        {
            if (_count == 0)
            {
                return 0f;
            }

            if (p <= 0f)
            {
                return Min;
            }

            Array.Copy(_samples, _scratch, _count);
            Array.Sort(_scratch, 0, _count);
            int rank = (int)Math.Ceiling(p / 100f * _count) - 1;
            if (rank < 0)
            {
                rank = 0;
            }
            else if (rank >= _count)
            {
                rank = _count - 1;
            }

            return _scratch[rank];
        }

        public int CountAbove(float threshold)
        {
            int n = 0;
            for (int i = 0; i < _count; i++)
            {
                if (_samples[i] > threshold)
                {
                    n++;
                }
            }

            return n;
        }

        /// <summary>Percentage of samples strictly above the threshold, 0 when empty.</summary>
        public float PercentAbove(float threshold)
        {
            return _count == 0 ? 0f : 100f * CountAbove(threshold) / _count;
        }

        public void Clear()
        {
            _count = 0;
            _head = 0;
            _sum = 0d;
        }
    }
}

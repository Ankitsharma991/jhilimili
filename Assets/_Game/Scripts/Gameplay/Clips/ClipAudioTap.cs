using UnityEngine;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Copies what the player hears (the mix at the AudioListener) while a clip records (Phase 9 step 6). Runs on the audio
    /// thread; the recorder drains it on the main thread. Two seconds are kept; anything older is dropped, never blocked on.
    /// </summary>
    [RequireComponent(typeof(AudioListener))]
    public sealed class ClipAudioTap : MonoBehaviour
    {
        public const int Channels = 2;

        private readonly object _lock = new object();
        private float[] _ring = new float[1];
        private int _start;
        private int _count;
        private volatile bool _active;

        /// <summary>Samples dropped because the main thread fell behind (a long hitch while recording).</summary>
        public int Dropped { get; private set; }

        public void Begin(int sampleRate)
        {
            lock (_lock)
            {
                _ring = new float[sampleRate * Channels * 2];
                _start = 0;
                _count = 0;
                Dropped = 0;
            }

            _active = true;
        }

        public void End() => _active = false;

        /// <summary>Moves the waiting samples (interleaved stereo) into <paramref name="into"/>; returns how many.</summary>
        public int Drain(float[] into)
        {
            lock (_lock)
            {
                int n = Mathf.Min(_count, into.Length);
                for (int i = 0; i < n; i++)
                {
                    into[i] = _ring[(_start + i) % _ring.Length];
                }

                _start = (_start + n) % _ring.Length;
                _count -= n;
                return n;
            }
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!_active || channels <= 0)
            {
                return;
            }

            lock (_lock)
            {
                int frames = data.Length / channels;
                for (int f = 0; f < frames; f++)
                {
                    // Down- or up-mix to stereo: the first two channels, or the one channel twice.
                    float left = data[f * channels];
                    float right = channels > 1 ? data[f * channels + 1] : left;
                    Push(left);
                    Push(right);
                }
            }
        }

        private void Push(float v)
        {
            if (_count == _ring.Length)
            {
                _start = (_start + 1) % _ring.Length;
                _count--;
                Dropped++;
            }

            _ring[(_start + _count) % _ring.Length] = v;
            _count++;
        }
    }
}

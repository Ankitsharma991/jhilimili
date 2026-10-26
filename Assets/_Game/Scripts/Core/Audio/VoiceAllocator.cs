using System;

namespace UtsavNights.Core.Audio
{
    /// <summary>
    /// Decides which voice plays a new sound (PRD 11: voice cap per tier, priority). Free voices first;
    /// when full, steal the least important voice, oldest first among equals. A sound never steals a
    /// more important one, so a fuse hiss cannot cut a bang. Engine-free and allocation-free.
    /// </summary>
    public sealed class VoiceAllocator
    {
        private readonly Voice[] _voices;
        private int _serial;

        public VoiceAllocator(int voiceCount)
        {
            if (voiceCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(voiceCount));
            }

            _voices = new Voice[voiceCount];
        }

        public int Capacity => _voices.Length;

        public int ActiveCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _voices.Length; i++)
                {
                    if (_voices[i].Active)
                    {
                        n++;
                    }
                }

                return n;
            }
        }

        /// <summary>Times a sound was dropped because every voice was more important.</summary>
        public int Rejected { get; private set; }

        /// <summary>Times a less important voice was cut to make room.</summary>
        public int Stolen { get; private set; }

        private struct Voice
        {
            public bool Active;
            public int Priority;
            public int Serial;
        }

        /// <summary>Returns the voice index to use, or -1 when the sound should be skipped.</summary>
        /// <param name="priority">Higher is more important.</param>
        public int Allocate(int priority)
        {
            int victim = -1;
            for (int i = 0; i < _voices.Length; i++)
            {
                if (!_voices[i].Active)
                {
                    return Claim(i, priority);
                }

                if (victim < 0 || IsLessImportant(i, victim))
                {
                    victim = i;
                }
            }

            if (_voices[victim].Priority > priority)
            {
                Rejected++;
                return -1;
            }

            Stolen++;
            return Claim(victim, priority);
        }

        public void Release(int index)
        {
            if (index >= 0 && index < _voices.Length)
            {
                _voices[index].Active = false;
            }
        }

        public bool IsActive(int index) => index >= 0 && index < _voices.Length && _voices[index].Active;

        public int PriorityOf(int index) => _voices[index].Priority;

        public void ReleaseAll()
        {
            for (int i = 0; i < _voices.Length; i++)
            {
                _voices[i].Active = false;
            }
        }

        private bool IsLessImportant(int a, int b)
        {
            if (_voices[a].Priority != _voices[b].Priority)
            {
                return _voices[a].Priority < _voices[b].Priority;
            }

            return _voices[a].Serial < _voices[b].Serial;
        }

        private int Claim(int index, int priority)
        {
            _voices[index].Active = true;
            _voices[index].Priority = priority;
            _voices[index].Serial = ++_serial;
            return index;
        }
    }
}

using System;
using System.Collections.Generic;

namespace UtsavNights.Core.Diagnostics
{
    /// <summary>
    /// One tester's blind sound test (F03 acceptance: 6 of 8 crackers identified by sound alone). Plays
    /// every cracker once in a shuffled order; the tester names each one or says "not sure".
    /// </summary>
    public sealed class BlindSoundTest
    {
        public const int PassCorrect = 6;

        /// <summary>Guess id recorded when the tester is not sure.</summary>
        public const string NotSure = "not_sure";

        private readonly string[] _order;
        private readonly List<Answer> _answers = new List<Answer>();
        private int _replays;

        public BlindSoundTest(IReadOnlyList<string> crackerIds, int seed)
        {
            if (crackerIds == null || crackerIds.Count == 0)
            {
                throw new ArgumentException("at least one cracker id is needed", nameof(crackerIds));
            }

            _order = new string[crackerIds.Count];
            for (int i = 0; i < _order.Length; i++)
            {
                _order[i] = crackerIds[i];
            }

            // Fisher-Yates with a recorded seed, so a session can be replayed exactly.
            var random = new Random(seed);
            for (int i = _order.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (_order[i], _order[j]) = (_order[j], _order[i]);
            }

            Seed = seed;
        }

        public readonly struct Answer
        {
            public Answer(int index, string playedId, string guessId, int replays)
            {
                Index = index;
                PlayedId = playedId;
                GuessId = guessId;
                Replays = replays;
            }

            public int Index { get; }

            public string PlayedId { get; }

            public string GuessId { get; }

            /// <summary>Extra plays the tester asked for before answering.</summary>
            public int Replays { get; }

            public bool Correct => PlayedId == GuessId;
        }

        public int Seed { get; }

        public int Count => _order.Length;

        /// <summary>Zero-based position of the sound being asked about; equals Count when complete.</summary>
        public int Index => _answers.Count;

        public bool IsComplete => _answers.Count >= _order.Length;

        /// <summary>Cracker id of the sound being asked about; null when complete.</summary>
        public string Current => IsComplete ? null : _order[_answers.Count];

        public IReadOnlyList<Answer> Answers => _answers;

        public int CorrectCount
        {
            get
            {
                int n = 0;
                foreach (Answer a in _answers)
                {
                    if (a.Correct)
                    {
                        n++;
                    }
                }

                return n;
            }
        }

        /// <summary>Pass threshold scaled to the catalog size (6 of 8).</summary>
        public int RequiredCorrect => (int)Math.Ceiling(PassCorrect / 8.0 * _order.Length);

        public bool Passed => IsComplete && CorrectCount >= RequiredCorrect;

        public string PlayedAt(int index) => _order[index];

        public void RecordReplay()
        {
            if (!IsComplete)
            {
                _replays++;
            }
        }

        /// <returns>The recorded answer.</returns>
        public Answer Guess(string guessId)
        {
            if (IsComplete)
            {
                throw new InvalidOperationException("the test is already complete");
            }

            var answer = new Answer(_answers.Count, _order[_answers.Count], string.IsNullOrEmpty(guessId) ? NotSure : guessId, _replays);
            _answers.Add(answer);
            _replays = 0;
            return answer;
        }
    }
}

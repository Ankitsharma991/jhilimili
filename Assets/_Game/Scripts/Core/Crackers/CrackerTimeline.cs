using System;
using System.Collections.Generic;

namespace UtsavNights.Core.Crackers
{
    /// <summary>One timed cue on a cracker timeline. <see cref="StageIndex"/> points back at the definition's stage.</summary>
    public readonly struct TimelineCue
    {
        public TimelineCue(float time, CueKind kind, int stageIndex, int repeat = 0)
        {
            Time = time;
            Kind = kind;
            StageIndex = stageIndex;
            Repeat = repeat;
        }

        /// <summary>Which repetition of the stage this cue is (0 for stages that do not repeat).</summary>
        public int Repeat { get; }

        /// <summary>Seconds after ignition.</summary>
        public float Time { get; }

        public CueKind Kind { get; }

        public int StageIndex { get; }
    }

    /// <summary>
    /// Immutable, engine-free description of when things happen after ignition. Built from a
    /// CrackerDefinition and validated once, so the state machine can trust it.
    /// </summary>
    public sealed class CrackerTimeline
    {
        private readonly TimelineCue[] _cues;

        public CrackerTimeline(float fuseSeconds, float cooldownSeconds, IReadOnlyList<TimelineCue> cues)
        {
            List<string> errors = Validate(fuseSeconds, cooldownSeconds, cues);
            if (errors.Count > 0)
            {
                throw new ArgumentException(string.Join("; ", errors));
            }

            FuseSeconds = fuseSeconds;
            CooldownSeconds = cooldownSeconds;
            _cues = new TimelineCue[cues.Count];
            for (int i = 0; i < cues.Count; i++)
            {
                _cues[i] = cues[i];
            }

            StopSeconds = _cues[_cues.Length - 1].Time;
        }

        public float FuseSeconds { get; }

        public float CooldownSeconds { get; }

        /// <summary>Time of the final Stop cue, i.e. total effect length after ignition.</summary>
        public float StopSeconds { get; }

        public int CueCount => _cues.Length;

        public TimelineCue this[int index] => _cues[index];

        /// <summary>
        /// Returns every problem with the inputs (empty when valid). Shared with the content linter so
        /// editor messages and runtime checks never disagree.
        /// </summary>
        public static List<string> Validate(float fuseSeconds, float cooldownSeconds, IReadOnlyList<TimelineCue> cues)
        {
            var errors = new List<string>();
            if (!(fuseSeconds > 0f))
            {
                errors.Add($"fuse time must be greater than 0 (is {fuseSeconds})");
            }

            if (!(cooldownSeconds >= 0f))
            {
                errors.Add($"cooldown must be 0 or more (is {cooldownSeconds})");
            }

            if (cues == null || cues.Count == 0)
            {
                errors.Add("timeline needs at least one stage, ending with a stop stage");
                return errors;
            }

            int stops = 0;
            for (int i = 0; i < cues.Count; i++)
            {
                TimelineCue cue = cues[i];
                if (!(cue.Time >= 0f))
                {
                    errors.Add($"stage {cue.StageIndex} starts at {cue.Time} s; times must be 0 or more");
                }

                if (i > 0 && cue.Time < cues[i - 1].Time)
                {
                    errors.Add($"stage {cue.StageIndex} at {cue.Time} s is earlier than the stage before it");
                }

                if (cue.Kind == CueKind.Stop)
                {
                    stops++;
                }
            }

            TimelineCue last = cues[cues.Count - 1];
            if (stops != 1 || last.Kind != CueKind.Stop)
            {
                errors.Add("exactly one stop stage is required, and it must be the last stage");
            }
            else if (last.Time < fuseSeconds)
            {
                errors.Add($"stop at {last.Time} s comes before the fuse ends at {fuseSeconds} s");
            }

            return errors;
        }
    }
}

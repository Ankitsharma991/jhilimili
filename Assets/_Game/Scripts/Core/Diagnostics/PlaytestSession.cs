namespace UtsavNights.Core.Diagnostics
{
    /// <summary>
    /// Counts what a playtest session needs (PRD 17 metrics before real analytics exists):
    /// time to first ignition, ignitions, payoffs, flame-outs, and a frame-time window for periodic
    /// performance samples. All times are seconds on one clock (realtime since launch).
    /// </summary>
    public sealed class PlaytestSession
    {
        public const float DefaultSampleIntervalSeconds = 30f;

        private readonly RollingStats _frames = new RollingStats(4096);
        private float _sinceSample;

        public PlaytestSession(float startTime, float sampleIntervalSeconds = DefaultSampleIntervalSeconds)
        {
            StartTime = startTime;
            SampleIntervalSeconds = sampleIntervalSeconds > 0f ? sampleIntervalSeconds : DefaultSampleIntervalSeconds;
        }

        public float StartTime { get; }

        public float SampleIntervalSeconds { get; }

        /// <summary>When the main scene became interactive; -1 until then.</summary>
        public float SceneReadyTime { get; private set; } = -1f;

        public float FirstIgnitionTime { get; private set; } = -1f;

        public int Ignitions { get; private set; }

        public int Payoffs { get; private set; }

        public int FlameOuts { get; private set; }

        public int QualityChanges { get; private set; }

        /// <summary>Seconds from interactive scene to the first ignition (PRD 17 target: 30 s median); -1 if none.</summary>
        public float TimeToFirstIgnition =>
            FirstIgnitionTime >= 0f && SceneReadyTime >= 0f ? FirstIgnitionTime - SceneReadyTime : -1f;

        public void MarkSceneReady(float time)
        {
            if (SceneReadyTime < 0f)
            {
                SceneReadyTime = time;
            }
        }

        /// <returns>True for the first ignition of the session.</returns>
        public bool RecordIgnition(float time)
        {
            Ignitions++;
            if (FirstIgnitionTime >= 0f)
            {
                return false;
            }

            FirstIgnitionTime = time;
            if (SceneReadyTime < 0f)
            {
                SceneReadyTime = StartTime;
            }

            return true;
        }

        public void RecordPayoff() => Payoffs++;

        public void RecordFlameOut() => FlameOuts++;

        public void RecordQualityChange() => QualityChanges++;

        public float Duration(float now) => now - StartTime;

        /// <summary>Adds a frame; returns true when a performance sample is due.</summary>
        public bool AddFrame(float frameMs, float deltaSeconds)
        {
            if (frameMs > 0f && frameMs < 1000f)
            {
                _frames.Add(frameMs);
            }

            _sinceSample += deltaSeconds;
            if (_sinceSample < SampleIntervalSeconds)
            {
                return false;
            }

            _sinceSample = 0f;
            return true;
        }

        public float WindowAverageFps => SpikeMath.FpsFromFrameMs(_frames.Average);

        public float WindowP95Ms => _frames.Percentile(95f);

        public int WindowFrames => _frames.Count;

        public void ClearWindow() => _frames.Clear();
    }
}

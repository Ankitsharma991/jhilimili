using System;

namespace UtsavNights.Core.Haptics
{
    /// <summary>
    /// A vibration as Android expects it: segment durations in ms and amplitudes 0..255 (0 = off).
    /// Built once per pattern and intensity and cached by the service, so playing allocates nothing.
    /// </summary>
    public sealed class HapticWaveform
    {
        public HapticWaveform(long[] timingsMs, int[] amplitudes)
        {
            if (timingsMs == null || amplitudes == null || timingsMs.Length != amplitudes.Length || timingsMs.Length == 0)
            {
                throw new ArgumentException("timings and amplitudes must be non-empty and the same length");
            }

            TimingsMs = timingsMs;
            Amplitudes = amplitudes;
        }

        public long[] TimingsMs { get; }

        public int[] Amplitudes { get; }

        public long TotalMs
        {
            get
            {
                long total = 0;
                foreach (long t in TimingsMs)
                {
                    total += t;
                }

                return total;
            }
        }

        /// <summary>
        /// On/off pattern for devices without amplitude control: alternating off, on durations starting
        /// with an initial off delay, as Android's createWaveform(long[], int) expects.
        /// </summary>
        public long[] OnOffTimingsMs => _onOff ?? (_onOff = BuildOnOff());

        private long[] _onOff;

        private long[] BuildOnOff()
        {
            var result = new System.Collections.Generic.List<long> { 0 };
            bool on = false;
            for (int i = 0; i < TimingsMs.Length; i++)
            {
                bool segmentOn = Amplitudes[i] > 0;
                if (segmentOn == on)
                {
                    result[result.Count - 1] += TimingsMs[i];
                }
                else
                {
                    result.Add(TimingsMs[i]);
                    on = segmentOn;
                }
            }

            return result.ToArray();
        }

        public int PeakAmplitude
        {
            get
            {
                int peak = 0;
                foreach (int a in Amplitudes)
                {
                    peak = Math.Max(peak, a);
                }

                return peak;
            }
        }
    }

    /// <summary>
    /// The three PRD 11 patterns. Intensity 0..1 scales amplitude; any non-zero segment keeps at least
    /// amplitude 1 so a low slider never silently turns a pulse off.
    /// </summary>
    public static class HapticWaveforms
    {
        /// <summary>Fuse catching: one short, light pulse.</summary>
        public static HapticWaveform Tick(float intensity) => Build(intensity, new long[] { 18 }, new[] { 110 });

        /// <summary>Bang: a strong hit followed by a softer tail.</summary>
        public static HapticWaveform Thud(float intensity) =>
            Build(intensity, new long[] { 45, 25, 70 }, new[] { 255, 0, 120 });

        /// <summary>Fountain or cake roar: swells up, holds, fades (about 1.2 s).</summary>
        public static HapticWaveform Rumble(float intensity) =>
            Build(intensity, new long[] { 150, 150, 150, 150, 150, 150, 150, 150 }, new[] { 50, 90, 130, 160, 160, 130, 90, 50 });

        private static HapticWaveform Build(float intensity, long[] timings, int[] fullAmplitudes)
        {
            float scale = Math.Max(0f, Math.Min(1f, intensity));
            var amplitudes = new int[fullAmplitudes.Length];
            for (int i = 0; i < fullAmplitudes.Length; i++)
            {
                if (fullAmplitudes[i] <= 0 || scale <= 0f)
                {
                    amplitudes[i] = 0;
                    continue;
                }

                amplitudes[i] = Math.Max(1, Math.Min(255, (int)Math.Round(fullAmplitudes[i] * scale)));
            }

            return new HapticWaveform(timings, amplitudes);
        }
    }
}

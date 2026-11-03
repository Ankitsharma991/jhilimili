using System;

namespace UtsavNights.Core.Explore
{
    /// <summary>
    /// A footstep on packed earth and stone, synthesised (Phase 12 step 1): a soft low thud and a short gritty scuff, with
    /// a few variations so steps don't repeat exactly.
    /// </summary>
    public static class Footsteps
    {
        public const int Variations = 4;
        public const float Seconds = 0.14f;

        public static float[] Generate(int sampleRate, int variation)
        {
            var random = new Random(4401 + variation * 37);
            int n = (int)(sampleRate * Seconds);
            var data = new float[n];
            float low = 0f, scuff = 0f;
            float thudFreq = 70f + variation * 9f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sampleRate;
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                low += 0.08f * (white - low);
                scuff = 0.6f * scuff + 0.4f * white;
                float thud = (float)Math.Sin(2 * Math.PI * thudFreq * t) * (float)Math.Exp(-t * 45f);
                float grit = (scuff - low) * (float)Math.Exp(-t * 60f) * 0.35f;
                data[i] = 0.7f * thud + low * (float)Math.Exp(-t * 30f) + grit;
            }

            float peak = 0f;
            foreach (float s in data)
            {
                peak = Math.Max(peak, Math.Abs(s));
            }

            for (int i = 0; i < n; i++)
            {
                data[i] *= peak > 0f ? 0.7f / peak : 1f;
            }

            return data;
        }
    }
}

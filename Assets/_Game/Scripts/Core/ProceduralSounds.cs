using System;

namespace UtsavNights.Core
{
    /// <summary>
    /// Synthesised placeholder sounds for the Phase 1 audio spike. They are original
    /// (no licence needed) and deterministic for a given seed. Real cracker audio arrives in Phase 3.
    /// </summary>
    public static class ProceduralSounds
    {
        public const float PeakLevel = 0.9f;

        public enum Kind
        {
            Click = 0,
            Bang = 1,
            Whistle = 2,
            FuseHiss = 3,
            Crackle = 4,
            Whoosh = 5,
            Thump = 6,
            Pop = 7,
        }

        public static readonly Kind[] AllKinds =
        {
            Kind.Click, Kind.Bang, Kind.Whistle, Kind.FuseHiss, Kind.Crackle, Kind.Whoosh, Kind.Thump, Kind.Pop,
        };

        public static float DurationSeconds(Kind kind)
        {
            switch (kind)
            {
                case Kind.Click: return 0.04f;
                case Kind.Bang: return 0.9f;
                case Kind.Whistle: return 1.2f;
                case Kind.FuseHiss: return 1.0f;
                case Kind.Crackle: return 1.2f;
                case Kind.Whoosh: return 1.0f;
                case Kind.Thump: return 0.5f;
                case Kind.Pop: return 0.15f;
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        public static int SampleCount(Kind kind, int sampleRate)
        {
            return (int)Math.Round(DurationSeconds(kind) * sampleRate);
        }

        /// <summary>Generates mono samples in [-PeakLevel, PeakLevel].</summary>
        public static float[] Generate(Kind kind, int sampleRate, int seed)
        {
            if (sampleRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleRate), "Sample rate must be positive.");
            }

            var data = new float[SampleCount(kind, sampleRate)];
            var rng = new Random(seed);
            float dt = 1f / sampleRate;

            switch (kind)
            {
                case Kind.Click: FillClick(data, dt, rng); break;
                case Kind.Bang: FillBang(data, dt, rng); break;
                case Kind.Whistle: FillWhistle(data, dt); break;
                case Kind.FuseHiss: FillHiss(data, sampleRate, rng); break;
                case Kind.Crackle: FillCrackle(data, sampleRate, rng); break;
                case Kind.Whoosh: FillWhoosh(data, dt, rng); break;
                case Kind.Thump: FillThump(data, dt); break;
                case Kind.Pop: FillPop(data, dt, rng); break;
            }

            Normalize(data, PeakLevel);
            return data;
        }

        private static float Noise(Random rng) => (float)(rng.NextDouble() * 2.0 - 1.0);

        private static void FillClick(float[] d, float dt, Random rng)
        {
            for (int i = 0; i < d.Length; i++)
            {
                float t = i * dt;
                float env = (float)Math.Exp(-t * 250.0);
                d[i] = env * (0.6f * Noise(rng) + 0.6f * (float)Math.Sin(2.0 * Math.PI * 2200.0 * t));
            }
        }

        private static void FillBang(float[] d, float dt, Random rng)
        {
            float low = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i * dt;
                low += 0.08f * (Noise(rng) - low);
                float body = low * (float)Math.Exp(-t * 5.0) * 3f;
                float crack = Noise(rng) * (float)Math.Exp(-t * 40.0);
                float thump = (float)Math.Sin(2.0 * Math.PI * 55.0 * t) * (float)Math.Exp(-t * 7.0);
                d[i] = (float)Math.Tanh(body + crack + 0.9f * thump);
            }
        }

        private static void FillWhistle(float[] d, float dt)
        {
            double phase = 0.0;
            float duration = d.Length * dt;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i * dt;
                float u = t / duration;
                double freq = 2600.0 - 1300.0 * u;
                phase += 2.0 * Math.PI * freq * dt;
                float attack = Math.Min(1f, t / 0.05f);
                float release = Math.Min(1f, (duration - t) / 0.1f);
                float vibrato = 1f + 0.15f * (float)Math.Sin(2.0 * Math.PI * 9.0 * t);
                d[i] = attack * release * vibrato * (float)Math.Sin(phase);
            }
        }

        private static void FillHiss(float[] d, int sampleRate, Random rng)
        {
            // High-passed noise, cross-faded at the edges so it can loop without a click.
            float prev = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float n = Noise(rng);
                d[i] = n - 0.85f * prev;
                prev = n;
            }

            int fade = Math.Min(d.Length / 2, sampleRate / 100);
            for (int i = 0; i < fade; i++)
            {
                float g = (float)i / fade;
                d[i] *= g;
                d[d.Length - 1 - i] *= g;
            }
        }

        private static void FillCrackle(float[] d, int sampleRate, Random rng)
        {
            int popLength = Math.Max(1, sampleRate / 400);
            int nextPop = 0;
            for (int i = 0; i < d.Length; i++)
            {
                if (i >= nextPop)
                {
                    float amp = 0.4f + 0.6f * (float)rng.NextDouble();
                    int end = Math.Min(d.Length, i + popLength);
                    for (int j = i; j < end; j++)
                    {
                        float k = (float)(j - i) / popLength;
                        d[j] += amp * Noise(rng) * (1f - k) * (1f - k);
                    }

                    nextPop = i + (int)(sampleRate * (0.008 + rng.NextDouble() * 0.05));
                }
            }
        }

        private static void FillWhoosh(float[] d, float dt, Random rng)
        {
            float state = 0f;
            float duration = d.Length * dt;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i * dt;
                float u = t / duration;
                float coefficient = 0.02f + 0.3f * u;
                state += coefficient * (Noise(rng) - state);
                float env = (float)Math.Sin(Math.PI * u);
                d[i] = state * env;
            }
        }

        private static void FillThump(float[] d, float dt)
        {
            double phase = 0.0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i * dt;
                double freq = 40.0 + 60.0 * Math.Exp(-t * 12.0);
                phase += 2.0 * Math.PI * freq * dt;
                d[i] = (float)Math.Sin(phase) * (float)Math.Exp(-t * 9.0);
            }
        }

        private static void FillPop(float[] d, float dt, Random rng)
        {
            for (int i = 0; i < d.Length; i++)
            {
                float t = i * dt;
                float env = (float)Math.Exp(-t * 45.0);
                d[i] = env * (0.7f * Noise(rng) + 0.5f * (float)Math.Sin(2.0 * Math.PI * 420.0 * t));
            }
        }

        private static void Normalize(float[] d, float peak)
        {
            float max = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float a = Math.Abs(d[i]);
                if (a > max)
                {
                    max = a;
                }
            }

            if (max <= 0f)
            {
                return;
            }

            float gain = peak / max;
            for (int i = 0; i < d.Length; i++)
            {
                d[i] *= gain;
            }
        }
    }
}

using System;
using System.IO;

namespace UtsavNights.EditorTools.Main
{
    /// <summary>Writes mono 16-bit PCM WAV files (placeholder sounds generated in the editor).</summary>
    public static class WavWriter
    {
        public static void Write(string path, float[] samples, int sampleRate)
        {
            if (samples == null || samples.Length == 0)
            {
                throw new ArgumentException("no samples", nameof(samples));
            }

            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var w = new BinaryWriter(stream))
            {
                int dataBytes = samples.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + dataBytes);
                w.Write(new[] { 'W', 'A', 'V', 'E' });
                w.Write(new[] { 'f', 'm', 't', ' ' });
                w.Write(16);
                w.Write((short)1);
                w.Write((short)1);
                w.Write(sampleRate);
                w.Write(sampleRate * 2);
                w.Write((short)2);
                w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' });
                w.Write(dataBytes);
                foreach (float s in samples)
                {
                    float c = s < -1f ? -1f : s > 1f ? 1f : s;
                    w.Write((short)Math.Round(c * short.MaxValue));
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;

namespace UtsavNights.Core.Clips
{
    /// <summary>Where a clip is shared (F14: WhatsApp and Instagram, or anything through the share sheet).</summary>
    public enum ShareTarget
    {
        WhatsApp = 0,
        Instagram = 1,
        Other = 2,
    }

    /// <summary>A clip's encoded size and rate.</summary>
    public readonly struct ClipFormat
    {
        public ClipFormat(int width, int height, int fps, int bitrate)
        {
            Width = width;
            Height = height;
            Fps = fps;
            Bitrate = bitrate;
        }

        public int Width { get; }

        public int Height { get; }

        public int Fps { get; }

        public int Bitrate { get; }

        /// <summary>Bytes in one I420 frame: Y at full size, U and V at half size each way.</summary>
        public int FrameBytes => Width * Height * 3 / 2;
    }

    /// <summary>
    /// The rules for clips (Phase 9 step 6, F14 "a 10 to 20 second clip shares to WhatsApp and Instagram"): how long, how
    /// big, what the files are called and how many are kept.
    /// </summary>
    public static class ClipRules
    {
        public const float MinSeconds = 10f;
        public const float MaxSeconds = 20f;
        public const int LongSide = 1280;
        public const int Fps = 30;
        public const int KeptClips = 5;
        public const string Folder = "clips";

        /// <summary>The share text that goes with a clip.</summary>
        public const string ShareText = "Made in Utsav Nights";

        public static readonly Dictionary<ShareTarget, string> Packages = new Dictionary<ShareTarget, string>
        {
            [ShareTarget.WhatsApp] = "com.whatsapp",
            [ShareTarget.Instagram] = "com.instagram.android",
            [ShareTarget.Other] = string.Empty,
        };

        /// <summary>
        /// The clip's size for a screen: the same shape, the long side at most 1280, both sides multiples of 16 (what
        /// hardware encoders expect), and never larger than the screen.
        /// </summary>
        public static ClipFormat FormatFor(int screenWidth, int screenHeight)
        {
            int w = Math.Max(16, screenWidth), h = Math.Max(16, screenHeight);
            int longest = Math.Max(w, h);
            int sw = longest > LongSide ? (int)((long)w * LongSide / longest) : w;
            int sh = longest > LongSide ? (int)((long)h * LongSide / longest) : h;
            int cw = Math.Max(16, sw / 16 * 16);
            int ch = Math.Max(16, sh / 16 * 16);

            // About 0.12 bits per pixel per frame: clear fireworks at a size that shares quickly (about 8 Mbit/s at 720p).
            int bitrate = (int)Math.Min(12_000_000, Math.Max(2_000_000, cw * (long)ch * Fps * 0.3));
            return new ClipFormat(cw, ch, Fps, bitrate);
        }

        /// <summary>How long a show's clip records: the show plus a moment for the last sparks, within 10 to 20 seconds.</summary>
        public static float ShowClipSeconds(float showLength) => Math.Max(MinSeconds, Math.Min(MaxSeconds, showLength + 1f));

        /// <summary>True once a recording is long enough to keep (stopping earlier is refused, the PRD's 10 seconds).</summary>
        public static bool CanStop(float seconds) => seconds >= MinSeconds;

        /// <summary>A file name from the time: utsav_20261029_213142.mp4.</summary>
        public static string FileName(DateTime when) => $"utsav_{when:yyyyMMdd_HHmmss}.mp4";

        /// <summary>The clips to delete so that only the newest <see cref="KeptClips"/> stay (names sort by time).</summary>
        public static List<string> ToDelete(IEnumerable<string> names)
        {
            var list = new List<string>();
            foreach (string n in names)
            {
                if (n.StartsWith("utsav_", StringComparison.Ordinal) && n.EndsWith(".mp4", StringComparison.Ordinal))
                {
                    list.Add(n);
                }
            }

            list.Sort(StringComparer.Ordinal);
            list.Reverse();
            return list.Count > KeptClips ? list.GetRange(KeptClips, list.Count - KeptClips) : new List<string>();
        }

        /// <summary>
        /// Interleaved float samples (-1 to 1) as 16-bit little-endian PCM into <paramref name="output"/>; returns the bytes
        /// written. Out-of-range samples are clipped.
        /// </summary>
        public static int ToPcm16(float[] samples, int count, byte[] output, int offset = 0)
        {
            int n = Math.Min(count, (output.Length - offset) / 2);
            for (int i = 0; i < n; i++)
            {
                float f = samples[i];
                f = f > 1f ? 1f : f < -1f ? -1f : f;
                short s = (short)Math.Round(f * 32767f);
                output[offset + 2 * i] = (byte)(s & 0xff);
                output[offset + 2 * i + 1] = (byte)((s >> 8) & 0xff);
            }

            return n * 2;
        }

        /// <summary>Microseconds of audio in a number of interleaved samples.</summary>
        public static long AudioMicros(long samples, int sampleRate, int channels) => samples * 1_000_000L / ((long)sampleRate * channels);
    }

    /// <summary>
    /// BT.601 limited-range I420 (what H.264 encoders take): the CPU reference for the GPU conversion shader, the editor's
    /// fallback and the tests. RGB in 0 to 255, rows top to bottom.
    /// </summary>
    public static class I420
    {
        public static byte Y(float r, float g, float b) => Clamp(16f + 0.257f * r + 0.504f * g + 0.098f * b);

        public static byte U(float r, float g, float b) => Clamp(128f - 0.148f * r - 0.291f * g + 0.439f * b);

        public static byte V(float r, float g, float b) => Clamp(128f + 0.439f * r - 0.368f * g - 0.071f * b);

        /// <summary>RGB24 (top row first) to I420; U and V average each 2 x 2 block.</summary>
        public static byte[] FromRgb(byte[] rgb, int w, int h)
        {
            var o = new byte[w * h * 3 / 2];
            int cw = w / 2, uAt = w * h, vAt = uAt + cw * (h / 2);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = (y * w + x) * 3;
                    o[y * w + x] = Y(rgb[i], rgb[i + 1], rgb[i + 2]);
                }
            }

            for (int y = 0; y < h / 2; y++)
            {
                for (int x = 0; x < cw; x++)
                {
                    float r = 0f, g = 0f, b = 0f;
                    for (int k = 0; k < 4; k++)
                    {
                        int i = (((2 * y) + (k / 2)) * w + (2 * x) + (k % 2)) * 3;
                        r += rgb[i];
                        g += rgb[i + 1];
                        b += rgb[i + 2];
                    }

                    o[uAt + y * cw + x] = U(r / 4f, g / 4f, b / 4f);
                    o[vAt + y * cw + x] = V(r / 4f, g / 4f, b / 4f);
                }
            }

            return o;
        }

        /// <summary>I420 back to RGB24 (to look at a recorded frame).</summary>
        public static byte[] ToRgb(byte[] yuv, int w, int h)
        {
            var o = new byte[w * h * 3];
            int cw = w / 2, uAt = w * h, vAt = uAt + cw * (h / 2);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float c = yuv[y * w + x] - 16f;
                    float d = yuv[uAt + (y / 2) * cw + x / 2] - 128f;
                    float e = yuv[vAt + (y / 2) * cw + x / 2] - 128f;
                    int i = (y * w + x) * 3;
                    o[i] = Clamp(1.164f * c + 1.596f * e);
                    o[i + 1] = Clamp(1.164f * c - 0.392f * d - 0.813f * e);
                    o[i + 2] = Clamp(1.164f * c + 2.017f * d);
                }
            }

            return o;
        }

        /// <summary>A small RGB24 picture of a frame, every <paramref name="step"/>-th pixel each way (the share sheet's preview).</summary>
        public static byte[] Thumbnail(byte[] yuv, int w, int h, int step, out int tw, out int th)
        {
            tw = Math.Max(1, w / step);
            th = Math.Max(1, h / step);
            var o = new byte[tw * th * 3];
            int cw = w / 2, uAt = w * h, vAt = uAt + cw * (h / 2);
            for (int ty = 0; ty < th; ty++)
            {
                for (int tx = 0; tx < tw; tx++)
                {
                    int x = tx * step, y = ty * step;
                    float c = yuv[y * w + x] - 16f;
                    float d = yuv[uAt + (y / 2) * cw + x / 2] - 128f;
                    float e = yuv[vAt + (y / 2) * cw + x / 2] - 128f;
                    int i = (ty * tw + tx) * 3;
                    o[i] = Clamp(1.164f * c + 1.596f * e);
                    o[i + 1] = Clamp(1.164f * c - 0.392f * d - 0.813f * e);
                    o[i + 2] = Clamp(1.164f * c + 2.017f * d);
                }
            }

            return o;
        }

        /// <summary>
        /// Where output byte <paramref name="index"/> of a packed I420 frame takes its colour from, in source pixels (top row
        /// 0): the Y plane samples one pixel, U and V the centre of a 2 x 2 block. The shader does the same sum.
        /// </summary>
        public static (int plane, float x, float y) Source(int index, int w, int h)
        {
            int ySize = w * h, cSize = (w / 2) * (h / 2);
            if (index < ySize)
            {
                return (0, index % w + 0.5f, index / w + 0.5f);
            }

            int plane = index < ySize + cSize ? 1 : 2;
            int c = index - ySize - (plane == 2 ? cSize : 0);
            int cw = w / 2;
            return (plane, (c % cw) * 2f + 1f, (c / cw) * 2f + 1f);
        }

        private static byte Clamp(float v) => (byte)(v < 0f ? 0 : v > 255f ? 255 : (int)(v + 0.5f));
    }
}

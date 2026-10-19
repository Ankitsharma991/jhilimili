namespace UtsavNights.Core
{
    /// <summary>Small pure helpers used by the Phase 1 spikes and later by gameplay.</summary>
    public static class SpikeMath
    {
        /// <summary>Android baseline density (mdpi).</summary>
        public const float BaselineDpi = 160f;

        /// <summary>Used when the platform reports no DPI (Screen.dpi can be 0).</summary>
        public const float FallbackDpi = 420f;

        /// <summary>Converts density-independent pixels to screen pixels.</summary>
        public static float DpToPx(float dp, float dpi)
        {
            float effectiveDpi = dpi > 0f ? dpi : FallbackDpi;
            return dp * effectiveDpi / BaselineDpi;
        }

        /// <summary>
        /// Software output latency added by the mixer's buffers, in milliseconds:
        /// bufferLength * numBuffers / sampleRate. Excludes driver and hardware latency.
        /// </summary>
        public static float BufferLatencyMs(int bufferLength, int numBuffers, int sampleRate)
        {
            if (bufferLength <= 0 || numBuffers <= 0 || sampleRate <= 0)
            {
                return 0f;
            }

            return bufferLength * (float)numBuffers * 1000f / sampleRate;
        }

        /// <summary>Average frames per second for an average frame time in ms; 0 when unknown.</summary>
        public static float FpsFromFrameMs(float averageFrameMs)
        {
            return averageFrameMs > 0f ? 1000f / averageFrameMs : 0f;
        }
    }
}

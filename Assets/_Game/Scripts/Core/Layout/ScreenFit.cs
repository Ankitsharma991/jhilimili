using System;

namespace UtsavNights.Core.Layout
{
    /// <summary>
    /// Responsive sizing for every screen (PRD 12: 16:9 to 21:9, 5-inch phones, tablets). Engine-free.
    ///
    /// UI is designed in reference units where 1 dp = 2.625 units (a 1080 px, 420 dpi phone). The canvas
    /// scale makes UI the same physical size on every device (dp), but never so large that the layout stops
    /// fitting: the canvas always offers at least <see cref="MinLayoutWidth"/> x <see cref="MinLayoutHeight"/>
    /// units, which keeps the tray under 20 % of the height and the settings rows readable.
    /// </summary>
    public static class ScreenFit
    {
        public const float ReferenceUnitsPerDp = 2.625f;
        public const float FallbackReferenceWidth = 1080f;
        public const float MinLayoutWidth = 900f;
        public const float MinLayoutHeight = 1500f;

        /// <summary>Screens at least this wide in dp (shorter side) are tablets (Android sw600dp).</summary>
        public const float TabletMinWidthDp = 600f;

        /// <summary>UI is held further away on tablets; scale it up a little (still capped to fit).</summary>
        public const float TabletBoost = 1.25f;

        /// <summary>Canvas scale factor (screen pixels per reference unit).</summary>
        public static float CanvasScale(int screenWidth, int screenHeight, float dpi)
        {
            if (screenWidth <= 0 || screenHeight <= 0)
            {
                return 1f;
            }

            float physical = dpi > 0f
                ? dpi / 160f / ReferenceUnitsPerDp
                : screenWidth / FallbackReferenceWidth;
            if (dpi > 0f && IsTablet(screenWidth, screenHeight, dpi))
            {
                physical *= TabletBoost;
            }
            float fit = Math.Min(screenWidth / MinLayoutWidth, screenHeight / MinLayoutHeight);
            return Math.Max(0.05f, Math.Min(physical, fit));
        }

        public static bool IsTablet(int screenWidth, int screenHeight, float dpi)
        {
            return dpi > 0f && Math.Min(screenWidth, screenHeight) / (dpi / 160f) >= TabletMinWidthDp;
        }

        /// <summary>Canvas size in reference units for a scale.</summary>
        public static float CanvasUnits(int screenPixels, float scale) => scale > 0f ? screenPixels / scale : screenPixels;

        /// <summary>
        /// Vertical field of view that keeps at least <paramref name="minHorizontalDeg"/> visible across the
        /// screen (tall phones would otherwise crop the sides), never below the designed vertical FOV and
        /// never above <paramref name="maxVerticalDeg"/>.
        /// </summary>
        public static float VerticalFov(float aspect, float designVerticalDeg, float minHorizontalDeg, float maxVerticalDeg)
        {
            if (!(aspect > 0f))
            {
                return designVerticalDeg;
            }

            double halfH = minHorizontalDeg * 0.5 * Math.PI / 180.0;
            double neededV = 2.0 * Math.Atan(Math.Tan(halfH) / aspect) * 180.0 / Math.PI;
            float v = (float)Math.Max(designVerticalDeg, neededV);
            return Math.Min(v, maxVerticalDeg);
        }

        public static float HorizontalFov(float verticalDeg, float aspect)
        {
            double halfV = verticalDeg * 0.5 * Math.PI / 180.0;
            return (float)(2.0 * Math.Atan(Math.Tan(halfV) * aspect) * 180.0 / Math.PI);
        }
    }
}

using UnityEngine;

namespace UtsavNights.UI
{
    /// <summary>
    /// Visual direction from PRD 12: deep indigo, saffron, marigold, magenta accents, off-white UI.
    /// Sizes are in reference pixels for a 1080 x 1920 canvas scaled by width (about 2.6 px per dp).
    /// </summary>
    public static class UiTheme
    {
        public static readonly Color Indigo = new Color(0.07f, 0.05f, 0.18f, 0.92f);
        public static readonly Color IndigoLight = new Color(0.16f, 0.11f, 0.33f, 0.95f);
        public static readonly Color Saffron = new Color(1f, 0.6f, 0.2f);
        public static readonly Color Marigold = new Color(1f, 0.76f, 0.16f);
        public static readonly Color Magenta = new Color(0.92f, 0.22f, 0.6f);
        public static readonly Color OffWhite = new Color(1f, 0.97f, 0.9f);
        public static readonly Color Muted = new Color(1f, 0.97f, 0.9f, 0.55f);

        public const float ReferenceWidth = 1080f;
        public const float ReferenceHeight = 1920f;

        /// <summary>48 dp minimum touch target (PRD 12) in reference pixels.</summary>
        public const float MinTouch = 128f;

        public const float CornerRadius = 28f;
        public const float BodyText = 34f;
        public const float SmallText = 28f;
        public const float TitleText = 44f;
    }
}

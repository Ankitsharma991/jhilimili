using System;
using NUnit.Framework;
using UnityEngine;
using UtsavNights.UI;

namespace UtsavNights.Tests
{
    /// <summary>PRD 14: text and background pairs used by the UI meet WCAG 2.1 AA (4.5:1 for normal text).</summary>
    public class UiContrastTests
    {
        private const float NormalText = 4.5f;

        // Translucent panels are measured over black, which is what the night scene mostly is behind the UI.
        private static readonly object[] Pairs =
        {
            new object[] { "body on panel", UiTheme.OffWhite, UiTheme.Indigo },
            new object[] { "body on card", UiTheme.OffWhite, UiTheme.IndigoLight },
            new object[] { "secondary on panel", UiTheme.Muted, UiTheme.Indigo },
            new object[] { "secondary on card", UiTheme.Muted, UiTheme.IndigoLight },
            new object[] { "section header", UiTheme.Saffron, UiTheme.Indigo },
            new object[] { "title", UiTheme.Marigold, UiTheme.Indigo },
            new object[] { "selected label", UiTheme.Indigo, UiTheme.Saffron },
            new object[] { "finale running", UiTheme.Indigo, UiTheme.Magenta },
        };

        [TestCaseSource(nameof(Pairs))]
        public void MeetsAa(string use, Color text, Color background)
        {
            Color bg = Over(background, Color.black);
            float ratio = Ratio(Over(text, bg), bg);
            Assert.GreaterOrEqual(ratio, NormalText, $"{use}: {ratio:F2}:1");
        }

        [Test]
        public void OffWhiteOnSaffronIsKnownToFail()
        {
            // Documents why selected items switch their label to indigo.
            Assert.Less(Ratio(UiTheme.OffWhite, UiTheme.Saffron), NormalText);
        }

        private static Color Over(Color top, Color bottom) =>
            new Color(Mathf.Lerp(bottom.r, top.r, top.a), Mathf.Lerp(bottom.g, top.g, top.a), Mathf.Lerp(bottom.b, top.b, top.a), 1f);

        private static float Ratio(Color a, Color b)
        {
            float la = Luminance(a), lb = Luminance(b);
            return (Math.Max(la, lb) + 0.05f) / (Math.Min(la, lb) + 0.05f);
        }

        private static float Luminance(Color c) => 0.2126f * Linear(c.r) + 0.7152f * Linear(c.g) + 0.0722f * Linear(c.b);

        private static float Linear(float v) => v <= 0.04045f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
    }
}

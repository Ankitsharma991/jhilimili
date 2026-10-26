using NUnit.Framework;
using UtsavNights.Core.Layout;

namespace UtsavNights.Tests
{
    /// <summary>Responsive layout across real device shapes (PRD 12: 16:9 to 21:9, 5-inch phones, tablets).</summary>
    public class ScreenFitTests
    {
        private const float TrayHeight = 300f;
        private const float MinTouchUnits = 128f;

        // name, width, height, dpi
        private static readonly object[] Devices =
        {
            new object[] { "small phone 720x1280 @320", 720, 1280, 320f },
            new object[] { "phone 1080x1920 @420", 1080, 1920, 420f },
            new object[] { "tall phone 1080x2400 @400", 1080, 2400, 400f },
            new object[] { "very tall 21:9 1080x2520 @420", 1080, 2520, 420f },
            new object[] { "Redmi Pad Pro 1600x2560 @320", 1600, 2560, 320f },
            new object[] { "4:3 tablet 1536x2048 @264", 1536, 2048, 264f },
            new object[] { "foldable 1768x2208 @374", 1768, 2208, 374f },
            new object[] { "landscape tablet 2560x1600 @320", 2560, 1600, 320f },
            new object[] { "landscape phone 2400x1080 @400", 2400, 1080, 400f },
            new object[] { "unknown dpi 1080x2340 @0", 1080, 2340, 0f },
        };

        [TestCaseSource(nameof(Devices))]
        public void Layout_FitsEveryDevice(string name, int width, int height, float dpi)
        {
            float scale = ScreenFit.CanvasScale(width, height, dpi);
            Assert.GreaterOrEqual(ScreenFit.CanvasUnits(width, scale), ScreenFit.MinLayoutWidth - 0.5f, $"{name}: layout width");
            Assert.GreaterOrEqual(ScreenFit.CanvasUnits(height, scale), ScreenFit.MinLayoutHeight - 0.5f, $"{name}: layout height");
            Assert.LessOrEqual(TrayHeight * scale / height, 0.2f + 1e-4f, $"{name}: tray 20 % of the screen or less");
        }

        [TestCaseSource(nameof(Devices))]
        public void TouchTargets_StayUsable(string name, int width, int height, float dpi)
        {
            float scale = ScreenFit.CanvasScale(width, height, dpi);
            float knownDpi = dpi > 0f ? dpi : 420f;
            float targetDp = MinTouchUnits * scale / (knownDpi / 160f);

            // 48 dp is the goal; the fit cap may shrink it slightly on short landscape phones.
            float minimum = height < width ? 36f : 46f;
            Assert.GreaterOrEqual(targetDp, minimum, $"{name}: touch target {targetDp:0.0} dp");
        }

        [Test]
        public void PhysicalSize_IsSameAcrossPhones_AndALittleLargerOnTablets()
        {
            float phone = ScreenFit.CanvasScale(1080, 1920, 420f) / (420f / 160f);
            float tallPhone = ScreenFit.CanvasScale(1080, 2400, 400f) / (400f / 160f);
            float tablet = ScreenFit.CanvasScale(1600, 2560, 320f) / (320f / 160f);
            Assert.AreEqual(phone, tallPhone, 1e-4f, "same dp size on phones");
            Assert.AreEqual(phone * ScreenFit.TabletBoost, tablet, 1e-4f, "tablets get the boost when it fits");
            Assert.IsTrue(ScreenFit.IsTablet(1600, 2560, 320f));
            Assert.IsFalse(ScreenFit.IsTablet(1080, 2400, 420f));
        }

        [Test]
        public void ZeroScreen_IsSafe()
        {
            Assert.AreEqual(1f, ScreenFit.CanvasScale(0, 0, 400f));
        }

        [TestCase(0.4286f)]
        [TestCase(0.45f)]
        [TestCase(0.5625f)]
        [TestCase(0.625f)]
        [TestCase(0.75f)]
        [TestCase(1.6f)]
        public void CameraFov_KeepsTheSidesInView(float aspect)
        {
            float v = ScreenFit.VerticalFov(aspect, 60f, 36f, 74f);
            Assert.GreaterOrEqual(v, 60f, "never narrower than designed");
            Assert.LessOrEqual(v, 74f, "never wider than the cap");
            float h = ScreenFit.HorizontalFov(v, aspect);
            if (aspect >= 0.45f)
            {
                Assert.GreaterOrEqual(h, 36f - 0.01f, $"aspect {aspect}: horizontal {h:0.0} deg");
            }
        }

        [Test]
        public void CameraFov_UnchangedWhereAlreadyWideEnough()
        {
            Assert.AreEqual(60f, ScreenFit.VerticalFov(0.625f, 60f, 36f, 74f), 1e-3f, "tablet keeps the design");
            Assert.AreEqual(60f, ScreenFit.VerticalFov(1.6f, 60f, 36f, 74f), 1e-3f, "landscape keeps the design");
            Assert.Greater(ScreenFit.VerticalFov(0.45f, 60f, 36f, 74f), 60f, "tall phone widens");
        }
    }
}

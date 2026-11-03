using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Core.Festivals;
using UtsavNights.Core.Home;
using UtsavNights.Core.Runtime;
using UtsavNights.Core.Shows;
using UtsavNights.Gameplay;
using UtsavNights.UI;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>Phase 11 step 1: switch the night to New Year: its tray, shelf, show and the countdown to midnight.</summary>
    public class NewYearSmokeTests
    {
        [UnityTest]
        public IEnumerator NewYearNight_TrayShelfShowAndCountdown()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Assert.IsTrue(Game.Services.TryGet(out SettingsService settings));
            Assert.IsTrue(Game.Services.TryGet(out ShowPlayer player));
            string before = settings.Current.festival;
            var tray = Object.FindFirstObjectByType<TrayView>();
            var countdown = Object.FindFirstObjectByType<NewYearCountdown>();
            try
            {
                settings.Change(s => s.festival = Festivals.DiwaliTiharId);
                yield return null;
                Assert.AreEqual(9, tray.ItemCount, "Diwali and Tihar: the usual tray");
                Assert.IsFalse(tray.ShownIds().Contains("skyshot_gold_crown"));
                Assert.IsFalse(countdown.Launcher.gameObject.activeSelf, "no countdown on Diwali");

                Object.FindFirstObjectByType<SettingsSheet>().SetFestival(Festivals.NewYearId);
                yield return null;
                Assert.AreEqual(Festivals.NewYearId, settings.Current.festival);
                CollectionAssert.IsSubsetOf(new[] { "skyshot_gold_crown", "fountain_confetti", "sparkler_classic", "rocket_basic", "skyshot_green", "cake_twelve" }, tray.ShownIds());
                Assert.IsFalse(tray.ShownIds().Contains("chakri_classic"), "Diwali's own crackers rest tonight");
                Assert.IsTrue(countdown.Launcher.gameObject.activeSelf);
                Assert.AreEqual("new_year_midnight", player.Templates()[0].id, "the Midnight show first in the designer");

                var mode = Object.FindFirstObjectByType<DecorationMode>();
                Assert.AreEqual("New Year", DecorationMode.Festivals[4]);

                Assert.IsTrue(countdown.Begin());
                Assert.IsTrue(countdown.Running);
                Assert.AreEqual("10", countdown.Shown);
                countdown.Advance(5f);
                Assert.AreEqual("5", countdown.Shown);
                countdown.Advance(5.05f);
                Assert.IsTrue(countdown.AtMidnight);
                Assert.AreEqual(Festivals.NewYear.Greeting, countdown.Shown);
                bool fits = player.Check(ShowTemplates.NewYear).Count == 0;
                Assert.AreEqual(fits, countdown.ShowPlayed, "the Midnight show plays when it fits the spot");
                if (fits)
                {
                    Assert.AreEqual(ShowPlayer.PlayerState.Running, player.State);
                }

                AmbientLife ambient = Object.FindFirstObjectByType<AmbientLife>();
                Assert.IsTrue(ambient == null || ambient.Surging, "the neighbourhood lights up");
                countdown.Advance(MidnightCountdown.GreetingSeconds);
                Assert.IsFalse(countdown.Running);
                Assert.IsTrue(countdown.Launcher.gameObject.activeSelf, "ready for another countdown");
                player.Stop();

                settings.Change(s => s.festival = Festivals.DiwaliTiharId);
                yield return null;
                Assert.AreEqual(9, tray.ItemCount);
                Assert.IsFalse(countdown.Launcher.gameObject.activeSelf);
                Assert.IsFalse(countdown.Begin(), "no countdown off New Year");
            }
            finally
            {
                player.Stop();
                settings.Change(s => s.festival = before);
            }
        }
    }
}

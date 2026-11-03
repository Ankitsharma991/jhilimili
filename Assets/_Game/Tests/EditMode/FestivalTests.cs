using System;
using System.Linq;
using NUnit.Framework;
using UtsavNights.Core.Analytics;
using UtsavNights.Core.Festivals;
using UtsavNights.Core.Home;
using UtsavNights.Core.Settings;
using UtsavNights.Core.Shows;

namespace UtsavNights.Tests
{
    /// <summary>Phase 11 step 1: festivals as data, New Year's shelf and tray, the countdown and the cheer.</summary>
    public class FestivalTests
    {
        [Test]
        public void Registry_HasDiwaliTiharFirst_AndNewYear()
        {
            Assert.AreEqual(Festivals.DiwaliTiharId, Festivals.Default.Id);
            Assert.AreSame(Festivals.NewYear, Festivals.Find("new_year"));
            Assert.AreSame(Festivals.Default, Festivals.Find("eid"), "an unknown festival falls back to the default");
            Assert.IsTrue(Festivals.NewYear.Countdown);
            Assert.IsFalse(Festivals.DiwaliTihar.Countdown);
            foreach (FestivalInfo f in Festivals.All)
            {
                Assert.IsTrue(AnalyticsSchema.IsSafeId(f.Id), f.Id);
            }
        }

        [Test]
        public void Crackers_BelongByTheirTags()
        {
            Assert.IsTrue(Festivals.HasCracker(Festivals.NewYear, new[] { "diwali", "tihar", "new_year" }));
            Assert.IsFalse(Festivals.HasCracker(Festivals.NewYear, new[] { "diwali", "tihar" }));
            Assert.IsTrue(Festivals.HasCracker(Festivals.DiwaliTihar, new[] { "tihar" }));
            Assert.IsFalse(Festivals.HasCracker(Festivals.DiwaliTihar, new[] { "new_year" }));
            Assert.IsFalse(Festivals.HasCracker(Festivals.NewYear, null));
        }

        [Test]
        public void Shelf_ShowsTonightsDecorations_AndEverydayOnes()
        {
            DecorInfo balloons = HomeCatalog.Decoration("balloons_cluster");
            DecorInfo diya = HomeCatalog.Decoration("diya_clay");
            DecorInfo bench = HomeCatalog.Decoration("bench_wooden");
            Assert.IsTrue(Festivals.HasDecoration(Festivals.NewYear, balloons));
            Assert.IsFalse(Festivals.HasDecoration(Festivals.DiwaliTihar, balloons));
            Assert.IsFalse(Festivals.HasDecoration(Festivals.NewYear, diya));
            Assert.IsTrue(Festivals.HasDecoration(Festivals.NewYear, bench), "everyday items every night");
            int newYear = HomeCatalog.Decorations.Count(d => (d.Festivals & Festival.NewYear) != 0);
            Assert.GreaterOrEqual(newYear, 4, "lights, a lantern, bunting and balloons at least");
            foreach (string id in new[] { "string_lights_gold", "lantern_star_new_year", "bunting_party", "balloons_cluster" })
            {
                Assert.IsNotNull(HomeCatalog.Decoration(id), id);
                Assert.IsTrue(AnalyticsSchema.IsSafeId(id), id);
            }
        }

        [Test]
        public void Countdown_TenToOne_ThenMidnight()
        {
            Assert.AreEqual(10, MidnightCountdown.Shown(0f));
            Assert.AreEqual(10, MidnightCountdown.Shown(0.99f));
            Assert.AreEqual(9, MidnightCountdown.Shown(1f));
            Assert.AreEqual(1, MidnightCountdown.Shown(9.5f));
            Assert.AreEqual(0, MidnightCountdown.Shown(10f));
            Assert.IsFalse(MidnightCountdown.IsMidnight(9.99f));
            Assert.IsTrue(MidnightCountdown.IsMidnight(10f));
            Assert.AreEqual("11:59 pm", MidnightCountdown.Clock(3f));
            Assert.AreEqual("12:00 am", MidnightCountdown.Clock(10f));
        }

        [Test]
        public void Cheer_SwellsThenFades_WithoutClipping()
        {
            float[] cheer = CheerSound.Generate(44100, 2027);
            Assert.AreEqual(44100 * 5, cheer.Length);
            Assert.LessOrEqual(cheer.Max(Math.Abs), 0.81f);
            double Rms(float from, float to)
            {
                int a = (int)(from * 44100), b = (int)(to * 44100);
                double sum = 0;
                for (int i = a; i < b; i++)
                {
                    sum += cheer[i] * cheer[i];
                }

                return Math.Sqrt(sum / (b - a));
            }

            Assert.Less(Rms(0f, 0.2f), Rms(0.8f, 1.2f), "swells");
            Assert.Less(Rms(4.5f, 5f), Rms(0.8f, 1.2f), "fades");
            CollectionAssert.AreEqual(cheer, CheerSound.Generate(44100, 2027), "the same every time");
        }

        [Test]
        public void Settings_KeepAKnownFestival_AndMigrate()
        {
            var s = new GameSettings { festival = "eid" };
            Assert.IsTrue(s.Sanitize());
            Assert.AreEqual(Festivals.DiwaliTiharId, s.festival);
            s = new GameSettings { version = 2, festival = null };
            Assert.IsTrue(s.Migrate());
            s.Sanitize();
            Assert.AreEqual(GameSettings.CurrentVersion, s.version);
            Assert.AreEqual(Festivals.DiwaliTiharId, s.festival);
            var newYear = new GameSettings { festival = Festivals.NewYearId };
            Assert.IsFalse(newYear.Sanitize());
            var changes = SettingsDiff.Changes(new GameSettings(), newYear);
            Assert.IsTrue(changes.Exists(c => c.Key == "festival" && c.Value == "new_year"));
        }

        [Test]
        public void MidnightShow_UsesNewYearCrackers_OnePerPad()
        {
            ShowDef show = ShowTemplates.NewYear;
            Assert.AreEqual(6, show.pads.Count);
            Assert.AreEqual(show.pads.Count, show.cues.Select(c => c.pad).Distinct().Count(), "one cracker per pad");
            Assert.IsTrue(show.cues.Any(c => c.cracker == "skyshot_gold_crown"));
            Assert.IsTrue(show.cues.Any(c => c.cracker == "fountain_confetti"));
            CollectionAssert.AreEqual(new[] { show }, ShowTemplates.ForFestival(Festivals.NewYearId));
            CollectionAssert.IsEmpty(ShowTemplates.ForFestival(Festivals.DiwaliTiharId));
            CollectionAssert.DoesNotContain(ShowTemplates.All, show, "not in the everyday list");
        }
    }
}

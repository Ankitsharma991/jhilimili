using System.Collections.Generic;
using NUnit.Framework;
using UtsavNights.Core.Shows;

namespace UtsavNights.Tests
{
    /// <summary>Phase 9 step 4: the show rules (pads, spacing, pad reuse, aerials, the phone's item limit), templates, the library and ordering.</summary>
    public class ShowRulesTests
    {
        // Fuse, effect plus cooldown, aerial: stand-ins close to the real catalog.
        private static readonly Dictionary<string, CrackerTiming> Timings = new Dictionary<string, CrackerTiming>
        {
            ["sparkler_classic"] = new CrackerTiming(1f, 8f, false),
            ["flowerpot_gold"] = new CrackerTiming(2f, 7f, false),
            ["chakri_classic"] = new CrackerTiming(2f, 6f, false),
            ["chain_lar"] = new CrackerTiming(2f, 6f, false),
            ["rocket_basic"] = new CrackerTiming(1.5f, 4f, true),
            ["skyshot_green"] = new CrackerTiming(2f, 5f, true),
            ["cake_twelve"] = new CrackerTiming(2f, 9f, true),
        };

        private static CrackerTiming? Lookup(string id) => Timings.TryGetValue(id, out CrackerTiming t) ? t : (CrackerTiming?)null;

        private static List<string> Check(ShowDef s, int limit = 6, float radius = 2.3f, bool aerials = true) => ShowRules.Check(s, Lookup, limit, radius, aerials);

        private static ShowDef Two() => new ShowDef
        {
            id = "t",
            name = "Test",
            pads = ShowTemplates.Layout(2),
            cues = { new ShowCue { cracker = "flowerpot_gold", pad = 0, at = 0f }, new ShowCue { cracker = "rocket_basic", pad = 1, at = 3f } },
        };

        [Test]
        public void Templates_PassAtTheCourtyard_OnTheSmallestTier()
        {
            foreach (ShowDef t in ShowTemplates.All)
            {
                CollectionAssert.IsEmpty(Check(t), t.name);
            }
        }

        [Test]
        public void Layout_FitsTheRooftopAndKeepsPadsApart()
        {
            for (int n = 1; n <= ShowRules.MaxPads; n++)
            {
                ShowDef s = ShowTemplates.Blank("b", "Blank", n);
                s.cues.Add(new ShowCue { cracker = "sparkler_classic", pad = 0, at = 0f });
                CollectionAssert.IsEmpty(Check(s, radius: n <= 6 ? 1.6f : 2.3f), $"{n} pads");
            }
        }

        [Test]
        public void Pads_OutsideTheArea_OrTooClose_AreRefused()
        {
            ShowDef s = Two();
            s.pads[1].dx = 3f;
            Assert.IsTrue(Check(s).Exists(p => p.Contains("outside")));
            s = Two();
            s.pads[1].dx = s.pads[0].dx + 0.2f;
            s.pads[1].dz = s.pads[0].dz;
            Assert.IsTrue(Check(s).Exists(p => p.Contains("too close")));
        }

        [Test]
        public void APad_IsBusyUntilItsCrackerIsCleared()
        {
            ShowDef s = Two();
            s.cues[1].pad = 0;
            s.cues[1].cracker = "chakri_classic";
            s.cues[1].at = 5f;
            Assert.IsTrue(Check(s).Exists(p => p.Contains("pad 1 is still busy")), "the flowerpot holds pad 1 until 9 s");
            s.cues[1].at = 10f;
            CollectionAssert.IsEmpty(Check(s), "free again once the flowerpot is cleared and a second set aside for placing");
        }

        [Test]
        public void Aerials_AreRefusedWhereTheSpotAllowsNone()
        {
            Assert.IsTrue(Check(Two(), aerials: false).Exists(p => p.Contains("flies")));
            CollectionAssert.IsEmpty(Check(Two(), aerials: true));
        }

        [Test]
        public void ThePhonesItemLimit_CountsCrackersOutTogether()
        {
            ShowDef s = ShowTemplates.Blank("b", "Busy", 8);
            for (int i = 0; i < 8; i++)
            {
                s.cues.Add(new ShowCue { cracker = "sparkler_classic", pad = i, at = i * 0.5f });
            }

            Assert.IsTrue(Check(s, limit: 6).Exists(p => p.Contains("up to 6")));
            CollectionAssert.IsEmpty(Check(s, limit: 10));

            // Spread out, the same eight fit the smallest tier.
            for (int i = 0; i < 8; i++)
            {
                s.cues[i].at = i * 4f;
            }

            CollectionAssert.IsEmpty(Check(s, limit: 6));
        }

        [Test]
        public void UnknownCrackers_BadPads_TimesAndNames_AreRefused()
        {
            ShowDef s = Two();
            s.cues[0].cracker = "mystery";
            s.cues[1].pad = 5;
            s.name = "";
            List<string> p = Check(s);
            Assert.IsTrue(p.Exists(x => x.Contains("isn't a cracker")));
            Assert.IsTrue(p.Exists(x => x.Contains("doesn't exist")));
            Assert.IsTrue(p.Exists(x => x.Contains("name")));
            s = Two();
            s.cues[1].at = 500f;
            Assert.IsTrue(Check(s).Exists(x => x.Contains("shows run")));
            CollectionAssert.IsNotEmpty(Check(new ShowDef { name = "Empty", pads = ShowTemplates.Layout(1) }));
        }

        [Test]
        public void Order_AndShift_AndLength()
        {
            ShowDef s = Two();
            s.cues.Reverse();
            List<ShowCue> order = ShowRules.Order(s);
            Assert.AreEqual("flowerpot_gold", order[0].cracker);
            ShowRules.Shift(order[1], 1.26f);
            Assert.AreEqual(4.3f, order[1].at, 1e-4f, "delays snap to a tenth of a second");
            ShowRules.Shift(order[0], -5f);
            Assert.AreEqual(0f, order[0].at);
            Assert.AreEqual(4.3f + 1.5f + 4f, s.Length(Lookup), 1e-4f, "the rocket's end is the show's end");
        }

        [Test]
        public void Library_SavesCopies_ReplacesById_AndFills()
        {
            var lib = new ShowLibrary();
            ShowDef s = Two();
            Assert.IsTrue(lib.Put(s));
            s.name = "Changed";
            Assert.AreEqual("Test", lib.Find("t").name, "the library keeps a copy");
            Assert.IsTrue(lib.Put(s));
            Assert.AreEqual(1, lib.shows.Count);
            Assert.AreEqual("Changed", lib.Find("t").name);
            Assert.AreEqual("show_1", lib.NewId());
            for (int i = 1; i < ShowRules.MaxSavedShows; i++)
            {
                ShowDef more = Two();
                more.id = lib.NewId();
                Assert.IsTrue(lib.Put(more));
            }

            ShowDef extra = Two();
            extra.id = lib.NewId();
            Assert.IsFalse(lib.Put(extra), "full");
            Assert.IsTrue(lib.Remove("t"));
            Assert.IsTrue(lib.Put(extra));
        }
    }
}

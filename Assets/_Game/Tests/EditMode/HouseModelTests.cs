using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UtsavNights.Core.Home;

namespace UtsavNights.Tests
{
    /// <summary>Phase 7 step 1: the house model, exact undo and redo, the journal and its crash recovery (PRD 9.1, 13.5).</summary>
    public class HouseModelTests
    {
        [Test]
        public void Catalogue_IdsAreUniqueAndDefaultsFitTheirPoints()
        {
            Assert.AreEqual(HomeCatalog.Modules.Length, HomeCatalog.Modules.Select(m => m.Id).Distinct().Count());
            Assert.AreEqual(HomeCatalog.Decorations.Length, HomeCatalog.Decorations.Select(d => d.Id).Distinct().Count());
            Assert.AreEqual(HomeCatalog.Points.Length, HomeCatalog.Points.Select(p => p.Id).Distinct().Count());
            foreach (AttachmentPoint p in HomeCatalog.Points)
            {
                ModuleInfo m = HomeCatalog.Module(p.DefaultModule);
                Assert.IsNotNull(m, p.Id);
                Assert.AreEqual(p.Type, m.Type, p.Id);
            }

            foreach (ModuleType type in Enum.GetValues(typeof(ModuleType)))
            {
                Assert.GreaterOrEqual(HomeCatalog.Modules.Count(m => m.Type == type), 2, $"{type} needs a choice");
            }
        }

        [Test]
        public void Catalogue_EveryDecorationIsPlaceable()
        {
            foreach (DecorInfo d in HomeCatalog.Decorations)
            {
                Assert.AreNotEqual(Surface.None, d.Surfaces, d.Id);
                Assert.Greater(d.Width, 0f, d.Id);
                Assert.Greater(d.Depth, 0f, d.Id);
                Assert.Greater(d.Cost, 0, d.Id);
                Assert.Greater(d.Variants, 0, d.Id);
                Assert.GreaterOrEqual(d.MaxScale, 1f, d.Id);
                Assert.AreEqual(d.Mode == PlacementMode.Decal, d.Height == 0f, $"{d.Id}: only rangoli lie flat");
                Assert.AreNotEqual(Festival.None, d.Festivals, d.Id);
            }

            foreach (DecorCategory c in Enum.GetValues(typeof(DecorCategory)))
            {
                Assert.GreaterOrEqual(HomeCatalog.Decorations.Count(d => d.Category == c), 3, $"{c} needs at least three items");
            }

            foreach (Festival f in new[] { Festival.Diwali, Festival.Tihar, Festival.Everyday })
            {
                Assert.GreaterOrEqual(HomeCatalog.Decorations.Count(d => (d.Festivals & f) != 0), 8, $"{f} needs a full set");
            }
        }

        [Test]
        public void DefaultDesign_IsAlreadyClean()
        {
            HouseDesign d = HouseDesign.Default();
            Assert.AreEqual(HomeCatalog.Points.Length, d.modules.Count);
            Assert.IsFalse(d.Sanitize());
            Assert.IsFalse(HouseState.Fresh().Sanitize());
        }

        [Test]
        public void EveryCommand_UndoesExactlyAndRedoesExactly()
        {
            var rng = new Random(7);
            HouseDesign design = HouseDesign.Default();
            var history = new EditHistory(500);
            var before = new List<string>();
            var after = new List<string>();
            var kinds = new HashSet<string>();
            HouseDesign layout = null;

            for (int step = 0; step < 300; step++)
            {
                IHouseCommand c = RandomCommand(rng, design, layout);
                string fp = design.Fingerprint();
                if (!history.Do(c, design))
                {
                    Assert.AreEqual(fp, design.Fingerprint(), $"a refused {c.Kind} must change nothing");
                    continue;
                }

                kinds.Add(c.Kind);
                before.Add(fp);
                after.Add(design.Fingerprint());
                if (step == 120)
                {
                    layout = design.Clone();
                }
            }

            CollectionAssert.IsSupersetOf(kinds, new[] { "place", "remove", "pose", "module", "paint", "layout" });
            for (int i = before.Count - 1; i >= 0; i--)
            {
                Assert.IsTrue(history.Undo(design));
                Assert.AreEqual(before[i], design.Fingerprint(), $"undo of step {i}");
            }

            Assert.AreEqual(HouseDesign.Default().Fingerprint(), design.Fingerprint());
            for (int i = 0; i < after.Count; i++)
            {
                Assert.IsTrue(history.Redo(design));
                Assert.AreEqual(after[i], design.Fingerprint(), $"redo of step {i}");
            }
        }

        [Test]
        public void History_KeepsFiftyStepsAndANewEditClearsRedo()
        {
            HouseDesign design = HouseDesign.Default();
            var history = new EditHistory();
            for (int i = 0; i < 60; i++)
            {
                Assert.IsTrue(history.Do(PlaceDecor.New(design, Diya(i * 0.2f, 1f)), design));
            }

            Assert.AreEqual(50, history.UndoCount);
            while (history.Undo(design))
            {
            }

            Assert.AreEqual(10, design.decorations.Count, "the ten oldest steps fell off");
            Assert.AreEqual(50, history.RedoCount);
            Assert.IsTrue(history.Do(new SetPaint(2, 1, 0), design));
            Assert.AreEqual(0, history.RedoCount);
        }

        [Test]
        public void RefusedEdits_ChangeNothing()
        {
            HouseDesign design = HouseDesign.Default();
            string fp = design.Fingerprint();
            Assert.IsFalse(new SetModule("door", "window_jali").Apply(design), "a window does not fit a door");
            Assert.IsFalse(new SetModule("door", string.Empty).Apply(design), "the door is not optional");
            Assert.IsFalse(new SetModule("door", "door_carved_arch", 9).Apply(design), "no such variant");
            Assert.IsFalse(new SetModule("chimney", "door_carved_arch").Apply(design));
            Assert.IsFalse(new SetPaint(99, 0, 0).Apply(design));
            Assert.IsFalse(new RemoveDecor(42).Apply(design));
            Assert.IsFalse(new PoseDecor(42, Diya(0, 0)).Apply(design));
            Assert.IsFalse(new PlaceDecor(new PlacedDecor { uid = 1, item = "fountain_of_youth" }).Apply(design));
            Assert.AreEqual(fp, design.Fingerprint());
            Assert.IsTrue(new SetModule("balcony", string.Empty).Apply(design), "the balcony is optional");
        }

        [Test]
        public void Codec_RoundTripsEveryCommand()
        {
            HouseDesign design = HouseDesign.Default();
            new PlaceDecor(Diya(1f, 2f, uid: 3)).Apply(design);
            var odd = new PlacedDecor { uid = 9, item = "garland_marigold", x = -1.25e-3f, y = 2.5f, z = 1f / 3f, yaw = 359.99f, scale = 1.2f, surface = Surface.Wall, ex = 2f, ey = 2.4f, ez = 0.1f, sag = 0.3f };
            var commands = new IHouseCommand[]
            {
                new PlaceDecor(odd), new RemoveDecor(3), new PoseDecor(3, odd), new SetModule("balcony", string.Empty),
                new SetModule("door", "door_carved_arch", 2), new SetPaint(3, 4, 1), new LoadDesign(design),
            };
            foreach (IHouseCommand c in commands)
            {
                string text = HouseCommandCodec.Encode(c);
                StringAssert.DoesNotContain("\n", text);
                IHouseCommand back = HouseCommandCodec.Decode(text);
                Assert.IsNotNull(back, text);
                Assert.AreEqual(text, HouseCommandCodec.Encode(back));
            }

            Assert.AreEqual("a|b%c\n", new CommandReader(Write(w => w.Str("a|b%c\n"))).Str(), "separators in a string are escaped");
            Assert.IsNull(HouseCommandCodec.Decode("place|1|diya_clay"), "too few fields");
            Assert.IsNull(HouseCommandCodec.Decode("paint|1|2|3|4"), "too many fields");
            Assert.IsNull(HouseCommandCodec.Decode("teleport|1"));
            Assert.IsNull(HouseCommandCodec.Decode("paint|1|x|3"));
        }

        [Test]
        public void Codec_IgnoresTheDeviceCulture()
        {
            System.Globalization.CultureInfo saved = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                // Decimal commas (Hindi uses a point, but many other locales do not).
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
                string text = HouseCommandCodec.Encode(new PlaceDecor(Diya(1.5f, 2.25f, uid: 4)));
                StringAssert.Contains("1.5", text);
                Assert.IsNotNull(HouseCommandCodec.Decode(text));
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = saved;
            }
        }

        [Test]
        public void Journal_ReplaysAKilledSessionExactly()
        {
            var journal = new MemoryJournalStore();
            HouseState onDisk = HouseState.Fresh();
            onDisk.saveSerial = 4;
            var live = new HouseEditor(onDisk.Clone(), journal);
            Assert.AreEqual(0, live.Recover());
            Edit(live, new Random(3), 80);

            // The app is killed: nothing saved, only the journal survives.
            var survived = new MemoryJournalStore();
            survived.Lines.AddRange(journal.Lines);
            var restored = new HouseEditor(onDisk.Clone(), survived);
            Assert.Greater(journal.Lines.Count, 40);
            Assert.AreEqual(journal.Lines.Count - 1, restored.Recover());
            Assert.AreEqual(live.Design.Fingerprint(), restored.Design.Fingerprint());
            Assert.AreEqual(live.UndoCount, restored.UndoCount, "the undo history comes back too");
            Assert.AreEqual(live.RedoCount, restored.RedoCount);
            CollectionAssert.AreEqual(journal.Lines, survived.Lines, "recovery rewrites the same journal");

            // A second kill right after recovery loses nothing either.
            var again = new HouseEditor(onDisk.Clone(), survived);
            again.Recover();
            Assert.AreEqual(restored.Design.Fingerprint(), again.Design.Fingerprint());

            while (live.Undo())
            {
                Assert.IsTrue(again.Undo());
                Assert.AreEqual(live.Design.Fingerprint(), again.Design.Fingerprint());
            }
        }

        [Test]
        public void Journal_ATornLastLineIsDropped()
        {
            var journal = new MemoryJournalStore();
            var live = new HouseEditor(HouseState.Fresh(), journal);
            live.Recover();
            live.Do(PlaceDecor.New(live.Design, Diya(0f, 1f)));
            string oneItem = live.Design.Fingerprint();
            live.Do(PlaceDecor.New(live.Design, Diya(0.5f, 1f)));
            string last = journal.Lines[journal.Lines.Count - 1];
            journal.Lines[journal.Lines.Count - 1] = last.Substring(0, last.Length / 2);
            journal.Lines.Add(HouseJournal.Undid());

            var restored = new HouseEditor(HouseState.Fresh(), journal);
            Assert.AreEqual(1, restored.Recover(), "replay stops at the torn line");
            Assert.AreEqual(oneItem, restored.Design.Fingerprint());
            Assert.AreEqual(2, journal.Lines.Count, "the journal is rewritten without the torn line");
        }

        [Test]
        public void Journal_FromAnotherSaveIsNeverReplayed()
        {
            var journal = new MemoryJournalStore();
            HouseState state = HouseState.Fresh();
            var live = new HouseEditor(state, journal);
            live.Recover();
            live.Do(PlaceDecor.New(live.Design, Diya(0f, 1f)));

            // Killed after house.json was written but before the journal was reset.
            HouseState written = live.BeginSave().Clone();
            var restored = new HouseEditor(written, journal);
            Assert.AreEqual(0, restored.Recover(), "the journal names the older save");
            Assert.AreEqual(1, restored.Design.decorations.Count, "the edit is not applied twice");

            Assert.AreEqual(0, new HouseEditor(HouseState.Fresh(), new MemoryJournalStore()).Recover(), "an empty journal");
        }

        [Test]
        public void Save_StartsAFreshJournalAndClearsUndo()
        {
            var journal = new MemoryJournalStore();
            var editor = new HouseEditor(HouseState.Fresh(), journal);
            editor.Recover();
            Assert.IsFalse(editor.Unsaved);
            editor.Do(new SetPaint(3, 2, 1));
            Assert.IsTrue(editor.Unsaved);
            editor.BeginSave();
            editor.SaveWritten();
            Assert.IsFalse(editor.Unsaved);
            Assert.IsFalse(editor.CanUndo);
            Assert.AreEqual(new[] { HouseJournal.Base(1) }, journal.Lines.ToArray());

            editor.BeginSave();
            editor.SaveFailed();
            Assert.AreEqual(1, editor.State.saveSerial);
        }

        [Test]
        public void Layouts_ThreeFreeSlotsAndLoadingIsUndoable()
        {
            var editor = new HouseEditor(HouseState.Fresh(), new MemoryJournalStore());
            editor.Recover();
            editor.Do(new SetPaint(1, 1, 1));
            Assert.IsTrue(editor.SaveLayout(0, "  Diwali  "));
            editor.Do(PlaceDecor.New(editor.Design, Diya(0f, 1f)));
            Assert.IsTrue(editor.SaveLayout(1, ""));
            Assert.IsTrue(editor.SaveLayout(2, new string('x', 40)));
            Assert.IsFalse(editor.SaveLayout(3, "fourth"), "three layouts are free");
            Assert.IsFalse(editor.SaveLayout(5, "gap"));
            Assert.AreEqual("Diwali", editor.State.layouts[0].name);
            Assert.AreEqual("Layout 2", editor.State.layouts[1].name);
            Assert.AreEqual(HouseState.MaxLayoutName, editor.State.layouts[2].name.Length);

            string current = editor.Design.Fingerprint();
            Assert.IsTrue(editor.LoadLayout(0));
            Assert.AreEqual(0, editor.Design.decorations.Count);
            Assert.IsTrue(editor.Undo());
            Assert.AreEqual(current, editor.Design.Fingerprint());
            Assert.IsTrue(editor.State.DeleteLayout(1));
            Assert.AreEqual(2, editor.State.layouts.Count);
        }

        [Test]
        public void Duplicate_PlacesAnOffsetCopyWithANewUid()
        {
            var editor = new HouseEditor(HouseState.Fresh(), new MemoryJournalStore());
            editor.Do(PlaceDecor.New(editor.Design, new PlacedDecor { item = "string_lights_warm", x = 0, y = 2, z = 0, ex = 2, ey = 2, ez = 0, surface = Surface.Wall }));
            int uid = editor.Design.decorations[0].uid;
            Assert.IsTrue(editor.Duplicate(uid, 0.3f, 0f));
            PlacedDecor copy = editor.Design.decorations[1];
            Assert.AreNotEqual(uid, copy.uid);
            Assert.AreEqual(0.3f, copy.x, 1e-6f);
            Assert.AreEqual(2.3f, copy.ex, 1e-6f);
            Assert.IsFalse(editor.Duplicate(999, 0f, 0f));
        }

        [Test]
        public void Migrate_FromAnUnversionedSave()
        {
            var old = new HouseState { version = 0, layouts = null };
            old.design.decorations.Add(new PlacedDecor { item = "diya_clay" });
            old.design.decorations.Add(new PlacedDecor { item = "pot_marigold" });
            Assert.IsTrue(old.Migrate());
            Assert.AreEqual(HouseState.CurrentVersion, old.version);
            CollectionAssert.AreEqual(new[] { 1, 2 }, old.design.decorations.Select(d => d.uid).ToArray());
            Assert.AreEqual(3, old.design.nextUid);
            Assert.IsNotNull(old.layouts);
            Assert.IsFalse(old.Migrate());
        }

        [Test]
        public void Sanitize_RepairsAnEditedSave()
        {
            var s = new HouseState();
            s.design.wallColour = 99;
            s.design.modules[0].module = "window_jali";
            s.design.modules.RemoveAt(s.design.modules.Count - 1);
            s.design.decorations.Add(new PlacedDecor { uid = 5, item = "diya_clay", scale = 40f, yaw = -90f, surface = Surface.Wall | Surface.Ground });
            s.design.decorations.Add(new PlacedDecor { uid = 5, item = "pot_palm" });
            s.design.decorations.Add(new PlacedDecor { uid = 6, item = "unicorn" });
            s.design.decorations.Add(new PlacedDecor { uid = 7, item = "pot_palm", x = float.NaN });
            s.design.decorations.Add(null);
            s.design.nextUid = 1;
            for (int i = 0; i < 5; i++)
            {
                s.SaveLayout(Math.Min(i, s.layouts.Count), "l" + i, 9);
            }

            Assert.IsTrue(s.Sanitize());
            Assert.AreEqual(HomeCatalog.WallColours.Length - 1, s.design.wallColour);
            Assert.AreEqual("door_wooden_panel", s.design.ModuleAt("door").module, "a wrong-type module goes back to the default");
            Assert.IsNotNull(s.design.ModuleAt("gate"), "a missing point is filled");
            Assert.AreEqual(1, s.design.decorations.Count);
            PlacedDecor d = s.design.decorations[0];
            Assert.AreEqual(1.5f, d.scale);
            Assert.AreEqual(270f, d.yaw);
            Assert.AreEqual(Surface.Ground, d.surface, "one surface, and one the diya may use");
            Assert.AreEqual(6, s.design.nextUid);
            Assert.AreEqual(HouseState.FreeLayouts, s.layouts.Count);
            Assert.IsFalse(s.Sanitize());
        }

        private static PlacedDecor Diya(float x, float z, int uid = 0) => new PlacedDecor { uid = uid, item = "diya_clay", x = x, z = z };

        private static string Write(Action<CommandWriter> write)
        {
            var w = new CommandWriter();
            write(w);
            return w.ToString();
        }

        private static void Edit(HouseEditor editor, Random rng, int steps)
        {
            HouseDesign layout = null;
            for (int i = 0; i < steps; i++)
            {
                int roll = rng.Next(10);
                if (roll < 2)
                {
                    editor.Undo();
                }
                else if (roll < 3)
                {
                    editor.Redo();
                }
                else
                {
                    editor.Do(RandomCommand(rng, editor.Design, layout));
                }

                if (i == 30)
                {
                    layout = editor.Design.Clone();
                }
            }
        }

        private static IHouseCommand RandomCommand(Random rng, HouseDesign design, HouseDesign layout)
        {
            DecorInfo[] items = HomeCatalog.Decorations;
            int roll = rng.Next(layout != null ? 11 : 10);
            int count = design.decorations.Count;
            if (roll < 4 || count == 0)
            {
                DecorInfo info = items[rng.Next(items.Length)];
                return PlaceDecor.New(design, RandomPose(rng, info));
            }

            if (roll < 5)
            {
                return new RemoveDecor(design.decorations[rng.Next(count)].uid);
            }

            if (roll < 7)
            {
                PlacedDecor target = design.decorations[rng.Next(count)];
                return new PoseDecor(target.uid, RandomPose(rng, HomeCatalog.Decoration(target.item)));
            }

            if (roll < 9)
            {
                AttachmentPoint p = HomeCatalog.Points[rng.Next(HomeCatalog.Points.Length)];
                ModuleInfo[] fits = HomeCatalog.Modules.Where(m => m.Type == p.Type).ToArray();
                ModuleInfo m = fits[rng.Next(fits.Length)];
                return p.Optional && rng.Next(4) == 0 ? new SetModule(p.Id, string.Empty) : new SetModule(p.Id, m.Id, rng.Next(m.Variants));
            }

            if (roll < 10)
            {
                return new SetPaint(rng.Next(HomeCatalog.WallColours.Length), rng.Next(HomeCatalog.TrimColours.Length), rng.Next(HomeCatalog.Materials.Length));
            }

            return new LoadDesign(layout);
        }

        private static PlacedDecor RandomPose(Random rng, DecorInfo info) => new PlacedDecor
        {
            item = info.Id,
            variant = rng.Next(info.Variants),
            x = (float)(rng.NextDouble() * 8 - 4),
            y = (float)(rng.NextDouble() * 3),
            z = (float)(rng.NextDouble() * 6),
            yaw = (float)(rng.NextDouble() * 360),
            scale = HomeCatalog.MinScale + (float)rng.NextDouble() * (info.MaxScale - HomeCatalog.MinScale),
            surface = (Surface)((int)info.Surfaces & -(int)info.Surfaces),
            ex = (float)(rng.NextDouble() * 8 - 4),
            ey = (float)(rng.NextDouble() * 3),
            ez = (float)(rng.NextDouble() * 6),
            sag = (float)rng.NextDouble() * 0.5f,
        };
    }
}

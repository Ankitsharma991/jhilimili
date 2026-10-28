using System.IO;
using NUnit.Framework;
using UtsavNights.Core.Home;
using UtsavNights.Gameplay;

namespace UtsavNights.Tests
{
    /// <summary>Phase 7 step 1: house.json and house.journal on disk survive restarts, kills and damage (PRD 9.1, 13.5).</summary>
    public class HouseStoreTests
    {
        private string _dir;
        private HouseStore _store;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "utsav-house-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _store = new HouseStore(Path.Combine(_dir, HouseStore.FileName));
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_dir, true);

        [Test]
        public void NoFile_GivesTheDefaultHouse()
        {
            HouseEditor editor = _store.Open();
            Assert.AreEqual(HouseDesign.Default().Fingerprint(), editor.Design.Fingerprint());
            Assert.AreEqual(0, _store.Recovered);
            Assert.AreEqual(Path.Combine(_dir, HouseStore.JournalName), _store.Journal.Path);
        }

        [Test]
        public void SaveThenLoad_KeepsEverything()
        {
            HouseEditor editor = _store.Open();
            editor.Do(new SetModule("door", "door_carved_arch", 2));
            editor.Do(new SetPaint(3, 1, 2));
            editor.Do(PlaceDecor.New(editor.Design, new PlacedDecor { item = "garland_marigold", x = -1f, y = 2.2f, z = 0.05f, ex = 1f, ey = 2.2f, ez = 0.05f, sag = 0.2f, surface = Surface.Wall }));
            editor.SaveLayout(0, "दिवाली");
            Assert.IsTrue(_store.Save(editor));

            HouseEditor back = new HouseStore(_store.Path).Open();
            Assert.AreEqual(editor.Design.Fingerprint(), back.Design.Fingerprint());
            Assert.AreEqual("दिवाली", back.State.layouts[0].name);
            Assert.AreEqual(1, back.State.saveSerial);
            Assert.IsFalse(back.Unsaved);
        }

        [Test]
        public void KilledMidEdit_RestoresOnTheNextStart()
        {
            HouseEditor editor = _store.Open();
            editor.Do(new SetPaint(2, 2, 0));
            Assert.IsTrue(_store.Save(editor));
            editor.Do(PlaceDecor.New(editor.Design, new PlacedDecor { item = "diya_clay", x = 0.4f, z = 1.2f }));
            editor.Do(PlaceDecor.New(editor.Design, new PlacedDecor { item = "rangoli_lotus", x = 0f, z = 2f }));
            editor.Undo();
            string expected = editor.Design.Fingerprint();

            // No save: the process dies here. A new store reads only what is on disk.
            var restart = new HouseStore(_store.Path);
            HouseEditor restored = restart.Open();
            Assert.AreEqual(3, restart.Recovered);
            Assert.AreEqual(expected, restored.Design.Fingerprint());
            Assert.IsTrue(restored.CanRedo, "the undone rangoli can still be redone");
            Assert.IsTrue(restored.Unsaved);
        }

        [Test]
        public void TornJournalLine_KeepsTheEditsBeforeIt()
        {
            HouseEditor editor = _store.Open();
            editor.Do(new SetPaint(5, 0, 0));
            editor.Do(new SetPaint(6, 0, 0));
            string text = File.ReadAllText(_store.Journal.Path);
            File.WriteAllText(_store.Journal.Path, text.Substring(0, text.Length - 6));

            HouseEditor restored = new HouseStore(_store.Path).Open();
            Assert.AreEqual(5, restored.Design.wallColour);
        }

        [Test]
        public void CorruptOrEditedFile_IsRepaired()
        {
            File.WriteAllText(_store.Path, "{ not json");
            Assert.AreEqual(HouseDesign.Default().Fingerprint(), _store.Open().Design.Fingerprint());

            File.WriteAllText(_store.Path, "{\"version\":0,\"design\":{\"wallColour\":40,\"decorations\":[{\"item\":\"diya_clay\",\"scale\":9},{\"item\":\"ghost\"}]}}");
            HouseState loaded = _store.Load();
            Assert.AreEqual(HouseState.CurrentVersion, loaded.version);
            Assert.AreEqual(1, loaded.design.decorations.Count);
            Assert.AreEqual(1, loaded.design.decorations[0].uid);
            Assert.AreEqual(HomeCatalog.Points.Length, loaded.design.modules.Count);
            StringAssert.Contains("\"version\": 1", File.ReadAllText(_store.Path), "the repaired file is written back");
        }
    }
}

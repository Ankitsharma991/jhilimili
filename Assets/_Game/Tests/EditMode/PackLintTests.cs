using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Neighbourhood;
using UtsavNights.Core.Packs;

namespace UtsavNights.Tests
{
    /// <summary>Phase 9 step 1: the pack format and linter (PRD 13.4, 6, 16). Structural rules; JSON is read by a stand-in.</summary>
    public class PackLintTests
    {
        /// <summary>Returns fixed objects for any bytes: the Unity tests use the real JSON reader.</summary>
        private sealed class FakeReader : IPackContentReader
        {
            public EnvironmentDef Environment = SamplePacks.BlueCityEnvironment();
            public SettlementLayout Layout = SamplePacks.BlueCityLayout();
            public WardrobePackManifest Wardrobe;
            public List<string> Shows = new List<string>();

            public EnvironmentDef ReadEnvironment(byte[] json) => Environment;

            public SettlementLayout ReadLayout(byte[] json) => Layout;

            public WardrobePackManifest ReadWardrobe(byte[] json) => Wardrobe;

            public List<string> CheckShows(byte[] json) => Shows;
        }

        private static (PackManifest manifest, MemoryPackFiles files) Sample()
        {
            var files = new MemoryPackFiles();
            files.Files[PackLint.EnvironmentFile] = Encoding.UTF8.GetBytes("{\"id\":\"blue_city_lane\"}");
            files.Files[PackLint.LayoutFile] = Encoding.UTF8.GetBytes("{\"environment\":\"blue_city_lane\"}");
            files.Files[PackLint.PreviewFile] = Png(256, 144);
            PackManifest m = PackBuilder.Seal(SamplePacks.BlueCityManifest(), files.Files);
            return (m, files);
        }

        private static byte[] Png(int w, int h)
        {
            var b = new byte[40];
            new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82 }.CopyTo(b, 0);
            b[16] = (byte)(w >> 24); b[17] = (byte)(w >> 16); b[18] = (byte)(w >> 8); b[19] = (byte)w;
            b[20] = (byte)(h >> 24); b[21] = (byte)(h >> 16); b[22] = (byte)(h >> 8); b[23] = (byte)h;
            return b;
        }

        private static string Problems(PackManifest m, IPackFiles f, IPackContentReader r = null) => string.Join("\n", PackLint.Check(m, f, r ?? new FakeReader()));

        [Test]
        public void SamplePack_Passes()
        {
            (PackManifest m, MemoryPackFiles f) = Sample();
            CollectionAssert.IsEmpty(PackLint.Check(m, f, new FakeReader()), Problems(m, f));
            Assert.AreEqual(3, m.files.Count);
            Assert.AreEqual(m.files.Sum(x => x.sizeBytes), m.sizeBytes);
            long worst = ChunkPlanner.WorstTriangles(SamplePacks.BlueCityLayout(), SamplePacks.BlueCityEnvironment().ToInfo(m.packId, 0f));
            Assert.LessOrEqual(worst, m.budget.maxTriangles, "the sample's real cost is inside its budget");
        }

        [Test]
        public void TamperedMissingOrMisdeclaredFiles_AreRefused()
        {
            (PackManifest m, MemoryPackFiles f) = Sample();
            f.Files[PackLint.LayoutFile] = Encoding.UTF8.GetBytes("{\"environment\":\"evil\"}");
            StringAssert.Contains("does not match its size and hash", Problems(m, f));

            (m, f) = Sample();
            f.Files.Remove(PackLint.PreviewFile);
            StringAssert.Contains("'preview.png' is missing", Problems(m, f));

            (m, f) = Sample();
            m.sizeBytes += 1;
            StringAssert.Contains("not the sum of the files", Problems(m, f));

            (m, f) = Sample();
            m.files.Reverse();
            StringAssert.Contains("digest does not match", Problems(m, f));

            (m, f) = Sample();
            m.files[0].path = "../escape.json";
            StringAssert.Contains("lowercase relative path", Problems(m, f));
        }

        [Test]
        public void IdsVersionsTypesAndRequirements_AreChecked()
        {
            (PackManifest m, MemoryPackFiles f) = Sample();
            m.packId = "Bad Id";
            StringAssert.Contains("pack id", Problems(m, f));
            (m, f) = Sample();
            m.version = "1.0";
            StringAssert.Contains("major.minor.patch", Problems(m, f));
            (m, f) = Sample();
            m.type = "cheats";
            StringAssert.Contains("unknown pack type", Problems(m, f));
            (m, f) = Sample();
            m.requires.Add(new PackRequirement { pack = m.packId, min = "1.0.0" });
            StringAssert.Contains("requires itself", Problems(m, f));

            Assert.IsTrue(SemVer.TryParse("1.10.0", out SemVer a));
            Assert.IsTrue(SemVer.TryParse("1.9.3", out SemVer b));
            Assert.Greater(a.CompareTo(b), 0, "1.10.0 is newer than 1.9.3");
        }

        [Test]
        public void Budgets_AreEnforced()
        {
            (PackManifest m, MemoryPackFiles f) = Sample();
            m.budget.maxTriangles = 900000;
            StringAssert.Contains("over the environment cap", Problems(m, f));

            (m, f) = Sample();
            f.Files[PackLint.PreviewFile] = Png(2048, 2048);
            m = PackBuilder.Seal(m, f.Files);
            StringAssert.Contains("textures take 16.0 MB", Problems(m, f), "a 2048 x 2048 preview is over the declared 1 MB");

            (m, f) = Sample();
            m.budget.maxTriangles = 1000;
            StringAssert.Contains("the settlement draws up to", Problems(m, f), "the real cost is measured, not trusted");
        }

        [Test]
        public void EnvironmentContent_IsReviewedNewAndValid()
        {
            (PackManifest m, MemoryPackFiles f) = Sample();
            m.review.advisor = string.Empty;
            StringAssert.Contains("cultural reviewer", Problems(m, f));

            (m, f) = Sample();
            var reader = new FakeReader();
            reader.Environment.id = Environments.TownLane;
            reader.Layout.environment = Environments.TownLane;
            StringAssert.Contains("built-in environment", Problems(m, f, reader));

            reader = new FakeReader();
            reader.Environment.roofs = new[] { "door_wooden_panel" };
            reader.Environment.roofWeights = new[] { 1 };
            StringAssert.Contains("not a roof module", Problems(m, f, reader));

            reader = new FakeReader();
            reader.Layout.lots.Find(l => l.id == "lotp1").x = 4f;
            StringAssert.Contains("layout.json: lots", Problems(m, f, reader));
        }

        [Test]
        public void WardrobeAndShowPacks_UseTheirOwnChecks()
        {
            var files = new MemoryPackFiles();
            files.Files[PackLint.WardrobeFile] = Encoding.UTF8.GetBytes("{}");
            PackManifest m = PackBuilder.Seal(new PackManifest
            {
                packId = "wardrobe_festive_two", type = PackTypes.Wardrobe, title = "Festive two", budget = new PackBudget { maxTriangles = 20000, maxTextureMB = 0f },
            }, files.Files);
            var reader = new FakeReader { Wardrobe = new WardrobePackManifest { id = "festive_two", title = "Festive two", garments = new PackGarment[0] } };
            Assert.IsTrue(PackLint.Check(m, files, reader).Any(p => p.StartsWith("wardrobe.json")), "an empty wardrobe pack is refused by the Phase 6 validator");

            var shows = new MemoryPackFiles();
            shows.Files[PackLint.ShowsFile] = Encoding.UTF8.GetBytes("{}");
            PackManifest s = PackBuilder.Seal(new PackManifest { packId = "shows_diwali", type = PackTypes.Shows, title = "Diwali shows" }, shows.Files);
            CollectionAssert.IsEmpty(PackLint.Check(s, shows, new FakeReader()));
            Assert.IsTrue(PackLint.Check(s, shows, new FakeReader { Shows = new List<string> { "bad" } }).Contains("shows.json: bad"));
        }
    }
}

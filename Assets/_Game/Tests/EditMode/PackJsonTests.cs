using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UtsavNights.Core.Packs;
using UtsavNights.EditorTools;

namespace UtsavNights.Tests
{
    /// <summary>Phase 9 step 1: the sample pack built through real JSON passes the linter, and the catalog reads back.</summary>
    public class PackJsonTests
    {
        [Test]
        public void SamplePack_BuildsLintsAndReadsBack()
        {
            List<string> problems = PackTool.BuildSamples();
            CollectionAssert.IsEmpty(problems, string.Join("\n", problems));
            PackCatalog catalog = JsonUtility.FromJson<PackCatalog>(File.ReadAllText(Path.Combine(PackTool.SampleFolder, "catalog.json")));
            PackManifest m = catalog.Find(SamplePacks.BlueCityPack);
            Assert.IsNotNull(m);
            string folder = Path.Combine(PackTool.SampleFolder, "packs", m.packId, m.version);
            foreach (PackFile f in m.files)
            {
                byte[] data = File.ReadAllBytes(Path.Combine(folder, f.path));
                Assert.AreEqual(f.sha256, PackHash.Sha256(data), f.path);
            }

            Assert.IsTrue(PackLint.TryPngSize(File.ReadAllBytes(Path.Combine(folder, PackLint.PreviewFile)), out int w, out int h));
            Assert.AreEqual(256, w);
            Assert.AreEqual(144, h);
        }
    }
}

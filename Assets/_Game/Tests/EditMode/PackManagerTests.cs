using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Neighbourhood;
using UtsavNights.Core.Packs;

namespace UtsavNights.Tests
{
    /// <summary>
    /// Phase 9 step 2: install, update, delete, dependencies and rollback (F21). The exit gate: a pack update with a simulated
    /// network failure leaves the app consistent, and so does a crash in the middle of the swap.
    /// </summary>
    public class PackManagerTests
    {
        /// <summary>Stand-in JSON for the Mono tests: values are kept by token (the game uses JsonUtility).</summary>
        private sealed class TokenJson : IPackJson
        {
            private static readonly Dictionary<string, object> Store = new Dictionary<string, object>();
            private static int _next;

            public string Write(object value)
            {
                string token = "obj:" + _next++;
                Store[token] = value is PackIndex i ? new PackIndex { installed = i.installed.Select(r => new PackRequirement { pack = r.pack, min = r.min }).ToList() } : value;
                return token;
            }

            public PackManifest ReadManifest(string json) => Store.TryGetValue(json, out object o) ? o as PackManifest : null;

            public PackIndex ReadIndex(string json) => Store.TryGetValue(json, out object o) ? o as PackIndex : null;

            public PackCatalog ReadCatalog(string json) => Store.TryGetValue(json, out object o) ? o as PackCatalog : null;
        }

        private sealed class Reader : IPackContentReader
        {
            public EnvironmentDef ReadEnvironment(byte[] json) => SamplePacks.BlueCityEnvironment();

            public SettlementLayout ReadLayout(byte[] json) => SamplePacks.BlueCityLayout();

            public WardrobePackManifest ReadWardrobe(byte[] json) => null;

            public List<string> CheckShows(byte[] json) => new List<string>();
        }

        /// <summary>A server in memory that can drop the connection after a number of files, or serve a damaged file.</summary>
        private sealed class Server : IPackTransport
        {
            public readonly Dictionary<string, byte[]> Files = new Dictionary<string, byte[]>();
            public int FailAfter = int.MaxValue;
            public string Corrupt;
            public int Fetched;

            public byte[] Fetch(string path)
            {
                if (Fetched++ >= FailAfter)
                {
                    throw new PackTransportException("connection lost");
                }

                if (!Files.TryGetValue(path, out byte[] data))
                {
                    throw new PackTransportException("404");
                }

                return path == Corrupt ? data.Reverse().ToArray() : data;
            }
        }

        private static readonly byte[] Png = { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 1, 0, 0, 0, 0, 144, 8, 2, 0, 0, 0, 0, 0, 0 };

        private static PackManifest Publish(Server server, PackCatalog catalog, string version, string marker, List<PackRequirement> requires = null, string id = SamplePacks.BlueCityPack)
        {
            var files = new Dictionary<string, byte[]>
            {
                [PackLint.EnvironmentFile] = Encoding.UTF8.GetBytes("{\"id\":\"" + marker + "\"}"),
                [PackLint.LayoutFile] = Encoding.UTF8.GetBytes("{\"v\":\"" + version + "\"}"),
                [PackLint.PreviewFile] = Png,
            };
            PackManifest m = SamplePacks.BlueCityManifest();
            m.packId = id;
            m.version = version;
            m.requires = requires ?? new List<PackRequirement>();
            PackBuilder.Seal(m, files);
            foreach (KeyValuePair<string, byte[]> f in files)
            {
                server.Files[PackManager.ServerPath(m, f.Key)] = f.Value;
            }

            catalog.packs.RemoveAll(p => p.packId == id);
            catalog.packs.Add(m);
            return m;
        }

        private static string Marker(PackManager manager, string id = SamplePacks.BlueCityPack) =>
            Encoding.UTF8.GetString(manager.Files(id).Read(PackLint.EnvironmentFile));

        [Test]
        public void Install_ThenUpdate_ThenDelete()
        {
            var storage = new MemoryPackStorage();
            var server = new Server();
            var catalog = new PackCatalog();
            Publish(server, catalog, "1.0.0", "one");
            var manager = new PackManager(storage, new TokenJson(), new Reader());
            long reported = 0;
            PackResult r = manager.Install(catalog, SamplePacks.BlueCityPack, server, (done, total) => reported = done);
            Assert.IsTrue(r.Ok, r.Message);
            Assert.AreEqual("1.0.0", manager.VersionOf(SamplePacks.BlueCityPack));
            Assert.AreEqual(catalog.packs[0].sizeBytes, reported, "progress reaches the pack's size");
            StringAssert.Contains("one", Marker(manager));
            CollectionAssert.IsEmpty(manager.Updates(catalog));

            Publish(server, catalog, "1.1.0", "two");
            Assert.AreEqual(1, manager.Updates(catalog).Count, "an update is offered");
            Assert.IsTrue(manager.Install(catalog, SamplePacks.BlueCityPack, server).Ok);
            Assert.AreEqual("1.1.0", manager.VersionOf(SamplePacks.BlueCityPack));
            StringAssert.Contains("two", Marker(manager));
            Assert.IsFalse(storage.DirectoryExists(PackManager.OldDir(SamplePacks.BlueCityPack)), "the old version is dropped once the new one is recorded");

            var reopened = new PackManager(storage, new TokenJson(), new Reader());
            Assert.AreEqual("1.1.0", reopened.VersionOf(SamplePacks.BlueCityPack), "the index survives a restart");
            Assert.IsTrue(reopened.Delete(SamplePacks.BlueCityPack).Ok);
            Assert.IsFalse(reopened.IsInstalled(SamplePacks.BlueCityPack));
            Assert.IsFalse(storage.DirectoryExists(PackManager.InstalledDir(SamplePacks.BlueCityPack)));
        }

        [Test]
        public void NetworkFailureDuringAnUpdate_LeavesTheOldVersionWhole()
        {
            var storage = new MemoryPackStorage();
            var server = new Server();
            var catalog = new PackCatalog();
            Publish(server, catalog, "1.0.0", "one");
            var manager = new PackManager(storage, new TokenJson(), new Reader());
            Assert.IsTrue(manager.Install(catalog, SamplePacks.BlueCityPack, server).Ok);
            Dictionary<string, byte[]> before = Snapshot(storage);

            Publish(server, catalog, "1.1.0", "two");
            server.Fetched = 0;
            server.FailAfter = 1;
            PackResult r = manager.Install(catalog, SamplePacks.BlueCityPack, server);
            Assert.IsFalse(r.Ok);
            StringAssert.Contains("Nothing changed", r.Message);
            Assert.AreEqual("1.0.0", manager.VersionOf(SamplePacks.BlueCityPack));
            CollectionAssert.AreEquivalent(before.Keys, storage.Files.Keys, "no staging left behind, no file changed");
            foreach (KeyValuePair<string, byte[]> f in before)
            {
                CollectionAssert.AreEqual(f.Value, storage.Files[f.Key], f.Key);
            }

            CollectionAssert.IsEmpty(PackLint.Check(manager.Manifest(SamplePacks.BlueCityPack), manager.Files(SamplePacks.BlueCityPack), new Reader()), "the old pack still verifies");
        }

        [Test]
        public void DamagedDownload_IsRefusedAndNothingChanges()
        {
            var storage = new MemoryPackStorage();
            var server = new Server();
            var catalog = new PackCatalog();
            PackManifest m = Publish(server, catalog, "1.0.0", "one");
            server.Corrupt = PackManager.ServerPath(m, PackLint.LayoutFile);
            var manager = new PackManager(storage, new TokenJson(), new Reader());
            PackResult r = manager.Install(catalog, SamplePacks.BlueCityPack, server);
            Assert.IsFalse(r.Ok);
            StringAssert.Contains("damaged", r.Message);
            Assert.IsFalse(manager.IsInstalled(SamplePacks.BlueCityPack));
            CollectionAssert.IsEmpty(storage.Files.Keys.Where(k => k.Contains("staging")));
        }

        [TestCase("old aside")]
        [TestCase("new in")]
        [TestCase("recorded")]
        public void KilledMidSwap_RecoversToOneWholeVersion(string step)
        {
            var storage = new MemoryPackStorage();
            var server = new Server();
            var catalog = new PackCatalog();
            Publish(server, catalog, "1.0.0", "one");
            var manager = new PackManager(storage, new TokenJson(), new Reader());
            Assert.IsTrue(manager.Install(catalog, SamplePacks.BlueCityPack, server).Ok);

            Publish(server, catalog, "1.1.0", "two");
            manager.SwapStep = s =>
            {
                if (s == step)
                {
                    throw new InvalidOperationException("killed");
                }
            };
            Assert.Throws<InvalidOperationException>(() => manager.Install(catalog, SamplePacks.BlueCityPack, server));

            var restarted = new PackManager(storage, new TokenJson(), new Reader());
            restarted.Recover();
            string expected = step == "recorded" ? "1.1.0" : "1.0.0";
            Assert.AreEqual(expected, restarted.VersionOf(SamplePacks.BlueCityPack), $"killed after '{step}'");
            Assert.AreEqual(expected, restarted.Manifest(SamplePacks.BlueCityPack).version, "the files match the index");
            CollectionAssert.IsEmpty(PackLint.Check(restarted.Manifest(SamplePacks.BlueCityPack), restarted.Files(SamplePacks.BlueCityPack), new Reader()));
            Assert.IsFalse(storage.DirectoryExists(PackManager.OldDir(SamplePacks.BlueCityPack)));
            Assert.IsFalse(storage.DirectoryExists(PackManager.StagingDir(SamplePacks.BlueCityPack)));
        }

        [Test]
        public void Requirements_InstallFirst_AndBlockDeletion()
        {
            var storage = new MemoryPackStorage();
            var server = new Server();
            var catalog = new PackCatalog();
            Publish(server, catalog, "1.2.0", "props", id: "core_props");
            Publish(server, catalog, "1.0.0", "street", new List<PackRequirement> { new PackRequirement { pack = "core_props", min = "1.2.0" } });
            var manager = new PackManager(storage, new TokenJson(), new Reader());
            Assert.IsTrue(manager.Install(catalog, SamplePacks.BlueCityPack, server).Ok);
            Assert.IsTrue(manager.IsInstalled("core_props"), "the requirement came first");
            PackResult r = manager.Delete("core_props");
            Assert.IsFalse(r.Ok);
            StringAssert.Contains("needs this pack", r.Message);
            Assert.IsFalse(manager.Install(catalog, "missing_pack", server).Ok);
        }

        private static Dictionary<string, byte[]> Snapshot(MemoryPackStorage s) => s.Files.ToDictionary(k => k.Key, v => v.Value.ToArray());
    }
}

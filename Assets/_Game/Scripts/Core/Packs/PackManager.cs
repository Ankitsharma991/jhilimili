using System;
using System.Collections.Generic;
using System.Text;

namespace UtsavNights.Core.Packs
{
    /// <summary>Where packs live on the phone (files) or in tests (memory). Paths use '/'.</summary>
    public interface IPackStorage
    {
        bool Exists(string path);

        byte[] Read(string path);

        void Write(string path, byte[] data);

        void Delete(string path);

        bool DirectoryExists(string dir);

        void DeleteDirectory(string dir);

        /// <summary>Renames a whole directory (atomic on one file system).</summary>
        void MoveDirectory(string from, string to);
    }

    /// <summary>Fetches a file from the pack server, by path relative to its root (catalog.json, packs/id/1.0.0/layout.json).</summary>
    public interface IPackTransport
    {
        /// <summary>The file's bytes; throws <see cref="PackTransportException"/> when the network fails.</summary>
        byte[] Fetch(string path);
    }

    public sealed class PackTransportException : Exception
    {
        public PackTransportException(string message)
            : base(message)
        {
        }
    }

    public sealed class MemoryPackStorage : IPackStorage
    {
        public Dictionary<string, byte[]> Files { get; } = new Dictionary<string, byte[]>();

        public bool Exists(string path) => Files.ContainsKey(path);

        public byte[] Read(string path) => Files.TryGetValue(path, out byte[] b) ? b : null;

        public void Write(string path, byte[] data) => Files[path] = data;

        public void Delete(string path) => Files.Remove(path);

        public bool DirectoryExists(string dir)
        {
            string prefix = dir.TrimEnd('/') + "/";
            foreach (string k in Files.Keys)
            {
                if (k.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public void DeleteDirectory(string dir)
        {
            string prefix = dir.TrimEnd('/') + "/";
            foreach (string k in new List<string>(Files.Keys))
            {
                if (k.StartsWith(prefix, StringComparison.Ordinal))
                {
                    Files.Remove(k);
                }
            }
        }

        public void MoveDirectory(string from, string to)
        {
            string a = from.TrimEnd('/') + "/", b = to.TrimEnd('/') + "/";
            foreach (string k in new List<string>(Files.Keys))
            {
                if (k.StartsWith(a, StringComparison.Ordinal))
                {
                    Files[b + k.Substring(a.Length)] = Files[k];
                    Files.Remove(k);
                }
            }
        }
    }

    /// <summary>The packs on the phone and their versions (packs/index.json).</summary>
    [Serializable]
    public sealed class PackIndex
    {
        public List<PackRequirement> installed = new List<PackRequirement>();

        public string VersionOf(string packId) => installed.Find(p => p.pack == packId)?.min;
    }

    /// <summary>Reads and writes manifests and the index; the game supplies JSON (JsonUtility).</summary>
    public interface IPackJson
    {
        string Write(object value);

        PackManifest ReadManifest(string json);

        PackIndex ReadIndex(string json);

        PackCatalog ReadCatalog(string json);
    }

    /// <summary>The result of an install, update or delete, in plain words for the storage manager.</summary>
    public readonly struct PackResult
    {
        public PackResult(bool ok, string message)
        {
            Ok = ok;
            Message = message;
        }

        public bool Ok { get; }

        public string Message { get; }

        public static PackResult Done(string message) => new PackResult(true, message);

        public static PackResult Failed(string message) => new PackResult(false, message);
    }

    /// <summary>
    /// The pack manager (Phase 9 step 2, F21, PRD 13.4): installs, updates and deletes packs on the phone. A download goes to
    /// a staging folder beside the installed version, file by file with each hash checked on arrival; the staged pack is
    /// linted whole; then a three-step swap (old aside, new in, index written, old deleted) replaces it. Any failure deletes
    /// the staging folder and leaves the installed version exactly as it was (the PRD's automatic rollback);
    /// <see cref="Recover"/> finishes or undoes a swap that a crash interrupted. Dependencies install first.
    /// </summary>
    public sealed class PackManager
    {
        public const string Root = "packs";
        public const string IndexPath = Root + "/index.json";

        private readonly IPackStorage _storage;
        private readonly IPackJson _json;
        private readonly IPackContentReader _reader;
        private PackIndex _index;

        public PackManager(IPackStorage storage, IPackJson json, IPackContentReader reader)
        {
            _storage = storage;
            _json = json;
            _reader = reader;
            _index = LoadIndex();
        }

        /// <summary>Called after each step of a swap; tests throw from it to simulate the app being killed mid-swap.</summary>
        public Action<string> SwapStep { get; set; } = _ => { };

        public IReadOnlyList<PackRequirement> Installed => _index.installed;

        public static string InstalledDir(string packId) => $"{Root}/installed/{packId}";

        public static string StagingDir(string packId) => $"{Root}/staging/{packId}";

        public static string OldDir(string packId) => $"{Root}/old/{packId}";

        public static string ServerPath(PackManifest m, string file) => $"packs/{m.packId}/{m.version}/{file}";

        public bool IsInstalled(string packId) => _index.VersionOf(packId) != null;

        public string VersionOf(string packId) => _index.VersionOf(packId);

        /// <summary>The installed pack's manifest, or null.</summary>
        public PackManifest Manifest(string packId)
        {
            byte[] data = _storage.Read(InstalledDir(packId) + "/manifest.json");
            return data == null ? null : _json.ReadManifest(Encoding.UTF8.GetString(data));
        }

        public IPackFiles Files(string packId) => new StorageFiles(_storage, InstalledDir(packId));

        public long InstalledBytes(string packId) => Manifest(packId)?.sizeBytes ?? 0;

        /// <summary>Packs in the catalog newer than the installed version.</summary>
        public List<PackManifest> Updates(PackCatalog catalog)
        {
            var list = new List<PackManifest>();
            foreach (PackRequirement p in _index.installed)
            {
                PackManifest remote = catalog?.Find(p.pack);
                if (remote != null && SemVer.TryParse(remote.version, out SemVer r) && SemVer.TryParse(p.min, out SemVer have) && r.CompareTo(have) > 0)
                {
                    list.Add(remote);
                }
            }

            return list;
        }

        /// <summary>Installs or updates a pack from the catalog, its missing or outdated requirements first.</summary>
        public PackResult Install(PackCatalog catalog, string packId, IPackTransport transport, Action<long, long> progress = null)
        {
            var order = new List<PackManifest>();
            string problem = Plan(catalog, packId, order, new HashSet<string>());
            if (problem != null)
            {
                return PackResult.Failed(problem);
            }

            foreach (PackManifest m in order)
            {
                PackResult r = InstallOne(m, transport, progress);
                if (!r.Ok)
                {
                    return r;
                }
            }

            return PackResult.Done($"{catalog.Find(packId).title} is ready.");
        }

        /// <summary>Removes a pack, unless an installed pack needs it.</summary>
        public PackResult Delete(string packId)
        {
            if (!IsInstalled(packId))
            {
                return PackResult.Failed("That pack isn't on this phone.");
            }

            foreach (PackRequirement p in _index.installed)
            {
                PackManifest other = p.pack == packId ? null : Manifest(p.pack);
                if (other != null && other.requires.Exists(r => r.pack == packId))
                {
                    return PackResult.Failed($"{other.title} needs this pack. Delete that one first.");
                }
            }

            string title = Manifest(packId)?.title ?? packId;
            _index.installed.RemoveAll(p => p.pack == packId);
            SaveIndex();
            _storage.DeleteDirectory(InstalledDir(packId));
            return PackResult.Done($"{title} was deleted.");
        }

        /// <summary>
        /// Finishes or undoes a swap that a crash interrupted, and clears half-finished downloads. Run once at start, before
        /// anything reads the packs.
        /// </summary>
        public void Recover()
        {
            // A first install killed before it was recorded leaves files the index never lists: unused, and overwritten by
            // the next install of that pack.
            foreach (PackRequirement entry in new List<PackRequirement>(_index.installed))
            {
                string id = entry.pack;
                bool old = _storage.DirectoryExists(OldDir(id)), now = _storage.DirectoryExists(InstalledDir(id));
                string indexed = _index.VersionOf(id);
                string have = now ? Manifest(id)?.version : null;
                if (old && (!now || have != indexed))
                {
                    // The new version never got recorded: put the old one back.
                    if (now)
                    {
                        _storage.DeleteDirectory(InstalledDir(id));
                    }

                    _storage.MoveDirectory(OldDir(id), InstalledDir(id));
                }
                else if (old)
                {
                    _storage.DeleteDirectory(OldDir(id));
                }

                _storage.DeleteDirectory(StagingDir(id));
            }
        }

        private string Plan(PackCatalog catalog, string packId, List<PackManifest> order, HashSet<string> visiting)
        {
            PackManifest m = catalog?.Find(packId);
            if (m == null)
            {
                return "That pack isn't in the catalog any more.";
            }

            if (!visiting.Add(packId))
            {
                return "These packs need each other in a loop and can't be installed.";
            }

            foreach (PackRequirement r in m.requires)
            {
                string have = VersionOf(r.pack);
                bool ok = have != null && SemVer.TryParse(have, out SemVer h) && SemVer.TryParse(r.min, out SemVer need) && h.CompareTo(need) >= 0;
                if (!ok && order.Find(o => o.packId == r.pack) == null)
                {
                    string problem = Plan(catalog, r.pack, order, visiting);
                    if (problem != null)
                    {
                        return problem;
                    }
                }
            }

            if (order.Find(o => o.packId == m.packId) == null)
            {
                order.Add(m);
            }

            return null;
        }

        private PackResult InstallOne(PackManifest m, IPackTransport transport, Action<long, long> progress)
        {
            string staging = StagingDir(m.packId);
            _storage.DeleteDirectory(staging);
            long done = 0;
            try
            {
                foreach (PackFile f in m.files)
                {
                    byte[] data = transport.Fetch(ServerPath(m, f.path));
                    if (data == null || data.LongLength != f.sizeBytes || PackHash.Sha256(data) != f.sha256)
                    {
                        _storage.DeleteDirectory(staging);
                        return PackResult.Failed($"{m.title} arrived damaged. Nothing changed; try again.");
                    }

                    _storage.Write(staging + "/" + f.path, data);
                    done += data.LongLength;
                    progress?.Invoke(done, m.sizeBytes);
                }
            }
            catch (PackTransportException)
            {
                _storage.DeleteDirectory(staging);
                return PackResult.Failed($"The download of {m.title} stopped. Nothing changed; try again on Wi-Fi.");
            }

            List<string> problems = PackLint.Check(m, new StorageFiles(_storage, staging), _reader);
            if (problems.Count > 0)
            {
                _storage.DeleteDirectory(staging);
                return PackResult.Failed($"{m.title} didn't pass its checks ({problems[0]}). Nothing changed.");
            }

            _storage.Write(staging + "/manifest.json", Encoding.UTF8.GetBytes(_json.Write(m)));

            // The swap: old aside, new in, record it, drop the old.
            string installed = InstalledDir(m.packId), old = OldDir(m.packId);
            _storage.DeleteDirectory(old);
            if (_storage.DirectoryExists(installed))
            {
                _storage.MoveDirectory(installed, old);
            }

            SwapStep("old aside");
            _storage.MoveDirectory(staging, installed);
            SwapStep("new in");
            _index.installed.RemoveAll(p => p.pack == m.packId);
            _index.installed.Add(new PackRequirement { pack = m.packId, min = m.version });
            SaveIndex();
            SwapStep("recorded");
            _storage.DeleteDirectory(old);
            return PackResult.Done($"{m.title} {m.version} installed.");
        }

        private PackIndex LoadIndex()
        {
            byte[] data = _storage.Read(IndexPath);
            PackIndex index = data == null ? null : _json.ReadIndex(Encoding.UTF8.GetString(data));
            return index ?? new PackIndex();
        }

        private void SaveIndex() => _storage.Write(IndexPath, Encoding.UTF8.GetBytes(_json.Write(_index)));

        private sealed class StorageFiles : IPackFiles
        {
            private readonly IPackStorage _storage;
            private readonly string _dir;

            public StorageFiles(IPackStorage storage, string dir)
            {
                _storage = storage;
                _dir = dir;
            }

            public bool Exists(string path) => _storage.Exists(_dir + "/" + path);

            public byte[] Read(string path) => _storage.Read(_dir + "/" + path);
        }
    }
}

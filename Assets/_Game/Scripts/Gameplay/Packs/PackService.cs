using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UtsavNights.Core.Neighbourhood;
using UtsavNights.Core.Packs;
using UtsavNights.Core.Runtime;

namespace UtsavNights.Gameplay
{
    /// <summary>A pack was installed or updated (analytics in step 7).</summary>
    public readonly struct PackInstalledEvent
    {
        public PackInstalledEvent(string packId, string version, string type)
        {
            PackId = packId;
            Version = version;
            Type = type;
        }

        public string PackId { get; }

        public string Version { get; }

        public string Type { get; }
    }

    /// <summary>An install or update failed (a short reason id, never a message or URL).</summary>
    public readonly struct PackFailedEvent
    {
        public PackFailedEvent(string packId, string reason)
        {
            PackId = packId;
            Reason = reason;
        }

        public string PackId { get; }

        public string Reason { get; }
    }

    public readonly struct PackDeletedEvent
    {
        public PackDeletedEvent(string packId) => PackId = packId;

        public string PackId { get; }
    }

    /// <summary>
    /// Content packs on the phone (Phase 9 steps 2 and 3, F21): recovers any interrupted swap at start, registers installed
    /// environment packs, fetches the catalog, installs and updates on a worker thread (the game never waits), and deletes.
    /// Wi-Fi only by default (PRD 12). Progress and results come back to the main thread in <see cref="Poll"/>.
    /// </summary>
    public sealed class PackService
    {
        public const string WifiOnlyPrefKey = "packs.wifiOnly";
        private const string CatalogCache = PackManager.Root + "/catalog.json";

        private readonly PackManager _manager;
        private readonly IPackStorage _storage;
        private readonly JsonPackJson _json = new JsonPackJson();
        private readonly object _lock = new object();
        private Task<PackResult> _job;
        private string _jobPack;
        private long _done;
        private long _total;

        public PackService(string storageRoot, IPackTransport transport)
        {
            _storage = new FilePackStorage(storageRoot);
            _manager = new PackManager(_storage, _json, new JsonPackReader());
            Transport = transport;
            _manager.Recover();
            Catalog = LoadCachedCatalog();
            RegisterInstalled();
        }

        /// <summary>Something changed: a pack installed, updated or deleted, or the catalog arrived.</summary>
        public event Action Changed;

        /// <summary>The finished result of the last install (shown by the screen that started it).</summary>
        public event Action<string, PackResult> Finished;

        public IPackTransport Transport { get; set; }

        public PackCatalog Catalog { get; private set; }

        public PackManager Manager => _manager;

        public bool Busy => _job != null;

        public string BusyPack => _jobPack;

        /// <summary>0 to 1 for the pack being downloaded.</summary>
        public float Progress
        {
            get
            {
                lock (_lock)
                {
                    return _total > 0 ? (float)_done / _total : 0f;
                }
            }
        }

        public static bool WifiOnly
        {
            get => PlayerPrefs.GetInt(WifiOnlyPrefKey, 1) == 1;
            set => PlayerPrefs.SetInt(WifiOnlyPrefKey, value ? 1 : 0);
        }

        public bool HasServer => Transport != null;

        /// <summary>Fetches catalog.json (on a worker thread) and keeps a copy for offline browsing.</summary>
        public async Task<bool> RefreshCatalog()
        {
            if (Transport == null)
            {
                return false;
            }

            IPackTransport transport = Transport;
            try
            {
                byte[] data = await Task.Run(() => transport.Fetch("catalog.json"));
                PackCatalog catalog = _json.ReadCatalog(JsonPackReader.Text(data));
                if (catalog == null)
                {
                    return false;
                }

                Catalog = catalog;
                _storage.Write(CatalogCache, data);
                Changed?.Invoke();
                return true;
            }
            catch (PackTransportException e)
            {
                Debug.Log("[Packs] catalog unavailable: " + e.Message);
                return false;
            }
        }

        /// <summary>
        /// Starts installing (or updating) a pack. False with a reason when it cannot start: busy, no catalog entry, or Wi-Fi only
        /// and not on Wi-Fi. The result arrives through <see cref="Finished"/>.
        /// </summary>
        public bool Install(string packId, out string reason)
        {
            reason = string.Empty;
            if (Busy)
            {
                reason = "Another download is running. Try again when it finishes.";
                return false;
            }

            if (Catalog?.Find(packId) == null || Transport == null)
            {
                reason = "That pack isn't available right now.";
                return false;
            }

            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                reason = "No internet connection.";
                return false;
            }

            if (WifiOnly && Application.internetReachability != NetworkReachability.ReachableViaLocalAreaNetwork)
            {
                reason = "Connect to Wi-Fi, or allow mobile data in Downloads and storage.";
                return false;
            }

            StartJob(packId);
            return true;
        }

        /// <summary>Tests and the editor: install ignoring the network state (the folder transport needs no network).</summary>
        public void InstallNow(string packId) => StartJob(packId);

        public PackResult Delete(string packId)
        {
            if (Busy)
            {
                return PackResult.Failed("Wait for the download to finish.");
            }

            string envId = EnvironmentOf(packId);
            PackResult r = _manager.Delete(packId);
            if (r.Ok)
            {
                if (envId != null)
                {
                    Environments.Unregister(envId);
                    LeaveDeletedEnvironment(envId);
                }

                Game.Events.Publish(new PackDeletedEvent(packId));
                Changed?.Invoke();
            }

            return r;
        }

        /// <summary>A pack's preview image (environment packs), or null.</summary>
        public Texture2D Preview(string packId)
        {
            byte[] png = _storage.Read(PackManager.InstalledDir(packId) + "/" + PackLint.PreviewFile);
            if (png == null)
            {
                return null;
            }

            var t = new Texture2D(2, 2);
            return t.LoadImage(png) ? t : null;
        }

        /// <summary>Called every frame by the installer: hands a finished job back to the main thread.</summary>
        public void Poll()
        {
            if (_job == null || !_job.IsCompleted)
            {
                return;
            }

            string packId = _jobPack;
            PackResult result = _job.IsFaulted ? PackResult.Failed("Something went wrong. Nothing changed.") : _job.Result;
            if (_job.IsFaulted)
            {
                Debug.LogWarning("[Packs] install failed: " + _job.Exception?.GetBaseException().Message);
            }

            _job = null;
            _jobPack = null;
            if (result.Ok)
            {
                RegisterInstalled();
                PackManifest m = _manager.Manifest(packId);
                Game.Events.Publish(new PackInstalledEvent(packId, m?.version ?? "0.0.0", m?.type ?? "unknown"));
            }
            else
            {
                Game.Events.Publish(new PackFailedEvent(packId, Reason(result.Message)));
            }

            Changed?.Invoke();
            Finished?.Invoke(packId, result);
        }

        /// <summary>The environment id an installed environment pack provides, or null.</summary>
        public string EnvironmentOf(string packId)
        {
            foreach (EnvironmentInfo e in Environments.Available)
            {
                if (e.PackId == packId)
                {
                    return e.Id;
                }
            }

            return null;
        }

        private void StartJob(string packId)
        {
            PackCatalog catalog = Catalog;
            IPackTransport transport = Transport;
            lock (_lock)
            {
                _done = 0;
                _total = catalog.Find(packId)?.sizeBytes ?? 0;
            }

            _jobPack = packId;
            _job = Task.Run(() => _manager.Install(catalog, packId, transport, (done, total) =>
            {
                lock (_lock)
                {
                    _done = done;
                    _total = total;
                }
            }));
            Changed?.Invoke();
        }

        /// <summary>Registers every installed environment pack with the settlement library (at start and after installs).</summary>
        private void RegisterInstalled()
        {
            var reader = new JsonPackReader();
            foreach (PackRequirement p in new List<PackRequirement>(_manager.Installed))
            {
                PackManifest m = _manager.Manifest(p.pack);
                if (m == null || m.type != PackTypes.Environment)
                {
                    continue;
                }

                string dir = PackManager.InstalledDir(p.pack) + "/";
                EnvironmentDef def = reader.ReadEnvironment(_storage.Read(dir + PackLint.EnvironmentFile));
                SettlementLayout layout = reader.ReadLayout(_storage.Read(dir + PackLint.LayoutFile));
                if (def != null && layout != null && def.Problems().Count == 0)
                {
                    Environments.Register(def.ToInfo(m.packId, m.sizeBytes / 1048576f), layout);
                }
            }
        }

        private void LeaveDeletedEnvironment(string envId)
        {
            if (envId != null && PlayerPrefs.GetString(NeighbourhoodInstaller.EnvironmentPrefKey, string.Empty) == envId)
            {
                PlayerPrefs.SetString(NeighbourhoodInstaller.EnvironmentPrefKey, Environments.TownLane);
            }

            if (envId != null && Game.Services.TryGet(out SettlementView settlement) && settlement.Layout.environment == envId)
            {
                settlement.Load(Environments.TownLane);
            }
        }

        private PackCatalog LoadCachedCatalog()
        {
            byte[] data = _storage.Read(CatalogCache);
            return data != null ? _json.ReadCatalog(JsonPackReader.Text(data)) ?? new PackCatalog() : new PackCatalog();
        }

        private static string Reason(string message) =>
            message.Contains("stopped") ? "network" : message.Contains("damaged") ? "damaged" : message.Contains("checks") ? "lint" : "other";
    }
}

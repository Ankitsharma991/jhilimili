using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UtsavNights.Core.Cloud;
using UtsavNights.Core.Home;
using UtsavNights.Core.Neighbourhood;
using UtsavNights.Core.Runtime;

namespace UtsavNights.Gameplay
{
    /// <summary>A home was shared, visited or reported (analytics in step 7: counts and ids, never the code's contents).</summary>
    public readonly struct HomeVisitEvent
    {
        public HomeVisitEvent(string action, bool ok, string reason, bool offline = false)
        {
            Action = action;
            Ok = ok;
            Reason = reason;
            Offline = offline;
        }

        /// <summary>"shared", "visited", "reported" or "withdrawn".</summary>
        public string Action { get; }

        public bool Ok { get; }

        public string Reason { get; }

        public bool Offline { get; }
    }

    /// <summary>
    /// Home visits by code (Phase 10 step 4, F23). Sharing needs the Google sign-in (codes belong to an account and can be
    /// taken down); visiting needs no account (a silent anonymous one is made for it). A visit shows the friend's house in
    /// place of the player's own, view only, in its neighbourhood when this phone has it. The ten newest visits are kept
    /// on the phone, so a home can be seen again offline (offline first); a code that's been hidden or taken down is
    /// forgotten when the server says so.
    /// </summary>
    public sealed class HomeVisits
    {
        public const string FileName = "visits.json";

        private readonly CloudService _cloud;
        private readonly string _path;
        private readonly Func<long> _clock;
        private string _ownEnvironment;

        public HomeVisits(CloudService cloud, string folder = null, Func<long> clock = null)
        {
            _cloud = cloud;
            _path = Path.Combine(folder ?? Application.persistentDataPath, FileName);
            _clock = clock ?? (() => DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            History = Load();
        }

        public event Action Changed;

        public VisitHistory History { get; }

        /// <summary>The code being visited, or null.</summary>
        public string Current { get; private set; }

        public HomeSnapshot CurrentSnapshot { get; private set; }

        /// <summary>The visit is the copy kept on this phone (the server couldn't be reached).</summary>
        public bool OfflineCopy { get; private set; }

        /// <summary>Plain notes about how the visit is shown (fewer decorations on this phone, another neighbourhood).</summary>
        public string Notes { get; private set; } = string.Empty;

        public bool Visiting => Current != null;

        public static string Serialise(HomeSnapshot snapshot) => JsonUtility.ToJson(snapshot);

        /// <summary>A snapshot from the network, cleaned, or null with the reason.</summary>
        public static HomeSnapshot Parse(string payload, out string error)
        {
            error = null;
            HomeSnapshot s;
            try
            {
                s = JsonUtility.FromJson<HomeSnapshot>(payload);
            }
            catch (Exception)
            {
                s = null;
            }

            if (s == null)
            {
                error = "That code isn't a home.";
                return null;
            }

            List<string> problems = s.Problems();
            if (problems.Count > 0)
            {
                error = s.version > HomeSnapshot.CurrentVersion ? "That home was shared from a newer version of the game. Update the game first." : "That home can't be shown: " + problems[0] + ".";
                return null;
            }

            s.Clean();
            return s;
        }

        /// <summary>Shares the player's home as it is now. Returns the code, or null with the reason.</summary>
        public async Task<(string code, string error)> ShareMyHome(bool showAvatar)
        {
            if (!_cloud.SignedIn)
            {
                return (null, CloudRules.Explain(CloudError.SignedOut));
            }

            if (!Game.Services.TryGet(out HomeService home))
            {
                return (null, "The house isn't ready yet.");
            }

            // The account document (with its key) exists before anything is shared from it (the rules expect it).
            CloudResult<string> account = await _cloud.Cloud.BackupKey();
            if (!account.Ok)
            {
                return (null, Fail("shared", account.Error));
            }

            Game.Services.TryGet(out ProfileService profiles);
            string environment = Game.Services.TryGet(out SettlementView settlement) && settlement.Layout != null ? settlement.Layout.environment : string.Empty;
            HomeSnapshot snapshot = HomeSnapshot.From(home.Design, environment, showAvatar ? profiles?.Current : null);
            CloudResult<ShareInfo> published = await _cloud.Cloud.Publish(ShareKind.Home, Serialise(snapshot));
            if (!published.Ok)
            {
                return (null, Fail("shared", published.Error));
            }

            Game.Events.Publish(new HomeVisitEvent("shared", true, string.Empty));
            Changed?.Invoke();
            return (published.Value.code, null);
        }

        /// <summary>Visits a home by its code. Empty on success, else the reason.</summary>
        public async Task<string> Visit(string typed)
        {
            string code = ShareCodes.Normalise(typed);
            if (code == null)
            {
                return CloudRules.Explain(CloudError.Invalid);
            }

            if (_cloud.Cloud.Uid == null)
            {
                await _cloud.Cloud.SignInAnonymously();
            }

            CloudResult<ShareInfo> opened = await _cloud.Cloud.Open(code);
            if (!opened.Ok)
            {
                VisitRecord kept = History.Find(code);
                if ((opened.Error == CloudError.Offline || opened.Error == CloudError.NotConfigured || opened.Error == CloudError.Server) && kept != null)
                {
                    HomeSnapshot copy = Parse(kept.payload, out _);
                    if (copy != null)
                    {
                        Begin(code, copy, offline: true);
                        Game.Events.Publish(new HomeVisitEvent("visited", true, string.Empty, offline: true));
                        return string.Empty;
                    }
                }

                if (opened.Error == CloudError.Hidden || opened.Error == CloudError.Withdrawn || opened.Error == CloudError.Expired || opened.Error == CloudError.NotFound)
                {
                    Forget(code);
                }

                return Fail("visited", opened.Error);
            }

            if (opened.Value.kind != ShareKind.Home)
            {
                return "That code is a fireworks show, not a home. Open it from the show designer.";
            }

            HomeSnapshot snapshot = Parse(opened.Value.payload, out string error);
            if (snapshot == null)
            {
                return error;
            }

            History.Remember(code, opened.Value.payload, _clock());
            Save();
            _ = _cloud.Cloud.RecordVisit(code);
            Begin(code, snapshot, offline: false);
            Game.Events.Publish(new HomeVisitEvent("visited", true, string.Empty));
            return string.Empty;
        }

        /// <summary>Back to the player's own home.</summary>
        public void Leave()
        {
            if (!Visiting)
            {
                return;
            }

            if (Game.Services.TryGet(out HomeService home))
            {
                home.EndVisit();
            }

            CourtyardAvatar avatar = UnityEngine.Object.FindFirstObjectByType<CourtyardAvatar>();
            if (avatar != null)
            {
                avatar.EndVisit();
            }

            if (_ownEnvironment != null && Game.Services.TryGet(out SettlementView settlement) && settlement.Layout.environment != _ownEnvironment)
            {
                settlement.Load(_ownEnvironment);
            }

            _ownEnvironment = null;
            Current = null;
            CurrentSnapshot = null;
            OfflineCopy = false;
            Notes = string.Empty;
            Changed?.Invoke();
        }

        /// <summary>Reports the home being visited (or any code). Empty on success.</summary>
        public async Task<string> Report(string code, ReportReason reason)
        {
            if (_cloud.Cloud.Uid == null)
            {
                await _cloud.Cloud.SignInAnonymously();
            }

            CloudResult<bool> r = await _cloud.Cloud.Report(code, reason);
            Game.Events.Publish(new HomeVisitEvent("reported", r.Ok, r.Ok ? CloudRules.ReasonId(reason) : r.Error.ToString().ToLowerInvariant()));
            return r.Ok ? string.Empty : CloudRules.Explain(r.Error);
        }

        /// <summary>Takes one of the player's own codes down.</summary>
        public async Task<string> Withdraw(string code)
        {
            CloudResult<bool> r = await _cloud.Cloud.Withdraw(code);
            Game.Events.Publish(new HomeVisitEvent("withdrawn", r.Ok, r.Ok ? string.Empty : r.Error.ToString().ToLowerInvariant()));
            Changed?.Invoke();
            return r.Ok ? string.Empty : CloudRules.Explain(r.Error);
        }

        /// <summary>The player's own shared homes (empty when signed out).</summary>
        public async Task<List<ShareInfo>> MyHomes()
        {
            var homes = new List<ShareInfo>();
            if (!_cloud.SignedIn)
            {
                return homes;
            }

            CloudResult<List<ShareInfo>> r = await _cloud.Cloud.MyShares();
            if (r.Ok)
            {
                homes.AddRange(r.Value.FindAll(s => s.kind == ShareKind.Home));
            }

            return homes;
        }

        public void Forget(string code)
        {
            if (History.Forget(code))
            {
                Save();
                Changed?.Invoke();
            }
        }

        private void Begin(string code, HomeSnapshot snapshot, bool offline)
        {
            Leave();
            int tier = Game.Services.TryGet(out QualityManager quality) ? quality.Tier : 1;
            HouseDesign shown = HomeSnapshot.ForTier(snapshot.design, tier);
            var notes = new List<string>();
            if (shown.decorations.Count < snapshot.design.decorations.Count)
            {
                notes.Add($"Showing {shown.decorations.Count} of {snapshot.design.decorations.Count} decorations, as many as this phone shows smoothly.");
            }

            WalkController walk = UnityEngine.Object.FindFirstObjectByType<WalkController>();
            if (walk != null && walk.IsOpen)
            {
                walk.Close();
            }

            if (Game.Services.TryGet(out PlacementController placement))
            {
                placement.Cancel();
            }

            if (Game.Services.TryGet(out SettlementView settlement) && settlement.Layout != null && !string.IsNullOrEmpty(snapshot.environment)
                && snapshot.environment != settlement.Layout.environment)
            {
                if (Environments.Find(snapshot.environment) != null)
                {
                    _ownEnvironment = settlement.Layout.environment;
                    settlement.Load(snapshot.environment);
                }
                else
                {
                    notes.Add("Their neighbourhood isn't on this phone, so the house stands in yours.");
                }
            }

            if (Game.Services.TryGet(out HomeService home))
            {
                home.ShowVisit(shown);
            }

            CourtyardAvatar avatar = UnityEngine.Object.FindFirstObjectByType<CourtyardAvatar>();
            if (avatar != null)
            {
                avatar.ShowVisit(snapshot.hasAvatar ? snapshot.avatar : null);
            }

            Current = code;
            CurrentSnapshot = snapshot;
            OfflineCopy = offline;
            Notes = string.Join(" ", notes);
            Changed?.Invoke();
        }

        private string Fail(string action, CloudError error)
        {
            Game.Events.Publish(new HomeVisitEvent(action, false, error.ToString().ToLowerInvariant()));
            return CloudRules.Explain(error);
        }

        private VisitHistory Load()
        {
            try
            {
                if (File.Exists(_path))
                {
                    VisitHistory h = JsonUtility.FromJson<VisitHistory>(File.ReadAllText(_path));
                    if (h != null)
                    {
                        h.visits.RemoveAll(v => v == null || ShareCodes.Normalise(v.code) == null);
                        return h;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Visits] {_path} unreadable, starting with no visits: {e.Message}");
            }

            return new VisitHistory();
        }

        private void Save()
        {
            try
            {
                AtomicFile.WriteAllText(_path, JsonUtility.ToJson(History));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Visits] could not save {_path}: {e.Message}");
            }
        }
    }
}

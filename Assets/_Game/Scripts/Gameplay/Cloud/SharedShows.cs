using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UtsavNights.Core.Cloud;
using UtsavNights.Core.Runtime;
using UtsavNights.Core.Shows;

namespace UtsavNights.Gameplay
{
    /// <summary>A show was shared or opened by code (analytics in step 7).</summary>
    public readonly struct ShowShareEvent
    {
        public ShowShareEvent(string action, bool ok, string reason, int cues = 0)
        {
            Action = action;
            Ok = ok;
            Reason = reason;
            Cues = cues;
        }

        /// <summary>"shared" or "opened".</summary>
        public string Action { get; }

        public bool Ok { get; }

        public string Reason { get; }

        public int Cues { get; }
    }

    /// <summary>
    /// Fireworks shows shared by code (Phase 10 step 5, the PRD's "shared fireworks show, asynchronous first"). A show
    /// travels as its pads and cues only: its name stays on the sharer's phone (the code stands in for it), so there's no
    /// free text to moderate. The friend's phone loads it as a new show, checks it against its own spot and limits (a
    /// show from a high-end phone may need fewer crackers at once), plays it, and can keep a copy. The ten newest shows
    /// opened are kept for offline.
    /// </summary>
    public sealed class SharedShows
    {
        public const string FileName = "shows_received.json";

        private readonly CloudService _cloud;
        private readonly string _path;
        private readonly Func<long> _clock;

        public SharedShows(CloudService cloud, string folder = null, Func<long> clock = null)
        {
            _cloud = cloud;
            _path = Path.Combine(folder ?? Application.persistentDataPath, FileName);
            _clock = clock ?? (() => DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            Received = Load();
        }

        public VisitHistory Received { get; }

        /// <summary>The show as it travels: no id, no name.</summary>
        public static string Serialise(ShowDef show)
        {
            ShowDef copy = show.Clone();
            copy.id = string.Empty;
            copy.name = string.Empty;
            return JsonUtility.ToJson(copy);
        }

        /// <summary>A show from the network as a new show named after its code, or null with the reason.</summary>
        public static ShowDef Parse(string payload, string code, out string error)
        {
            error = null;
            ShowDef show;
            try
            {
                show = JsonUtility.FromJson<ShowDef>(payload);
            }
            catch (Exception)
            {
                show = null;
            }

            if (show == null || show.pads == null || show.cues == null || show.pads.Count == 0)
            {
                error = "That code isn't a fireworks show.";
                return null;
            }

            if (show.pads.Count > ShowRules.MaxPads || show.cues.Count > ShowRules.MaxCues)
            {
                error = "That show is bigger than the game allows.";
                return null;
            }

            show.cues.RemoveAll(c => c == null || c.pad < 0 || c.pad >= show.pads.Count || string.IsNullOrEmpty(c.cracker));
            show.name = "From " + ShareCodes.Display(code);
            show.id = string.Empty;
            return show;
        }

        /// <summary>Shares a show that plays on this phone. Returns its code, or null with the reason.</summary>
        public async Task<(string code, string error)> Share(ShowDef show)
        {
            if (!_cloud.SignedIn)
            {
                return (null, CloudRules.Explain(CloudError.SignedOut));
            }

            if (!Game.Services.TryGet(out ShowPlayer player))
            {
                return (null, "The show player isn't ready.");
            }

            if (player.Check(show) is { Count: > 0 } problems)
            {
                return (null, "Only a show that plays here can be shared: " + problems[0]);
            }

            CloudResult<string> account = await _cloud.Cloud.BackupKey();
            CloudResult<ShareInfo> published = account.Ok ? await _cloud.Cloud.Publish(ShareKind.Show, Serialise(show)) : new CloudResult<ShareInfo>(account.Error);
            Game.Events.Publish(new ShowShareEvent("shared", published.Ok, published.Ok ? string.Empty : published.Error.ToString().ToLowerInvariant(), show.cues.Count));
            return published.Ok ? (published.Value.code, null) : (null, CloudRules.Explain(published.Error));
        }

        /// <summary>Opens a friend's show by code (no account needed). Falls back to the copy kept on this phone when offline.</summary>
        public async Task<(ShowDef show, string error)> Open(string typed)
        {
            string code = ShareCodes.Normalise(typed);
            if (code == null)
            {
                return (null, CloudRules.Explain(CloudError.Invalid));
            }

            if (_cloud.Cloud.Uid == null)
            {
                await _cloud.Cloud.SignInAnonymously();
            }

            CloudResult<ShareInfo> opened = await _cloud.Cloud.Open(code);
            if (!opened.Ok)
            {
                VisitRecord kept = Received.Find(code);
                if ((opened.Error == CloudError.Offline || opened.Error == CloudError.NotConfigured || opened.Error == CloudError.Server) && kept != null)
                {
                    ShowDef copy = Parse(kept.payload, code, out _);
                    if (copy != null)
                    {
                        Game.Events.Publish(new ShowShareEvent("opened", true, "offline", copy.cues.Count));
                        return (copy, null);
                    }
                }

                if (opened.Error == CloudError.Hidden || opened.Error == CloudError.Withdrawn || opened.Error == CloudError.Expired || opened.Error == CloudError.NotFound)
                {
                    Forget(code);
                }

                Game.Events.Publish(new ShowShareEvent("opened", false, opened.Error.ToString().ToLowerInvariant()));
                return (null, CloudRules.Explain(opened.Error));
            }

            if (opened.Value.kind != ShareKind.Show)
            {
                return (null, "That code is a home, not a show. Open it from Visit.");
            }

            ShowDef show = Parse(opened.Value.payload, code, out string error);
            if (show == null)
            {
                return (null, error);
            }

            Received.Remember(code, opened.Value.payload, _clock());
            Save();
            _ = _cloud.Cloud.RecordVisit(code);
            Game.Events.Publish(new ShowShareEvent("opened", true, string.Empty, show.cues.Count));
            return (show, null);
        }

        public void Forget(string code)
        {
            if (Received.Forget(code))
            {
                Save();
            }
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
                Debug.LogWarning($"[Shows] {_path} unreadable, starting with no received shows: {e.Message}");
            }

            return new VisitHistory();
        }

        private void Save()
        {
            try
            {
                AtomicFile.WriteAllText(_path, JsonUtility.ToJson(Received));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Shows] could not save {_path}: {e.Message}");
            }
        }
    }
}

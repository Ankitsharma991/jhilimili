using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace UtsavNights.Core.Cloud
{
    /// <summary>
    /// The server side of <see cref="MemoryCloud"/>: every account, backup and share, kept in memory. One server, many
    /// clients, so tests can play two phones and two players. Its checks mirror Docs/Phase10/firebase/firestore.rules
    /// and storage.rules; when one changes, the other must too.
    /// </summary>
    public sealed class MemoryCloudServer
    {
        internal readonly Dictionary<string, Account> Accounts = new Dictionary<string, Account>();
        internal readonly Dictionary<string, ShareInfo> Shares = new Dictionary<string, ShareInfo>();
        internal readonly Dictionary<string, HashSet<string>> Reporters = new Dictionary<string, HashSet<string>>();
        private int _nextUid = 1;

        public MemoryCloudServer(Func<long> clock = null) => Clock = clock ?? (() => DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        public Func<long> Clock { get; set; }

        /// <summary>Tests: makes the next published code this one (to test a clash).</summary>
        public string NextCode { get; set; }

        public int ShareCount => Shares.Count;

        public int BackupCount(string uid) => Accounts.TryGetValue(uid ?? string.Empty, out Account a) ? a.Backups.Count : 0;

        /// <summary>Tests and the moderation review: a share as stored, or null.</summary>
        public ShareInfo Peek(string code) => Shares.TryGetValue(code ?? string.Empty, out ShareInfo s) ? s.Clone() : null;

        /// <summary>The review's decision on a hidden share: keep it (reports cleared) or remove it.</summary>
        public void Review(string code, bool keep)
        {
            if (!Shares.ContainsKey(code))
            {
                return;
            }

            if (keep)
            {
                Shares[code].reports = 0;
                Reporters.Remove(code);
            }
            else
            {
                Shares.Remove(code);
                Reporters.Remove(code);
            }
        }

        internal string NewUid(bool anonymous) => (anonymous ? "anon" : "user") + (_nextUid++).ToString("D4");

        internal sealed class Account
        {
            public string Key;
            public bool Anonymous;
            public long Day;
            public int SharesToday;
            public readonly List<(CloudBackupInfo info, byte[] data)> Backups = new List<(CloudBackupInfo, byte[])>();
        }
    }

    /// <summary>
    /// A client of <see cref="MemoryCloudServer"/> (Phase 10 step 2): what the tests use, and what the editor uses until
    /// Firebase is installed. Answers at once (completed tasks), unless <see cref="Offline"/>.
    /// </summary>
    public sealed class MemoryCloud : ICloud
    {
        private readonly MemoryCloudServer _server;

        public MemoryCloud(MemoryCloudServer server, string googleAccount = "google-1")
        {
            _server = server;
            GoogleAccount = googleAccount;
        }

        public string Name => "memory";

        /// <summary>Which Google account "signs in" (the same id on two phones is the same player); null cancels.</summary>
        public string GoogleAccount { get; set; }

        public bool Offline { get; set; }

        public string Uid { get; private set; }

        public bool IsAnonymous => Uid != null && _server.Accounts.TryGetValue(Uid, out MemoryCloudServer.Account a) && a.Anonymous;

        private long Now => _server.Clock();

        public Task<CloudResult<string>> SignInWithGoogle()
        {
            if (Offline)
            {
                return Done<string>(CloudError.Offline);
            }

            if (GoogleAccount == null)
            {
                return Done<string>(CloudError.Cancelled);
            }

            string uid = "g_" + GoogleAccount;
            if (!_server.Accounts.ContainsKey(uid))
            {
                _server.Accounts[uid] = new MemoryCloudServer.Account();
            }

            Uid = uid;
            return Done(new CloudResult<string>(uid));
        }

        public Task<CloudResult<string>> SignInAnonymously()
        {
            if (Offline)
            {
                return Done<string>(CloudError.Offline);
            }

            if (Uid == null)
            {
                Uid = _server.NewUid(true);
                _server.Accounts[Uid] = new MemoryCloudServer.Account { Anonymous = true };
            }

            return Done(new CloudResult<string>(Uid));
        }

        public void SignOut() => Uid = null;

        public Task<CloudResult<string>> BackupKey()
        {
            if (Guard(out CloudError e, requireGoogle: true))
            {
                return Done<string>(e);
            }

            MemoryCloudServer.Account a = _server.Accounts[Uid];
            a.Key ??= Convert.ToBase64String(Backup.BackupCrypto.NewKey());
            return Done(new CloudResult<string>(a.Key));
        }

        public Task<CloudResult<CloudBackupInfo>> PutBackup(byte[] sealedBackup, CloudBackupInfo info)
        {
            if (Guard(out CloudError e, requireGoogle: true))
            {
                return Done<CloudBackupInfo>(e);
            }

            if (sealedBackup == null || sealedBackup.Length == 0 || sealedBackup.Length > CloudRules.MaxBackupBytes)
            {
                return Done<CloudBackupInfo>(CloudError.TooLarge);
            }

            MemoryCloudServer.Account a = _server.Accounts[Uid];
            var stored = new CloudBackupInfo
            {
                id = Now.ToString("D12") + "_" + a.Backups.Count.ToString("D3"),
                createdUnix = Now,
                bytes = sealedBackup.Length,
                decorations = info?.decorations ?? 0,
                shows = info?.shows ?? 0,
                avatar = info?.avatar ?? false,
            };
            a.Backups.Add((stored, (byte[])sealedBackup.Clone()));

            // The newest few stay (the client deletes the rest; here the server does it for it).
            a.Backups.Sort((x, y) => string.CompareOrdinal(y.info.id, x.info.id));
            if (a.Backups.Count > CloudRules.KeptBackups)
            {
                a.Backups.RemoveRange(CloudRules.KeptBackups, a.Backups.Count - CloudRules.KeptBackups);
            }

            return Done(new CloudResult<CloudBackupInfo>(Copy(stored)));
        }

        public Task<CloudResult<List<CloudBackupInfo>>> ListBackups()
        {
            if (Guard(out CloudError e, requireGoogle: true))
            {
                return Done<List<CloudBackupInfo>>(e);
            }

            var list = new List<CloudBackupInfo>();
            foreach ((CloudBackupInfo info, byte[] _) in _server.Accounts[Uid].Backups)
            {
                list.Add(Copy(info));
            }

            return Done(new CloudResult<List<CloudBackupInfo>>(list));
        }

        public Task<CloudResult<byte[]>> GetBackup(string id)
        {
            if (Guard(out CloudError e, requireGoogle: true))
            {
                return Done<byte[]>(e);
            }

            foreach ((CloudBackupInfo info, byte[] data) in _server.Accounts[Uid].Backups)
            {
                if (info.id == id)
                {
                    return Done(new CloudResult<byte[]>((byte[])data.Clone()));
                }
            }

            return Done<byte[]>(CloudError.NotFound);
        }

        public Task<CloudResult<bool>> DeleteBackup(string id)
        {
            if (Guard(out CloudError e, requireGoogle: true))
            {
                return Done<bool>(e);
            }

            int removed = _server.Accounts[Uid].Backups.RemoveAll(b => b.info.id == id);
            return Done(removed > 0 ? new CloudResult<bool>(true) : new CloudResult<bool>(CloudError.NotFound));
        }

        public Task<CloudResult<ShareInfo>> Publish(ShareKind kind, string payload)
        {
            if (Guard(out CloudError e, requireGoogle: true))
            {
                return Done<ShareInfo>(e);
            }

            if (string.IsNullOrEmpty(payload) || payload.Length > CloudRules.MaxPayloadChars)
            {
                return Done<ShareInfo>(CloudError.TooLarge);
            }

            // rules: users/{uid} dayCount < SharesPerDay for today's day number, raised in the same batch.
            MemoryCloudServer.Account a = _server.Accounts[Uid];
            long day = Now / CloudRules.DaySeconds;
            if (a.Day != day)
            {
                a.Day = day;
                a.SharesToday = 0;
            }

            if (a.SharesToday >= CloudRules.SharesPerDay)
            {
                return Done<ShareInfo>(CloudError.Quota);
            }

            // rules: create only (a taken code is refused; the client tries another).
            string code = null;
            for (int attempt = 0; attempt < 5 && code == null; attempt++)
            {
                string candidate = _server.NextCode ?? ShareCodes.New();
                _server.NextCode = null;
                if (!_server.Shares.ContainsKey(candidate))
                {
                    code = candidate;
                }
            }

            if (code == null)
            {
                return Done<ShareInfo>(CloudError.Server);
            }

            a.SharesToday++;
            var share = new ShareInfo { code = code, kind = kind, owner = Uid, createdUnix = Now, lastVisitUnix = Now, payload = payload };
            _server.Shares[code] = share;
            return Done(new CloudResult<ShareInfo>(share.Clone()));
        }

        public Task<CloudResult<ShareInfo>> Open(string code)
        {
            if (Guard(out CloudError e, requireGoogle: false))
            {
                return Done<ShareInfo>(e);
            }

            string clean = ShareCodes.Normalise(code);
            if (clean == null)
            {
                return Done<ShareInfo>(CloudError.Invalid);
            }

            _server.Shares.TryGetValue(clean, out ShareInfo share);

            // rules: the owner can always read their own; anyone else only while it's visible.
            CloudError visible = CloudRules.Visibility(share, Now);
            if (visible != CloudError.None && !(share != null && share.owner == Uid && visible != CloudError.NotFound))
            {
                return Done<ShareInfo>(visible);
            }

            ShareInfo copy = share.Clone();
            if (share.owner != Uid)
            {
                copy.owner = string.Empty; // a visitor never learns the owner's account id
            }

            return Done(new CloudResult<ShareInfo>(copy));
        }

        public Task<CloudResult<bool>> RecordVisit(string code)
        {
            if (Guard(out CloudError e, requireGoogle: false))
            {
                return Done<bool>(e);
            }

            // rules: only visits (+1) and lastVisit (now) change, on a visible share, by someone other than the owner.
            if (!_server.Shares.TryGetValue(code ?? string.Empty, out ShareInfo share) || CloudRules.Visibility(share, Now) != CloudError.None)
            {
                return Done<bool>(CloudError.NotFound);
            }

            if (share.owner == Uid)
            {
                return Done(new CloudResult<bool>(false));
            }

            share.visits++;
            share.lastVisitUnix = Now;
            return Done(new CloudResult<bool>(true));
        }

        public Task<CloudResult<bool>> Report(string code, ReportReason reason)
        {
            if (Guard(out CloudError e, requireGoogle: false))
            {
                return Done<bool>(e);
            }

            if (!_server.Shares.TryGetValue(code ?? string.Empty, out ShareInfo share) || share.withdrawn)
            {
                return Done<bool>(CloudError.NotFound);
            }

            if (share.owner == Uid)
            {
                return Done<bool>(CloudError.Denied);
            }

            // rules: shares/{code}/reports/{uid} is created once (its id is the reporter), with reports raised by one.
            if (!_server.Reporters.TryGetValue(code, out HashSet<string> reporters))
            {
                reporters = _server.Reporters[code] = new HashSet<string>();
            }

            if (!reporters.Add(Uid))
            {
                return Done(new CloudResult<bool>(false)); // already reported by this account; counts once
            }

            share.reports++;
            return Done(new CloudResult<bool>(true));
        }

        public Task<CloudResult<bool>> Withdraw(string code)
        {
            if (Guard(out CloudError e, requireGoogle: true))
            {
                return Done<bool>(e);
            }

            if (!_server.Shares.TryGetValue(code ?? string.Empty, out ShareInfo share))
            {
                return Done<bool>(CloudError.NotFound);
            }

            if (share.owner != Uid)
            {
                return Done<bool>(CloudError.Denied);
            }

            share.withdrawn = true;
            share.payload = string.Empty; // nothing of it stays readable
            return Done(new CloudResult<bool>(true));
        }

        public Task<CloudResult<List<ShareInfo>>> MyShares()
        {
            if (Guard(out CloudError e, requireGoogle: true))
            {
                return Done<List<ShareInfo>>(e);
            }

            var mine = new List<ShareInfo>();
            foreach (ShareInfo s in _server.Shares.Values)
            {
                if (s.owner == Uid && !s.withdrawn)
                {
                    mine.Add(s.Clone());
                }
            }

            mine.Sort((x, y) => y.createdUnix.CompareTo(x.createdUnix));
            return Done(new CloudResult<List<ShareInfo>>(mine));
        }

        public Task<CloudResult<bool>> DeleteMyData()
        {
            if (Guard(out CloudError e, requireGoogle: true))
            {
                return Done<bool>(e);
            }

            var codes = new List<string>();
            foreach (ShareInfo s in _server.Shares.Values)
            {
                if (s.owner == Uid)
                {
                    codes.Add(s.code);
                }
            }

            foreach (string c in codes)
            {
                _server.Shares.Remove(c);
                _server.Reporters.Remove(c);
            }

            _server.Accounts.Remove(Uid);
            Uid = null;
            return Done(new CloudResult<bool>(true));
        }

        /// <summary>True (with the error) when the call can't go ahead: offline, signed out, or anonymous where Google is needed.</summary>
        private bool Guard(out CloudError error, bool requireGoogle)
        {
            error = Offline ? CloudError.Offline
                : Uid == null || !_server.Accounts.ContainsKey(Uid) ? CloudError.SignedOut
                : requireGoogle && IsAnonymous ? CloudError.SignedOut
                : CloudError.None;
            return error != CloudError.None;
        }

        private static CloudBackupInfo Copy(CloudBackupInfo i) => new CloudBackupInfo
        {
            id = i.id, createdUnix = i.createdUnix, bytes = i.bytes, decorations = i.decorations, shows = i.shows, avatar = i.avatar,
        };

        private static Task<CloudResult<T>> Done<T>(CloudResult<T> result) => Task.FromResult(result);

        private static Task<CloudResult<T>> Done<T>(CloudError error) => Task.FromResult(new CloudResult<T>(error));
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Storage;
using UnityEngine;
using UtsavNights.Core.Backup;
using UtsavNights.Core.Cloud;

namespace UtsavNights.FirebaseCloud
{
    /// <summary>
    /// The game's cloud on Firebase (Phase 10 step 6): Firebase Auth (Google through Credential Manager, or anonymous for
    /// visitors), Firestore (accounts, backup lists, shares, reports) and Storage (sealed backups). Every write has the
    /// shape Docs/Phase10/firebase/firestore.rules and storage.rules allow, so the rules are the real guard and this class
    /// only follows them. Errors come back as <see cref="CloudError"/>; nothing here throws to the game.
    /// </summary>
    public sealed class FirebaseCloud : ICloud
    {
        private const string ContentType = "application/octet-stream";
        private static Task<DependencyStatus> _ready;

        public string Name => "firebase";

        public string Uid => Ready ? Auth.CurrentUser?.UserId : null;

        public bool IsAnonymous => Ready && Auth.CurrentUser != null && Auth.CurrentUser.IsAnonymous;

        private static bool Ready => _ready != null && _ready.IsCompleted && !_ready.IsFaulted && _ready.Result == DependencyStatus.Available;

        private static FirebaseAuth Auth => FirebaseAuth.DefaultInstance;

        private static FirebaseFirestore Db => FirebaseFirestore.DefaultInstance;

        private static StorageReference Bucket => FirebaseStorage.DefaultInstance.RootReference;

        private static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        private static long Today => Now / CloudRules.DaySeconds;

        private static Timestamp ExpireAt => Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow.AddDays(CloudRules.ExpiryDays));

        public async Task<CloudResult<string>> SignInWithGoogle()
        {
            if (!await Start())
            {
                return CloudError.NotConfigured;
            }

            string answer = await GoogleSignInBridge.RequestIdToken();
            if (answer == "cancelled")
            {
                return CloudError.Cancelled;
            }

            if (!answer.StartsWith("ok:", StringComparison.Ordinal))
            {
                Debug.LogWarning("[Cloud] Google sign-in: " + answer);
                return new CloudResult<string>(answer.Contains("network") ? CloudError.Offline : CloudError.Server, answer);
            }

            return await Try(async () =>
            {
                Credential credential = GoogleAuthProvider.GetCredential(answer.Substring(3), null);
                FirebaseUser user = await Auth.SignInWithCredentialAsync(credential);
                return new CloudResult<string>(user.UserId);
            });
        }

        public async Task<CloudResult<string>> SignInAnonymously()
        {
            if (!await Start())
            {
                return CloudError.NotConfigured;
            }

            if (Auth.CurrentUser != null)
            {
                return new CloudResult<string>(Auth.CurrentUser.UserId);
            }

            return await Try(async () =>
            {
                AuthResult r = await Auth.SignInAnonymouslyAsync();
                return new CloudResult<string>(r.User.UserId);
            });
        }

        public void SignOut()
        {
            if (Ready)
            {
                Auth.SignOut();
                GoogleSignInBridge.ClearState();
            }
        }

        public Task<CloudResult<string>> BackupKey() => Google(async uid =>
        {
            DocumentReference account = Db.Document("users/" + uid);
            DocumentSnapshot snap = await account.GetSnapshotAsync(Source.Default);
            if (snap.Exists && snap.TryGetValue("backupKey", out string key, ServerTimestampBehavior.None) && !string.IsNullOrEmpty(key))
            {
                return new CloudResult<string>(key);
            }

            // rules: created once, with exactly these fields and a 44-character key.
            key = Convert.ToBase64String(BackupCrypto.NewKey());
            await account.SetAsync(new Dictionary<string, object> { { "backupKey", key }, { "day", 0L }, { "sharesToday", 0L } }, SetOptions.Overwrite);
            return new CloudResult<string>(key);
        });

        public Task<CloudResult<CloudBackupInfo>> PutBackup(byte[] sealedBackup, CloudBackupInfo info) => Google(async uid =>
        {
            if (sealedBackup == null || sealedBackup.Length == 0 || sealedBackup.Length > CloudRules.MaxBackupBytes)
            {
                return new CloudResult<CloudBackupInfo>(CloudError.TooLarge);
            }

            long now = Now;
            string id = now.ToString("D12") + "_" + (DateTime.UtcNow.Millisecond % 1000).ToString("D3");
            await Bucket.Child($"backups/{uid}/{id}.utsav").PutBytesAsync(sealedBackup, new MetadataChange { ContentType = ContentType }, null, default, null);
            var stored = new CloudBackupInfo
            {
                id = id, createdUnix = now, bytes = sealedBackup.Length, decorations = info?.decorations ?? 0, shows = info?.shows ?? 0, avatar = info?.avatar ?? false,
            };
            await Db.Document($"users/{uid}/backups/{id}").SetAsync(new Dictionary<string, object>
            {
                { "createdUnix", stored.createdUnix }, { "bytes", stored.bytes }, { "decorations", (long)stored.decorations }, { "shows", (long)stored.shows }, { "avatar", stored.avatar },
            }, SetOptions.Overwrite);
            return new CloudResult<CloudBackupInfo>(stored);
        });

        public Task<CloudResult<List<CloudBackupInfo>>> ListBackups() => Google(async uid =>
        {
            QuerySnapshot q = await Db.Collection($"users/{uid}/backups").OrderByDescending("createdUnix").GetSnapshotAsync(Source.Default);
            var list = new List<CloudBackupInfo>();
            foreach (DocumentSnapshot d in q.Documents)
            {
                list.Add(new CloudBackupInfo
                {
                    id = d.Id,
                    createdUnix = Long(d, "createdUnix"),
                    bytes = Long(d, "bytes"),
                    decorations = (int)Long(d, "decorations"),
                    shows = (int)Long(d, "shows"),
                    avatar = d.TryGetValue("avatar", out bool avatar, ServerTimestampBehavior.None) && avatar,
                });
            }

            return new CloudResult<List<CloudBackupInfo>>(list);
        });

        public Task<CloudResult<byte[]>> GetBackup(string id) => Google(async uid =>
        {
            byte[] data = await Bucket.Child($"backups/{uid}/{id}.utsav").GetBytesAsync(CloudRules.MaxBackupBytes);
            return new CloudResult<byte[]>(data);
        });

        public Task<CloudResult<bool>> DeleteBackup(string id) => Google(async uid =>
        {
            await Bucket.Child($"backups/{uid}/{id}.utsav").DeleteAsync();
            await Db.Document($"users/{uid}/backups/{id}").DeleteAsync();
            return new CloudResult<bool>(true);
        });

        public Task<CloudResult<ShareInfo>> Publish(ShareKind kind, string payload) => Google(async uid =>
        {
            if (string.IsNullOrEmpty(payload) || payload.Length > CloudRules.MaxPayloadChars)
            {
                return new CloudResult<ShareInfo>(CloudError.TooLarge);
            }

            // The day's count: +1 today, or 1 on a new day (rules: it only moves by publishing).
            DocumentSnapshot account = await Db.Document("users/" + uid).GetSnapshotAsync(Source.Server);
            long day = Long(account, "day");
            long count = day == Today ? Long(account, "sharesToday") + 1 : 1;
            if (count > CloudRules.SharesPerDay)
            {
                return new CloudResult<ShareInfo>(CloudError.Quota);
            }

            for (int attempt = 0; attempt < 3; attempt++)
            {
                string code = ShareCodes.New();
                long now = Now;
                WriteBatch batch = Db.StartBatch();
                batch.Update(Db.Document("users/" + uid), new Dictionary<string, object> { { "day", Today }, { "sharesToday", count } });
                batch.Set(Db.Document("shares/" + code), new Dictionary<string, object>
                {
                    { "kind", CloudRules.KindId(kind) }, { "owner", uid }, { "createdUnix", now }, { "lastVisitUnix", now }, { "visits", 0L }, { "reports", 0L },
                    { "withdrawn", false }, { "version", 1L }, { "expireAt", ExpireAt },
                }, SetOptions.Overwrite);
                batch.Set(Db.Document($"shares/{code}/content/main"), new Dictionary<string, object> { { "payload", payload } }, SetOptions.Overwrite);
                try
                {
                    await batch.CommitAsync();
                    return new CloudResult<ShareInfo>(new ShareInfo { code = code, kind = kind, owner = uid, createdUnix = now, lastVisitUnix = now, payload = payload });
                }
                catch (Exception e) when (Code(e) == FirestoreError.PermissionDenied || Code(e) == FirestoreError.AlreadyExists)
                {
                    // A taken code reads as denied (create only); try another. Three in a row is something else.
                }
            }

            return new CloudResult<ShareInfo>(CloudError.Denied);
        });

        public async Task<CloudResult<ShareInfo>> Open(string code)
        {
            string clean = ShareCodes.Normalise(code);
            if (clean == null)
            {
                return CloudError.Invalid;
            }

            return await Signed(async uid =>
            {
                DocumentSnapshot s = await Db.Document("shares/" + clean).GetSnapshotAsync(Source.Default);
                if (!s.Exists)
                {
                    return new CloudResult<ShareInfo>(CloudError.NotFound);
                }

                ShareInfo share = Share(s);
                bool mine = share.owner == uid;
                CloudError visible = CloudRules.Visibility(share, Now);
                if (visible != CloudError.None && !mine)
                {
                    return new CloudResult<ShareInfo>(visible);
                }

                DocumentSnapshot content = await Db.Document($"shares/{clean}/content/main").GetSnapshotAsync(Source.Default);
                if (!content.Exists || !content.TryGetValue("payload", out string payload, ServerTimestampBehavior.None))
                {
                    return new CloudResult<ShareInfo>(share.withdrawn ? CloudError.Withdrawn : CloudError.NotFound);
                }

                share.payload = payload;
                if (!mine)
                {
                    share.owner = string.Empty;
                }

                return new CloudResult<ShareInfo>(share);
            });
        }

        public Task<CloudResult<bool>> RecordVisit(string code) => Signed(async uid =>
        {
            DocumentSnapshot s = await Db.Document("shares/" + code).GetSnapshotAsync(Source.Default);
            if (s.Exists && s.TryGetValue("owner", out string owner, ServerTimestampBehavior.None) && owner == uid)
            {
                return new CloudResult<bool>(false); // rules: the owner's own look doesn't count
            }

            await Db.Document("shares/" + code).UpdateAsync(new Dictionary<string, object>
            {
                { "visits", FieldValue.Increment(1L) }, { "lastVisitUnix", Now }, { "expireAt", ExpireAt },
            });
            return new CloudResult<bool>(true);
        });

        public Task<CloudResult<bool>> Report(string code, ReportReason reason) => Signed(async uid =>
        {
            WriteBatch batch = Db.StartBatch();
            batch.Set(Db.Document($"shares/{code}/reports/{uid}"), new Dictionary<string, object> { { "reason", CloudRules.ReasonId(reason) }, { "unix", Now } }, SetOptions.Overwrite);
            batch.Update(Db.Document("shares/" + code), new Dictionary<string, object> { { "reports", FieldValue.Increment(1L) } });
            try
            {
                await batch.CommitAsync();
                return new CloudResult<bool>(true);
            }
            catch (Exception e) when (Code(e) == FirestoreError.PermissionDenied)
            {
                return new CloudResult<bool>(false); // already reported by this account (or the owner's own): counts once
            }
        });

        public Task<CloudResult<bool>> Withdraw(string code) => Google(async uid =>
        {
            WriteBatch batch = Db.StartBatch();
            batch.Update(Db.Document("shares/" + code), new Dictionary<string, object> { { "withdrawn", true } });
            batch.Delete(Db.Document($"shares/{code}/content/main"));
            await batch.CommitAsync();
            return new CloudResult<bool>(true);
        });

        public Task<CloudResult<List<ShareInfo>>> MyShares() => Google(async uid =>
        {
            QuerySnapshot q = await Db.Collection("shares").WhereEqualTo("owner", uid).OrderByDescending("createdUnix").GetSnapshotAsync(Source.Default);
            var mine = new List<ShareInfo>();
            foreach (DocumentSnapshot d in q.Documents)
            {
                ShareInfo s = Share(d);
                if (!s.withdrawn)
                {
                    mine.Add(s);
                }
            }

            return new CloudResult<List<ShareInfo>>(mine);
        });

        public Task<CloudResult<bool>> DeleteMyData() => Google(async uid =>
        {
            CloudResult<List<CloudBackupInfo>> backups = await ListBackups();
            foreach (CloudBackupInfo b in backups.Value ?? new List<CloudBackupInfo>())
            {
                await DeleteBackup(b.id);
            }

            QuerySnapshot shares = await Db.Collection("shares").WhereEqualTo("owner", uid).GetSnapshotAsync(Source.Server);
            foreach (DocumentSnapshot s in shares.Documents)
            {
                WriteBatch batch = Db.StartBatch();
                batch.Delete(Db.Document($"shares/{s.Id}/content/main"));
                batch.Delete(s.Reference);
                await batch.CommitAsync();
            }

            await Db.Document("users/" + uid).DeleteAsync();
            try
            {
                await Auth.CurrentUser.DeleteAsync(); // the account itself, when Firebase allows it without a fresh sign-in
            }
            catch (Exception e)
            {
                Debug.Log("[Cloud] the account stays (its data is gone): " + e.GetBaseException().Message);
            }

            SignOut();
            return new CloudResult<bool>(true);
        });

        private static async Task<bool> Start()
        {
            _ready ??= FirebaseApp.CheckAndFixDependenciesAsync();
            try
            {
                return await _ready == DependencyStatus.Available;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Cloud] Firebase unavailable: " + e.GetBaseException().Message);
                return false;
            }
        }

        /// <summary>Runs a call that needs the Google sign-in (not a visitor's anonymous account).</summary>
        private async Task<CloudResult<T>> Google<T>(Func<string, Task<CloudResult<T>>> call)
        {
            if (!await Start())
            {
                return CloudError.NotConfigured;
            }

            return Auth.CurrentUser == null || Auth.CurrentUser.IsAnonymous ? CloudError.SignedOut : await Try(() => call(Auth.CurrentUser.UserId));
        }

        /// <summary>Runs a call that any account may make, anonymous included.</summary>
        private async Task<CloudResult<T>> Signed<T>(Func<string, Task<CloudResult<T>>> call)
        {
            if (!await Start())
            {
                return CloudError.NotConfigured;
            }

            return Auth.CurrentUser == null ? CloudError.SignedOut : await Try(() => call(Auth.CurrentUser.UserId));
        }

        private static async Task<CloudResult<T>> Try<T>(Func<Task<CloudResult<T>>> call)
        {
            try
            {
                return await call();
            }
            catch (Exception e)
            {
                Exception root = e.GetBaseException();
                CloudError error = Map(root);
                Debug.LogWarning($"[Cloud] {error}: {root.Message}");
                return new CloudResult<T>(error, root.Message);
            }
        }

        private static CloudError Map(Exception e)
        {
            switch (e)
            {
                case FirestoreException f:
                    switch (f.ErrorCode)
                    {
                        case FirestoreError.Unavailable:
                        case FirestoreError.DeadlineExceeded:
                            return CloudError.Offline;
                        case FirestoreError.PermissionDenied:
                            return CloudError.Denied;
                        case FirestoreError.NotFound:
                            return CloudError.NotFound;
                        case FirestoreError.ResourceExhausted:
                            return CloudError.Quota;
                        case FirestoreError.Unauthenticated:
                            return CloudError.SignedOut;
                        case FirestoreError.Cancelled:
                            return CloudError.Cancelled;
                        default:
                            return CloudError.Server;
                    }

                case StorageException s:
                    if (s.ErrorCode == StorageException.ErrorObjectNotFound)
                    {
                        return CloudError.NotFound;
                    }

                    if (s.ErrorCode == StorageException.ErrorNotAuthenticated)
                    {
                        return CloudError.SignedOut;
                    }

                    if (s.ErrorCode == StorageException.ErrorNotAuthorized)
                    {
                        return CloudError.Denied;
                    }

                    if (s.ErrorCode == StorageException.ErrorQuotaExceeded)
                    {
                        return CloudError.Quota;
                    }

                    return s.IsRecoverableException ? CloudError.Offline : CloudError.Server;

                case FirebaseException fe when fe.Message.IndexOf("network", StringComparison.OrdinalIgnoreCase) >= 0:
                    return CloudError.Offline;

                default:
                    return CloudError.Server;
            }
        }

        private static FirestoreError Code(Exception e) => e.GetBaseException() is FirestoreException f ? f.ErrorCode : FirestoreError.Unknown;

        private static ShareInfo Share(DocumentSnapshot d)
        {
            d.TryGetValue("kind", out string kind, ServerTimestampBehavior.None);
            d.TryGetValue("owner", out string owner, ServerTimestampBehavior.None);
            d.TryGetValue("withdrawn", out bool withdrawn, ServerTimestampBehavior.None);
            return new ShareInfo
            {
                code = d.Id,
                kind = kind == "show" ? ShareKind.Show : ShareKind.Home,
                owner = owner ?? string.Empty,
                createdUnix = Long(d, "createdUnix"),
                lastVisitUnix = Long(d, "lastVisitUnix"),
                visits = (int)Long(d, "visits"),
                reports = (int)Long(d, "reports"),
                withdrawn = withdrawn,
                version = (int)Math.Max(1, Long(d, "version")),
            };
        }

        private static long Long(DocumentSnapshot d, string field) =>
            d != null && d.Exists && d.TryGetValue(field, out long value, ServerTimestampBehavior.None) ? value : 0L;
    }
}

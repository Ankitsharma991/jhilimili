using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UtsavNights.Core.Backup;
using UtsavNights.Core.Cloud;
using UtsavNights.Core.Runtime;

namespace UtsavNights.Gameplay
{
    /// <summary>The account changed (signed in, out, or deleted).</summary>
    public readonly struct CloudAccountEvent
    {
        public CloudAccountEvent(bool signedIn) => SignedIn = signedIn;

        public bool SignedIn { get; }
    }

    /// <summary>A cloud backup was made or restored (analytics in step 7: counts only).</summary>
    public readonly struct CloudBackupEvent
    {
        public CloudBackupEvent(bool restored, bool ok, string reason)
        {
            Restored = restored;
            Ok = ok;
            Reason = reason;
        }

        public bool Restored { get; }

        public bool Ok { get; }

        /// <summary>A short id from <see cref="CloudError"/> when it failed.</summary>
        public string Reason { get; }
    }

    /// <summary>
    /// The optional account and cloud backup (Phase 10 step 3, F24). Signing in is only for this and for owning share
    /// codes; the game never needs it. A cloud backup is the same archive as a backup file, sealed with the account's own
    /// key (kept in the account's Firestore document, apart from the files), so the storage only ever holds ciphertext
    /// and a new phone signed in to the same Google account can open it. Backups are made only when the player asks.
    /// Every method answers in plain words: an empty string means it worked.
    /// </summary>
    public sealed class CloudService
    {
        public CloudService(ICloud cloud, BackupService backup)
        {
            Cloud = cloud;
            Backup = backup;
        }

        public ICloud Cloud { get; }

        public BackupService Backup { get; }

        public bool Configured => !(Cloud is UnconfiguredCloud);

        /// <summary>Signed in with Google (not just a visitor's anonymous account).</summary>
        public bool SignedIn => Cloud.Uid != null && !Cloud.IsAnonymous;

        public async Task<string> SignIn()
        {
            if (SignedIn)
            {
                return string.Empty;
            }

            if (Cloud.Uid != null)
            {
                Cloud.SignOut(); // a visitor's anonymous account makes way for the Google one
            }

            CloudResult<string> r = await Cloud.SignInWithGoogle();
            if (r.Ok)
            {
                Game.Events.Publish(new CloudAccountEvent(true));
            }

            return r.Ok ? string.Empty : CloudRules.Explain(r.Error);
        }

        public void SignOut()
        {
            Cloud.SignOut();
            Game.Events.Publish(new CloudAccountEvent(false));
        }

        /// <summary>Backs up now and keeps the newest three.</summary>
        public async Task<string> BackUpNow()
        {
            if (!SignedIn)
            {
                return Done(false, CloudError.SignedOut);
            }

            CloudResult<string> key = await Cloud.BackupKey();
            if (!key.Ok)
            {
                return Done(false, key.Error);
            }

            BackupArchive archive = Backup.Collect();
            if (Backup.Check(archive, out BackupSummary summary) is string problem)
            {
                return problem;
            }

            byte[] keyBytes = Convert.FromBase64String(key.Value);
            byte[] sealedBytes = await Task.Run(() => BackupCrypto.SealWithKey(archive, keyBytes));
            var info = new CloudBackupInfo { decorations = summary.Decorations, shows = summary.Shows, avatar = summary.Avatar };
            CloudResult<CloudBackupInfo> put = await Cloud.PutBackup(sealedBytes, info);
            if (!put.Ok)
            {
                return Done(false, put.Error);
            }

            // Older ones go (the memory cloud already did; Firebase leaves it to the client).
            CloudResult<List<CloudBackupInfo>> list = await Cloud.ListBackups();
            if (list.Ok)
            {
                for (int i = CloudRules.KeptBackups; i < list.Value.Count; i++)
                {
                    await Cloud.DeleteBackup(list.Value[i].id);
                }
            }

            return Done(false, CloudError.None);
        }

        /// <summary>The account's backups, newest first (empty with the reason when they can't be listed).</summary>
        public async Task<(List<CloudBackupInfo> backups, string error)> List()
        {
            if (!SignedIn)
            {
                return (new List<CloudBackupInfo>(), CloudRules.Explain(CloudError.SignedOut));
            }

            CloudResult<List<CloudBackupInfo>> r = await Cloud.ListBackups();
            return r.Ok ? (r.Value, string.Empty) : (new List<CloudBackupInfo>(), CloudRules.Explain(r.Error));
        }

        /// <summary>Downloads, opens and checks one backup, ready for the confirm step. Nothing is written.</summary>
        public async Task<(BackupArchive archive, BackupSummary summary, string error)> Fetch(string id)
        {
            if (!SignedIn)
            {
                return (null, default, CloudRules.Explain(CloudError.SignedOut));
            }

            CloudResult<string> key = await Cloud.BackupKey();
            CloudResult<byte[]> data = key.Ok ? await Cloud.GetBackup(id) : new CloudResult<byte[]>(key.Error);
            if (!data.Ok)
            {
                return (null, default, Done(true, data.Error));
            }

            byte[] keyBytes = Convert.FromBase64String(key.Value);
            (BackupError error, BackupArchive archive) = await Task.Run(() =>
            {
                BackupError e = BackupCrypto.OpenWithKey(data.Value, keyBytes, out BackupArchive a);
                return (e, a);
            });
            if (error != BackupError.None)
            {
                Game.Events.Publish(new CloudBackupEvent(true, false, error.ToString().ToLowerInvariant()));
                return (null, default, BackupRules.Explain(error));
            }

            string problem = Backup.Check(archive, out BackupSummary summary);
            return (problem == null ? archive : null, summary, problem ?? string.Empty);
        }

        /// <summary>Restores a fetched backup (the caller reloads the scene).</summary>
        public string Restore(BackupArchive archive)
        {
            bool ok = Backup.Apply(archive, out string error);
            Game.Events.Publish(new CloudBackupEvent(true, ok, ok ? string.Empty : "write"));
            return ok ? string.Empty : error;
        }

        /// <summary>Deletes the account's backups, key and shares from the cloud, and signs out. This phone keeps its own saves.</summary>
        public async Task<string> DeleteMyData()
        {
            if (!SignedIn)
            {
                return CloudRules.Explain(CloudError.SignedOut);
            }

            CloudResult<bool> r = await Cloud.DeleteMyData();
            if (r.Ok)
            {
                Cloud.SignOut();
                Game.Events.Publish(new CloudAccountEvent(false));
            }

            return r.Ok ? string.Empty : CloudRules.Explain(r.Error);
        }

        private static string Done(bool restored, CloudError error)
        {
            Game.Events.Publish(new CloudBackupEvent(restored, error == CloudError.None, error == CloudError.None ? string.Empty : error.ToString().ToLowerInvariant()));
            return CloudRules.Explain(error);
        }
    }
}

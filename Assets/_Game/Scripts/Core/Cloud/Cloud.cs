using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace UtsavNights.Core.Cloud
{
    public enum CloudError
    {
        None,
        Offline,

        /// <summary>The game has no server yet (Firebase not connected).</summary>
        NotConfigured,
        SignedOut,
        Cancelled,
        NotFound,

        /// <summary>Hidden after reports, until reviewed.</summary>
        Hidden,
        Withdrawn,
        Expired,

        /// <summary>Too many shares today, or too many backups.</summary>
        Quota,
        TooLarge,
        Denied,
        Invalid,
        Server,
    }

    public readonly struct CloudResult<T>
    {
        public CloudResult(T value)
        {
            Value = value;
            Error = CloudError.None;
            Detail = string.Empty;
        }

        public CloudResult(CloudError error, string detail = "")
        {
            Value = default;
            Error = error;
            Detail = detail ?? string.Empty;
        }

        public T Value { get; }

        public CloudError Error { get; }

        /// <summary>For logs only; the player sees <see cref="CloudRules.Explain"/>.</summary>
        public string Detail { get; }

        public bool Ok => Error == CloudError.None;

        public static implicit operator CloudResult<T>(CloudError error) => new CloudResult<T>(error);
    }

    public enum ShareKind
    {
        Home,
        Show,
    }

    /// <summary>A fixed list, so a report never carries free text.</summary>
    public enum ReportReason
    {
        Offensive,
        Inappropriate,
        Spam,
        Other,
    }

    /// <summary>A backup in the cloud: when, how big and a few counts for the restore list. Never the contents.</summary>
    public sealed class CloudBackupInfo
    {
        public string id = string.Empty;
        public long createdUnix;
        public long bytes;
        public int decorations;
        public int shows;
        public bool avatar;
    }

    /// <summary>A shared home or show as the server keeps it.</summary>
    public sealed class ShareInfo
    {
        public string code = string.Empty;
        public ShareKind kind;
        public string owner = string.Empty;
        public long createdUnix;
        public long lastVisitUnix;
        public int visits;
        public int reports;
        public bool withdrawn;
        public int version = 1;

        /// <summary>The snapshot as JSON (step 4 for homes, step 5 for shows).</summary>
        public string payload = string.Empty;

        public ShareInfo Clone() => (ShareInfo)MemberwiseClone();
    }

    /// <summary>
    /// What the game needs from its server (Phase 10 step 2). The Firebase backend (step 6) does these with Firebase Auth,
    /// Firestore and Storage, where the rules files enforce what <see cref="MemoryCloud"/> enforces here: a player reads
    /// and writes only their own account and backups; anyone signed in (an anonymous visitor too) can open a visible share
    /// by its code but not list them; reports and visits change only their counters.
    /// </summary>
    public interface ICloud
    {
        string Name { get; }

        /// <summary>The account id, or null when signed out.</summary>
        string Uid { get; }

        /// <summary>True for a visitor's silent anonymous account (no backups on it).</summary>
        bool IsAnonymous { get; }

        Task<CloudResult<string>> SignInWithGoogle();

        /// <summary>Only to open a share without a Google account.</summary>
        Task<CloudResult<string>> SignInAnonymously();

        void SignOut();

        /// <summary>The account's backup key (created on first use; base64 of 32 bytes).</summary>
        Task<CloudResult<string>> BackupKey();

        Task<CloudResult<CloudBackupInfo>> PutBackup(byte[] sealedBackup, CloudBackupInfo info);

        /// <summary>Newest first.</summary>
        Task<CloudResult<List<CloudBackupInfo>>> ListBackups();

        Task<CloudResult<byte[]>> GetBackup(string id);

        Task<CloudResult<bool>> DeleteBackup(string id);

        /// <summary>Publishes a payload and returns its new code.</summary>
        Task<CloudResult<ShareInfo>> Publish(ShareKind kind, string payload);

        /// <summary>Opens a share by code (any account, anonymous included). Hidden, withdrawn and expired shares are refused.</summary>
        Task<CloudResult<ShareInfo>> Open(string code);

        /// <summary>Counts a visit (and keeps the share from expiring).</summary>
        Task<CloudResult<bool>> RecordVisit(string code);

        /// <summary>One report per account per share.</summary>
        Task<CloudResult<bool>> Report(string code, ReportReason reason);

        Task<CloudResult<bool>> Withdraw(string code);

        Task<CloudResult<List<ShareInfo>>> MyShares();

        /// <summary>Deletes everything this account has in the cloud: backups, its key and its shares.</summary>
        Task<CloudResult<bool>> DeleteMyData();
    }

    /// <summary>
    /// Share codes: 8 characters from digits and consonants only. With no vowels (and no Y) no code spells a word, and
    /// the lookalikes are gone: no 0, O, 1, I or L, and no S or Z beside 5 and 2. 25 characters, about six billion
    /// codes. The last character is a check character, so a mistyped code is caught on the phone before asking the
    /// server. Shown as "BCDF-2345".
    /// </summary>
    public static class ShareCodes
    {
        public const string Alphabet = "23456789BCDFGHJKMNPQRTVWX";
        public const int Length = 8;

        public static string New()
        {
            var chars = new char[Length];
            var random = new byte[Length - 1];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(random);
            }

            for (int i = 0; i < Length - 1; i++)
            {
                chars[i] = Alphabet[random[i] % Alphabet.Length];
            }

            chars[Length - 1] = Check(chars, Length - 1);
            return new string(chars);
        }

        /// <summary>The typed code tidied (spaces and dashes dropped, upper case), or null if it isn't a valid code.</summary>
        public static string Normalise(string typed)
        {
            if (string.IsNullOrWhiteSpace(typed))
            {
                return null;
            }

            var sb = new StringBuilder(Length);
            foreach (char c in typed.ToUpperInvariant())
            {
                if (c == ' ' || c == '-' || c == '_')
                {
                    continue;
                }

                if (Alphabet.IndexOf(c) < 0)
                {
                    return null;
                }

                sb.Append(c);
            }

            string code = sb.ToString();
            return code.Length == Length && Check(code.ToCharArray(), Length - 1) == code[Length - 1] ? code : null;
        }

        public static string Display(string code) => code != null && code.Length == Length ? code.Substring(0, 4) + "-" + code.Substring(4) : code;

        /// <summary>Weights coprime to 25 (no 5) whose neighbours differ by 1 or 2: every single wrong character and every swapped pair changes the check.</summary>
        private static readonly int[] Weights = { 1, 2, 3, 4, 6, 7, 8 };

        private static char Check(char[] chars, int count)
        {
            int sum = 0;
            for (int i = 0; i < count; i++)
            {
                sum += Weights[i] * Alphabet.IndexOf(chars[i]);
            }

            return Alphabet[sum % Alphabet.Length];
        }
    }

    /// <summary>Moderation, quotas and limits, the same on the phone, in the memory cloud and in the Firebase rules.</summary>
    public static class CloudRules
    {
        public const int HideAtReports = 3;
        public const int SharesPerDay = 10;
        public const int ExpiryDays = 365;
        public const int KeptBackups = 3;
        public const int MaxPayloadChars = 200_000;
        public const int MaxBackupBytes = 8 * 1024 * 1024;
        public const long DaySeconds = 24 * 60 * 60;

        public static string KindId(ShareKind kind) => kind == ShareKind.Home ? "home" : "show";

        public static string ReasonId(ReportReason reason) => reason.ToString().ToLowerInvariant();

        public static bool Expired(ShareInfo share, long nowUnix) => nowUnix - Math.Max(share.lastVisitUnix, share.createdUnix) > ExpiryDays * DaySeconds;

        /// <summary>Why a share can't be opened now, or None.</summary>
        public static CloudError Visibility(ShareInfo share, long nowUnix)
        {
            if (share == null)
            {
                return CloudError.NotFound;
            }

            if (share.withdrawn)
            {
                return CloudError.Withdrawn;
            }

            if (share.reports >= HideAtReports)
            {
                return CloudError.Hidden;
            }

            return Expired(share, nowUnix) ? CloudError.Expired : CloudError.None;
        }

        /// <summary>The player's words for a cloud error.</summary>
        public static string Explain(CloudError error)
        {
            switch (error)
            {
                case CloudError.Offline:
                    return "No connection. Try again when you're online.";
                case CloudError.NotConfigured:
                    return "This needs the game's online service, which isn't switched on yet.";
                case CloudError.SignedOut:
                    return "Sign in with Google first.";
                case CloudError.Cancelled:
                    return "Cancelled.";
                case CloudError.NotFound:
                    return "There's nothing at that code. Check it and try again.";
                case CloudError.Hidden:
                    return "That code has been hidden after reports, while it's reviewed.";
                case CloudError.Withdrawn:
                    return "Its owner has taken that code down.";
                case CloudError.Expired:
                    return "That code has expired.";
                case CloudError.Quota:
                    return "That's the limit for today. Try again tomorrow.";
                case CloudError.TooLarge:
                    return "That's too large to share.";
                case CloudError.Denied:
                    return "That isn't allowed.";
                case CloudError.Invalid:
                    return "That code isn't right. Codes have 8 letters and numbers, like BCDF-2345.";
                case CloudError.Server:
                    return "The online service had a problem. Try again later.";
                default:
                    return string.Empty;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace UtsavNights.Core.Backup
{
    /// <summary>
    /// The plain contents of a backup (Phase 10 step 1, PRD 13.5): named parts, each a save file's text, with when and by
    /// which version it was made. Parts are read by name, so a newer game can add parts an older one skips.
    /// </summary>
    public sealed class BackupArchive
    {
        public const int CurrentVersion = 1;
        private const string Magic = "UTSA";

        public int version = CurrentVersion;
        public long createdUnix;
        public string appVersion = string.Empty;
        public readonly SortedDictionary<string, string> parts = new SortedDictionary<string, string>(StringComparer.Ordinal);

        public string Part(string name) => parts.TryGetValue(name, out string text) ? text : null;

        public byte[] ToBytes()
        {
            using var stream = new MemoryStream();
            using (var w = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                w.Write(Encoding.ASCII.GetBytes(Magic));
                w.Write(version);
                w.Write(createdUnix);
                w.Write(appVersion ?? string.Empty);
                w.Write(parts.Count);
                foreach (KeyValuePair<string, string> p in parts)
                {
                    w.Write(p.Key);
                    w.Write(p.Value ?? string.Empty);
                }
            }

            return stream.ToArray();
        }

        /// <summary>Reads an archive; null when the bytes aren't one (or break the size rules).</summary>
        public static BackupArchive FromBytes(byte[] data)
        {
            try
            {
                using var r = new BinaryReader(new MemoryStream(data), Encoding.UTF8);
                if (Encoding.ASCII.GetString(r.ReadBytes(4)) != Magic)
                {
                    return null;
                }

                var a = new BackupArchive { version = r.ReadInt32(), createdUnix = r.ReadInt64(), appVersion = r.ReadString() };
                int count = r.ReadInt32();
                if (count < 0 || count > BackupRules.MaxParts)
                {
                    return null;
                }

                for (int i = 0; i < count; i++)
                {
                    string name = r.ReadString();
                    string text = r.ReadString();
                    if (text.Length > BackupRules.MaxPartChars || a.parts.ContainsKey(name))
                    {
                        return null;
                    }

                    a.parts[name] = text;
                }

                return a;
            }
            catch (Exception e) when (e is EndOfStreamException || e is IOException || e is FormatException || e is ArgumentException)
            {
                return null;
            }
        }
    }

    public enum BackupError
    {
        None,

        /// <summary>Not a backup file at all.</summary>
        NotABackup,

        /// <summary>Made by a newer version of the game.</summary>
        Newer,

        /// <summary>The password is wrong, or the file was changed or damaged (the check can't tell which).</summary>
        WrongKeyOrDamaged,

        TooLarge,
    }

    /// <summary>
    /// Seals an archive: AES-256-CBC, then HMAC-SHA256 over the header and the ciphertext (encrypt then MAC), so a wrong
    /// key or any changed byte is caught before decryption. The key is either a password (PBKDF2-SHA256, for export files)
    /// or 32 random bytes (the cloud backup's account key).
    ///
    /// Layout: "UTSB" | format (1) | kdf (0 key, 1 password) | iterations (int32) | salt (16) | iv (16) | ciphertext | mac (32).
    /// </summary>
    public static class BackupCrypto
    {
        public const int Format = 1;
        public const int KeyBytes = 32;
        public const int PasswordIterations = 120_000;
        private const int MinIterations = 10_000;
        private const int MaxIterations = 2_000_000;
        private const int SaltBytes = 16;
        private const int IvBytes = 16;
        private const int MacBytes = 32;
        private const int HeaderBytes = 4 + 1 + 1 + 4 + SaltBytes + IvBytes;
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("UTSB");

        public static byte[] NewKey()
        {
            var key = new byte[KeyBytes];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(key);
            }

            return key;
        }

        public static byte[] SealWithPassword(BackupArchive archive, string password) => Seal(archive, 1, PasswordIterations, password, null);

        public static byte[] SealWithKey(BackupArchive archive, byte[] key) => Seal(archive, 0, 0, null, key);

        public static BackupError OpenWithPassword(byte[] sealedBytes, string password, out BackupArchive archive) => Open(sealedBytes, password, null, out archive);

        public static BackupError OpenWithKey(byte[] sealedBytes, byte[] key, out BackupArchive archive) => Open(sealedBytes, null, key, out archive);

        /// <summary>True when the bytes start like a sealed backup (to tell a backup file from any other file).</summary>
        public static bool LooksLikeBackup(byte[] data) => data != null && data.Length >= HeaderBytes + MacBytes && StartsWithMagic(data);

        /// <summary>Whether a sealed backup needs a password (an export file) or the account key (a cloud backup).</summary>
        public static bool NeedsPassword(byte[] data) => LooksLikeBackup(data) && data[5] == 1;

        private static byte[] Seal(BackupArchive archive, byte kdf, int iterations, string password, byte[] key)
        {
            byte[] plain = archive.ToBytes();
            if (plain.Length > BackupRules.MaxBytes)
            {
                throw new InvalidOperationException("the backup is too large");
            }

            var header = new byte[HeaderBytes];
            Array.Copy(Magic, header, 4);
            header[4] = Format;
            header[5] = kdf;
            BitConverter.GetBytes(iterations).CopyTo(header, 6);
            using (var rng = RandomNumberGenerator.Create())
            {
                var salt = new byte[SaltBytes + IvBytes];
                rng.GetBytes(salt);
                Array.Copy(salt, 0, header, 10, salt.Length);
            }

            (byte[] enc, byte[] mac) = Keys(kdf, iterations, password, key, header);
            byte[] cipher;
            using (Aes aes = Aes.Create())
            {
                aes.Key = enc;
                aes.IV = Slice(header, 10 + SaltBytes, IvBytes);
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                using ICryptoTransform t = aes.CreateEncryptor();
                cipher = t.TransformFinalBlock(plain, 0, plain.Length);
            }

            var output = new byte[HeaderBytes + cipher.Length + MacBytes];
            header.CopyTo(output, 0);
            cipher.CopyTo(output, HeaderBytes);
            using (var hmac = new HMACSHA256(mac))
            {
                hmac.ComputeHash(output, 0, HeaderBytes + cipher.Length).CopyTo(output, HeaderBytes + cipher.Length);
            }

            return output;
        }

        private static BackupError Open(byte[] data, string password, byte[] key, out BackupArchive archive)
        {
            archive = null;
            if (!LooksLikeBackup(data))
            {
                return BackupError.NotABackup;
            }

            if (data.Length > BackupRules.MaxBytes + HeaderBytes + MacBytes + 32)
            {
                return BackupError.TooLarge;
            }

            if (data[4] > Format)
            {
                return BackupError.Newer;
            }

            byte kdf = data[5];
            int iterations = BitConverter.ToInt32(data, 6);
            if (kdf > 1 || (kdf == 1 && (password == null || iterations < MinIterations || iterations > MaxIterations)) || (kdf == 0 && (key == null || key.Length != KeyBytes)))
            {
                return BackupError.WrongKeyOrDamaged;
            }

            int cipherLength = data.Length - HeaderBytes - MacBytes;
            if (cipherLength <= 0 || cipherLength % 16 != 0)
            {
                return BackupError.WrongKeyOrDamaged;
            }

            (byte[] enc, byte[] mac) = Keys(kdf, iterations, password, key, Slice(data, 0, HeaderBytes));
            using (var hmac = new HMACSHA256(mac))
            {
                byte[] expected = hmac.ComputeHash(data, 0, HeaderBytes + cipherLength);
                if (!FixedTimeEquals(expected, data, HeaderBytes + cipherLength))
                {
                    return BackupError.WrongKeyOrDamaged;
                }
            }

            byte[] plain;
            try
            {
                using Aes aes = Aes.Create();
                aes.Key = enc;
                aes.IV = Slice(data, 10 + SaltBytes, IvBytes);
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                using ICryptoTransform t = aes.CreateDecryptor();
                plain = t.TransformFinalBlock(data, HeaderBytes, cipherLength);
            }
            catch (CryptographicException)
            {
                return BackupError.WrongKeyOrDamaged;
            }

            archive = BackupArchive.FromBytes(plain);
            if (archive == null)
            {
                return BackupError.WrongKeyOrDamaged;
            }

            if (archive.version > BackupArchive.CurrentVersion)
            {
                archive = null;
                return BackupError.Newer;
            }

            return BackupError.None;
        }

        /// <summary>Separate encryption and MAC keys from the password (PBKDF2) or the account key (HMAC labels).</summary>
        private static (byte[] enc, byte[] mac) Keys(byte kdf, int iterations, string password, byte[] key, byte[] header)
        {
            if (kdf == 1)
            {
                using var pbkdf2 = new Rfc2898DeriveBytes(Encoding.UTF8.GetBytes(password), Slice(header, 10, SaltBytes), iterations, HashAlgorithmName.SHA256);
                byte[] both = pbkdf2.GetBytes(64);
                return (Slice(both, 0, 32), Slice(both, 32, 32));
            }

            using var h = new HMACSHA256(key);
            byte[] salt = Slice(header, 10, SaltBytes);
            return (h.ComputeHash(Concat(Encoding.ASCII.GetBytes("utsav-backup-enc"), salt)), h.ComputeHash(Concat(Encoding.ASCII.GetBytes("utsav-backup-mac"), salt)));
        }

        private static bool StartsWithMagic(byte[] data)
        {
            for (int i = 0; i < Magic.Length; i++)
            {
                if (data[i] != Magic[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool FixedTimeEquals(byte[] expected, byte[] data, int offset)
        {
            int diff = 0;
            for (int i = 0; i < expected.Length; i++)
            {
                diff |= expected[i] ^ data[offset + i];
            }

            return diff == 0;
        }

        private static byte[] Slice(byte[] data, int offset, int length)
        {
            var s = new byte[length];
            Array.Copy(data, offset, s, 0, length);
            return s;
        }

        private static byte[] Concat(byte[] a, byte[] b)
        {
            var c = new byte[a.Length + b.Length];
            a.CopyTo(c, 0);
            b.CopyTo(c, a.Length);
            return c;
        }
    }

    /// <summary>What a backup holds and the limits on it.</summary>
    public static class BackupRules
    {
        public const string Settings = "settings";
        public const string Profile = "profile";
        public const string House = "house";
        public const string Shows = "shows";

        /// <summary>The timed cracker unlocks only: never the purchase, which comes back through Google Play's Restore.</summary>
        public const string Unlocks = "unlocks";

        public const int MinPasswordLength = 8;
        public const int MaxParts = 16;
        public const int MaxPartChars = 2 * 1024 * 1024;
        public const int MaxBytes = 6 * 1024 * 1024;
        public const string Extension = ".utsav";
        public const string Folder = "backups";

        /// <summary>The parts this version writes, in the order a restore applies them.</summary>
        public static readonly string[] Parts = { Settings, Profile, House, Shows, Unlocks };

        /// <summary>Why a password can't protect an export file, or null when it can.</summary>
        public static string PasswordProblem(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
            {
                return $"Use at least {MinPasswordLength} characters.";
            }

            if (password.Trim().Length != password.Length)
            {
                return "No spaces at the start or end.";
            }

            return null;
        }

        public static string FileName(DateTime local) => "utsav_backup_" + local.ToString("yyyyMMdd_HHmm", System.Globalization.CultureInfo.InvariantCulture) + Extension;

        /// <summary>The player's words for a failed open.</summary>
        public static string Explain(BackupError error)
        {
            switch (error)
            {
                case BackupError.NotABackup:
                    return "That file isn't an Utsav Nights backup.";
                case BackupError.Newer:
                    return "That backup is from a newer version of the game. Update the game first.";
                case BackupError.WrongKeyOrDamaged:
                    return "Wrong password, or the file is damaged.";
                case BackupError.TooLarge:
                    return "That file is too large to be a backup.";
                default:
                    return string.Empty;
            }
        }
    }
}

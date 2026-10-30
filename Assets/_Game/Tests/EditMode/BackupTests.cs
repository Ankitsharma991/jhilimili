using System.Text;
using NUnit.Framework;
using UtsavNights.Core.Backup;

namespace UtsavNights.Tests
{
    /// <summary>Phase 10 step 1 (F24, PRD 13.5): the backup format, its encryption and integrity check.</summary>
    public class BackupTests
    {
        private static BackupArchive Sample()
        {
            var a = new BackupArchive { createdUnix = 1_790_000_000, appVersion = "0.10.0" };
            a.parts[BackupRules.Settings] = "{\"version\":2,\"quietMode\":true}";
            a.parts[BackupRules.House] = "{\"version\":1,\"design\":{\"decorations\":[{\"item\":\"diya_clay\"}]}} नमस्ते";
            return a;
        }

        [Test]
        public void Archive_RoundTripsItsParts()
        {
            BackupArchive back = BackupArchive.FromBytes(Sample().ToBytes());
            Assert.IsNotNull(back);
            Assert.AreEqual(1_790_000_000, back.createdUnix);
            Assert.AreEqual("0.10.0", back.appVersion);
            Assert.AreEqual(Sample().Part(BackupRules.House), back.Part(BackupRules.House), "unicode text survives");
            Assert.IsNull(back.Part(BackupRules.Shows), "a part it doesn't have");
            Assert.IsNull(BackupArchive.FromBytes(Encoding.ASCII.GetBytes("not an archive at all")));
        }

        [Test]
        public void Password_RoundTrip_AndAWrongPasswordFails()
        {
            byte[] sealedBytes = BackupCrypto.SealWithPassword(Sample(), "diwali-2026");
            Assert.IsTrue(BackupCrypto.LooksLikeBackup(sealedBytes));
            Assert.IsTrue(BackupCrypto.NeedsPassword(sealedBytes));
            Assert.AreEqual(BackupError.None, BackupCrypto.OpenWithPassword(sealedBytes, "diwali-2026", out BackupArchive back));
            Assert.AreEqual(Sample().Part(BackupRules.Settings), back.Part(BackupRules.Settings));
            Assert.AreEqual(BackupError.WrongKeyOrDamaged, BackupCrypto.OpenWithPassword(sealedBytes, "diwali-2025", out back));
            Assert.IsNull(back);
            StringAssert.DoesNotContain("quietMode", Encoding.UTF8.GetString(sealedBytes), "the contents are encrypted");
        }

        [Test]
        public void Key_RoundTrip_AndAnotherKeyFails()
        {
            byte[] key = BackupCrypto.NewKey();
            byte[] sealedBytes = BackupCrypto.SealWithKey(Sample(), key);
            Assert.IsFalse(BackupCrypto.NeedsPassword(sealedBytes));
            Assert.AreEqual(BackupError.None, BackupCrypto.OpenWithKey(sealedBytes, key, out _));
            Assert.AreEqual(BackupError.WrongKeyOrDamaged, BackupCrypto.OpenWithKey(sealedBytes, BackupCrypto.NewKey(), out _));
            Assert.AreEqual(BackupError.WrongKeyOrDamaged, BackupCrypto.OpenWithPassword(sealedBytes, "anything-at-all", out _), "a cloud backup can't be opened with a password");
            CollectionAssert.AreNotEqual(sealedBytes, BackupCrypto.SealWithKey(Sample(), key), "a fresh salt and IV every time");
        }

        [Test]
        public void AnyChangedByte_IsCaught()
        {
            byte[] key = BackupCrypto.NewKey();
            byte[] good = BackupCrypto.SealWithKey(Sample(), key);
            foreach (int at in new[] { 6, 12, 30, 40, good.Length / 2, good.Length - 40, good.Length - 1 })
            {
                var bad = (byte[])good.Clone();
                bad[at] ^= 0x10;
                Assert.AreEqual(BackupError.WrongKeyOrDamaged, BackupCrypto.OpenWithKey(bad, key, out _), "byte " + at);
            }

            var cut = new byte[good.Length - 16];
            System.Array.Copy(good, cut, cut.Length);
            Assert.AreEqual(BackupError.WrongKeyOrDamaged, BackupCrypto.OpenWithKey(cut, key, out _), "cut short");
        }

        [Test]
        public void OtherFilesAndNewerVersions_AreNamed()
        {
            Assert.AreEqual(BackupError.NotABackup, BackupCrypto.OpenWithPassword(Encoding.ASCII.GetBytes("PK\u0003\u0004 a zip file, not a backup, long enough to pass the size check ......"), "x", out _));
            Assert.AreEqual(BackupError.NotABackup, BackupCrypto.OpenWithKey(new byte[3], BackupCrypto.NewKey(), out _));

            byte[] key = BackupCrypto.NewKey();
            byte[] newerFormat = BackupCrypto.SealWithKey(Sample(), key);
            newerFormat[4] = BackupCrypto.Format + 1;
            Assert.AreEqual(BackupError.Newer, BackupCrypto.OpenWithKey(newerFormat, key, out _));

            BackupArchive newer = Sample();
            newer.version = BackupArchive.CurrentVersion + 1;
            Assert.AreEqual(BackupError.Newer, BackupCrypto.OpenWithKey(BackupCrypto.SealWithKey(newer, key), key, out BackupArchive none));
            Assert.IsNull(none);
            foreach (BackupError e in new[] { BackupError.NotABackup, BackupError.Newer, BackupError.WrongKeyOrDamaged, BackupError.TooLarge })
            {
                Assert.IsNotEmpty(BackupRules.Explain(e), e.ToString());
            }
        }

        [Test]
        public void Rules_PasswordsAndNames()
        {
            Assert.IsNotNull(BackupRules.PasswordProblem("short"));
            Assert.IsNotNull(BackupRules.PasswordProblem(" padded-password "));
            Assert.IsNotNull(BackupRules.PasswordProblem(null));
            Assert.IsNull(BackupRules.PasswordProblem("tihar-lights"));
            Assert.AreEqual("utsav_backup_20261030_1905.utsav", BackupRules.FileName(new System.DateTime(2026, 10, 30, 19, 5, 0)));
            CollectionAssert.DoesNotContain(BackupRules.Parts, "progress", "the purchase is never in a backup");
        }
    }
}

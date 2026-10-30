using System.Collections.Generic;
using NUnit.Framework;
using UtsavNights.Core.Cloud;

namespace UtsavNights.Tests
{
    /// <summary>
    /// Phase 10 step 2 (F23, F24): share codes, moderation and quotas, and the memory cloud enforcing what the Firebase
    /// rules enforce (tools/rules-test runs the same cases against the rules on the emulators).
    /// </summary>
    public class CloudModelTests
    {
        private long _now = 1_790_000_000;
        private MemoryCloudServer _server;
        private MemoryCloud _alice;
        private MemoryCloud _bob;
        private MemoryCloud _visitor;

        [SetUp]
        public void SetUp()
        {
            _server = new MemoryCloudServer(() => _now);
            _alice = new MemoryCloud(_server, "alice");
            _bob = new MemoryCloud(_server, "bob");
            _visitor = new MemoryCloud(_server, null);
            Assert.IsTrue(_alice.SignInWithGoogle().Result.Ok);
            Assert.IsTrue(_bob.SignInWithGoogle().Result.Ok);
            Assert.IsTrue(_visitor.SignInAnonymously().Result.Ok);
        }

        [Test]
        public void Codes_AreWellFormed_AndMistypesAreCaught()
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < 2000; i++)
            {
                string code = ShareCodes.New();
                Assert.AreEqual(ShareCodes.Length, code.Length);
                Assert.AreEqual(code, ShareCodes.Normalise(code), code);
                foreach (char c in "AEIOUY01LSZ")
                {
                    Assert.AreEqual(-1, code.IndexOf(c), code);
                }

                seen.Add(code);
            }

            Assert.Greater(seen.Count, 1990, "codes don't repeat in practice");
            string one = ShareCodes.New();
            Assert.AreEqual(one, ShareCodes.Normalise(" " + ShareCodes.Display(one).ToLowerInvariant() + " "), "typed in lower case with the dash");
            StringAssert.Contains("-", ShareCodes.Display(one));

            // Every single wrong character and every swapped neighbour pair is caught by the check character.
            int caught = 0, tried = 0;
            for (int i = 0; i < ShareCodes.Length; i++)
            {
                foreach (char c in ShareCodes.Alphabet)
                {
                    if (c == one[i])
                    {
                        continue;
                    }

                    char[] typo = one.ToCharArray();
                    typo[i] = c;
                    tried++;
                    caught += ShareCodes.Normalise(new string(typo)) == null ? 1 : 0;
                }

                if (i < ShareCodes.Length - 1 && one[i] != one[i + 1])
                {
                    char[] swap = one.ToCharArray();
                    (swap[i], swap[i + 1]) = (swap[i + 1], swap[i]);
                    tried++;
                    caught += ShareCodes.Normalise(new string(swap)) == null ? 1 : 0;
                }
            }

            Assert.AreEqual(tried, caught);
            Assert.IsNull(ShareCodes.Normalise("BCDF-234"), "too short");
            Assert.IsNull(ShareCodes.Normalise("BCDF-23A5"), "a vowel");
            Assert.IsNull(ShareCodes.Normalise(null));
        }

        [Test]
        public void Accounts_SeeOnlyTheirOwnBackups_AndVisitorsHaveNone()
        {
            string key = _alice.BackupKey().Result.Value;
            Assert.AreEqual(44, key.Length);
            Assert.AreEqual(key, _alice.BackupKey().Result.Value, "the key never changes");
            Assert.AreNotEqual(key, _bob.BackupKey().Result.Value);
            Assert.AreEqual(CloudError.SignedOut, _visitor.BackupKey().Result.Error, "an anonymous visitor has no backups");

            CloudBackupInfo made = _alice.PutBackup(new byte[] { 1, 2, 3 }, new CloudBackupInfo { decorations = 4, avatar = true }).Result.Value;
            Assert.AreEqual(1, _alice.ListBackups().Result.Value.Count);
            Assert.AreEqual(0, _bob.ListBackups().Result.Value.Count);
            Assert.AreEqual(CloudError.NotFound, _bob.GetBackup(made.id).Result.Error, "Bob can't read Alice's backup");
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, _alice.GetBackup(made.id).Result.Value);
            Assert.AreEqual(CloudError.TooLarge, _alice.PutBackup(new byte[CloudRules.MaxBackupBytes + 1], null).Result.Error);

            // The same Google account on a second phone is the same player.
            var newPhone = new MemoryCloud(_server, "alice");
            Assert.IsTrue(newPhone.SignInWithGoogle().Result.Ok);
            Assert.AreEqual(key, newPhone.BackupKey().Result.Value);
            Assert.AreEqual(1, newPhone.ListBackups().Result.Value.Count);
        }

        [Test]
        public void Backups_KeepTheNewestThree()
        {
            for (int i = 0; i < 5; i++)
            {
                _now += 60;
                Assert.IsTrue(_alice.PutBackup(new[] { (byte)i }, null).Result.Ok);
            }

            List<CloudBackupInfo> list = _alice.ListBackups().Result.Value;
            Assert.AreEqual(CloudRules.KeptBackups, list.Count);
            Assert.Greater(list[0].createdUnix, list[1].createdUnix, "newest first");
            CollectionAssert.AreEqual(new byte[] { 4 }, _alice.GetBackup(list[0].id).Result.Value);
        }

        [Test]
        public void Shares_OpenByCode_ForAnyone_WithoutTheOwnersId()
        {
            ShareInfo share = _alice.Publish(ShareKind.Home, "{\"design\":{}}").Result.Value;
            Assert.IsNotNull(ShareCodes.Normalise(share.code));
            CloudResult<ShareInfo> opened = _visitor.Open(ShareCodes.Display(share.code).ToLowerInvariant()).Result;
            Assert.IsTrue(opened.Ok);
            Assert.AreEqual("{\"design\":{}}", opened.Value.payload);
            Assert.AreEqual(string.Empty, opened.Value.owner, "a visitor never learns the owner's account");
            Assert.AreEqual(CloudError.Invalid, _visitor.Open("BCDF-234").Result.Error);
            Assert.AreEqual(CloudError.SignedOut, new MemoryCloud(_server).Open(share.code).Result.Error, "even visitors sign in (anonymously)");
            Assert.AreEqual(CloudError.SignedOut, _visitor.Publish(ShareKind.Home, "{}").Result.Error, "visitors can't publish");
            Assert.AreEqual(CloudError.TooLarge, _alice.Publish(ShareKind.Show, new string('x', CloudRules.MaxPayloadChars + 1)).Result.Error);

            Assert.IsTrue(_visitor.RecordVisit(share.code).Result.Value);
            Assert.IsFalse(_alice.RecordVisit(share.code).Result.Value, "the owner's own look doesn't count");
            Assert.AreEqual(1, _server.Peek(share.code).visits);
            Assert.AreEqual(1, _alice.MyShares().Result.Value.Count);
            Assert.AreEqual(0, _bob.MyShares().Result.Value.Count);
        }

        [Test]
        public void ATakenCode_IsNeverReused()
        {
            ShareInfo first = _alice.Publish(ShareKind.Home, "{}").Result.Value;
            _server.NextCode = first.code;
            ShareInfo second = _bob.Publish(ShareKind.Home, "{}").Result.Value;
            Assert.AreNotEqual(first.code, second.code);
            Assert.AreEqual("alice", _server.Peek(first.code).owner.Substring(2));
        }

        [Test]
        public void Publishing_IsLimitedPerDay()
        {
            for (int i = 0; i < CloudRules.SharesPerDay; i++)
            {
                Assert.IsTrue(_alice.Publish(ShareKind.Show, "{}").Result.Ok, "share " + i);
            }

            Assert.AreEqual(CloudError.Quota, _alice.Publish(ShareKind.Show, "{}").Result.Error);
            Assert.IsTrue(_bob.Publish(ShareKind.Show, "{}").Result.Ok, "per account");
            _now += CloudRules.DaySeconds;
            Assert.IsTrue(_alice.Publish(ShareKind.Show, "{}").Result.Ok, "a new day");
        }

        [Test]
        public void ThreeReports_FromDifferentPlayers_HideAShare_UntilReviewed()
        {
            string code = _alice.Publish(ShareKind.Home, "{}").Result.Value.code;
            var reporters = new List<MemoryCloud>();
            for (int i = 0; i < CloudRules.HideAtReports; i++)
            {
                var r = new MemoryCloud(_server, null);
                Assert.IsTrue(r.SignInAnonymously().Result.Ok);
                reporters.Add(r);
            }

            Assert.IsTrue(reporters[0].Report(code, ReportReason.Offensive).Result.Value);
            Assert.IsFalse(reporters[0].Report(code, ReportReason.Spam).Result.Value, "one report per account");
            Assert.AreEqual(CloudError.Denied, _alice.Report(code, ReportReason.Other).Result.Error, "not on your own");
            Assert.IsTrue(reporters[1].Report(code, ReportReason.Inappropriate).Result.Value);
            Assert.IsTrue(_visitor.Open(code).Result.Ok, "two reports: still open");
            Assert.IsTrue(reporters[2].Report(code, ReportReason.Spam).Result.Value);
            Assert.AreEqual(CloudError.Hidden, _visitor.Open(code).Result.Error);
            Assert.IsTrue(_alice.Open(code).Result.Ok, "the owner still sees their own");
            Assert.AreEqual(CloudError.NotFound, _visitor.RecordVisit(code).Result.Error);

            _server.Review(code, keep: true);
            Assert.IsTrue(_visitor.Open(code).Result.Ok, "kept after review");
            _server.Review(code, keep: false);
            Assert.AreEqual(CloudError.NotFound, _visitor.Open(code).Result.Error, "removed after review");
        }

        [Test]
        public void Withdrawn_Expired_AndDeletedData()
        {
            string withdrawn = _alice.Publish(ShareKind.Home, "{\"secret\":1}").Result.Value.code;
            Assert.AreEqual(CloudError.Denied, _bob.Withdraw(withdrawn).Result.Error, "only the owner");
            Assert.IsTrue(_alice.Withdraw(withdrawn).Result.Value);
            Assert.AreEqual(CloudError.Withdrawn, _visitor.Open(withdrawn).Result.Error);
            Assert.AreEqual(string.Empty, _server.Peek(withdrawn).payload, "nothing of it stays readable");
            Assert.AreEqual(0, _alice.MyShares().Result.Value.Count);

            string old = _alice.Publish(ShareKind.Home, "{}").Result.Value.code;
            _now += 200 * CloudRules.DaySeconds;
            Assert.IsTrue(_visitor.RecordVisit(old).Result.Value, "a visit keeps it alive");
            _now += 300 * CloudRules.DaySeconds;
            Assert.IsTrue(_visitor.Open(old).Result.Ok, "300 days after the last visit");
            _now += 66 * CloudRules.DaySeconds;
            Assert.AreEqual(CloudError.Expired, _visitor.Open(old).Result.Error, "a year without visits");

            _alice.PutBackup(new byte[] { 9 }, null);
            Assert.IsTrue(_alice.DeleteMyData().Result.Value);
            Assert.IsNull(_alice.Uid, "signed out afterwards");
            Assert.IsNull(_server.Peek(old));
            Assert.AreEqual(0, _server.BackupCount("g_alice"));
            Assert.IsTrue(_alice.SignInWithGoogle().Result.Ok);
            Assert.AreEqual(0, _alice.ListBackups().Result.Value.Count, "a fresh start");
        }

        [Test]
        public void Offline_AndCancelled_AreNamed()
        {
            _alice.Offline = true;
            Assert.AreEqual(CloudError.Offline, _alice.ListBackups().Result.Error);
            Assert.AreEqual(CloudError.Offline, _alice.Open("BCDF2345").Result.Error);
            var nobody = new MemoryCloud(_server, null);
            Assert.AreEqual(CloudError.Cancelled, nobody.SignInWithGoogle().Result.Error);
            foreach (CloudError e in System.Enum.GetValues(typeof(CloudError)))
            {
                Assert.AreEqual(e == CloudError.None, string.IsNullOrEmpty(CloudRules.Explain(e)), e.ToString());
            }
        }
    }
}

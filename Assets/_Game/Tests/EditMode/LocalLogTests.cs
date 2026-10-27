using System.IO;
using NUnit.Framework;
using UtsavNights.Gameplay;

namespace UtsavNights.Tests
{
    /// <summary>Phase 5 step 1: local logs stay bounded on players' phones.</summary>
    public class LocalLogTests
    {
        private string _dir;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "utsav-locallog-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _path = Path.Combine(_dir, "log.jsonl");
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_dir, true);

        [Test]
        public void Append_AddsOneLinePerCall()
        {
            LocalLog.Append(_path, "{\"a\":1}");
            LocalLog.Append(_path, "{\"a\":2}");
            CollectionAssert.AreEqual(new[] { "{\"a\":1}", "{\"a\":2}" }, File.ReadAllLines(_path));
            Assert.IsFalse(File.Exists(LocalLog.OldPath(_path)));
        }

        [Test]
        public void PastTheCap_TheFileMovesAsideAndTotalStaysBounded()
        {
            string line = new string('x', 99);
            for (int i = 0; i < 100; i++)
            {
                LocalLog.Append(_path, line, maxBytes: 1000);
            }

            Assert.LessOrEqual(new FileInfo(_path).Length, 1000);
            Assert.LessOrEqual(new FileInfo(LocalLog.OldPath(_path)).Length, 1000);
            Assert.AreEqual(2, Directory.GetFiles(_dir).Length, "only the log and one .old file");
            Assert.AreEqual(line, File.ReadAllLines(_path)[0], "lines are never split");
        }
    }
}

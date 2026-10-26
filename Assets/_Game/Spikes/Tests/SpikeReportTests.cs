using NUnit.Framework;
using UnityEngine;
using UtsavNights.Spikes;

namespace UtsavNights.Tests
{
    public class SpikeReportTests
    {
        [SetUp]
        public void SetUp() => SpikeReport.ClearInMemory();

        [TearDown]
        public void TearDown() => SpikeReport.ClearInMemory();

        [Test]
        public void Record_AddsEntryWithTier()
        {
            SpikeReport.CurrentTier = "Mid";
            SpikeEntry entry = SpikeReport.Record("gateA_particles", "avg_fps", 41.5f, "fps", save: false);
            Assert.AreEqual(1, SpikeReport.Entries.Count);
            Assert.AreEqual("Mid", entry.tier);
            Assert.AreEqual(41.5f, entry.value);
            Assert.IsFalse(string.IsNullOrEmpty(entry.utc));
        }

        [Test]
        public void Json_RoundTrips()
        {
            SpikeReport.Record("gateB_audio", "overlap_peak_voices", 16f, "voices", save: false);
            string json = SpikeReport.ToJson();
            var parsed = JsonUtility.FromJson<SpikeReportData>(json);
            Assert.AreEqual(1, parsed.entries.Count);
            Assert.AreEqual("overlap_peak_voices", parsed.entries[0].metric);
            Assert.AreEqual(16f, parsed.entries[0].value);
            Assert.IsFalse(string.IsNullOrEmpty(parsed.device));
        }
    }
}

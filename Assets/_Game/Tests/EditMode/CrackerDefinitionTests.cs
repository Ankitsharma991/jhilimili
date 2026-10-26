using System;
using NUnit.Framework;
using UnityEngine;
using UtsavNights.Content;
using UtsavNights.Core.Crackers;

namespace UtsavNights.Tests
{
    public class CrackerDefinitionTests
    {
        private CrackerDefinition _def;

        [SetUp]
        public void SetUp()
        {
            _def = ScriptableObject.CreateInstance<CrackerDefinition>();
            _def.Id = "flowerpot_gold";
            _def.Category = CrackerCategory.GroundFountain;
            _def.FuseSeconds = 2.5f;
            _def.CooldownSeconds = 2f;
            _def.MutableStages.Add(new StageDefinition { time = 0f, haptic = HapticPattern.Tick });
            _def.MutableStages.Add(new StageDefinition { time = 2.5f, payoff = true, light = true, haptic = HapticPattern.Rumble });
            _def.MutableStages.Add(new StageDefinition { time = 10f, stop = true });
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_def);

        [Test]
        public void Defaults_MatchSchemaAndPrd()
        {
            var fresh = ScriptableObject.CreateInstance<CrackerDefinition>();
            Assert.AreEqual(CrackerDefinition.CurrentSchema, fresh.Schema);
            Assert.AreEqual("core", fresh.Pack);
            Assert.AreEqual(2f, fresh.CooldownSeconds, "PRD 6.3: clear spent items after 2 s");
            Assert.AreEqual(250, fresh.GetQuality(QualityTier.Low).maxParticles);
            Assert.IsTrue(fresh.GetQuality(QualityTier.High).bloom);
            Assert.IsFalse(fresh.GetQuality(QualityTier.Low).light);
            UnityEngine.Object.DestroyImmediate(fresh);
        }

        [Test]
        public void StageKinds_FollowFlags()
        {
            Assert.AreEqual(CueKind.Normal, _def.Stages[0].Kind);
            Assert.AreEqual(CueKind.Payoff, _def.Stages[1].Kind);
            Assert.AreEqual(CueKind.Stop, _def.Stages[2].Kind);
            Assert.AreEqual(CueKind.Stop, new StageDefinition { payoff = true, stop = true }.Kind, "stop wins");
        }

        [Test]
        public void BuildTimeline_MapsStagesInTimeOrder()
        {
            // Author out of order: the timeline must still come out sorted, keeping stage indices.
            _def.MutableStages.Insert(0, new StageDefinition { time = 5f });
            CrackerTimeline t = _def.BuildTimeline();
            Assert.AreEqual(4, t.CueCount);
            Assert.AreEqual(0f, t[0].Time);
            Assert.AreEqual(1, t[0].StageIndex);
            Assert.AreEqual(2.5f, t[1].Time);
            Assert.AreEqual(5f, t[2].Time);
            Assert.AreEqual(0, t[2].StageIndex);
            Assert.AreEqual(10f, t.StopSeconds);
            Assert.AreEqual(2.5f, t.FuseSeconds);
        }

        [Test]
        public void BuiltTimeline_DrivesStateMachineToPayoff()
        {
            var sm = new CrackerStateMachine(_def.BuildTimeline());
            sm.Place();
            sm.Ignite();
            sm.Tick(3f);
            Assert.AreEqual(CrackerState.Payoff, sm.State);
        }

        [Test]
        public void InvalidTimeline_IsReportedNotBuilt()
        {
            _def.MutableStages.RemoveAt(2);
            Assert.IsNotEmpty(_def.ValidateTimeline());
            Assert.Throws<ArgumentException>(() => _def.BuildTimeline());
        }

        [Test]
        public void NullStageEntries_AreIgnored()
        {
            _def.MutableStages.Insert(1, null);
            Assert.IsEmpty(_def.ValidateTimeline());
        }

        [Test]
        public void AerialCategories()
        {
            Assert.IsFalse(_def.IsAerial);
            _def.Category = CrackerCategory.Rocket;
            Assert.IsTrue(_def.IsAerial);
            Assert.IsTrue(CrackerCategory.MultiShotCake.IsAerial());
            Assert.IsFalse(CrackerCategory.Sparkler.IsAerial());
        }

        [Test]
        public void SetQuality_PerTier()
        {
            _def.SetQuality(QualityTier.Mid, new TierQuality(600, true, false));
            Assert.AreEqual(600, _def.GetQuality(QualityTier.Mid).maxParticles);
            Assert.AreEqual(250, _def.GetQuality(QualityTier.Low).maxParticles);
        }
    }
}

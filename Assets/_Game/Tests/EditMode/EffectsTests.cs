using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UtsavNights.Content;
using UtsavNights.Core.Effects;
using UtsavNights.Effects;

namespace UtsavNights.Tests
{
    public class EffectsTests
    {
        private readonly List<Object> _created = new List<Object>();
        private Transform _root;

        [SetUp]
        public void SetUp()
        {
            _root = Track(new GameObject("fx-root")).transform;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _created)
            {
                if (o != null)
                {
                    Object.DestroyImmediate(o);
                }
            }

            _created.Clear();
        }

        private T Track<T>(T o) where T : Object
        {
            _created.Add(o);
            return o;
        }

        private ParticleSystem Prefab(string name, int maxParticles)
        {
            var go = Track(new GameObject(name));
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.maxParticles = maxParticles;
            main.playOnAwake = false;
            return ps;
        }

        [Test]
        public void Play_GrantsTierCap_AndAppliesItToTheInstance()
        {
            var budget = new ParticleBudget(3000);
            var pool = new EffectPool(_root, budget);
            EffectHandle h = pool.Play(Prefab("fountain", 900), Vector3.zero, Quaternion.identity, 500);
            Assert.IsTrue(h.IsValid);
            Assert.IsTrue(pool.IsPlaying(h));
            Assert.AreEqual(500, budget.Reserved);
            Assert.AreEqual(1, pool.ActiveCount);
            ParticleSystem instance = _root.GetComponentInChildren<ParticleSystem>(true);
            Assert.AreEqual(500, instance.main.maxParticles);
        }

        [Test]
        public void ZeroTierCap_UsesPrefabMaximum()
        {
            var budget = new ParticleBudget(3000);
            var pool = new EffectPool(_root, budget);
            pool.Play(Prefab("spark", 120), Vector3.zero, Quaternion.identity, 0);
            Assert.AreEqual(120, budget.Reserved);
        }

        [Test]
        public void StopWithClear_RecyclesInstance_AndReturnsBudget()
        {
            var budget = new ParticleBudget(3000);
            var pool = new EffectPool(_root, budget);
            ParticleSystem prefab = Prefab("fountain", 500);
            EffectHandle first = pool.Play(prefab, Vector3.zero, Quaternion.identity, 500);
            Assert.IsTrue(pool.Stop(first, clear: true));
            Assert.AreEqual(0, budget.Reserved);
            Assert.AreEqual(0, pool.ActiveCount);
            Assert.IsFalse(pool.IsPlaying(first));

            EffectHandle second = pool.Play(prefab, Vector3.one, Quaternion.identity, 500);
            Assert.AreEqual(1, pool.InstancesCreated, "the pooled instance was reused");
            Assert.IsFalse(pool.Stop(first), "stale handle is ignored");
            Assert.IsTrue(pool.IsPlaying(second));
        }

        [Test]
        public void BudgetExhausted_SkipsEffect()
        {
            var budget = new ParticleBudget(500);
            var pool = new EffectPool(_root, budget);
            ParticleSystem prefab = Prefab("fountain", 500);
            pool.Play(prefab, Vector3.zero, Quaternion.identity, 500);
            EffectHandle skipped = pool.Play(prefab, Vector3.zero, Quaternion.identity, 500);
            Assert.IsFalse(skipped.IsValid);
            Assert.AreEqual(1, pool.SkippedForBudget);
            Assert.AreEqual(1, pool.ActiveCount);
        }

        [Test]
        public void NullPrefab_ReturnsNone()
        {
            var pool = new EffectPool(_root, new ParticleBudget(100));
            Assert.IsFalse(pool.Play(null, Vector3.zero, Quaternion.identity, 10).IsValid);
        }

        [Test]
        public void Prewarm_CreatesInstancesUpFront()
        {
            var pool = new EffectPool(_root, new ParticleBudget(3000));
            ParticleSystem prefab = Prefab("fountain", 100);
            pool.Prewarm(prefab, 3);
            Assert.AreEqual(3, pool.InstancesCreated);
            pool.Play(prefab, Vector3.zero, Quaternion.identity, 100);
            Assert.AreEqual(3, pool.InstancesCreated);
        }

        [Test]
        public void ManyEffects_GrowSlots_AndStopAllClearsEverything()
        {
            var budget = new ParticleBudget(100000);
            var pool = new EffectPool(_root, budget, initialSlots: 4);
            ParticleSystem prefab = Prefab("spark", 50);
            for (int i = 0; i < 10; i++)
            {
                Assert.IsTrue(pool.Play(prefab, Vector3.zero, Quaternion.identity, 50).IsValid);
            }

            Assert.AreEqual(10, pool.ActiveCount);
            pool.StopAll(clear: true);
            Assert.AreEqual(0, pool.ActiveCount);
            Assert.AreEqual(0, budget.Reserved);
        }

        [Test]
        public void DestroyedInstance_ReleasesBudgetOnTick()
        {
            var budget = new ParticleBudget(3000);
            var pool = new EffectPool(_root, budget);
            pool.Play(Prefab("fountain", 400), Vector3.zero, Quaternion.identity, 400);
            Object.DestroyImmediate(_root.GetComponentInChildren<ParticleSystem>(true).gameObject);
            pool.Tick(0.016f);
            Assert.AreEqual(0, budget.Reserved);
            Assert.AreEqual(0, pool.ActiveCount);
        }

        [Test]
        public void Flash_IsRateLimited_AndFades()
        {
            var flashes = new FlashLightPool(_root, 3, new FlashRateLimiter(3, 1.0));
            Assert.IsTrue(flashes.TryFlash(Vector3.zero, Color.white, 4f, 0.2f));
            Assert.IsTrue(flashes.TryFlash(Vector3.zero, Color.white, 4f, 0.2f));
            Assert.IsTrue(flashes.TryFlash(Vector3.zero, Color.white, 4f, 0.2f));
            Assert.IsFalse(flashes.TryFlash(Vector3.zero, Color.white, 4f, 0.2f), "fourth flash within a second");
            Assert.AreEqual(4f, flashes.IntensityOf(0), 1e-4f);
            flashes.Tick(0.1f);
            Assert.AreEqual(1f, flashes.IntensityOf(0), 1e-3f, "quadratic fade at half time");
            flashes.Tick(0.2f);
            Assert.AreEqual(0, flashes.ActiveCount);
            flashes.Tick(1f);
            Assert.IsTrue(flashes.TryFlash(Vector3.zero, Color.white, 4f, 0.2f), "window has rolled");
        }

        [Test]
        public void ReduceFlash_HalvesIntensity_AndDisabledBlocks()
        {
            var flashes = new FlashLightPool(_root, 3, new FlashRateLimiter(3, 1.0)) { ReduceFlash = true };
            flashes.TryFlash(Vector3.zero, Color.white, 4f, 0.2f);
            Assert.AreEqual(2f, flashes.IntensityOf(0), 1e-4f);
            flashes.Enabled = false;
            Assert.IsFalse(flashes.TryFlash(Vector3.zero, Color.white, 4f, 0.2f));
        }

        [Test]
        public void Service_AppliesTierCaps_AndLightRules()
        {
            var service = new EffectsService(_root, QualityTier.Low);
            Assert.AreEqual(QualityBudgets.MaxSceneParticles(QualityTier.Low), service.Budget.Cap);
            Assert.IsFalse(service.Flashes.Enabled, "no flash lights on Low");

            var def = Track(ScriptableObject.CreateInstance<CrackerDefinition>());
            def.SetQuality(QualityTier.Low, new TierQuality(250, false, false));
            def.SetQuality(QualityTier.High, new TierQuality(900, true, true));
            var stage = new StageDefinition { emitter = Prefab("fountain", 2000), light = true };

            Assert.IsTrue(service.PlayStage(stage, def, Vector3.zero, Quaternion.identity).IsValid);
            Assert.AreEqual(250, service.Budget.Reserved, "Low tier allowance from the definition");
            Assert.IsFalse(service.FlashStage(stage, def, Vector3.zero));

            service.SetTier(QualityTier.High);
            Assert.AreEqual(QualityBudgets.MaxSceneParticles(QualityTier.High), service.Budget.Cap);
            Assert.IsTrue(service.FlashStage(stage, def, Vector3.zero));
            service.PlayStage(stage, def, Vector3.zero, Quaternion.identity);
            Assert.AreEqual(250 + 900, service.Budget.Reserved);
        }

        [Test]
        public void Service_IgnoresStagesWithoutContent()
        {
            var service = new EffectsService(_root, QualityTier.Mid);
            Assert.IsFalse(service.PlayStage(new StageDefinition(), null, Vector3.zero, Quaternion.identity).IsValid);
            Assert.IsFalse(service.FlashStage(new StageDefinition { light = false }, null, Vector3.zero));
            Assert.IsFalse(service.PlayStage(null, null, Vector3.zero, Quaternion.identity).IsValid);
        }
    }
}

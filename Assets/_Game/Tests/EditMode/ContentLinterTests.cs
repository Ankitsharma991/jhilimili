using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UtsavNights.Content;
using UtsavNights.EditorTools.ContentLint;

namespace UtsavNights.Tests
{
    public class ContentLinterTests
    {
        private readonly List<Object> _created = new List<Object>();

        private T Track<T>(T obj) where T : Object
        {
            _created.Add(obj);
            return obj;
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

        /// <summary>A definition that passes every rule with no errors and no warnings.</summary>
        private CrackerDefinition ValidDefinition(string id = "flowerpot_gold")
        {
            var def = Track(ScriptableObject.CreateInstance<CrackerDefinition>());
            def.name = id;
            def.Id = id;
            def.DisplayName = new LocalizedName("Flowerpot", "Anar", "Anar");
            def.ModelPrefab = Track(new GameObject("model"));
            def.CreditsModel = "own";
            def.CreditsAudio = "own-001";

            var emitterGo = Track(new GameObject("fx"));
            var emitter = emitterGo.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = emitter.main;
            main.maxParticles = 500;

            AudioClip clip = Track(AudioClip.Create("roar", 4800, 1, 48000, false));
            def.MutableStages.Add(new StageDefinition { time = 0f, sound = clip, haptic = HapticPattern.Tick });
            def.MutableStages.Add(new StageDefinition { time = 2.5f, emitter = emitter, payoff = true, light = true });
            def.MutableStages.Add(new StageDefinition { time = 10f, stop = true });
            return def;
        }

        private static bool Has(List<LintIssue> issues, string rule, LintSeverity severity)
        {
            return issues.Exists(i => i.Rule == rule && i.Severity == severity);
        }

        [Test]
        public void ValidDefinition_IsClean()
        {
            List<LintIssue> issues = CrackerDefinitionLinter.Lint(ValidDefinition());
            CollectionAssert.IsEmpty(issues, string.Join("\n", issues));
        }

        [TestCase("")]
        [TestCase("Flowerpot")]
        [TestCase("1pot")]
        [TestCase("flower-pot")]
        public void BadIds_AreErrors(string id)
        {
            CrackerDefinition def = ValidDefinition();
            def.Id = id;
            List<LintIssue> issues = CrackerDefinitionLinter.Lint(def);
            Assert.IsTrue(Has(issues, "id.missing", LintSeverity.Error) || Has(issues, "id.format", LintSeverity.Error));
        }

        [Test]
        public void MissingAudio_IsError()
        {
            CrackerDefinition def = ValidDefinition();
            def.MutableStages[0].sound = null;
            Assert.IsTrue(Has(CrackerDefinitionLinter.Lint(def), "audio.missing", LintSeverity.Error));
        }

        [Test]
        public void AudioWithoutLicence_IsError()
        {
            CrackerDefinition def = ValidDefinition();
            def.CreditsAudio = " ";
            Assert.IsTrue(Has(CrackerDefinitionLinter.Lint(def), "licence.audio", LintSeverity.Error));
        }

        [Test]
        public void ModelWithoutLicence_IsError()
        {
            CrackerDefinition def = ValidDefinition();
            def.CreditsModel = string.Empty;
            Assert.IsTrue(Has(CrackerDefinitionLinter.Lint(def), "licence.model", LintSeverity.Error));
        }

        [Test]
        public void MissingEmitter_IsError()
        {
            CrackerDefinition def = ValidDefinition();
            def.MutableStages[1].emitter = null;
            Assert.IsTrue(Has(CrackerDefinitionLinter.Lint(def), "effect.missing", LintSeverity.Error));
        }

        [Test]
        public void TimelineProblems_AreErrors()
        {
            CrackerDefinition def = ValidDefinition();
            def.MutableStages[2].stop = false;
            Assert.IsTrue(Has(CrackerDefinitionLinter.Lint(def), "timeline.invalid", LintSeverity.Error));
        }

        [Test]
        public void ParticlesOverSceneBudget_IsError()
        {
            CrackerDefinition def = ValidDefinition();
            def.SetQuality(QualityTier.Low, new TierQuality(QualityBudgets.MaxSceneParticles(QualityTier.Low) + 1, false, false));
            Assert.IsTrue(Has(CrackerDefinitionLinter.Lint(def), "budget.particles", LintSeverity.Error));
        }

        [Test]
        public void ZeroParticlesWithEmitter_IsError()
        {
            CrackerDefinition def = ValidDefinition();
            def.SetQuality(QualityTier.Low, new TierQuality(0, false, false));
            Assert.IsTrue(Has(CrackerDefinitionLinter.Lint(def), "budget.particles_zero", LintSeverity.Error));
        }

        [Test]
        public void TooManyVoices_IsError()
        {
            CrackerDefinition def = ValidDefinition();
            def.Voices = QualityBudgets.MaxVoices(QualityTier.Low) + 1;
            Assert.IsTrue(Has(CrackerDefinitionLinter.Lint(def), "budget.voices", LintSeverity.Error));
        }

        [Test]
        public void NoSurfaceOrIgnition_AreErrors()
        {
            CrackerDefinition def = ValidDefinition();
            def.Surfaces = PlacementSurface.None;
            def.IgnitionMethods = IgnitionMethod.None;
            List<LintIssue> issues = CrackerDefinitionLinter.Lint(def);
            Assert.IsTrue(Has(issues, "placement.surfaces", LintSeverity.Error));
            Assert.IsTrue(Has(issues, "ignition.methods", LintSeverity.Error));
        }

        [Test]
        public void WrongSchema_IsError()
        {
            CrackerDefinition def = ValidDefinition();
            def.Schema = 0;
            Assert.IsTrue(Has(CrackerDefinitionLinter.Lint(def), "schema.version", LintSeverity.Error));
        }

        [Test]
        public void Warnings_DoNotCountAsErrors()
        {
            CrackerDefinition def = ValidDefinition();
            def.MutableStages[1].payoff = false;
            def.ModelPrefab = null;
            def.SetQuality(QualityTier.Low, new TierQuality(250, true, true));
            List<LintIssue> issues = CrackerDefinitionLinter.Lint(def);
            Assert.IsTrue(Has(issues, "stage.payoff", LintSeverity.Warning));
            Assert.IsTrue(Has(issues, "model.missing", LintSeverity.Warning));
            Assert.IsTrue(Has(issues, "budget.light_low", LintSeverity.Warning));
            Assert.IsTrue(Has(issues, "budget.bloom_low", LintSeverity.Warning));
            Assert.AreEqual(0, CrackerDefinitionLinter.CountErrors(issues), string.Join("\n", issues));
        }

        [Test]
        public void EmitterAboveHighCap_IsWarning()
        {
            CrackerDefinition def = ValidDefinition();
            ParticleSystem.MainModule main = def.MutableStages[1].emitter.main;
            main.maxParticles = 5000;
            Assert.IsTrue(Has(CrackerDefinitionLinter.Lint(def), "budget.emitter", LintSeverity.Warning));
        }

        [Test]
        public void DuplicateIds_AcrossAssets_AreErrors()
        {
            List<LintIssue> issues = CrackerDefinitionLinter.LintAll(new[] { ValidDefinition("rocket_basic"), ValidDefinition("rocket_basic") });
            Assert.IsTrue(Has(issues, "id.duplicate", LintSeverity.Error));
        }

        [Test]
        public void NullDefinition_IsError()
        {
            Assert.IsTrue(Has(CrackerDefinitionLinter.Lint(null), "asset.null", LintSeverity.Error));
        }
    }
}

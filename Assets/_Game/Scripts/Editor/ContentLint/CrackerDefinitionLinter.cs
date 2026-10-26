using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UtsavNights.Content;

namespace UtsavNights.EditorTools.ContentLint
{
    /// <summary>
    /// Rules for <see cref="CrackerDefinition"/> assets (PRD 13.3 validation, PRD 18 content lint).
    /// Errors: the item would break or violate licence/budget rules. Warnings: likely mistakes.
    /// </summary>
    public static class CrackerDefinitionLinter
    {
        private static readonly Regex IdPattern = new Regex("^[a-z][a-z0-9_]*$");
        private static readonly QualityTier[] Tiers = { QualityTier.Low, QualityTier.Mid, QualityTier.High };

        public static List<LintIssue> Lint(CrackerDefinition def)
        {
            var issues = new List<LintIssue>();
            if (def == null)
            {
                issues.Add(new LintIssue(LintSeverity.Error, "asset.null", "(null)", "definition is missing"));
                return issues;
            }

            string name = string.IsNullOrEmpty(def.Id) ? def.name : def.Id;
            void Error(string rule, string message) => issues.Add(new LintIssue(LintSeverity.Error, rule, name, message));
            void Warn(string rule, string message) => issues.Add(new LintIssue(LintSeverity.Warning, rule, name, message));

            // Identity
            if (string.IsNullOrEmpty(def.Id))
            {
                Error("id.missing", "id is empty");
            }
            else if (!IdPattern.IsMatch(def.Id))
            {
                Error("id.format", $"id '{def.Id}' must be lowercase letters, digits and underscores, starting with a letter");
            }

            if (def.Schema != CrackerDefinition.CurrentSchema)
            {
                Error("schema.version", $"schema {def.Schema} is not the current schema {CrackerDefinition.CurrentSchema}; migrate it");
            }

            if (string.IsNullOrWhiteSpace(def.DisplayName.en))
            {
                Error("name.english", "English display name is empty");
            }

            if (def.Festivals == null || def.Festivals.Count == 0)
            {
                Warn("tags.festival", "no festival tags; the item will not appear in festival filters");
            }

            // Placement and ignition
            if (def.Footprint.x <= 0f || def.Footprint.y <= 0f)
            {
                Error("placement.footprint", "footprint must be positive on both axes");
            }

            Vector3 volume = def.EffectVolume;
            if (volume.x <= 0f || volume.y <= 0f || volume.z <= 0f)
            {
                Error("placement.volume", "effect volume must be positive on all axes");
            }

            if (def.Surfaces == PlacementSurface.None)
            {
                Error("placement.surfaces", "no allowed surface; the item can never be placed");
            }

            if (def.IgnitionMethods == IgnitionMethod.None)
            {
                Error("ignition.methods", "no ignition method; the item can never be lit");
            }

            // Timeline
            foreach (string problem in def.ValidateTimeline())
            {
                Error("timeline.invalid", problem);
            }

            bool hasSound = false;
            bool hasEmitter = false;
            bool hasPayoff = false;
            bool hasLight = false;
            IReadOnlyList<StageDefinition> stages = def.Stages;
            for (int i = 0; i < stages.Count; i++)
            {
                StageDefinition stage = stages[i];
                if (stage == null)
                {
                    Warn("stage.null", $"stage {i} is an empty entry");
                    continue;
                }

                hasSound |= stage.sound != null;
                hasEmitter |= stage.emitter != null;
                hasPayoff |= stage.payoff;
                hasLight |= stage.light;

                if (stage.stop && (stage.emitter != null || stage.sound != null))
                {
                    Warn("stage.stop_content", $"stage {i} is the stop stage but also starts an effect or sound; it will be cut immediately");
                }

                if (stage.emitter != null)
                {
                    int prefabMax = stage.emitter.main.maxParticles;
                    int highCap = def.GetQuality(QualityTier.High).maxParticles;
                    if (highCap > 0 && prefabMax > highCap)
                    {
                        Warn("budget.emitter", $"stage {i} emitter allows {prefabMax} particles, above the High cap of {highCap}; the governor will clamp it");
                    }
                }
            }

            if (!hasSound)
            {
                Error("audio.missing", "no stage plays a sound; every cracker needs its own audio identity (PRD 11)");
            }

            if (!hasEmitter)
            {
                Error("effect.missing", "no stage starts a particle effect");
            }

            if (!hasPayoff)
            {
                Warn("stage.payoff", "no stage is marked as payoff; analytics and camera cues rely on it");
            }

            // Budgets
            int previous = -1;
            foreach (QualityTier tier in Tiers)
            {
                TierQuality quality = def.GetQuality(tier);
                int sceneCap = QualityBudgets.MaxSceneParticles(tier);
                if (quality.maxParticles > sceneCap)
                {
                    Error("budget.particles", $"{tier} allows {quality.maxParticles} particles, above the {tier} scene budget of {sceneCap}");
                }

                if (hasEmitter && quality.maxParticles <= 0)
                {
                    Error("budget.particles_zero", $"{tier} particle budget is 0, so the effect would be invisible");
                }

                if (quality.maxParticles < previous)
                {
                    Warn("budget.order", $"{tier} particle budget is lower than the tier below it");
                }

                previous = quality.maxParticles;
            }

            if (def.GetQuality(QualityTier.Low).light && hasLight)
            {
                Warn("budget.light_low", "Low tier enables flash lights; PRD 12 keeps dynamic lights to mid and high");
            }

            if (def.GetQuality(QualityTier.Low).bloom)
            {
                Warn("budget.bloom_low", "Low tier requests bloom; bloom is mid and high only");
            }

            if (def.Voices > QualityBudgets.MaxVoices(QualityTier.Low))
            {
                Error("budget.voices", $"{def.Voices} voices exceeds the Low tier total of {QualityBudgets.MaxVoices(QualityTier.Low)}");
            }

            // Licences (PRD 16: only original or clearly licensed assets)
            if (hasSound && string.IsNullOrWhiteSpace(def.CreditsAudio))
            {
                Error("licence.audio", "audio has no licence id; add a row to Docs/LICENCE_LOG.md and put its id in Credits Audio");
            }

            if (def.ModelPrefab != null && string.IsNullOrWhiteSpace(def.CreditsModel))
            {
                Error("licence.model", "model has no licence id (use 'own' for original work)");
            }

            if (def.ModelPrefab == null)
            {
                Warn("model.missing", "no model prefab; a placeholder will be used");
            }

            return issues;
        }

        /// <summary>Lints each definition plus cross-asset rules (unique ids).</summary>
        public static List<LintIssue> LintAll(IReadOnlyList<CrackerDefinition> definitions)
        {
            var issues = new List<LintIssue>();
            var seen = new Dictionary<string, string>();
            foreach (CrackerDefinition def in definitions)
            {
                issues.AddRange(Lint(def));
                if (def == null || string.IsNullOrEmpty(def.Id))
                {
                    continue;
                }

                if (seen.TryGetValue(def.Id, out string other))
                {
                    issues.Add(new LintIssue(LintSeverity.Error, "id.duplicate", def.Id, $"id is also used by asset '{other}'"));
                }
                else
                {
                    seen.Add(def.Id, def.name);
                }
            }

            return issues;
        }

        public static int CountErrors(IReadOnlyList<LintIssue> issues)
        {
            int n = 0;
            foreach (LintIssue issue in issues)
            {
                if (issue.Severity == LintSeverity.Error)
                {
                    n++;
                }
            }

            return n;
        }
    }
}

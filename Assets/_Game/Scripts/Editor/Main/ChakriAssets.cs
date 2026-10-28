using System;
using UnityEditor;
using UnityEngine;
using UtsavNights.Core.Art;
using UtsavNights.Content;
using UtsavNights.Core;
using UtsavNights.Core.Crackers;
using F = UtsavNights.EditorTools.Main.FlowerpotAssets;
using Random = System.Random;

namespace UtsavNights.EditorTools.Main
{
    /// <summary>
    /// Ground spinner, chakri (PRD 7: whirr, pops, sparks on a circular path). Built from code: placeholder
    /// audio (own-003), spinning spark prefab, a coil model and the definition.
    /// </summary>
    public static class ChakriAssets
    {
        public const string DefinitionPath = "Assets/_Game/Definitions/Crackers/Cracker_ChakriClassic.asset";
        private const string Folder = "Assets/_Game/Prefabs/Crackers/Chakri";
        private const float CoilRadius = 0.075f;

        public static CrackerDefinition Build()
        {
            AudioClip fuse = AssetDatabase.LoadAssetAtPath<AudioClip>($"{F.AudioFolder}/fuse_hiss_loop.wav");
            AudioClip whirr = F.WriteClip("chakri_whirr_loop", Whirr(F.SampleRate, 5001), true);
            AudioClip pop = F.WriteClip("chakri_pop", ProceduralSounds.Generate(ProceduralSounds.Kind.Pop, F.SampleRate, 5002), false);

            Texture2D glow = AssetDatabase.LoadAssetAtPath<Texture2D>(F.GlowTexturePath);
            Material additive = F.ParticleMaterial("Chakri_Spark_Additive", glow, additive: true);
            ParticleSystem fuseSpark = AssetDatabase.LoadAssetAtPath<ParticleSystem>("Assets/_Game/Prefabs/Crackers/Flowerpot/FX_Flowerpot_FuseSpark.prefab");
            ParticleSystem spin = F.SavePrefab(BuildSpinSparks(additive), "FX_Chakri_Spin", Folder);
            ParticleSystem halo = F.SavePrefab(BuildHalo(additive), "FX_Chakri_Halo", Folder);
            GameObject model = F.SaveGameObject(BuildModel(), "Model_Chakri", Folder);

            CrackerDefinition def = AssetDatabase.LoadAssetAtPath<CrackerDefinition>(DefinitionPath);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<CrackerDefinition>();
                AssetDatabase.CreateAsset(def, DefinitionPath);
            }

            def.Id = "chakri_classic";
            def.Schema = CrackerDefinition.CurrentSchema;
            def.DisplayName = new LocalizedName("Ground spinner", "Chakri", "Chakri");
            def.Category = CrackerCategory.GroundSpinner;
            def.SetFestivals("diwali", "tihar");
            def.Intensity = Intensity.Medium;
            def.Footprint = new Vector2(0.25f, 0.25f);
            def.EffectVolume = new Vector3(1.4f, 0.8f, 1.4f);
            def.Surfaces = PlacementSurface.Ground;
            def.IgnitionMethods = IgnitionMethod.Lighter | IgnitionMethod.Match;
            def.FuseSeconds = 2f;
            def.FuseAnchor = new Vector3(CoilRadius + 0.02f, 0.03f, 0f);
            def.CooldownSeconds = 1.5f;
            def.ModelPrefab = model;
            def.Voices = 3;
            def.Cost = 1;
            def.Pack = "core";
            def.CreditsModel = "own";
            def.CreditsAudio = "own-003";
            def.SetQuality(QualityTier.Low, new TierQuality(220, false, false));
            def.SetQuality(QualityTier.Mid, new TierQuality(420, true, false));
            def.SetQuality(QualityTier.High, new TierQuality(650, true, true));

            var stages = def.MutableStages;
            stages.Clear();
            stages.Add(new StageDefinition
            {
                time = 0f, emitter = fuseSpark, anchor = StageAnchor.Fuse, endWithNextEmitter = true,
                sound = fuse, loopSound = true, volume = 0.45f, haptic = HapticPattern.Tick,
            });
            stages.Add(new StageDefinition
            {
                time = 2f, emitter = spin, payoff = true, motion = StageMotion.Spin, spinDegreesPerSecond = 1260f, spinModel = true,
                sound = whirr, loopSound = true, volume = 0.7f, haptic = HapticPattern.Rumble, camera = CameraCue.PushInSmall,
            });
            stages.Add(new StageDefinition { time = 2f, emitter = halo });
            stages.Add(new StageDefinition
            {
                time = 3.2f, sound = pop, volume = 0.6f, repeatCount = 5, repeatInterval = 0.85f, haptic = HapticPattern.Tick,
            });
            stages.Add(new StageDefinition { time = 8f, stop = true });

            EditorUtility.SetDirty(def);
            return def;
        }

        /// <summary>One-second loopable whirr: a buzzy tone fluttering at the spin rate over hiss.</summary>
        private static float[] Whirr(int rate, int seed)
        {
            int n = rate;
            var data = new float[n];
            var rng = new Random(seed);
            double phase = 0.0;
            float hiss = 0f;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / rate;
                phase += 2.0 * Math.PI * 180.0 / rate;
                float saw = (float)(phase / Math.PI % 2.0) - 1f;
                float flutter = 0.55f + 0.45f * (float)Math.Sin(2.0 * Math.PI * 21.0 * t);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                hiss += 0.3f * (noise - hiss);
                data[i] = (saw * 0.35f + (noise - hiss) * 0.5f) * flutter;
            }

            int fade = rate / 50;
            for (int i = 0; i < fade; i++)
            {
                float g = (float)i / fade;
                data[i] *= g;
                data[n - 1 - i] *= g;
            }

            float peak = 0f;
            foreach (float s in data)
            {
                peak = Math.Max(peak, Math.Abs(s));
            }

            for (int i = 0; i < n; i++)
            {
                data[i] = data[i] / peak * 0.8f;
            }

            return data;
        }

        private static ParticleSystem BuildSpinSparks(Material m)
        {
            ParticleSystem ps = F.NewSystem("FX_Chakri_Spin", m, 650, true);
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.4f, 3.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.05f);
            main.gravityModifier = 0.45f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.92f, 0.55f), new Color(0.55f, 1f, 0.45f));
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 820f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 6f;
            shape.radius = 0.004f;
            shape.position = new Vector3(CoilRadius, 0.03f, 0f);
            shape.rotation = new Vector3(0f, 0f, 0f);
            F.Fade(ps, Color.white, new Color(0.9f, 0.55f, 0.1f), 0.3f);
            return ps;
        }

        private static ParticleSystem BuildHalo(Material m)
        {
            ParticleSystem ps = F.NewSystem("FX_Chakri_Halo", m, 60, false);
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.4f, 0.5f), new Color(0.6f, 1f, 0.5f, 0.4f));
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 120f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.02f;
            shape.position = new Vector3(0f, 0.04f, 0f);
            return ps;
        }

        private static GameObject BuildModel()
        {
            var root = new GameObject("Model_Chakri");
            Material paperA = CrackerWrappers.Material("Chakri_Paper_Red", WrapperPattern.Dots, new Color(0.85f, 0.18f, 0.2f), new Color(0.95f, 0.74f, 0.2f), new Color(0.95f, 0.74f, 0.2f));
            Material paperB = CrackerWrappers.Material("Chakri_Paper_Green", WrapperPattern.Dots, new Color(0.2f, 0.62f, 0.3f), new Color(0.95f, 0.74f, 0.2f), new Color(0.95f, 0.74f, 0.2f));
            Material core = SceneKit.LitMaterial("Chakri_Core", new Color(0.9f, 0.82f, 0.4f), 0.4f);
            SceneKit.Primitive(PrimitiveType.Cylinder, "CoilOuter", root.transform, new Vector3(0f, 0.03f, 0f), new Vector3(CoilRadius * 2f, 0.012f, CoilRadius * 2f), paperA, keepCollider: false);
            SceneKit.Primitive(PrimitiveType.Cylinder, "CoilInner", root.transform, new Vector3(0f, 0.031f, 0f), new Vector3(CoilRadius * 1.4f, 0.013f, CoilRadius * 1.4f), paperB, keepCollider: false);
            SceneKit.Primitive(PrimitiveType.Cylinder, "Core", root.transform, new Vector3(0f, 0.032f, 0f), new Vector3(CoilRadius * 0.6f, 0.014f, CoilRadius * 0.6f), core, keepCollider: false);
            SceneKit.Primitive(PrimitiveType.Cylinder, "Pin", root.transform, new Vector3(0f, 0.015f, 0f), new Vector3(0.01f, 0.015f, 0.01f), core, keepCollider: false);
            SceneKit.Primitive(PrimitiveType.Cylinder, "Fuse", root.transform, new Vector3(CoilRadius + 0.012f, 0.03f, 0f), new Vector3(0.006f, 0.012f, 0.006f), paperA, keepCollider: false).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            return root;
        }
    }
}

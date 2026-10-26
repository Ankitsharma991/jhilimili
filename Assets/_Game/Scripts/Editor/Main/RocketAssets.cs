using UnityEditor;
using UnityEngine;
using UtsavNights.Content;
using UtsavNights.Core;
using F = UtsavNights.EditorTools.Main.FlowerpotAssets;

namespace UtsavNights.EditorTools.Main
{
    /// <summary>
    /// Rocket in a bottle (PRD 7: aerial, light, camera tracks up, whistle and burst). Built from code:
    /// placeholder audio (own-003), trail and burst prefabs, a bottle model and the definition.
    /// </summary>
    public static class RocketAssets
    {
        public const string DefinitionPath = "Assets/_Game/Definitions/Crackers/Cracker_RocketBasic.asset";
        private const string Folder = "Assets/_Game/Prefabs/Crackers/Rocket";
        public const float BurstHeight = 7f;
        public const float RiseSeconds = 1.1f;

        public static CrackerDefinition Build()
        {
            AudioClip fuse = AssetDatabase.LoadAssetAtPath<AudioClip>($"{F.AudioFolder}/fuse_hiss_loop.wav");
            AudioClip whistle = F.WriteClip("rocket_whistle", ProceduralSounds.Generate(ProceduralSounds.Kind.Whistle, F.SampleRate, 4001), false);
            AudioClip bang = F.WriteClip("rocket_burst", ProceduralSounds.Generate(ProceduralSounds.Kind.Bang, F.SampleRate, 4002), false);
            AudioClip crackle = F.WriteClip("rocket_crackle", ProceduralSounds.Generate(ProceduralSounds.Kind.Crackle, F.SampleRate, 4003), false);

            Texture2D glow = AssetDatabase.LoadAssetAtPath<Texture2D>(F.GlowTexturePath);
            Material additive = F.ParticleMaterial("Rocket_Spark_Additive", glow, additive: true);
            ParticleSystem fuseSpark = AssetDatabase.LoadAssetAtPath<ParticleSystem>("Assets/_Game/Prefabs/Crackers/Flowerpot/FX_Flowerpot_FuseSpark.prefab");
            ParticleSystem trail = F.SavePrefab(BuildTrail(additive), "FX_Rocket_Trail", Folder);
            ParticleSystem burst = F.SavePrefab(BuildBurst(additive), "FX_Rocket_Burst", Folder);
            ParticleSystem glitter = F.SavePrefab(BuildGlitter(additive), "FX_Rocket_Glitter", Folder);
            GameObject model = F.SaveGameObject(BuildModel(), "Model_Rocket", Folder);

            CrackerDefinition def = AssetDatabase.LoadAssetAtPath<CrackerDefinition>(DefinitionPath);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<CrackerDefinition>();
                AssetDatabase.CreateAsset(def, DefinitionPath);
            }

            def.Id = "rocket_basic";
            def.Schema = CrackerDefinition.CurrentSchema;
            def.DisplayName = new LocalizedName("Rocket", "Rocket", "Rocket");
            def.Category = CrackerCategory.Rocket;
            def.SetFestivals("diwali", "tihar");
            def.Intensity = Intensity.Loud;
            def.Footprint = new Vector2(0.25f, 0.25f);
            def.EffectVolume = new Vector3(1.5f, BurstHeight + 1f, 1.5f);
            def.Surfaces = PlacementSurface.Ground | PlacementSurface.Rooftop | PlacementSurface.LaunchPad;
            def.IgnitionMethods = IgnitionMethod.Lighter | IgnitionMethod.Match;
            def.FuseSeconds = 2f;
            def.FuseAnchor = new Vector3(0.03f, 0.27f, 0f);
            def.CooldownSeconds = 1.5f;
            def.ModelPrefab = model;
            def.Voices = 3;
            def.Cost = 2;
            def.Pack = "core";
            def.CreditsModel = "own";
            def.CreditsAudio = "own-003";
            def.SetQuality(QualityTier.Low, new TierQuality(260, false, false));
            def.SetQuality(QualityTier.Mid, new TierQuality(450, true, false));
            def.SetQuality(QualityTier.High, new TierQuality(700, true, true));

            var stages = def.MutableStages;
            stages.Clear();
            stages.Add(new StageDefinition
            {
                time = 0f, emitter = fuseSpark, anchor = StageAnchor.Fuse, endWithNextEmitter = true,
                sound = fuse, loopSound = true, volume = 0.45f, haptic = HapticPattern.Tick,
            });
            stages.Add(new StageDefinition
            {
                time = 2f, emitter = trail, motion = StageMotion.Launch, launchHeight = BurstHeight, launchRiseSeconds = RiseSeconds,
                sound = whistle, volume = 0.7f, endLoopSound = true, hideModel = true, camera = CameraCue.AerialFollow,
                haptic = HapticPattern.Tick,
            });
            stages.Add(new StageDefinition
            {
                time = 2f + RiseSeconds, emitter = burst, anchor = StageAnchor.Apex, launchHeight = BurstHeight, payoff = true,
                sound = bang, volume = 0.9f, duckOthers = 0.45f, light = true, lightColor = new Color(1f, 0.55f, 0.85f),
                lightPeak = 4f, haptic = HapticPattern.Thud,
            });
            stages.Add(new StageDefinition
            {
                time = 2.35f + RiseSeconds, emitter = glitter, anchor = StageAnchor.Apex, launchHeight = BurstHeight - 0.6f,
                sound = crackle, volume = 0.5f,
            });
            stages.Add(new StageDefinition { time = 6f, stop = true });

            EditorUtility.SetDirty(def);
            return def;
        }

        private static ParticleSystem BuildTrail(Material m)
        {
            ParticleSystem ps = F.NewSystem("FX_Rocket_Trail", m, 220, true);
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
            main.gravityModifier = 0.5f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.6f), new Color(1f, 0.6f, 0.2f));
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 260f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 14f;
            shape.radius = 0.01f;
            shape.position = new Vector3(0f, 0.25f, 0f);
            shape.rotation = new Vector3(90f, 0f, 0f);
            F.Fade(ps, Color.white, new Color(1f, 0.4f, 0.1f), 0.2f);
            return ps;
        }

        private static ParticleSystem BuildBurst(Material m)
        {
            ParticleSystem ps = F.NewSystem("FX_Rocket_Burst", m, 700, true);
            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.duration = 0.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.3f, 1.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5.5f, 7.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.11f);
            main.gravityModifier = 0.3f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.32f, 0.78f), new Color(1f, 0.75f, 0.25f));
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)650, (short)700) });
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 100f;
            limit.drag = 1.4f;
            F.Fade(ps, Color.white, new Color(0.55f, 0.1f, 0.7f), 0.35f);
            return ps;
        }

        private static ParticleSystem BuildGlitter(Material m)
        {
            ParticleSystem ps = F.NewSystem("FX_Rocket_Glitter", m, 180, false);
            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.duration = 0.8f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            main.gravityModifier = 0.15f;
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(1f, 0.92f, 0.65f));
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 220f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 2.4f;
            F.Fade(ps, Color.white, new Color(1f, 0.85f, 0.5f), 0.5f);
            return ps;
        }

        private static GameObject BuildModel()
        {
            var root = new GameObject("Model_Rocket");
            Material glass = SceneKit.LitMaterial("Rocket_Bottle", new Color(0.12f, 0.32f, 0.18f), 0.85f);
            Material tube = SceneKit.LitMaterial("Rocket_Tube", new Color(0.82f, 0.16f, 0.22f), 0.35f);
            Material cone = SceneKit.LitMaterial("Rocket_Cone", new Color(0.95f, 0.78f, 0.2f), 0.4f);
            Material stick = SceneKit.LitMaterial("Rocket_Stick", new Color(0.6f, 0.45f, 0.28f), 0.1f);
            SceneKit.Primitive(PrimitiveType.Cylinder, "Bottle", root.transform, new Vector3(0f, 0.09f, 0f), new Vector3(0.09f, 0.09f, 0.09f), glass, keepCollider: false);
            SceneKit.Primitive(PrimitiveType.Cylinder, "Neck", root.transform, new Vector3(0f, 0.2f, 0f), new Vector3(0.035f, 0.03f, 0.035f), glass, keepCollider: false);
            SceneKit.Primitive(PrimitiveType.Cylinder, "Stick", root.transform, new Vector3(0f, 0.2f, 0f), new Vector3(0.008f, 0.12f, 0.008f), stick, keepCollider: false);
            SceneKit.Primitive(PrimitiveType.Cylinder, "Tube", root.transform, new Vector3(0f, 0.33f, 0f), new Vector3(0.04f, 0.06f, 0.04f), tube, keepCollider: false);
            SceneKit.Primitive(PrimitiveType.Sphere, "Cone", root.transform, new Vector3(0f, 0.4f, 0f), new Vector3(0.04f, 0.05f, 0.04f), cone, keepCollider: false);
            return root;
        }
    }
}

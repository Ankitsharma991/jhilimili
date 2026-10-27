using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UtsavNights.Content;
using UtsavNights.Core;
using Random = System.Random;

namespace UtsavNights.EditorTools.Main
{
    /// <summary>
    /// Builds the Phase 2 flowerpot (Anar) from code: placeholder audio, particle prefabs, model,
    /// lighter prefab and the CrackerDefinition asset. Re-running updates everything in place.
    /// Audio is synthesised (licence own-003) until licensed recordings replace it.
    /// </summary>
    public static class FlowerpotAssets
    {
        public const string DefinitionPath = "Assets/_Game/Definitions/Crackers/Cracker_FlowerpotGold.asset";
        private const string Root = "Assets/_Game/Prefabs/Crackers/Flowerpot";
        internal const string AudioFolder = "Assets/_Game/Audio/Placeholder";
        private const string MaterialFolder = "Assets/_Game/Materials/Crackers";
        internal const string GlowTexturePath = "Assets/_Game/Art/Spike/glow_soft.png";
        internal const int SampleRate = 48000;

        public sealed class Result
        {
            public CrackerDefinition Definition;
            public GameObject LighterPrefab;
            public AudioClip LighterClick;
            public AudioClip FlameOut;
        }

        public static Result BuildAll()
        {
            EditorAssetUtil.EnsureFolder(Root);
            EditorAssetUtil.EnsureFolder(AudioFolder);
            EditorAssetUtil.EnsureFolder(MaterialFolder);
            EditorAssetUtil.EnsureFolder("Assets/_Game/Definitions/Crackers");

            AudioClip fuseHiss = WriteClip("fuse_hiss_loop", ProceduralSounds.Generate(ProceduralSounds.Kind.FuseHiss, SampleRate, 3001), true);
            AudioClip roar = WriteClip("fountain_roar_loop", FountainRoar(SampleRate, 3002), true);
            AudioClip whoosh = WriteClip("fountain_ignite", ProceduralSounds.Generate(ProceduralSounds.Kind.Whoosh, SampleRate, 3003), false);
            AudioClip click = WriteClip("lighter_click", ProceduralSounds.Generate(ProceduralSounds.Kind.Click, SampleRate, 3004), false);
            AudioClip flameOut = WriteClip("lighter_flame_out", ProceduralSounds.Generate(ProceduralSounds.Kind.Pop, SampleRate, 3005), false);

            Texture2D glow = AssetDatabase.LoadAssetAtPath<Texture2D>(GlowTexturePath);
            Material additive = ParticleMaterial("Flowerpot_Spark_Additive", glow, additive: true);
            Material smokeMat = ParticleMaterial("Flowerpot_Smoke_Alpha", glow, additive: false);

            ParticleSystem fountain = SavePrefab(BuildFountain(additive), "FX_Flowerpot_Fountain");
            ParticleSystem fuseSpark = SavePrefab(BuildFuseSpark(additive), "FX_Flowerpot_FuseSpark");
            ParticleSystem smoke = SavePrefab(BuildSmoke(smokeMat), "FX_Flowerpot_Smoke");
            GameObject model = SaveGameObject(BuildModel(), "Model_Flowerpot");
            GameObject lighter = SaveGameObject(BuildLighter(additive), "Lighter");

            CrackerDefinition def = AssetDatabase.LoadAssetAtPath<CrackerDefinition>(DefinitionPath);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<CrackerDefinition>();
                AssetDatabase.CreateAsset(def, DefinitionPath);
            }

            def.Id = "flowerpot_gold";
            def.Schema = CrackerDefinition.CurrentSchema;
            def.DisplayName = new LocalizedName("Flowerpot", "Anar", "Anar");
            def.Category = CrackerCategory.GroundFountain;
            def.SetFestivals("diwali", "tihar");
            def.Intensity = Intensity.Medium;
            def.Footprint = new Vector2(0.3f, 0.3f);
            def.EffectVolume = new Vector3(1.2f, 3f, 1.2f);
            def.Surfaces = PlacementSurface.Ground | PlacementSurface.Rooftop;
            def.IgnitionMethods = IgnitionMethod.Lighter | IgnitionMethod.Match;
            def.FuseSeconds = 2.5f;
            def.FuseAnchor = new Vector3(0f, 0.33f, 0f);
            def.CooldownSeconds = 2f;
            def.ModelPrefab = model;
            def.Voices = 3;
            def.Cost = 2;
            def.Pack = "core";
            def.CreditsModel = "own";
            def.CreditsAudio = "own-003";
            def.SetQuality(QualityTier.Low, new TierQuality(250, false, false));
            def.SetQuality(QualityTier.Mid, new TierQuality(500, true, false));
            def.SetQuality(QualityTier.High, new TierQuality(900, true, true));

            var stages = def.MutableStages;
            stages.Clear();
            stages.Add(new StageDefinition
            {
                time = 0f, emitter = fuseSpark, anchor = StageAnchor.Fuse, endWithNextEmitter = true,
                sound = fuseHiss, loopSound = true, volume = 0.5f, haptic = HapticPattern.Tick,
            });
            stages.Add(new StageDefinition
            {
                time = 2.5f, emitter = fountain, payoff = true, sound = roar, loopSound = true, volume = 0.75f,
                haptic = HapticPattern.Rumble, light = true, lightColor = new Color(1f, 0.7f, 0.28f), lightPeak = 2.5f,
                camera = CameraCue.PushInSmall,
            });
            stages.Add(new StageDefinition { time = 2.5f, sound = whoosh, volume = 0.7f });
            stages.Add(new StageDefinition { time = 2.7f, emitter = smoke });
            stages.Add(new StageDefinition { time = 10f, stop = true });

            EditorUtility.SetDirty(def);
            CrackerCatalog catalog = CatalogAssets.EnsureCore();
            catalog.Upsert(def);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return new Result { Definition = def, LighterPrefab = lighter, LighterClick = click, FlameOut = flameOut };
        }

        /// <summary>Two seconds of loopable roar: low-passed noise with a slow flutter, edges cross-faded.</summary>
        private static float[] FountainRoar(int rate, int seed)
        {
            int n = rate * 2;
            var data = new float[n];
            var rng = new Random(seed);
            float low = 0f;
            float band = 0f;
            for (int i = 0; i < n; i++)
            {
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                low += 0.05f * (noise - low);
                band += 0.35f * (noise - band);
                float flutter = 0.8f + 0.2f * (float)Math.Sin(2.0 * Math.PI * 7.0 * i / rate);
                data[i] = (low * 2.2f + (band - low) * 0.6f) * flutter;
            }

            int fade = rate / 20;
            for (int i = 0; i < fade; i++)
            {
                float g = (float)i / fade;
                float a = data[i];
                float b = data[n - fade + i];
                data[i] = a * g + b * (1f - g);
            }

            Array.Resize(ref data, n - fade);
            float peak = 0f;
            foreach (float s in data)
            {
                peak = Math.Max(peak, Math.Abs(s));
            }

            for (int i = 0; i < data.Length; i++)
            {
                data[i] = data[i] / peak * 0.85f;
            }

            return data;
        }

        internal static AudioClip WriteClip(string name, float[] samples, bool loop)
        {
            string path = $"{AudioFolder}/{name}.wav";
            WavWriter.Write(path, samples, SampleRate);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            ContentLint.AssetImportLinter.Apply(importer);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        internal static Material ParticleMaterial(string name, Texture2D texture, bool additive)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", additive ? 2f : 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        internal static ParticleSystem NewSystem(string name, Material material, int maxParticles, bool stretch)
        {
            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 5f;
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            renderer.velocityScale = 0.04f;
            renderer.lengthScale = 1.4f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return ps;
        }

        internal static void Fade(ParticleSystem ps, Color start, Color end, float endSize)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(gradient);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, endSize));
        }

        private static ParticleSystem BuildFountain(Material material)
        {
            ParticleSystem ps = NewSystem("FX_Flowerpot_Fountain", material, 900, true);
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.3f, 2.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4.5f, 6.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            main.gravityModifier = 0.95f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.55f), new Color(1f, 0.6f, 0.18f));
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 260f, 1f, 420f));
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 11f;
            shape.radius = 0.025f;
            shape.position = new Vector3(0f, 0.3f, 0f);
            shape.rotation = new Vector3(-90f, 0f, 0f);
            Fade(ps, Color.white, new Color(1f, 0.42f, 0.08f), 0.25f);
            return ps;
        }

        private static ParticleSystem BuildFuseSpark(Material material)
        {
            ParticleSystem ps = NewSystem("FX_Flowerpot_FuseSpark", material, 80, true);
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.025f);
            main.gravityModifier = 0.4f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.92f, 0.55f), new Color(1f, 0.5f, 0.1f));
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 140f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.008f;
            Fade(ps, Color.white, new Color(1f, 0.4f, 0.1f), 0.3f);
            return ps;
        }

        private static ParticleSystem BuildSmoke(Material material)
        {
            ParticleSystem ps = NewSystem("FX_Flowerpot_Smoke", material, 40, false);
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 3.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.55f, 0.5f, 0.47f, 0.22f);
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 7f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 0.08f;
            shape.position = new Vector3(0f, 0.35f, 0f);
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(gradient);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 2.6f));
            return ps;
        }

        private static GameObject BuildModel()
        {
            var root = new GameObject("Model_Flowerpot");
            Material clay = SceneKit.LitMaterial("Flowerpot_Clay", new Color(0.62f, 0.3f, 0.14f), 0.25f);
            Material band = SceneKit.LitMaterial("Flowerpot_Band", new Color(0.85f, 0.65f, 0.18f), 0.4f);
            Material fuse = SceneKit.LitMaterial("Flowerpot_Fuse", new Color(0.75f, 0.12f, 0.08f), 0.2f);
            SceneKit.Primitive(PrimitiveType.Cylinder, "Body", root.transform, new Vector3(0f, 0.13f, 0f), new Vector3(0.24f, 0.13f, 0.24f), clay, keepCollider: false);
            SceneKit.Primitive(PrimitiveType.Cylinder, "Band", root.transform, new Vector3(0f, 0.2f, 0f), new Vector3(0.245f, 0.015f, 0.245f), band, keepCollider: false);
            SceneKit.Primitive(PrimitiveType.Sphere, "Top", root.transform, new Vector3(0f, 0.27f, 0f), new Vector3(0.17f, 0.08f, 0.17f), clay, keepCollider: false);
            SceneKit.Primitive(PrimitiveType.Cylinder, "Fuse", root.transform, new Vector3(0f, 0.315f, 0f), new Vector3(0.012f, 0.025f, 0.012f), fuse, keepCollider: false);
            return root;
        }

        private static GameObject BuildLighter(Material sparkMaterial)
        {
            // The root sits at the flame tip, which is the point tested against fuses.
            var root = new GameObject("Lighter");
            Material body = SceneKit.LitMaterial("Lighter_Body_Main", new Color(0.78f, 0.1f, 0.38f), 0.6f);
            Material metal = SceneKit.LitMaterial("Lighter_Metal", new Color(0.75f, 0.75f, 0.78f), 0.8f);
            Material flameMat = SceneKit.EmissiveMaterial("Lighter_Flame_Main", new Color(1f, 0.62f, 0.2f), 6f);
            SceneKit.Primitive(PrimitiveType.Cube, "Body", root.transform, new Vector3(0f, -0.12f, 0f), new Vector3(0.045f, 0.14f, 0.028f), body, keepCollider: false);
            SceneKit.Primitive(PrimitiveType.Cube, "Cap", root.transform, new Vector3(0f, -0.04f, 0f), new Vector3(0.046f, 0.03f, 0.03f), metal, keepCollider: false);
            GameObject flame = SceneKit.Primitive(PrimitiveType.Sphere, "Flame", root.transform, new Vector3(0f, 0f, 0f), new Vector3(0.022f, 0.045f, 0.022f), flameMat, keepCollider: false);

            ParticleSystem flicker = NewSystem("Flicker", sparkMaterial, 24, false);
            flicker.transform.SetParent(flame.transform, false);
            ParticleSystem.MainModule main = flicker.main;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.05f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.3f, 0.8f), new Color(1f, 0.45f, 0.1f, 0.6f));
            ParticleSystem.EmissionModule emission = flicker.emission;
            emission.rateOverTime = 40f;
            ParticleSystem.ShapeModule shape = flicker.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.005f;
            return root;
        }

        internal static ParticleSystem SavePrefab(ParticleSystem ps, string name, string folder = Root)
        {
            GameObject saved = SaveGameObject(ps.gameObject, name, folder);
            return saved.GetComponent<ParticleSystem>();
        }

        internal static GameObject SaveGameObject(GameObject go, string name, string folder = Root)
        {
            EditorAssetUtil.EnsureFolder(folder);
            string path = $"{folder}/{name}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            return prefab;
        }
    }
}

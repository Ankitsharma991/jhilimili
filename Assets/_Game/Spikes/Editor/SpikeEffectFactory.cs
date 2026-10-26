using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace UtsavNights.EditorTools
{
    /// <summary>
    /// Authors the Phase 1 particle prefabs in code: five stress effects capped at 600 particles each
    /// (3,000 total, Gate A) plus a small fuse spark. Shuriken only; no VFX Graph (PRD 13.1).
    /// </summary>
    internal static class SpikeEffectFactory
    {
        public const string PrefabFolder = "Assets/_Game/Prefabs/Spike";
        public const string ArtFolder = "Assets/_Game/Art/Spike";
        public const string MaterialFolder = "Assets/_Game/Materials/Spike";
        public const int ParticlesPerEffect = 600;

        public sealed class Prefabs
        {
            public ParticleSystem Fountain;
            public ParticleSystem Spinner;
            public ParticleSystem AerialBurst;
            public ParticleSystem ChainCrackle;
            public ParticleSystem Sparkler;
            public ParticleSystem FuseSpark;
        }

        public static Prefabs CreateAll()
        {
            EditorAssetUtil.EnsureFolder(PrefabFolder);
            EditorAssetUtil.EnsureFolder(ArtFolder);
            EditorAssetUtil.EnsureFolder(MaterialFolder);
            Texture2D glow = CreateOrLoadGlowTexture(ArtFolder + "/glow_soft.png");
            Material additive = CreateOrUpdateAdditiveMaterial(MaterialFolder + "/Spark_Additive.mat", glow);

            return new Prefabs
            {
                Fountain = Save(BuildFountain(additive)),
                Spinner = Save(BuildSpinner(additive)),
                AerialBurst = Save(BuildAerialBurst(additive)),
                ChainCrackle = Save(BuildChainCrackle(additive)),
                Sparkler = Save(BuildSparkler(additive)),
                FuseSpark = Save(BuildFuseSpark(additive)),
            };
        }

        private static ParticleSystem Save(ParticleSystem ps)
        {
            string path = $"{PrefabFolder}/{ps.gameObject.name}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(ps.gameObject, path);
            Object.DestroyImmediate(ps.gameObject);
            return prefab.GetComponent<ParticleSystem>();
        }

        private static ParticleSystem NewSystem(string name, Material material, int maxParticles, bool stretch)
        {
            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 5f;
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            renderer.velocityScale = 0.04f;
            renderer.lengthScale = 1.5f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.sortMode = ParticleSystemSortMode.None;
            return ps;
        }

        private static void FadeOut(ParticleSystem ps, Color start, Color end)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
        }

        private static ParticleSystem BuildFountain(Material m)
        {
            ParticleSystem ps = NewSystem("FX_Spike_Fountain", m, ParticlesPerEffect, true);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
            main.gravityModifier = 0.9f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.86f, 0.45f), new Color(1f, 0.55f, 0.15f));
            var emission = ps.emission;
            emission.rateOverTime = 280f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.03f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            FadeOut(ps, Color.white, new Color(1f, 0.45f, 0.1f));
            return ps;
        }

        private static ParticleSystem BuildSpinner(Material m)
        {
            ParticleSystem ps = NewSystem("FX_Spike_Spinner", m, ParticlesPerEffect, true);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.05f);
            main.gravityModifier = 0.3f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.6f), new Color(0.6f, 1f, 0.5f));
            var emission = ps.emission;
            emission.rateOverTime = 850f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 5f;
            shape.radius = 0.01f;
            shape.rotation = new Vector3(0f, 90f, 0f);
            FadeOut(ps, Color.white, new Color(0.9f, 0.6f, 0.1f));
            return ps;
        }

        private static ParticleSystem BuildAerialBurst(Material m)
        {
            ParticleSystem ps = NewSystem("FX_Spike_AerialBurst", m, ParticlesPerEffect, true);
            var main = ps.main;
            main.duration = 2.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.12f);
            main.gravityModifier = 0.25f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.35f, 0.75f), new Color(1f, 0.7f, 0.2f));
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)ParticlesPerEffect, (short)ParticlesPerEffect, 1, 0.01f) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 100f;
            limit.drag = 1.2f;
            FadeOut(ps, Color.white, new Color(0.5f, 0.1f, 0.6f));
            return ps;
        }

        private static ParticleSystem BuildChainCrackle(Material m)
        {
            ParticleSystem ps = NewSystem("FX_Spike_ChainCrackle", m, ParticlesPerEffect, false);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(1f, 0.8f, 0.5f));
            var emission = ps.emission;
            emission.rateOverTime = 1500f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(2f, 0.02f, 0.1f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 2f;
            noise.quality = ParticleSystemNoiseQuality.Low;
            FadeOut(ps, Color.white, new Color(1f, 0.6f, 0.2f));
            return ps;
        }

        private static ParticleSystem BuildSparkler(Material m)
        {
            ParticleSystem ps = NewSystem("FX_Spike_Sparkler", m, ParticlesPerEffect, true);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.04f);
            main.gravityModifier = 0.6f;
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(1f, 0.9f, 0.55f));
            var emission = ps.emission;
            emission.rateOverTime = 1000f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.02f;
            FadeOut(ps, Color.white, new Color(1f, 0.7f, 0.3f));
            return ps;
        }

        private static ParticleSystem BuildFuseSpark(Material m)
        {
            ParticleSystem ps = NewSystem("FX_Spike_FuseSpark", m, 80, true);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.03f);
            main.gravityModifier = 0.4f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.5f), new Color(1f, 0.5f, 0.1f));
            var emission = ps.emission;
            emission.rateOverTime = 150f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.01f;
            FadeOut(ps, Color.white, new Color(1f, 0.4f, 0.1f));
            return ps;
        }

        private static Texture2D CreateOrLoadGlowTexture(string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
            {
                return existing;
            }

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    a = Mathf.Pow(a * a * (3f - 2f * a), 1.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            texture.SetPixels32(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 64;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material CreateOrUpdateAdditiveMaterial(string path, Texture2D glow)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                Debug.LogError("[Utsav setup] URP particle shader not found. Is URP installed and active?");
                shader = Shader.Find("Sprites/Default");
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.SetTexture("_BaseMap", glow);
            material.SetColor("_BaseColor", Color.white);
            // Transparent + additive (URP Particles/Unlit property contract).
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}

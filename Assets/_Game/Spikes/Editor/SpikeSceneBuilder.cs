using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UtsavNights.Spikes;

namespace UtsavNights.EditorTools
{
    /// <summary>
    /// Builds the Phase 1 spike scene from code (Spike A render blockout plus the B, C and D spike rig),
    /// so the scene can be regenerated after any change and in CI.
    /// </summary>
    public static class SpikeSceneBuilder
    {
        public const string ScenePath = "Assets/_Game/Scenes/Spike_Phase1.unity";
        public const string LightingSettingsPath = "Assets/_Game/Settings/Rendering/Lighting_Spike.lighting";
        private const string MaterialFolder = SpikeEffectFactory.MaterialFolder;

        // Palette from PRD 12: deep indigo, saffron, marigold, magenta, off-white.
        private static readonly Color NightSky = new Color(0.035f, 0.03f, 0.10f);
        private static readonly Color Ambient = new Color(0.07f, 0.06f, 0.14f);
        private static readonly Color Saffron = new Color(1f, 0.6f, 0.2f);
        private static readonly Color Marigold = new Color(1f, 0.75f, 0.15f);
        private static readonly Color Magenta = new Color(0.9f, 0.2f, 0.6f);

        [MenuItem("Utsav/Setup/3. Build Spike Scene", priority = 22)]
        public static void Build()
        {
            if (GraphicsSettings.defaultRenderPipeline == null)
            {
                ProjectSetup.CreateQualityTiers();
            }

            SpikeEffectFactory.Prefabs fx = SpikeEffectFactory.CreateAll();
            EditorAssetUtil.EnsureFolder("Assets/_Game/Scenes");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Ambient;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = NightSky;
            RenderSettings.fogStartDistance = 12f;
            RenderSettings.fogEndDistance = 45f;

            Camera camera = CreateCamera();
            CreateMoonLight();
            CreateCourtyard();
            Light flash = CreateFlashLight();
            (Transform fuse, Renderer marker) = CreatePlaceholderFlowerpot();
            Transform lighter = CreateLighter();
            CreatePostFxVolume();
            CreateEventSystem();

            var root = new GameObject("Spikes");
            root.AddComponent<PerfMonitor>();
            var particles = root.AddComponent<ParticleStressSpike>();
            root.AddComponent<AudioLatencySpike>();
            var lighterSpike = root.AddComponent<LighterDragSpike>();
            var tiers = root.AddComponent<QualityTierSwitcher>();
            root.AddComponent<SpikeHud>();

            particles.Configure(
                new[]
                {
                    new EffectSlot { label = "Fountain", prefab = fx.Fountain, position = new Vector3(-1.1f, 0.05f, 2.2f) },
                    new EffectSlot { label = "Spinner", prefab = fx.Spinner, position = new Vector3(1.1f, 0.05f, 2.6f), spinDegreesPerSecond = 900f },
                    new EffectSlot { label = "AerialBurst", prefab = fx.AerialBurst, position = new Vector3(0f, 6f, 10f) },
                    new EffectSlot { label = "ChainCrackle", prefab = fx.ChainCrackle, position = new Vector3(0f, 0.05f, 3.8f) },
                    new EffectSlot { label = "Sparkler", prefab = fx.Sparkler, position = new Vector3(-0.6f, 1.0f, 1.8f) },
                },
                flash);
            lighterSpike.Configure(camera, lighter, fuse, marker, fx.FuseSpark, fx.Fountain);
            tiers.Configure(camera);
            EditorUtility.SetDirty(particles);
            EditorUtility.SetDirty(lighterSpike);
            EditorUtility.SetDirty(tiers);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log($"[Utsav setup] Spike scene saved to {ScenePath} and set as the only build scene.");
        }

        [MenuItem("Utsav/Setup/4. Bake Spike Lighting", priority = 23)]
        public static void BakeLighting()
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.path != ScenePath)
            {
                active = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingSettingsPath);
            if (settings == null)
            {
                settings = new LightingSettings { name = "Lighting_Spike" };
                AssetDatabase.CreateAsset(settings, LightingSettingsPath);
            }

            // Small, fast, mobile-friendly bake: baked GI only, non-directional, compressed lightmaps.
            settings.bakedGI = true;
            settings.realtimeGI = false;
            settings.lightmapper = LightingSettings.Lightmapper.ProgressiveCPU;
            settings.mixedBakeMode = MixedLightingMode.Subtractive;
            settings.directionalityMode = LightmapsMode.NonDirectional;
            settings.lightmapResolution = 8f;
            settings.lightmapPadding = 2;
            settings.lightmapMaxSize = 512;
            settings.directSampleCount = 16;
            settings.indirectSampleCount = 64;
            settings.maxBounces = 2;
            EditorUtility.SetDirty(settings);

            Lightmapping.lightingSettings = settings;
            bool ok = Lightmapping.Bake();
            EditorSceneManager.MarkSceneDirty(active);
            EditorSceneManager.SaveScene(active);
            Debug.Log(ok ? "[Utsav setup] Lighting baked." : "[Utsav setup] Lighting bake failed or was cancelled; the scene still runs unbaked.");
        }

        private static Camera CreateCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.SetPositionAndRotation(new Vector3(0f, 1.7f, -5.5f), Quaternion.Euler(-3f, 0f, 0f));
            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = NightSky;
            camera.fieldOfView = 62f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 60f;
            go.AddComponent<AudioListener>();
            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.None;
            data.renderShadows = false;
            return camera;
        }

        private static void CreateMoonLight()
        {
            var go = new GameObject("Moonlight");
            go.transform.rotation = Quaternion.Euler(35f, -30f, 0f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.55f, 0.62f, 0.95f);
            light.intensity = 0.3f;
            light.shadows = LightShadows.None;
            light.lightmapBakeType = LightmapBakeType.Mixed;
        }

        private static void CreateCourtyard()
        {
            Material stone = LitMaterial("Courtyard_Stone", new Color(0.2f, 0.14f, 0.13f), 0.15f);
            Material plaster = LitMaterial("Wall_Plaster", new Color(0.6f, 0.4f, 0.3f), 0.1f);
            Material wood = LitMaterial("Lamp_Post", new Color(0.12f, 0.08f, 0.06f), 0.2f);
            Material bulb = EmissiveMaterial("Lamp_Bulb", Marigold, 4f);
            Material diya = EmissiveMaterial("Diya_Flame", Saffron, 3f);
            Material stringBulb = EmissiveMaterial("String_Light", Marigold, 3f);
            Material stringBulbAlt = EmissiveMaterial("String_Light_Magenta", Magenta, 3f);

            var environment = new GameObject("Environment");

            GameObject ground = Primitive(PrimitiveType.Plane, "Ground", environment.transform, Vector3.zero, new Vector3(3f, 1f, 3f), stone);
            MakeStatic(ground);
            GameObject wall = Primitive(PrimitiveType.Cube, "Wall", environment.transform, new Vector3(0f, 1.5f, 5f), new Vector3(12f, 3f, 0.4f), plaster);
            MakeStatic(wall);

            GameObject post = Primitive(PrimitiveType.Cylinder, "LampPost", environment.transform, new Vector3(2.2f, 1.1f, 4.4f), new Vector3(0.12f, 1.1f, 0.12f), wood);
            MakeStatic(post);
            GameObject lamp = Primitive(PrimitiveType.Sphere, "LampBulb", environment.transform, new Vector3(2.2f, 2.35f, 4.4f), Vector3.one * 0.25f, bulb);
            MakeStatic(lamp);
            var lampLightGo = new GameObject("LampLight (baked)");
            lampLightGo.transform.SetParent(environment.transform, false);
            lampLightGo.transform.position = new Vector3(2.2f, 2.2f, 4.0f);
            var lampLight = lampLightGo.AddComponent<Light>();
            lampLight.type = LightType.Point;
            lampLight.color = new Color(1f, 0.72f, 0.4f);
            lampLight.range = 7f;
            lampLight.intensity = 3f;
            lampLight.shadows = LightShadows.None;
            lampLight.lightmapBakeType = LightmapBakeType.Baked;

            float[] diyaX = { -1.8f, -0.9f, 0.9f, 1.8f };
            foreach (float x in diyaX)
            {
                GameObject d = Primitive(PrimitiveType.Sphere, "Diya", environment.transform, new Vector3(x, 0.05f, 0.4f), new Vector3(0.12f, 0.06f, 0.12f), diya);
                MakeStatic(d);
            }

            // String lights along the wall top with a gentle sag between hooks.
            for (int i = 0; i <= 20; i++)
            {
                float x = -5f + i * 0.5f;
                float sag = 0.15f * Mathf.Sin(Mathf.PI * (i % 5) / 5f);
                GameObject b = Primitive(PrimitiveType.Sphere, "StringLight", environment.transform, new Vector3(x, 3.05f - sag, 4.75f), Vector3.one * 0.07f, i % 3 == 0 ? stringBulbAlt : stringBulb);
                MakeStatic(b);
            }
        }

        private static Light CreateFlashLight()
        {
            var go = new GameObject("BurstFlash (realtime)");
            go.transform.position = new Vector3(0f, 6f, 8f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.8f, 0.6f);
            light.range = 25f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            light.enabled = false;
            return light;
        }

        private static (Transform fuse, Renderer marker) CreatePlaceholderFlowerpot()
        {
            Material clay = LitMaterial("Flowerpot_Clay", new Color(0.55f, 0.25f, 0.12f), 0.2f);
            Material fuseMat = LitMaterial("Fuse_Marker", new Color(0.9f, 0.2f, 0.1f), 0.3f);
            var pot = new GameObject("Flowerpot (placeholder)");
            pot.transform.position = new Vector3(0f, 0f, 1f);
            Primitive(PrimitiveType.Cylinder, "Body", pot.transform, new Vector3(0f, 0.14f, 1f), new Vector3(0.22f, 0.14f, 0.22f), clay);
            GameObject marker = Primitive(PrimitiveType.Sphere, "FuseMarker", pot.transform, new Vector3(0f, 0.32f, 1f), Vector3.one * 0.05f, fuseMat);
            var anchor = new GameObject("FuseAnchor");
            anchor.transform.SetParent(pot.transform, false);
            anchor.transform.position = new Vector3(0f, 0.32f, 1f);
            return (anchor.transform, marker.GetComponent<Renderer>());
        }

        private static Transform CreateLighter()
        {
            Material body = LitMaterial("Lighter_Body", new Color(0.8f, 0.1f, 0.35f), 0.6f);
            Material flame = EmissiveMaterial("Lighter_Flame", new Color(1f, 0.65f, 0.2f), 5f);
            // The root sits at the flame tip, which is the point tested against the fuse.
            var root = new GameObject("Lighter");
            Primitive(PrimitiveType.Cube, "Body", root.transform, new Vector3(0f, -0.11f, 0f), new Vector3(0.04f, 0.14f, 0.025f), body);
            Primitive(PrimitiveType.Sphere, "Flame", root.transform, Vector3.zero, new Vector3(0.025f, 0.045f, 0.025f), flame);
            foreach (Collider c in root.GetComponentsInChildren<Collider>())
            {
                Object.DestroyImmediate(c);
            }

            return root.transform;
        }

        private static void CreatePostFxVolume()
        {
            var go = new GameObject("PostFx Volume");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = ProjectSetup.CreateOrLoadPostFxProfile();
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem", typeof(EventSystem));
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 worldPosition, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = worldPosition;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        private static void MakeStatic(GameObject go)
        {
            GameObjectUtility.SetStaticEditorFlags(
                go,
                StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            Collider collider = go.GetComponent<Collider>();
            if (collider != null && go.name != "Ground")
            {
                Object.DestroyImmediate(collider);
            }
        }

        private static Material LitMaterial(string name, Color color, float smoothness)
        {
            Material m = LoadOrCreateMaterial(name, "Universal Render Pipeline/Lit");
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material EmissiveMaterial(string name, Color color, float intensity)
        {
            Material m = LoadOrCreateMaterial(name, "Universal Render Pipeline/Lit");
            m.SetColor("_BaseColor", color);
            m.SetColor("_EmissionColor", color * intensity);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material LoadOrCreateMaterial(string name, string shaderName)
        {
            EditorAssetUtil.EnsureFolder(MaterialFolder);
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[Utsav setup] Shader '{shaderName}' not found; is URP active?");
                shader = Shader.Find("Standard");
            }

            material = new Material(shader) { enableInstancing = true };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}

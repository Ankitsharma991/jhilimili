using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UtsavNights.Audio;
using UtsavNights.Core.Runtime;
using UtsavNights.Effects;
using UtsavNights.Gameplay;
using UtsavNights.Haptics;

namespace UtsavNights.EditorTools.Main
{
    /// <summary>
    /// Builds the Phase 2 Boot and Main scenes from code and makes them the build scenes:
    ///   Unity -batchmode -projectPath . -executeMethod UtsavNights.EditorTools.Main.MainSceneBuilder.BuildBatch -quit
    /// </summary>
    public static class MainSceneBuilder
    {
        public const string BootScenePath = "Assets/_Game/Scenes/Boot.unity";
        public const string MainScenePath = "Assets/_Game/Scenes/Main.unity";
        public const string LightingPath = "Assets/_Game/Settings/Rendering/Lighting_Main.lighting";

        // PRD 12 palette: deep indigo, saffron, marigold, magenta, off-white.
        private static readonly Color NightSky = new Color(0.04f, 0.03f, 0.11f);
        private static readonly Color Ambient = new Color(0.08f, 0.07f, 0.15f);
        private static readonly Color Saffron = new Color(1f, 0.6f, 0.2f);
        private static readonly Color Marigold = new Color(1f, 0.76f, 0.16f);
        private static readonly Color Magenta = new Color(0.92f, 0.22f, 0.6f);

        [MenuItem("Utsav/Phase 2/Build Boot and Main Scenes", priority = 80)]
        public static void BuildAll()
        {
            FlowerpotAssets.Result assets = FlowerpotAssets.BuildAll();
            BuildBoot();
            BuildMain(assets);
            BakeMainLighting();
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootScenePath, true),
                new EditorBuildSettingsScene(MainScenePath, true),
            };
            AssetDatabase.SaveAssets();
            Debug.Log("[Utsav setup] Boot and Main scenes built; they are now the build scenes.");
        }

        public static void BuildBatch()
        {
            try
            {
                BuildAll();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        private static void BuildBoot()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraGo = new GameObject("Boot Camera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = NightSky;
            camera.cullingMask = 0;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;

            var canvasGo = new GameObject("Splash", typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(canvasGo.transform, false);
            var title = titleGo.GetComponent<Text>();
            title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            title.text = "Utsav Nights";
            title.fontSize = 96;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = Marigold;
            var rect = title.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var boot = new GameObject("Bootstrap").AddComponent<Bootstrap>();
            boot.Configure("Main");
            EditorSceneManager.SaveScene(scene, BootScenePath);
        }

        private static void BuildMain(FlowerpotAssets.Result assets)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Ambient;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = NightSky;
            RenderSettings.fogStartDistance = 10f;
            RenderSettings.fogEndDistance = 40f;

            // Camera: low three-quarter view (PRD 6.2), pot in the lower middle, sky above for the fountain.
            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraGo.transform.SetPositionAndRotation(new Vector3(0f, 1.05f, -2.0f), Quaternion.Euler(7f, 0f, 0f));
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = NightSky;
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 50f;
            cameraGo.AddComponent<AudioListener>();
            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.None;
            cameraData.renderShadows = false;

            var rig = new GameObject("CameraRig").AddComponent<CameraRig>();
            var groundShot = new GameObject("GroundShot").transform;
            groundShot.SetParent(rig.transform, false);
            groundShot.SetPositionAndRotation(cameraGo.transform.position, cameraGo.transform.rotation);
            rig.Configure(camera, groundShot);

            CreateLights();
            CreateCourtyard();
            CreateLightProbes();

            var systems = new GameObject("Systems");
            systems.AddComponent<EffectsInstaller>();
            systems.AddComponent<AudioInstaller>();
            systems.AddComponent<HapticsInstaller>();
            systems.AddComponent<QualityManager>().Configure(camera);

            GameObject lighterVisual = (GameObject)PrefabUtility.InstantiatePrefab(assets.LighterPrefab);
            Transform flame = lighterVisual.transform.Find("Flame");
            var lighter = new GameObject("LighterController").AddComponent<LighterController>();
            lighter.Configure(camera, lighterVisual.transform, flame != null ? flame.gameObject : null, assets.LighterClick, assets.FlameOut);

            var pot = new GameObject("Flowerpot").AddComponent<CrackerRuntime>();
            pot.transform.position = new Vector3(0f, 0f, 0.6f);
            pot.Configure(assets.Definition);

            new GameObject("FirstRunHint").AddComponent<FirstRunHint>().Configure(camera);

            var volumeGo = new GameObject("PostFx Volume");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = ProjectSetup.CreateOrLoadPostFxProfile();

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
            eventSystem.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();

            EditorUtility.SetDirty(pot);
            EditorUtility.SetDirty(lighter);
            EditorSceneManager.SaveScene(scene, MainScenePath);
        }

        private static void CreateLights()
        {
            var moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.transform.rotation = Quaternion.Euler(38f, -25f, 0f);
            moon.type = LightType.Directional;
            moon.color = new Color(0.55f, 0.62f, 0.95f);
            moon.intensity = 0.28f;
            moon.shadows = LightShadows.None;
            moon.lightmapBakeType = LightmapBakeType.Mixed;

            var lamp = new GameObject("Lamp Light (baked)").AddComponent<Light>();
            lamp.transform.position = new Vector3(-1.9f, 2.1f, 2.9f);
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.72f, 0.42f);
            lamp.range = 6f;
            lamp.intensity = 2.6f;
            lamp.shadows = LightShadows.None;
            lamp.lightmapBakeType = LightmapBakeType.Baked;
        }

        private static void CreateCourtyard()
        {
            Material stone = SceneKit.LitMaterial("Courtyard_Floor", new Color(0.22f, 0.16f, 0.14f), 0.2f);
            Material border = SceneKit.LitMaterial("Courtyard_Border", new Color(0.36f, 0.26f, 0.2f), 0.15f);
            Material plaster = SceneKit.LitMaterial("Courtyard_Wall", new Color(0.62f, 0.43f, 0.3f), 0.1f);
            Material wood = SceneKit.LitMaterial("Courtyard_Wood", new Color(0.14f, 0.09f, 0.06f), 0.25f);
            Material diyaClay = SceneKit.LitMaterial("Diya_Clay", new Color(0.55f, 0.27f, 0.12f), 0.2f);
            Material diyaFlame = SceneKit.EmissiveMaterial("Diya_Flame_Main", Saffron, 4f);
            Material bulb = SceneKit.EmissiveMaterial("Lamp_Bulb_Main", Marigold, 4f);
            Material stringA = SceneKit.EmissiveMaterial("String_Light_Marigold", Marigold, 3f);
            Material stringB = SceneKit.EmissiveMaterial("String_Light_Magenta_Main", Magenta, 3f);

            Transform env = new GameObject("Environment").transform;
            Static(SceneKit.Primitive(PrimitiveType.Plane, "Floor", env, Vector3.zero, new Vector3(2f, 1f, 2f), stone, keepCollider: true));
            Static(SceneKit.Primitive(PrimitiveType.Cube, "BackWall", env, new Vector3(0f, 1.4f, 3.6f), new Vector3(9f, 2.8f, 0.35f), plaster, false));
            Static(SceneKit.Primitive(PrimitiveType.Cube, "LeftWall", env, new Vector3(-3.6f, 1.4f, 0.8f), new Vector3(0.35f, 2.8f, 5.6f), plaster, false));
            Static(SceneKit.Primitive(PrimitiveType.Cube, "RightWall", env, new Vector3(3.6f, 1.4f, 0.8f), new Vector3(0.35f, 2.8f, 5.6f), plaster, false));
            Static(SceneKit.Primitive(PrimitiveType.Cube, "Doorway", env, new Vector3(1.4f, 1.0f, 3.42f), new Vector3(1.0f, 2.0f, 0.05f), wood, false));
            Static(SceneKit.Primitive(PrimitiveType.Cube, "Step", env, new Vector3(0f, 0.04f, 2.8f), new Vector3(6f, 0.08f, 1.1f), border, false));

            Static(SceneKit.Primitive(PrimitiveType.Cylinder, "LampPost", env, new Vector3(-1.9f, 1.05f, 3.2f), new Vector3(0.1f, 1.05f, 0.1f), wood, false));
            Static(SceneKit.Primitive(PrimitiveType.Sphere, "LampBulb", env, new Vector3(-1.9f, 2.25f, 3.2f), Vector3.one * 0.22f, bulb, false));

            // Diyas in a shallow arc in front of the step.
            for (int i = 0; i < 7; i++)
            {
                float a = Mathf.Lerp(-0.9f, 0.9f, i / 6f);
                var pos = new Vector3(Mathf.Sin(a) * 1.9f, 0f, 2.1f - Mathf.Cos(a) * 0.5f);
                Static(SceneKit.Primitive(PrimitiveType.Sphere, "Diya", env, pos + new Vector3(0f, 0.03f, 0f), new Vector3(0.11f, 0.05f, 0.11f), diyaClay, false));
                Static(SceneKit.Primitive(PrimitiveType.Sphere, "DiyaFlame", env, pos + new Vector3(0f, 0.08f, 0f), new Vector3(0.025f, 0.045f, 0.025f), diyaFlame, false));
            }

            // String lights along the top of the back wall with a sag between hooks.
            for (int i = 0; i <= 24; i++)
            {
                float x = -4.2f + i * 0.35f;
                float sag = 0.14f * Mathf.Sin(Mathf.PI * (i % 6) / 6f);
                Static(SceneKit.Primitive(PrimitiveType.Sphere, "StringLight", env, new Vector3(x, 2.72f - sag, 3.38f), Vector3.one * 0.055f, i % 3 == 0 ? stringB : stringA, false));
            }
        }

        private static void Static(GameObject go) => SceneKit.MakeStatic(go);

        /// <summary>
        /// Baked light probes so moving objects (crackers, lighter) pick up the lamp, diya and string
        /// light glow. Cheap on mobile; without them dynamic objects only get flat ambient.
        /// </summary>
        private static void CreateLightProbes()
        {
            var group = new GameObject("Light Probes").AddComponent<LightProbeGroup>();
            var positions = new System.Collections.Generic.List<Vector3>();
            float[] heights = { 0.15f, 0.8f, 1.8f, 2.7f };
            for (float x = -3f; x <= 3.01f; x += 1f)
            {
                for (float z = -1.5f; z <= 3.01f; z += 0.75f)
                {
                    foreach (float y in heights)
                    {
                        positions.Add(new Vector3(x, y, z));
                    }
                }
            }

            group.probePositions = positions.ToArray();
        }

        [MenuItem("Utsav/Phase 2/Bake Main Lighting", priority = 81)]
        public static void BakeMainLighting()
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.path != MainScenePath)
            {
                active = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            }

            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingPath);
            if (settings == null)
            {
                settings = new LightingSettings { name = "Lighting_Main" };
                AssetDatabase.CreateAsset(settings, LightingPath);
            }

            settings.bakedGI = true;
            settings.realtimeGI = false;
            settings.lightmapper = LightingSettings.Lightmapper.ProgressiveCPU;
            settings.mixedBakeMode = MixedLightingMode.Subtractive;
            settings.directionalityMode = LightmapsMode.NonDirectional;
            settings.lightmapResolution = 10f;
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
            Debug.Log(ok ? "[Utsav setup] Main lighting baked." : "[Utsav setup] Main lighting bake failed; the scene still runs unbaked.");
        }
    }
}

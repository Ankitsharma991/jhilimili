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
            UiSetup.EnsureTmpEssentials();
            AdsSetup.Apply();
            FlowerpotAssets.Result assets = FlowerpotAssets.BuildAll();
            Content.CrackerDefinition rocket = RocketAssets.Build();
            Content.CrackerDefinition chakri = ChakriAssets.Build();
            MoreCrackers.Set more = MoreCrackers.BuildAll();
            AssetDatabase.SaveAssets();

            // Tray order (F04): gentle to loud, ground to sky, hero cake last.
            Content.CrackerCatalog catalog = CatalogAssets.EnsureCore();
            catalog.MutableEntries.Clear();
            foreach (Content.CrackerDefinition def in new[] { assets.Definition, more.Sparkler, chakri, more.Lar, more.Bomb, rocket, more.Skyshot, more.Cake })
            {
                catalog.Upsert(def);
            }

            // Phase 4 premium item (PRD 15.1): locked until a rewarded ad or the Remove-ads purchase.
            catalog.Upsert(more.Peacock, locked: true, unlockHint: "Premium: watch a short ad to unlock for 24 hours");

            EditorUtility.SetDirty(catalog);
            TrayIconRenderer.RenderAll(assets.Definition, more.Sparkler, chakri, more.Lar, more.Bomb, rocket, more.Skyshot, more.Cake, more.Peacock);
            AssetDatabase.SaveAssets();
            BuildBoot();
            BuildMain(assets, catalog);
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
            canvasGo.AddComponent<AdaptiveCanvasScaler>();
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

        private static void BuildMain(FlowerpotAssets.Result assets, Content.CrackerCatalog catalog)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EnvironmentArt.Result environment = EnvironmentArt.Ensure();
            RenderSettings.skybox = environment.Sky;
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
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = NightSky;
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 150f; // Phase 8: the street, the far lots and the landmarks beyond the open space
            cameraGo.AddComponent<AudioListener>();
            cameraGo.AddComponent<AdaptiveCameraFov>().Configure(60f, 36f, 74f);
            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.dithering = true; // keeps the dark night-sky gradient from banding after tonemapping
            cameraData.antialiasing = AntialiasingMode.None;
            cameraData.renderShadows = false;

            var rig = new GameObject("CameraRig").AddComponent<CameraRig>();
            var groundShot = new GameObject("GroundShot").transform;
            groundShot.SetParent(rig.transform, false);
            groundShot.SetPositionAndRotation(cameraGo.transform.position, cameraGo.transform.rotation);
            rig.Configure(camera, groundShot);

            CreateLights();
            CreateCourtyard(environment);
            CreateLightProbes();

            var systems = new GameObject("Systems");
            systems.AddComponent<EffectsInstaller>();
            systems.AddComponent<AudioInstaller>();
            systems.AddComponent<HapticsInstaller>();
            systems.AddComponent<QualityManager>().Configure(camera);
            systems.AddComponent<SettingsInstaller>();
            systems.AddComponent<MonetisationInstaller>();
            systems.AddComponent<AvatarInstaller>();
            systems.AddComponent<DecorationController>().Configure(camera, rig);
            systems.AddComponent<WalkController>().Configure(camera, rig);
            systems.AddComponent<AnalyticsInstaller>();
            systems.AddComponent<PlaytestRecorder>();
            systems.AddComponent<DevOverlay>();

            GameObject lighterVisual = (GameObject)PrefabUtility.InstantiatePrefab(assets.LighterPrefab);
            Transform flame = lighterVisual.transform.Find("Flame");
            var lighter = new GameObject("LighterController").AddComponent<LighterController>();
            lighter.Configure(camera, lighterVisual.transform, flame != null ? flame.gameObject : null, assets.LighterClick, assets.FlameOut);

            new GameObject("CrackerSpawner").AddComponent<CrackerSpawner>();
            new GameObject("WaveController").AddComponent<WaveController>().Configure(camera);
            new GameObject("FinaleController").AddComponent<FinaleController>();
            CreatePlacement(camera);

            // PRD 6.1: one cracker already stands ready; everything else comes from the tray.
            // Phase 6: the player's avatar stands in the courtyard once they have saved one.
            new GameObject("CourtyardAvatar").AddComponent<CourtyardAvatar>();

            new GameObject("StartingCrackers").AddComponent<StartingCrackers>().Configure(
                new StartingCrackers.Item { definition = assets.Definition, position = new Vector3(0f, 0f, 0.6f) });

            CreateHud(catalog);

            new GameObject("FirstRunHint").AddComponent<FirstRunHint>().Configure(camera);

            var volumeGo = new GameObject("PostFx Volume");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = ProjectSetup.CreateOrLoadPostFxProfile();

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
            eventSystem.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();

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

        /// <summary>
        /// The courtyard (Phase 5 visual pass): a 1.5 m compound wall so the sky fills the upper half of the view,
        /// a wooden gate between pillars, diyas, string lights on poles above the wall, and the neighbourhood
        /// beyond: houses with lit windows, a temple spire and a water tank against the night sky.
        /// </summary>
        private static void CreateCourtyard(EnvironmentArt.Result art)
        {
            Material stone = SceneKit.TexturedMaterial("Courtyard_Floor", art.Stone, new Vector2(2.88f, 2.4f), 0.25f);
            Material border = SceneKit.LitMaterial("Courtyard_Border", new Color(0.36f, 0.26f, 0.2f), 0.15f);
            Material wood = SceneKit.LitMaterial("Courtyard_Wood", new Color(0.14f, 0.09f, 0.06f), 0.25f);
            Material diyaClay = SceneKit.LitMaterial("Diya_Clay", new Color(0.55f, 0.27f, 0.12f), 0.2f);
            Material diyaFlame = SceneKit.EmissiveMaterial("Diya_Flame_Main", Saffron, 4f);
            Material bulb = SceneKit.EmissiveMaterial("Lamp_Bulb_Main", Marigold, 4f);
            Material stringA = SceneKit.EmissiveMaterial("String_Light_Marigold", Marigold, 3f);
            Material stringB = SceneKit.EmissiveMaterial("String_Light_Magenta_Main", Magenta, 3f);

            Transform env = new GameObject("Environment").transform;
            // Floor and step are ground for placement; the lamp post, diyas and the house's walls and gate (HomeInstaller) block it.
            // The courtyard's own floor (inside the boundary walls); the settlement draws the street and the land beyond.
            GameObject floor = SceneKit.Primitive(PrimitiveType.Plane, "Floor", env, new Vector3(0f, 0f, 0.6f), new Vector3(0.72f, 1f, 0.6f), stone, keepCollider: true);
            floor.AddComponent<PlacementSurfaceMarker>().Configure(Content.PlacementSurface.Ground);
            Static(floor);
            // Phase 7: the courtyard walls and the gate are the player's house's boundary and gate modules, built at runtime
            // by the HomeInstaller with the house behind the camera (Core/Home/HouseGeometry).
            Texture2D[] walls = HouseArt.Ensure();
            new GameObject("Home").AddComponent<HomeInstaller>().Configure(walls);

            // Phase 8: the settlement around the home lot (street, open space, neighbour lots), built at runtime.
            new GameObject("Neighbourhood").AddComponent<NeighbourhoodInstaller>().Configure(walls, art.Stone,
                AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/Crackers/Rocket_Spark_Additive.mat"));

            GameObject step = SceneKit.Primitive(PrimitiveType.Cube, "Step", env, new Vector3(0f, 0.04f, 2.8f), new Vector3(6f, 0.08f, 1.1f), border, true);
            step.AddComponent<PlacementSurfaceMarker>().Configure(Content.PlacementSurface.Ground);
            Static(step);

            Static(SceneKit.Primitive(PrimitiveType.Cylinder, "LampPost", env, new Vector3(-1.9f, 1.05f, 3.2f), new Vector3(0.1f, 1.05f, 0.1f), wood, true));
            Static(SceneKit.Primitive(PrimitiveType.Sphere, "LampBulb", env, new Vector3(-1.9f, 2.25f, 3.2f), Vector3.one * 0.22f, bulb, false));

            // Diyas in a shallow arc in front of the step.
            for (int i = 0; i < 7; i++)
            {
                float a = Mathf.Lerp(-0.9f, 0.9f, i / 6f);
                var pos = new Vector3(Mathf.Sin(a) * 1.9f, 0f, 2.1f - Mathf.Cos(a) * 0.5f);
                Static(SceneKit.Primitive(PrimitiveType.Sphere, "Diya", env, pos + new Vector3(0f, 0.03f, 0f), new Vector3(0.11f, 0.05f, 0.11f), diyaClay, true));
                Static(SceneKit.Primitive(PrimitiveType.Sphere, "DiyaFlame", env, pos + new Vector3(0f, 0.08f, 0f), new Vector3(0.025f, 0.045f, 0.025f), diyaFlame, false));
            }

            // String lights hanging between bamboo poles (and the lamp post) above the wall.
            var hooks = new[]
            {
                new Vector3(-3.35f, 2.35f, 3.3f), new Vector3(-1.9f, 2.12f, 3.2f), new Vector3(2.3f, 2.3f, 3.3f),
                new Vector3(3.35f, 2.4f, 3.3f),
            };
            foreach (Vector3 hook in hooks)
            {
                if (!Mathf.Approximately(hook.x, -1.9f))
                {
                    Static(SceneKit.Primitive(PrimitiveType.Cylinder, "BambooPole", env, new Vector3(hook.x, hook.y / 2f, hook.z), new Vector3(0.05f, hook.y / 2f, 0.05f), wood, true));
                }
            }

            int bulbIndex = 0;
            for (int h = 0; h < hooks.Length - 1; h++)
            {
                Vector3 a = hooks[h], b = hooks[h + 1];
                int count = Mathf.Max(3, Mathf.RoundToInt(Vector3.Distance(a, b) / 0.3f));
                for (int i = 1; i < count; i++)
                {
                    float t = i / (float)count;
                    Vector3 p = Vector3.Lerp(a, b, t) - new Vector3(0f, 0.32f * 4f * t * (1f - t), 0f);
                    Static(SceneKit.Primitive(PrimitiveType.Sphere, "StringLight", env, p, Vector3.one * 0.055f, bulbIndex++ % 3 == 0 ? stringB : stringA, false));
                }
            }

            CreateSkyline(env);
        }

        /// <summary>
        /// Distant landmarks beyond the settlement (Phase 8: the neighbour houses are the settlement's lots now): a temple
        /// spire with a saffron flag and a water tank on legs, far enough to stay low on screen and leave the sky open.
        /// Batching-static only: far away, so they stay out of the lightmap bake.
        /// </summary>
        private static void CreateSkyline(Transform env)
        {
            Material house = SceneKit.LitMaterial("Skyline_House", new Color(0.075f, 0.06f, 0.1f), 0.05f);
            Material temple = SceneKit.LitMaterial("Skyline_Temple", new Color(0.16f, 0.1f, 0.08f), 0.05f);
            Material flag = SceneKit.EmissiveMaterial("Skyline_Flag", Saffron, 0.8f);

            Transform root = new GameObject("Skyline").transform;
            root.SetParent(env, false);

            // Temple spire (shikhara): stacked, narrowing blocks with a finial and a flag.
            Vector3 templeBase = new Vector3(16f, 0f, 58f);
            float y = 0f;
            for (int i = 0; i < 6; i++)
            {
                float size = 3.2f - i * 0.48f;
                float tier = i == 0 ? 3.2f : 1.1f;
                Skyline(SceneKit.Primitive(PrimitiveType.Cube, "Shikhara", root, templeBase + new Vector3(0f, y + tier / 2f, 0f), new Vector3(size, tier, size), temple, false));
                y += tier;
            }

            Skyline(SceneKit.Primitive(PrimitiveType.Sphere, "Kalash", root, templeBase + new Vector3(0f, y + 0.25f, 0f), new Vector3(0.45f, 0.55f, 0.45f), temple, false));
            Skyline(SceneKit.Primitive(PrimitiveType.Cube, "FlagPole", root, templeBase + new Vector3(0f, y + 1.1f, 0f), new Vector3(0.05f, 1.4f, 0.05f), temple, false));
            GameObject flagQuad = SceneKit.Primitive(PrimitiveType.Quad, "Flag", root, templeBase + new Vector3(0.38f, y + 1.55f, -0.05f), new Vector3(0.7f, 0.42f, 1f), flag, false);
            flagQuad.transform.localRotation = Quaternion.Euler(0f, 0f, -8f);
            Skyline(flagQuad);

            // Water tank on legs, far to the left.
            Vector3 tank = new Vector3(-15f, 2.6f, 54f);
            Skyline(SceneKit.Primitive(PrimitiveType.Cylinder, "WaterTank", root, tank + new Vector3(0f, 1.6f, 0f), new Vector3(1.3f, 0.55f, 1.3f), house, false));
            foreach (Vector2 leg in new[] { new Vector2(-0.45f, -0.45f), new Vector2(0.45f, -0.45f), new Vector2(-0.45f, 0.45f), new Vector2(0.45f, 0.45f) })
            {
                Skyline(SceneKit.Primitive(PrimitiveType.Cube, "TankLeg", root, tank + new Vector3(leg.x, 0.5f, leg.y), new Vector3(0.06f, 1.0f, 0.06f), house, false));
            }
        }

        /// <summary>Far scenery: batched, but not part of the lightmap bake (lit by probes and the moon).</summary>
        private static void Skyline(GameObject go)
        {
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            go.GetComponent<MeshRenderer>().receiveGI = ReceiveGI.LightProbes;
        }

        private static void Static(GameObject go) => SceneKit.MakeStatic(go);

        private static void CreateHud(Content.CrackerCatalog catalog)
        {
            var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            canvasGo.AddComponent<AdaptiveCanvasScaler>();
            var safe = new GameObject("SafeArea", typeof(RectTransform), typeof(UI.SafeAreaFitter));
            safe.transform.SetParent(canvasGo.transform, false);
            var safeRect = (RectTransform)safe.transform;
            safeRect.anchorMin = Vector2.zero;
            safeRect.anchorMax = Vector2.one;
            safeRect.offsetMin = Vector2.zero;
            safeRect.offsetMax = Vector2.zero;
            var trayGo = new GameObject("TrayView", typeof(RectTransform));
            trayGo.transform.SetParent(safe.transform, false);
            var trayRect = (RectTransform)trayGo.transform;
            trayRect.anchorMin = Vector2.zero;
            trayRect.anchorMax = Vector2.one;
            trayRect.offsetMin = Vector2.zero;
            trayRect.offsetMax = Vector2.zero;
            trayGo.AddComponent<UI.TrayView>().Configure(catalog);
            var settingsGo = new GameObject("SettingsSheet", typeof(RectTransform));
            settingsGo.transform.SetParent(safe.transform, false);
            var settingsRect = (RectTransform)settingsGo.transform;
            settingsRect.anchorMin = Vector2.zero;
            settingsRect.anchorMax = Vector2.one;
            settingsRect.offsetMin = Vector2.zero;
            settingsRect.offsetMax = Vector2.zero;
            settingsGo.AddComponent<UI.SettingsSheet>();

            // Phase 6: the avatar creator, opened from the person button next to Settings.
            var avatarGo = new GameObject("AvatarCreator", typeof(RectTransform));
            avatarGo.transform.SetParent(safe.transform, false);
            var avatarRect = (RectTransform)avatarGo.transform;
            avatarRect.anchorMin = Vector2.zero;
            avatarRect.anchorMax = Vector2.one;
            avatarRect.offsetMin = Vector2.zero;
            avatarRect.offsetMax = Vector2.zero;
            avatarGo.AddComponent<UI.AvatarCreator>();
            // Phase 7: decoration mode, opened from the house button next to the avatar button.
            var decorGo = new GameObject("DecorationMode", typeof(RectTransform));
            decorGo.transform.SetParent(safe.transform, false);
            var decorRect = (RectTransform)decorGo.transform;
            decorRect.anchorMin = Vector2.zero;
            decorRect.anchorMax = Vector2.one;
            decorRect.offsetMin = Vector2.zero;
            decorRect.offsetMax = Vector2.zero;
            decorGo.AddComponent<UI.DecorationMode>();

            // Phase 8: walking in the neighbourhood, from the walking-figure button next to the house button.
            var walkGo = new GameObject("WalkMode", typeof(RectTransform));
            walkGo.transform.SetParent(safe.transform, false);
            var walkRect = (RectTransform)walkGo.transform;
            walkRect.anchorMin = Vector2.zero;
            walkRect.anchorMax = Vector2.one;
            walkRect.offsetMin = Vector2.zero;
            walkRect.offsetMax = Vector2.zero;
            walkGo.AddComponent<UI.WalkMode>();
            var finaleGo = new GameObject("Finale", typeof(RectTransform));
            finaleGo.transform.SetParent(safe.transform, false);
            var finaleRect = (RectTransform)finaleGo.transform;
            finaleRect.anchorMin = Vector2.zero;
            finaleRect.anchorMax = Vector2.one;
            finaleRect.offsetMin = Vector2.zero;
            finaleRect.offsetMax = Vector2.zero;
            finaleGo.AddComponent<UI.FinaleButton>();
            finaleGo.transform.SetSiblingIndex(settingsGo.transform.GetSiblingIndex());
            canvasGo.AddComponent<UI.ScreenReaderSupport>();
            canvasGo.AddComponent<UI.MeasureTools>();
            canvasGo.AddComponent<UI.UnlockSheet>();
        }

        private static void CreatePlacement(Camera camera)
        {
            Material valid = SceneKit.EmissiveMaterial("Ghost_Valid", new Color(0.35f, 1f, 0.45f), 1.6f);
            Material invalid = SceneKit.EmissiveMaterial("Ghost_Invalid", new Color(1f, 0.28f, 0.22f), 1.6f);
            var ghostGo = new GameObject("PlacementGhost");
            GameObject ring = SceneKit.Primitive(PrimitiveType.Cylinder, "Ring", ghostGo.transform, new Vector3(0f, 0.01f, 0f), new Vector3(0.5f, 0.004f, 0.5f), valid, keepCollider: false);
            var ghost = ghostGo.AddComponent<GhostPreview>();
            ghost.Configure(ring.GetComponent<Renderer>(), valid, invalid);

            // Soft additive column for aerial corridors (cylinder: 2 units tall at scale 1).
            Texture2D glow = AssetDatabase.LoadAssetAtPath<Texture2D>(FlowerpotAssets.GlowTexturePath);
            Material beamValid = FlowerpotAssets.ParticleMaterial("Ghost_Beam_Valid", glow, additive: true);
            beamValid.SetColor("_BaseColor", new Color(0.35f, 1f, 0.45f, 0.18f));
            Material beamInvalid = FlowerpotAssets.ParticleMaterial("Ghost_Beam_Invalid", glow, additive: true);
            beamInvalid.SetColor("_BaseColor", new Color(1f, 0.3f, 0.25f, 0.18f));
            EditorUtility.SetDirty(beamValid);
            EditorUtility.SetDirty(beamInvalid);
            GameObject beam = SceneKit.Primitive(PrimitiveType.Cylinder, "Beam", ghostGo.transform, Vector3.zero, Vector3.one, beamValid, keepCollider: false);
            beam.SetActive(false);
            ghost.ConfigureBeam(beam.GetComponent<Renderer>(), beamValid, beamInvalid);
            new GameObject("PlacementController").AddComponent<PlacementController>().Configure(camera, ghost);
        }

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
            settings.directSampleCount = 32;
            settings.indirectSampleCount = 256;

            // Smooths the noise that showed as contour blotches on the walls.
            settings.filteringMode = LightingSettings.FilterMode.Advanced;
            settings.denoiserTypeIndirect = LightingSettings.DenoiserType.None;
            settings.filterTypeIndirect = LightingSettings.FilterType.Gaussian;
            settings.filteringGaussianRadiusIndirect = 3f;
            settings.filterTypeDirect = LightingSettings.FilterType.Gaussian;
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

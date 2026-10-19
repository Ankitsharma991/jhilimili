using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UtsavNights.EditorTools
{
    /// <summary>
    /// Phase 1 project configuration as code (PRD 19 Phase 1, Appendix A), so a clean clone can be set up
    /// with one menu click or one batch-mode command:
    ///   Unity -batchmode -projectPath . -executeMethod UtsavNights.EditorTools.ProjectSetup.RunAllBatch -quit
    /// Every step is idempotent.
    /// </summary>
    public static class ProjectSetup
    {
        public const string ApplicationId = "com.ankitsharma.utsavnights";
        public const string ProductName = "Utsav Nights";
        public const string CompanyName = "Ankit Sharma";
        public const string Version = "0.1.0";
        public const AndroidSdkVersions MinSdk = AndroidSdkVersions.AndroidApiLevel26;
        public const string RenderingFolder = "Assets/_Game/Settings/Rendering";
        public const string BloomProfilePath = RenderingFolder + "/PostFx_Night.asset";

        private const string DefaultPostProcessDataPath =
            "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";

        public static readonly TierSpec[] Tiers =
        {
            new TierSpec("Low", 0.8f, 1, false, false, 2, 1, 0.7f),
            new TierSpec("Mid", 0.9f, 1, true, false, 4, 0, 1.0f),
            new TierSpec("High", 1.0f, 2, true, true, 4, 0, 1.5f),
        };

        public const int DefaultTierIndex = 1;

        public readonly struct TierSpec
        {
            public readonly string Name;
            public readonly float RenderScale;
            public readonly int Msaa;
            public readonly bool Hdr;
            public readonly bool Shadows;
            public readonly int AdditionalLightsPerObject;
            public readonly int TextureMipLimit;
            public readonly float LodBias;

            public TierSpec(string name, float renderScale, int msaa, bool hdr, bool shadows, int additionalLights, int textureMipLimit, float lodBias)
            {
                Name = name;
                RenderScale = renderScale;
                Msaa = msaa;
                Hdr = hdr;
                Shadows = shadows;
                AdditionalLightsPerObject = additionalLights;
                TextureMipLimit = textureMipLimit;
                LodBias = lodBias;
            }
        }

        [MenuItem("Utsav/Setup/Run All Phase 1 Setup", priority = 0)]
        public static void RunAll()
        {
            ApplyPlayerSettings();
            CreateQualityTiers();
            SpikeSceneBuilder.Build();
            SpikeSceneBuilder.BakeLighting();
            AssetDatabase.SaveAssets();
            Debug.Log("[Utsav setup] Phase 1 setup complete. If asked, restart the editor to enable the Input System backend.");
        }

        public static void RunAllBatch()
        {
            try
            {
                RunAll();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Utsav/Setup/1. Apply Android Player Settings", priority = 20)]
        public static void ApplyPlayerSettings()
        {
            NamedBuildTarget android = NamedBuildTarget.Android;
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.SetApplicationIdentifier(android, ApplicationId);
            PlayerSettings.Android.bundleVersionCode = Math.Max(1, PlayerSettings.Android.bundleVersionCode);

            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = MinSdk;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Medium);
            PlayerSettings.gcIncremental = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.enableFrameTimingStats = true;
            PlayerSettings.Android.optimizedFramePacing = true;
            PlayerSettings.SplashScreen.show = false;

            EditorUserBuildSettings.buildAppBundle = true;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;

            SetActiveInputHandlerToInputSystem();
            SetBestLatencyAudio();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Utsav setup] Player settings applied: {ApplicationId}, IL2CPP, ARM64, API {(int)MinSdk}+, AAB, ASTC, portrait, incremental GC.");
        }

        private static void SetActiveInputHandlerToInputSystem()
        {
            UnityEngine.Object playerSettings = Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings");
            if (playerSettings == null)
            {
                Debug.LogWarning("[Utsav setup] PlayerSettings asset not found; set Active Input Handling to 'Input System Package (New)' manually.");
                return;
            }

            var so = new SerializedObject(playerSettings);
            SerializedProperty handler = so.FindProperty("activeInputHandler");
            if (handler == null)
            {
                Debug.LogWarning("[Utsav setup] activeInputHandler not found; set Active Input Handling manually.");
                return;
            }

            // 0 = old Input Manager, 1 = Input System package, 2 = both.
            if (handler.intValue != 1)
            {
                handler.intValue = 1;
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.LogWarning("[Utsav setup] Active Input Handling switched to the Input System package. Restart the editor.");
            }
        }

        private static void SetBestLatencyAudio()
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/AudioManager.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogWarning("[Utsav setup] AudioManager.asset not found; set DSP Buffer Size to Best latency manually.");
                return;
            }

            var so = new SerializedObject(assets[0]);
            // 0 default, 256 best latency, 512 good latency, 1024 best performance.
            EditorAssetUtil.SetInt(so, "m_DSPBufferSize", 256);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Utsav/Setup/2. Create URP Quality Tiers", priority = 21)]
        public static void CreateQualityTiers()
        {
            EditorAssetUtil.EnsureFolder(RenderingFolder);
            var assets = new UniversalRenderPipelineAsset[Tiers.Length];
            for (int i = 0; i < Tiers.Length; i++)
            {
                assets[i] = CreateOrUpdatePipelineAsset(Tiers[i]);
            }

            ConfigureQualityLevels(assets);
            GraphicsSettings.defaultRenderPipeline = assets[DefaultTierIndex];
            CreateOrLoadPostFxProfile();
            AssetDatabase.SaveAssets();
            QualitySettings.SetQualityLevel(DefaultTierIndex, true);
            Debug.Log("[Utsav setup] URP tiers Low / Mid / High created; Mid is the default until the Phase 2 governor.");
        }

        private static UniversalRenderPipelineAsset CreateOrUpdatePipelineAsset(TierSpec spec)
        {
            string assetPath = $"{RenderingFolder}/URP_{spec.Name}.asset";
            string rendererPath = $"{RenderingFolder}/URP_{spec.Name}_Renderer.asset";
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            if (asset == null)
            {
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, rendererPath);
                EnsurePostProcessData(renderer);
                asset = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(asset, assetPath);
            }

            asset.renderScale = spec.RenderScale;
            asset.msaaSampleCount = spec.Msaa;
            asset.supportsHDR = spec.Hdr;
            asset.shadowDistance = spec.Shadows ? 25f : 0f;
            asset.useSRPBatcher = true;

            var so = new SerializedObject(asset);
            EditorAssetUtil.SetBool(so, "m_MainLightShadowsSupported", spec.Shadows);
            EditorAssetUtil.SetBool(so, "m_AdditionalLightShadowsSupported", false);
            EditorAssetUtil.SetBool(so, "m_SoftShadowsSupported", spec.Shadows);
            EditorAssetUtil.SetInt(so, "m_AdditionalLightsPerObjectLimit", spec.AdditionalLightsPerObject);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void EnsurePostProcessData(UniversalRendererData renderer)
        {
            var so = new SerializedObject(renderer);
            SerializedProperty data = so.FindProperty("postProcessData");
            if (data == null || data.objectReferenceValue != null)
            {
                return;
            }

            var defaultData = AssetDatabase.LoadAssetAtPath<PostProcessData>(DefaultPostProcessDataPath);
            if (defaultData == null)
            {
                Debug.LogWarning("[Utsav setup] Default PostProcessData not found; bloom may not render. Assign it on the renderer asset.");
                return;
            }

            data.objectReferenceValue = defaultData;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureQualityLevels(UniversalRenderPipelineAsset[] assets)
        {
            UnityEngine.Object[] qualityAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
            if (qualityAssets == null || qualityAssets.Length == 0)
            {
                Debug.LogError("[Utsav setup] QualitySettings.asset not found.");
                return;
            }

            var so = new SerializedObject(qualityAssets[0]);
            SerializedProperty levels = so.FindProperty("m_QualitySettings");
            levels.arraySize = Tiers.Length;
            for (int i = 0; i < Tiers.Length; i++)
            {
                SerializedProperty level = levels.GetArrayElementAtIndex(i);
                level.FindPropertyRelative("name").stringValue = Tiers[i].Name;
                SerializedProperty pipeline = level.FindPropertyRelative("customRenderPipeline");
                if (pipeline != null)
                {
                    pipeline.objectReferenceValue = assets[i];
                }

                EditorAssetUtil.SetRelativeInt(level, "vSyncCount", 0);
                EditorAssetUtil.SetRelativeInt(level, "antiAliasing", 0);
                EditorAssetUtil.SetRelativeInt(level, "globalTextureMipmapLimit", Tiers[i].TextureMipLimit);
                EditorAssetUtil.SetRelativeFloat(level, "lodBias", Tiers[i].LodBias);
            }

            EditorAssetUtil.SetInt(so, "m_CurrentQuality", DefaultTierIndex);
            SerializedProperty perPlatform = so.FindProperty("m_PerPlatformDefaultQuality");
            if (perPlatform != null && perPlatform.isArray)
            {
                for (int i = 0; i < perPlatform.arraySize; i++)
                {
                    EditorAssetUtil.SetRelativeInt(perPlatform.GetArrayElementAtIndex(i), "second", DefaultTierIndex);
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Bloom plus neutral tonemapping; only rendered when a camera enables post-processing (Mid / High).</summary>
        public static VolumeProfile CreateOrLoadPostFxProfile()
        {
            EditorAssetUtil.EnsureFolder(RenderingFolder);
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(BloomProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, BloomProfilePath);
            }

            if (!profile.TryGet(out Bloom bloom))
            {
                bloom = profile.Add<Bloom>(true);
                bloom.name = "Bloom";
                AssetDatabase.AddObjectToAsset(bloom, profile);
            }

            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(1.2f);
            bloom.scatter.Override(0.6f);
            bloom.highQualityFiltering.Override(false);

            if (!profile.TryGet(out Tonemapping tonemapping))
            {
                tonemapping = profile.Add<Tonemapping>(true);
                tonemapping.name = "Tonemapping";
                AssetDatabase.AddObjectToAsset(tonemapping, profile);
            }

            tonemapping.mode.Override(TonemappingMode.Neutral);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }
    }
}

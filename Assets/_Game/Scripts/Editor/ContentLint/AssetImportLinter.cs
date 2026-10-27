using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UtsavNights.Core.Assets;

namespace UtsavNights.EditorTools.ContentLint
{
    /// <summary>
    /// Phase 5 step 2: applies <see cref="ImportRules"/> to new game audio and art when they are first imported,
    /// and checks every existing one for the content lint (errors fail the build). Utsav/Content/Apply Import
    /// Rules puts every asset back on its rule after a manual change.
    /// </summary>
    public sealed class AssetImportLinter : AssetPostprocessor
    {
        private void OnPreprocessAudio()
        {
            if (ImportRules.IsGameAudio(assetPath) && assetImporter.importSettingsMissing)
            {
                Apply((AudioImporter)assetImporter);
            }
        }

        private void OnPreprocessTexture()
        {
            if (ImportRules.IsGameArt(assetPath) && assetImporter.importSettingsMissing)
            {
                Apply((TextureImporter)assetImporter);
            }
        }

        [MenuItem("Utsav/Content/Apply Import Rules", priority = 61)]
        public static void ApplyAll()
        {
            int count = 0;
            foreach (string path in FindGameAssets())
            {
                AssetImporter importer = AssetImporter.GetAtPath(path);
                if (importer is AudioImporter audio)
                {
                    Apply(audio);
                }
                else if (importer is TextureImporter texture)
                {
                    Apply(texture);
                }
                else
                {
                    continue;
                }

                importer.SaveAndReimport();
                count++;
            }

            Debug.Log($"{ContentLintRunner.LogTag} import rules applied to {count} assets");
        }

        public static void Apply(AudioImporter importer)
        {
            AudioSpec spec = ImportRules.SpecFor(ImportRules.RoleOf(importer.assetPath));
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = ToUnity(spec.Load);
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = spec.VorbisQuality;
            settings.preloadAudioData = spec.Preload;
            int rate = WavRate(importer.assetPath);
            if (rate > ImportRules.MaxUsefulFrequency)
            {
                settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
                settings.sampleRateOverride = (uint)ImportRules.MaxUsefulFrequency;
            }

            importer.defaultSampleSettings = settings;
            importer.forceToMono = spec.ForceMono;
            importer.loadInBackground = spec.LoadInBackground;
            EditorUtility.SetDirty(importer);
        }

        public static void Apply(TextureImporter importer)
        {
            bool icon = importer.assetPath.StartsWith(ImportRules.IconFolder, System.StringComparison.Ordinal);
            int max = icon ? ImportRules.MaxIconSize : ImportRules.MaxArtSize;
            if (icon)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
            }

            importer.maxTextureSize = Mathf.Min(importer.maxTextureSize, max);
            if (importer.textureCompression == TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Compressed;
            }

            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
            if (android.overridden && android.maxTextureSize > max)
            {
                android.maxTextureSize = max;
                importer.SetPlatformTextureSettings(android);
            }

            EditorUtility.SetDirty(importer);
        }

        public static List<LintIssue> Lint()
        {
            var clips = new List<AudioFacts>();
            var textures = new List<TextureFacts>();
            foreach (string path in FindGameAssets())
            {
                AssetImporter importer = AssetImporter.GetAtPath(path);
                if (importer is AudioImporter audio)
                {
                    clips.Add(Facts(audio));
                }
                else if (importer is TextureImporter texture)
                {
                    textures.Add(Facts(texture));
                }
            }

            var issues = new List<LintIssue>();
            foreach (AssetIssue issue in ImportRules.CheckAudio(clips))
            {
                issues.Add(ToLint("asset.audio", issue));
            }

            foreach (AssetIssue issue in ImportRules.CheckTextures(textures))
            {
                issues.Add(ToLint("asset.texture", issue));
            }

            float ram = 0f;
            clips.ForEach(c => ram += ImportRules.DecompressedMb(c));
            Debug.Log($"{ContentLintRunner.LogTag} import rules: {clips.Count} sounds ({ram:0.0} MB decompressed of " +
                      $"{ImportRules.DecompressedBudgetMb:0} MB), {textures.Count} textures");
            return issues;
        }

        private static IEnumerable<string> FindGameAssets()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip t:Texture2D", new[] { "Assets/_Game/Audio", "Assets/_Game/Art" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (ImportRules.IsGameAudio(path) || ImportRules.IsGameArt(path))
                {
                    yield return path;
                }
            }
        }

        private static AudioFacts Facts(AudioImporter importer)
        {
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            if (importer.ContainsSampleSettingsOverride("Android"))
            {
                settings = importer.GetOverrideSampleSettings("Android");
            }

            var facts = new AudioFacts
            {
                Path = importer.assetPath,
                Load = FromUnity(settings.loadType),
                Vorbis = settings.compressionFormat == AudioCompressionFormat.Vorbis,
                Quality = settings.quality,
                ForceMono = importer.forceToMono,
                Preload = settings.preloadAudioData,
                LoadInBackground = importer.loadInBackground,
            };

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(importer.assetPath);
            if (clip != null)
            {
                facts.Seconds = clip.length;
                facts.Frequency = clip.frequency;
                facts.Channels = clip.channels;
                if (clip.loadType == AudioClipLoadType.DecompressOnLoad && clip.LoadAudioData() && clip.samples > 0)
                {
                    var samples = new float[clip.samples * clip.channels];
                    if (clip.GetData(samples, 0))
                    {
                        facts.LeadingSilence = ImportRules.LeadingSilence(samples, clip.channels, clip.frequency);
                        facts.Peak = ImportRules.Peak(samples);
                    }
                }
            }

            return facts;
        }

        private static TextureFacts Facts(TextureImporter importer)
        {
            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
            bool overridden = android.overridden;
            return new TextureFacts
            {
                Path = importer.assetPath,
                MaxSize = overridden ? Mathf.Max(android.maxTextureSize, 0) : importer.maxTextureSize,
                Compressed = overridden
                    ? android.format != TextureImporterFormat.RGBA32 && android.format != TextureImporterFormat.RGB24 &&
                      (android.format != TextureImporterFormat.Automatic || android.textureCompression != TextureImporterCompression.Uncompressed)
                    : importer.textureCompression != TextureImporterCompression.Uncompressed,
                Mipmaps = importer.mipmapEnabled,
                IsSprite = importer.textureType == TextureImporterType.Sprite,
            };
        }

        private static int WavRate(string path)
        {
            if (!path.EndsWith(".wav", System.StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
            {
                return 0;
            }

            var header = new byte[65536];
            int read;
            using (FileStream stream = File.OpenRead(path))
            {
                read = stream.Read(header, 0, header.Length);
            }

            if (read < header.Length)
            {
                System.Array.Resize(ref header, read);
            }

            return ImportRules.WavSampleRate(header);
        }

        private static LintIssue ToLint(string rule, AssetIssue issue) =>
            new LintIssue(issue.Error ? LintSeverity.Error : LintSeverity.Warning, rule, issue.Path, issue.Message);

        private static AudioClipLoadType ToUnity(AudioLoad load) => load switch
        {
            AudioLoad.Streaming => AudioClipLoadType.Streaming,
            AudioLoad.CompressedInMemory => AudioClipLoadType.CompressedInMemory,
            _ => AudioClipLoadType.DecompressOnLoad,
        };

        private static AudioLoad FromUnity(AudioClipLoadType load) => load switch
        {
            AudioClipLoadType.Streaming => AudioLoad.Streaming,
            AudioClipLoadType.CompressedInMemory => AudioLoad.CompressedInMemory,
            _ => AudioLoad.DecompressOnLoad,
        };
    }
}

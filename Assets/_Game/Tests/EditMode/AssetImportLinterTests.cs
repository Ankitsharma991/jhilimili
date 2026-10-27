using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UtsavNights.EditorTools;
using UtsavNights.EditorTools.ContentLint;
using UtsavNights.EditorTools.Main;

namespace UtsavNights.Tests
{
    /// <summary>Phase 5 step 2 in the editor: a new recording gets its import rule; today's content passes the lint.</summary>
    public class AssetImportLinterTests
    {
        private const string Folder = "Assets/_Game/Audio/ZzImportRuleTest";

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(Folder);

        [Test]
        public void NewHighRateLoop_IsImportedOnItsRule()
        {
            EditorAssetUtil.EnsureFolder(Folder);
            string path = Folder + "/hiss_loop.wav";
            var samples = new float[96000];
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = 0.3f * Mathf.Sin(i * 0.05f);
            }

            WavWriter.Write(path, samples, 96000);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            Assert.AreEqual(AudioClipLoadType.DecompressOnLoad, settings.loadType);
            Assert.AreEqual(AudioCompressionFormat.Vorbis, settings.compressionFormat);
            Assert.AreEqual(0.6f, settings.quality, 0.001f);
            Assert.IsTrue(importer.forceToMono);
            Assert.AreEqual(AudioSampleRateSetting.OverrideSampleRate, settings.sampleRateSetting);
            Assert.AreEqual(48000u, settings.sampleRateOverride);
            Assert.AreEqual(48000, AssetDatabase.LoadAssetAtPath<AudioClip>(path).frequency, "resampled on import");
            Assert.IsFalse(AssetImportLinter.Lint().Any(i => i.AssetName == path), "a file on its rule has no findings");
        }

        [Test]
        public void ChangedSettings_AreCaughtByTheLint()
        {
            EditorAssetUtil.EnsureFolder(Folder);
            string path = Folder + "/boom.wav";
            WavWriter.Write(path, new float[4800], 48000);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();

            Assert.IsTrue(AssetImportLinter.Lint().Any(i => i.AssetName == path && i.Severity == LintSeverity.Error));
        }

        [Test]
        public void ShippedSoundsAndIcons_HaveNoImportErrors()
        {
            var errors = AssetImportLinter.Lint().Where(i => i.Severity == LintSeverity.Error).ToList();
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }
    }
}

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UtsavNights.Core.Neighbourhood;
using UtsavNights.Core.Packs;
using UtsavNights.EditorTools.Main;
using UtsavNights.Gameplay;

namespace UtsavNights.EditorTools
{
    /// <summary>
    /// Builds and lints content packs (Phase 9 step 1). Output follows the hosting layout: catalog.json at the root and each
    /// pack's files under packs/{packId}/{version}/, ready to copy to static HTTPS storage. CI runs <see cref="CiBuildSamples"/>,
    /// which fails the build when any pack breaks a rule (PRD 6: the build tool refuses oversize content).
    /// </summary>
    public static class PackTool
    {
        public const string SampleFolder = "Docs/Phase9/sample_packs";

        [MenuItem("Utsav/Phase 9/Build Sample Packs", priority = 95)]
        public static List<string> BuildSamples()
        {
            var problems = new List<string>();
            var catalog = new PackCatalog();
            EnvironmentDef def = SamplePacks.BlueCityEnvironment();
            var files = new Dictionary<string, byte[]>
            {
                [PackLint.EnvironmentFile] = JsonPackReader.Bytes(def),
                [PackLint.LayoutFile] = JsonPackReader.Bytes(SamplePacks.BlueCityLayout()),
                [PackLint.PreviewFile] = Preview(def),
            };
            PackManifest manifest = PackBuilder.Seal(SamplePacks.BlueCityManifest(), files);
            problems.AddRange(Write(manifest, files));
            catalog.packs.Add(manifest);
            Directory.CreateDirectory(SampleFolder);
            File.WriteAllText(Path.Combine(SampleFolder, "catalog.json"), JsonUtility.ToJson(catalog, true));
            foreach (string p in problems)
            {
                Debug.LogError("[Packs] " + p);
            }

            Debug.Log($"[Packs] {catalog.packs.Count} pack(s) in {SampleFolder}; {problems.Count} problem(s)");
            return problems;
        }

        public static void CiBuildSamples() => EditorApplication.Exit(BuildSamples().Count == 0 ? 0 : 1);

        /// <summary>Lints a sealed pack and writes its files and manifest; returns the linter's problems.</summary>
        public static List<string> Write(PackManifest manifest, Dictionary<string, byte[]> files)
        {
            var memory = new MemoryPackFiles();
            foreach (KeyValuePair<string, byte[]> f in files)
            {
                memory.Files[f.Key] = f.Value;
            }

            List<string> problems = PackLint.Check(manifest, memory, new JsonPackReader());
            string folder = Path.Combine(SampleFolder, "packs", manifest.packId, manifest.version);
            Directory.CreateDirectory(folder);
            foreach (KeyValuePair<string, byte[]> f in files)
            {
                string path = Path.Combine(folder, f.Key);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, f.Value);
            }

            File.WriteAllText(Path.Combine(folder, "manifest.json"), JsonUtility.ToJson(manifest, true));
            return problems;
        }

        private static byte[] Preview(EnvironmentDef def)
        {
            Core.Art.PaintCanvas canvas = EnvironmentPreviews.Paint(def.ToInfo(SamplePacks.BlueCityPack, 0f), 256, 144);
            var texture = new Texture2D(canvas.Width, canvas.Height, TextureFormat.RGB24, false);
            var pixels = new Color32[canvas.Width * canvas.Height];
            for (int y = 0; y < canvas.Height; y++)
            {
                for (int x = 0; x < canvas.Width; x++)
                {
                    int i = (y * canvas.Width + x) * 4;
                    pixels[(canvas.Height - 1 - y) * canvas.Width + x] = new Color(canvas.Pixels[i], canvas.Pixels[i + 1], canvas.Pixels[i + 2]);
                }
            }

            texture.SetPixels32(pixels);
            byte[] png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);
            return png;
        }
    }
}

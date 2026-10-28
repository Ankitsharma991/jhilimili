using System.IO;
using UnityEditor;
using UnityEngine;
using UtsavNights.Core.Art;

namespace UtsavNights.EditorTools.Main
{
    /// <summary>
    /// Writes the courtyard's procedural textures (<see cref="EnvironmentPainter"/>, LICENCE_LOG own-006) and the
    /// night-sky skybox material. Called by the Main scene builder; safe to run again (it repaints in place).
    /// </summary>
    public static class EnvironmentArt
    {
        public const string SkyFolder = "Assets/_Game/Art/Sky";
        public const string Folder = "Assets/_Game/Art/Environment";
        public const string SkyTexturePath = SkyFolder + "/NightSky.png";
        public const string StonePath = Folder + "/StoneTiles.png";
        public const string PlasterPath = Folder + "/Plaster.png";
        public const string SkyMaterialPath = "Assets/_Game/Materials/Main/Sky_Night.mat";

        public sealed class Result
        {
            public Material Sky;
            public Texture2D Stone;
            public Texture2D Plaster;
        }

        public static Result Ensure()
        {
            EditorAssetUtil.EnsureFolder(SkyFolder);
            EditorAssetUtil.EnsureFolder(Folder);
            Write(EnvironmentPainter.NightSky(), SkyTexturePath);
            Write(EnvironmentPainter.StoneTiles(), StonePath);
            Write(EnvironmentPainter.Plaster(), PlasterPath);

            // High quality (BC7 / ASTC 4x4): normal compression stored the dark gradient in 5-6-5 bits and showed
            // olive and red rings near the zenith when the camera looks up at a burst.
            Import(SkyTexturePath, 2048, mipmaps: false, TextureWrapMode.Clamp, TextureImporterCompression.CompressedHQ);
            Import(StonePath, 512, mipmaps: true, TextureWrapMode.Repeat);
            Import(PlasterPath, 512, mipmaps: true, TextureWrapMode.Repeat);

            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Panoramic"));
                AssetDatabase.CreateAsset(sky, SkyMaterialPath);
            }

            sky.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(SkyTexturePath));
            sky.SetFloat("_Mapping", 1f); // latitude-longitude layout
            sky.SetFloat("_ImageType", 0f); // 360 degrees
            sky.SetFloat("_Exposure", 1f);
            sky.SetFloat("_Rotation", 0f);
            EditorUtility.SetDirty(sky);
            AssetDatabase.SaveAssets();

            return new Result
            {
                Sky = sky,
                Stone = AssetDatabase.LoadAssetAtPath<Texture2D>(StonePath),
                Plaster = AssetDatabase.LoadAssetAtPath<Texture2D>(PlasterPath),
            };
        }

        private static void Write(PaintCanvas canvas, string path)
        {
            var texture = new Texture2D(canvas.Width, canvas.Height, TextureFormat.RGB24, false);
            var pixels = new Color32[canvas.Width * canvas.Height];
            float[] p = canvas.Pixels;
            for (int y = 0; y < canvas.Height; y++)
            {
                for (int x = 0; x < canvas.Width; x++)
                {
                    int i = (y * canvas.Width + x) * 4;

                    // Canvas rows run top-down; Unity textures bottom-up.
                    pixels[(canvas.Height - 1 - y) * canvas.Width + x] = new Color32(
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(p[i]) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(p[i + 1]) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(p[i + 2]) * 255f), 255);
                }
            }

            texture.SetPixels32(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        private static void Import(string path, int maxSize, bool mipmaps, TextureWrapMode wrap,
            TextureImporterCompression compression = TextureImporterCompression.Compressed)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.maxTextureSize = maxSize;
            importer.mipmapEnabled = mipmaps;
            importer.wrapModeU = TextureWrapMode.Repeat;
            importer.wrapModeV = wrap;
            importer.textureCompression = compression;
            importer.SaveAndReimport();
        }
    }
}

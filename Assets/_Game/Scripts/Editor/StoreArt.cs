using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;
using UtsavNights.Core.Art;

namespace UtsavNights.EditorTools
{
    /// <summary>
    /// Phase 5 step 5: paints the app icon and the Play store art with <see cref="StoreArtPainter"/> (own work,
    /// LICENCE_LOG own-005). The two adaptive icon layers go into Assets/_Game/Art/AppIcon and become the
    /// Android launcher icon. The 512 px Play icon and the 1024 x 500 feature graphic go to
    /// Docs/Phase5/store for upload in Play Console. Batch: -executeMethod UtsavNights.EditorTools.StoreArt.CiGenerate
    /// </summary>
    public static class StoreArt
    {
        public const string IconFolder = "Assets/_Game/Art/AppIcon";
        public const string BackgroundPath = IconFolder + "/AppIcon_Background.png";
        public const string ForegroundPath = IconFolder + "/AppIcon_Foreground.png";
        public const string StoreFolder = "Docs/Phase5/store";
        public const int IconSourceSize = 1024;

        [MenuItem("Utsav/Store/Generate App Icon and Store Art", priority = 70)]
        public static void Generate()
        {
            EditorAssetUtil.EnsureFolder(IconFolder);
            Write(StoreArtPainter.IconBackground(IconSourceSize), BackgroundPath);
            Write(StoreArtPainter.IconForeground(IconSourceSize), ForegroundPath);
            AssetDatabase.ImportAsset(BackgroundPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(ForegroundPath, ImportAssetOptions.ForceSynchronousImport);
            ApplyIcons();

            Directory.CreateDirectory(StoreFolder);
            Write(StoreArtPainter.Icon(512), StoreFolder + "/play-icon-512.png", opaque: true);
            Write(StoreArtPainter.FeatureGraphic(), StoreFolder + "/feature-graphic-1024x500.png", opaque: true);
            Debug.Log($"[StoreArt] app icon layers in {IconFolder}; Play icon and feature graphic in {StoreFolder}");
        }

        public static void CiGenerate()
        {
            Generate();
            AssetDatabase.SaveAssets();
            EditorApplication.Exit(0);
        }

        /// <summary>Every adaptive icon size uses the same 1024 px layers; Unity scales them per density.</summary>
        public static void ApplyIcons()
        {
            var background = AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundPath);
            var foreground = AssetDatabase.LoadAssetAtPath<Texture2D>(ForegroundPath);
            PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, AndroidPlatformIconKind.Adaptive);
            foreach (PlatformIcon icon in icons)
            {
                icon.SetTextures(background, foreground);
            }

            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, AndroidPlatformIconKind.Adaptive, icons);
        }

        /// <summary>Play wants opaque PNGs for the icon and feature graphic (no transparency).</summary>
        private static void Write(PaintCanvas canvas, string path, bool opaque = false)
        {
            var texture = new Texture2D(canvas.Width, canvas.Height, TextureFormat.RGBA32, false);
            var pixels = new Color32[canvas.Width * canvas.Height];
            float[] p = canvas.Pixels;
            for (int y = 0; y < canvas.Height; y++)
            {
                for (int x = 0; x < canvas.Width; x++)
                {
                    int i = (y * canvas.Width + x) * 4;

                    // The canvas holds premultiplied colour (light adds colour and alpha together); PNG wants straight alpha.
                    float a = opaque ? 1f : p[i + 3];
                    float unpremultiply = a > 0.001f ? 1f / a : 0f;

                    // Canvas rows run top-down; Unity textures bottom-up.
                    pixels[(canvas.Height - 1 - y) * canvas.Width + x] = new Color32(
                        ToByte(p[i] * unpremultiply), ToByte(p[i + 1] * unpremultiply), ToByte(p[i + 2] * unpremultiply), ToByte(a));
                }
            }

            texture.SetPixels32(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        private static byte ToByte(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
    }
}

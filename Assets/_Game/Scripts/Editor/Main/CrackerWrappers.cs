using System.IO;
using UnityEditor;
using UnityEngine;
using UtsavNights.Core.Art;

namespace UtsavNights.EditorTools.Main
{
    /// <summary>
    /// Printed-paper wrapper materials for the cracker models (Phase 5 visual pass): paints the pattern with
    /// <see cref="WrapperPainter"/> into Assets/_Game/Art/Wrappers and returns a textured material of the same name.
    /// </summary>
    public static class CrackerWrappers
    {
        public const string Folder = "Assets/_Game/Art/Wrappers";

        public static Material Material(string name, WrapperPattern pattern, Color baseColor, Color accent, Color trim, float smoothness = 0.35f)
        {
            EditorAssetUtil.EnsureFolder(Folder);
            string path = $"{Folder}/{name}.png";
            PaintCanvas canvas = WrapperPainter.Paint(pattern, ToRgb(baseColor), ToRgb(accent), ToRgb(trim));
            var texture = new Texture2D(canvas.Width, canvas.Height, TextureFormat.RGB24, false);
            var pixels = new Color32[canvas.Width * canvas.Height];
            for (int y = 0; y < canvas.Height; y++)
            {
                for (int x = 0; x < canvas.Width; x++)
                {
                    int i = (y * canvas.Width + x) * 4;
                    pixels[(canvas.Height - 1 - y) * canvas.Width + x] = new Color(canvas.Pixels[i], canvas.Pixels[i + 1], canvas.Pixels[i + 2], 1f);
                }
            }

            texture.SetPixels32(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.maxTextureSize = 256;
            importer.mipmapEnabled = true;
            importer.wrapModeU = TextureWrapMode.Repeat;
            importer.wrapModeV = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return SceneKit.TexturedMaterial(name, AssetDatabase.LoadAssetAtPath<Texture2D>(path), Vector2.one, smoothness);
        }

        private static Rgb ToRgb(Color c) => new Rgb(c.r, c.g, c.b);
    }
}

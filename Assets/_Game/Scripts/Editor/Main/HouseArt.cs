using UnityEditor;
using UnityEngine;
using UtsavNights.Core.Art;
using UtsavNights.Core.Home;

namespace UtsavNights.EditorTools.Main
{
    /// <summary>Writes the four house wall textures (<see cref="HouseTexturePainter"/>, LICENCE_LOG own-008). Safe to run again.</summary>
    public static class HouseArt
    {
        public const string Folder = "Assets/_Game/Art/House";

        /// <summary>Textures by <see cref="HomeCatalog.Materials"/> index.</summary>
        public static Texture2D[] Ensure()
        {
            EditorAssetUtil.EnsureFolder(Folder);
            var textures = new Texture2D[HomeCatalog.Materials.Length];
            for (int i = 0; i < textures.Length; i++)
            {
                string path = $"{Folder}/Wall_{HomeCatalog.Materials[i]}.png";
                EnvironmentArt.Write(HouseTexturePainter.Paint(i), path);
                EnvironmentArt.Import(path, 512, mipmaps: true, TextureWrapMode.Repeat);
                textures[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }

            return textures;
        }
    }
}

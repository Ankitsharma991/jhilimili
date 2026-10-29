using UnityEditor;
using UnityEngine;
using UtsavNights.Core.Neighbourhood;

namespace UtsavNights.EditorTools.Main
{
    /// <summary>Writes the environment browser's painted preview cards (<see cref="EnvironmentPreviews"/>, LICENCE_LOG own-009).</summary>
    public static class EnvironmentPreviewArt
    {
        public const string Folder = "Assets/_Game/Art/Environments";

        /// <summary>Preview textures in <see cref="Environments.All"/> order.</summary>
        public static Texture2D[] Ensure()
        {
            EditorAssetUtil.EnsureFolder(Folder);
            var textures = new Texture2D[Environments.All.Length];
            for (int i = 0; i < textures.Length; i++)
            {
                string path = $"{Folder}/Preview_{Environments.All[i].Id}.png";
                EnvironmentArt.Write(EnvironmentPreviews.Paint(Environments.All[i]), path);
                EnvironmentArt.Import(path, 512, mipmaps: false, TextureWrapMode.Clamp);
                textures[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }

            return textures;
        }
    }
}

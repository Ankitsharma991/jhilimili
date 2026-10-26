using UnityEditor;
using UnityEngine;
using UtsavNights.Content;

namespace UtsavNights.EditorTools.Main
{
    public static class CatalogAssets
    {
        public const string CorePath = "Assets/_Game/Definitions/Catalog_Core.asset";

        /// <summary>Loads or creates the core catalog (the tray's crackers, in order).</summary>
        public static CrackerCatalog EnsureCore()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CrackerCatalog>(CorePath);
            if (catalog == null)
            {
                EditorAssetUtil.EnsureFolder("Assets/_Game/Definitions");
                catalog = ScriptableObject.CreateInstance<CrackerCatalog>();
                AssetDatabase.CreateAsset(catalog, CorePath);
            }

            return catalog;
        }
    }
}

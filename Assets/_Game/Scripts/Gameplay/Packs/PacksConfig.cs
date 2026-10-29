using UnityEngine;

namespace UtsavNights.Gameplay
{
    /// <summary>Where the pack catalog lives (PRD 13.4). Empty until the owner hosts packs: the app then shows only built-ins.</summary>
    [CreateAssetMenu(menuName = "Utsav/Packs Config", fileName = "PacksConfig")]
    public sealed class PacksConfig : ScriptableObject
    {
        public const string ResourcePath = "PacksConfig";

        [Tooltip("HTTPS address of the folder holding catalog.json and packs/ (the layout Utsav/Phase 9/Build Sample Packs writes).")]
        public string catalogUrl = string.Empty;

        public static string Url
        {
            get
            {
                PacksConfig config = Resources.Load<PacksConfig>(ResourcePath);
                return config != null ? config.catalogUrl ?? string.Empty : string.Empty;
            }
        }
    }
}

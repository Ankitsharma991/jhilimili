using UnityEngine;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Public links shown in Settings (Resources/AppLinks). The privacy policy URL is required before the
    /// closed test (PRD 16); while it is empty the Settings row stays hidden.
    /// </summary>
    [CreateAssetMenu(menuName = "Utsav/App Links", fileName = "AppLinks")]
    public sealed class AppLinks : ScriptableObject
    {
        public const string ResourceName = "AppLinks";

        [Tooltip("Public privacy policy page, https only (Docs/Phase4/site/privacy-policy.html).")]
        public string privacyPolicyUrl = string.Empty;

        public bool HasPrivacyPolicy => privacyPolicyUrl.StartsWith("https://");

        public static AppLinks Load()
        {
            var links = Resources.Load<AppLinks>(ResourceName);
            return links != null ? links : CreateInstance<AppLinks>();
        }
    }
}

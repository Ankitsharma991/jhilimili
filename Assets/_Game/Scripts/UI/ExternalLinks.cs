using UnityEngine;

namespace UtsavNights.UI
{
    /// <summary>
    /// The only place the game opens a web page (in the phone's browser, outside the app). Kept in its own
    /// file so the offline guard (F09) can allow exactly this and nothing else.
    /// </summary>
    public static class ExternalLinks
    {
        /// <summary>Test hook: the last URL opened, instead of launching a browser in tests.</summary>
        public static string LastOpened { get; private set; }

        public static bool SuppressForTests { get; set; }

        public static void Open(string url)
        {
            if (string.IsNullOrEmpty(url) || !url.StartsWith("https://"))
            {
                return;
            }

            LastOpened = url;
            if (!SuppressForTests)
            {
                Application.OpenURL(url);
            }
        }
    }
}

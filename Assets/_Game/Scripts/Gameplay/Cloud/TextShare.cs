using UnityEngine;

namespace UtsavNights.Gameplay
{
    /// <summary>Sends a line of text (a share code) through Android's share sheet; in the editor it is copied and logged.</summary>
    public static class TextShare
    {
        public static void Share(string text)
        {
            GUIUtility.systemCopyBuffer = text;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var intentClass = new AndroidJavaClass("android.content.Intent");
                using var intent = new AndroidJavaObject("android.content.Intent", intentClass.GetStatic<string>("ACTION_SEND"));
                intent.Call<AndroidJavaObject>("setType", "text/plain");
                intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), text);
                // ACTION_SEND always offers the share sheet; createChooser takes a CharSequence, which JNI lookup by String misses.
                activity.Call("startActivity", intent);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Share] " + e.Message);
            }
#else
            Debug.Log("[Share] " + text);
#endif
        }
    }
}

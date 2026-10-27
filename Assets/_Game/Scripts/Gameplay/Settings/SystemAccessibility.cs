using UnityEngine;

namespace UtsavNights.Gameplay
{
    /// <summary>Reads Android accessibility preferences that should shape first-run defaults.</summary>
    public static class SystemAccessibility
    {
        /// <summary>True when Android "Remove animations" (animator duration scale 0) is on.</summary>
        public static bool AnimationsDisabled()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var global = new AndroidJavaClass("android.provider.Settings$Global"))
                using (AndroidJavaObject resolver = UnityEngine.Android.AndroidApplication.currentContext.Call<AndroidJavaObject>("getContentResolver"))
                {
                    return global.CallStatic<float>("getFloat", resolver, "animator_duration_scale", 1f) == 0f;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Settings] could not read animation scale: " + e.Message);
                return false;
            }
#else
            return false;
#endif
        }
    }
}

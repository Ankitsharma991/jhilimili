using System;
using UnityEngine;
using UtsavNights.Core.Clips;

namespace UtsavNights.Gameplay
{
    /// <summary>A clip was shared (Phase 9 step 7 counts these as clip_shared).</summary>
    public readonly struct ClipSharedEvent
    {
        public ClipSharedEvent(ShareTarget target, float seconds, string source, bool fellBack)
        {
            Target = target;
            Seconds = seconds;
            Source = source;
            FellBack = fellBack;
        }

        public ShareTarget Target { get; }

        public float Seconds { get; }

        public string Source { get; }

        /// <summary>The chosen app wasn't installed, so the share sheet opened instead.</summary>
        public bool FellBack { get; }
    }

    /// <summary>
    /// Sends a clip to WhatsApp, Instagram or the share sheet (Phase 9 step 6, F14) through the app's FileProvider. In the
    /// editor nothing is sent: the clip's path is logged.
    /// </summary>
    public static class ClipSharer
    {
        public static bool IsInstalled(ShareTarget target)
        {
            if (target == ShareTarget.Other)
            {
                return true;
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var share = new AndroidJavaClass("com.ankitsharma.utsavnights.clips.ClipShare");
                return share.CallStatic<bool>("isInstalled", activity, ClipRules.Packages[target]);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Clips] {e.Message}");
                return false;
            }
#else
            return false;
#endif
        }

        /// <summary>Shares the clip. Returns empty on success, else what went wrong in plain words.</summary>
        public static string Share(ClipResult clip, ShareTarget target)
        {
            if (!clip.Ok || string.IsNullOrEmpty(clip.Path) || !System.IO.File.Exists(clip.Path))
            {
                return "The clip isn't on the phone any more.";
            }

            string result;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var share = new AndroidJavaClass("com.ankitsharma.utsavnights.clips.ClipShare");
                result = share.CallStatic<string>("share", activity, clip.Path, ClipRules.Packages[target], ClipRules.ShareText);
            }
            catch (Exception e)
            {
                result = e.Message;
            }
#else
            Debug.Log($"[Clips] share to {target}: {clip.Path}");
            result = "ok";
#endif
            bool fellBack = result == "fallback";
            if (result != "ok" && !fellBack)
            {
                return "Couldn't open sharing: " + result;
            }

            Core.Runtime.Game.Events.Publish(new ClipSharedEvent(target, clip.Seconds, clip.Source, fellBack));
            return string.Empty;
        }
    }
}

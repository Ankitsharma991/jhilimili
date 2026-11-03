using System;
using System.Globalization;
using UnityEngine;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Runtime;

namespace UtsavNights.Gameplay
{
    /// <summary>A photo suggestion was used or failed (analytics in step 5: outcomes only, never anything of the photo).</summary>
    public readonly struct PhotoAvatarEvent
    {
        public PhotoAvatarEvent(string outcome, bool camera)
        {
            Outcome = outcome;
            Camera = camera;
        }

        /// <summary>"suggested", "noface", "cancelled" or "error".</summary>
        public string Outcome { get; }

        public bool Camera { get; }
    }

    /// <summary>
    /// The photo-assisted avatar (Phase 11 step 3, F26, PRD 8 selfie privacy). Only for players who confirmed they are 18
    /// or older. The photo comes from the camera app or the system picker (no permission), is read into a small copy in
    /// memory, the camera's file is deleted at once, the face is found and sampled on the phone, and the pixels are cleared
    /// before the suggestion comes back. Nothing of the photo is stored, sent or logged; only the suggested skin tone, hair
    /// colour and face shape go into the creator, where the player can change them like any choice.
    /// </summary>
    public static class PhotoAvatar
    {
        public const string AgeKey = "utsav.photo.age";

        /// <summary>Editor and tests: the "photo" a request analyses (RGB bytes, size and the face), or null to cancel.</summary>
        public static Func<(byte[] rgb, int width, int height, FaceBox face)?> EditorPhoto;

        public enum AgeAnswer
        {
            Unknown = 0,
            Adult = 1,
            Minor = 2,
        }

        public static AgeAnswer Age
        {
            get => (AgeAnswer)PlayerPrefs.GetInt(AgeKey, 0);
            set
            {
                PlayerPrefs.SetInt(AgeKey, (int)value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Whether the feature may be offered: never to someone who said they're under 18.</summary>
        public static bool Offered => Age != AgeAnswer.Minor;

        /// <summary>Asks for a photo and calls back with the suggestion, or null and the reason in plain words (null reason: cancelled).</summary>
        public static void Request(bool camera, Action<PhotoSuggestion?, string> done)
        {
            if (Age != AgeAnswer.Adult)
            {
                done(null, "This is for players 18 and over.");
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            PhotoAvatarReceiver.Expect(result => Finish(result, camera, done));
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            using var photo = new AndroidJavaClass("com.ankitsharma.utsavnights.photo.PhotoFaceActivity");
            photo.CallStatic("start", activity, camera, PhotoAvatarReceiver.ObjectName, nameof(PhotoAvatarReceiver.OnPhotoResult));
#else
            var picked = EditorPhoto?.Invoke();
            if (picked == null)
            {
                Report("cancelled", camera);
                done(null, null);
                return;
            }

            (byte[] rgb, int w, int h, FaceBox face) = picked.Value;
            Deliver(PhotoAnalysis.Analyse(rgb, w, h, face), rgb, camera, done);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void Finish(string result, bool camera, Action<PhotoSuggestion?, string> done)
        {
            if (result == "cancelled")
            {
                Report("cancelled", camera);
                done(null, null);
                return;
            }

            if (result == "noface")
            {
                Report("noface", camera);
                done(null, NoFace);
                return;
            }

            if (!result.StartsWith("ok:", StringComparison.Ordinal))
            {
                Report("error", camera);
                done(null, "The photo couldn't be used: " + result.Substring(Math.Min(6, result.Length)));
                return;
            }

            string[] f = result.Substring(3).Split(',');
            byte[] rgb;
            using (var photo = new AndroidJavaClass("com.ankitsharma.utsavnights.photo.PhotoFaceActivity"))
            {
                rgb = photo.CallStatic<byte[]>("takePixels");
            }

            int w = int.Parse(f[0], CultureInfo.InvariantCulture), h = int.Parse(f[1], CultureInfo.InvariantCulture);
            var face = new FaceBox(float.Parse(f[2], CultureInfo.InvariantCulture), float.Parse(f[3], CultureInfo.InvariantCulture), float.Parse(f[4], CultureInfo.InvariantCulture));
            Deliver(PhotoAnalysis.Analyse(rgb, w, h, face), rgb, camera, done);
        }
#endif

        public const string NoFace = "No face could be seen. Try a clear photo facing the camera in good light, or pick a ready-made look.";

        private static void Deliver(PhotoSuggestion? suggestion, byte[] rgb, bool camera, Action<PhotoSuggestion?, string> done)
        {
            if (rgb != null)
            {
                Array.Clear(rgb, 0, rgb.Length); // the photo's pixels go now, before anything else happens
            }

            Report(suggestion.HasValue ? "suggested" : "noface", camera);
            done(suggestion, suggestion.HasValue ? null : NoFace);
        }

        private static void Report(string outcome, bool camera) => Game.Events.Publish(new PhotoAvatarEvent(outcome, camera));
    }
}

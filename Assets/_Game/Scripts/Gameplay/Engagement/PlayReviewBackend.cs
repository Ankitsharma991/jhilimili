using System;
using UnityEngine;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Google Play In-App Review through JNI (com.google.android.play:review, PlayReviewDependencies.xml):
    /// requestReviewFlow, then launchReviewFlow. Play decides whether a dialog actually appears (quota,
    /// already rated, app not installed from Play) and reports success either way. Play's callbacks arrive
    /// on the Android UI thread and are passed back to Unity's main thread.
    /// </summary>
    public sealed class PlayReviewBackend : IReviewBackend
    {
        public string Name => "google-play";

        public void Request(Action<bool> done)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                AndroidJavaObject activity = UnityEngine.Android.AndroidApplication.currentActivity;
                AndroidJavaObject manager;
                using (var factory = new AndroidJavaClass("com.google.android.play.core.review.ReviewManagerFactory"))
                {
                    manager = factory.CallStatic<AndroidJavaObject>("create", activity);
                }

                AndroidJavaObject request = manager.Call<AndroidJavaObject>("requestReviewFlow");
                request.Call<AndroidJavaObject>("addOnCompleteListener", new CompleteListener(task =>
                {
                    if (!task.Call<bool>("isSuccessful"))
                    {
                        Finish(done, false, "request failed");
                        return;
                    }

                    AndroidJavaObject info = task.Call<AndroidJavaObject>("getResult");
                    AndroidJavaObject flow = manager.Call<AndroidJavaObject>("launchReviewFlow", activity, info);
                    flow.Call<AndroidJavaObject>("addOnCompleteListener", new CompleteListener(_ => Finish(done, true, null)));
                }));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Review] Play review unavailable: {e.Message}");
                done?.Invoke(false);
            }
#else
            done?.Invoke(false);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void Finish(Action<bool> done, bool ran, string error)
        {
            UnityEngine.Android.AndroidApplication.InvokeOnUnityMainThread(() =>
            {
                if (error != null)
                {
                    Debug.LogWarning($"[Review] {error}");
                }

                done?.Invoke(ran);
            });
        }

        private sealed class CompleteListener : AndroidJavaProxy
        {
            private readonly Action<AndroidJavaObject> _onComplete;

            public CompleteListener(Action<AndroidJavaObject> onComplete) : base("com.google.android.gms.tasks.OnCompleteListener")
            {
                _onComplete = onComplete;
            }

            // Called by Play's Task with the finished task.
            public void onComplete(AndroidJavaObject task) => _onComplete(task);
        }
#endif
    }
}

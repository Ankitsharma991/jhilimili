using System.Threading.Tasks;
using UnityEngine;

namespace UtsavNights.FirebaseCloud
{
    /// <summary>
    /// Google sign-in through Android's Credential Manager (GoogleSignIn.java in the UtsavSignIn library). Asks only for
    /// a Google ID token for Firebase Auth; the game never reads the name, email or picture. UnitySendMessage needs a
    /// GameObject by name, so this one lives for the whole run.
    /// </summary>
    public sealed class GoogleSignInBridge : MonoBehaviour
    {
        public const string ObjectName = "UtsavGoogleSignIn";

        private static GoogleSignInBridge _instance;
        private TaskCompletionSource<string> _pending;

        /// <summary>"ok:&lt;id token&gt;", "cancelled" or "error:&lt;message&gt;".</summary>
        public static Task<string> RequestIdToken()
        {
            Ensure();
            _instance._pending?.TrySetResult("cancelled");
            _instance._pending = new TaskCompletionSource<string>();
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var signIn = new AndroidJavaClass("com.ankitsharma.utsavnights.signin.GoogleSignIn"))
            {
                signIn.CallStatic("signIn", activity, ObjectName, nameof(OnSignInResult));
            }
#else
            _instance._pending.TrySetResult("error:Google sign-in runs on the phone only");
#endif
            return _instance._pending.Task;
        }

        /// <summary>Forgets the chosen account in Credential Manager, so the next sign-in asks again.</summary>
        public static void ClearState()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            using var signIn = new AndroidJavaClass("com.ankitsharma.utsavnights.signin.GoogleSignIn");
            signIn.CallStatic("signOut", activity);
#endif
        }

        public void OnSignInResult(string result)
        {
            TaskCompletionSource<string> pending = _pending;
            _pending = null;
            pending?.TrySetResult(result ?? "cancelled");
        }

        private static void Ensure()
        {
            if (_instance != null)
            {
                return;
            }

            var go = new GameObject(ObjectName);
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<GoogleSignInBridge>();
        }
    }
}

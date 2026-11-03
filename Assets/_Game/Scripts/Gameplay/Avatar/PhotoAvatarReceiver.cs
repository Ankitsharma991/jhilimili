using System;
using UnityEngine;

namespace UtsavNights.Gameplay
{
    /// <summary>Receives the photo step's answer (UnitySendMessage needs a GameObject by name).</summary>
    public sealed class PhotoAvatarReceiver : MonoBehaviour
    {
        public const string ObjectName = "UtsavPhotoAvatar";

        private static PhotoAvatarReceiver _instance;
        private Action<string> _pending;

        public static void Expect(Action<string> callback)
        {
            if (_instance == null)
            {
                var go = new GameObject(ObjectName);
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<PhotoAvatarReceiver>();
            }

            _instance._pending = callback;
        }

        public void OnPhotoResult(string result)
        {
            Action<string> callback = _pending;
            _pending = null;
            callback?.Invoke(result ?? "cancelled");
        }
    }
}

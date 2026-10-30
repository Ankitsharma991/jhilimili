using System;
using UnityEngine;

namespace UtsavNights.Gameplay
{
    /// <summary>Receives the file picker's answer (UnitySendMessage needs a GameObject by name) and passes it to the waiting call.</summary>
    public sealed class BackupFileReceiver : MonoBehaviour
    {
        public const string ObjectName = "UtsavBackupFiles";

        private static BackupFileReceiver _instance;
        private Action<string> _pending;

        public static void Expect(Action<string> callback)
        {
            if (_instance == null)
            {
                var go = new GameObject(ObjectName);
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<BackupFileReceiver>();
            }

            _instance._pending = callback;
        }

        /// <summary>Called by BackupFileActivity: "ok:path", "cancelled" or "error:message".</summary>
        public void OnFileResult(string result)
        {
            Action<string> callback = _pending;
            _pending = null;
            callback?.Invoke(result ?? "cancelled");
        }
    }
}

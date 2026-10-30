using UnityEngine;
using UtsavNights.Core.Cloud;
using UtsavNights.Core.Runtime;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Registers the <see cref="CloudService"/> (Phase 10 step 3). Firebase when its package is installed (step 6);
    /// otherwise the memory cloud in the editor and development builds (one server for the whole run, so sign-in and
    /// backups outlive a restore's scene reload), and in a release build an honest "not switched on yet".
    /// </summary>
    [DefaultExecutionOrder(-127)]
    public sealed class CloudInstaller : MonoBehaviour
    {
        private static ICloud _shared;
        private bool _owner;

        /// <summary>Tests: the memory server behind the editor's cloud (null when another cloud is in use).</summary>
        public static MemoryCloudServer EditorServer { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _shared = null;
            EditorServer = null;
        }

        private void Awake()
        {
            if (Game.Services.IsRegistered<CloudService>())
            {
                return;
            }

            _owner = true;
            _shared ??= Create();
            Game.Services.Register(new CloudService(_shared, new BackupService()));
            Debug.Log($"[Cloud] backend={_shared.Name}");
        }

        private void OnDestroy()
        {
            if (_owner)
            {
                Game.Services.Unregister<CloudService>();
            }
        }

        private static ICloud Create()
        {
            if (MonetisationProviders.Cloud != null)
            {
                return MonetisationProviders.Cloud();
            }

            if (Application.isEditor || Debug.isDebugBuild)
            {
                EditorServer = new MemoryCloudServer();
                return new MemoryCloud(EditorServer, "dev-player");
            }

            return new UnconfiguredCloud();
        }
    }
}

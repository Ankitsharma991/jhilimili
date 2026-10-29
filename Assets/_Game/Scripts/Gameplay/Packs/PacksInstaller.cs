using System.IO;
using UnityEngine;
using UtsavNights.Core.Packs;
using UtsavNights.Core.Runtime;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Creates the <see cref="PackService"/> before the neighbourhood is built (so an installed environment the player chose
    /// last time is ready), and hands finished downloads back each frame. In its own file, as Unity requires.
    /// </summary>
    [DefaultExecutionOrder(-128)]
    public sealed class PacksInstaller : MonoBehaviour
    {
        private bool _owner;
        private PackService _service;

        public static string StorageRoot => Application.persistentDataPath;

        private void Awake()
        {
            if (Game.Services.IsRegistered<PackService>())
            {
                return;
            }

            _owner = true;
            string url = PacksConfig.Url;
            IPackTransport transport = string.IsNullOrEmpty(url) ? null : new HttpPackTransport(url);
            _service = new PackService(StorageRoot, transport);
            Game.Services.Register(_service);
            if (transport != null)
            {
                _ = _service.RefreshCatalog();
            }
        }

        private void Update() => _service?.Poll();

        private void OnDestroy()
        {
            if (_owner)
            {
                Game.Services.Unregister<PackService>();
            }
        }
    }
}

using UnityEngine;
using UtsavNights.Core.Runtime;

namespace UtsavNights.Gameplay
{
    /// <summary>Registers the <see cref="PlayerService"/> (Phase 12 step 2). In its own file, as Unity requires.</summary>
    [DefaultExecutionOrder(-126)]
    public sealed class PlayerInstaller : MonoBehaviour
    {
        private PlayerService _service;

        private void Awake()
        {
            if (Game.Services.IsRegistered<PlayerService>())
            {
                return;
            }

            _service = new PlayerService();
            Game.Services.Register(_service);
            Game.Loop.Add(_service);
        }

        private void OnDestroy()
        {
            if (_service == null)
            {
                return;
            }

            Game.Loop.Remove(_service);
            _service.Dispose();
            Game.Services.Unregister<PlayerService>();
        }
    }
}

using UnityEngine;
using UtsavNights.Core.Runtime;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Creates the <see cref="ProfileService"/> for the scene. In its own file: Unity stores a scene component by its script
    /// file, and a component sharing a file under another name was saved as an embedded script that did not load.
    /// </summary>
    [DefaultExecutionOrder(-130)]
    public sealed class AvatarInstaller : MonoBehaviour
    {
        private bool _owner;

        private void Awake()
        {
            if (Game.Services.IsRegistered<ProfileService>())
            {
                return;
            }

            _owner = true;
            Game.Services.Register(new ProfileService(new ProfileStore()));
        }

        private void OnDestroy()
        {
            if (_owner)
            {
                Game.Services.Unregister<ProfileService>();
            }
        }
    }
}

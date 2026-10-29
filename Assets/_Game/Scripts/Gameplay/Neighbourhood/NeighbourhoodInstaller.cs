using UnityEngine;
using UtsavNights.Core.Neighbourhood;
using UtsavNights.Core.Runtime;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Builds the settlement around the player's house and registers its <see cref="SettlementView"/> (Phase 8). The
    /// environment is the player's last choice (the environment browser, step 6), the town lane at first. In its own
    /// file, as Unity requires for scene components (see <see cref="AvatarInstaller"/>).
    /// </summary>
    [DefaultExecutionOrder(-120)]
    public sealed class NeighbourhoodInstaller : MonoBehaviour
    {
        public const string EnvironmentPrefKey = "neighbourhood.environment";

        [SerializeField] private Texture2D[] wallTextures = new Texture2D[0];
        [SerializeField] private Texture2D groundTexture;

        private bool _owner;

        public void Configure(Texture2D[] walls, Texture2D ground)
        {
            wallTextures = walls;
            groundTexture = ground;
        }

        private void Awake()
        {
            if (Game.Services.IsRegistered<SettlementView>())
            {
                return;
            }

            _owner = true;
            QualityManager quality = FindFirstObjectByType<QualityManager>();
            string environment = PlayerPrefs.GetString(EnvironmentPrefKey, Environments.TownLane);
            SettlementView view = SettlementView.Create(transform, environment, wallTextures, groundTexture, () => quality != null ? quality.Tier : 1);
            Game.Services.Register(view);
        }

        private void OnDestroy()
        {
            if (_owner)
            {
                Game.Services.Unregister<SettlementView>();
            }
        }
    }
}

using UnityEngine;
using UtsavNights.Core.Home;
using UtsavNights.Core.Runtime;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Builds the player's house, its decorations and its colliders in the scene and registers the <see cref="HomeService"/>.
    /// In its own file, as Unity requires for scene components (see <see cref="AvatarInstaller"/>).
    /// </summary>
    [DefaultExecutionOrder(-125)]
    public sealed class HomeInstaller : MonoBehaviour
    {
        [SerializeField] private Texture2D[] wallTextures = new Texture2D[0];

        private bool _owner;
        private HouseView _house;
        private DecorView _decor;
        private Transform _colliders;

        public void Configure(Texture2D[] textures) => wallTextures = textures;

        private void Awake()
        {
            if (Game.Services.IsRegistered<HomeService>())
            {
                return;
            }

            _owner = true;
            _house = HouseView.Create(transform, wallTextures);
            _decor = DecorView.Create(transform);
            _colliders = new GameObject("HouseColliders").transform;
            _colliders.SetParent(transform, false);
            _colliders.localPosition = new Vector3(0f, 0f, HouseGeometry.WorldFrontZ);
            QualityManager quality = FindFirstObjectByType<QualityManager>();
            Game.Services.Register(new HomeService(new HouseStore(), _house, _decor, () => quality != null ? quality.Tier : 1, _colliders));

            // The porch lamp and facade uplight only matter when the camera faces the house (decoration mode). In the
            // fireworks view they would light nothing visible and take the low tier's two per-object lights from crackers.
            _house.SetLightsEnabled(false);
        }

        private void OnDestroy()
        {
            if (_owner)
            {
                Game.Services.Unregister<HomeService>();
            }
        }
    }
}

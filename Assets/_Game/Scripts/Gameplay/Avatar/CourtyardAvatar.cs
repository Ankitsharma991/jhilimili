using UnityEngine;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Quality;
using UtsavNights.Core.Runtime;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// The player's avatar in the courtyard (Phase 6 step 6). Appears once the player has saved an avatar, so the R0 courtyard
    /// stays as it was for everyone who never opens the creator. Stands between the diyas and the step, crouches with the
    /// ignite gesture when a cracker is lit, and turns to celebrate when a burst starts. A capsule keeps crackers off its spot.
    /// Low tier limits skinning to two bones per vertex, the least that keeps long hems whole (PRD 8 asks for rigid skinning there).
    /// </summary>
    public sealed class CourtyardAvatar : MonoBehaviour
    {
        // On the floor just in front of the step (which starts at z 2.25) and behind the diyas, inside the portrait frame.
        public static readonly Vector3 Spot = new Vector3(-0.7f, 0f, 2.12f);

        /// <summary>Facing the camera and a little towards the play area.</summary>
        public const float RestYaw = 160f;

        public const float CelebrateCooldown = 2.5f;
        private const float TurnSpeed = 240f;
        private const float LookHoldSeconds = 3f;

        private ProfileService _profiles;
        private float _yaw = RestYaw;
        private float _targetYaw = RestYaw;
        private float _lookUntil;
        private float _lastCelebrate = float.NegativeInfinity;
        private int _tier = TierDetection.Mid;

        public AvatarView View { get; private set; }

        public AvatarAnimator Animator { get; private set; }

        public bool Visible => View != null && View.gameObject.activeSelf;

        private void Start()
        {
            if (Game.Services.TryGet(out _profiles))
            {
                _profiles.Changed += Refresh;
            }

            if (Game.Services.TryGet(out QualityManager quality))
            {
                _tier = quality.Tier;
            }

            Game.Events.Subscribe<CrackerIgnitedEvent>(OnIgnited);
            Game.Events.Subscribe<PayoffStartedEvent>(OnPayoff);
            Game.Events.Subscribe<QualityChangedEvent>(OnQuality);
            Refresh();
        }

        private void OnDestroy()
        {
            if (_profiles != null)
            {
                _profiles.Changed -= Refresh;
            }

            Game.Events.Unsubscribe<CrackerIgnitedEvent>(OnIgnited);
            Game.Events.Unsubscribe<PayoffStartedEvent>(OnPayoff);
            Game.Events.Unsubscribe<QualityChangedEvent>(OnQuality);
        }

        /// <summary>Shows, updates or hides the avatar to match the saved profile.</summary>
        public void Refresh()
        {
            AvatarProfile profile = _profiles?.Current;
            if (profile == null || !profile.created)
            {
                if (View != null)
                {
                    View.gameObject.SetActive(false);
                }

                return;
            }

            if (View == null)
            {
                View = AvatarView.Create(transform, profile, "CourtyardAvatar");
                View.transform.position = Spot;
                Animator = View.gameObject.AddComponent<AvatarAnimator>();
                var blocker = View.gameObject.AddComponent<CapsuleCollider>();
                blocker.center = new Vector3(0f, 0.85f, 0f);
                blocker.height = 1.7f;
                blocker.radius = 0.28f;
            }
            else
            {
                View.Apply(profile);
            }

            View.gameObject.SetActive(true);
            ApplyTier();
            View.transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        }

        private void OnIgnited(CrackerIgnitedEvent e)
        {
            if (Visible)
            {
                Animator.Play(AvatarClip.Ignite);
            }
        }

        private void OnPayoff(PayoffStartedEvent e)
        {
            if (!Visible)
            {
                return;
            }

            // Face the burst for a moment, then turn back.
            Vector3 to = e.Position - View.transform.position;
            if (to.sqrMagnitude > 0.01f)
            {
                _targetYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                _lookUntil = Time.time + LookHoldSeconds;
            }

            if (Time.time - _lastCelebrate >= CelebrateCooldown && Animator.Clip != AvatarClip.Ignite)
            {
                _lastCelebrate = Time.time;
                Animator.Play(AvatarClip.Celebrate);
            }
        }

        private void OnQuality(QualityChangedEvent e)
        {
            _tier = e.Tier;
            ApplyTier();
        }

        private void ApplyTier()
        {
            if (View == null)
            {
                return;
            }

            // Two bones, not one, on low tier: long hems blend both thighs and would tear down the middle with one.
            SkinQuality quality = _tier <= TierDetection.Low ? SkinQuality.Bone2 : SkinQuality.Auto;
            View.Body.quality = quality;
            foreach (Renderer r in View.GarmentRenderers)
            {
                if (r is SkinnedMeshRenderer skinned)
                {
                    skinned.quality = quality;
                }
            }
        }

        private void Update()
        {
            if (!Visible)
            {
                return;
            }

            if (Time.time > _lookUntil)
            {
                _targetYaw = RestYaw;
            }

            _yaw = Mathf.MoveTowardsAngle(_yaw, _targetYaw, TurnSpeed * Time.deltaTime);
            View.transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        }
    }
}

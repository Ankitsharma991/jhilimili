using UnityEngine;
using UtsavNights.Core.CameraRig;
using UtsavNights.Core.Runtime;
using UtsavNights.Core.Services;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Small custom camera rig (PRD 6.2, 13.1 "Cinemachine or a small custom rig"):
    /// a ground shot (low three-quarter view), an aerial follow that eases up toward a burst over 2 s,
    /// holds and returns (a new touch cancels it), a small push-in at ignition, and a 100 to 150 ms shake
    /// on big bangs that is off in Quiet mode and with reduce-motion. Ticked by the central loop.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class CameraRig : MonoBehaviour, ITickable
    {
        [SerializeField] private Camera targetCamera;

        [Tooltip("Pose of the default ground shot. Created from the camera's start pose if empty.")]
        [SerializeField] private Transform groundShot;

        [Header("Aerial follow")]
        [SerializeField] private float aerialRiseSeconds = 2f;
        [SerializeField] private float aerialHoldSeconds = 1.5f;
        [SerializeField] private float aerialReturnSeconds = 1.2f;
        [SerializeField] private float aerialLift = 0.6f;
        [SerializeField] private bool cancelAerialOnTouch = true;

        [Header("Push-in")]
        [SerializeField] private float pushInDistance = 0.35f;
        [SerializeField] private float pushInSeconds = 0.9f;

        [Header("Shake")]
        [Tooltip("Metres of offset at full strength.")]
        [SerializeField] private float shakeAmplitude = 0.05f;
        [SerializeField] private float shakeFrequency = 28f;
        [SerializeField] private float shakeSeconds = 0.13f;

        private readonly AerialMove _aerial = new AerialMove();
        private readonly PushIn _push = new PushIn();
        private readonly ShakeEnvelope _shake = new ShakeEnvelope();
        private Vector3 _aerialFocus;
        private Vector3 _pushTarget;
        private float _time;
        private bool _reduceMotion;
        private bool _quietMode;
        private bool _registered;
        private bool _overrideOn;
        private float _overrideWeight;
        private Vector3 _overridePosition;
        private Quaternion _overrideRotation = Quaternion.identity;

        /// <summary>Seconds the camera takes to turn to an override pose (decoration mode) and back.</summary>
        public const float OverrideSeconds = 0.8f;

        public bool IsAerialActive => _aerial.IsActive;

        /// <summary>0 on the ground shot, 1 fully on the override pose (eased).</summary>
        public float OverrideWeight => _overrideWeight;

        public bool IsOverridden => _overrideOn;

        /// <summary>
        /// Hands the camera to another view (decoration mode turns it to the house). The camera eases over
        /// <see cref="OverrideSeconds"/>, or cuts with reduce-motion. Aerial follow is cancelled.
        /// </summary>
        public void BeginOverride(Vector3 position, Quaternion rotation)
        {
            _overrideOn = true;
            _aerial.Cancel();
            SetOverridePose(position, rotation);
            if (_reduceMotion)
            {
                _overrideWeight = 1f;
            }
        }

        public void SetOverridePose(Vector3 position, Quaternion rotation)
        {
            _overridePosition = position;
            _overrideRotation = rotation;
        }

        /// <summary>Eases back to the ground shot.</summary>
        public void EndOverride()
        {
            _overrideOn = false;
            if (_reduceMotion)
            {
                _overrideWeight = 0f;
            }
        }

        public float AerialWeight => _aerial.Weight;

        public float ShakeStrength => _shake.Strength;

        /// <summary>Accessibility: disables shake (PRD 14: reduced-motion preference).</summary>
        public bool ReduceMotion
        {
            get => _reduceMotion;
            set
            {
                _reduceMotion = value;
                _shake.Allowed = !_reduceMotion && !_quietMode;
            }
        }

        /// <summary>Quiet mode also disables shake (PRD 6.2).</summary>
        public bool QuietMode
        {
            get => _quietMode;
            set
            {
                _quietMode = value;
                _shake.Allowed = !_reduceMotion && !_quietMode;
            }
        }

        public void Configure(Camera cameraToDrive, Transform groundShotPose)
        {
            targetCamera = cameraToDrive;
            groundShot = groundShotPose;
        }

        private void Awake()
        {
            TouchInput.EnsureEnabled();
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (groundShot == null && targetCamera != null)
            {
                var pose = new GameObject("GroundShot").transform;
                pose.SetParent(transform, false);
                pose.SetPositionAndRotation(targetCamera.transform.position, targetCamera.transform.rotation);
                groundShot = pose;
            }

            if (!Game.Services.IsRegistered<CameraRig>())
            {
                Game.Services.Register(this);
                _registered = true;
            }
        }

        private void OnEnable() => Game.Loop.Add(this);

        private void OnDisable() => Game.Loop.Remove(this);

        private void OnDestroy()
        {
            if (_registered)
            {
                Game.Services.Unregister<CameraRig>();
            }
        }

        /// <summary>Ease up toward an aerial burst, hold, then return (rockets, skyshots, cakes).</summary>
        /// <summary>Moves the fireworks view (Phase 8: the celebration spot). Ease-free: the caller fades or is overridden.</summary>
        public void SetGroundShot(Vector3 position, Quaternion rotation)
        {
            if (groundShot != null)
            {
                groundShot.SetPositionAndRotation(position, rotation);
            }
        }

        public Transform GroundShot => groundShot;

        public void FollowAerial(Vector3 focus)
        {
            _aerialFocus = focus;
            _aerial.Start(aerialRiseSeconds, aerialHoldSeconds, aerialReturnSeconds);
        }

        public void CancelAerial() => _aerial.Cancel();

        /// <summary>Small move toward a ground cracker at ignition or payoff.</summary>
        public void PushInToward(Vector3 target)
        {
            // Reduce motion also drops this small camera move (PRD 14 reduced-motion preference).
            if (_reduceMotion)
            {
                return;
            }

            _pushTarget = target;
            _push.Start(pushInSeconds);
        }

        /// <param name="strength01">0..1 relative to the configured amplitude.</param>
        /// <returns>False when shake is disabled by Quiet mode or reduce-motion.</returns>
        public bool Shake(float strength01) => _shake.Start(Mathf.Clamp01(strength01) * shakeAmplitude, shakeSeconds);

        public void Tick(float deltaSeconds)
        {
            if (targetCamera == null || groundShot == null)
            {
                return;
            }

            _time += deltaSeconds;
            if (cancelAerialOnTouch && _aerial.IsActive && !_aerial.IsReturning && AnyTouchBegan())
            {
                _aerial.Cancel();
            }

            _aerial.Tick(deltaSeconds);
            _push.Tick(deltaSeconds);
            _shake.Tick(deltaSeconds);

            Vector3 position = groundShot.position;
            Quaternion rotation = groundShot.rotation;

            float aerial = _aerial.Weight;
            if (aerial > 0f)
            {
                position += Vector3.up * (aerialLift * aerial);
                Vector3 toFocus = _aerialFocus - position;
                if (toFocus.sqrMagnitude > 0.0001f)
                {
                    rotation = Quaternion.Slerp(rotation, Quaternion.LookRotation(toFocus, Vector3.up), aerial);
                }
            }

            float push = _push.Weight;
            if (push > 0f)
            {
                Vector3 toTarget = _pushTarget - position;
                float distance = toTarget.magnitude;
                if (distance > 0.001f)
                {
                    position += toTarget / distance * Mathf.Min(pushInDistance, distance * 0.25f) * push;
                }
            }

            float step = deltaSeconds / OverrideSeconds;
            _overrideWeight = Mathf.Clamp01(_overrideWeight + (_overrideOn ? step : -step));
            if (_overrideWeight > 0f)
            {
                // Swing through a point above the courtyard, so the turn to the house never passes through a wall.
                float t = Mathf.SmoothStep(0f, 1f, _overrideWeight);
                Vector3 mid = (position + _overridePosition) * 0.5f + Vector3.up * 0.6f * Mathf.Sin(t * Mathf.PI);
                position = Vector3.Lerp(Vector3.Lerp(position, mid, t), Vector3.Lerp(mid, _overridePosition, t), t);
                rotation = Quaternion.Slerp(rotation, _overrideRotation, t);
            }

            float shake = _shake.Strength;
            if (shake > 0f)
            {
                float x = Mathf.PerlinNoise(_time * shakeFrequency, 0.37f) * 2f - 1f;
                float y = Mathf.PerlinNoise(0.71f, _time * shakeFrequency) * 2f - 1f;
                position += rotation * new Vector3(x, y, 0f) * shake;
            }

            targetCamera.transform.SetPositionAndRotation(position, rotation);
        }

        private static bool AnyTouchBegan()
        {
            var touches = ETouch.activeTouches;
            for (int i = 0; i < touches.Count; i++)
            {
                if (touches[i].phase == TouchPhase.Began)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

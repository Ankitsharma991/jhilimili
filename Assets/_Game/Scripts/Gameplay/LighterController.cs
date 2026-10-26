using UnityEngine;
using UtsavNights.Audio;
using UtsavNights.Content;
using UtsavNights.Core;
using UtsavNights.Core.Ignition;
using UtsavNights.Core.Runtime;
using UtsavNights.Core.Services;
using UtsavNights.Haptics;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// The player's lighter (F02). Follows one finger with the flame offset above the touch point so the
    /// finger never hides the fuse (PRD 6.3), finds the nearest ignitable fuse in screen space, and lets
    /// <see cref="LighterModel"/> decide ignition and flame-out. Ticked by the central loop.
    /// </summary>
    [DefaultExecutionOrder(-300)]
    public sealed class LighterController : MonoBehaviour, ITickable
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform lighter;
        [SerializeField] private GameObject flame;

        [Tooltip("How far above the finger the flame sits, in dp.")]
        [SerializeField] private float fingerOffsetDp = 64f;

        [Tooltip("Radius around a fuse, in dp, where the flame counts as touching it (48 dp target = 24; generous by default).")]
        [SerializeField] private float fuseRadiusDp = 36f;

        [Tooltip("Distance from the camera used before any fuse has been approached, in metres.")]
        [SerializeField] private float idleDepth = 6f;

        [SerializeField] private AudioClip lightClip;
        [SerializeField] private AudioClip flameOutClip;

        private LighterModel _model;
        private IgnitionTargets _targets;
        private AudioService _audio;
        private HapticsService _haptics;
        private PlacementController _placement;
        private int _touchId = -1;
        private float _depth;

        public LighterModel Model => _model;

        public IgnitionTargets Targets => _targets;

        /// <summary>Screen position of the flame tip this frame (valid while touching).</summary>
        public Vector2 FlameScreen { get; private set; }

        public void Configure(Camera cameraToUse, Transform lighterTransform, GameObject flameObject, AudioClip light = null, AudioClip goesOut = null)
        {
            targetCamera = cameraToUse;
            lighter = lighterTransform;
            flame = flameObject;
            lightClip = light;
            flameOutClip = goesOut;
        }

        private void Awake()
        {
            TouchInput.EnsureEnabled();
            _model = new LighterModel();
            _model.FlameLit += OnFlameLit;
            _model.FlameWentOut += OnFlameWentOut;
            _model.Ignited += OnIgnited;
            _depth = idleDepth;

            _targets = IgnitionTargets.Shared;

            if (lighter != null)
            {
                lighter.gameObject.SetActive(false);
            }
        }

        private void OnEnable() => Game.Loop.Add(this);

        private void OnDisable() => Game.Loop.Remove(this);

        private void OnDestroy()
        {
            if (_model != null)
            {
                _model.FlameLit -= OnFlameLit;
                _model.FlameWentOut -= OnFlameWentOut;
                _model.Ignited -= OnIgnited;
            }
        }

        public void Tick(float deltaSeconds)
        {
            if (targetCamera == null)
            {
                return;
            }

            ResolveOptionalServices();

            // While a tray item is being placed, touches belong to placement.
            if (_placement != null && _placement.IsPlacing)
            {
                _touchId = -1;
                _model.Update(false, -1, deltaSeconds);
                UpdateVisual(Vector3.zero);
                return;
            }

            bool touching = TryGetTouch(out Vector2 touch);
            int targetId = -1;
            Vector3 world = Vector3.zero;
            if (touching)
            {
                Vector2 flamePx = touch + new Vector2(0f, SpikeMath.DpToPx(fingerOffsetDp, Screen.dpi));
                flamePx.x = Mathf.Clamp(flamePx.x, 0f, Screen.width);
                flamePx.y = Mathf.Clamp(flamePx.y, 0f, Screen.height);
                FlameScreen = flamePx;

                IIgnitionTarget target = _model.State == FlameState.Out
                    ? null
                    : _targets.PickOnScreen(targetCamera, flamePx, SpikeMath.DpToPx(fuseRadiusDp, Screen.dpi));
                if (target != null)
                {
                    targetId = target.TargetId;
                    _depth = Mathf.Max(targetCamera.nearClipPlane + 0.05f, Vector3.Dot(target.FuseWorldPosition - targetCamera.transform.position, targetCamera.transform.forward));
                }

                world = targetCamera.ScreenToWorldPoint(new Vector3(flamePx.x, flamePx.y, _depth));
            }

            _model.Update(touching, targetId, deltaSeconds);
            UpdateVisual(world);
        }

        private bool TryGetTouch(out Vector2 position)
        {
            var touches = ETouch.activeTouches;
            position = default;
            bool found = false;
            for (int i = 0; i < touches.Count; i++)
            {
                ETouch touch = touches[i];
                if (_touchId == -1 && touch.phase == TouchPhase.Began && !InputBlockers.IsBlocked(touch.screenPosition))
                {
                    _touchId = touch.touchId;
                }

                if (touch.touchId != _touchId)
                {
                    continue;
                }

                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    break;
                }

                position = touch.screenPosition;
                found = true;
                break;
            }

            if (!found)
            {
                _touchId = -1;
            }

            return found;
        }

        private void UpdateVisual(Vector3 world)
        {
            bool visible = _model.State != FlameState.Hidden;
            if (lighter != null)
            {
                if (lighter.gameObject.activeSelf != visible)
                {
                    lighter.gameObject.SetActive(visible);
                }

                if (visible)
                {
                    lighter.position = world;
                }
            }

            bool burning = _model.State == FlameState.Burning;
            if (flame != null && flame.activeSelf != burning)
            {
                flame.SetActive(burning);
            }
        }

        private void ResolveOptionalServices()
        {
            if (_audio == null)
            {
                Game.Services.TryGet(out _audio);
            }

            if (_haptics == null)
            {
                Game.Services.TryGet(out _haptics);
            }

            if (_placement == null)
            {
                Game.Services.TryGet(out _placement);
            }
        }

        private void OnFlameLit()
        {
            _audio?.Play(lightClip, 0.7f, SoundPriority.Interface);
        }

        private void OnFlameWentOut()
        {
            _audio?.Play(flameOutClip, 0.5f, SoundPriority.Interface);
            Game.Events.Publish(new FlameOutEvent());
        }

        private void OnIgnited(int targetId)
        {
            IIgnitionTarget target = _targets.FindById(targetId);
            if (target == null || !target.CanIgnite)
            {
                return;
            }

            target.Ignite();
            _haptics?.Play(HapticPattern.Tick);
        }
    }
}

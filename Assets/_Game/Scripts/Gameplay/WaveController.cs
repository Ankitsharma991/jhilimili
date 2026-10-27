using UnityEngine;
using UtsavNights.Core;
using UtsavNights.Core.Runtime;
using UtsavNights.Core.Services;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Hand-held crackers (PRD 7: sparkler, drag to light, wave). Touching near a burning wavable cracker picks
    /// it up; it follows the finger with a slight lean, and goes back to its spot when released or spent.
    /// The lighter asks <see cref="TryClaim"/> first, so a finger is never both waving and lighting.
    /// </summary>
    [DefaultExecutionOrder(-305)]
    public sealed class WaveController : MonoBehaviour, ITickable
    {
        [SerializeField] private Camera targetCamera;

        [Tooltip("How close (dp) a touch must start to the cracker's tip or body to pick it up.")]
        [SerializeField] private float grabRadiusDp = 64f;

        [Tooltip("Follow sharpness; higher is snappier.")]
        [SerializeField] private float follow = 22f;

        [SerializeField] private float maxLeanDegrees = 28f;

        private CrackerRuntime _held;
        private int _touchId = -1;
        private float _depth;
        private Vector3 _grabOffset;
        private Vector3 _homePosition;
        private Quaternion _homeRotation;
        private Vector3 _lastPosition;
        private bool _registered;

        public bool IsWaving => _held != null;

        public CrackerRuntime Held => _held;

        public int TouchId => _touchId;

        public void Configure(Camera cameraToUse) => targetCamera = cameraToUse;

        private void Awake()
        {
            TouchInput.EnsureEnabled();
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (!Game.Services.IsRegistered<WaveController>())
            {
                Game.Services.Register(this);
                _registered = true;
            }
        }

        private void OnEnable() => Game.Loop.Add(this);

        private void OnDisable()
        {
            Game.Loop.Remove(this);
            Release();
        }

        private void OnDestroy()
        {
            if (_registered)
            {
                Game.Services.Unregister<WaveController>();
            }
        }

        /// <summary>Called by the lighter for every new touch: picks up a burning wavable cracker under it.</summary>
        public bool TryClaim(int touchId, Vector2 screenPosition)
        {
            if (_held != null || targetCamera == null)
            {
                return false;
            }

            CrackerRuntime target = FindWavableNear(screenPosition);
            if (target == null)
            {
                return false;
            }

            Grab(target, touchId, screenPosition);
            return true;
        }

        /// <summary>
        /// Picks up a cracker directly. A touch id of -1 means scripted control (tests, accessibility):
        /// it is moved with <see cref="MoveTo"/> and only let go with <see cref="Release"/>.
        /// </summary>
        public void Grab(CrackerRuntime target, int touchId, Vector2 screenPosition)
        {
            _held = target;
            _touchId = touchId;
            _homePosition = target.transform.position;
            _homeRotation = target.transform.rotation;
            _lastPosition = _homePosition;
            Transform cam = targetCamera.transform;
            _depth = Mathf.Max(targetCamera.nearClipPlane + 0.1f, Vector3.Dot(_homePosition - cam.position, cam.forward));
            _grabOffset = _homePosition - ScreenToWorld(screenPosition);
        }

        /// <summary>Moves the held cracker as if the finger were at <paramref name="screenPosition"/>.</summary>
        public void MoveTo(Vector2 screenPosition, float deltaSeconds)
        {
            if (_held == null)
            {
                return;
            }

            Vector3 target = ScreenToWorld(screenPosition) + _grabOffset;
            Transform t = _held.transform;
            float k = 1f - Mathf.Exp(-follow * Mathf.Max(0f, deltaSeconds));
            Vector3 next = Vector3.Lerp(t.position, target, k);
            Vector3 velocity = deltaSeconds > 0f ? (next - _lastPosition) / deltaSeconds : Vector3.zero;
            _lastPosition = next;

            // Lean into the swing, like a sparkler held at its base.
            Vector3 side = targetCamera.transform.right;
            float lean = Mathf.Clamp(Vector3.Dot(velocity, side) * -6f, -maxLeanDegrees, maxLeanDegrees);
            t.SetPositionAndRotation(next, _homeRotation * Quaternion.AngleAxis(lean, Quaternion.Inverse(_homeRotation) * targetCamera.transform.forward));
        }

        public void Release()
        {
            if (_held != null)
            {
                _held.transform.SetPositionAndRotation(_homePosition, _homeRotation);
            }

            _held = null;
            _touchId = -1;
        }

        public void Tick(float deltaSeconds)
        {
            if (_held == null)
            {
                return;
            }

            if (!_held.IsBurning)
            {
                Release();
                return;
            }

            var touches = ETouch.activeTouches;
            for (int i = 0; i < touches.Count; i++)
            {
                ETouch touch = touches[i];
                if (touch.touchId != _touchId)
                {
                    continue;
                }

                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    break;
                }

                MoveTo(touch.screenPosition, deltaSeconds);
                return;
            }

            if (_touchId >= 0)
            {
                Release();
            }
        }

        private CrackerRuntime FindWavableNear(Vector2 screenPosition)
        {
            float radius = SpikeMath.DpToPx(grabRadiusDp, Screen.dpi);
            float best = radius * radius;
            CrackerRuntime found = null;
            var all = IgnitionTargets.Shared.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (!(all[i] is CrackerRuntime c) || c.Definition == null || !c.Definition.Wavable || !c.IsBurning)
                {
                    continue;
                }

                // The tip, the middle and the base all count as "the sparkler".
                Vector3 tip = c.FuseWorldPosition;
                Vector3 bottom = c.transform.position;
                for (int s = 0; s <= 2; s++)
                {
                    Vector3 screen = targetCamera.WorldToScreenPoint(Vector3.Lerp(bottom, tip, s * 0.5f));
                    if (screen.z <= 0f)
                    {
                        continue;
                    }

                    float d = ((Vector2)screen - screenPosition).sqrMagnitude;
                    if (d <= best)
                    {
                        best = d;
                        found = c;
                    }
                }
            }

            return found;
        }

        private Vector3 ScreenToWorld(Vector2 screen) => targetCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, _depth));
    }
}

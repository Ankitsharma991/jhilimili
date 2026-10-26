using System;
using UnityEngine;
using UtsavNights.Content;
using UtsavNights.Core;
using UtsavNights.Core.Placement;
using UtsavNights.Core.Runtime;
using UtsavNights.Core.Services;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Placement flow (F04, PRD 6.3): after a tray item is chosen, a ghost follows the finger (lifted a
    /// little so the finger does not hide it), tinted green or red with a reason, and releasing on a
    /// valid spot places the cracker. While placing, the lighter ignores touches.
    /// </summary>
    [DefaultExecutionOrder(-310)]
    public sealed class PlacementController : MonoBehaviour, ITickable
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private GhostPreview ghost;

        [Tooltip("Ghost sits this far above the finger on screen, in dp.")]
        [SerializeField] private float fingerOffsetDp = 40f;

        [Tooltip("Stay in placement mode after a successful placement (place several of the same item).")]
        [SerializeField] private bool stayAfterPlacing;

        private readonly PlacementValidator _validator = new PlacementValidator();
        private CrackerSpawner _spawner;
        private int _touchId = -1;
        private bool _registered;

        /// <summary>Raised when the reason under the finger changes (empty string = valid).</summary>
        public event Action<string> ReasonChanged;

        /// <summary>Raised after a cracker has been placed.</summary>
        public event Action<CrackerRuntime> Placed;

        /// <summary>Raised when placement starts (the selected definition) or stops (null).</summary>
        public event Action<CrackerDefinition> SelectionChanged;

        public bool IsPlacing => Selected != null;

        public bool StayAfterPlacing
        {
            get => stayAfterPlacing;
            set => stayAfterPlacing = value;
        }

        public CrackerDefinition Selected { get; private set; }

        public PlacementCheck LastCheck { get; private set; }

        public PlacementValidator Validator => _validator;

        public void Configure(Camera cameraToUse, GhostPreview ghostPreview)
        {
            targetCamera = cameraToUse;
            ghost = ghostPreview;
        }

        private void Awake()
        {
            TouchInput.EnsureEnabled();
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (!Game.Services.IsRegistered<PlacementController>())
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
                Game.Services.Unregister<PlacementController>();
            }
        }

        public void BeginPlacing(CrackerDefinition definition)
        {
            if (definition == null)
            {
                Cancel();
                return;
            }

            Selected = definition;
            _touchId = -1;
            ghost?.Hide();
            SetReason(string.Empty);
            SelectionChanged?.Invoke(definition);
        }

        public void Cancel()
        {
            bool wasPlacing = Selected != null;
            Selected = null;
            _touchId = -1;
            ghost?.Hide();
            SetReason(string.Empty);
            if (wasPlacing)
            {
                SelectionChanged?.Invoke(null);
            }
        }

        /// <summary>Checks a screen point without placing (also used by tests and the tray).</summary>
        public PlacementCheck CheckAt(Vector2 screenPoint)
        {
            if (Selected == null || targetCamera == null)
            {
                return default;
            }

            ResolveSpawner();
            int placed = _spawner != null ? _spawner.ActiveCount : 0;
            int limit = _spawner != null ? _spawner.ItemLimit : 0;
            LastCheck = _validator.Check(Selected, targetCamera.ScreenPointToRay(screenPoint), placed, limit);
            return LastCheck;
        }

        /// <summary>Tries to place the selected item at a screen point.</summary>
        public CrackerRuntime PlaceAt(Vector2 screenPoint)
        {
            PlacementCheck check = CheckAt(screenPoint);
            if (!check.Valid || _spawner == null)
            {
                SetReason(PlacementRules.Message(check.Block));
                return null;
            }

            Vector3 toCamera = targetCamera.transform.position - check.Point;
            toCamera.y = 0f;
            Quaternion facing = toCamera.sqrMagnitude > 0.001f ? Quaternion.LookRotation(-toCamera.normalized, Vector3.up) : Quaternion.identity;
            CrackerRuntime runtime = _spawner.Spawn(Selected, check.Point, facing);
            if (runtime != null)
            {
                Placed?.Invoke(runtime);
                if (!stayAfterPlacing)
                {
                    Cancel();
                }
            }

            return runtime;
        }

        public void Tick(float deltaSeconds)
        {
            if (Selected == null)
            {
                return;
            }

            var touches = ETouch.activeTouches;
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

                found = true;
                Vector2 point = touch.screenPosition + new Vector2(0f, SpikeMath.DpToPx(fingerOffsetDp, Screen.dpi));
                if (touch.phase == TouchPhase.Ended)
                {
                    _touchId = -1;
                    ghost?.Hide();
                    PlaceAt(point);
                    return;
                }

                if (touch.phase == TouchPhase.Canceled)
                {
                    _touchId = -1;
                    ghost?.Hide();
                    return;
                }

                PlacementCheck check = CheckAt(point);
                SetReason(PlacementRules.Message(check.Block));
                if (ghost != null && check.Block != PlacementBlock.NoSurface)
                {
                    if (!ghost.IsVisible)
                    {
                        ghost.Show(Selected);
                    }

                    ghost.Move(check.Point, check.Valid);
                }

                break;
            }

            if (!found)
            {
                _touchId = -1;
            }
        }

        private string _reason = string.Empty;

        private void SetReason(string reason)
        {
            if (reason == _reason)
            {
                return;
            }

            _reason = reason;
            ReasonChanged?.Invoke(reason);
        }

        private void ResolveSpawner()
        {
            if (_spawner == null)
            {
                Game.Services.TryGet(out _spawner);
            }
        }
    }
}

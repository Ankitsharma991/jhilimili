using System;
using System.Collections.Generic;
using UnityEngine;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Home;
using UtsavNights.Core.Neighbourhood;
using UtsavNights.Core.Runtime;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace UtsavNights.Gameplay
{
    /// <summary>The player went for a walk (analytics in step 7).</summary>
    public readonly struct WalkStartedEvent
    {
        public WalkStartedEvent(string environment) => Environment = environment;

        public string Environment { get; }
    }

    /// <summary>The walk ended: how long and how far (whole seconds and metres only).</summary>
    public readonly struct WalkEndedEvent
    {
        public WalkEndedEvent(int seconds, int metres)
        {
            Seconds = seconds;
            Metres = metres;
        }

        public int Seconds { get; }

        public int Metres { get; }
    }

    /// <summary>The map's fast travel to a spot.</summary>
    public readonly struct FastTravelEvent
    {
        public FastTravelEvent(string spot) => Spot = spot;

        public string Spot { get; }
    }

    /// <summary>
    /// Walking in the neighbourhood (Phase 8 step 3, PRD 10 controls): the player's avatar walks where the ground is tapped
    /// (or with the optional joystick), the camera follows behind it (drag to look round, pinch for a limited zoom, pulled in
    /// front of walls), the map moves it to a spot behind a short fade, and stairs fade too. The home gate opens for the walk.
    /// The touch methods are public, so tests drive exactly what a finger would.
    /// </summary>
    public sealed class WalkController : MonoBehaviour
    {
        public const float FadeSeconds = 0.35f;
        public const float MinDistance = 2.4f;
        public const float MaxDistance = 6.5f;

        private Camera _camera;
        private CameraRig _rig;
        private SettlementView _settlement;
        private HomeService _home;
        private WalkNavigator _nav;
        private WalkAgent _agent;
        private AvatarView _avatar;
        private AvatarAnimator _animator;
        private float _orbitYaw;
        private float _orbitPitch = 18f;
        private float _distance = 4.2f;
        private int _seenFades;
        private float _fade;
        private float _startedAt;
        private float _metres;
        private V3 _last;
        private int _touchId = -1;
        private Vector2 _downAt;
        private Vector2 _lastAt;
        private bool _dragged;
        private float _pinchStart;

        public event Action StateChanged;

        public bool IsOpen { get; private set; }

        /// <summary>Screen points over the walk mode's buttons, map and joystick; set by the UI.</summary>
        public Func<Vector2, bool> IsOverUi { get; set; } = _ => false;

        /// <summary>The joystick's push, -1 to 1 on each axis (x right, y forward relative to the camera); set by the UI.</summary>
        public Vector2 Stick { get; set; }

        /// <summary>0 to 1: how dark the fade overlay is (stairs, fast travel).</summary>
        public float Fade => _fade;

        public WalkNavigator Navigator => _nav;

        public WalkAgent Agent => _agent;

        public AvatarView Avatar => _avatar;

        public SettlementLayout Layout => _settlement != null ? _settlement.Layout : null;

        public string Message { get; private set; } = string.Empty;

        public void Configure(Camera cameraToDrive, CameraRig rig)
        {
            _camera = cameraToDrive;
            _rig = rig;
        }

        public bool Open()
        {
            if (IsOpen || !Game.Services.TryGet(out _settlement) || !Game.Services.TryGet(out _home))
            {
                return false;
            }

            DecorationController decorating = FindFirstObjectByType<DecorationController>();
            if (decorating != null && decorating.IsOpen)
            {
                return false;
            }

            _camera = _camera != null ? _camera : Camera.main;
            _rig = _rig != null ? _rig : FindFirstObjectByType<CameraRig>();
            TouchInput.EnsureEnabled();
            _nav = new WalkNavigator(_settlement.Layout, _home.Design);
            WalkNode start = _settlement.Layout.Node("courtyard");
            _agent = new WalkAgent(start.Position, 0f);
            _seenFades = 0;
            _metres = 0f;
            _last = _agent.Position;
            _startedAt = Time.unscaledTime;
            _orbitYaw = 0f;

            AvatarProfile profile = Game.Services.TryGet(out ProfileService profiles) && profiles.Current.created ? profiles.Current : AvatarPresets.Default;
            _avatar = AvatarView.Create(transform, profile, "WalkingAvatar");
            _animator = _avatar.gameObject.AddComponent<AvatarAnimator>();
            CourtyardAvatar courtyard = FindFirstObjectByType<CourtyardAvatar>();
            if (courtyard != null && courtyard.View != null)
            {
                courtyard.View.gameObject.SetActive(false);
            }

            if (_home.House != null)
            {
                _home.House.SetGateOpen(true);
            }

            _settlement.Focus = () => ToVector(_agent.Position);
            IsOpen = true;
            Place();
            CameraPose(out Vector3 p, out Quaternion r);
            if (_rig != null)
            {
                _rig.BeginOverride(p, r);
            }

            Message = "Tap the ground to walk there. Drag to look around.";
            Game.Events.Publish(new WalkStartedEvent(_settlement.Layout.environment));
            StateChanged?.Invoke();
            return true;
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            Game.Events.Publish(new WalkEndedEvent((int)(Time.unscaledTime - _startedAt), (int)_metres));
            if (_avatar != null)
            {
                Destroy(_avatar.gameObject);
            }

            CourtyardAvatar courtyard = FindFirstObjectByType<CourtyardAvatar>();
            if (courtyard != null)
            {
                courtyard.Refresh();
            }

            if (_home != null && _home.House != null)
            {
                _home.House.SetGateOpen(false);
            }

            if (_settlement != null)
            {
                _settlement.Focus = null;
            }

            if (_rig != null)
            {
                _rig.EndOverride();
            }

            Stick = Vector2.zero;
            StateChanged?.Invoke();
        }

        /// <summary>Walks to a world point (moved to the nearest walkable point). False when there is no way there.</summary>
        public bool WalkTo(Vector3 world)
        {
            if (!IsOpen)
            {
                return false;
            }

            List<Waypoint> route = _nav.Route(_agent.Position, ToV3(world));
            if (route == null)
            {
                Message = "You can't get there from here.";
                StateChanged?.Invoke();
                return false;
            }

            _agent.Follow(route);
            Message = string.Empty;
            StateChanged?.Invoke();
            return true;
        }

        /// <summary>The map's fast travel: a fade, and the avatar stands at the spot facing where its fireworks go.</summary>
        public bool FastTravel(string spotId)
        {
            SpotDef spot = Layout?.Spot(spotId);
            WalkNode node = Layout?.Node(spotId);
            if (!IsOpen || spot == null || node == null)
            {
                return false;
            }

            V3 at = _nav.IsWalkable(node.Position) ? node.Position : _nav.Nearest(node.Position);
            if (!_nav.IsWalkable(at))
            {
                Message = spot.name + " isn't open at your house.";
                StateChanged?.Invoke();
                return false;
            }

            _agent.Place(at, spot.camYaw);
            _orbitYaw = 0f;
            _fade = 1f;
            _last = at;
            Place();
            Game.Events.Publish(new FastTravelEvent(spot.id));
            StateChanged?.Invoke();
            return true;
        }

        /// <summary>Advances the walk (tests call it to skip time; the game calls it every frame).</summary>
        public void Advance(float deltaSeconds)
        {
            if (!IsOpen)
            {
                return;
            }

            if (Stick.sqrMagnitude > 0.01f)
            {
                float yaw = (_agent.Yaw + _orbitYaw) * Mathf.Deg2Rad;
                var forward = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
                var right = new Vector3(forward.z, 0f, -forward.x);
                Vector3 move = (right * Stick.x + forward * Stick.y) * (WalkAgent.Speed * deltaSeconds);
                V3 next = _nav.Slide(_agent.Position, new V3(move.x, 0f, move.z));
                float keepYaw = _orbitYaw + _agent.Yaw;
                _agent.Nudge(next);
                _orbitYaw = keepYaw - _agent.Yaw;
            }

            _agent.Tick(deltaSeconds);
            if (_agent.Faded != _seenFades)
            {
                _seenFades = _agent.Faded;
                _fade = 1f;
            }

            _metres += (_agent.Position - _last).Length;
            _last = _agent.Position;
            _fade = Mathf.Max(0f, _fade - deltaSeconds / FadeSeconds);
            Place();
        }

        // ---- Touch ----

        public void PointerDown(Vector2 screen)
        {
            if (!IsOpen || IsOverUi(screen))
            {
                return;
            }

            _downAt = _lastAt = screen;
            _dragged = false;
        }

        public void PointerMove(Vector2 screen)
        {
            if (!IsOpen)
            {
                return;
            }

            if ((screen - _downAt).magnitude > Screen.dpi * 0.05f + 6f)
            {
                _dragged = true;
            }

            if (_dragged)
            {
                Vector2 d = screen - _lastAt;
                _orbitYaw += d.x * 180f / Mathf.Max(1f, Screen.width);
                _orbitPitch = Mathf.Clamp(_orbitPitch - d.y * 90f / Mathf.Max(1f, Screen.height), 4f, 55f);
            }

            _lastAt = screen;
        }

        /// <summary>A tap (no drag) walks to the ground under it.</summary>
        public void PointerUp(Vector2 screen)
        {
            if (!IsOpen || _dragged || IsOverUi(screen) || _camera == null)
            {
                _dragged = false;
                return;
            }

            Ray ray = _camera.ScreenPointToRay(screen);
            if (Physics.Raycast(ray, out RaycastHit hit, 80f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                WalkTo(hit.point);
            }
        }

        public void Zoom(float factor) => _distance = Mathf.Clamp(_distance / factor, MinDistance, MaxDistance);

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            ReadTouches();
            Advance(Time.deltaTime);
        }

        private void ReadTouches()
        {
            var touches = ETouch.activeTouches;
            if (touches.Count >= 2)
            {
                float span = (touches[0].screenPosition - touches[1].screenPosition).magnitude;
                if (_pinchStart > 0f)
                {
                    Zoom(span / _pinchStart);
                }

                _pinchStart = Mathf.Max(1f, span);
                _touchId = -2;
                return;
            }

            _pinchStart = 0f;
            if (_touchId == -2)
            {
                if (touches.Count == 0)
                {
                    _touchId = -1;
                }

                return;
            }

            for (int i = 0; i < touches.Count; i++)
            {
                ETouch t = touches[i];
                if (_touchId == -1 && t.phase == TouchPhase.Began)
                {
                    if (IsOverUi(t.screenPosition))
                    {
                        continue;
                    }

                    _touchId = t.touchId;
                    PointerDown(t.screenPosition);
                }
                else if (t.touchId == _touchId && t.phase == TouchPhase.Moved)
                {
                    PointerMove(t.screenPosition);
                }
                else if (t.touchId == _touchId && (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled))
                {
                    _touchId = -1;
                    PointerUp(t.screenPosition);
                }
            }
        }

        private void Place()
        {
            if (_avatar == null)
            {
                return;
            }

            _avatar.transform.SetPositionAndRotation(ToVector(_agent.Position), Quaternion.Euler(0f, _agent.Yaw, 0f));
            if (_animator != null)
            {
                _animator.SetWalking(_agent.Walking || Stick.sqrMagnitude > 0.01f);
            }

            if (_rig != null)
            {
                CameraPose(out Vector3 p, out Quaternion r);
                _rig.SetOverridePose(p, r);
            }
        }

        /// <summary>Behind and above the avatar, looking at its head; pulled in front of any wall between.</summary>
        private void CameraPose(out Vector3 position, out Quaternion rotation)
        {
            Vector3 head = ToVector(_agent.Position) + Vector3.up * 1.45f;
            Quaternion look = Quaternion.Euler(_orbitPitch, _agent.Yaw + _orbitYaw, 0f);
            Vector3 back = look * Vector3.back;
            float distance = _distance;
            if (Physics.SphereCast(head, 0.2f, back, out RaycastHit hit, _distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                distance = Mathf.Max(0.6f, hit.distance - 0.1f);
            }

            position = head + back * distance;
            rotation = Quaternion.LookRotation(head - position, Vector3.up);
        }

        private void OnDestroy()
        {
            if (IsOpen)
            {
                Close();
            }
        }

        private static Vector3 ToVector(V3 v) => new Vector3(v.X, v.Y, v.Z);

        private static V3 ToV3(Vector3 v) => new V3(v.x, v.y, v.z);
    }
}

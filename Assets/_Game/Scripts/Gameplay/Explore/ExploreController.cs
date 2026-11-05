using System;
using System.Collections.Generic;
using UnityEngine;
using UtsavNights.Audio;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.District;
using UtsavNights.Core.Explore;
using UtsavNights.Core.Home;
using UtsavNights.Core.Neighbourhood;
using UtsavNights.Core.Runtime;

namespace UtsavNights.Gameplay
{
    /// <summary>Explore mode began or ended (analytics later; distances in whole metres).</summary>
    public readonly struct ExploreEvent
    {
        public ExploreEvent(bool started, int seconds, int metres, int jumps)
        {
            Started = started;
            Seconds = seconds;
            Metres = metres;
            Jumps = jumps;
        }

        public bool Started { get; }

        public int Seconds { get; }

        public int Metres { get; }

        public int Jumps { get; }
    }

    /// <summary>
    /// Explore mode (Phase 12 step 1): the player's own avatar as a game character in the neighbourhood. The stick moves it
    /// relative to the camera (walk, jog, sprint, jump, land: <see cref="Locomotion"/>), a CharacterController keeps it on
    /// the ground and out of walls (the neighbourhood's walls become colliders), the follow camera orbits over its shoulder
    /// and pulls in at walls (<see cref="OrbitCamera"/>), the run and walk play at the pace of the feet, footsteps sound,
    /// and an Interact prompt offers what's near: celebrate at a spot, go inside the house.
    /// </summary>
    public sealed class ExploreController : MonoBehaviour
    {
        public const float InteractRange = 2.2f;
        public const float LookSensitivity = 0.2f;
        private const float BodyHeight = 1.7f;
        private const float BodyRadius = 0.3f;

        /// <summary>Phase 12 step 5: the scooter waits where the lane meets the bazaar road, at the south edge out of the way, short of the traffic, facing into the bazaar.</summary>
        public static readonly Vector3 ParkingSpot = new Vector3(33f, 0f, 4.7f);
        public const float ParkingHeading = 90f;

        /// <summary>How far the scooter's middle is ahead of the rider's hips, and its nose and tail from the rider.</summary>
        public const float SeatBack = 0.38f;
        public const float NoseAhead = 1.25f;
        public const float TailBehind = 0.45f;

        /// <summary>Slow enough to step off (m/s).</summary>
        public const float DismountSpeed = 2.5f;

        public const float RideCameraDistance = 4.6f;

        private const string JobPrefix = "job:";
        private const string RoofDown = "roof_down";
        private const string LightFuse = "light_fuse";

        private readonly OrbitCamera _orbit = new OrbitCamera();
        private readonly List<(string id, string label, Vector3 at)> _targets = new List<(string, string, Vector3)>();
        private readonly AudioClip[] _steps = new AudioClip[Footsteps.Variations];
        private Camera _camera;
        private CameraRig _rig;
        private SettlementView _settlement;
        private HomeService _home;
        private WalkNavigator _nav;
        private DistrictView _district;
        private (bool on, Color colour, FogMode mode, float density) _fog;
        private GameObject _world;
        private AvatarView _avatar;
        private AvatarAnimator _animator;
        private CharacterController _body;
        private LocomotionState _state;
        private float _distanceVelocity;
        private float _cameraDistance;
        private float _startedAt;
        private float _metres;
        private int _jumps;
        private int _step;
        private bool _jumpPressed;
        private ScreenOrientation _orientation;
        private float _lookScale = 1f;
        private bool _invertLook;
        private bool _autoRecentre = true;
        private (bool p, bool pu, bool l, bool r) _rotation;
        private GameObject _scooter;
        private Mesh _scooterMesh;
        private HomeMaterials _scooterMaterials;
        private Vector3 _parked = ParkingSpot;
        private float _parkedHeading = ParkingHeading;
        private ScooterState _ride;
        private float _walkCameraDistance;
        private JobService _jobs;
        private TownFireworks _town;
        private string _jobPromptStep;
        private string _jobPromptId;
        private VoiceHandle _engine;
        private AudioClip _engineClip;
        private AudioClip _hornClip;

        public event Action StateChanged;

        public bool IsOpen { get; private set; }

        /// <summary>Set by the HUD each frame: the stick's push (x right, y forward, relative to the camera).</summary>
        public Vector2 Stick { get; set; }

        /// <summary>Set by the HUD: sprint held or toggled on.</summary>
        public bool Sprint { get; set; }

        public Transform Player => _avatar != null ? _avatar.transform : null;

        public LocomotionState State => _state;

        public Gait Gait => Locomotion.GaitOf(_state);

        public float Speed => Riding ? Mathf.Abs(_ride.Speed) : _state.GroundSpeed;

        /// <summary>Phase 12 step 5: on the scooter.</summary>
        public bool Riding { get; private set; }

        public ScooterState RideState => _ride;

        /// <summary>The player's scooter while it's out (the district is open), or null.</summary>
        public Transform Scooter => _scooter != null ? _scooter.transform : null;

        /// <summary>Where the scooter is parked and which way it faces (kept between visits to the bazaar this session).</summary>
        public (Vector3 at, float heading) Parked => (_parked, _parkedHeading);

        public int Horns { get; private set; }

        public OrbitCamera Orbit => _orbit;

        /// <summary>What Interact would do now ("Celebrate here", "Go inside"), or empty.</summary>
        public string Prompt { get; private set; } = string.Empty;

        public string PromptTarget { get; private set; } = string.Empty;

        public float Metres => _metres;

        public void Configure(Camera cameraToDrive, CameraRig rig)
        {
            _camera = cameraToDrive;
            _rig = rig;
        }

        /// <summary>Starts exploring from the courtyard. False when it can't (walking, decorating, inside or visiting).</summary>
        public bool Open()
        {
            if (IsOpen || !Game.Services.TryGet(out _settlement) || !Game.Services.TryGet(out _home) || _home.Visiting)
            {
                return false;
            }

            WalkController walk = FindFirstObjectByType<WalkController>();
            DecorationController decorating = FindFirstObjectByType<DecorationController>();
            InteriorController inside = FindFirstObjectByType<InteriorController>();
            if ((walk != null && walk.IsOpen) || (decorating != null && decorating.IsOpen) || (inside != null && inside.IsInside))
            {
                return false;
            }

            _camera = _camera != null ? _camera : Camera.main;
            _rig = _rig != null ? _rig : FindFirstObjectByType<CameraRig>();
            if (Game.Services.TryGet(out PlacementController placement))
            {
                placement.Cancel();
            }

            TouchInput.EnsureEnabled();
            _nav = new WalkNavigator(_settlement.Layout, _home.Design);
            Game.Services.TryGet(out _district);
            BuildWorld();
            AvatarProfile profile = Game.Services.TryGet(out ProfileService profiles) && profiles.Current.created ? profiles.Current : AvatarPresets.Default;
            _avatar = AvatarView.Create(transform, profile, "ExplorePlayer");
            _animator = _avatar.gameObject.AddComponent<AvatarAnimator>();
            _animator.FootDown += OnFootDown;
            _body = _avatar.gameObject.AddComponent<CharacterController>();
            _body.height = BodyHeight;
            _body.radius = BodyRadius;
            _body.center = new Vector3(0f, BodyHeight / 2f + 0.02f, 0f);
            _body.stepOffset = 0.32f;
            _body.slopeLimit = 48f;
            _body.skinWidth = 0.03f;
            WalkNode start = _settlement.Layout.Node("courtyard");
            Teleport(new Vector3(start.Position.X, 0f, start.Position.Z), 180f);

            CourtyardAvatar courtyard = FindFirstObjectByType<CourtyardAvatar>();
            if (courtyard != null && courtyard.View != null)
            {
                courtyard.View.gameObject.SetActive(false);
            }

            if (_home.House != null)
            {
                _home.House.SetGateOpen(true);
            }

            // In the bazaar, the lane behind keeps the detail it has at its east end: no rebuilding lots as the player rides off.
            float laneEnd = DistrictGenerator.Bazaar.Join.X0;
            _settlement.Focus = () => Player != null ? new Vector3(Mathf.Min(Player.position.x, laneEnd), Player.position.y, Player.position.z) : Vector3.zero;
            Haze(true);
            if (Game.Services.TryGet(out SettingsService settings))
            {
                ApplySettings(settings.Current);
            }

            Game.Events.Subscribe<Core.Settings.SettingsChangedEvent>(OnSettings);
            CollectTargets();
            Landscape(true);
            _startedAt = Time.unscaledTime;
            _metres = 0f;
            _jumps = 0;
            IsOpen = true;
            _cameraDistance = _orbit.Distance;
            Pose(out Vector3 p, out Quaternion r, 0f);
            _rig?.BeginOverride(p, r);
            Game.Events.Publish(new ExploreEvent(true, 0, 0, 0));
            StateChanged?.Invoke();
            return true;
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            if (Riding)
            {
                Dismount();
            }

            DestroyScooter();
            IsOpen = false;
            Game.Events.Unsubscribe<Core.Settings.SettingsChangedEvent>(OnSettings);
            Game.Events.Publish(new ExploreEvent(false, (int)(Time.unscaledTime - _startedAt), (int)_metres, _jumps));
            if (_avatar != null)
            {
                Destroy(_avatar.gameObject);
            }

            if (_world != null)
            {
                Destroy(_world);
            }

            _avatar = null;
            _animator = null;
            _body = null;
            CourtyardAvatar courtyard = FindFirstObjectByType<CourtyardAvatar>();
            if (courtyard != null)
            {
                courtyard.Refresh();
            }

            if (_home?.House != null)
            {
                _home.House.SetGateOpen(false);
            }

            if (_settlement != null)
            {
                _settlement.Focus = null;
            }

            _rig?.EndOverride();
            Haze(false);
            Landscape(false);
            Stick = Vector2.zero;
            Sprint = false;
            Prompt = string.Empty;
            PromptTarget = string.Empty;
            StateChanged?.Invoke();
        }

        /// <summary>Same as tapping Jump.</summary>
        public void Jump()
        {
            if (Riding)
            {
                Horn();
                return;
            }

            _jumpPressed = true;
        }

        /// <summary>Turns the camera by a drag on the screen (pixels), with the player's sensitivity and invert settings.</summary>
        public void Look(Vector2 pixels) =>
            _orbit.Look(pixels.x * LookSensitivity * _lookScale, pixels.y * LookSensitivity * _lookScale, _invertLook);

        /// <summary>Phase 12 step 2: the control settings that shape the camera.</summary>
        public void ApplySettings(Core.Settings.GameSettings s)
        {
            _lookScale = s.lookSensitivity;
            _invertLook = s.invertLook;
            _autoRecentre = s.autoRecentre;
        }

        /// <summary>Same as tapping Interact: does what the prompt says. False when there's nothing near.</summary>
        public bool Interact()
        {
            if (!IsOpen || string.IsNullOrEmpty(PromptTarget))
            {
                return false;
            }

            string target = PromptTarget;
            if (target == "scooter")
            {
                Mount();
                return true;
            }

            if (target == "scooter_off")
            {
                Dismount();
                return true;
            }

            // Phase 12 step 6: a job's step here, or down off a roof.
            if (target == RoofDown)
            {
                return GoDown();
            }

            if (target == LightFuse)
            {
                return Game.Services.TryGet(out TownFireworks town) && town.LightAt(_avatar.transform.position);
            }

            if (target.StartsWith(JobPrefix, StringComparison.Ordinal))
            {
                return Game.Services.TryGet(out JobService jobs) && jobs.Interact(_avatar.transform.position);
            }

            if (target == "door")
            {
                Close();
                InteriorController inside = FindFirstObjectByType<InteriorController>();
                return inside != null && inside.Enter(Rooms.Main);
            }

            if (Game.Services.TryGet(out CelebrationService celebration) && celebration.Choose(target))
            {
                Close();
                return true;
            }

            return false;
        }

        /// <summary>Puts the character somewhere at once (the start, tests).</summary>
        public void Teleport(Vector3 world, float facing)
        {
            if (_body != null)
            {
                _body.enabled = false;
            }

            _avatar.transform.SetPositionAndRotation(world, Quaternion.Euler(0f, facing, 0f));
            if (_body != null)
            {
                _body.enabled = true;
            }

            _state = new LocomotionState { Facing = facing, Grounded = true };
            _ride.Heading = facing;
            _ride.Speed = 0f;
            _orbit.Yaw = facing;
        }

        private void Update()
        {
            if (!IsOpen || _body == null)
            {
                return;
            }

            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            SyncScooter();
            if (Riding)
            {
                RideStep(dt);
                UpdatePrompt();
                return;
            }

            // The stick, turned from the camera's view into the world.
            float yaw = _orbit.Yaw * Mathf.Deg2Rad;
            Vector2 stick = Vector2.ClampMagnitude(Stick, 1f);
            float wx = stick.x * Mathf.Cos(yaw) + stick.y * Mathf.Sin(yaw);
            float wz = -stick.x * Mathf.Sin(yaw) + stick.y * Mathf.Cos(yaw);
            var input = new LocomotionInput(wx, wz, Sprint && stick.magnitude > Locomotion.Deadzone, _jumpPressed);
            _jumpPressed = false;
            bool wasGrounded = _state.Grounded;
            float fall = -_state.Velocity.Y;
            _state = Locomotion.Step(_state, input, dt);
            if (_state.Jumped)
            {
                _jumps++;
            }

            Vector3 before = _avatar.transform.position;
            _body.Move(new Vector3(_state.Velocity.X, _state.Velocity.Y, _state.Velocity.Z) * dt);
            Vector3 after = _avatar.transform.position;

            // Stay on the neighbourhood's walkable ground (its edges are open, not walled).
            if (!Walkable(after))
            {
                // Back to where it was if that was fine (the river's edge, the end of the town); else onto the lane.
                if (Walkable(before))
                {
                    Move(new Vector3(before.x, after.y, before.z));
                }
                else
                {
                    V3 near = _nav.Nearest(new V3(after.x, 0f, after.z));
                    Move(new Vector3(near.X, after.y, near.Z));
                }

                after = _avatar.transform.position;
                _state.Velocity = new V3(0f, _state.Velocity.Y, 0f);
            }

            bool grounded = _body.isGrounded;
            if (grounded && !wasGrounded)
            {
                _state = Locomotion.Land(_state, fall);
            }

            _state.Grounded = grounded;
            if (grounded && _state.Velocity.Y < 0f)
            {
                _state.Velocity = new V3(_state.Velocity.X, -2f, _state.Velocity.Z);
            }

            _metres += new Vector2(after.x - before.x, after.z - before.z).magnitude;
            _avatar.transform.rotation = Quaternion.Euler(0f, _state.Facing, 0f);
            _animator.SetLocomotion(_state.GroundSpeed, grounded || _state.Airborne <= Locomotion.CoyoteTime); // it ticks itself
            _orbit.Follow(_state.Facing, _state.GroundSpeed > 0.5f, dt, _autoRecentre);
            UpdatePrompt();
        }

        private void OnSettings(Core.Settings.SettingsChangedEvent e) => ApplySettings(e.Settings);

        private void OnDestroy() => Game.Events.Unsubscribe<Core.Settings.SettingsChangedEvent>(OnSettings);

        private void LateUpdate()
        {
            if (!IsOpen || _avatar == null)
            {
                return;
            }

            Pose(out Vector3 p, out Quaternion r, Time.deltaTime);
            _rig?.SetOverridePose(p, r);
        }

        private void Pose(out Vector3 position, out Quaternion rotation, float dt)
        {
            Vector3 at = _avatar.transform.position;
            var character = new V3(at.x, at.y, at.z);
            V3 pivot = _orbit.Pivot(character);
            V3 back = _orbit.Back();
            var from = new Vector3(pivot.X, pivot.Y, pivot.Z);
            var dir = new Vector3(back.X, back.Y, back.Z);
            float? hit = null;
            if (Physics.SphereCast(from, 0.18f, dir, out RaycastHit h, _orbit.Distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && h.collider != _body)
            {
                hit = h.distance;
            }

            float clear = _orbit.Clear(hit);

            // Pull in at once at a wall; ease back out after it.
            _cameraDistance = clear < _cameraDistance || dt <= 0f ? clear : OrbitCamera.Smooth(_cameraDistance, clear, ref _distanceVelocity, 0.35f, dt);
            V3 p = _orbit.Position(character, _cameraDistance);
            position = new Vector3(p.X, p.Y, p.Z);
            rotation = Quaternion.LookRotation(from - position, Vector3.up);
        }

        private bool Walkable(Vector3 world) => _nav.IsWalkable(new V3(world.x, 0f, world.z)) || (_district != null && _district.IsWalkable(world));

        private void Move(Vector3 world)
        {
            _body.enabled = false;
            _avatar.transform.position = world;
            _body.enabled = true;
        }

        private void UpdatePrompt()
        {
            Vector3 at = _avatar.transform.position;
            string label = string.Empty, id = string.Empty;
            float best = InteractRange;
            if (Riding)
            {
                if (Mathf.Abs(_ride.Speed) < DismountSpeed)
                {
                    label = "Get off";
                    id = "scooter_off";
                }
            }
            else
            {
                foreach ((string tid, string tlabel, Vector3 point) in _targets)
                {
                    float d = new Vector2(point.x - at.x, point.z - at.z).magnitude;
                    if (d < best)
                    {
                        best = d;
                        label = tlabel;
                        id = tid;
                    }
                }

                if (_scooter != null && new Vector2(_parked.x - at.x, _parked.z - at.z).magnitude < best)
                {
                    label = "Ride the scooter";
                    id = "scooter";
                }

                // A job's step comes first; up on a roof, the way down is always there.
                if (_jobs != null || Game.Services.TryGet(out _jobs))
                {
                    if (_jobs.PromptAt(at, out string jobLabel, out string jobStep))
                    {
                        if (!ReferenceEquals(jobStep, _jobPromptStep))
                        {
                            _jobPromptStep = jobStep;
                            _jobPromptId = JobPrefix + jobStep; // made once per step, not every frame
                        }

                        label = jobLabel;
                        id = _jobPromptId;
                    }
                }

                // Phase 12 step 7: a cracker the player set down, ready to light.
                if (id.Length == 0 && (_town != null || Game.Services.TryGet(out _town)) && _town.LightableAt(at) != null)
                {
                    label = "Light the fuse";
                    id = LightFuse;
                }

                if (id.Length == 0 && at.y > 1.5f)
                {
                    label = "Go down to the street";
                    id = RoofDown;
                }
            }

            if (id != PromptTarget)
            {
                Prompt = label;
                PromptTarget = id;
                StateChanged?.Invoke();
            }
        }

        private void CollectTargets()
        {
            _targets.Clear();
            Vector3 door = new Vector3(0f, 0f, HouseGeometry.WorldFrontZ + 0.7f);
            _targets.Add(("door", "Go inside", door));
            if (!Game.Services.TryGet(out CelebrationService celebration))
            {
                return;
            }

            foreach (SpotDef spot in _settlement.Layout.spots)
            {
                if (celebration.Availability(spot.id).Open)
                {
                    string where = string.IsNullOrEmpty(spot.name) ? "here" : spot.name.ToLowerInvariant();
                    _targets.Add((spot.id, $"Celebrate at the {where}", new Vector3(spot.x, 0f, spot.z)));
                }
            }
        }

        /// <summary>The neighbourhood's walls and houses as solid boxes, and the ground, for the character and the camera.</summary>
        private void BuildWorld()
        {
            _world = new GameObject("ExploreWorld");
            _world.transform.SetParent(transform, false);
            var ground = _world.AddComponent<BoxCollider>();
            ground.center = new Vector3(140f, -0.5f, 0f);
            ground.size = new Vector3(800f, 1f, 800f); // the lane and the district beside it
            foreach (Box3 b in _nav.Obstacles)
            {
                // The navigator widens obstacles by the walker's radius; the body has its own.
                float minX = b.Min.X + WalkNavigator.Radius, maxX = b.Max.X - WalkNavigator.Radius;
                float minZ = b.Min.Z + WalkNavigator.Radius, maxZ = b.Max.Z - WalkNavigator.Radius;
                float minY = Mathf.Max(0f, b.Min.Y + 1f), maxY = b.Max.Y;
                if (maxX - minX < 0.02f || maxZ - minZ < 0.02f || maxY - minY < 0.02f)
                {
                    continue;
                }

                var go = new GameObject("Wall");
                go.transform.SetParent(_world.transform, false);
                var box = go.AddComponent<BoxCollider>();
                box.center = new Vector3((minX + maxX) / 2f, (minY + maxY) / 2f, (minZ + maxZ) / 2f);
                box.size = new Vector3(maxX - minX, maxY - minY, maxZ - minZ);
            }
        }

        /// <summary>The scooter is out while the bazaar is (it's parked on the bazaar road), and always while riding.</summary>
        private void SyncScooter()
        {
            bool want = Riding || (_district != null && _district.Active);
            if (want && _scooter == null)
            {
                _scooterMaterials ??= new HomeMaterials();
                _scooterMesh = HouseMeshes.ToMesh(VehicleKit.Build(VehicleKind.Scooter, 2, rider: false), "PlayerScooter", out HousePaint[] paints);
                _scooter = new GameObject("PlayerScooter");
                _scooter.transform.SetParent(transform, false);
                _scooter.AddComponent<MeshFilter>().sharedMesh = _scooterMesh;
                var renderer = _scooter.AddComponent<MeshRenderer>();
                var materials = new Material[paints.Length];
                for (int i = 0; i < paints.Length; i++)
                {
                    materials[i] = _scooterMaterials.Get(paints[i], DistrictPalette.Colour(paints[i]));
                }

                renderer.sharedMaterials = materials;
                var box = _scooter.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0.55f, 0f);
                box.size = new Vector3(0.7f, 1.1f, 1.8f);
                _scooter.transform.SetPositionAndRotation(_parked, Quaternion.Euler(0f, _parkedHeading, 0f));
            }
            else if (!want && _scooter != null)
            {
                DestroyScooter();
            }
        }

        private void DestroyScooter()
        {
            if (_scooter != null)
            {
                Destroy(_scooter);
            }

            HouseMeshes.Destroy(_scooterMesh);
            _scooterMaterials?.Clear();
            _scooterMaterials = null;
            _scooter = null;
            _scooterMesh = null;
        }

        /// <summary>Onto the scooter: seated, the camera a little farther back, the engine running.</summary>
        public bool Mount()
        {
            if (!IsOpen || Riding || _scooter == null)
            {
                return false;
            }

            Riding = true;
            _ride = new ScooterState { Heading = _parkedHeading };
            _state = new LocomotionState { Facing = _parkedHeading, Grounded = true };
            _scooter.GetComponent<BoxCollider>().enabled = false;
            Quaternion facing = Quaternion.Euler(0f, _parkedHeading, 0f);
            Move(_parked - facing * Vector3.forward * SeatBack);
            _avatar.transform.rotation = facing;
            _animator.Riding = true;
            _animator.SetLocomotion(0f, true);
            _walkCameraDistance = _orbit.Distance;
            _orbit.Distance = Mathf.Max(_orbit.Distance, RideCameraDistance);
            _orbit.Yaw = _parkedHeading;
            if (Game.Services.TryGet(out AudioService audio))
            {
                if (_engineClip == null)
                {
                    const int rate = 44100;
                    float[] data = VehicleSounds.Engine(rate);
                    _engineClip = AudioClip.Create("scooter_engine", data.Length, 1, rate, false);
                    _engineClip.SetData(data, 0);
                }

                _engine = audio.Play(_engineClip, 0.22f, SoundPriority.Effect, loop: true);
            }

            PlaceScooter();
            StateChanged?.Invoke();
            return true;
        }

        /// <summary>Off the scooter: parked where it stands, upright, and the rider steps off to the side that's clear.</summary>
        public bool Dismount()
        {
            if (!Riding)
            {
                return false;
            }

            Riding = false;
            float h = _ride.Heading;
            Quaternion facing = Quaternion.Euler(0f, h, 0f);
            Vector3 seat = _avatar.transform.position;
            _parked = new Vector3(seat.x, 0f, seat.z) + facing * Vector3.forward * SeatBack;
            _parkedHeading = h;
            if (_scooter != null)
            {
                _scooter.transform.SetPositionAndRotation(_parked, facing);
                _scooter.GetComponent<BoxCollider>().enabled = true;
            }

            Vector3 left = facing * Vector3.left * 0.8f;
            Vector3 off = Walkable(seat + left) ? seat + left : Walkable(seat - left) ? seat - left : seat - facing * Vector3.forward * 0.8f;
            Move(off);
            _avatar.transform.rotation = facing;
            _state = new LocomotionState { Facing = h, Grounded = true };
            _animator.Riding = false;
            _animator.SetLocomotion(0f, true);
            _orbit.Distance = _walkCameraDistance > 0f ? _walkCameraDistance : OrbitCamera.DefaultDistance;
            _ride.Speed = 0f;
            if (Game.Services.TryGet(out AudioService audio))
            {
                audio.Stop(_engine, 0.2f);
            }

            _engine = VoiceHandle.None;
            StateChanged?.Invoke();
            return true;
        }

        /// <summary>Down from a roof to the street at the house's front door.</summary>
        private bool GoDown()
        {
            Vector3 at = _avatar.transform.position;
            DistrictBuilding b = _district != null ? _district.Plan.RoofAt(at.x, at.z, at.y) : null;
            if (b == null)
            {
                return false;
            }

            (float x, float z) = JobCatalog.DoorOf(b, JobCatalog.DoorStep);
            Teleport(new Vector3(x, 0f, z), JobCatalog.FacingYaw(b.Front));
            return true;
        }

        /// <summary>The horn: a beep, and people ahead in the way step aside.</summary>
        private void Horn()
        {
            Horns++;
            Vector3 at = _avatar.transform.position;
            if (Game.Services.TryGet(out CrowdView crowd) && crowd.Sim != null)
            {
                crowd.Sim.Horn(at.x, at.z, _ride.Heading);
            }

            if (Game.Services.TryGet(out AudioService audio))
            {
                if (_hornClip == null)
                {
                    const int rate = 44100;
                    float[] data = VehicleSounds.Horn(rate);
                    _hornClip = AudioClip.Create("scooter_horn", data.Length, 1, rate, false);
                    _hornClip.SetData(data, 0);
                }

                audio.Play(_hornClip, 0.4f, SoundPriority.Effect);
            }
        }

        private void RideStep(float dt)
        {
            float yaw = _orbit.Yaw * Mathf.Deg2Rad;
            Vector2 stick = Vector2.ClampMagnitude(Stick, 1f);
            float wx = stick.x * Mathf.Cos(yaw) + stick.y * Mathf.Sin(yaw);
            float wz = -stick.x * Mathf.Sin(yaw) + stick.y * Mathf.Cos(yaw);
            _ride = ScooterModel.Step(_ride, new ScooterInput(wx, wz), dt);

            float h = _ride.Heading * Mathf.Deg2Rad;
            var forward = new Vector3(Mathf.Sin(h), 0f, Mathf.Cos(h));
            Vector3 before = _avatar.transform.position;
            Vector3 want = forward * (_ride.Speed * dt);
            float sign = _ride.Speed >= 0f ? 1f : -1f, reach = sign > 0f ? NoseAhead : TailBehind;

            // The nose (or the tail, rolling back) must stay on open ground and clear of anything solid.
            Vector3 end = before + forward * (sign * reach) + want;
            bool blocked = !Walkable(end);
            if (!blocked && want.sqrMagnitude > 1e-8f
                && Physics.SphereCast(before + Vector3.up * 0.6f, 0.25f, forward * sign, out RaycastHit hit, reach - 0.25f + want.magnitude,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && hit.collider != _body)
            {
                blocked = true;
            }

            if (blocked)
            {
                _ride = ScooterModel.Bump(_ride, 0f);
                want = Vector3.zero;
            }

            _body.Move(want + Vector3.down * (2f * dt));
            Vector3 after = _avatar.transform.position;
            if (!Walkable(after))
            {
                Move(new Vector3(before.x, after.y, before.z));
                after = before;
            }

            float wanted = want.magnitude;
            if (wanted > 1e-4f)
            {
                float moved = new Vector2(after.x - before.x, after.z - before.z).magnitude;
                _ride = ScooterModel.Bump(_ride, moved / wanted);
            }

            _metres += new Vector2(after.x - before.x, after.z - before.z).magnitude;
            _avatar.transform.rotation = Quaternion.Euler(0f, _ride.Heading, -_ride.Lean);
            PlaceScooter();
            _animator.SetLocomotion(Speed, true);
            _orbit.Follow(_ride.Heading, Speed > 0.5f, dt, _autoRecentre);
            if (_engine.IsValid && Game.Services.TryGet(out AudioService audio))
            {
                audio.SetPitch(_engine, 0.75f + 1.1f * Mathf.Abs(_ride.Speed) / ScooterModel.MaxSpeed);
            }
        }

        private void PlaceScooter()
        {
            if (_scooter == null)
            {
                return;
            }

            Transform t = _avatar.transform;
            Quaternion r = t.rotation;
            _scooter.transform.SetPositionAndRotation(t.position + r * (Vector3.forward * SeatBack), r);
        }

        private void OnFootDown()
        {
            if (!_state.Grounded || !Game.Services.TryGet(out AudioService audio))
            {
                return;
            }

            int i = _step++ % _steps.Length;
            if (_steps[i] == null)
            {
                const int rate = 44100;
                float[] data = Footsteps.Generate(rate, i);
                _steps[i] = AudioClip.Create("footstep_" + i, data.Length, 1, rate, false);
                _steps[i].SetData(data, 0);
            }

            audio.Play(_steps[i], Gait == Gait.Sprint ? 0.42f : 0.3f, SoundPriority.Effect);
        }

        /// <summary>A night haze while exploring, so the far town fades into the dark instead of stopping at a tile's edge.</summary>
        private void Haze(bool on)
        {
            if (on)
            {
                _fog = (RenderSettings.fog, RenderSettings.fogColor, RenderSettings.fogMode, RenderSettings.fogDensity);
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Exponential;
                RenderSettings.fogColor = new Color(0.06f, 0.05f, 0.12f);
                RenderSettings.fogDensity = 0.012f;
            }
            else
            {
                RenderSettings.fog = _fog.on;
                RenderSettings.fogColor = _fog.colour;
                RenderSettings.fogMode = _fog.mode;
                RenderSettings.fogDensity = _fog.density;
            }
        }

        /// <summary>Explore plays in landscape; the rest of the game keeps its own orientation.</summary>
        private void Landscape(bool on)
        {
            if (Application.isEditor)
            {
                return;
            }

            if (on)
            {
                _orientation = Screen.orientation;
                _rotation = (Screen.autorotateToPortrait, Screen.autorotateToPortraitUpsideDown, Screen.autorotateToLandscapeLeft, Screen.autorotateToLandscapeRight);
                Screen.autorotateToPortrait = false;
                Screen.autorotateToPortraitUpsideDown = false;
                Screen.autorotateToLandscapeLeft = true;
                Screen.autorotateToLandscapeRight = true;
                Screen.orientation = ScreenOrientation.LandscapeLeft;
                Screen.orientation = ScreenOrientation.AutoRotation;
            }
            else
            {
                Screen.autorotateToPortrait = _rotation.p;
                Screen.autorotateToPortraitUpsideDown = _rotation.pu;
                Screen.autorotateToLandscapeLeft = _rotation.l;
                Screen.autorotateToLandscapeRight = _rotation.r;
                Screen.orientation = _orientation;
            }
        }
    }
}

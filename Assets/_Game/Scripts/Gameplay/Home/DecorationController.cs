using System;
using System.Collections.Generic;
using UnityEngine;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Home;
using UtsavNights.Core.Runtime;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Decoration mode in the 3D view (Phase 7 step 5, PRD 9.1 and 12): the camera turns to the house; an armed catalogue
    /// item follows the finger as a ghost tinted green or red (with the reason when red) and is placed on release; tapping
    /// a placed item selects it, dragging moves it, two fingers rotate and scale it; path items are drawn from one hook to
    /// another and re-hung by dragging an end. Every change is one undoable command on the <see cref="HomeService"/>.
    /// Touches over the mode's own UI are ignored (<see cref="IsOverUi"/>). The pointer methods are public, so tests drive
    /// exactly what a finger would.
    /// </summary>
    public sealed class DecorationController : MonoBehaviour
    {
        public enum View
        {
            House = 0,
            Courtyard = 1,
        }

        private const int IgnoreUntilLifted = -3;

        private static readonly Color Valid = new Color(0.3f, 1f, 0.45f);
        private static readonly Color Invalid = new Color(1f, 0.25f, 0.2f);
        private static readonly Color Selection = new Color(1f, 0.76f, 0.16f);

        private HomeService _home;
        private Camera _camera;
        private CameraRig _rig;
        private GameObject _ghost;
        private MeshFilter _ghostFilter;
        private MeshRenderer _ghostRenderer;
        private Material _validMaterial;
        private Material _invalidMaterial;
        private Material _selectionMaterial;
        private GameObject _ring;
        private readonly Dictionary<string, Mesh> _ghostMeshes = new Dictionary<string, Mesh>();
        private Mesh _pathMesh;

        private bool _down;
        private Vector2 _downAt;
        private Vector2 _lastAt;
        private bool _dragged;
        private bool _panning;
        private int _grabbedEnd = -1;
        private V3? _pathStart;
        private float _ghostYaw;
        private float _ghostScale = 1f;
        private int _touchId = -1;
        private float _twistStart;
        private float _pinchStart;
        private float _gestureYaw;
        private float _gestureScale = 1f;
        private int _editsThisVisit;

        public event Action StateChanged;

        public bool IsOpen { get; private set; }

        public View CurrentView { get; private set; }

        /// <summary>The catalogue item being placed, or null.</summary>
        public string Armed { get; private set; }

        public int ArmedVariant { get; private set; }

        /// <summary>The selected decoration's uid, or 0.</summary>
        public int Selected { get; private set; }

        /// <summary>The last check of the ghost (null when nothing is being placed or moved).</summary>
        public PlacementResult Preview { get; private set; }

        /// <summary>A hint or the reason the ghost is red.</summary>
        public string Message { get; private set; } = string.Empty;

        public float Pan { get; private set; }

        public float Height { get; private set; } = 1.6f;

        public float Distance { get; private set; } = 4.6f;

        /// <summary>Screen points over the mode's buttons and drawers; set by the UI.</summary>
        public Func<Vector2, bool> IsOverUi { get; set; } = _ => false;

        public GameObject Ghost => _ghost;

        public bool PathStarted => _pathStart.HasValue;

        public void Configure(Camera cameraToDrive, CameraRig rig)
        {
            _camera = cameraToDrive;
            _rig = rig;
        }

        public bool Open()
        {
            if (IsOpen || !Game.Services.TryGet(out _home))
            {
                return false;
            }

            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_rig == null)
            {
                _rig = FindFirstObjectByType<CameraRig>();
            }

            TouchInput.EnsureEnabled();
            EnsureGhost();
            IsOpen = true;
            _editsThisVisit = 0;
            CurrentView = View.House;
            Pan = 0f;
            Height = 1.6f;
            Distance = 4.6f;
            Armed = null;
            Selected = 0;
            _home.Editor.Changed += OnEdited;
            CameraPose(out Vector3 p, out Quaternion r);
            _rig?.BeginOverride(p, r);
            _home.House?.SetLightsEnabled(true);
            Message = "Pick a decoration below, or tap one on the house to move it.";
            Game.Events.Publish(new DecorModeOpenedEvent(_home.Design.decorations.Count));
            StateChanged?.Invoke();
            return true;
        }

        /// <summary>Leaves decoration mode. Done saves house.json (and starts a fresh journal); the camera turns back.</summary>
        public void Close(bool save = true)
        {
            if (!IsOpen)
            {
                return;
            }

            Disarm();
            Deselect();
            bool saved = save && _home.Editor.Unsaved && _home.Save();
            _home.Editor.Changed -= OnEdited;
            _rig?.EndOverride();
            IsOpen = false;
            Game.Events.Publish(new DecorModeClosedEvent(_editsThisVisit, saved, _home.Design.decorations.Count));
            StateChanged?.Invoke();
        }

        public void Arm(string itemId, int variant = 0)
        {
            DecorInfo info = HomeCatalog.Decoration(itemId);
            if (!IsOpen || info == null)
            {
                return;
            }

            Deselect();
            Armed = itemId;
            ArmedVariant = Mathf.Clamp(variant, 0, info.Variants - 1);
            _ghostYaw = 0f;
            _ghostScale = 1f;
            _pathStart = null;
            Preview = null;
            HideGhost();
            if (info.Mode == PlacementMode.Path)
            {
                SetView(View.House);
                Message = $"Tap where the {Lower(info.Name)} starts, then where it ends.";
            }
            else
            {
                if (info.Mode == PlacementMode.Decal && CurrentView == View.House)
                {
                    SetView(View.Courtyard);
                }

                Message = $"Touch where the {Lower(info.Name)} should go.";
            }

            StateChanged?.Invoke();
        }

        public void Disarm()
        {
            Armed = null;
            _pathStart = null;
            Preview = null;
            HideGhost();
            StateChanged?.Invoke();
        }

        public void Select(int uid)
        {
            PlacedDecor d = _home?.Design.Find(uid);
            if (d == null)
            {
                Deselect();
                return;
            }

            Armed = null;
            Selected = uid;
            _gestureYaw = 0f;
            _gestureScale = 1f;
            Message = HomeCatalog.Decoration(d.item)?.Name ?? string.Empty;
            ShowSelectionRing(d);
            StateChanged?.Invoke();
        }

        public void Deselect()
        {
            if (Selected != 0)
            {
                SetDecorVisible(Selected, true);
            }

            Selected = 0;
            Preview = null;
            HideGhost();
            if (_ring != null)
            {
                _ring.SetActive(false);
            }

            StateChanged?.Invoke();
        }

        public void SetView(View view)
        {
            CurrentView = view;
            Pan = 0f;
            Height = view == View.House ? 1.6f : 2.6f;
            Distance = view == View.House ? 4.6f : 3.2f;
            UpdateCamera();
            StateChanged?.Invoke();
        }

        /// <summary>Moves the camera: x across the house, y up (house view) or along the courtyard (courtyard view).</summary>
        public void PanBy(float dx, float dy)
        {
            Pan = Mathf.Clamp(Pan + dx, -2.4f, 2.4f);
            Height = CurrentView == View.House ? Mathf.Clamp(Height + dy, 1.2f, 4.6f) : Mathf.Clamp(Height + dy, 1.2f, 4.4f);
            UpdateCamera();
        }

        public void ZoomBy(float factor)
        {
            Distance = CurrentView == View.House ? Mathf.Clamp(Distance / factor, 2.6f, 5.6f) : Mathf.Clamp(Distance / factor, 2f, 4.2f);
            UpdateCamera();
        }

        // ---- Pointer (one finger) ----

        public void PointerDown(Vector2 screen)
        {
            if (!IsOpen || IsOverUi(screen))
            {
                return;
            }

            _down = true;
            _dragged = false;
            _panning = false;
            _grabbedEnd = -1;
            _downAt = _lastAt = screen;
            if (Armed != null)
            {
                UpdateGhost(screen);
                return;
            }

            if (Selected != 0)
            {
                PlacedDecor d = _home.Design.Find(Selected);
                DecorInfo info = HomeCatalog.Decoration(d?.item);
                if (info != null && info.Mode == PlacementMode.Path)
                {
                    _grabbedEnd = NearestEnd(d, screen);
                    if (_grabbedEnd >= 0)
                    {
                        return;
                    }
                }
                else if (d != null && Pick(screen) == Selected)
                {
                    return;
                }
            }

            int picked = Pick(screen);
            if (picked != 0)
            {
                Select(picked);
                return;
            }

            _panning = true;
        }

        public void PointerMove(Vector2 screen)
        {
            if (!IsOpen || !_down)
            {
                return;
            }

            if ((screen - _downAt).magnitude > Screen.dpi * 0.05f + 4f)
            {
                _dragged = true;
            }

            Vector2 delta = screen - _lastAt;
            _lastAt = screen;
            if (Armed != null)
            {
                UpdateGhost(screen);
            }
            else if (Selected != 0 && !_panning && _dragged)
            {
                PreviewMove(screen);
            }
            else if (_panning && _dragged)
            {
                float scale = 3.5f / Mathf.Max(1f, Screen.width);
                PanBy(-delta.x * scale, -delta.y * scale);
            }
        }

        public void PointerUp(Vector2 screen)
        {
            if (!IsOpen || !_down)
            {
                return;
            }

            _down = false;
            if (Armed != null)
            {
                Release(screen);
            }
            else if (Selected != 0 && !_panning && _dragged)
            {
                CommitMove(screen);
            }
            else if (_panning && !_dragged && Selected != 0)
            {
                Deselect();
            }

            _panning = false;
            _grabbedEnd = -1;
        }

        // ---- Two fingers ----

        /// <summary>Rotates the ghost or the selected item by a twist (degrees); committed when the fingers lift.</summary>
        public void Twist(float degrees)
        {
            if (Armed != null)
            {
                _ghostYaw += degrees;
                if (Preview?.Pose != null)
                {
                    RecheckGhost();
                }
            }
            else if (Selected != 0)
            {
                _gestureYaw += degrees;
                PreviewGesture();
            }
        }

        /// <summary>Scales the ghost or the selected item by a pinch factor, within the item's limits.</summary>
        public void Pinch(float factor)
        {
            if (Armed != null)
            {
                _ghostScale *= factor;
                if (Preview?.Pose != null)
                {
                    RecheckGhost();
                }
            }
            else if (Selected != 0)
            {
                _gestureScale *= factor;
                PreviewGesture();
            }
            else
            {
                ZoomBy(factor);
            }
        }

        public void EndGesture()
        {
            if (Selected != 0 && (Mathf.Abs(_gestureYaw) > 0.01f || Mathf.Abs(_gestureScale - 1f) > 0.001f))
            {
                PlacementResult r = GestureResult();
                if (r != null && r.Ok)
                {
                    Commit(new PoseDecor(Selected, r.Pose));
                }
                else if (r != null)
                {
                    Message = r.Reason;
                }

                _gestureYaw = 0f;
                _gestureScale = 1f;
                SetDecorVisible(Selected, true);
                HideGhost();
                ShowSelectionRing(_home.Design.Find(Selected));
                StateChanged?.Invoke();
            }
        }

        // ---- Buttons for the selected item ----

        /// <summary>Turns the selected item 15 degrees (the one-finger way to rotate).</summary>
        public bool RotateSelected(float degrees = DecorPlacement.YawStep)
        {
            PlacedDecor d = _home?.Design.Find(Selected);
            if (d == null || HomeCatalog.Decoration(d.item).Mode == PlacementMode.Path || DecorPlacement.Hangs(HomeCatalog.Decoration(d.item)))
            {
                return false;
            }

            PlacedDecor pose = d.Clone();
            pose.yaw = HouseDesign.NormaliseYaw(Mathf.Round((d.yaw + degrees) / DecorPlacement.YawStep) * DecorPlacement.YawStep);
            return TryCommitPose(pose);
        }

        public bool ScaleSelected(float factor)
        {
            PlacedDecor d = _home?.Design.Find(Selected);
            DecorInfo info = HomeCatalog.Decoration(d?.item);
            if (d == null || info.Mode == PlacementMode.Path)
            {
                return false;
            }

            PlacedDecor pose = d.Clone();
            pose.scale = Mathf.Clamp(d.scale * factor, HomeCatalog.MinScale, info.MaxScale);
            if (DecorPlacement.Hangs(info))
            {
                // Keep the hook where it is: the item grows downward and outward.
                pose.y = d.y + info.Height * (d.scale - pose.scale);
            }

            return Mathf.Abs(pose.scale - d.scale) > 1e-4f && TryCommitPose(pose);
        }

        public bool RecolourSelected()
        {
            PlacedDecor d = _home?.Design.Find(Selected);
            if (d == null)
            {
                return false;
            }

            PlacedDecor pose = d.Clone();
            pose.variant = (d.variant + 1) % HomeCatalog.Decoration(d.item).Variants;
            return Commit(new PoseDecor(d.uid, pose));
        }

        /// <summary>Duplicates the selected item beside it, trying a few offsets until one is valid.</summary>
        public bool DuplicateSelected()
        {
            PlacedDecor d = _home?.Design.Find(Selected);
            DecorInfo info = HomeCatalog.Decoration(d?.item);
            if (d == null)
            {
                return false;
            }

            float step = Mathf.Max(info.Width, 0.3f) * d.scale + 0.05f;
            foreach (Vector2 o in new[] { new Vector2(step, 0f), new Vector2(-step, 0f), new Vector2(0f, step), new Vector2(0f, -step), new Vector2(0f, 0.3f) })
            {
                PlacedDecor copy = d.Clone();
                copy.uid = _home.Design.nextUid;
                bool path = info.Mode == PlacementMode.Path;
                copy.x += o.x;
                copy.ex += o.x;
                if (path)
                {
                    copy.y -= o.y;
                    copy.ey -= o.y;
                }
                else
                {
                    copy.z += o.y;
                }

                PlacementResult r = DecorPlacement.Check(_home.Context, copy);
                if (r.Ok && Commit(new PlaceDecor(copy)))
                {
                    Select(copy.uid);
                    return true;
                }

                Message = r.Reason;
            }

            StateChanged?.Invoke();
            return false;
        }

        public bool DeleteSelected()
        {
            int uid = Selected;
            if (uid == 0)
            {
                return false;
            }

            Deselect();
            return Commit(new RemoveDecor(uid));
        }

        public bool Undo()
        {
            Deselect();
            bool ok = _home != null && _home.Editor.Undo();
            if (ok)
            {
                _editsThisVisit++;
            }

            return ok;
        }

        public bool Redo()
        {
            Deselect();
            bool ok = _home != null && _home.Editor.Redo();
            if (ok)
            {
                _editsThisVisit++;
            }

            return ok;
        }

        /// <summary>House tab: swap a module, change the paint. One undo step each.</summary>
        public bool Apply(IHouseCommand command)
        {
            bool ok = Commit(command);
            if (ok && command is SetModule)
            {
                foreach (PlacementResult broken in DecorPlacement.Audit(_home.Context))
                {
                    Message = $"Some decorations no longer fit: {broken.Reason}";
                    break;
                }
            }

            return ok;
        }

        /// <summary>The world point a screen point looks at on the house's surfaces (tests aim with it).</summary>
        public SurfaceHit? Raycast(Vector2 screen)
        {
            if (_camera == null || _home == null)
            {
                return null;
            }

            Ray ray = _camera.ScreenPointToRay(screen);
            return DecorPlacement.Raycast(_home.Context, ToHouse(ray.origin), new V3(ray.direction.x, ray.direction.y, ray.direction.z));
        }

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            UpdateCamera();
            var touches = ETouch.activeTouches;
            if (touches.Count >= 2)
            {
                TwoFingers(touches[0], touches[1]);
                return;
            }

            if (_pinchStart != 0f)
            {
                // The gesture ended: commit it, and ignore the finger left on the glass until it lifts too.
                _twistStart = _pinchStart = 0f;
                EndGesture();
                _touchId = touches.Count > 0 ? IgnoreUntilLifted : -1;
            }

            if (_touchId == IgnoreUntilLifted)
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

        private void TwoFingers(ETouch a, ETouch b)
        {
            if (_touchId >= 0)
            {
                // A second finger turns a drag into a gesture: drop the drag without committing it.
                _touchId = -1;
                _down = false;
                _panning = false;
                if (Selected != 0)
                {
                    SetDecorVisible(Selected, true);
                    HideGhost();
                }
            }

            Vector2 d = b.screenPosition - a.screenPosition;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, span = d.magnitude;
            if (_pinchStart == 0f)
            {
                _twistStart = angle;
                _pinchStart = Mathf.Max(1f, span);
                return;
            }

            float twist = Mathf.DeltaAngle(_twistStart, angle);
            float pinch = span / _pinchStart;
            _twistStart = angle;
            _pinchStart = Mathf.Max(1f, span);
            Twist(-twist);
            Pinch(pinch);
        }

        // ---- Placing ----

        private void UpdateGhost(Vector2 screen)
        {
            SurfaceHit? hit = Raycast(screen);
            DecorInfo info = HomeCatalog.Decoration(Armed);
            if (!hit.HasValue)
            {
                Preview = null;
                Message = "Point at the house, the walls or the courtyard floor.";
                HideGhost();
                StateChanged?.Invoke();
                return;
            }

            if (info.Mode == PlacementMode.Path)
            {
                if (!_pathStart.HasValue)
                {
                    bool ok = (hit.Value.Patch.Kind & info.Surfaces) != 0;
                    Preview = new PlacementResult(null, ok ? PlacementProblem.None : PlacementProblem.WrongSurface, ok ? string.Empty : DecorPlacement.Reason(PlacementProblem.WrongSurface, info));
                    ShowMarker(hit.Value.Point, ok);
                }
                else
                {
                    var start = new SurfaceHit(Find(_pathStart.Value), _pathStart.Value, 0f);
                    Preview = DecorPlacement.PathBetween(_home.Context, Armed, start, hit.Value, 0.25f, ArmedVariant);
                    ShowGhost(Preview);
                }
            }
            else
            {
                Preview = DecorPlacement.PlaceAt(_home.Context, Armed, hit.Value, _ghostYaw, _ghostScale, ArmedVariant);
                _ghostScale = Preview.Pose?.scale ?? _ghostScale;
                ShowGhost(Preview);
            }

            Message = Preview.Ok ? string.Empty : Preview.Reason;
            StateChanged?.Invoke();
        }

        private void RecheckGhost()
        {
            PlacedDecor pose = Preview.Pose.Clone();
            DecorInfo info = HomeCatalog.Decoration(pose.item);
            pose.yaw = DecorPlacement.Hangs(info) ? pose.yaw : HouseDesign.NormaliseYaw(Mathf.Round(_ghostYaw / DecorPlacement.YawStep) * DecorPlacement.YawStep);
            _ghostScale = Mathf.Clamp(_ghostScale, HomeCatalog.MinScale, info.MaxScale);
            pose.scale = _ghostScale;
            Preview = DecorPlacement.Check(_home.Context, pose);
            ShowGhost(Preview);
            Message = Preview.Ok ? string.Empty : Preview.Reason;
            StateChanged?.Invoke();
        }

        private void Release(Vector2 screen)
        {
            DecorInfo info = HomeCatalog.Decoration(Armed);
            UpdateGhost(screen);
            if (Preview == null)
            {
                return;
            }

            if (info.Mode == PlacementMode.Path && !_pathStart.HasValue)
            {
                if (Preview.Ok)
                {
                    SurfaceHit? hit = Raycast(screen);
                    _pathStart = hit?.Point;
                    Message = $"Now tap where the {Lower(info.Name)} ends.";
                    StateChanged?.Invoke();
                }

                return;
            }

            if (Preview.Ok)
            {
                PlacedDecor pose = Preview.Pose.Clone();
                pose.uid = _home.Design.nextUid;
                if (Commit(new PlaceDecor(pose)))
                {
                    Message = info.Mode == PlacementMode.Path ? "Hung. Tap again to hang another." : "Placed. Touch again to place another.";
                }
            }

            _pathStart = null;
            HideGhost();
            Preview = null;
            StateChanged?.Invoke();
        }

        // ---- Moving ----

        private void PreviewMove(Vector2 screen)
        {
            PlacedDecor d = _home.Design.Find(Selected);
            DecorInfo info = HomeCatalog.Decoration(d?.item);
            SurfaceHit? hit = Raycast(screen);
            if (d == null || !hit.HasValue)
            {
                return;
            }

            if (info.Mode == PlacementMode.Path)
            {
                if (_grabbedEnd < 0)
                {
                    return;
                }

                var a = new V3(d.x, d.y, d.z);
                var b = new V3(d.ex, d.ey, d.ez);
                V3 fixedEnd = _grabbedEnd == 0 ? b : a;
                Preview = DecorPlacement.PathBetween(_home.Context, d.item, hit.Value, new SurfaceHit(Find(fixedEnd), fixedEnd, 0f), d.sag, d.variant, d.uid);
                if (_grabbedEnd == 1 && Preview.Pose != null)
                {
                    Swap(Preview.Pose);
                }
            }
            else
            {
                Preview = DecorPlacement.PlaceAt(_home.Context, d.item, hit.Value, d.yaw, d.scale, d.variant, d.uid);
            }

            SetDecorVisible(Selected, false);
            ShowGhost(Preview);
            Message = Preview.Ok ? string.Empty : Preview.Reason;
            StateChanged?.Invoke();
        }

        private void CommitMove(Vector2 screen)
        {
            PreviewMove(screen);
            SetDecorVisible(Selected, true);
            HideGhost();
            if (Preview != null && Preview.Ok)
            {
                Commit(new PoseDecor(Selected, Preview.Pose));
            }
            else if (Preview != null)
            {
                Message = Preview.Reason;
            }

            Preview = null;
            ShowSelectionRing(_home.Design.Find(Selected));
            StateChanged?.Invoke();
        }

        private void PreviewGesture()
        {
            PlacementResult r = GestureResult();
            if (r == null)
            {
                return;
            }

            SetDecorVisible(Selected, false);
            ShowGhost(r);
            Message = r.Ok ? string.Empty : r.Reason;
            StateChanged?.Invoke();
        }

        private PlacementResult GestureResult()
        {
            PlacedDecor d = _home?.Design.Find(Selected);
            DecorInfo info = HomeCatalog.Decoration(d?.item);
            if (d == null || info.Mode == PlacementMode.Path)
            {
                return null;
            }

            PlacedDecor pose = d.Clone();
            if (!DecorPlacement.Hangs(info))
            {
                pose.yaw = HouseDesign.NormaliseYaw(Mathf.Round((d.yaw + _gestureYaw) / DecorPlacement.YawStep) * DecorPlacement.YawStep);
            }

            pose.scale = Mathf.Clamp(d.scale * _gestureScale, HomeCatalog.MinScale, info.MaxScale);
            if (DecorPlacement.Hangs(info))
            {
                pose.y = d.y + info.Height * (d.scale - pose.scale);
            }

            return DecorPlacement.Check(_home.Context, pose);
        }

        private bool TryCommitPose(PlacedDecor pose)
        {
            PlacementResult r = DecorPlacement.Check(_home.Context, pose);
            if (!r.Ok)
            {
                Message = r.Reason;
                StateChanged?.Invoke();
                return false;
            }

            return Commit(new PoseDecor(pose.uid, pose));
        }

        private bool Commit(IHouseCommand command)
        {
            if (_home == null || !_home.Do(command))
            {
                return false;
            }

            _editsThisVisit++;
            if (Selected != 0)
            {
                ShowSelectionRing(_home.Design.Find(Selected));
            }

            StateChanged?.Invoke();
            return true;
        }

        private void OnEdited()
        {
            if (Selected != 0 && _home.Design.Find(Selected) == null)
            {
                Deselect();
            }
        }

        // ---- Picking ----

        /// <summary>The decoration under a screen point: standing and hung items by their colliders, then rangoli and strands.</summary>
        private int Pick(Vector2 screen)
        {
            Ray ray = _camera.ScreenPointToRay(screen);
            if (Physics.Raycast(ray, out RaycastHit physical, 50f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                physical.collider.transform.parent == _home.Decor.transform)
            {
                return UidOf(physical.collider.gameObject.name);
            }

            SurfaceHit? hit = Raycast(screen);
            int best = 0;
            float bestDistance = 0.18f;
            foreach (PlacedDecor d in _home.Design.decorations)
            {
                DecorInfo info = HomeCatalog.Decoration(d.item);
                if (info == null)
                {
                    continue;
                }

                if (info.Mode == PlacementMode.Decal && hit.HasValue)
                {
                    float r = Mathf.Max(info.Width, info.Depth) * d.scale * 0.5f;
                    float dist = new Vector2(hit.Value.Point.X - d.x, hit.Value.Point.Z - d.z).magnitude;
                    if (dist < r && best == 0)
                    {
                        best = d.uid;
                    }
                }
                else if (info.Mode == PlacementMode.Path)
                {
                    for (int i = 0; i <= 16; i++)
                    {
                        V3 p = DecorGenerator.PathPoint(new V3(d.x, d.y, d.z), new V3(d.ex, d.ey, d.ez), d.sag, i / 16f);
                        float dist = DistanceToRay(ray, ToWorld(p));
                        if (dist < bestDistance)
                        {
                            bestDistance = dist;
                            best = d.uid;
                        }
                    }
                }
            }

            return best;
        }

        private int NearestEnd(PlacedDecor d, Vector2 screen)
        {
            Vector3 a = _camera.WorldToScreenPoint(ToWorld(new V3(d.x, d.y, d.z)));
            Vector3 b = _camera.WorldToScreenPoint(ToWorld(new V3(d.ex, d.ey, d.ez)));
            float reach = Mathf.Max(60f, Screen.width * 0.08f);
            float da = Vector2.Distance(a, screen), db = Vector2.Distance(b, screen);
            return da < reach && da <= db ? 0 : db < reach ? 1 : -1;
        }

        private static int UidOf(string objectName)
        {
            int hash = objectName.LastIndexOf('#');
            return hash >= 0 && int.TryParse(objectName.Substring(hash + 1), out int uid) ? uid : 0;
        }

        private static float DistanceToRay(Ray ray, Vector3 p) => Vector3.Cross(ray.direction, p - ray.origin).magnitude;

        private SurfacePatch Find(V3 point)
        {
            foreach (SurfacePatch patch in _home.Context.Surfaces)
            {
                if (patch.Holds(point, 0.02f, 0.02f))
                {
                    return patch;
                }
            }

            return _home.Context.Surfaces[0];
        }

        private static void Swap(PlacedDecor pose)
        {
            (pose.x, pose.ex) = (pose.ex, pose.x);
            (pose.y, pose.ey) = (pose.ey, pose.y);
            (pose.z, pose.ez) = (pose.ez, pose.z);
        }

        // ---- Camera ----

        private void UpdateCamera()
        {
            if (_rig == null || !IsOpen)
            {
                return;
            }

            CameraPose(out Vector3 p, out Quaternion r);
            _rig.SetOverridePose(p, r);
        }

        /// <summary>House view looks at the front from the courtyard; courtyard view looks down at the floor from above the step.</summary>
        private void CameraPose(out Vector3 position, out Quaternion rotation)
        {
            Vector3 eye, target;
            if (CurrentView == View.House)
            {
                eye = new Vector3(Pan, Height, Distance);
                target = new Vector3(Pan * 0.85f, Height + 0.3f, 0f);
            }
            else
            {
                eye = new Vector3(Pan, Height + 1.6f, 0.6f);
                target = new Vector3(Pan, 0f, 0.6f + Distance);
            }

            position = ToWorld(eye);
            rotation = Quaternion.LookRotation(target - eye, Vector3.up);
        }

        private static Vector3 ToWorld(Vector3 house) => house + new Vector3(0f, 0f, HouseGeometry.WorldFrontZ);

        private static Vector3 ToWorld(V3 house) => new Vector3(house.X, house.Y, house.Z + HouseGeometry.WorldFrontZ);

        private static V3 ToHouse(Vector3 world) => new V3(world.x, world.y, world.z - HouseGeometry.WorldFrontZ);

        // ---- Ghost and selection ----

        private void EnsureGhost()
        {
            if (_ghost != null)
            {
                return;
            }

            _validMaterial = Tint("DecorGhost_Valid", Valid);
            _invalidMaterial = Tint("DecorGhost_Invalid", Invalid);
            _selectionMaterial = Tint("DecorSelection", Selection);
            _ghost = new GameObject("DecorGhost");
            _ghost.transform.SetParent(transform, false);
            _ghostFilter = _ghost.AddComponent<MeshFilter>();
            _ghostRenderer = _ghost.AddComponent<MeshRenderer>();
            _ghostRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ghost.SetActive(false);

            var ringMesh = new HouseMesh();
            ringMesh.FlatRing(0f, 0f, 0.5f, 0.56f, 32, 0.01f, HousePaint.Flame);
            _ring = new GameObject("DecorSelectionRing");
            _ring.transform.SetParent(transform, false);
            _ring.AddComponent<MeshFilter>().sharedMesh = HouseMeshes.ToMesh(ringMesh, "DecorSelectionRing", out _);
            _ring.AddComponent<MeshRenderer>().sharedMaterial = _selectionMaterial;
            _ring.SetActive(false);
        }

        private static Material Tint(string name, Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            m.SetColor("_BaseColor", c * 0.6f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * 0.9f);
            return m;
        }

        private void ShowGhost(PlacementResult r)
        {
            if (r?.Pose == null || _ghost == null)
            {
                HideGhost();
                return;
            }

            PlacedDecor pose = r.Pose;
            DecorInfo info = HomeCatalog.Decoration(pose.item);
            Mesh mesh;
            if (info.Mode == PlacementMode.Path)
            {
                HouseMesh data = DecorGenerator.Path(pose.item, new V3(pose.x, pose.y, pose.z), new V3(pose.ex, pose.ey, pose.ez), pose.sag);
                if (data == null)
                {
                    HideGhost();
                    return;
                }

                HouseMeshes.Destroy(_pathMesh);
                mesh = _pathMesh = HouseMeshes.ToMesh(data, "DecorGhostPath", out _);
                _ghost.transform.position = ToWorld(Vector3.zero);
                _ghost.transform.rotation = Quaternion.identity;
                _ghost.transform.localScale = Vector3.one;
            }
            else
            {
                if (!_ghostMeshes.TryGetValue(pose.item, out mesh))
                {
                    mesh = HouseMeshes.ToMesh(DecorGenerator.Item(pose.item), "DecorGhost_" + pose.item, out _);
                    _ghostMeshes[pose.item] = mesh;
                }

                _ghost.transform.position = ToWorld(new V3(pose.x, pose.y, pose.z));
                _ghost.transform.rotation = Quaternion.Euler(0f, pose.yaw, 0f);
                _ghost.transform.localScale = Vector3.one * pose.scale;
            }

            _ghostFilter.sharedMesh = mesh;
            var materials = new Material[mesh.subMeshCount];
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = r.Ok ? _validMaterial : _invalidMaterial;
            }

            _ghostRenderer.sharedMaterials = materials;
            _ghost.SetActive(true);
        }

        private void ShowMarker(V3 point, bool ok)
        {
            if (_ring == null)
            {
                return;
            }

            _ring.transform.position = ToWorld(point);
            _ring.transform.rotation = Quaternion.identity;
            _ring.transform.localScale = Vector3.one * 0.25f;
            _ring.GetComponent<MeshRenderer>().sharedMaterial = ok ? _validMaterial : _invalidMaterial;
            _ring.SetActive(true);
        }

        private void ShowSelectionRing(PlacedDecor d)
        {
            if (_ring == null || d == null)
            {
                return;
            }

            DecorInfo info = HomeCatalog.Decoration(d.item);
            bool path = info.Mode == PlacementMode.Path;
            V3 at = path ? DecorGenerator.PathPoint(new V3(d.x, d.y, d.z), new V3(d.ex, d.ey, d.ez), d.sag, 0.5f) : new V3(d.x, d.y, d.z);
            _ring.transform.position = ToWorld(at);
            _ring.transform.rotation = Quaternion.identity;
            _ring.transform.localScale = Vector3.one * Mathf.Max(0.3f, path ? 0.4f : Mathf.Max(info.Width, info.Depth) * d.scale * 1.1f);
            _ring.GetComponent<MeshRenderer>().sharedMaterial = _selectionMaterial;
            _ring.SetActive(true);
        }

        private void HideGhost()
        {
            if (_ghost != null)
            {
                _ghost.SetActive(false);
            }

            if (_ring != null && Selected == 0)
            {
                _ring.SetActive(false);
            }
        }

        private void SetDecorVisible(int uid, bool visible)
        {
            GameObject go = _home?.Decor != null ? _home.Decor.ObjectFor(uid) : null;
            if (go != null)
            {
                go.GetComponent<MeshRenderer>().enabled = visible;
            }
        }

        private void OnDestroy()
        {
            if (IsOpen)
            {
                Close(save: false);
            }

            foreach (Mesh m in _ghostMeshes.Values)
            {
                HouseMeshes.Destroy(m);
            }

            HouseMeshes.Destroy(_pathMesh);
            HouseMeshes.Destroy(_validMaterial);
            HouseMeshes.Destroy(_invalidMaterial);
            HouseMeshes.Destroy(_selectionMaterial);
        }

        private static string Lower(string name) => name.Length > 1 && char.IsUpper(name[0]) && !char.IsUpper(name[1]) ? char.ToLowerInvariant(name[0]) + name.Substring(1) : name;
    }
}

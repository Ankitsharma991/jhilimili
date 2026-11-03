using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Home;
using UtsavNights.Core.Runtime;

namespace UtsavNights.Gameplay
{
    /// <summary>The player went in or came out (analytics in step 5).</summary>
    public readonly struct InteriorEvent
    {
        public InteriorEvent(string room, bool entered, int decorations)
        {
            Room = room;
            Entered = entered;
            Decorations = decorations;
        }

        public string Room { get; }

        public bool Entered { get; }

        public int Decorations { get; }
    }

    /// <summary>
    /// Interiors (Phase 11 step 2, F25). Going in fades out, hides the outside (the house, its decorations, the
    /// neighbourhood and its ambient life, the courtyard avatar, the moon), builds the room away from the courtyard and fades
    /// back in; coming out destroys the room and brings the outside back. Only one of the two is ever drawn, and the
    /// room with its decorations stays inside its tier's triangle budget, so memory and draw cost stay bounded.
    /// Decorations inside are house edits like any other (undo journal, house.json), with their room set.
    /// </summary>
    public sealed class InteriorController : MonoBehaviour
    {
        /// <summary>Rooms are built here, far below the courtyard, so nothing outside can show through or collide.</summary>
        public static readonly Vector3 Origin = new Vector3(0f, -60f, 0f);

        public const float FadeSeconds = 0.35f;
        private const float EyeHeight = 1.45f;
        private const float MaxYaw = 55f;

        private readonly List<GameObject> _hidden = new List<GameObject>();
        private readonly List<Light> _lights = new List<Light>();
        private Camera _camera;
        private CameraRig _rig;
        private HomeService _home;
        private GameObject _root;
        private DecorView _decor;
        private HomeMaterials _materials;
        private Mesh _mesh;
        private Color _ambient;
        private float _yaw;
        private float _pitch = -8f;
        private bool _busy;

        public event Action StateChanged;

        public bool IsInside => Room != null;

        public RoomDef Room { get; private set; }

        /// <summary>0 to 1: how dark the fade is.</summary>
        public float Fade { get; private set; }

        public bool Busy => _busy;

        public string Message { get; private set; } = string.Empty;

        /// <summary>Triangles drawn inside now (the room and its decorations).</summary>
        public int Triangles { get; private set; }

        public void Configure(Camera cameraToDrive, CameraRig rig)
        {
            _camera = cameraToDrive;
            _rig = rig;
        }

        /// <summary>Goes into a room, or moves to another one when already inside. False when it can't right now (and why in <see cref="Message"/>).</summary>
        public bool Enter(string roomId)
        {
            RoomDef room = Rooms.Find(roomId);
            if (room == null || _busy || !Game.Services.TryGet(out _home))
            {
                return false;
            }

            if (_home.Visiting)
            {
                Message = "You can only go inside your own home.";
                return false;
            }

            WalkController walk = FindFirstObjectByType<WalkController>();
            DecorationController decorating = FindFirstObjectByType<DecorationController>();
            if ((walk != null && walk.IsOpen) || (decorating != null && decorating.IsOpen))
            {
                Message = "Finish walking or decorating first.";
                return false;
            }

            StartCoroutine(Go(room));
            return true;
        }

        /// <summary>Back outside.</summary>
        public bool Leave()
        {
            if (!IsInside || _busy)
            {
                return false;
            }

            StartCoroutine(Go(null));
            return true;
        }

        /// <summary>Turns the view (drag to look); the eye stays where it is, by the door.</summary>
        public void Look(float yawDegrees, float pitchDegrees)
        {
            if (!IsInside)
            {
                return;
            }

            _yaw = Mathf.Clamp(_yaw + yawDegrees, -MaxYaw, MaxYaw);
            _pitch = Mathf.Clamp(_pitch + pitchDegrees, -35f, 20f);
            Pose(out Vector3 p, out Quaternion r);
            _rig?.SetOverridePose(p, r);
        }

        /// <summary>What a tap at a screen point would land on: a surface, or a placed item (its uid).</summary>
        public bool Tap(Vector2 screen, out RoomHit hit, out int itemUid)
        {
            hit = default;
            itemUid = 0;
            if (!IsInside || _camera == null)
            {
                return false;
            }

            Ray ray = _camera.ScreenPointToRay(screen);
            Physics.SyncTransforms(); // an item placed this frame can be picked at once
            if (!Physics.Raycast(ray, out RaycastHit h, 15f))
            {
                return false;
            }

            string name = h.collider.gameObject.name;
            int hash = name.IndexOf('#');
            if (hash > 0 && int.TryParse(name.Substring(hash + 1), out int uid) && h.collider.transform.IsChildOf(_root.transform))
            {
                itemUid = uid;
                return true;
            }

            RoomSurfaceMarker marker = h.collider.GetComponent<RoomSurfaceMarker>();
            if (marker == null)
            {
                return false;
            }

            Vector3 local = h.point - Origin;
            var point = new V3(local.x, local.y, local.z);
            if (marker.Surface == Surface.Step)
            {
                if (h.normal.y < 0.7f)
                {
                    return false; // the side of a table is no place for a diya
                }

                point = new V3(local.x, marker.Top, local.z);
            }
            else if (marker.Surface == Surface.Ground)
            {
                point = new V3(local.x, 0f, local.z);
            }

            hit = new RoomHit(marker.Surface, point, new V3(marker.Normal.x, marker.Normal.y, marker.Normal.z));
            return true;
        }

        /// <summary>Places an item where <paramref name="hit"/> landed. Null when it's placed, else the reason.</summary>
        public string Place(string itemId, int variant, RoomHit hit)
        {
            DecorInfo info = HomeCatalog.Decoration(itemId);
            if (!IsInside || info == null)
            {
                return "Choose something to place first.";
            }

            PlacedDecor pose = InteriorPlacement.PoseFor(Room, info, hit);
            pose.variant = Mathf.Clamp(variant, 0, info.Variants - 1);
            pose.uid = _home.Design.nextUid;
            string problem = InteriorPlacement.Problem(Room, _home.Design.InRoom(Room.Id), pose, Tier);
            if (problem != null)
            {
                return problem;
            }

            if (!_home.Do(PlaceDecor.New(_home.Design, pose)))
            {
                return "That couldn't be placed.";
            }

            _home.Save();
            Game.Events.Publish(new DecorationPlacedEvent(info.Id, info.Category));
            ShowDecorations();
            return null;
        }

        public bool Remove(int uid)
        {
            PlacedDecor d = IsInside ? _home.Design.Find(uid) : null;
            if (d == null || d.room != Room.Id || !_home.Do(new RemoveDecor(uid)))
            {
                return false;
            }

            _home.Save();
            ShowDecorations();
            return true;
        }

        public List<PlacedDecor> Items() => IsInside ? _home.Design.InRoom(Room.Id) : new List<PlacedDecor>();

        private int Tier => Game.Services.TryGet(out QualityManager quality) ? quality.Tier : 1;

        private IEnumerator Go(RoomDef room)
        {
            _busy = true;
            StateChanged?.Invoke();
            yield return Fading(0f, 1f);
            if (IsInside)
            {
                Teardown(room == null);
            }

            if (room != null)
            {
                Build(room);
            }

            StateChanged?.Invoke();
            yield return Fading(1f, 0f);
            _busy = false;
            StateChanged?.Invoke();
        }

        private IEnumerator Fading(float from, float to)
        {
            for (float t = 0f; t < FadeSeconds; t += Time.unscaledDeltaTime)
            {
                Fade = Mathf.Lerp(from, to, t / FadeSeconds);
                yield return null;
            }

            Fade = to;
        }

        private void Build(RoomDef room)
        {
            _camera = _camera != null ? _camera : Camera.main;
            _rig = _rig != null ? _rig : FindFirstObjectByType<CameraRig>();
            bool firstRoom = !IsInside && _hidden.Count == 0;
            if (firstRoom)
            {
                HideOutside();
            }

            Room = room;
            _root = new GameObject("Interior_" + room.Id);
            _root.transform.position = Origin;
            _materials ??= new HomeMaterials();

            HouseMesh data = InteriorGenerator.Room(room);
            _mesh = HouseMeshes.ToMesh(data, "Interior_" + room.Id, out HousePaint[] paints);
            var shell = new GameObject("Room");
            shell.transform.SetParent(_root.transform, false);
            shell.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var renderer = shell.AddComponent<MeshRenderer>();
            var materials = new Material[paints.Length];
            for (int i = 0; i < paints.Length; i++)
            {
                materials[i] = _materials.Get(paints[i], InteriorGenerator.Colour(paints[i], _home.Design));
            }

            renderer.sharedMaterials = materials;
            Colliders(room);
            Lights(room);

            _decor = DecorView.Create(_root.transform, "RoomDecorations");
            _decor.transform.localPosition = Vector3.zero;
            ShowDecorations();

            _yaw = 0f;
            _pitch = -8f;
            Pose(out Vector3 p, out Quaternion r);
            if (_rig != null)
            {
                if (firstRoom)
                {
                    _rig.BeginOverride(p, r);
                }
                else
                {
                    _rig.SetOverridePose(p, r);
                }
            }

            Message = $"{room.Name}. Choose a decoration below, then tap a floor, wall or shelf. Drag to look around.";
            Game.Events.Publish(new InteriorEvent(room.Id, true, Items().Count));
        }

        private void Teardown(bool outside)
        {
            Game.Events.Publish(new InteriorEvent(Room.Id, false, Items().Count));
            Destroy(_root);
            HouseMeshes.Destroy(_mesh);
            _mesh = null;
            _lights.Clear();
            _root = null;
            _decor = null;
            Room = null;
            Triangles = 0;
            if (outside)
            {
                _materials?.Clear();
                _materials = null;
                ShowOutside();
                _rig?.EndOverride();
                Message = string.Empty;
            }
        }

        private void ShowDecorations()
        {
            List<PlacedDecor> items = _home.Design.InRoom(Room.Id);
            _decor.Show(items);
            Triangles = InteriorPlacement.Triangles(Room, items);
        }

        private void HideOutside()
        {
            _hidden.Clear();
            if (Game.Services.TryGet(out PlacementController placement))
            {
                placement.Cancel();
            }

            CrackerSpawner spawner = FindFirstObjectByType<CrackerSpawner>();
            if (spawner != null)
            {
                spawner.ClearAll();
            }

            var outside = new List<GameObject>();
            if (Game.Services.TryGet(out SettlementView settlement))
            {
                outside.Add(settlement.gameObject);
            }

            if (_home.House != null)
            {
                outside.Add(_home.House.gameObject);
            }

            if (_home.Decor != null)
            {
                outside.Add(_home.Decor.gameObject);
            }

            if (_home.Colliders != null)
            {
                outside.Add(_home.Colliders.gameObject);
            }

            CourtyardAvatar avatar = FindFirstObjectByType<CourtyardAvatar>();
            if (avatar != null && avatar.View != null)
            {
                outside.Add(avatar.View.gameObject);
            }

            if (RenderSettings.sun != null)
            {
                outside.Add(RenderSettings.sun.gameObject);
            }

            foreach (GameObject go in outside)
            {
                if (go.activeSelf)
                {
                    go.SetActive(false);
                    _hidden.Add(go);
                }
            }

            _ambient = RenderSettings.ambientLight;
            RenderSettings.ambientLight = new Color(0.32f, 0.26f, 0.2f);
        }

        private void ShowOutside()
        {
            foreach (GameObject go in _hidden)
            {
                if (go != null)
                {
                    go.SetActive(true);
                }
            }

            _hidden.Clear();
            RenderSettings.ambientLight = _ambient;
            _home?.Refresh();
        }

        private void Colliders(RoomDef room)
        {
            float hw = room.HalfWidth, d = room.Depth, h = room.Height;
            Box("Floor", new Vector3(0f, -0.05f, d / 2f), new Vector3(room.Width, 0.1f, d + 0.4f), Surface.Ground, Vector3.up);
            Box("LeftWall", new Vector3(-hw - 0.05f, h / 2f, d / 2f), new Vector3(0.1f, h, d), Surface.Wall, Vector3.right);
            Box("RightWall", new Vector3(hw + 0.05f, h / 2f, d / 2f), new Vector3(0.1f, h, d), Surface.Wall, Vector3.left);
            Box("BackWall", new Vector3(0f, h / 2f, d + 0.05f), new Vector3(room.Width, h, 0.1f), Surface.Wall, Vector3.back);
            Box("Ceiling", new Vector3(0f, h + 0.05f, d / 2f), new Vector3(room.Width, 0.1f, d), Surface.None, Vector3.down);
            foreach (RoomFurniture f in room.Furniture)
            {
                if (!f.Solid)
                {
                    continue;
                }

                Vector3 c = new Vector3((f.Box.Min.X + f.Box.Max.X) / 2f, (f.Box.Min.Y + f.Box.Max.Y) / 2f, (f.Box.Min.Z + f.Box.Max.Z) / 2f);
                Vector3 s = new Vector3(f.Box.Max.X - f.Box.Min.X, f.Box.Max.Y - f.Box.Min.Y, f.Box.Max.Z - f.Box.Min.Z);
                Box(f.Name, c, s, f.TopTakesItems ? Surface.Step : Surface.None, Vector3.up, f.Box.Max.Y);
            }
        }

        private void Box(string name, Vector3 centre, Vector3 size, Surface surface, Vector3 normal, float top = 0f)
        {
            var go = new GameObject("Surface_" + name);
            go.transform.SetParent(_root.transform, false);
            var box = go.AddComponent<BoxCollider>();
            box.center = centre;
            box.size = size;
            go.AddComponent<RoomSurfaceMarker>().Configure(surface, normal, top);
        }

        private void Lights(RoomDef room)
        {
            int count = Core.Home.InteriorBudget.For(Core.Home.InteriorBudget.Lights, Tier);
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("RoomLight" + i);
                go.transform.SetParent(_root.transform, false);
                go.transform.localPosition = new Vector3(count == 1 ? 0f : (i == 0 ? -1f : 1f), room.Height - 0.4f, room.Depth * 0.55f);
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.78f, 0.52f);
                light.range = 6f;
                light.intensity = count == 1 ? 1.6f : 1.1f;
                light.shadows = LightShadows.None;
                _lights.Add(light);
            }
        }

        private void Pose(out Vector3 position, out Quaternion rotation)
        {
            position = Origin + new Vector3(0f, EyeHeight, 0.25f);
            rotation = Quaternion.Euler(-_pitch, _yaw, 0f); // a negative pitch looks down
        }
    }
}

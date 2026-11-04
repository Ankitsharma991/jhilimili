using UnityEngine;
using UnityEngine.UI;
using UtsavNights.Core.District;
using UtsavNights.Gameplay;

namespace UtsavNights.UI
{
    /// <summary>The town map as a texture (Phase 12 step 6): drawn once from the plan and kept for the session.</summary>
    public static class MapTexture
    {
        private static Texture2D _texture;
        private static MapFrame _frame;

        public static MapFrame Frame => _frame ??= new MapFrame(DistrictGenerator.Bazaar);

        public static Texture2D Get()
        {
            if (_texture == null)
            {
                MapFrame f = Frame;
                _texture = new Texture2D(MapFrame.Width, f.Height, TextureFormat.RGBA32, false)
                {
                    name = "TownMap",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };
                _texture.LoadRawTextureData(MapRaster.Draw(DistrictGenerator.Bazaar, f));
                _texture.Apply(false, true);
            }

            return _texture;
        }
    }

    /// <summary>
    /// The minimap (Phase 12 step 6), top left while exploring: the town map round the player, turned so the way the
    /// camera looks is up; the route to the job's next step (or the map's waypoint) as dots; home, the scooter and the
    /// target; and the target on the rim when it's off the map. Moved and switched each frame, never rebuilt.
    /// </summary>
    public sealed class Minimap : MonoBehaviour
    {
        public const float Size = 320f;

        /// <summary>Map pixels per metre: about 64 m across.</summary>
        public const float Zoom = Size / 64f;

        public const int Dots = 48;
        public const float RouteInterval = 0.5f;

        private readonly float[] _xs = new float[MapRoute.MaxPoints];
        private readonly float[] _zs = new float[MapRoute.MaxPoints];
        private readonly RectTransform[] _dots = new RectTransform[Dots];
        private RectTransform _content;
        private RectTransform _map;
        private RectTransform _home;
        private RectTransform _scooter;
        private RectTransform _target;
        private RectTransform _rim;
        private RectTransform _arrow;
        private MapRoute _route;
        private float _clock;
        private int _routeCount;
        private Vector2 _mapSize;

        public int DotsShown { get; private set; }

        public bool HasTarget { get; private set; }

        public static Minimap Create(RectTransform parent)
        {
            RectTransform root = UiKit.Rect("Minimap", parent);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0f, 1f);
            root.sizeDelta = new Vector2(Size, Size);
            root.anchoredPosition = new Vector2(28f, -24f - UiTheme.MinTouch - 20f);
            Minimap m = root.gameObject.AddComponent<Minimap>();
            m.Build(root);
            return m;
        }

        private void Build(RectTransform root)
        {
            Image frame = UiKit.Panel(root, new Color(0.05f, 0.03f, 0.12f, 0.85f), raycast: false);
            frame.sprite = UiKit.Circle;
            RectTransform maskRect = UiKit.Rect("Mask", root);
            UiKit.Stretch(maskRect, 8f);
            Image mask = UiKit.Panel(maskRect, Color.white, raycast: false);
            mask.sprite = UiKit.Circle;
            maskRect.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            _content = UiKit.Rect("Turn", maskRect);
            _content.anchorMin = _content.anchorMax = _content.pivot = new Vector2(0.5f, 0.5f);
            _content.sizeDelta = Vector2.zero;

            MapFrame f = MapTexture.Frame;
            _mapSize = new Vector2((f.X1 - f.X0) * Zoom, (f.Z1 - f.Z0) * Zoom);
            _map = UiKit.Rect("Map", _content);
            _map.anchorMin = _map.anchorMax = new Vector2(0.5f, 0.5f);
            _map.pivot = Vector2.zero;
            _map.sizeDelta = _mapSize;
            RawImage raw = _map.gameObject.AddComponent<RawImage>();
            raw.texture = MapTexture.Get();
            raw.raycastTarget = false;

            for (int i = 0; i < Dots; i++)
            {
                _dots[i] = Marker(_map, "Dot" + i, 12f, UiTheme.Marigold);
                _dots[i].gameObject.SetActive(false);
            }

            _home = Marker(_map, "Home", 26f, UiTheme.Saffron);
            _scooter = Marker(_map, "Scooter", 20f, new Color(0.45f, 0.75f, 1f));
            _target = Marker(_map, "Target", 30f, UiTheme.Magenta);
            _rim = Marker(root, "TargetRim", 24f, UiTheme.Magenta);
            _rim.anchorMin = _rim.anchorMax = new Vector2(0.5f, 0.5f);

            // The player: a dot with a nose, always in the middle.
            _arrow = Marker(root, "Player", 26f, UiTheme.OffWhite);
            _arrow.anchorMin = _arrow.anchorMax = new Vector2(0.5f, 0.5f);
            _arrow.anchoredPosition = Vector2.zero;
            RectTransform nose = Marker(_arrow, "Nose", 12f, UiTheme.OffWhite);
            nose.anchorMin = nose.anchorMax = new Vector2(0.5f, 0.5f);
            nose.anchoredPosition = new Vector2(0f, 16f);
        }

        /// <summary>Follows the player (from the Explore HUD's update).</summary>
        public void Tick(ExploreController explore, JobService jobs, float dt)
        {
            if (explore == null || explore.Player == null)
            {
                return;
            }

            MapFrame f = MapTexture.Frame;
            Vector3 at = explore.Player.position;
            float yaw = explore.Orbit.Yaw;
            _content.localEulerAngles = new Vector3(0f, 0f, yaw);
            _map.anchoredPosition = -Point(f, at.x, at.z);
            float facing = explore.Riding ? explore.RideState.Heading : explore.State.Facing;
            _arrow.localEulerAngles = new Vector3(0f, 0f, yaw - facing);
            _home.anchoredPosition = Point(f, 0f, -3f);
            Transform scooter = explore.Scooter;
            bool showScooter = scooter != null && !explore.Riding;
            if (_scooter.gameObject.activeSelf != showScooter)
            {
                _scooter.gameObject.SetActive(showScooter);
            }

            if (showScooter)
            {
                _scooter.anchoredPosition = Point(f, scooter.position.x, scooter.position.z);
            }

            Vector3 target = default;
            HasTarget = jobs != null && jobs.TryTarget(at, out target);
            _clock -= dt;
            if (_clock <= 0f)
            {
                _clock = RouteInterval;
                _routeCount = 0;
                if (HasTarget)
                {
                    _route ??= new MapRoute(DistrictGenerator.Bazaar, WalkGraph.Build(DistrictGenerator.Bazaar));
                    _routeCount = _route.Find(at.x, at.z, target.x, target.z, _xs, _zs);
                }

                PlaceDots(f);
            }

            if (_target.gameObject.activeSelf != HasTarget)
            {
                _target.gameObject.SetActive(HasTarget);
            }

            // The target on the rim when it's beyond the map's edge.
            bool rim = false;
            if (HasTarget)
            {
                _target.anchoredPosition = Point(f, target.x, target.z);
                float dx = (target.x - at.x) * Zoom, dz = (target.z - at.z) * Zoom;
                float r = yaw * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
                var screen = new Vector2(dx * c - dz * s, dx * s + dz * c);
                float edge = Size / 2f - 20f;
                if (screen.magnitude > edge)
                {
                    rim = true;
                    _rim.anchoredPosition = screen.normalized * edge;
                }
            }

            if (_rim.gameObject.activeSelf != rim)
            {
                _rim.gameObject.SetActive(rim);
            }
        }

        /// <summary>Dots evenly along the route (closer together on a short one).</summary>
        private void PlaceDots(MapFrame f)
        {
            float length = MapRoute.Length(_xs, _zs, _routeCount);
            float step = Mathf.Max(5f, length / Dots);
            int n = 0, seg = 1;
            float along = step, segStart = 0f;
            while (n < Dots && seg < _routeCount)
            {
                float sx = _xs[seg] - _xs[seg - 1], sz = _zs[seg] - _zs[seg - 1];
                float segLength = Mathf.Sqrt(sx * sx + sz * sz);
                if (along > segStart + segLength)
                {
                    segStart += segLength;
                    seg++;
                    continue;
                }

                float t = segLength > 1e-4f ? (along - segStart) / segLength : 0f;
                _dots[n].anchoredPosition = Point(f, _xs[seg - 1] + sx * t, _zs[seg - 1] + sz * t);
                if (!_dots[n].gameObject.activeSelf)
                {
                    _dots[n].gameObject.SetActive(true);
                }

                n++;
                along += step;
            }

            DotsShown = n;
            for (; n < Dots; n++)
            {
                if (_dots[n].gameObject.activeSelf)
                {
                    _dots[n].gameObject.SetActive(false);
                }
            }
        }

        private Vector2 Point(MapFrame f, float x, float z)
        {
            (float u, float v) = f.ToUV(x, z);
            return new Vector2(u * _mapSize.x, v * _mapSize.y);
        }

        public static RectTransform Marker(RectTransform parent, string name, float size, Color colour)
        {
            RectTransform rect = UiKit.Rect(name, parent);
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            UiKit.Panel(rect, colour, raycast: false).sprite = UiKit.Circle;
            return rect;
        }
    }
}

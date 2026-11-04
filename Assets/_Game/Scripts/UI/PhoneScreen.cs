using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UtsavNights.Core.District;
using UtsavNights.Core.Explore;
using UtsavNights.Core.Neighbourhood;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;

namespace UtsavNights.UI
{
    /// <summary>
    /// The player's phone while exploring (Phase 12 step 6): six apps. Map (the town with the player, home, the scooter
    /// and the job's places; tap to set a waypoint), Messages (offers and thanks from the townsfolk), Jobs (take one, see
    /// what it pays, give it up), Camera (a photo of the view without the controls, to share), Home (back to the gate)
    /// and Settings. Pages are built when shown; nothing here runs per frame while it's closed.
    /// </summary>
    public sealed class PhoneScreen : MonoBehaviour
    {
        public enum Page
        {
            Home,
            Map,
            Messages,
            Jobs,
        }

        public const int PhotoWidth = 1920;

        private RectTransform _root;
        private RectTransform _body;
        private TextMeshProUGUI _title;
        private RectTransform _back;
        private ExploreMode _mode;
        private RectTransform _mapImage;

        public bool IsOpen => _root != null && _root.gameObject.activeSelf;

        public Page Current { get; private set; }

        /// <summary>The last photo taken, or empty.</summary>
        public string LastPhoto { get; private set; } = string.Empty;

        /// <summary>A photo was saved (its path).</summary>
        public event Action<string> PhotoTaken;

        public static PhoneScreen Create(RectTransform overlay, ExploreMode mode)
        {
            RectTransform root = UiKit.Rect("Phone", overlay);
            UiKit.Stretch(root);
            PhoneScreen phone = root.gameObject.AddComponent<PhoneScreen>();
            phone._mode = mode;
            phone._root = root;
            phone.Build();
            root.gameObject.SetActive(false);
            return phone;
        }

        public void Open(Page page = Page.Home)
        {
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            Show(page);
        }

        public void Close() => _root.gameObject.SetActive(false);

        /// <summary>Opens an app by name, as tapping its tile does: map, messages, jobs, camera, home or settings.</summary>
        public void OpenApp(string app)
        {
            switch (app)
            {
                case "map":
                    Show(Page.Map);
                    break;
                case "messages":
                    Show(Page.Messages);
                    break;
                case "jobs":
                    Show(Page.Jobs);
                    break;
                case "camera":
                    Close();
                    TakePhoto();
                    break;
                case "home":
                    Close();
                    GoHome();
                    break;
                case "settings":
                    Close();
                    SettingsSheet sheet = FindFirstObjectByType<SettingsSheet>(FindObjectsInactive.Include);
                    if (sheet != null)
                    {
                        sheet.Open();
                        sheet.transform.parent?.Find("SettingsOverlay")?.SetAsLastSibling();
                    }

                    break;
            }
        }

        /// <summary>Back to the lane at the player's own gate (off the scooter first).</summary>
        public void GoHome()
        {
            ExploreController c = _mode.Controller;
            if (c == null || !c.IsOpen)
            {
                return;
            }

            if (c.Riding)
            {
                c.Dismount();
            }

            c.Teleport(new Vector3(0f, 0f, SettlementLibrary.StreetZ0 + 0.8f), 180f);
        }

        /// <summary>
        /// A photo of the view as the camera sees it (no controls on it), saved as a PNG in the folder clips share from.
        /// Returns its path, or empty when there's no camera to take it with.
        /// </summary>
        public string TakePhoto()
        {
            Camera cam = Camera.main;
            if (cam == null || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                return string.Empty;
            }

            int w = Mathf.Min(PhotoWidth, Screen.width), h = Mathf.Max(1, Mathf.RoundToInt(w * Screen.height / (float)Mathf.Max(1, Screen.width)));
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            RenderTexture before = cam.targetTexture, active = RenderTexture.active;
            var image = new Texture2D(w, h, TextureFormat.RGB24, false);
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                image.Apply(false);
                string name = "utsav_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".png";
                string path = System.IO.Path.Combine(ClipSharer.PhotosFolder(), name);
                System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
                LastPhoto = path;
                PhotoTaken?.Invoke(path);
                return path;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Photos] couldn't take the photo: " + e.Message);
                return string.Empty;
            }
            finally
            {
                cam.targetTexture = before;
                RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(rt);
                Destroy(image);
            }
        }

        /// <summary>Sets the map's waypoint at a point on the map picture (0 to 1 each way), as a tap does.</summary>
        public void SetWaypointUV(float u, float v)
        {
            if (!Game.Services.TryGet(out JobService jobs))
            {
                return;
            }

            (float x, float z) = MapTexture.Frame.FromUV(Mathf.Clamp01(u), Mathf.Clamp01(v));
            jobs.Waypoint = new Vector3(x, 0f, z);
            if (Current == Page.Map)
            {
                Show(Page.Map);
            }
        }

        public void Show(Page page)
        {
            Current = page;
            for (int i = _body.childCount - 1; i >= 0; i--)
            {
                Destroy(_body.GetChild(i).gameObject);
            }

            _back.gameObject.SetActive(page != Page.Home);
            Game.Services.TryGet(out JobService jobs);
            switch (page)
            {
                case Page.Map:
                    _title.text = "Map";
                    BuildMap(jobs);
                    break;
                case Page.Messages:
                    _title.text = "Messages";
                    BuildMessages(jobs);
                    break;
                case Page.Jobs:
                    _title.text = "Jobs";
                    BuildJobs(jobs);
                    break;
                default:
                    _title.text = DateTime.Now.ToString("h:mm tt", CultureInfo.InvariantCulture);
                    BuildHome(jobs);
                    break;
            }
        }

        private void Build()
        {
            // The dim behind the phone takes the touches (no looking about while it's up); tapping it closes the phone.
            Image dim = UiKit.Panel(_root, new Color(0.02f, 0.01f, 0.06f, 0.55f));
            var closer = _root.gameObject.AddComponent<Button>();
            closer.targetGraphic = dim;
            closer.transition = Selectable.Transition.None;
            closer.onClick.AddListener(Close);

            RectTransform phone = Area(_root, "Handset", 0.31f, 0.03f, 0.69f, 0.97f);
            UiKit.Panel(phone, new Color(0.09f, 0.06f, 0.2f, 1f)).sprite = UiKit.Rounded;
            RectTransform screen = Area(phone, "Screen", 0f, 0f, 1f, 1f);
            screen.offsetMin = new Vector2(18f, 18f);
            screen.offsetMax = new Vector2(-18f, -18f);

            RectTransform header = Area(screen, "Header", 0f, 1f, 1f, 1f);
            header.offsetMin = new Vector2(0f, -UiTheme.MinTouch);
            _title = UiKit.Text(header, string.Empty, UiTheme.TitleText, TextAlignmentOptions.Center, UiTheme.Marigold);
            _back = UiKit.Rect("Back", header);
            _back.anchorMin = _back.anchorMax = _back.pivot = new Vector2(0f, 0.5f);
            _back.sizeDelta = new Vector2(150f, UiTheme.MinTouch * 0.8f);
            Button(_back, "Back", UiTheme.IndigoLight, UiTheme.OffWhite, () => Show(Page.Home));
            RectTransform close = UiKit.Rect("Close", header);
            close.anchorMin = close.anchorMax = close.pivot = new Vector2(1f, 0.5f);
            close.sizeDelta = new Vector2(150f, UiTheme.MinTouch * 0.8f);
            Button(close, "Close", UiTheme.IndigoLight, UiTheme.OffWhite, Close);

            _body = Area(screen, "Body", 0f, 0f, 1f, 1f);
            _body.offsetMax = new Vector2(0f, -UiTheme.MinTouch - 12f);
        }

        private void BuildHome(JobService jobs)
        {
            RectTransform tiles = Area(_body, "Apps", 0f, 0f, 1f, 1f);
            GridLayoutGroup grid = tiles.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(300f, 260f);
            grid.spacing = new Vector2(24f, 24f);
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.padding = new RectOffset(0, 0, 24, 0);
            int unread = jobs != null ? jobs.Unread : 0;
            string active = jobs?.Active != null ? jobs.Active.Def.Title : "Earn coins and reputation";
            Tile(tiles, "map", "Map", "Find your way", UiTheme.Saffron);
            Tile(tiles, "messages", "Messages", unread > 0 ? unread + " new" : "From the town", UiTheme.Magenta);
            Tile(tiles, "jobs", "Jobs", active, UiTheme.Marigold);
            Tile(tiles, "camera", "Camera", "Take a photo", new Color(0.45f, 0.75f, 1f));
            Tile(tiles, "home", "Home", "Back to your gate", new Color(0.55f, 0.85f, 0.6f));
            Tile(tiles, "settings", "Settings", "Sound, controls", UiTheme.OffWhite);
        }

        private void Tile(RectTransform parent, string app, string name, string sub, Color colour)
        {
            RectTransform tile = UiKit.Rect("App_" + app, parent);
            Image bg = UiKit.Panel(tile, UiTheme.IndigoLight);
            bg.sprite = UiKit.Rounded;
            var button = tile.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(() => OpenApp(app));
            RectTransform icon = UiKit.Rect("Icon", tile);
            icon.anchorMin = icon.anchorMax = icon.pivot = new Vector2(0.5f, 1f);
            icon.sizeDelta = new Vector2(96f, 96f);
            icon.anchoredPosition = new Vector2(0f, -24f);
            UiKit.Panel(icon, colour, raycast: false).sprite = UiKit.Circle;
            UiKit.Text(icon, name.Substring(0, 1), UiTheme.TitleText, TextAlignmentOptions.Center, UiTheme.Indigo);
            RectTransform label = Area(tile, "Name", 0f, 0.22f, 1f, 0.5f);
            UiKit.Text(label, name, UiTheme.BodyText, TextAlignmentOptions.Center, UiTheme.OffWhite);
            RectTransform small = Area(tile, "Sub", 0.04f, 0.02f, 0.96f, 0.24f);
            UiKit.Text(small, sub, UiTheme.SmallText * 0.8f, TextAlignmentOptions.Center, UiTheme.Muted);
        }

        private void BuildMap(JobService jobs)
        {
            MapFrame f = MapTexture.Frame;
            RectTransform holder = Area(_body, "MapHolder", 0f, 0.14f, 1f, 1f);
            _mapImage = UiKit.Rect("Map", holder);
            UiKit.Stretch(_mapImage);
            RawImage raw = _mapImage.gameObject.AddComponent<RawImage>();
            raw.texture = MapTexture.Get();
            AspectRatioFitter fit = _mapImage.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = MapFrame.Width / (float)f.Height;
            _mapImage.gameObject.AddComponent<MapTap>().Tapped = SetWaypointUV;

            ExploreController c = _mode.Controller;
            Pin("Home", 0f, -3f, 30f, UiTheme.Saffron, "Home");
            Pin("Chowk", DistrictGenerator.Bazaar.Square.CentreX, DistrictGenerator.Bazaar.Square.CentreZ, 22f, UiTheme.OffWhite, "Chowk");
            Pin("Ghat", DistrictGenerator.Bazaar.Ghat.CentreX, DistrictGenerator.Bazaar.Ghat.CentreZ, 22f, UiTheme.OffWhite, "Ghat");
            if (c != null && c.Scooter != null && !c.Riding)
            {
                Pin("Scooter", c.Scooter.position.x, c.Scooter.position.z, 22f, new Color(0.45f, 0.75f, 1f), "Scooter");
            }

            if (jobs?.Active != null)
            {
                for (int i = 0; i < jobs.Active.Def.Steps.Count; i++)
                {
                    if (jobs.Active.IsOpen(i))
                    {
                        JobStep s = jobs.Active.Def.Steps[i];
                        Pin("Step" + i, s.X, s.Z, 26f, UiTheme.Magenta, null);
                    }
                }
            }

            if (jobs?.Waypoint is Vector3 w)
            {
                Pin("Waypoint", w.x, w.z, 30f, UiTheme.Magenta, "Waypoint");
            }

            if (c != null && c.Player != null)
            {
                Pin("You", c.Player.position.x, c.Player.position.z, 30f, UiTheme.Marigold, "You");
            }

            RectTransform foot = Area(_body, "Foot", 0f, 0f, 1f, 0.13f);
            RectTransform hint = Area(foot, "Hint", 0f, 0f, 0.6f, 1f);
            UiKit.Text(hint, "Tap the map to set a waypoint", UiTheme.SmallText, TextAlignmentOptions.Left, UiTheme.Muted);
            RectTransform clear = Area(foot, "Clear", 0.62f, 0.05f, 1f, 0.95f);
            Button(clear, "Clear waypoint", UiTheme.IndigoLight, UiTheme.OffWhite, () =>
            {
                if (jobs != null)
                {
                    jobs.Waypoint = null;
                }

                Show(Page.Map);
            });
        }

        private void Pin(string name, float x, float z, float size, Color colour, string label)
        {
            (float u, float v) = MapTexture.Frame.ToUV(x, z);
            RectTransform pin = UiKit.Rect(name, _mapImage);
            pin.anchorMin = pin.anchorMax = new Vector2(u, v);
            pin.pivot = new Vector2(0.5f, 0.5f);
            pin.sizeDelta = new Vector2(size, size);
            UiKit.Panel(pin, colour, raycast: false).sprite = UiKit.Circle;
            if (!string.IsNullOrEmpty(label))
            {
                RectTransform text = UiKit.Rect("Label", pin);
                text.anchorMin = text.anchorMax = new Vector2(0.5f, 1f);
                text.pivot = new Vector2(0.5f, 0f);
                text.sizeDelta = new Vector2(200f, 40f);
                UiKit.Text(text, label, UiTheme.SmallText * 0.75f, TextAlignmentOptions.Center, UiTheme.OffWhite).raycastTarget = false;
            }
        }

        private void BuildMessages(JobService jobs)
        {
            RectTransform list = ScrollList();
            if (jobs == null || jobs.Messages.Count == 0)
            {
                Card(list, "No messages yet", "People in the town will message you here.", null, null);
                return;
            }

            foreach (PhoneMessage m in jobs.Messages)
            {
                jobs.MarkRead(m);
                JobDef job = string.IsNullOrEmpty(m.JobId) ? null : jobs.Find(m.JobId);
                bool offer = job != null && jobs.Active?.Def != job && jobs.IsAvailable(job) && m.Text != job.Thanks;
                Card(list, m.From, m.Text, offer ? "See the job" : null, offer ? (Action)(() => Show(Page.Jobs)) : null);
            }
        }

        private void BuildJobs(JobService jobs)
        {
            RectTransform list = ScrollList();
            if (jobs == null)
            {
                return;
            }

            foreach (JobDef job in jobs.Jobs)
            {
                int times = jobs.TimesDone(job.Id);
                (long coins, int rep) = JobPay.For(job, times);
                bool active = jobs.Active != null && jobs.Active.Def == job;
                bool open = jobs.IsAvailable(job);
                string pay = $"Pays {coins:N0} coins and {rep} reputation" + (times > 0 ? $" (done {times}×)" : string.Empty);
                string state = active ? "Now: " + jobs.Active.Objective
                    : !open ? $"Opens at level {job.MinLevel}, {Reputation.Titles[job.MinLevel - 1]}"
                    : job.TimeLimit > 0f ? $"{job.Giver} · {Mathf.RoundToInt(job.TimeLimit / 60f)} minutes" : job.Giver;
                string action = active ? "Give up" : open ? "Take the job" : null;
                Card(list, job.Title, pay + "\n" + state, action, active
                    ? () =>
                    {
                        jobs.Abandon();
                        Show(Page.Jobs);
                    }
                    : open ? (Action)(() =>
                    {
                        jobs.Accept(job.Id);
                        Close();
                    }) : null);
            }
        }

        private RectTransform ScrollList()
        {
            RectTransform view = Area(_body, "List", 0f, 0f, 1f, 1f);
            view.gameObject.AddComponent<RectMask2D>();
            UiKit.Panel(view, new Color(0f, 0f, 0f, 0.001f));
            ScrollRect scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            RectTransform content = UiKit.Rect("Content", view);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            VerticalLayoutGroup v = content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 16f;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            scroll.viewport = view;
            return content;
        }

        private static void Card(RectTransform list, string heading, string text, string action, Action onAction)
        {
            RectTransform card = UiKit.Rect("Card", list);
            UiKit.Panel(card, UiTheme.IndigoLight).sprite = UiKit.Rounded;
            VerticalLayoutGroup v = card.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(24, 24, 18, 18);
            v.spacing = 8f;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            Line(card, heading, UiTheme.BodyText, UiTheme.Marigold);
            Line(card, text, UiTheme.SmallText, UiTheme.OffWhite);
            if (action != null)
            {
                RectTransform b = UiKit.Rect("Action", card);
                LayoutElement e = b.gameObject.AddComponent<LayoutElement>();
                e.minHeight = e.preferredHeight = UiTheme.MinTouch * 0.8f;
                Button(b, action, UiTheme.Saffron, UiTheme.Indigo, onAction);
            }
        }

        private static void Line(RectTransform parent, string text, float size, Color colour)
        {
            TextMeshProUGUI t = UiKit.Text(parent, text, size, TextAlignmentOptions.TopLeft, colour);
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;
            t.rectTransform.anchorMin = Vector2.zero;
            t.rectTransform.anchorMax = Vector2.one;
        }

        private static void Button(RectTransform rect, string label, Color background, Color text, Action action)
        {
            Image image = UiKit.Panel(rect, background);
            image.sprite = UiKit.Rounded;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action?.Invoke());
            UiKit.Text(rect, label, UiTheme.BodyText * 0.85f, TextAlignmentOptions.Center, text);
        }

        private static RectTransform Area(RectTransform parent, string name, float x0, float y0, float x1, float y1)
        {
            RectTransform rect = UiKit.Rect(name, parent);
            rect.anchorMin = new Vector2(x0, y0);
            rect.anchorMax = new Vector2(x1, y1);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        /// <summary>Turns a tap on the map picture into a point on it (0 to 1 each way).</summary>
        private sealed class MapTap : MonoBehaviour, IPointerClickHandler
        {
            public Action<float, float> Tapped;

            public void OnPointerClick(PointerEventData e)
            {
                var rect = (RectTransform)transform;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera, out Vector2 local))
                {
                    Rect r = rect.rect;
                    Tapped?.Invoke((local.x - r.x) / r.width, (local.y - r.y) / r.height);
                }
            }
        }
    }
}

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UtsavNights.Core.Festivals;
using UtsavNights.Core.Home;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;

namespace UtsavNights.UI
{
    /// <summary>
    /// Inside the house (Phase 11 step 2, F25). A "Go inside" button sits above Countdown. Inside, the rest of the HUD
    /// steps aside: a bar with the room's name, Upstairs or Downstairs, and Outside; a strip of decorations for tonight's
    /// festival that suit a room; Remove for a tapped item; and a line saying why a placement didn't work. Drag to look
    /// round; tap a floor, wall, shelf or table to place.
    /// </summary>
    public sealed class InteriorMode : MonoBehaviour
    {
        private readonly List<GameObject> _hud = new List<GameObject>();
        private readonly List<(Image image, string id)> _chips = new List<(Image, string)>();
        private RectTransform _launcher;
        private RectTransform _overlay;
        private RectTransform _strip;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _floor;
        private TextMeshProUGUI _status;
        private GameObject _remove;
        private Image _fade;
        private InteriorController _controller;
        private HomeService _home;
        private int _selectedUid;

        public bool Inside => _controller != null && _controller.IsInside;

        public string Status => _status != null ? _status.text : string.Empty;

        public string Chosen { get; private set; }

        public int SelectedUid => _selectedUid;

        public RectTransform Launcher => _launcher;

        public InteriorController Controller => _controller;

        /// <summary>The decorations offered in the strip (tonight's festival and every day).</summary>
        public List<string> Offered { get; } = new List<string>();

        private void Awake() => Build();

        private void Start()
        {
            _controller = FindFirstObjectByType<InteriorController>();
            Game.Services.TryGet(out _home);
            if (_controller != null)
            {
                _controller.StateChanged += Refresh;
            }

            InputBlockers.Register(_launcher);
        }

        private void OnDestroy()
        {
            if (_controller != null)
            {
                _controller.StateChanged -= Refresh;
            }

            InputBlockers.Unregister(_launcher);
            InputBlockers.Unregister(_overlay);
        }

        private void Update()
        {
            if (_controller == null)
            {
                return;
            }

            _fade.color = new Color(0f, 0f, 0f, _controller.Fade);
            bool canGoIn = !Inside && !_controller.Busy && (_home == null || !_home.Visiting);
            if (_launcher.gameObject.activeSelf != canGoIn)
            {
                _launcher.gameObject.SetActive(canGoIn);
            }

            if (Inside && !_controller.Busy && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Leave();
            }
        }

        /// <summary>Same as tapping "Go inside".</summary>
        public bool GoInside() => Enter(Rooms.Main);

        public bool Enter(string room)
        {
            if (_controller == null)
            {
                _controller = FindFirstObjectByType<InteriorController>();
                if (_controller != null)
                {
                    _controller.StateChanged += Refresh;
                }
            }

            if (_controller == null)
            {
                return false;
            }

            if (!_controller.Enter(room))
            {
                SetStatus(_controller.Message);
                return false;
            }

            return true;
        }

        /// <summary>Same as tapping Upstairs or Downstairs.</summary>
        public bool OtherFloor() => Inside && Enter(_controller.Room.Id == Rooms.Main ? Rooms.Upper : Rooms.Main);

        public bool Leave() => _controller != null && _controller.Leave();

        /// <summary>Same as tapping a decoration in the strip.</summary>
        public void Choose(string itemId)
        {
            Chosen = Chosen == itemId ? null : itemId;
            _selectedUid = 0;
            _remove.SetActive(false);
            foreach ((Image image, string id) in _chips)
            {
                image.color = id == Chosen ? UiTheme.Saffron : UiTheme.IndigoLight;
            }

            SetStatus(Chosen == null ? "Tap a decoration in the room to remove it." : $"Tap where the {HomeCatalog.Decoration(Chosen).Name.ToLowerInvariant()} should go.");
        }

        /// <summary>Same as tapping the room at a screen point.</summary>
        public void TapAt(Vector2 screen)
        {
            if (!Inside || _controller.Busy || !_controller.Tap(screen, out RoomHit hit, out int uid))
            {
                return;
            }

            if (uid != 0)
            {
                _selectedUid = uid;
                _remove.SetActive(true);
                SetStatus("Remove it, or tap somewhere else.");
                return;
            }

            if (Chosen == null)
            {
                SetStatus("Choose a decoration below first.");
                return;
            }

            string problem = _controller.Place(Chosen, 0, hit);
            SetStatus(problem ?? "Placed.");
        }

        /// <summary>Same as tapping Remove.</summary>
        public bool RemoveSelected()
        {
            bool removed = _selectedUid != 0 && _controller.Remove(_selectedUid);
            _selectedUid = 0;
            _remove.SetActive(false);
            SetStatus(removed ? "Removed." : string.Empty);
            return removed;
        }

        private void Refresh()
        {
            bool inside = Inside;
            if (inside && !_overlay.gameObject.activeSelf)
            {
                HideHud();
                _overlay.gameObject.SetActive(true);
                _overlay.SetAsLastSibling();
                InputBlockers.Register(_overlay);
                FillStrip();
            }
            else if (!inside && !_controller.Busy && _overlay.gameObject.activeSelf)
            {
                _overlay.gameObject.SetActive(false);
                InputBlockers.Unregister(_overlay);
                ShowHud();
                Chosen = null;
                _selectedUid = 0;
            }

            if (inside)
            {
                _title.text = _controller.Room.Name;
                _floor.text = _controller.Room.Id == Rooms.Main ? "Upstairs" : "Downstairs";
                SetStatus(_controller.Message);
                _remove.SetActive(false);
            }
        }

        private void HideHud()
        {
            _hud.Clear();
            Transform parent = transform.parent;
            if (parent == null)
            {
                return;
            }

            foreach (Transform sibling in parent)
            {
                if (sibling != transform && sibling != _overlay && sibling != _fade.rectTransform && sibling.gameObject.activeSelf)
                {
                    sibling.gameObject.SetActive(false);
                    _hud.Add(sibling.gameObject);
                }
            }
        }

        private void ShowHud()
        {
            foreach (GameObject go in _hud)
            {
                if (go != null)
                {
                    go.SetActive(true);
                }
            }

            _hud.Clear();
        }

        private void FillStrip()
        {
            foreach (Transform child in _strip)
            {
                Destroy(child.gameObject);
            }

            _chips.Clear();
            Offered.Clear();
            FestivalInfo festival = Festivals.Find(Game.Services.TryGet(out SettingsService settings) ? settings.Current.festival : null);
            foreach (DecorInfo info in InteriorPlacement.Items())
            {
                if (!Festivals.HasDecoration(festival, info))
                {
                    continue;
                }

                Offered.Add(info.Id);
                RectTransform chip = UiKit.Rect(info.Id, _strip);
                chip.sizeDelta = new Vector2(230f, 0f);
                LayoutElement e = chip.gameObject.AddComponent<LayoutElement>();
                e.preferredWidth = e.minWidth = 230f;
                Image image = UiKit.Panel(chip, UiTheme.IndigoLight);
                image.sprite = UiKit.Rounded;
                var button = chip.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                string id = info.Id;
                button.onClick.AddListener(() => Choose(id));
                TextMeshProUGUI label = UiKit.Text(chip, info.Name, UiTheme.SmallText, TextAlignmentOptions.Center, UiTheme.OffWhite);
                label.textWrappingMode = TextWrappingModes.Normal;
                _chips.Add((image, id));
            }
        }

        private void SetStatus(string text)
        {
            if (_status != null)
            {
                _status.text = text ?? string.Empty;
            }
        }

        private void Build()
        {
            _launcher = UiKit.Rect("InsideButton", transform);
            _launcher.anchorMin = _launcher.anchorMax = _launcher.pivot = new Vector2(1f, 0f);
            _launcher.sizeDelta = new Vector2(240f, UiTheme.MinTouch);
            _launcher.anchoredPosition = new Vector2(-32f - 280f - 20f - 220f - 20f, TrayView.TrayHeight + 150f + UiTheme.MinTouch + 20f); // above Countdown
            Button(_launcher, "Go inside", UiTheme.IndigoLight, () => GoInside());

            _overlay = UiKit.Rect("Interior", transform.parent != null ? transform.parent : transform);
            UiKit.Stretch(_overlay);
            Image cover = _overlay.gameObject.AddComponent<Image>();
            cover.color = new Color(0f, 0f, 0f, 0f);
            InteriorTouch touch = _overlay.gameObject.AddComponent<InteriorTouch>();
            touch.Dragged += d => _controller?.Look(d.x * 0.15f, d.y * 0.12f);
            touch.Tapped += TapAt;

            RectTransform bar = Area(_overlay, "Bar", 0f, 1f, 1f, 1f, new Vector2(24f, -UiTheme.MinTouch - 36f), new Vector2(-24f, -16f));
            UiKit.Panel(bar, new Color(0.09f, 0.06f, 0.2f, 0.85f)).sprite = UiKit.Rounded;
            _title = UiKit.Text(Area(bar, "Title", 0f, 0f, 0.5f, 1f, new Vector2(24f, 0f), Vector2.zero), string.Empty, UiTheme.BodyText, TextAlignmentOptions.Left, UiTheme.Marigold);
            _floor = Button(Area(bar, "Floor", 0.52f, 0.12f, 0.75f, 0.88f, Vector2.zero, Vector2.zero), "Upstairs", UiTheme.IndigoLight, () => OtherFloor());
            Button(Area(bar, "Outside", 0.77f, 0.12f, 0.99f, 0.88f, Vector2.zero, Vector2.zero), "Outside", UiTheme.Saffron, () => Leave());

            RectTransform status = Area(_overlay, "Status", 0f, 1f, 1f, 1f, new Vector2(40f, -UiTheme.MinTouch - 150f), new Vector2(-40f, -UiTheme.MinTouch - 44f));
            UiKit.Panel(status, new Color(0.07f, 0.05f, 0.18f, 0.6f), raycast: false);
            _status = UiKit.Text(status, string.Empty, UiTheme.SmallText, TextAlignmentOptions.Center, UiTheme.OffWhite);
            _status.textWrappingMode = TextWrappingModes.Normal;

            RectTransform stripView = Area(_overlay, "Strip", 0f, 0f, 1f, 0f, new Vector2(24f, 24f), new Vector2(-24f, 24f + 120f));
            UiKit.Panel(stripView, new Color(0.09f, 0.06f, 0.2f, 0.85f)).sprite = UiKit.Rounded;
            stripView.gameObject.AddComponent<RectMask2D>();
            _strip = UiKit.Rect("Items", stripView);
            _strip.anchorMin = new Vector2(0f, 0f);
            _strip.anchorMax = new Vector2(0f, 1f);
            _strip.pivot = new Vector2(0f, 0.5f);
            _strip.offsetMin = new Vector2(0f, 10f);
            _strip.offsetMax = new Vector2(0f, -10f);
            HorizontalLayoutGroup row = _strip.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 12f;
            row.padding = new RectOffset(12, 12, 0, 0);
            row.childControlHeight = true;
            row.childControlWidth = true;
            row.childForceExpandHeight = true;
            row.childForceExpandWidth = false;
            _strip.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect scroll = stripView.gameObject.AddComponent<ScrollRect>();
            scroll.vertical = false;
            scroll.viewport = stripView;
            scroll.content = _strip;

            RectTransform remove = Area(_overlay, "Remove", 0.5f, 0f, 0.5f, 0f, new Vector2(-150f, 170f), new Vector2(150f, 170f + UiTheme.MinTouch));
            Button(remove, "Remove", UiTheme.Magenta, () => RemoveSelected());
            _remove = remove.gameObject;
            _remove.SetActive(false);

            RectTransform fade = UiKit.Rect("Fade", transform.parent != null ? transform.parent : transform);
            UiKit.Stretch(fade);
            _fade = UiKit.Panel(fade, new Color(0f, 0f, 0f, 0f), raycast: false);
            _overlay.gameObject.SetActive(false);
        }

        private static TextMeshProUGUI Button(RectTransform rect, string label, Color background, System.Action action)
        {
            Image image = UiKit.Panel(rect, background);
            image.sprite = UiKit.Rounded;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            return UiKit.Text(rect, label, UiTheme.BodyText, TextAlignmentOptions.Center, UiTheme.OffWhite);
        }

        private static RectTransform Area(RectTransform parent, string name, float x0, float y0, float x1, float y1, Vector2 offsetMin, Vector2 offsetMax)
        {
            RectTransform rect = UiKit.Rect(name, parent);
            rect.anchorMin = new Vector2(x0, y0);
            rect.anchorMax = new Vector2(x1, y1);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }
    }
}

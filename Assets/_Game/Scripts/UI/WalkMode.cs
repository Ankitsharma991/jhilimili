using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Home;
using UtsavNights.Core.Neighbourhood;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;

namespace UtsavNights.UI
{
    /// <summary>
    /// The walk mode's screen (Phase 8 step 3, PRD 10 controls). A walking-figure button beside the house button starts a
    /// walk. Top bar: Home (back to the courtyard and the fireworks), Map and Joystick. The map is a plan of the settlement
    /// with a pin for every celebration spot (tap to go there) and the avatar's dot. The optional left-thumb joystick
    /// moves the avatar; otherwise tapping the ground walks there. A full-screen blocker keeps the lighter and cracker
    /// placement from reacting during the walk.
    /// </summary>
    public sealed class WalkMode : MonoBehaviour
    {
        public const string JoystickPrefKey = "walk.joystick";
        private const float MapWidth = 940f;
        private const float MapHeight = 760f;
        private const float StickRadius = 150f;

        private readonly List<RectTransform> _uiRects = new List<RectTransform>();
        private readonly List<GameObject> _mapItems = new List<GameObject>();
        private RectTransform _overlay;
        private RectTransform _openButton;
        private RectTransform _map;
        private RectTransform _mapPlan;
        private RectTransform _marker;
        private RectTransform _stick;
        private RectTransform _knob;
        private Image _fade;
        private TextMeshProUGUI _message;
        private TextMeshProUGUI _stickLabel;
        private Button _celebrateHere;
        private RectTransform _card;
        private TextMeshProUGUI _cardTitle;
        private TextMeshProUGUI _cardReason;
        private Button _cardGo;
        private Button _cardCelebrate;
        private string _cardSpot;
        private WalkController _controller;
        private float _x0;
        private float _z0;
        private float _scale = 1f;

        public bool IsOpen => _overlay != null && _overlay.gameObject.activeSelf;

        public bool MapOpen => _map != null && _map.gameObject.activeSelf;

        public bool JoystickOn => PlayerPrefs.GetInt(JoystickPrefKey, 0) == 1;

        public RectTransform OpenButton => _openButton;

        public WalkController Controller => _controller;

        /// <summary>The spot under the avatar that the "Celebrate here" button would choose, or null.</summary>
        public string HereSpot { get; private set; }

        /// <summary>The spot pins on the map (tests and the screen reader).</summary>
        public int PinCount { get; private set; }

        private void Awake()
        {
            _controller = FindFirstObjectByType<WalkController>();
            if (_controller == null)
            {
                _controller = new GameObject("WalkController").AddComponent<WalkController>();
            }

            _controller.IsOverUi = IsOverUi;
            _controller.StateChanged += Refresh;
            Build();
        }

        private void OnEnable()
        {
            if (_openButton != null)
            {
                InputBlockers.Register(_openButton);
            }
        }

        private void OnDisable()
        {
            if (_openButton != null)
            {
                InputBlockers.Unregister(_openButton);
            }

            if (IsOpen)
            {
                Home();
            }
        }

        private void OnDestroy()
        {
            if (_controller != null)
            {
                _controller.StateChanged -= Refresh;
            }
        }

        public void Open()
        {
            if (IsOpen || !_controller.Open())
            {
                return;
            }

            _overlay.gameObject.SetActive(true);
            InputBlockers.Register(_overlay);
            _map.gameObject.SetActive(false);
            Refresh();
        }

        /// <summary>Ends the walk: back to the courtyard and the fireworks view.</summary>
        public void Home()
        {
            if (!IsOpen)
            {
                return;
            }

            _controller.Close();
            InputBlockers.Unregister(_overlay);
            _overlay.gameObject.SetActive(false);
        }

        public void ToggleMap()
        {
            bool show = !MapOpen;
            if (show)
            {
                BuildMap();
            }

            _card.gameObject.SetActive(false);
            _map.gameObject.SetActive(show);
        }

        public void ToggleJoystick()
        {
            PlayerPrefs.SetInt(JoystickPrefKey, JoystickOn ? 0 : 1);
            _controller.Stick = Vector2.zero;
            Refresh();
        }

        public void GoTo(string spotId)
        {
            if (_controller.FastTravel(spotId))
            {
                _map.gameObject.SetActive(false);
            }
        }

        /// <summary>Moves the celebration to a spot and ends the walk there (the fireworks view turns to it).</summary>
        public bool Celebrate(string spotId)
        {
            if (!Game.Services.TryGet(out CelebrationService celebration) || !celebration.Choose(spotId))
            {
                return false;
            }

            Home();
            return true;
        }

        /// <summary>The card for a map pin: the spot's name, the reason it is closed (if it is), Go there and Celebrate here.</summary>
        public void ShowCard(string spotId)
        {
            SpotDef spot = _controller.Layout?.Spot(spotId);
            if (spot == null)
            {
                return;
            }

            SpotAvailability a = Game.Services.TryGet(out CelebrationService celebration)
                ? celebration.Availability(spotId)
                : new SpotAvailability(true, string.Empty);
            _cardSpot = spotId;
            _cardTitle.text = spot.name;
            _cardReason.text = a.Open ? AerialNote(spot) : a.Reason;
            _cardGo.interactable = a.Open;
            _cardCelebrate.interactable = a.Open;
            _card.gameObject.SetActive(true);
        }

        public string CardReason => _card != null && _card.gameObject.activeSelf ? _cardReason.text : string.Empty;

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (MapOpen)
                {
                    _map.gameObject.SetActive(false);
                }
                else
                {
                    Home();
                    return;
                }
            }

            _fade.color = new Color(0f, 0f, 0f, _controller.Fade);
            HereSpot = null;
            if (_controller.Agent != null && Game.Services.TryGet(out CelebrationService celebration))
            {
                var at = new Vector3(_controller.Agent.Position.X, _controller.Agent.Position.Y, _controller.Agent.Position.Z);
                SpotDef here = celebration.SpotAt(at);
                HereSpot = here != null && celebration.Availability(here.id).Open ? here.id : null;
            }

            _celebrateHere.interactable = HereSpot != null;
            if (MapOpen && _controller.Agent != null)
            {
                _marker.anchoredPosition = MapPoint(_controller.Agent.Position.X, _controller.Agent.Position.Z);
            }
        }

        private bool IsOverUi(Vector2 screen)
        {
            foreach (RectTransform rect in _uiRects)
            {
                if (rect != null && rect.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(rect, screen, null))
                {
                    return true;
                }
            }

            return false;
        }

        private void Refresh()
        {
            if (!IsOpen)
            {
                return;
            }

            _message.text = _controller.Message;
            _message.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(_controller.Message));
            _stick.gameObject.SetActive(JoystickOn);
            _stickLabel.text = JoystickOn ? "Joystick on" : "Joystick off";
        }

        // ---- Building ----

        private void Build()
        {
            // The walking-figure button, left of the house button.
            _openButton = UiKit.Rect("WalkButton", transform);
            _openButton.anchorMin = _openButton.anchorMax = _openButton.pivot = new Vector2(1f, 1f);
            _openButton.sizeDelta = new Vector2(UiTheme.MinTouch, UiTheme.MinTouch);
            _openButton.anchoredPosition = new Vector2(-24f - 3f * (UiTheme.MinTouch + 20f), -24f);
            Image bg = UiKit.Panel(_openButton, UiTheme.Indigo);
            bg.sprite = UiKit.Circle;
            var open = _openButton.gameObject.AddComponent<Button>();
            open.targetGraphic = bg;
            open.onClick.AddListener(Open);
            Shape(_openButton, "Head", new Vector2(26f, 26f), new Vector2(6f, 34f), 0f, UiKit.Circle);
            Shape(_openButton, "Body", new Vector2(14f, 44f), new Vector2(2f, 6f), -12f, null);
            Shape(_openButton, "Leg", new Vector2(12f, 38f), new Vector2(-10f, -30f), -25f, null);
            Shape(_openButton, "Leg2", new Vector2(12f, 38f), new Vector2(14f, -30f), 25f, null);

            _overlay = UiKit.Rect("WalkOverlay", transform.parent != null ? transform.parent : transform);
            UiKit.Stretch(_overlay);
            Image blocker = UiKit.Panel(_overlay, new Color(0f, 0f, 0f, 0f));
            blocker.raycastTarget = false;

            RectTransform bar = UiKit.Rect("TopBar", _overlay);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.sizeDelta = new Vector2(0f, UiTheme.MinTouch + 40f);
            UiKit.Panel(bar, new Color(0.07f, 0.05f, 0.18f, 0.75f));
            _uiRects.Add(bar);
            var row = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(20, 20, 20, 20);
            row.spacing = 14f;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = true;
            BarButton(bar, "Fireworks", UiTheme.IndigoLight, UiTheme.OffWhite, Home);
            _celebrateHere = BarButton(bar, "Celebrate here", UiTheme.Saffron, UiTheme.Indigo, () =>
            {
                if (HereSpot != null)
                {
                    Celebrate(HereSpot);
                }
            });
            BarButton(bar, "Map", UiTheme.IndigoLight, UiTheme.OffWhite, ToggleMap);
            _stickLabel = BarButton(bar, "Joystick off", UiTheme.IndigoLight, UiTheme.OffWhite, ToggleJoystick).GetComponentInChildren<TextMeshProUGUI>();

            RectTransform line = UiKit.Rect("Message", _overlay);
            line.anchorMin = new Vector2(0f, 1f);
            line.anchorMax = new Vector2(1f, 1f);
            line.pivot = new Vector2(0.5f, 1f);
            line.sizeDelta = new Vector2(-40f, 90f);
            line.anchoredPosition = new Vector2(0f, -UiTheme.MinTouch - 52f);
            UiKit.Panel(line, new Color(0.07f, 0.05f, 0.18f, 0.7f), raycast: false);
            RectTransform text = UiKit.Rect("Text", line);
            UiKit.Stretch(text, 12f);
            _message = UiKit.Text(text, string.Empty, UiTheme.SmallText, TextAlignmentOptions.Center, UiTheme.OffWhite);
            _message.textWrappingMode = TextWrappingModes.Normal;

            BuildJoystick();
            BuildMapSheet();

            RectTransform fade = UiKit.Rect("Fade", _overlay);
            UiKit.Stretch(fade);
            _fade = UiKit.Panel(fade, new Color(0f, 0f, 0f, 0f), raycast: false);
            _overlay.gameObject.SetActive(false);
        }

        private void BuildJoystick()
        {
            _stick = UiKit.Rect("Joystick", _overlay);
            _stick.anchorMin = _stick.anchorMax = _stick.pivot = new Vector2(0f, 0f);
            _stick.sizeDelta = new Vector2(StickRadius * 2f, StickRadius * 2f);
            _stick.anchoredPosition = new Vector2(60f, 90f);
            Image ring = UiKit.Panel(_stick, new Color(1f, 1f, 1f, 0.12f));
            ring.sprite = UiKit.Circle;
            _uiRects.Add(_stick);
            _knob = UiKit.Rect("Knob", _stick);
            _knob.sizeDelta = new Vector2(110f, 110f);
            Image knob = UiKit.Panel(_knob, new Color(1f, 0.76f, 0.16f, 0.8f), raycast: false);
            knob.sprite = UiKit.Circle;
            var drag = _stick.gameObject.AddComponent<StickInput>();
            drag.Moved += v =>
            {
                _knob.anchoredPosition = v * StickRadius;
                _controller.Stick = v;
            };
            _stick.gameObject.SetActive(false);
        }

        private void BuildMapSheet()
        {
            _map = UiKit.Rect("Map", _overlay);
            _map.anchorMin = _map.anchorMax = new Vector2(0.5f, 0.5f);
            _map.sizeDelta = new Vector2(MapWidth + 60f, MapHeight + 150f);
            UiKit.Panel(_map, new Color(0.09f, 0.06f, 0.2f, 0.97f));
            _uiRects.Add(_map);
            RectTransform title = UiKit.Rect("Title", _map);
            title.anchorMin = new Vector2(0f, 1f);
            title.anchorMax = new Vector2(1f, 1f);
            title.pivot = new Vector2(0.5f, 1f);
            title.sizeDelta = new Vector2(-40f, 90f);
            UiKit.Text(title, "Where to?", UiTheme.BodyText, TextAlignmentOptions.Center, UiTheme.Marigold);

            // The environment browser: choose a different neighbourhood.
            RectTransform browse = UiKit.Rect("Neighbourhoods", _map);
            browse.anchorMin = browse.anchorMax = browse.pivot = new Vector2(1f, 1f);
            browse.sizeDelta = new Vector2(300f, 76f);
            browse.anchoredPosition = new Vector2(-16f, -8f);
            Button neighbourhoods = BarButton(browse, "Neighbourhoods", UiTheme.IndigoLight, UiTheme.OffWhite, () =>
            {
                EnvironmentBrowser browser = FindFirstObjectByType<EnvironmentBrowser>();
                if (browser != null)
                {
                    _map.gameObject.SetActive(false);
                    browser.Open();
                }
            });
            UiKit.Stretch((RectTransform)neighbourhoods.transform);
            _mapPlan = UiKit.Rect("Plan", _map);
            _mapPlan.anchorMin = _mapPlan.anchorMax = new Vector2(0.5f, 0.5f);
            _mapPlan.sizeDelta = new Vector2(MapWidth, MapHeight);
            _mapPlan.anchoredPosition = new Vector2(0f, -30f);

            _card = UiKit.Rect("SpotCard", _map);
            _card.anchorMin = new Vector2(0.05f, 0f);
            _card.anchorMax = new Vector2(0.95f, 0f);
            _card.pivot = new Vector2(0.5f, 0f);
            _card.sizeDelta = new Vector2(0f, 300f);
            _card.anchoredPosition = new Vector2(0f, 20f);
            UiKit.Panel(_card, new Color(0.16f, 0.11f, 0.33f, 0.98f));
            RectTransform cardTitle = UiKit.Rect("Title", _card);
            cardTitle.anchorMin = new Vector2(0f, 1f);
            cardTitle.anchorMax = new Vector2(1f, 1f);
            cardTitle.pivot = new Vector2(0.5f, 1f);
            cardTitle.sizeDelta = new Vector2(-40f, 64f);
            _cardTitle = UiKit.Text(cardTitle, string.Empty, UiTheme.BodyText, TextAlignmentOptions.Center, UiTheme.Marigold);
            RectTransform reason = UiKit.Rect("Reason", _card);
            reason.anchorMin = new Vector2(0f, 0.42f);
            reason.anchorMax = new Vector2(1f, 0.78f);
            reason.offsetMin = new Vector2(24f, 0f);
            reason.offsetMax = new Vector2(-24f, 0f);
            _cardReason = UiKit.Text(reason, string.Empty, UiTheme.SmallText, TextAlignmentOptions.Center, UiTheme.OffWhite);
            _cardReason.textWrappingMode = TextWrappingModes.Normal;
            RectTransform buttons = UiKit.Rect("Buttons", _card);
            buttons.anchorMin = new Vector2(0f, 0f);
            buttons.anchorMax = new Vector2(1f, 0.4f);
            buttons.offsetMin = new Vector2(20f, 16f);
            buttons.offsetMax = new Vector2(-20f, -4f);
            var h = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 14f;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = true;
            _cardGo = BarButton(buttons, "Go there", UiTheme.IndigoLight, UiTheme.OffWhite, () => GoTo(_cardSpot));
            _cardCelebrate = BarButton(buttons, "Celebrate here", UiTheme.Saffron, UiTheme.Indigo, () => Celebrate(_cardSpot));
            _card.gameObject.SetActive(false);
            _map.gameObject.SetActive(false);
        }

        /// <summary>Draws the plan: the open space and street, every lot (home in saffron), a pin per spot and the avatar's dot.</summary>
        private void BuildMap()
        {
            foreach (GameObject go in _mapItems)
            {
                Destroy(go);
            }

            _mapItems.Clear();
            SettlementLayout layout = _controller.Layout;
            if (layout == null)
            {
                return;
            }

            float x0 = layout.street.x0 - 1f, x1 = layout.street.x1 + 1f, z0 = -9f, z1 = layout.open.z1 + 13f;
            _scale = Mathf.Min(MapWidth / (x1 - x0), MapHeight / (z1 - z0));
            _x0 = (x0 + x1) / 2f;
            _z0 = (z0 + z1) / 2f;
            Block(layout.open.x0, layout.open.z0, layout.open.x1, layout.open.z1, new Color(0.3f, 0.24f, 0.2f, 1f), "Open");
            Block(layout.street.x0, layout.street.z0, layout.street.x1, layout.street.z1, new Color(0.42f, 0.34f, 0.28f, 1f), "Street");
            foreach (LotDef lot in layout.lots)
            {
                Box3 f = lot.Footprint;
                Color c = lot.home ? UiTheme.Saffron : lot.vacant ? new Color(0.25f, 0.2f, 0.16f, 1f) : new Color(0.2f, 0.15f, 0.32f, 1f);
                Block(f.Min.X, f.Min.Z, f.Max.X, f.Max.Z, c, lot.id);
            }

            PinCount = 0;
            foreach (SpotDef spot in layout.spots)
            {
                Pin(spot);
            }

            _marker = UiKit.Rect("You", _mapPlan);
            _marker.sizeDelta = new Vector2(30f, 30f);
            UiKit.Panel(_marker, UiTheme.Magenta, raycast: false).sprite = UiKit.Circle;
            _mapItems.Add(_marker.gameObject);
        }

        private void Block(float ax, float az, float bx, float bz, Color c, string name)
        {
            RectTransform r = UiKit.Rect(name, _mapPlan);
            Vector2 a = MapPoint(ax, az), b = MapPoint(bx, bz);
            r.anchoredPosition = (a + b) / 2f;
            r.sizeDelta = new Vector2(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
            UiKit.Panel(r, c, raycast: false);
            _mapItems.Add(r.gameObject);
        }

        private void Pin(SpotDef spot)
        {
            RectTransform r = UiKit.Rect("Pin " + spot.id, _mapPlan);
            r.anchoredPosition = MapPoint(spot.x, spot.z);
            r.sizeDelta = new Vector2(UiTheme.MinTouch * 0.7f, UiTheme.MinTouch * 0.7f);
            Image dot = UiKit.Panel(r, UiTheme.Marigold);
            dot.sprite = UiKit.Circle;
            var button = r.gameObject.AddComponent<Button>();
            button.targetGraphic = dot;
            string id = spot.id;
            button.onClick.AddListener(() => ShowCard(id));
            bool open = !Game.Services.TryGet(out CelebrationService celebration) || celebration.Availability(id).Open;
            dot.color = open ? UiTheme.Marigold : new Color(0.5f, 0.45f, 0.5f, 1f);
            RectTransform label = UiKit.Rect("Label", r);
            label.anchorMin = label.anchorMax = new Vector2(0.5f, 0f);
            label.pivot = new Vector2(0.5f, 1f);
            label.sizeDelta = new Vector2(220f, 50f);
            TextMeshProUGUI t = UiKit.Text(label, spot.name, 22f, TextAlignmentOptions.Center, UiTheme.OffWhite);
            t.textWrappingMode = TextWrappingModes.Normal;
            _mapItems.Add(r.gameObject);
            PinCount++;
        }

        private static string AerialNote(SpotDef spot) => CelebrationRules.RuleOf(spot) switch
        {
            Core.Placement.AerialRule.Pads => "Rockets and sky shots go up from the launch pads here.",
            Core.Placement.AerialRule.None => "Small crackers only here: sparklers fit, rockets and big fountains don't.",
            _ => "Open sky for rockets.",
        };

        private Vector2 MapPoint(float x, float z) => new Vector2((x - _x0) * _scale, (z - _z0) * _scale);

        private static void Shape(RectTransform parent, string name, Vector2 size, Vector2 at, float angle, Sprite sprite)
        {
            RectTransform r = UiKit.Rect(name, parent);
            r.sizeDelta = size;
            r.anchoredPosition = at;
            r.localRotation = Quaternion.Euler(0f, 0f, angle);
            Image i = UiKit.Panel(r, UiTheme.OffWhite, raycast: false);
            if (sprite != null)
            {
                i.sprite = sprite;
            }
        }

        private static Button BarButton(RectTransform parent, string label, Color background, Color text, Action action)
        {
            RectTransform rect = UiKit.Rect(label, parent);
            Image image = UiKit.Panel(rect, background);
            image.sprite = UiKit.Rounded;
            image.type = Image.Type.Sliced;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            TextMeshProUGUI t = UiKit.Text(rect, label, UiTheme.SmallText, TextAlignmentOptions.Center, text);
            t.enableAutoSizing = true;
            t.fontSizeMin = 18f;
            t.fontSizeMax = UiTheme.SmallText * UiKit.TextScale;
            return button;
        }
    }
}

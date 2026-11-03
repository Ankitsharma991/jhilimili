using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UtsavNights.Core.Explore;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;

namespace UtsavNights.UI
{
    /// <summary>
    /// The Explore screen (Phase 12 step 1). An Explore button sits bottom left of the HUD. Exploring, the rest of the HUD
    /// steps aside for game controls: a floating thumbstick on the left, drag anywhere else to look (pinch to zoom), Jump
    /// and Sprint on the right, an Interact button that appears with what it will do, and Exit. A keyboard works too
    /// (WASD, Shift, Space, E, Esc).
    /// </summary>
    public sealed class ExploreMode : MonoBehaviour
    {
        private readonly List<GameObject> _hud = new List<GameObject>();
        private RectTransform _launcher;
        private RectTransform _overlay;
        private VirtualStick _stick;
        private Image _sprintImage;
        private RectTransform _interact;
        private TextMeshProUGUI _interactLabel;
        private TextMeshProUGUI _speed;
        private ExploreController _controller;
        private bool _sprint;
        private bool _holdToSprint;
        private RectTransform _stickArea;
        private RectTransform _stickBase;
        private RectTransform _jump;
        private RectTransform _sprintButton;
        private RectTransform _pause;
        private Slider _lookSlider;
        private Slider _sizeSlider;
        private SwitchView _invert;
        private SwitchView _recentre;
        private SwitchView _hold;
        private SwitchView _left;
        private bool _binding;

        /// <summary>Screens that stay visible over Explore (the rest of the HUD steps aside).</summary>
        private static readonly HashSet<string> KeepVisible = new HashSet<string> { "ProfileScreen" };

        public bool Paused => _pause != null && _pause.gameObject.activeSelf;

        public bool IsOpen => _controller != null && _controller.IsOpen;

        public RectTransform Launcher => _launcher;

        public ExploreController Controller => _controller;

        public VirtualStick Stick => _stick;

        public bool Sprinting => _sprint;

        public string InteractLabel => _interact != null && _interact.gameObject.activeSelf ? _interactLabel.text : string.Empty;

        private void Awake() => Build();

        private void Start()
        {
            Bind();
            InputBlockers.Register(_launcher);
        }

        private void OnDestroy()
        {
            if (_controller != null)
            {
                _controller.StateChanged -= Refresh;
            }

            Game.Events.Unsubscribe<Core.Settings.SettingsChangedEvent>(OnSettings);
            if (Paused)
            {
                Time.timeScale = 1f;
            }

            InputBlockers.Unregister(_launcher);
            InputBlockers.Unregister(_overlay);
        }

        /// <summary>Same as tapping Explore.</summary>
        public bool Open()
        {
            Bind();
            return _controller != null && _controller.Open();
        }

        /// <summary>Same as tapping Exit.</summary>
        public void Exit() => _controller?.Close();

        public void ToggleSprint() => SetSprint(!_sprint);

        public void SetSprint(bool on)
        {
            _sprint = on;
            _sprintImage.color = _sprint ? UiTheme.Saffron : UiTheme.IndigoLight;
        }

        /// <summary>Same as tapping Pause: the game stops behind the menu.</summary>
        public void Pause()
        {
            if (!IsOpen)
            {
                return;
            }

            BindControls();
            _pause.gameObject.SetActive(true);
            _pause.SetAsLastSibling();
            Time.timeScale = 0f;
        }

        public void Resume()
        {
            _pause.gameObject.SetActive(false);
            Time.timeScale = 1f;
        }

        /// <summary>Same as tapping Profile in the pause menu.</summary>
        public void OpenProfile()
        {
            ProfileScreen profile = FindFirstObjectByType<ProfileScreen>(FindObjectsInactive.Include);
            profile?.Open();
        }

        /// <summary>Puts the stick and the buttons on the sides the player chose, at the size they chose.</summary>
        public void ApplyLayout(bool leftHanded, float scale)
        {
            _stickArea.anchorMin = new Vector2(leftHanded ? 0.55f : 0f, 0f);
            _stickArea.anchorMax = new Vector2(leftHanded ? 1f : 0.45f, 0.6f);
            Vector2 corner = new Vector2(leftHanded ? 0f : 1f, 0f);
            float side = leftHanded ? 1f : -1f;
            Place(_jump, corner, new Vector2(side * 60f, 60f), scale);
            Place(_sprintButton, corner, new Vector2(side * 270f, 40f), scale);
            Place(_interact, corner, new Vector2(side * 60f, 280f), scale);
            _stickBase.localScale = Vector3.one * scale;
        }

        private static void Place(RectTransform rect, Vector2 corner, Vector2 position, float scale)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = corner;
            rect.anchoredPosition = position * scale;
            rect.localScale = Vector3.one * scale;
        }

        private void Update()
        {
            if (_controller == null)
            {
                return;
            }

            if (Paused)
            {
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    Resume();
                }

                return;
            }

            _launcher.gameObject.SetActive(!IsOpen && CanExplore());
            if (!IsOpen)
            {
                return;
            }

            Vector2 move = _stick.Value;
            bool run = _sprint;
            Keyboard k = Keyboard.current;
            if (k != null)
            {
                var keys = new Vector2((k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0), (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0));
                if (keys != Vector2.zero)
                {
                    move = keys.normalized * (k.leftCtrlKey.isPressed ? 0.45f : 1f);
                }

                run |= k.leftShiftKey.isPressed;
                if (k.spaceKey.wasPressedThisFrame)
                {
                    _controller.Jump();
                }

                if (k.eKey.wasPressedThisFrame)
                {
                    _controller.Interact();
                }

                if (k.escapeKey.wasPressedThisFrame)
                {
                    Pause();
                    return;
                }
            }

            _controller.Stick = move;
            _controller.Sprint = run;
            Gait g = _controller.Gait;
            _speed.text = g == Gait.Idle ? string.Empty : g.ToString();
        }

        private bool CanExplore() => !Game.Services.TryGet(out HomeService home) || !home.Visiting;

        private void OnSettings(Core.Settings.SettingsChangedEvent e) => ApplySettings(e.Settings);

        private void ApplySettings(Core.Settings.GameSettings s)
        {
            _holdToSprint = s.holdToSprint;
            ApplyLayout(s.leftHanded, s.hudScale);
            if (_holdToSprint && _sprint)
            {
                SetSprint(false);
            }
        }

        private void BindControls()
        {
            if (!Game.Services.TryGet(out SettingsService settings))
            {
                return;
            }

            Core.Settings.GameSettings s = settings.Current;
            _binding = true;
            _lookSlider.value = ControlRanges.LookSlider(s.lookSensitivity);
            _sizeSlider.value = ControlRanges.HudSlider(s.hudScale);
            _invert.Set(s.invertLook, false);
            _recentre.Set(s.autoRecentre, false);
            _hold.Set(s.holdToSprint, false);
            _left.Set(s.leftHanded, false);
            _binding = false;
        }

        private void Edit(System.Action<Core.Settings.GameSettings> edit, bool saveNow)
        {
            if (!_binding && Game.Services.TryGet(out SettingsService settings))
            {
                settings.Change(edit, saveNow);
            }
        }

        private void Bind()
        {
            if (_controller != null)
            {
                return;
            }

            if (Game.Services.TryGet(out SettingsService settings))
            {
                ApplySettings(settings.Current);
            }

            Game.Events.Subscribe<Core.Settings.SettingsChangedEvent>(OnSettings);

            _controller = FindFirstObjectByType<ExploreController>();
            if (_controller != null)
            {
                _controller.StateChanged += Refresh;
            }
        }

        private void Refresh()
        {
            bool open = IsOpen;
            if (open && !_overlay.gameObject.activeSelf)
            {
                HideHud();
                _overlay.gameObject.SetActive(true);
                _overlay.SetAsLastSibling();
                InputBlockers.Register(_overlay);
            }
            else if (!open && _overlay.gameObject.activeSelf)
            {
                if (Paused)
                {
                    Resume();
                }

                _overlay.gameObject.SetActive(false);
                InputBlockers.Unregister(_overlay);
                ShowHud();
                _stick.Set(Vector2.zero);
                if (_sprint)
                {
                    ToggleSprint();
                }
            }

            if (open)
            {
                bool any = !string.IsNullOrEmpty(_controller.Prompt);
                _interact.gameObject.SetActive(any);
                _interactLabel.text = _controller.Prompt;
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
                if (sibling != transform && sibling != _overlay && !KeepVisible.Contains(sibling.name) && sibling.gameObject.activeSelf)
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

        private void Build()
        {
            _launcher = UiKit.Rect("ExploreButton", transform);
            _launcher.anchorMin = _launcher.anchorMax = _launcher.pivot = new Vector2(0f, 0f);
            _launcher.sizeDelta = new Vector2(240f, UiTheme.MinTouch);
            _launcher.anchoredPosition = new Vector2(32f, TrayView.TrayHeight + 150f);
            Button(_launcher, "Explore", UiTheme.Saffron, UiTheme.Indigo, () => Open());

            _overlay = UiKit.Rect("Explore", transform.parent != null ? transform.parent : transform);
            UiKit.Stretch(_overlay);

            // Everywhere not covered by a control: look and zoom.
            Image look = _overlay.gameObject.AddComponent<Image>();
            look.color = new Color(0f, 0f, 0f, 0f);
            LookPad pad = _overlay.gameObject.AddComponent<LookPad>();
            pad.Looked += d => _controller?.Look(d);
            pad.Zoomed += m => _controller?.Orbit.Zoom(m);

            // The thumbstick: the lower left 45% of the screen, centred where the thumb lands.
            RectTransform area = _stickArea = Area(_overlay, "StickArea", 0f, 0f, 0.45f, 0.6f, Vector2.zero, Vector2.zero);
            Image areaImage = area.gameObject.AddComponent<Image>();
            areaImage.color = new Color(0f, 0f, 0f, 0f);
            RectTransform stickBase = _stickBase = UiKit.Rect("Base", area);
            stickBase.anchorMin = stickBase.anchorMax = Vector2.zero;
            stickBase.sizeDelta = new Vector2(VirtualStick.Reach * 2f + 60f, VirtualStick.Reach * 2f + 60f);
            stickBase.anchoredPosition = new Vector2(260f, 260f);
            Image ring = UiKit.Panel(stickBase, new Color(1f, 1f, 1f, 0.12f), raycast: false);
            ring.sprite = UiKit.Circle;
            RectTransform knob = UiKit.Rect("Knob", stickBase);
            knob.sizeDelta = new Vector2(110f, 110f);
            Image knobImage = UiKit.Panel(knob, new Color(1f, 1f, 1f, 0.45f), raycast: false);
            knobImage.sprite = UiKit.Circle;
            _stick = area.gameObject.AddComponent<VirtualStick>();
            _stick.Configure(area, stickBase, knob);

            // Right thumb: Jump, Sprint, Interact.
            RectTransform jump = _jump = Round(_overlay, "Jump", new Vector2(-60f, 60f), 180f, UiTheme.IndigoLight);
            Button(jump, "Jump", UiTheme.IndigoLight, UiTheme.OffWhite, () => _controller?.Jump()).sprite = UiKit.Circle;

            // Sprint: tap on and off, or hold (the player's choice in the pause menu and Settings).
            RectTransform sprint = _sprintButton = Round(_overlay, "Sprint", new Vector2(-270f, 40f), 150f, UiTheme.IndigoLight);
            _sprintImage = UiKit.Panel(sprint, UiTheme.IndigoLight);
            _sprintImage.sprite = UiKit.Circle;
            UiKit.Text(sprint, "Sprint", UiTheme.BodyText, TextAlignmentOptions.Center, UiTheme.OffWhite);
            HoldButton hold = sprint.gameObject.AddComponent<HoldButton>();
            hold.Pressed += () => SetSprint(_holdToSprint || !_sprint);
            hold.Released += () =>
            {
                if (_holdToSprint)
                {
                    SetSprint(false);
                }
            };
            _interact = UiKit.Rect("Interact", _overlay);
            _interact.anchorMin = _interact.anchorMax = _interact.pivot = new Vector2(1f, 0f);
            _interact.sizeDelta = new Vector2(420f, UiTheme.MinTouch);
            _interact.anchoredPosition = new Vector2(-60f, 280f);
            Button(_interact, string.Empty, UiTheme.Saffron, UiTheme.Indigo, () => _controller?.Interact()).sprite = UiKit.Rounded;
            _interactLabel = _interact.GetComponentInChildren<TextMeshProUGUI>();
            _interact.gameObject.SetActive(false);

            RectTransform exit = UiKit.Rect("Exit", _overlay);
            exit.anchorMin = exit.anchorMax = exit.pivot = new Vector2(0f, 1f);
            exit.sizeDelta = new Vector2(200f, UiTheme.MinTouch);
            exit.anchoredPosition = new Vector2(28f, -24f);
            Button(exit, "Exit", UiTheme.IndigoLight, UiTheme.OffWhite, Exit).sprite = UiKit.Rounded;

            RectTransform pauseButton = UiKit.Rect("PauseButton", _overlay);
            pauseButton.anchorMin = pauseButton.anchorMax = pauseButton.pivot = new Vector2(1f, 1f);
            pauseButton.sizeDelta = new Vector2(200f, UiTheme.MinTouch);
            pauseButton.anchoredPosition = new Vector2(-28f, -24f);
            Button(pauseButton, "Pause", UiTheme.IndigoLight, UiTheme.OffWhite, Pause).sprite = UiKit.Rounded;
            BuildPause();

            RectTransform speed = Area(_overlay, "Gait", 0.35f, 1f, 0.65f, 1f, new Vector2(0f, -UiTheme.MinTouch - 24f), new Vector2(0f, -24f));
            _speed = UiKit.Text(speed, string.Empty, UiTheme.SmallText, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.5f));
            _speed.raycastTarget = false;
            _overlay.gameObject.SetActive(false);
        }

        private void BuildPause()
        {
            _pause = UiKit.Rect("PauseMenu", _overlay);
            UiKit.Stretch(_pause);
            UiKit.Panel(_pause, new Color(0.05f, 0.03f, 0.12f, 0.88f));
            RectTransform card = Area(_pause, "Card", 0.2f, 0.06f, 0.8f, 0.94f, Vector2.zero, Vector2.zero);
            UiKit.Panel(card, new Color(0.11f, 0.07f, 0.24f, 1f)).sprite = UiKit.Rounded;
            VerticalLayoutGroup list = card.gameObject.AddComponent<VerticalLayoutGroup>();
            list.padding = new RectOffset(32, 32, 28, 28);
            list.spacing = 12f;
            list.childControlWidth = true;
            list.childControlHeight = true;
            list.childForceExpandWidth = true;
            list.childForceExpandHeight = false;
            Row(card, "Paused", 64f, r => UiKit.Text(r, "Paused", UiTheme.TitleText, TextAlignmentOptions.Center, UiTheme.Marigold));
            Row(card, "Resume", UiTheme.MinTouch, r => Button(r, "Resume", UiTheme.Saffron, UiTheme.Indigo, Resume).sprite = UiKit.Rounded);
            Row(card, "Profile", UiTheme.MinTouch, r => Button(r, "Your profile", UiTheme.IndigoLight, UiTheme.OffWhite, OpenProfile).sprite = UiKit.Rounded);
            Row(card, "Look", UiTheme.MinTouch, r =>
            {
                Label(r, "Look speed");
                _lookSlider = SliderFactory.Create(r);
                Right((RectTransform)_lookSlider.transform);
                _lookSlider.onValueChanged.AddListener(v => Edit(s => s.lookSensitivity = ControlRanges.Look(v), false));
            });
            _invert = SwitchRow(card, "Invert up and down", on => Edit(s => s.invertLook = on, true));
            _recentre = SwitchRow(card, "Camera follows behind", on => Edit(s => s.autoRecentre = on, true));
            _hold = SwitchRow(card, "Hold to sprint", on => Edit(s => s.holdToSprint = on, true));
            _left = SwitchRow(card, "Left-handed controls", on => Edit(s => s.leftHanded = on, true));
            Row(card, "Size", UiTheme.MinTouch, r =>
            {
                Label(r, "Controls size");
                _sizeSlider = SliderFactory.Create(r);
                Right((RectTransform)_sizeSlider.transform);
                _sizeSlider.onValueChanged.AddListener(v => Edit(s => s.hudScale = ControlRanges.Hud(v), false));
            });
            Row(card, "Exit", UiTheme.MinTouch, r => Button(r, "Back to the courtyard", UiTheme.IndigoLight, UiTheme.OffWhite, () =>
            {
                Resume();
                Exit();
            }).sprite = UiKit.Rounded);
            _pause.gameObject.SetActive(false);
        }

        private SwitchView SwitchRow(RectTransform card, string label, System.Action<bool> changed)
        {
            SwitchView view = null;
            Row(card, label, UiTheme.MinTouch, r =>
            {
                Label(r, label);
                view = SwitchView.Create(r);
                Right((RectTransform)view.transform);
                view.Changed += changed;
            });
            return view;
        }

        private static void Row(RectTransform parent, string name, float height, System.Action<RectTransform> fill)
        {
            RectTransform row = UiKit.Rect(name, parent);
            LayoutElement e = row.gameObject.AddComponent<LayoutElement>();
            e.minHeight = e.preferredHeight = height;
            fill(row);
        }

        private static void Label(RectTransform row, string text) =>
            UiKit.Text(Area(row, "Label", 0f, 0f, 0.55f, 1f, Vector2.zero, Vector2.zero), text, UiTheme.BodyText, TextAlignmentOptions.Left, UiTheme.OffWhite);

        private static void Right(RectTransform control)
        {
            control.anchorMin = control.anchorMax = control.pivot = new Vector2(1f, 0.5f);
            control.anchoredPosition = Vector2.zero;
        }

        private static RectTransform Round(RectTransform parent, string name, Vector2 position, float size, Color colour)
        {
            RectTransform rect = UiKit.Rect(name, parent);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = position;
            return rect;
        }

        private static Image Button(RectTransform rect, string label, Color background, Color text, System.Action action)
        {
            Image image = UiKit.Panel(rect, background);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            UiKit.Text(rect, label, UiTheme.BodyText, TextAlignmentOptions.Center, text);
            return image;
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

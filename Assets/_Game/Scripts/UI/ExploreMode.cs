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

        public void ToggleSprint()
        {
            _sprint = !_sprint;
            _sprintImage.color = _sprint ? UiTheme.Saffron : UiTheme.IndigoLight;
        }

        private void Update()
        {
            if (_controller == null)
            {
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
                    Exit();
                    return;
                }
            }

            _controller.Stick = move;
            _controller.Sprint = run;
            Gait g = _controller.Gait;
            _speed.text = g == Gait.Idle ? string.Empty : g.ToString();
        }

        private bool CanExplore() => !Game.Services.TryGet(out HomeService home) || !home.Visiting;

        private void Bind()
        {
            if (_controller != null)
            {
                return;
            }

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
                if (sibling != transform && sibling != _overlay && sibling.gameObject.activeSelf)
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
            RectTransform area = Area(_overlay, "StickArea", 0f, 0f, 0.45f, 0.6f, Vector2.zero, Vector2.zero);
            Image areaImage = area.gameObject.AddComponent<Image>();
            areaImage.color = new Color(0f, 0f, 0f, 0f);
            RectTransform stickBase = UiKit.Rect("Base", area);
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
            RectTransform jump = Round(_overlay, "Jump", new Vector2(-60f, 60f), 180f, UiTheme.IndigoLight);
            Button(jump, "Jump", UiTheme.IndigoLight, UiTheme.OffWhite, () => _controller?.Jump()).sprite = UiKit.Circle;
            RectTransform sprint = Round(_overlay, "Sprint", new Vector2(-270f, 40f), 150f, UiTheme.IndigoLight);
            _sprintImage = Button(sprint, "Sprint", UiTheme.IndigoLight, UiTheme.OffWhite, ToggleSprint);
            _sprintImage.sprite = UiKit.Circle;
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

            RectTransform speed = Area(_overlay, "Gait", 0.35f, 1f, 0.65f, 1f, new Vector2(0f, -UiTheme.MinTouch - 24f), new Vector2(0f, -24f));
            _speed = UiKit.Text(speed, string.Empty, UiTheme.SmallText, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.5f));
            _speed.raycastTarget = false;
            _overlay.gameObject.SetActive(false);
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

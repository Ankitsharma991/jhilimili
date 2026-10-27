using System;
using UnityEngine;
using UnityEngine.Accessibility;
using UnityEngine.UI;

namespace UtsavNights.UI
{
    /// <summary>
    /// TalkBack support (PRD 14). While the system screen reader is on, mirrors the visible controls into
    /// Unity's accessibility hierarchy: the settings button, tray items, the finale button and the hint,
    /// or the settings sheet's controls while it is open. Double-tap invokes, swipe up or down adjusts
    /// sliders. Lighting a fuse stays a gesture on the 3D scene; the tray and finale cover the rest.
    /// </summary>
    public sealed class ScreenReaderSupport : MonoBehaviour
    {
        private const float CheckSeconds = 0.25f;
        private const float SliderStep = 0.1f;

        private TrayView _tray;
        private SettingsSheet _sheet;
        private FinaleButton _finale;
        private AccessibilityHierarchy _hierarchy;
        private string _signature;
        private float _nextCheck;

        public AccessibilityHierarchy Hierarchy => _hierarchy;

        private void OnEnable() => AssistiveSupport.screenReaderStatusChanged += OnStatusChanged;

        private void OnDisable()
        {
            AssistiveSupport.screenReaderStatusChanged -= OnStatusChanged;
            Clear();
        }

        private void Start()
        {
            _tray = FindFirstObjectByType<TrayView>();
            _sheet = FindFirstObjectByType<SettingsSheet>();
            _finale = FindFirstObjectByType<FinaleButton>();
        }

        private void OnStatusChanged(bool enabled)
        {
            _signature = null;
            if (!enabled)
            {
                Clear();
            }
        }

        private void Update()
        {
            if (!AssistiveSupport.isScreenReaderEnabled || Time.unscaledTime < _nextCheck)
            {
                return;
            }

            _nextCheck = Time.unscaledTime + CheckSeconds;
            Rebuild(false);
        }

        /// <summary>Builds the hierarchy for what is on screen now; only replaces it when that changed.</summary>
        public void Rebuild(bool force)
        {
            string signature = Signature();
            if (!force && signature == _signature)
            {
                _hierarchy?.RefreshNodeFrames();
                return;
            }

            bool screenChanged = _signature == null || (_signature[0] != signature[0]);
            _signature = signature;
            _hierarchy = new AccessibilityHierarchy();
            if (_sheet != null && _sheet.IsOpen)
            {
                BuildSheet();
            }
            else
            {
                BuildMain();
            }

            AssistiveSupport.activeHierarchy = _hierarchy;
            if (screenChanged)
            {
                AssistiveSupport.notificationDispatcher.SendScreenChanged(null);
            }
            else
            {
                AssistiveSupport.notificationDispatcher.SendLayoutChanged(null);
            }
        }

        // First character says which screen is showing, the rest catches label and selection changes.
        private string Signature()
        {
            bool open = _sheet != null && _sheet.IsOpen;
            string tray = _tray != null ? _tray.SelectedIndex + ":" + _tray.HintText : string.Empty;
            string finale = _finale != null ? _finale.Label : string.Empty;
            return (open ? "S" : "M") + tray + "|" + finale;
        }

        private void BuildMain()
        {
            if (_sheet != null && _sheet.OpenButton != null)
            {
                Button("Settings", _sheet.OpenButton, () => _sheet.Open());
            }

            if (_tray != null)
            {
                AccessibilityNode header = _hierarchy.AddNode("Crackers", null);
                header.role = AccessibilityRole.Header;
                header.frameGetter = FrameOf(_tray.Panel);
                for (int i = 0; i < _tray.ItemCount; i++)
                {
                    int index = i;
                    AccessibilityNode item = _hierarchy.AddNode(_tray.ItemLabel(i), null);
                    item.role = AccessibilityRole.Button;
                    item.frameGetter = FrameOf(_tray.ItemRect(i));
                    if (_tray.ItemLocked(i))
                    {
                        item.hint = "Premium. Double-tap for unlock options";
                        item.invoked += () =>
                        {
                            _tray.Select(index);
                            return true;
                        };
                        continue;
                    }

                    item.state = _tray.SelectedIndex == i ? AccessibilityState.Selected : AccessibilityState.None;
                    item.hint = "Double-tap to choose, then tap the ground to place";
                    item.invoked += () =>
                    {
                        _tray.Select(index);
                        return true;
                    };
                }

                if (!string.IsNullOrEmpty(_tray.HintText))
                {
                    AccessibilityNode hint = _hierarchy.AddNode(_tray.HintText, null);
                    hint.role = AccessibilityRole.StaticText;
                }
            }

            if (_finale != null && _finale.Rect != null && _finale.Rect.gameObject.activeInHierarchy)
            {
                Button(_finale.Label, _finale.Rect, () => _finale.Press());
            }
        }

        private void BuildSheet()
        {
            AccessibilityNode header = _hierarchy.AddNode("Settings", null);
            header.role = AccessibilityRole.Header;
            foreach (SettingsSheet.AccessibleControl control in _sheet.Controls)
            {
                if (control.Rect == null || !control.Rect.gameObject.activeInHierarchy)
                {
                    continue;
                }

                AccessibilityNode node = _hierarchy.AddNode(control.Label, null);
                node.frameGetter = FrameOf(control.Rect);
                if (control.Switch != null)
                {
                    SwitchView view = control.Switch;
                    node.role = AccessibilityRole.Toggle;
                    node.value = view.IsOn ? "On" : "Off";
                    node.state = view.Interactable ? AccessibilityState.None : AccessibilityState.Disabled;
                    node.invoked += () =>
                    {
                        view.Set(!view.IsOn, true);
                        node.value = view.IsOn ? "On" : "Off";
                        return true;
                    };
                }
                else if (control.Slider != null)
                {
                    Slider slider = control.Slider;
                    node.role = AccessibilityRole.Slider;
                    node.value = Percent(slider);
                    node.state = slider.interactable ? AccessibilityState.None : AccessibilityState.Disabled;
                    node.incremented += () => Nudge(slider, node, SliderStep);
                    node.decremented += () => Nudge(slider, node, -SliderStep);
                }
                else if (control.Button != null)
                {
                    Button button = control.Button;
                    node.role = AccessibilityRole.Button;
                    node.invoked += () =>
                    {
                        button.onClick.Invoke();
                        return true;
                    };
                }
                else if (control.Segmented != null)
                {
                    SegmentedView segmented = control.Segmented;
                    int segment = control.Segment;
                    node.role = AccessibilityRole.TabButton;
                    node.state = segmented.Selected == segment ? AccessibilityState.Selected : AccessibilityState.None;
                    node.invoked += () =>
                    {
                        segmented.Set(segment, true);
                        _signature = null;
                        return true;
                    };
                }
            }

            if (_sheet.DoneButton != null)
            {
                Button("Done", _sheet.DoneButton, () => _sheet.Close());
            }
        }

        private void Button(string label, RectTransform rect, Action press)
        {
            AccessibilityNode node = _hierarchy.AddNode(label, null);
            node.role = AccessibilityRole.Button;
            node.frameGetter = FrameOf(rect);
            node.invoked += () =>
            {
                press();
                return true;
            };
        }

        private static void Nudge(Slider slider, AccessibilityNode node, float step)
        {
            if (!slider.interactable)
            {
                return;
            }

            slider.value = Mathf.Clamp(slider.value + step * (slider.maxValue - slider.minValue), slider.minValue, slider.maxValue);
            node.value = Percent(slider);
        }

        private static string Percent(Slider slider) =>
            Mathf.RoundToInt(Mathf.InverseLerp(slider.minValue, slider.maxValue, slider.value) * 100f) + " percent";

        private void Clear()
        {
            if (_hierarchy != null && AssistiveSupport.activeHierarchy == _hierarchy)
            {
                AssistiveSupport.activeHierarchy = null;
            }

            _hierarchy = null;
            _signature = null;
        }

        /// <summary>Screen rectangle of a UI element with a top-left origin, as the screen reader expects.</summary>
        public static Func<Rect> FrameOf(RectTransform rect) => () => ScreenFrame(rect);

        public static Rect ScreenFrame(RectTransform rect)
        {
            if (rect == null)
            {
                return Rect.zero;
            }

            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return new Rect(min.x, Screen.height - max.y, max.x - min.x, max.y - min.y);
        }
    }
}

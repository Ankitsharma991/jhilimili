using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UtsavNights.UI
{
    /// <summary>On/off switch with a sliding knob (accessible 48 dp target).</summary>
    public sealed class SwitchView : MonoBehaviour
    {
        private Image _track;
        private RectTransform _knob;
        private Button _button;

        public event Action<bool> Changed;

        public bool IsOn { get; private set; }

        public bool Interactable
        {
            get => _button.interactable;
            set
            {
                _button.interactable = value;
                Refresh();
            }
        }

        public static SwitchView Create(Transform parent)
        {
            RectTransform rect = UiKit.Rect("Switch", parent);
            rect.sizeDelta = new Vector2(150f, 84f);
            var view = rect.gameObject.AddComponent<SwitchView>();
            view._track = UiKit.Panel(rect, UiTheme.IndigoLight);
            view._track.sprite = UiKit.Circle;
            view._button = rect.gameObject.AddComponent<Button>();
            view._button.targetGraphic = view._track;
            view._button.transition = Selectable.Transition.None;
            view._button.onClick.AddListener(() => view.Set(!view.IsOn, true));
            view._knob = UiKit.Rect("Knob", rect);
            view._knob.anchorMin = view._knob.anchorMax = new Vector2(0f, 0.5f);
            view._knob.sizeDelta = new Vector2(68f, 68f);
            Image knob = view._knob.gameObject.AddComponent<Image>();
            knob.sprite = UiKit.Circle;
            knob.color = UiTheme.OffWhite;
            knob.raycastTarget = false;
            view.Refresh();
            return view;
        }

        public void Set(bool on, bool notify)
        {
            if (IsOn == on)
            {
                return;
            }

            IsOn = on;
            Refresh();
            if (notify)
            {
                Changed?.Invoke(on);
            }
        }

        private void Refresh()
        {
            if (_track == null)
            {
                return;
            }

            Color on = _button != null && !_button.interactable ? UiTheme.Muted : UiTheme.Saffron;
            _track.color = IsOn ? on : UiTheme.IndigoLight;
            _knob.anchoredPosition = new Vector2(IsOn ? 116f : 42f, 0f);
        }
    }

    /// <summary>Row of mutually exclusive choices (quality tier).</summary>
    public sealed class SegmentedView : MonoBehaviour
    {
        private readonly List<Image> _segments = new List<Image>();

        public event Action<int> Changed;

        public int Selected { get; private set; } = -1;

        public static SegmentedView Create(Transform parent, params string[] labels)
        {
            RectTransform rect = UiKit.Rect("Segmented", parent);
            rect.sizeDelta = new Vector2(labels.Length * 150f, 96f);
            var view = rect.gameObject.AddComponent<SegmentedView>();
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                RectTransform segment = UiKit.Rect(labels[i], rect);
                Image image = UiKit.Panel(segment, UiTheme.IndigoLight);
                var button = segment.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => view.Set(index, true));
                UiKit.Text(segment, labels[i], UiTheme.SmallText, TextAlignmentOptions.Center, UiTheme.OffWhite);
                view._segments.Add(image);
            }

            return view;
        }

        public void Set(int index, bool notify)
        {
            if (index == Selected)
            {
                return;
            }

            Selected = index;
            for (int i = 0; i < _segments.Count; i++)
            {
                _segments[i].color = i == index ? UiTheme.Saffron : UiTheme.IndigoLight;
            }

            if (notify)
            {
                Changed?.Invoke(index);
            }
        }
    }

    public static class SliderFactory
    {
        /// <summary>A 0..1 slider with a rounded track, saffron fill and a marigold handle.</summary>
        public static Slider Create(Transform parent)
        {
            RectTransform rect = UiKit.Rect("Slider", parent);
            rect.sizeDelta = new Vector2(420f, 96f);
            var slider = rect.gameObject.AddComponent<Slider>();

            RectTransform track = UiKit.Rect("Track", rect);
            track.anchorMin = new Vector2(0f, 0.5f);
            track.anchorMax = new Vector2(1f, 0.5f);
            track.sizeDelta = new Vector2(-36f, 18f);
            UiKit.Panel(track, UiTheme.IndigoLight).sprite = UiKit.Circle;

            RectTransform fillArea = UiKit.Rect("Fill Area", rect);
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.sizeDelta = new Vector2(-36f, 18f);
            RectTransform fill = UiKit.Rect("Fill", fillArea);
            fill.sizeDelta = Vector2.zero;
            Image fillImage = UiKit.Panel(fill, UiTheme.Saffron, raycast: false);
            fillImage.sprite = UiKit.Circle;

            RectTransform handleArea = UiKit.Rect("Handle Slide Area", rect);
            UiKit.Stretch(handleArea);
            handleArea.offsetMin = new Vector2(18f, 0f);
            handleArea.offsetMax = new Vector2(-18f, 0f);
            RectTransform handle = UiKit.Rect("Handle", handleArea);
            handle.sizeDelta = new Vector2(64f, 64f);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.sprite = UiKit.Circle;
            handleImage.color = UiTheme.Marigold;

            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.transition = Selectable.Transition.None;
            return slider;
        }
    }
}

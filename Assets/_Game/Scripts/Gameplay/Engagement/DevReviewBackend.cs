using System;
using UnityEngine;
using UnityEngine.UI;
using UtsavNights.Core;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Development review prompt: a small "Test review prompt" card where Google Play's rating dialog would
    /// appear, so the timing can be checked on a sideloaded APK (Play shows nothing there).
    /// </summary>
    public sealed class DevReviewBackend : MonoBehaviour, IReviewBackend
    {
        private GameObject _canvas;
        private Action<bool> _done;

        public string Name => "development";

        public bool Visible => _canvas != null && _canvas.activeSelf;

        public void Request(Action<bool> done)
        {
            if (Visible)
            {
                done?.Invoke(false);
                return;
            }

            if (_canvas == null)
            {
                Build();
            }

            _done = done;
            _canvas.SetActive(true);
            InputBlockers.Register((RectTransform)_canvas.transform);
        }

        public void Close()
        {
            if (!Visible)
            {
                return;
            }

            _canvas.SetActive(false);
            InputBlockers.Unregister((RectTransform)_canvas.transform);
            Action<bool> done = _done;
            _done = null;
            done?.Invoke(true);
        }

        private void OnDestroy()
        {
            if (_canvas != null)
            {
                InputBlockers.Unregister((RectTransform)_canvas.transform);
            }
        }

        private void Build()
        {
            _canvas = new GameObject("DevReviewCard", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas.transform.SetParent(transform, false);
            var canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 290;
            var scaler = _canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = SpikeMath.DpToPx(1f, Screen.dpi);

            var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            dim.transform.SetParent(_canvas.transform, false);
            Stretch((RectTransform)dim.transform);
            dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);

            var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(_canvas.transform, false);
            var cardRect = (RectTransform)card.transform;
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(300f, 190f);
            card.GetComponent<Image>().color = Color.white;

            var text = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(card.transform, false);
            var textRect = text.rectTransform;
            textRect.anchorMin = new Vector2(0f, 0.4f);
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 0f);
            textRect.offsetMax = new Vector2(-16f, -12f);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 17;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.1f, 0.1f, 0.1f);
            text.text = "Test review prompt\nGoogle Play's rating dialog appears here in the store build.";
            text.raycastTarget = false;

            var button = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
            button.transform.SetParent(card.transform, false);
            var buttonRect = (RectTransform)button.transform;
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 0.2f);
            buttonRect.sizeDelta = new Vector2(160f, 48f);
            button.GetComponent<Image>().color = new Color(0.25f, 0.45f, 0.85f, 1f);
            button.GetComponent<Button>().onClick.AddListener(Close);
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(button.transform, false);
            Stretch(label.rectTransform);
            label.font = text.font;
            label.fontSize = 18;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.text = "Not now";
            label.raycastTarget = false;
            _canvas.SetActive(false);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}

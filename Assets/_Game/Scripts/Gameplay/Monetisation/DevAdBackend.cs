using System;
using UnityEngine;
using UnityEngine.UI;
using UtsavNights.Core;
using UtsavNights.Core.Monetisation;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Development ad network: a plain full-screen "Test ad" card, so the whole ad flow works with no
    /// account, SDK or network. Rewarded cards unlock their Close-with-reward button after a short
    /// countdown and can be skipped without the reward, like a real rewarded ad.
    /// </summary>
    public sealed class DevAdBackend : MonoBehaviour, IAdBackend
    {
        public const float RewardedSeconds = 3f;
        public const float InterstitialSeconds = 1.5f;

        private GameObject _canvas;
        private Text _title;
        private Text _countdown;
        private Button _close;
        private Text _closeLabel;
        private Button _skip;
        private Action<AdOutcome> _done;
        private AdKind _kind;
        private float _closableAt;

        public string Name => "development";

        /// <summary>Test hook: the next Show fails as if nothing loaded.</summary>
        public bool FailNext { get; set; }

        public bool Visible => _canvas != null && _canvas.activeSelf;

        public bool IsReady(AdKind kind) => true;

        public void Load(AdKind kind)
        {
        }

        public void Show(AdKind kind, string placement, Action<AdOutcome> done)
        {
            if (FailNext || Visible)
            {
                FailNext = false;
                done?.Invoke(AdOutcome.Failed);
                return;
            }

            if (_canvas == null)
            {
                Build();
            }

            _kind = kind;
            _done = done;
            _closableAt = Time.unscaledTime + (kind == AdKind.Rewarded ? RewardedSeconds : InterstitialSeconds);
            _title.text = kind == AdKind.Rewarded ? $"Test rewarded ad\n({placement})" : $"Test ad\n({placement})";
            _closeLabel.text = kind == AdKind.Rewarded ? "Close and get reward" : "Close";
            _skip.gameObject.SetActive(kind == AdKind.Rewarded);
            _canvas.SetActive(true);
            InputBlockers.Register((RectTransform)_canvas.transform);
            Update();
        }

        /// <summary>Same as tapping the close button (watched = true) or Skip (false).</summary>
        public void Finish(bool watched)
        {
            if (!Visible)
            {
                return;
            }

            _canvas.SetActive(false);
            InputBlockers.Unregister((RectTransform)_canvas.transform);
            Action<AdOutcome> done = _done;
            _done = null;
            done?.Invoke(_kind == AdKind.Rewarded && watched ? AdOutcome.Rewarded : AdOutcome.Closed);
        }

        private void Update()
        {
            if (!Visible)
            {
                return;
            }

            float left = _closableAt - Time.unscaledTime;
            _close.interactable = left <= 0f;
            _countdown.text = left > 0f ? Mathf.CeilToInt(left).ToString() : string.Empty;
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
            _canvas = new GameObject("DevAdCard", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas.transform.SetParent(transform, false);
            var canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            var scaler = _canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = SpikeMath.DpToPx(1f, Screen.dpi);

            Image background = Add<Image>("Background", _canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            background.color = new Color(0.12f, 0.12f, 0.12f, 1f);

            _title = Label("Title", new Vector2(0.5f, 0.62f), new Vector2(320f, 120f), 24);
            _countdown = Label("Countdown", new Vector2(0.5f, 0.45f), new Vector2(120f, 80f), 40);
            _close = ButtonAt("Close", new Vector2(0.5f, 0.3f), () => Finish(true), out _closeLabel);
            _skip = ButtonAt("Skip", new Vector2(0.5f, 0.2f), () => Finish(false), out Text skipLabel);
            skipLabel.text = "Skip (no reward)";
            _canvas.SetActive(false);
        }

        private Text Label(string name, Vector2 anchor, Vector2 size, int fontSize)
        {
            Text text = Add<Text>(name, _canvas.transform, anchor, anchor, Vector2.zero, size);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private Button ButtonAt(string name, Vector2 anchor, Action onClick, out Text label)
        {
            Image image = Add<Image>(name, _canvas.transform, anchor, anchor, Vector2.zero, new Vector2(240f, 52f));
            image.color = new Color(0.25f, 0.45f, 0.85f, 1f);
            var button = image.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => onClick());
            label = Add<Text>("Label", image.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 18;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            return button;
        }

        private static T Add<T>(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
            where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(T));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return go.GetComponent<T>();
        }
    }
}

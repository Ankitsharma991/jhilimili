using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UtsavNights.Audio;
using UtsavNights.Core;
using UtsavNights.Core.Runtime;
using UtsavNights.Effects;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Hidden performance overlay for device captures (PRD 14 device lab). Triple-tap the top-left corner
    /// to show or hide fps, frame time, tier, governor steps, particles and voices. The corner never
    /// lights the lighter. Refreshes 4 times a second; spike-style string building is fine here.
    /// Measurement tools (Phase 3 step 13) add buttons below the stats with <see cref="AddTool"/>.
    /// </summary>
    public sealed class DevOverlay : MonoBehaviour
    {
        private const float CornerDp = 72f;
        private const float TripleTapWindow = 0.8f;
        private const float RefreshSeconds = 0.25f;

        private readonly RollingStats _frames = new RollingStats(120);
        private readonly StringBuilder _sb = new StringBuilder(256);
        private Text _text;
        private GameObject _panel;
        private RectTransform _corner;
        private int _taps;
        private float _firstTapTime;
        private float _nextRefresh;
        private RectTransform _tools;

        public bool Visible => _panel != null && _panel.activeSelf;

        private void Awake()
        {
            TouchInput.EnsureEnabled();
            BuildUi();
        }

        private void OnDestroy()
        {
            if (_corner != null)
            {
                InputBlockers.Unregister(_corner);
            }
        }

        public void Toggle()
        {
            _panel.SetActive(!_panel.activeSelf);
            _tools.gameObject.SetActive(_panel.activeSelf);
        }

        /// <summary>Adds a button under the stats, shown only while the overlay is visible.</summary>
        public void AddTool(string label, Action action)
        {
            if (_tools == null)
            {
                BuildUi();
            }

            float height = SpikeMath.DpToPx(48f, Screen.dpi);
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_tools, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(SpikeMath.DpToPx(160f, Screen.dpi), height);
            rect.anchoredPosition = new Vector2(0f, -(_tools.childCount - 1) * (height + SpikeMath.DpToPx(6f, Screen.dpi)));
            go.GetComponent<Image>().color = new Color(0.15f, 0.3f, 0.15f, 0.85f);
            go.GetComponent<Button>().onClick.AddListener(() =>
            {
                Toggle();
                action();
            });
            InputBlockers.Register(rect);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(rect, false);
            var text = textGo.GetComponent<Text>();
            text.font = _text.font;
            text.fontSize = _text.fontSize;
            text.color = _text.color;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = label;
            text.raycastTarget = false;
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        }

        private void Update()
        {
            _frames.Add(Time.unscaledDeltaTime * 1000f);
            DetectTripleTap();
            if (Visible && Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + RefreshSeconds;
                Refresh();
            }
        }

        private void DetectTripleTap()
        {
            var touches = ETouch.activeTouches;
            float corner = SpikeMath.DpToPx(CornerDp, Screen.dpi);
            for (int i = 0; i < touches.Count; i++)
            {
                ETouch t = touches[i];
                if (t.phase != TouchPhase.Began || t.screenPosition.x > corner || t.screenPosition.y < Screen.height - corner)
                {
                    continue;
                }

                if (_taps == 0 || Time.unscaledTime - _firstTapTime > TripleTapWindow)
                {
                    _taps = 0;
                    _firstTapTime = Time.unscaledTime;
                }

                _taps++;
                if (_taps >= 3)
                {
                    _taps = 0;
                    Toggle();
                }
            }
        }

        private void Refresh()
        {
            _sb.Clear();
            _sb.Append("FPS ").Append(SpikeMath.FpsFromFrameMs(_frames.Average).ToString("0.0"))
                .Append("   p95 ").Append(_frames.Percentile(95f).ToString("0.0")).Append(" ms\n");
            if (Game.Services.TryGet(out QualityManager quality))
            {
                _sb.Append("Tier ").Append(quality.Tier).Append("   steps ")
                    .Append(quality.Governor != null ? quality.Governor.StepsApplied : 0)
                    .Append(quality.BenchmarkPending ? "   benchmarking" : string.Empty).Append('\n');
            }

            if (Game.Services.TryGet(out EffectsService effects))
            {
                _sb.Append("Particles ").Append(effects.Budget.Reserved).Append(" / ").Append(effects.Budget.Cap)
                    .Append("   effects ").Append(effects.Particles.ActiveCount).Append('\n');
            }

            if (Game.Services.TryGet(out AudioService audio))
            {
                _sb.Append("Voices ").Append(audio.ActiveVoices).Append(" / ").Append(audio.VoiceCount);
            }

            _text.text = _sb.ToString();
        }

        private void BuildUi()
        {
            if (_panel != null)
            {
                return;
            }

            var canvasGo = new GameObject("DevOverlay", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            _corner = new GameObject("CornerBlocker", typeof(RectTransform)).GetComponent<RectTransform>();
            _corner.SetParent(canvasGo.transform, false);
            _corner.anchorMin = _corner.anchorMax = _corner.pivot = new Vector2(0f, 1f);
            float corner = SpikeMath.DpToPx(CornerDp, Screen.dpi);
            _corner.sizeDelta = new Vector2(corner, corner);
            InputBlockers.Register(_corner);

            _panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(canvasGo.transform, false);
            var panelRect = (RectTransform)_panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(16f, -corner);
            panelRect.sizeDelta = new Vector2(SpikeMath.DpToPx(260f, Screen.dpi), SpikeMath.DpToPx(84f, Screen.dpi));
            var image = _panel.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.6f);
            image.raycastTarget = false;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(_panel.transform, false);
            _text = textGo.GetComponent<Text>();
            _text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _text.fontSize = Mathf.RoundToInt(SpikeMath.DpToPx(12f, Screen.dpi));
            _text.color = new Color(0.8f, 1f, 0.8f);
            _text.raycastTarget = false;
            var textRect = _text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 6f);
            textRect.offsetMax = new Vector2(-12f, -6f);
            _panel.SetActive(false);

            _tools = new GameObject("Tools", typeof(RectTransform)).GetComponent<RectTransform>();
            _tools.SetParent(canvasGo.transform, false);
            _tools.anchorMin = _tools.anchorMax = _tools.pivot = new Vector2(0f, 1f);
            _tools.anchoredPosition = new Vector2(16f, -corner - panelRect.sizeDelta.y - SpikeMath.DpToPx(8f, Screen.dpi));
            _tools.gameObject.SetActive(false);
        }
    }
}

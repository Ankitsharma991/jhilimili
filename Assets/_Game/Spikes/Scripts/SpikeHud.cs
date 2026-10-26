using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace UtsavNights.Spikes
{
    /// <summary>
    /// Runtime-built debug panel for the Phase 1 spikes: live stats at the top, controls at the bottom,
    /// and a small flash square used for the audio latency video test.
    /// Spike-only UI: it rebuilds a status string four times a second, which allocates a little.
    /// The product UI (Phase 3) must follow the no-per-frame-allocation rule.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class SpikeHud : MonoBehaviour
    {
        private const float ReferenceWidth = 1080f;
        private const float ReferenceHeight = 1920f;
        private const float RowHeight = 112f;
        private const float StatusHeight = 400f;
        private const float RefreshSeconds = 0.25f;
        private const float FlashSeconds = 0.08f;

        private readonly StringBuilder _sb = new StringBuilder(768);
        private PerfMonitor _perf;
        private ParticleStressSpike _particles;
        private AudioLatencySpike _audio;
        private LighterDragSpike _lighter;
        private QualityTierSwitcher _tiers;
        private Font _font;
        private RectTransform _safeArea;
        private RectTransform _topPanel;
        private RectTransform _bottomPanel;
        private Text _status;
        private Image _flashSquare;
        private Text _particlesLabel;
        private Text _tierLabel;
        private Text _captureLabel;
        private Text _modeLabel;
        private Text _dspLabel;
        private Text _soakLabel;
        private float _flashLevel;
        private float _nextRefresh;
        private Rect _appliedSafeArea;

        private void Awake()
        {
            _perf = GetComponent<PerfMonitor>();
            _particles = GetComponent<ParticleStressSpike>();
            _audio = GetComponent<AudioLatencySpike>();
            _lighter = GetComponent<LighterDragSpike>();
            _tiers = GetComponent<QualityTierSwitcher>();
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildUi();
        }

        public bool IsOverHud(Vector2 screenPosition)
        {
            return (_topPanel != null && RectTransformUtility.RectangleContainsScreenPoint(_topPanel, screenPosition, null)) ||
                   (_bottomPanel != null && RectTransformUtility.RectangleContainsScreenPoint(_bottomPanel, screenPosition, null));
        }

        /// <summary>Shows the white square for one flash, in the same frame the click sound is started.</summary>
        public void Flash()
        {
            _flashLevel = 1f;
            ApplyFlash();
        }

        private void Update()
        {
            if (Screen.safeArea != _appliedSafeArea)
            {
                ApplySafeArea();
            }

            if (_flashLevel > 0f)
            {
                _flashLevel = Mathf.Max(0f, _flashLevel - Time.unscaledDeltaTime / FlashSeconds);
                ApplyFlash();
            }

            if (Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + RefreshSeconds;
                RefreshStatus();
            }
        }

        private void ApplyFlash()
        {
            if (_flashSquare != null)
            {
                _flashSquare.color = new Color(1f, 1f, 1f, _flashLevel > 0f ? 1f : 0f);
            }
        }

        private void RefreshStatus()
        {
            _sb.Clear();
            _sb.Append("Utsav Nights  Phase 1 spikes   Tier ").Append(_tiers != null ? _tiers.CurrentName : "?")
                .Append("   Mode ").Append(SpikeInput.Mode == SpikeMode.Lighter ? "Lighter" : "Audio tap").Append('\n');
            if (_perf != null)
            {
                var live = _perf.Live;
                _sb.Append("FPS ").Append((1000f / Mathf.Max(0.001f, live.Average)).ToString("0.0"))
                    .Append("   p99 ").Append(live.Percentile(99f).ToString("0.0")).Append(" ms")
                    .Append("   >2x budget ").Append(live.PercentAbove(PerfMonitor.SpikeThresholdMs).ToString("0.0")).Append("%\n");
                _sb.Append("CPU ").Append(_perf.CpuFrameMs.ToString("0.0")).Append(" ms   GPU ")
                    .Append(_perf.GpuFrameMs.ToString("0.0")).Append(" ms   RAM ")
                    .Append((_perf.SystemMemoryBytes / 1048576f).ToString("0")).Append(" MB (peak ")
                    .Append((_perf.PeakSystemMemoryBytes / 1048576f).ToString("0")).Append(")\n");
            }

            if (_particles != null)
            {
                _sb.Append("Particles ").Append(_particles.LiveParticles).Append(" / peak ").Append(_particles.PeakParticles)
                    .Append(" / cap ").Append(_particles.MaxParticlesBudget).Append("   effects ").Append(_particles.ActiveEffects).Append('\n');
            }

            if (_audio != null)
            {
                _sb.Append("Voices ").Append(_audio.ActiveVoices).Append('/').Append(AudioLatencySpike.VoiceCount)
                    .Append(" (peak ").Append(_audio.PeakVoices).Append(")   DSP ").Append(_audio.DspBufferLength).Append('x')
                    .Append(_audio.DspNumBuffers).Append(" @").Append(_audio.SampleRate).Append(" = ")
                    .Append(_audio.BufferLatencyMs.ToString("0.0")).Append(" ms\n");
                _sb.Append("Tap to play ").Append(_audio.TapLatencyMs.Average.ToString("0.0")).Append(" ms avg over ")
                    .Append(_audio.TapLatencyMs.Count).Append(" taps\n");
            }

            if (_lighter != null)
            {
                _sb.Append("Lighter ").Append(_lighter.StateName).Append("   hold ")
                    .Append((_lighter.HoldProgress * 100f).ToString("0")).Append("%   ignitions ").Append(_lighter.Ignitions).Append('\n');
            }

            if (_perf != null)
            {
                _sb.Append("Scene ready ").Append((_perf.SceneReadySeconds * 1000d).ToString("0")).Append(" ms after engine start");
                if (_perf.IsCapturing)
                {
                    _sb.Append("   CAPTURING ").Append(_perf.CaptureRemaining.ToString("0")).Append(" s");
                }

                if (_perf.IsSoaking)
                {
                    _sb.Append("   SOAK ").Append((_perf.SoakRemaining / 60f).ToString("0.0")).Append(" min");
                }
            }

            _status.text = _sb.ToString();
            _particlesLabel.text = _particles != null && _particles.IsRunning ? "Stop 5 effects" : "Start 5 effects";
            _tierLabel.text = "Tier: " + (_tiers != null ? _tiers.CurrentName : "?");
            _captureLabel.text = _perf != null && _perf.IsCapturing ? "Capturing..." : "Capture 30 s";
            _modeLabel.text = SpikeInput.Mode == SpikeMode.Lighter ? "Mode: Lighter" : "Mode: Audio tap";
            _dspLabel.text = _audio != null ? "DSP buffer " + _audio.DspBufferLength : "DSP";
            _soakLabel.text = _perf != null && _perf.IsSoaking ? "Soaking..." : "Soak 15 min";
        }

        private void BuildUi()
        {
            var canvasGo = new GameObject("SpikeHUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.matchWidthOrHeight = 0f;

            _safeArea = NewRect("SafeArea", canvasGo.transform);
            Stretch(_safeArea);
            ApplySafeArea();

            _topPanel = NewRect("Status", _safeArea);
            _topPanel.anchorMin = new Vector2(0f, 1f);
            _topPanel.anchorMax = new Vector2(1f, 1f);
            _topPanel.pivot = new Vector2(0.5f, 1f);
            _topPanel.sizeDelta = new Vector2(0f, StatusHeight);
            AddImage(_topPanel, new Color(0.03f, 0.02f, 0.08f, 0.72f));
            _status = AddText(_topPanel, string.Empty, 30, TextAnchor.UpperLeft);
            var statusRect = _status.rectTransform;
            statusRect.offsetMin = new Vector2(24f, 12f);
            statusRect.offsetMax = new Vector2(-24f, -12f);

            var flash = NewRect("LatencyFlash", _safeArea);
            flash.anchorMin = flash.anchorMax = new Vector2(1f, 1f);
            flash.pivot = new Vector2(1f, 1f);
            flash.sizeDelta = new Vector2(200f, 200f);
            flash.anchoredPosition = new Vector2(-24f, -StatusHeight - 24f);
            _flashSquare = AddImage(flash, new Color(1f, 1f, 1f, 0f));

            _bottomPanel = NewRect("Controls", _safeArea);
            _bottomPanel.anchorMin = new Vector2(0f, 0f);
            _bottomPanel.anchorMax = new Vector2(1f, 0f);
            _bottomPanel.pivot = new Vector2(0.5f, 0f);
            _bottomPanel.sizeDelta = new Vector2(0f, RowHeight * 3f + 16f * 4f);
            AddImage(_bottomPanel, new Color(0.03f, 0.02f, 0.08f, 0.72f));
            var column = _bottomPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(16, 16, 16, 16);
            column.spacing = 16f;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = true;

            Transform row1 = AddRow(_bottomPanel);
            _particlesLabel = AddButton(row1, "Start 5 effects", () => _particles?.Toggle());
            _tierLabel = AddButton(row1, "Tier", () => _tiers?.Cycle());
            _captureLabel = AddButton(row1, "Capture 30 s", () => _perf?.StartCapture(30f));

            Transform row2 = AddRow(_bottomPanel);
            _modeLabel = AddButton(row2, "Mode", () => SpikeInput.CycleMode());
            AddButton(row2, "16 voices", () => _audio?.RunOverlapTest());
            _dspLabel = AddButton(row2, "DSP", () => _audio?.CycleDspBuffer());

            Transform row3 = AddRow(_bottomPanel);
            _soakLabel = AddButton(row3, "Soak 15 min", () => _perf?.StartSoak(15f * 60f));
            AddButton(row3, "Reset peaks", () => _perf?.ResetPeaks());
            AddButton(row3, "Log report path", () => Debug.Log($"{SpikeReport.LogTag} report file: {SpikeReport.FilePath}"));
        }

        private void ApplySafeArea()
        {
            Rect safe = Screen.safeArea;
            _appliedSafeArea = safe;
            if (_safeArea == null || Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            _safeArea.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            _safeArea.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            _safeArea.offsetMin = Vector2.zero;
            _safeArea.offsetMax = Vector2.zero;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Image AddImage(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private Text AddText(RectTransform parent, string value, int size, TextAnchor anchor)
        {
            RectTransform rect = NewRect("Text", parent);
            Stretch(rect);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = new Color(1f, 0.96f, 0.88f);
            text.text = value;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Transform AddRow(RectTransform parent)
        {
            RectTransform row = NewRect("Row", parent);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            return row;
        }

        private Text AddButton(Transform row, string label, UnityAction onClick)
        {
            RectTransform rect = NewRect(label, row);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.22f, 0.12f, 0.42f, 0.95f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = RowHeight;
            layout.flexibleWidth = 1f;
            Text text = AddText(rect, label, 32, TextAnchor.MiddleCenter);
            text.color = new Color(1f, 0.82f, 0.45f);
            return text;
        }
    }
}

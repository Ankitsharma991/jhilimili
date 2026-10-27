using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UtsavNights.Core.Finale;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;

namespace UtsavNights.UI
{
    /// <summary>
    /// Finale button (PRD 12 main scene; F13). Sits above the tray on the right. Dimmed with a hint until 3
    /// crackers are placed; shows progress while the finale plays; offers Replay afterwards.
    /// </summary>
    public sealed class FinaleButton : MonoBehaviour
    {
        private const float ReplaySeconds = 10f;
        private const float RefreshSeconds = 0.2f;

        private FinaleController _finale;
        private TrayView _tray;
        private RectTransform _rect;
        private Image _background;
        private TextMeshProUGUI _label;
        private float _replayUntil = -1f;
        private float _nextRefresh;

        public string Label => _label != null ? _label.text : string.Empty;

        public RectTransform Rect => _rect;

        private void Start()
        {
            Game.Services.TryGet(out _finale);
            _tray = FindFirstObjectByType<TrayView>();
            Build();
            if (_finale != null)
            {
                _finale.StateChanged += OnStateChanged;
            }

            InputBlockers.Register(_rect);
            Refresh();
        }

        private void OnDestroy()
        {
            if (_finale != null)
            {
                _finale.StateChanged -= OnStateChanged;
            }

            InputBlockers.Unregister(_rect);
        }

        /// <summary>Same as tapping the button.</summary>
        public void Press()
        {
            if (_finale == null)
            {
                return;
            }

            if (_finale.State == FinaleController.FinaleState.Running)
            {
                return;
            }

            if (_replayUntil > Time.unscaledTime && _finale.CanReplay && _finale.ReadyCount < FinaleSchedule.MinItems)
            {
                _finale.Replay();
            }
            else if (!_finale.StartFinale())
            {
                int need = FinaleSchedule.MinItems - _finale.ReadyCount;
                _tray?.ShowHint(need > 0 ? $"Place {need} more cracker{(need == 1 ? string.Empty : "s")} for a finale" : "Finale needs crackers that are not lit yet", 2.5f);
            }

            Refresh();
        }

        private void OnStateChanged(FinaleController.FinaleState state)
        {
            if (state == FinaleController.FinaleState.Idle)
            {
                _replayUntil = Time.unscaledTime + ReplaySeconds;
            }

            Refresh();
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + RefreshSeconds;
                Refresh();
            }
        }

        private void Refresh()
        {
            if (_finale == null || _label == null)
            {
                return;
            }

            if (_finale.State == FinaleController.FinaleState.Running)
            {
                _label.text = $"Finale {_finale.Lit}/{_finale.Total}";
                _background.color = UiTheme.Magenta;
                _label.color = UiTheme.Indigo;
                return;
            }

            bool ready = _finale.CanStart;
            bool replay = !ready && _finale.CanReplay && _replayUntil > Time.unscaledTime;
            _label.text = replay ? "Replay" : "Finale";
            _background.color = ready || replay ? UiTheme.Saffron : UiTheme.IndigoLight;
            _label.color = ready || replay ? UiTheme.Indigo : UiTheme.Muted;
        }

        private void Build()
        {
            _rect = UiKit.Rect("FinaleButton", transform);
            _rect.anchorMin = _rect.anchorMax = _rect.pivot = new Vector2(1f, 0f);
            _rect.sizeDelta = new Vector2(280f, UiTheme.MinTouch);
            _rect.anchoredPosition = new Vector2(-32f, TrayView.TrayHeight + 150f); // above the placement hint
            _background = UiKit.Panel(_rect, UiTheme.IndigoLight);
            var button = _rect.gameObject.AddComponent<Button>();
            button.targetGraphic = _background;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(Press);
            _label = UiKit.Text(_rect, "Finale", UiTheme.BodyText, TextAlignmentOptions.Center, UiTheme.Muted);
        }
    }
}

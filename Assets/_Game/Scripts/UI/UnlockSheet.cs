using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UtsavNights.Content;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;

namespace UtsavNights.UI
{
    /// <summary>
    /// Opens when a premium tray item is tapped (PRD 15.1, F10). The player chooses: watch a rewarded ad
    /// to unlock that cracker for 24 hours, or close. After the reward the cracker is picked up, ready to
    /// place. Nothing here ever shows an ad without the tap.
    /// </summary>
    public sealed class UnlockSheet : MonoBehaviour
    {
        public const string Placement = "unlock_premium";
        private const float CloseAfterRewardSeconds = 1.2f;

        private TrayView _tray;
        private AdService _ads;
        private ProgressService _progress;
        private RectTransform _overlay;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _status;
        private Button _watch;
        private int _index = -1;
        private float _closeAt = -1f;

        public bool IsOpen => _overlay != null && _overlay.gameObject.activeSelf;

        public string Status => _status != null ? _status.text : string.Empty;

        public RectTransform WatchButton => _watch != null ? (RectTransform)_watch.transform : null;

        private void Start()
        {
            _tray = FindFirstObjectByType<TrayView>();
            Game.Services.TryGet(out _ads);
            Game.Services.TryGet(out _progress);
            Build(_tray != null ? _tray.transform.parent : transform);
            if (_tray != null)
            {
                _tray.LockedItemTapped += Open;
            }
        }

        private void OnDestroy()
        {
            if (_tray != null)
            {
                _tray.LockedItemTapped -= Open;
            }

            if (_overlay != null)
            {
                InputBlockers.Unregister(_overlay);
            }
        }

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            if (_closeAt >= 0f && Time.unscaledTime >= _closeAt)
            {
                int index = _index;
                Close();
                _tray?.Select(index);
                return;
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
        }

        /// <summary>Shows the sheet for tray item <paramref name="index"/>.</summary>
        public void Open(int index)
        {
            if (_tray == null || index < 0 || index >= _tray.ItemCount)
            {
                return;
            }

            _index = index;
            _closeAt = -1f;
            _title.text = _tray.ItemLabel(index);
            _status.text = string.Empty;
            _watch.interactable = _ads != null;
            _overlay.gameObject.SetActive(true);
            _overlay.SetAsLastSibling();
            InputBlockers.Register(_overlay);
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            _closeAt = -1f;
            InputBlockers.Unregister(_overlay);
            _overlay.gameObject.SetActive(false);
        }

        /// <summary>Same as tapping "Watch a short ad".</summary>
        public void WatchAd()
        {
            if (!IsOpen || _ads == null || _ads.IsShowing || _closeAt >= 0f)
            {
                return;
            }

            CrackerDefinition definition = _tray.Catalog.Entries[_index].definition;
            _status.text = string.Empty;
            _ads.ShowRewarded(Placement, (outcome, message) =>
            {
                if (outcome == AdOutcome.Rewarded)
                {
                    _progress?.GrantRewardedUnlock(definition.Id);
                    _status.text = "Unlocked for 24 hours!";
                    _closeAt = Time.unscaledTime + CloseAfterRewardSeconds;
                }
                else if (outcome == AdOutcome.Closed)
                {
                    _status.text = "Watch to the end to unlock.";
                }
                else
                {
                    _status.text = message;
                }
            });
        }

        private void Build(Transform parent)
        {
            _overlay = UiKit.Rect("UnlockOverlay", parent);
            UiKit.Stretch(_overlay);
            var dim = _overlay.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.55f);
            var dimButton = _overlay.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(Close);

            RectTransform card = UiKit.Rect("Card", _overlay);
            card.anchorMin = new Vector2(0f, 0.5f);
            card.anchorMax = new Vector2(1f, 0.5f);
            card.sizeDelta = new Vector2(-64f, 640f);
            UiKit.Panel(card, new Color(0.09f, 0.06f, 0.2f, 0.98f));
            MaxWidth.Apply(card, 900f);

            _title = Label(card, "Title", -40f, 90f, UiTheme.TitleText, UiTheme.Marigold);
            Label(card, "Body", -140f, 150f, UiTheme.BodyText, UiTheme.OffWhite).text =
                "A premium cracker. Watch a short ad to play with it for the next 24 hours.";
            _status = Label(card, "Status", -300f, 70f, UiTheme.SmallText, UiTheme.Saffron);

            _watch = CardButton(card, "Watch a short ad", -390f, UiTheme.Saffron, UiTheme.Indigo, WatchAd);
            CardButton(card, "Not now", -520f, UiTheme.IndigoLight, UiTheme.OffWhite, Close);
            _overlay.gameObject.SetActive(false);
        }

        private static TextMeshProUGUI Label(RectTransform card, string name, float y, float height, float size, Color color)
        {
            RectTransform rect = UiKit.Rect(name, card);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(-80f, height);
            rect.anchoredPosition = new Vector2(0f, y);
            TextMeshProUGUI text = UiKit.Text(rect, string.Empty, size, TextAlignmentOptions.Center, color);
            text.enableAutoSizing = true;
            text.fontSizeMin = 24f;
            text.fontSizeMax = size * UiKit.TextScale;
            return text;
        }

        private static Button CardButton(RectTransform card, string label, float y, Color background, Color text, System.Action onClick)
        {
            RectTransform rect = UiKit.Rect(label, card);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(560f, UiTheme.MinTouch);
            rect.anchoredPosition = new Vector2(0f, y);
            Image image = UiKit.Panel(rect, background);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick());
            UiKit.Text(rect, label, UiTheme.BodyText, TextAlignmentOptions.Center, text);
            return button;
        }
    }
}

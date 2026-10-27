using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UtsavNights.Content;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;

namespace UtsavNights.UI
{
    /// <summary>
    /// The cracker tray (F04, PRD 12): a bottom strip of catalog items (icon, name, cost dots, lock), at most
    /// 20 % of the screen. Tap an item to place it, tap it again to cancel. The placement reason shows above
    /// the tray, and the tray pulses after a payoff to invite the next cracker (PRD 6.3).
    /// </summary>
    public sealed class TrayView : MonoBehaviour
    {
        public const float TrayHeight = 300f;
        private const float ItemWidth = 200f;
        private const float PulseSeconds = 0.45f;

        [SerializeField] private CrackerCatalog catalog;

        private readonly List<ItemView> _items = new List<ItemView>();
        private RectTransform _panel;
        private RectTransform _content;
        private TextMeshProUGUI _hint;
        private RectTransform _hintPanel;
        private RectTransform _viewport;
        private ScrollRect _scroll;
        private float _lastViewportWidth = -1f;
        private PlacementController _placement;
        private float _pulse = -1f;

        public RectTransform Panel => _panel;

        public int ItemCount => _items.Count;

        public int SelectedIndex { get; private set; } = -1;

        public string HintText => _hint != null ? _hint.text : string.Empty;

        public RectTransform ItemRect(int index) => _items[index].Rect;

        public string ItemLabel(int index)
        {
            CrackerDefinition d = _items[index].Entry.definition;
            return string.IsNullOrEmpty(d.DisplayName.en) ? d.Id : d.DisplayName.en;
        }

        public bool ItemLocked(int index) => _items[index].Entry.locked;

        private sealed class ItemView
        {
            public CrackerCatalog.Entry Entry;
            public Image Background;
            public RectTransform Rect;
            public TextMeshProUGUI Name;
        }

        public void Configure(CrackerCatalog crackerCatalog) => catalog = crackerCatalog;

        private void Awake()
        {
            Build();
        }

        private void Start()
        {
            Game.Services.TryGet(out _placement);
            if (_placement != null)
            {
                _placement.SelectionChanged += OnSelectionChanged;
                _placement.ReasonChanged += OnReasonChanged;
            }

            Game.Events.Subscribe<PayoffCompleteEvent>(OnPayoff);
            InputBlockers.Register(_panel);
        }

        private void OnDestroy()
        {
            if (_placement != null)
            {
                _placement.SelectionChanged -= OnSelectionChanged;
                _placement.ReasonChanged -= OnReasonChanged;
            }

            Game.Events.Unsubscribe<PayoffCompleteEvent>(OnPayoff);
            InputBlockers.Unregister(_panel);
        }

        /// <summary>Same as tapping item <paramref name="index"/>.</summary>
        public void Select(int index)
        {
            if (_placement == null || index < 0 || index >= _items.Count)
            {
                return;
            }

            CrackerCatalog.Entry entry = _items[index].Entry;
            if (entry.locked)
            {
                SetHint(string.IsNullOrEmpty(entry.unlockHint) ? "Locked" : entry.unlockHint);
                return;
            }

            if (SelectedIndex == index)
            {
                _placement.Cancel();
                return;
            }

            _placement.BeginPlacing(entry.definition);
        }

        private void Update()
        {
            if (_hintUntil > 0f && Time.unscaledTime >= _hintUntil)
            {
                _hintUntil = -1f;
                SetHint(_placement != null && _placement.IsPlacing ? "Tap the ground to place" : string.Empty);
            }

            if (_viewport != null && !Mathf.Approximately(_viewport.rect.width, _lastViewportWidth))
            {
                RefreshCentering();
            }

            if (_pulse < 0f)
            {
                return;
            }

            _pulse += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_pulse / PulseSeconds);
            _content.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(t * Mathf.PI));
            if (t >= 1f)
            {
                _pulse = -1f;
                _content.localScale = Vector3.one;
            }
        }

        private void OnSelectionChanged(CrackerDefinition selected)
        {
            SelectedIndex = -1;
            for (int i = 0; i < _items.Count; i++)
            {
                bool on = selected != null && _items[i].Entry.definition == selected;
                if (on)
                {
                    SelectedIndex = i;
                }

                _items[i].Background.color = on ? UiTheme.Saffron : UiTheme.IndigoLight;

                // Off-white on saffron is 2:1; indigo keeps the selected label at WCAG AA.
                if (_items[i].Name != null)
                {
                    _items[i].Name.color = on ? UiTheme.Indigo : UiTheme.OffWhite;
                }
                _items[i].Rect.localScale = on ? Vector3.one * 1.06f : Vector3.one;
            }

            SetHint(selected != null ? "Tap the ground to place" : string.Empty);
        }

        private void OnReasonChanged(string reason)
        {
            if (_placement != null && _placement.IsPlacing)
            {
                SetHint(string.IsNullOrEmpty(reason) ? "Tap the ground to place" : reason);
            }
        }

        private void OnPayoff(PayoffCompleteEvent e) => _pulse = 0f;

        private float _hintUntil = -1f;

        /// <summary>Shows a short message above the tray for a few seconds (finale, locked items).</summary>
        public void ShowHint(string text, float seconds)
        {
            SetHint(text);
            _hintUntil = Time.unscaledTime + seconds;
        }

        private void SetHint(string text)
        {
            _hint.text = text;
            _hintPanel.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        /// <summary>Centres the items when they all fit; otherwise they start at the left and scroll.</summary>
        private void RefreshCentering()
        {
            if (_content == null || _viewport == null)
            {
                return;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            float contentWidth = _content.rect.width;
            float viewportWidth = _viewport.rect.width;
            bool fits = contentWidth <= viewportWidth;
            _scroll.horizontal = !fits;
            _content.anchoredPosition = new Vector2(fits ? (viewportWidth - contentWidth) * 0.5f : 0f, 0f);
            _lastViewportWidth = viewportWidth;
        }

        private void Build()
        {
            _panel = UiKit.Rect("Tray", transform);
            _panel.anchorMin = new Vector2(0f, 0f);
            _panel.anchorMax = new Vector2(1f, 0f);
            _panel.pivot = new Vector2(0.5f, 0f);
            _panel.sizeDelta = new Vector2(-32f, TrayHeight);
            _panel.anchoredPosition = new Vector2(0f, 16f);
            UiKit.Panel(_panel, UiTheme.Indigo);
            MaxWidth.Apply(_panel, 1500f);

            var scroll = _panel.gameObject.AddComponent<ScrollRect>();
            _scroll = scroll;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            RectTransform viewport = UiKit.Rect("Viewport", _panel);
            _viewport = viewport;
            UiKit.Stretch(viewport, 12f);
            viewport.gameObject.AddComponent<RectMask2D>();
            _content = UiKit.Rect("Content", viewport);
            _content.anchorMin = new Vector2(0f, 0f);
            _content.anchorMax = new Vector2(0f, 1f);
            _content.pivot = new Vector2(0f, 0.5f);
            _content.sizeDelta = Vector2.zero; // exactly the viewport's height
            var layout = _content.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = true;
            layout.childAlignment = TextAnchor.MiddleLeft;
            _content.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = _content;

            RectTransform hintRect = UiKit.Rect("PlacementHint", transform);
            hintRect.anchorMin = new Vector2(0.1f, 0f);
            hintRect.anchorMax = new Vector2(0.9f, 0f);
            hintRect.pivot = new Vector2(0.5f, 0f);
            hintRect.sizeDelta = new Vector2(0f, 88f);
            hintRect.anchoredPosition = new Vector2(0f, TrayHeight + 36f);
            UiKit.Panel(hintRect, UiTheme.Indigo, raycast: false);
            MaxWidth.Apply(hintRect, 900f);
            _hintPanel = hintRect;
            _hint = UiKit.Text(hintRect, string.Empty, UiTheme.BodyText, TextAlignmentOptions.Center, UiTheme.OffWhite);
            hintRect.gameObject.SetActive(false);

            if (catalog == null)
            {
                return;
            }

            for (int i = 0; i < catalog.Count; i++)
            {
                CrackerCatalog.Entry entry = catalog.Entries[i];
                if (entry?.definition != null)
                {
                    _items.Add(BuildItem(entry, _items.Count));
                }
            }
        }

        private ItemView BuildItem(CrackerCatalog.Entry entry, int index)
        {
            RectTransform rect = UiKit.Rect("Item_" + entry.definition.Id, _content);
            rect.sizeDelta = new Vector2(ItemWidth, 0f);
            Image background = UiKit.Panel(rect, UiTheme.IndigoLight);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => Select(index));

            RectTransform iconRect = UiKit.Rect("Icon", rect);
            iconRect.anchorMin = new Vector2(0.5f, 1f);
            iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.sizeDelta = new Vector2(150f, 150f);
            iconRect.anchoredPosition = new Vector2(0f, -10f);
            var icon = iconRect.gameObject.AddComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            if (entry.definition.TrayIcon != null)
            {
                icon.sprite = entry.definition.TrayIcon;
            }
            else
            {
                icon.sprite = UiKit.Circle;
                icon.color = UiTheme.Marigold;
            }

            RectTransform nameRect = UiKit.Rect("Name", rect);
            nameRect.anchorMin = new Vector2(0f, 0f);
            nameRect.anchorMax = new Vector2(1f, 0f);
            nameRect.pivot = new Vector2(0.5f, 0f);
            nameRect.sizeDelta = new Vector2(-12f, 60f);
            nameRect.anchoredPosition = new Vector2(0f, 34f);
            string label = string.IsNullOrEmpty(entry.definition.DisplayName.en) ? entry.definition.Id : entry.definition.DisplayName.en;
            TextMeshProUGUI name = UiKit.Text(nameRect, label, UiTheme.SmallText, TextAlignmentOptions.Center, UiTheme.OffWhite);
            name.enableAutoSizing = true;
            name.fontSizeMin = 24f;
            name.fontSizeMax = name.fontSize;
            name.textWrappingMode = TextWrappingModes.NoWrap;

            // Cost dots (PRD 12): one marigold dot per cost point, max 3.
            int cost = Mathf.Clamp(entry.definition.Cost, 0, 3);
            for (int d = 0; d < cost; d++)
            {
                RectTransform dot = UiKit.Rect("Cost" + d, rect);
                dot.anchorMin = dot.anchorMax = new Vector2(0.5f, 0f);
                dot.sizeDelta = new Vector2(16f, 16f);
                dot.anchoredPosition = new Vector2((d - (cost - 1) * 0.5f) * 24f, 18f);
                var dotImage = dot.gameObject.AddComponent<Image>();
                dotImage.sprite = UiKit.Circle;
                dotImage.color = UiTheme.Marigold;
                dotImage.raycastTarget = false;
            }

            if (entry.locked)
            {
                icon.color = new Color(1f, 1f, 1f, 0.35f);
                RectTransform lockRect = UiKit.Rect("Lock", rect);
                UiKit.Stretch(lockRect);
                UiKit.Text(lockRect, "Locked", UiTheme.SmallText, TextAlignmentOptions.Center, UiTheme.Muted);
            }

            return new ItemView { Entry = entry, Background = background, Rect = rect, Name = name };
        }
    }
}

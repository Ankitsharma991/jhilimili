using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UtsavNights.Core.Runtime;
using UtsavNights.Core.Settings;
using UtsavNights.Gameplay;
using UtsavNights.Haptics;

namespace UtsavNights.UI
{
    /// <summary>
    /// Settings sheet (F07, PRD 12): sound sliders, Quiet mode, reduce flashes, reduce motion, vibration and
    /// graphics tier, applied live through <see cref="SettingsService"/>. Opens from the corner button,
    /// closes with Done, a tap outside or Android back. Shows the one-time Quiet-mode tip (PRD 11).
    /// </summary>
    public sealed class SettingsSheet : MonoBehaviour
    {
        private const float RowHeight = 136f;
        private const float TipDelay = 2.5f;
        private const float TipSeconds = 5f;
        private static readonly string[] TierLabels = { "Auto", "Low", "Mid", "High" };

        private SettingsService _settings;
        private RectTransform _overlay;
        private RectTransform _openButton;
        private RectTransform _tip;
        private Slider _master;
        private Slider _effects;
        private SwitchView _quiet;
        private SwitchView _reduceFlash;
        private SwitchView _reduceMotion;
        private SwitchView _vibration;
        private Slider _vibrationStrength;
        private TextMeshProUGUI _vibrationNote;
        private SegmentedView _tier;
        private SwitchView _usageStats;
        private TextMeshProUGUI _privacyNote;
        private ConsentService _consent;
        private PurchaseService _purchases;
        private TextMeshProUGUI _removeAdsNote;
        private TextMeshProUGUI _restoreNote;
        private GameObject _buyButton;
        private GameObject _privacyPolicyRow;
        private AppLinks _links;
        private bool _binding;
        private float _tipClock = -1f;

        public bool IsOpen => _overlay != null && _overlay.gameObject.activeSelf;

        public bool TipVisible => _tip != null && _tip.gameObject.activeSelf;

        public RectTransform OpenButton => _openButton;

        public RectTransform DoneButton { get; private set; }

        /// <summary>Spoken items for the screen reader while the sheet is open, in reading order.</summary>
        public IReadOnlyList<AccessibleControl> Controls => _controls;

        private readonly List<AccessibleControl> _controls = new List<AccessibleControl>();

        /// <summary>One settings control as the screen reader sees it.</summary>
        public sealed class AccessibleControl
        {
            public string Label;
            public RectTransform Rect;
            public SwitchView Switch;
            public Slider Slider;
            public SegmentedView Segmented;
            public int Segment = -1;
            public Button Button;
        }

        public void SetUsageStats(bool on) => _usageStats.Set(on, true);

        /// <summary>Same as tapping "Privacy options".</summary>
        public void OpenPrivacyOptions()
        {
            if (_consent == null)
            {
                return;
            }

            _consent.ShowPrivacyOptions(() =>
            {
                if (_privacyNote != null)
                {
                    _privacyNote.text = _consent.Description;
                }
            });
        }

        public string PrivacyNote => _privacyNote != null ? _privacyNote.text : string.Empty;

        public string RemoveAdsNote => _removeAdsNote != null ? _removeAdsNote.text : string.Empty;

        public string RestoreNote => _restoreNote != null ? _restoreNote.text : string.Empty;

        public bool BuyVisible => _buyButton != null && _buyButton.activeSelf;

        public bool PrivacyPolicyVisible => _privacyPolicyRow != null && _privacyPolicyRow.activeSelf;

        /// <summary>Same as tapping "Read" on the privacy policy row.</summary>
        public void OpenPrivacyPolicy()
        {
            if (_links != null && _links.HasPrivacyPolicy)
            {
                ExternalLinks.Open(_links.privacyPolicyUrl);
            }
        }

        /// <summary>Same as tapping "Buy" (F11).</summary>
        public void BuyRemoveAds()
        {
            _purchases?.BuyRemoveAds((result, message) =>
            {
                RefreshPurchases();
                if (!string.IsNullOrEmpty(message))
                {
                    _removeAdsNote.text = message;
                }
            });
        }

        /// <summary>Same as tapping "Restore".</summary>
        public void RestorePurchases()
        {
            if (_purchases == null)
            {
                return;
            }

            _restoreNote.text = "Checking...";
            _purchases.Restore(message =>
            {
                RefreshPurchases();
                _restoreNote.text = message;
            });
        }

        private void RefreshPurchases()
        {
            if (_removeAdsNote == null)
            {
                return;
            }

            bool owned = _purchases != null && _purchases.Owned;
            _buyButton.SetActive(_purchases != null && !owned);
            _removeAdsNote.text = _purchases == null ? "Purchases are not set up in this build"
                : owned ? "Purchased. Thank you for supporting Utsav Nights!"
                : string.IsNullOrEmpty(_purchases.Price) ? "No ads, every cracker unlocked, forever" : $"{_purchases.Price}, one time. No ads, every cracker unlocked";
        }

        private void Start()
        {
            Game.Services.TryGet(out _settings);
            Build();
            InputBlockers.Register(_openButton);
            if (_settings != null && !_settings.Current.quietTipShown)
            {
                _tipClock = 0f;
            }
        }

        private void OnDestroy()
        {
            InputBlockers.Unregister(_openButton);
            InputBlockers.Unregister(_overlay);
        }

        public void Open()
        {
            if (_settings == null || IsOpen)
            {
                return;
            }

            if (Game.Services.TryGet(out PlacementController placement))
            {
                placement.Cancel();
            }

            Bind(_settings.Current);
            _overlay.gameObject.SetActive(true);
            InputBlockers.Register(_overlay);
            HideTip();
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            _settings?.SaveIfDirty();
            InputBlockers.Unregister(_overlay);
            _overlay.gameObject.SetActive(false);
        }

        // Test and accessibility hooks: the same paths a tap or drag takes.
        public void SetQuietMode(bool on) => _quiet.Set(on, true);

        public void SetTier(int segment) => _tier.Set(segment, true);

        public void SetMasterVolume(float value) => _master.value = value;

        private void Update()
        {
            if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
            }

            if (_tipClock < 0f)
            {
                return;
            }

            _tipClock += Time.unscaledDeltaTime;
            if (!TipVisible && _tipClock >= TipDelay && _tipClock < TipDelay + TipSeconds && !IsOpen)
            {
                _tip.gameObject.SetActive(true);
            }

            if (_tipClock >= TipDelay + TipSeconds)
            {
                HideTip();
            }
        }

        private void HideTip()
        {
            if (_tipClock < 0f)
            {
                return;
            }

            _tipClock = -1f;
            _tip.gameObject.SetActive(false);
            _settings?.Change(s => s.quietTipShown = true);
        }

        private void Bind(GameSettings s)
        {
            _binding = true;
            _master.value = s.masterVolume;
            _effects.value = s.effectsVolume;
            _quiet.Set(s.quietMode, false);
            _reduceFlash.Set(s.reduceFlash, false);
            _reduceMotion.Set(s.reduceShake, false);
            bool hasVibrator = Game.Services.TryGet(out HapticsService haptics) && haptics.Backend.HasVibrator;
            _vibration.Set(s.hapticsEnabled, false);
            _vibration.Interactable = hasVibrator;
            _vibrationStrength.value = s.hapticsIntensity;
            _vibrationStrength.interactable = hasVibrator && s.hapticsEnabled;
            _vibrationNote.text = hasVibrator ? "Taps and rumbles with each cracker" : "This device has no vibration motor";
            _tier.Set(s.qualityTier + 1, false);
            _usageStats.Set(s.shareUsageStats, false);
            Game.Services.TryGet(out _consent);
            _privacyNote.text = _consent != null ? _consent.Description : "Ads are not set up in this build";
            Game.Services.TryGet(out _purchases);
            _restoreNote.text = "Bought on another phone? Get it back here";
            RefreshPurchases();
            _binding = false;
        }

        private void Edit(System.Action<GameSettings> edit, bool saveNow)
        {
            if (_binding || _settings == null)
            {
                return;
            }

            _settings.Change(edit, saveNow);
        }

        private void Build()
        {
            // Corner button (top right), three bars.
            _openButton = UiKit.Rect("SettingsButton", transform);
            _openButton.anchorMin = _openButton.anchorMax = _openButton.pivot = new Vector2(1f, 1f);
            _openButton.sizeDelta = new Vector2(UiTheme.MinTouch, UiTheme.MinTouch);
            _openButton.anchoredPosition = new Vector2(-24f, -24f);
            Image buttonBg = UiKit.Panel(_openButton, UiTheme.Indigo);
            buttonBg.sprite = UiKit.Circle;
            var open = _openButton.gameObject.AddComponent<Button>();
            open.targetGraphic = buttonBg;
            open.onClick.AddListener(Open);
            for (int i = 0; i < 3; i++)
            {
                RectTransform bar = UiKit.Rect("Bar", _openButton);
                bar.sizeDelta = new Vector2(56f, 9f);
                bar.anchoredPosition = new Vector2(0f, (1 - i) * 18f);
                UiKit.Panel(bar, UiTheme.OffWhite, raycast: false).sprite = UiKit.Circle;
            }

            // First-launch tip.
            _tip = UiKit.Rect("QuietTip", transform);
            _tip.anchorMin = new Vector2(0.06f, 1f);
            _tip.anchorMax = new Vector2(0.82f, 1f);
            _tip.pivot = new Vector2(0f, 1f);
            _tip.sizeDelta = new Vector2(0f, 150f);
            _tip.anchoredPosition = new Vector2(0f, -24f);
            UiKit.Panel(_tip, UiTheme.Indigo, raycast: false);
            MaxWidth.Apply(_tip, 900f);
            TextMeshProUGUI tipText = UiKit.Text(_tip, "Too loud? Turn on <b>Quiet mode</b> in settings for softer bangs and no flashes.",
                UiTheme.SmallText, TextAlignmentOptions.Left, UiTheme.OffWhite);
            tipText.rectTransform.offsetMin = new Vector2(28f, 12f);
            tipText.rectTransform.offsetMax = new Vector2(-28f, -12f);
            _tip.gameObject.SetActive(false);

            // Overlay: dim background (tap to close) and the sheet.
            _overlay = UiKit.Rect("SettingsOverlay", transform.parent != null ? transform.parent : transform);
            UiKit.Stretch(_overlay);
            var dim = _overlay.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.5f);
            var dimButton = _overlay.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(Close);

            RectTransform sheet = UiKit.Rect("Sheet", _overlay);
            sheet.anchorMin = new Vector2(0f, 0f);
            sheet.anchorMax = new Vector2(1f, 0.82f);
            sheet.offsetMin = new Vector2(16f, 16f);
            sheet.offsetMax = new Vector2(-16f, 0f);
            UiKit.Panel(sheet, new Color(0.09f, 0.06f, 0.2f, 0.98f));
            MaxWidth.Apply(sheet, 1100f);

            RectTransform title = UiKit.Rect("Title", sheet);
            title.anchorMin = new Vector2(0f, 1f);
            title.anchorMax = new Vector2(1f, 1f);
            title.pivot = new Vector2(0.5f, 1f);
            title.sizeDelta = new Vector2(-64f, 120f);
            title.anchoredPosition = new Vector2(0f, -16f);
            UiKit.Text(title, "Settings", UiTheme.TitleText, TextAlignmentOptions.Left, UiTheme.Marigold);

            RectTransform done = UiKit.Rect("Done", sheet);
            done.anchorMin = done.anchorMax = done.pivot = new Vector2(1f, 1f);
            done.sizeDelta = new Vector2(220f, UiTheme.MinTouch);
            done.anchoredPosition = new Vector2(-28f, -12f);
            Image doneBg = UiKit.Panel(done, UiTheme.Saffron);
            var doneButton = done.gameObject.AddComponent<Button>();
            doneButton.targetGraphic = doneBg;
            doneButton.onClick.AddListener(Close);
            UiKit.Text(done, "Done", UiTheme.BodyText, TextAlignmentOptions.Center, UiTheme.Indigo);
            DoneButton = done;

            RectTransform viewport = UiKit.Rect("Viewport", sheet);
            UiKit.Stretch(viewport);
            viewport.offsetMin = new Vector2(32f, 24f);
            viewport.offsetMax = new Vector2(-32f, -150f);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform rows = UiKit.Rect("Rows", viewport);
            rows.anchorMin = new Vector2(0f, 1f);
            rows.anchorMax = new Vector2(1f, 1f);
            rows.pivot = new Vector2(0.5f, 1f);
            rows.sizeDelta = Vector2.zero; // exactly the viewport's width (a default 100 would overflow and clip)
            var layout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            rows.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = sheet.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.viewport = viewport;
            scroll.content = rows;

            Section(rows, "Sound");
            _master = Row(rows, "Volume", null, SliderFactory.Create(rows).gameObject).GetComponentInChildren<Slider>();
            _master.onValueChanged.AddListener(v => Edit(s => s.masterVolume = v, false));
            _effects = Row(rows, "Fireworks volume", null, SliderFactory.Create(rows).gameObject).GetComponentInChildren<Slider>();
            _effects.onValueChanged.AddListener(v => Edit(s => s.effectsVolume = v, false));
            _quiet = Row(rows, "Quiet mode", "Softer bangs, no flashes or shake", SwitchView.Create(rows).gameObject).GetComponentInChildren<SwitchView>();
            _quiet.Changed += on => Edit(s => s.quietMode = on, true);

            Section(rows, "Comfort");
            _reduceFlash = Row(rows, "Reduce flashes", "Halves flash brightness", SwitchView.Create(rows).gameObject).GetComponentInChildren<SwitchView>();
            _reduceFlash.Changed += on => Edit(s => s.reduceFlash = on, true);
            _reduceMotion = Row(rows, "Reduce motion", "No camera shake", SwitchView.Create(rows).gameObject).GetComponentInChildren<SwitchView>();
            _reduceMotion.Changed += on => Edit(s => s.reduceShake = on, true);

            Section(rows, "Vibration");
            GameObject vibrationRow = Row(rows, "Vibration", "Taps and rumbles with each cracker", SwitchView.Create(rows).gameObject);
            _vibration = vibrationRow.GetComponentInChildren<SwitchView>();
            _vibrationNote = vibrationRow.transform.Find("Labels/Sub").GetComponentInChildren<TextMeshProUGUI>();
            _vibration.Changed += on =>
            {
                Edit(s => s.hapticsEnabled = on, true);
                _vibrationStrength.interactable = on && _vibration.Interactable;
            };
            _vibrationStrength = Row(rows, "Vibration strength", null, SliderFactory.Create(rows).gameObject).GetComponentInChildren<Slider>();
            _vibrationStrength.onValueChanged.AddListener(v => Edit(s => s.hapticsIntensity = v, false));

            Section(rows, "Graphics");
            _tier = Row(rows, "Quality", "Auto picks the best level for this device", SegmentedView.Create(rows, TierLabels).gameObject).GetComponentInChildren<SegmentedView>();
            _tier.Changed += index => Edit(s => s.qualityTier = index - 1, true);

            // F11: one-time Remove ads and unlock all, and Restore (PRD 15.1).
            Section(rows, "Remove ads");
            GameObject buyRow = Row(rows, "Remove ads and unlock all", "No ads, every cracker unlocked", RowButton(rows, "Buy", BuyRemoveAds));
            _removeAdsNote = buyRow.transform.Find("Labels/Sub").GetComponentInChildren<TextMeshProUGUI>();
            _buyButton = buyRow.GetComponentInChildren<Button>().gameObject;
            GameObject restoreRow = Row(rows, "Restore purchases", "Bought on another phone? Get it back here", RowButton(rows, "Restore", RestorePurchases));
            _restoreNote = restoreRow.transform.Find("Labels/Sub").GetComponentInChildren<TextMeshProUGUI>();

            // PRD 15.2 and 16: a Privacy options entry, and analytics the player can switch off.
            Section(rows, "Privacy");
            _usageStats = Row(rows, "Share usage statistics", "Anonymous; no personal data", SwitchView.Create(rows).gameObject).GetComponentInChildren<SwitchView>();
            _usageStats.Changed += on => Edit(s => s.shareUsageStats = on, true);
            GameObject privacyRow = Row(rows, "Privacy options", "Checking ad consent", RowButton(rows, "Open", OpenPrivacyOptions));
            _privacyNote = privacyRow.transform.Find("Labels/Sub").GetComponentInChildren<TextMeshProUGUI>();
            _links = AppLinks.Load();
            _privacyPolicyRow = Row(rows, "Privacy policy", "What data the app and its ads use", RowButton(rows, "Read", OpenPrivacyPolicy));
            _privacyPolicyRow.SetActive(_links.HasPrivacyPolicy);

            Accessible("Volume", _master);
            Accessible("Fireworks volume", _effects);
            Accessible("Quiet mode", _quiet);
            Accessible("Reduce flashes", _reduceFlash);
            Accessible("Reduce motion", _reduceMotion);
            Accessible("Vibration", _vibration);
            Accessible("Vibration strength", _vibrationStrength);
            for (int i = 0; i < _tier.Count; i++)
            {
                _controls.Add(new AccessibleControl { Label = "Graphics " + TierLabels[i], Rect = _tier.SegmentRect(i), Segmented = _tier, Segment = i });
            }

            Button buy = _buyButton.GetComponent<Button>();
            _controls.Add(new AccessibleControl { Label = "Remove ads and unlock all, buy", Rect = (RectTransform)buy.transform, Button = buy });
            Button restore = restoreRow.GetComponentInChildren<Button>();
            _controls.Add(new AccessibleControl { Label = "Restore purchases", Rect = (RectTransform)restore.transform, Button = restore });
            Accessible("Share usage statistics", _usageStats);
            Button privacyButton = privacyRow.GetComponentInChildren<Button>();
            _controls.Add(new AccessibleControl { Label = "Privacy options", Rect = (RectTransform)privacyButton.transform, Button = privacyButton });
            Button policyButton = _privacyPolicyRow.GetComponentInChildren<Button>(true);
            _controls.Add(new AccessibleControl { Label = "Privacy policy", Rect = (RectTransform)policyButton.transform, Button = policyButton });

            _overlay.gameObject.SetActive(false);
        }

        private void Accessible(string label, Slider slider) =>
            _controls.Add(new AccessibleControl { Label = label, Rect = (RectTransform)slider.transform, Slider = slider });

        private void Accessible(string label, SwitchView view) =>
            _controls.Add(new AccessibleControl { Label = label, Rect = (RectTransform)view.transform, Switch = view });

        private static GameObject RowButton(RectTransform rows, string label, System.Action onClick)
        {
            RectTransform rect = UiKit.Rect("Button_" + label, rows);
            rect.sizeDelta = new Vector2(200f, UiTheme.MinTouch);
            Image image = UiKit.Panel(rect, UiTheme.IndigoLight);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick());
            UiKit.Text(rect, label, UiTheme.BodyText, TextAlignmentOptions.Center, UiTheme.OffWhite);
            return rect.gameObject;
        }

        private static void Section(RectTransform rows, string label)
        {
            RectTransform rect = UiKit.Rect("Section_" + label, rows);
            rect.sizeDelta = new Vector2(0f, 70f);
            TextMeshProUGUI text = UiKit.Text(rect, label.ToUpperInvariant(), 26f, TextAlignmentOptions.BottomLeft, UiTheme.Saffron);
            text.characterSpacing = 6f;
        }

        /// <summary>A labelled row with an optional second line and a control on the right.</summary>
        private static GameObject Row(RectTransform rows, string label, string sub, GameObject control)
        {
            RectTransform row = UiKit.Rect("Row_" + label, rows);
            row.sizeDelta = new Vector2(0f, RowHeight);

            RectTransform labels = UiKit.Rect("Labels", row);
            labels.anchorMin = new Vector2(0f, 0f);
            labels.anchorMax = new Vector2(0.55f, 1f);
            labels.offsetMin = Vector2.zero;
            labels.offsetMax = Vector2.zero;
            RectTransform main = UiKit.Rect("Main", labels);
            main.anchorMin = new Vector2(0f, string.IsNullOrEmpty(sub) ? 0f : 0.45f);
            main.anchorMax = new Vector2(1f, 1f);
            main.offsetMin = main.offsetMax = Vector2.zero;
            TextMeshProUGUI mainText = UiKit.Text(main, label, UiTheme.BodyText, TextAlignmentOptions.Left, UiTheme.OffWhite);
            mainText.enableAutoSizing = true;
            mainText.fontSizeMin = 28f;
            mainText.fontSizeMax = mainText.fontSize;
            RectTransform subRect = UiKit.Rect("Sub", labels);
            subRect.anchorMin = new Vector2(0f, 0f);
            subRect.anchorMax = new Vector2(1f, 0.45f);
            subRect.offsetMin = subRect.offsetMax = Vector2.zero;
            TextMeshProUGUI subText = UiKit.Text(subRect, sub ?? string.Empty, 26f, TextAlignmentOptions.TopLeft, UiTheme.Muted);
            subText.enableAutoSizing = true;
            subText.fontSizeMin = 24f;
            subText.fontSizeMax = subText.fontSize;

            var c = (RectTransform)control.transform;
            c.SetParent(row, false);
            c.anchorMin = c.anchorMax = c.pivot = new Vector2(1f, 0.5f);
            c.anchoredPosition = Vector2.zero;
            return row.gameObject;
        }
    }
}

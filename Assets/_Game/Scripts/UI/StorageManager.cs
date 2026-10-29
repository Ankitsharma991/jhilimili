using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UtsavNights.Core.Packs;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;

namespace UtsavNights.UI
{
    /// <summary>
    /// The storage manager (Phase 9 step 3, PRD 12 "Storage manager: sizes, delete, update, Wi-Fi only toggle"). Lists every
    /// pack on the phone with its version and size, offers an update when the catalog has a newer one, deletes, and switches
    /// mobile-data downloads on or off (Wi-Fi only by default). Opened from Settings, Downloads and storage.
    /// </summary>
    public sealed class StorageManager : MonoBehaviour
    {
        private readonly List<GameObject> _rows = new List<GameObject>();
        private RectTransform _overlay;
        private RectTransform _list;
        private TextMeshProUGUI _total;
        private TextMeshProUGUI _status;
        private SwitchView _wifi;
        private PackService _packs;

        public bool IsOpen => _overlay != null && _overlay.gameObject.activeSelf;

        /// <summary>Pack ids listed (tests and the screen reader).</summary>
        public List<string> Listed { get; } = new List<string>();

        public string Status => _status != null ? _status.text : string.Empty;

        private void Awake() => Build();

        private void OnDestroy()
        {
            if (_packs != null)
            {
                _packs.Changed -= Refresh;
                _packs.Finished -= OnFinished;
            }
        }

        private void Update()
        {
            if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
        }

        public void Open()
        {
            if (_packs == null && Game.Services.TryGet(out _packs))
            {
                _packs.Changed += Refresh;
                _packs.Finished += OnFinished;
            }

            _overlay.gameObject.SetActive(true);
            _overlay.SetAsLastSibling();
            InputBlockers.Register(_overlay);
            _wifi.Set(PackService.WifiOnly, false);
            Refresh();
        }

        public void Close()
        {
            InputBlockers.Unregister(_overlay);
            _overlay.gameObject.SetActive(false);
        }

        public PackResult Delete(string packId)
        {
            PackResult r = _packs != null ? _packs.Delete(packId) : PackResult.Failed("No packs here.");
            _status.text = r.Message;
            Refresh();
            return r;
        }

        public bool UpdatePack(string packId)
        {
            string reason = "No packs here.";
            if (_packs == null || !_packs.Install(packId, out reason))
            {
                _status.text = reason;
                return false;
            }

            _status.text = "Updating…";
            return true;
        }

        public void CheckForUpdates()
        {
            if (_packs == null || !_packs.HasServer)
            {
                _status.text = "Packs aren't available yet.";
                return;
            }

            _status.text = "Checking…";
            _ = CheckAsync();
        }

        private async System.Threading.Tasks.Task CheckAsync()
        {
            bool ok = await _packs.RefreshCatalog();
            if (_status != null)
            {
                _status.text = ok ? $"{_packs.Manager.Updates(_packs.Catalog).Count} update(s) available." : "Couldn't reach the pack server.";
            }
        }

        private void OnFinished(string packId, PackResult result) => _status.text = result.Message;

        private void Refresh()
        {
            if (!IsOpen)
            {
                return;
            }

            foreach (GameObject row in _rows)
            {
                Destroy(row);
            }

            _rows.Clear();
            Listed.Clear();
            long total = 0;
            if (_packs != null)
            {
                List<PackManifest> updates = _packs.Manager.Updates(_packs.Catalog);
                foreach (PackRequirement p in _packs.Manager.Installed)
                {
                    PackManifest m = _packs.Manager.Manifest(p.pack);
                    if (m == null)
                    {
                        continue;
                    }

                    total += m.sizeBytes;
                    PackManifest newer = updates.Find(u => u.packId == m.packId);
                    _rows.Add(Row(m, newer));
                    Listed.Add(m.packId);
                }
            }

            _total.text = Listed.Count == 0 ? "No downloaded packs yet. The built-in neighbourhoods are always here." : $"{Listed.Count} pack(s), {PackLint.Format(total)} in all";
        }

        private GameObject Row(PackManifest m, PackManifest newer)
        {
            RectTransform row = UiKit.Rect(m.packId, _list);
            row.sizeDelta = new Vector2(0f, 150f);
            UiKit.Panel(row, UiTheme.IndigoLight).sprite = UiKit.Rounded;
            RectTransform text = UiKit.Rect("Text", row);
            text.anchorMin = new Vector2(0f, 0f);
            text.anchorMax = new Vector2(0.55f, 1f);
            text.offsetMin = new Vector2(24f, 10f);
            text.offsetMax = new Vector2(0f, -10f);
            string sub = newer != null ? $"Version {m.version}, update to {newer.version}" : $"Version {m.version}";
            TextMeshProUGUI t = UiKit.Text(text, $"{m.title}\n<size=75%>{sub}  ·  {PackLint.Format(m.sizeBytes)}</size>", UiTheme.SmallText, TextAlignmentOptions.Left, UiTheme.OffWhite);
            t.textWrappingMode = TextWrappingModes.Normal;
            string id = m.packId;
            if (newer != null)
            {
                SmallButton(row, "Update", 0.57f, 0.77f, UiTheme.Saffron, UiTheme.Indigo, () => UpdatePack(id));
            }

            SmallButton(row, "Delete", 0.79f, 0.98f, UiTheme.Magenta, UiTheme.OffWhite, () => Delete(id));
            return row.gameObject;
        }

        private void Build()
        {
            _overlay = UiKit.Rect("StorageManager", transform.parent != null ? transform.parent : transform);
            UiKit.Stretch(_overlay);
            UiKit.Panel(_overlay, new Color(0.09f, 0.06f, 0.2f, 0.99f));
            RectTransform title = UiKit.Rect("Title", _overlay);
            title.anchorMin = new Vector2(0f, 1f);
            title.anchorMax = new Vector2(1f, 1f);
            title.pivot = new Vector2(0.5f, 1f);
            title.sizeDelta = new Vector2(-300f, UiTheme.MinTouch);
            title.anchoredPosition = new Vector2(0f, -20f);
            UiKit.Text(title, "Downloads and storage", UiTheme.TitleText, TextAlignmentOptions.Center, UiTheme.Marigold);
            TopButton("Close", new Vector2(0f, 1f), new Vector2(28f, -20f), UiTheme.IndigoLight, UiTheme.OffWhite, Close);

            RectTransform wifiRow = UiKit.Rect("WifiOnly", _overlay);
            wifiRow.anchorMin = new Vector2(0f, 1f);
            wifiRow.anchorMax = new Vector2(1f, 1f);
            wifiRow.pivot = new Vector2(0.5f, 1f);
            wifiRow.sizeDelta = new Vector2(-48f, UiTheme.MinTouch);
            wifiRow.anchoredPosition = new Vector2(0f, -UiTheme.MinTouch - 44f);
            RectTransform wifiLabel = UiKit.Rect("Label", wifiRow);
            wifiLabel.anchorMin = Vector2.zero;
            wifiLabel.anchorMax = new Vector2(0.75f, 1f);
            wifiLabel.offsetMin = wifiLabel.offsetMax = Vector2.zero;
            UiKit.Text(wifiLabel, "Download on Wi-Fi only", UiTheme.BodyText, TextAlignmentOptions.Left, UiTheme.OffWhite);
            _wifi = SwitchView.Create(wifiRow);
            var switchRect = (RectTransform)_wifi.transform;
            switchRect.anchorMin = switchRect.anchorMax = switchRect.pivot = new Vector2(1f, 0.5f);
            switchRect.anchoredPosition = Vector2.zero;
            _wifi.Changed += on => PackService.WifiOnly = on;

            RectTransform total = UiKit.Rect("Total", _overlay);
            total.anchorMin = new Vector2(0f, 1f);
            total.anchorMax = new Vector2(1f, 1f);
            total.pivot = new Vector2(0.5f, 1f);
            total.sizeDelta = new Vector2(-48f, 80f);
            total.anchoredPosition = new Vector2(0f, -2f * UiTheme.MinTouch - 60f);
            _total = UiKit.Text(total, string.Empty, UiTheme.SmallText, TextAlignmentOptions.Left, UiTheme.Muted);
            _total.textWrappingMode = TextWrappingModes.Normal;

            RectTransform viewport = UiKit.Rect("Packs", _overlay);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(24f, UiTheme.MinTouch + 110f);
            viewport.offsetMax = new Vector2(-24f, -2f * UiTheme.MinTouch - 150f);
            viewport.gameObject.AddComponent<RectMask2D>();
            _list = UiKit.Rect("Rows", viewport);
            _list.anchorMin = new Vector2(0f, 1f);
            _list.anchorMax = new Vector2(1f, 1f);
            _list.pivot = new Vector2(0.5f, 1f);
            _list.sizeDelta = Vector2.zero;
            var layout = _list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            _list.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.viewport = viewport;
            scroll.content = _list;

            TopButton("Check for updates", new Vector2(0.5f, 0f), new Vector2(0f, 90f), UiTheme.IndigoLight, UiTheme.OffWhite, CheckForUpdates, 460f);
            RectTransform status = UiKit.Rect("Status", _overlay);
            status.anchorMin = new Vector2(0f, 0f);
            status.anchorMax = new Vector2(1f, 0f);
            status.pivot = new Vector2(0.5f, 0f);
            status.sizeDelta = new Vector2(-48f, 70f);
            status.anchoredPosition = new Vector2(0f, 16f);
            _status = UiKit.Text(status, string.Empty, UiTheme.SmallText, TextAlignmentOptions.Center, UiTheme.OffWhite);
            _status.textWrappingMode = TextWrappingModes.Normal;
            _overlay.gameObject.SetActive(false);
        }

        private void TopButton(string label, Vector2 anchor, Vector2 position, Color background, Color text, Action action, float width = 220f)
        {
            RectTransform rect = UiKit.Rect(label, _overlay);
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.sizeDelta = new Vector2(width, UiTheme.MinTouch);
            rect.anchoredPosition = position;
            Image image = UiKit.Panel(rect, background);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            UiKit.Text(rect, label, UiTheme.BodyText, TextAlignmentOptions.Center, text);
        }

        private static void SmallButton(RectTransform row, string label, float from, float to, Color background, Color text, Action action)
        {
            RectTransform rect = UiKit.Rect(label, row);
            rect.anchorMin = new Vector2(from, 0.15f);
            rect.anchorMax = new Vector2(to, 0.85f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Image image = UiKit.Panel(rect, background);
            image.sprite = UiKit.Rounded;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            UiKit.Text(rect, label, UiTheme.SmallText, TextAlignmentOptions.Center, text);
        }
    }
}

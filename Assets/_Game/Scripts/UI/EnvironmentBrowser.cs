using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UtsavNights.Core.Home;
using UtsavNights.Core.Neighbourhood;
using UtsavNights.Core.Packs;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;

namespace UtsavNights.UI
{
    /// <summary>
    /// The environment browser (Phase 8 step 6, Phase 9 step 3; PRD 12 "filter chips, preview, size, offline badge, download
    /// progress"). Opens from the walk map's Neighbourhoods button. Cards for every environment on the phone (built in, or from
    /// an installed pack: "On this phone", Choose) and for every environment pack in the catalog not yet downloaded (its
    /// size, Download, a progress bar while it downloads). Choosing ends the walk, builds that settlement and remembers it.
    /// </summary>
    public sealed class EnvironmentBrowser : MonoBehaviour
    {
        public static readonly string[] Filters = { "All", "Diwali", "Tihar", "North India", "Nepal", "East India" };
        private static readonly string[] RegionIds = { null, null, null, "north_india", "nepal", "east_india" };

        [SerializeField] private Texture2D[] previews = new Texture2D[0];

        private readonly List<Card> _cards = new List<Card>();
        private RectTransform _overlay;
        private RectTransform _rows;
        private SegmentedView _filter;
        private TextMeshProUGUI _status;
        private PackService _packs;

        private sealed class Card
        {
            public RectTransform Root;
            public string Id;
            public bool Download;
            public Festival Festival;
            public string Region;
            public Button Button;
            public TextMeshProUGUI Label;
            public RectTransform Fill;
            public Texture2D OwnPreview;
        }

        public bool IsOpen => _overlay != null && _overlay.gameObject.activeSelf;

        /// <summary>Ids on the cards the filter shows: environment ids, and pack ids for downloads (tests and the screen reader).</summary>
        public List<string> Shown
        {
            get
            {
                var ids = new List<string>();
                foreach (Card c in _cards)
                {
                    if (c.Root.gameObject.activeSelf)
                    {
                        ids.Add(c.Id);
                    }
                }

                return ids;
            }
        }

        public string Status => _status != null ? _status.text : string.Empty;

        public void Configure(Texture2D[] textures) => previews = textures ?? new Texture2D[0];

        private void Awake() => Build();

        private void OnDisable()
        {
            if (IsOpen)
            {
                Close();
            }
        }

        private void OnDestroy()
        {
            if (_packs != null)
            {
                _packs.Changed -= OnPacksChanged;
                _packs.Finished -= OnFinished;
            }

            ClearCards();
        }

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            if (_packs != null && _packs.Busy)
            {
                foreach (Card c in _cards)
                {
                    if (c.Download && c.Id == _packs.BusyPack)
                    {
                        c.Fill.anchorMax = new Vector2(Mathf.Clamp01(_packs.Progress), 1f);
                    }
                }
            }
        }

        public void Open()
        {
            if (IsOpen)
            {
                return;
            }

            if (_packs == null && Game.Services.TryGet(out _packs))
            {
                _packs.Changed += OnPacksChanged;
                _packs.Finished += OnFinished;
                if (_packs.HasServer)
                {
                    _ = _packs.RefreshCatalog();
                }
            }

            _overlay.gameObject.SetActive(true);
            _overlay.SetAsLastSibling();
            InputBlockers.Register(_overlay);
            _status.text = _packs != null && !_packs.HasServer ? string.Empty : _status.text;
            Rebuild();
            _filter.Set(0, false);
            Filter(0);
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            InputBlockers.Unregister(_overlay);
            _overlay.gameObject.SetActive(false);
        }

        public void Filter(int index)
        {
            _filter.Set(index, false);
            foreach (Card c in _cards)
            {
                c.Root.gameObject.SetActive(Matches(c, index));
            }

            Refresh();
        }

        /// <summary>Ends any walk, builds the environment's settlement and remembers it.</summary>
        public bool Choose(string environmentId)
        {
            if (Environments.Find(environmentId) == null || !Game.Services.TryGet(out SettlementView settlement))
            {
                return false;
            }

            WalkMode walk = FindFirstObjectByType<WalkMode>();
            if (walk != null && walk.IsOpen)
            {
                walk.Home();
            }

            PlayerPrefs.SetString(NeighbourhoodInstaller.EnvironmentPrefKey, environmentId);
            if (settlement.Layout.environment != environmentId)
            {
                settlement.Load(environmentId);
                Game.Events.Publish(new EnvironmentChosenEvent(environmentId));
            }

            Refresh();
            Close();
            return true;
        }

        /// <summary>Starts downloading an environment pack; the card shows its progress.</summary>
        public bool Download(string packId)
        {
            if (_packs == null)
            {
                return false;
            }

            if (!_packs.Install(packId, out string reason))
            {
                _status.text = reason;
                return false;
            }

            _status.text = "Downloading…";
            Refresh();
            return true;
        }

        private void OnPacksChanged()
        {
            if (IsOpen && (_packs == null || !_packs.Busy))
            {
                int filter = _filter.Selected;
                Rebuild();
                Filter(Mathf.Max(0, filter));
            }
        }

        private void OnFinished(string packId, PackResult result)
        {
            if (_status != null)
            {
                _status.text = result.Message;
            }
        }

        private static bool Matches(Card c, int filter) => filter switch
        {
            1 => (c.Festival & Festival.Diwali) != 0,
            2 => (c.Festival & Festival.Tihar) != 0,
            3 or 4 or 5 => c.Region == RegionIds[filter],
            _ => true,
        };

        private void Refresh()
        {
            string current = Game.Services.TryGet(out SettlementView settlement) ? settlement.Layout.environment : null;
            bool busy = _packs != null && _packs.Busy;
            foreach (Card c in _cards)
            {
                if (c.Download)
                {
                    bool mine = busy && _packs.BusyPack == c.Id;
                    c.Button.interactable = !busy;
                    c.Label.text = mine ? "Downloading" : "Download";
                    c.Fill.parent.gameObject.SetActive(mine);
                }
                else
                {
                    bool inUse = c.Id == current;
                    c.Button.interactable = !inUse;
                    c.Label.text = inUse ? "In use" : "Choose";
                }
            }
        }

        private void Build()
        {
            _overlay = UiKit.Rect("EnvironmentBrowser", transform.parent != null ? transform.parent : transform);
            UiKit.Stretch(_overlay);
            UiKit.Panel(_overlay, new Color(0.09f, 0.06f, 0.2f, 0.99f));

            RectTransform title = UiKit.Rect("Title", _overlay);
            title.anchorMin = new Vector2(0f, 1f);
            title.anchorMax = new Vector2(1f, 1f);
            title.pivot = new Vector2(0.5f, 1f);
            title.sizeDelta = new Vector2(-300f, UiTheme.MinTouch);
            title.anchoredPosition = new Vector2(0f, -20f);
            UiKit.Text(title, "Neighbourhoods", UiTheme.TitleText, TextAlignmentOptions.Center, UiTheme.Marigold);
            Button(_overlay, "Close", new Vector2(0f, 1f), new Vector2(28f, -20f), 220f, UiTheme.IndigoLight, UiTheme.OffWhite, Close);

            _filter = SegmentedView.Create(_overlay, Filters);
            var filterRect = (RectTransform)_filter.transform;
            filterRect.anchorMin = new Vector2(0f, 1f);
            filterRect.anchorMax = new Vector2(1f, 1f);
            filterRect.pivot = new Vector2(0.5f, 1f);
            filterRect.sizeDelta = new Vector2(-40f, UiTheme.MinTouch * 0.85f);
            filterRect.anchoredPosition = new Vector2(0f, -UiTheme.MinTouch - 40f);
            _filter.Changed += Filter;

            RectTransform status = UiKit.Rect("Status", _overlay);
            status.anchorMin = new Vector2(0f, 0f);
            status.anchorMax = new Vector2(1f, 0f);
            status.pivot = new Vector2(0.5f, 0f);
            status.sizeDelta = new Vector2(-48f, 70f);
            status.anchoredPosition = new Vector2(0f, 16f);
            _status = UiKit.Text(status, string.Empty, UiTheme.SmallText, TextAlignmentOptions.Center, UiTheme.OffWhite);
            _status.textWrappingMode = TextWrappingModes.Normal;

            RectTransform viewport = UiKit.Rect("Cards", _overlay);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(24f, 96f);
            viewport.offsetMax = new Vector2(-24f, -2.1f * UiTheme.MinTouch - 50f);
            viewport.gameObject.AddComponent<RectMask2D>();
            _rows = UiKit.Rect("Rows", viewport);
            _rows.anchorMin = new Vector2(0f, 1f);
            _rows.anchorMax = new Vector2(1f, 1f);
            _rows.pivot = new Vector2(0.5f, 1f);
            _rows.sizeDelta = Vector2.zero;
            var layout = _rows.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 20f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            _rows.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.viewport = viewport;
            scroll.content = _rows;
            _overlay.gameObject.SetActive(false);
        }

        private void Rebuild()
        {
            ClearCards();
            foreach (EnvironmentInfo env in Environments.Available)
            {
                int builtIn = Array.IndexOf(Environments.All, env);
                Texture2D preview = builtIn >= 0 && builtIn < previews.Length ? previews[builtIn] : _packs?.Preview(env.PackId);
                Card c = CardFor(env.Id, env.Name, $"{RegionName(env.Region)}  ·  {FestivalName(env.Festival)}", env.Description,
                    env.BuiltIn ? "Built in, no download" : $"Downloaded, {env.SizeMb:0.0} MB", "On this phone", preview, download: false);
                c.Festival = env.Festival;
                c.Region = env.Region;
                c.OwnPreview = builtIn < 0 ? preview : null;
            }

            if (_packs?.Catalog == null)
            {
                return;
            }

            foreach (PackManifest m in _packs.Catalog.packs)
            {
                if (m.type != PackTypes.Environment || _packs.Manager.IsInstalled(m.packId))
                {
                    continue;
                }

                string region = m.tags?.geo != null && m.tags.geo.Length > 0 ? m.tags.geo[0] : string.Empty;
                Festival festival = Festival.None;
                foreach (string f in m.tags?.festival ?? new string[0])
                {
                    festival |= Enum.TryParse(f, true, out Festival one) ? one : Festival.None;
                }

                Card c = CardFor(m.packId, m.title, $"{RegionName(region)}  ·  {FestivalName(festival)}", m.description, PackLint.Format(m.sizeBytes),
                    "Download", null, download: true);
                c.Festival = festival;
                c.Region = region;
            }
        }

        private Card CardFor(string id, string name, string chips, string description, string size, string badgeText, Texture2D preview, bool download)
        {
            RectTransform card = UiKit.Rect(id, _rows);
            card.sizeDelta = new Vector2(0f, 640f);
            UiKit.Panel(card, UiTheme.IndigoLight).sprite = UiKit.Rounded;

            RectTransform image = UiKit.Rect("Preview", card);
            image.anchorMin = new Vector2(0f, 1f);
            image.anchorMax = new Vector2(1f, 1f);
            image.pivot = new Vector2(0.5f, 1f);
            image.sizeDelta = new Vector2(-32f, 360f);
            image.anchoredPosition = new Vector2(0f, -16f);
            RawImage raw = image.gameObject.AddComponent<RawImage>();
            raw.texture = preview;
            raw.color = preview != null ? Color.white : new Color(0.2f, 0.15f, 0.35f);
            raw.raycastTarget = false;

            RectTransform badge = UiKit.Rect("Badge", image);
            badge.anchorMin = badge.anchorMax = badge.pivot = new Vector2(1f, 1f);
            badge.sizeDelta = new Vector2(250f, 56f);
            badge.anchoredPosition = new Vector2(-12f, -12f);
            UiKit.Panel(badge, new Color(0.07f, 0.05f, 0.18f, 0.85f), raycast: false).sprite = UiKit.Rounded;
            UiKit.Text(badge, badgeText, UiTheme.SmallText * 0.8f, TextAlignmentOptions.Center, UiTheme.Marigold);

            Line(card, name, UiTheme.BodyText, UiTheme.Marigold, -392f, 52f);
            Line(card, chips, UiTheme.SmallText, UiTheme.OffWhite, -444f, 40f);
            Line(card, description, UiTheme.SmallText, UiTheme.Muted, -486f, 72f);
            Line(card, size, UiTheme.SmallText * 0.85f, UiTheme.Muted, -560f, 40f, 0.6f);

            // Download progress (packs); hidden for what is already on the phone.
            RectTransform track = UiKit.Rect("Progress", card);
            track.anchorMin = new Vector2(0f, 0f);
            track.anchorMax = new Vector2(0.6f, 0f);
            track.pivot = new Vector2(0f, 0f);
            track.sizeDelta = new Vector2(-40f, 12f);
            track.anchoredPosition = new Vector2(24f, 16f);
            UiKit.Panel(track, new Color(1f, 1f, 1f, 0.15f), raycast: false);
            RectTransform fill = UiKit.Rect("Fill", track);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            UiKit.Panel(fill, UiTheme.Saffron, raycast: false);
            track.gameObject.SetActive(false);

            RectTransform choose = UiKit.Rect("Choose", card);
            choose.anchorMin = choose.anchorMax = choose.pivot = new Vector2(1f, 0f);
            choose.sizeDelta = new Vector2(300f, UiTheme.MinTouch);
            choose.anchoredPosition = new Vector2(-20f, 20f);
            Image bg = UiKit.Panel(choose, UiTheme.Saffron);
            bg.sprite = UiKit.Rounded;
            var button = choose.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(() =>
            {
                if (download)
                {
                    Download(id);
                }
                else
                {
                    Choose(id);
                }
            });
            TextMeshProUGUI label = UiKit.Text(choose, download ? "Download" : "Choose", UiTheme.BodyText, TextAlignmentOptions.Center, UiTheme.Indigo);
            var c = new Card { Root = card, Id = id, Download = download, Button = button, Label = label, Fill = fill };
            _cards.Add(c);
            return c;
        }

        private void ClearCards()
        {
            foreach (Card c in _cards)
            {
                if (c.OwnPreview != null)
                {
                    Destroy(c.OwnPreview);
                }

                if (c.Root != null)
                {
                    Destroy(c.Root.gameObject);
                }
            }

            _cards.Clear();
        }

        private static void Line(RectTransform card, string text, float size, Color colour, float y, float height, float width = 1f)
        {
            RectTransform r = UiKit.Rect("Line", card);
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(width, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.sizeDelta = new Vector2(-48f, height);
            r.anchoredPosition = new Vector2(width < 1f ? -24f * (1f - width) : 0f, y);
            TextMeshProUGUI t = UiKit.Text(r, text, size, TextAlignmentOptions.Left, colour);
            t.textWrappingMode = TextWrappingModes.Normal;
        }

        private static string RegionName(string region) => region switch
        {
            "north_india" => "North India",
            "nepal" => "Nepal",
            "east_india" => "East India",
            "west_india" => "West India",
            _ => region,
        };

        private static string FestivalName(Festival f) => (f & Festival.Tihar) != 0 ? "Tihar" : (f & Festival.Diwali) != 0 ? "Diwali" : "Festive";

        private static void Button(RectTransform parent, string label, Vector2 anchor, Vector2 position, float width, Color background, Color text, Action action)
        {
            RectTransform rect = UiKit.Rect(label, parent);
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.sizeDelta = new Vector2(width, UiTheme.MinTouch);
            rect.anchoredPosition = position;
            Image image = UiKit.Panel(rect, background);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            UiKit.Text(rect, label, UiTheme.BodyText, TextAlignmentOptions.Center, text);
        }
    }
}

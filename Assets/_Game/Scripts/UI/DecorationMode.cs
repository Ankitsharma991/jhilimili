using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UtsavNights.Core.Art;
using UtsavNights.Core.Home;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;

namespace UtsavNights.UI
{
    /// <summary>
    /// Decoration mode's screen (F17 and F18, PRD 9.1 and 12). Opens from the house button beside the avatar button. Top
    /// bar: view (house or floor), layouts, undo, redo, Done. Bottom drawer: a Decorate tab (festival filter, categories,
    /// items) and a House tab (modules, colours, wall material). A bar for the selected item: colour, rotate, size, copy,
    /// delete. The 3D view between them belongs to <see cref="DecorationController"/>; a full-screen blocker keeps the
    /// lighter and cracker placement from reacting while the mode is open. Done (or Android back) saves house.json.
    /// </summary>
    public sealed class DecorationMode : MonoBehaviour
    {
        public static readonly string[] Tabs = { "Decorate", "House" };
        public static readonly string[] Festivals = { "All", "Diwali", "Tihar", "Everyday" };
        public static readonly string[] Categories = { "Lamps", "Lights", "Garlands", "Plants", "Furniture", "Rangoli" };
        private const float RowHeight = 112f;
        private const float CardWidth = 250f;

        private static readonly (string label, string[] points, bool optional)[] ModuleRows =
        {
            ("Door", new[] { "door" }, false),
            ("Ground floor windows", new[] { "window_ground_left", "window_ground_right" }, false),
            ("Upper windows", new[] { "window_upper_left", "window_upper_right" }, false),
            ("Balcony", new[] { "balcony" }, true),
            ("Roof", new[] { "roof" }, false),
            ("Courtyard walls", new[] { "boundary" }, false),
            ("Gate", new[] { "gate" }, false),
        };

        private static readonly string[] WallColourNames = { "Warm lime", "Ochre", "Terracotta", "Sky blue", "Mint", "Pink", "Saffron", "Grey", "Lime white" };

        private readonly List<Action> _refreshers = new List<Action>();
        private readonly List<RectTransform> _uiRects = new List<RectTransform>();
        private readonly List<(Image card, string item)> _cards = new List<(Image, string)>();
        private RectTransform _overlay;
        private RectTransform _openButton;
        private RectTransform _drawer;
        private RectTransform _selectionBar;
        private RectTransform _layoutsSheet;
        private RectTransform _decoratePage;
        private RectTransform _housePage;
        private RectTransform _itemsRow;
        private TextMeshProUGUI _message;
        private TextMeshProUGUI _viewLabel;
        private Button _undo;
        private Button _redo;
        private SegmentedView _tabs;
        private SegmentedView _festival;
        private SegmentedView _category;
        private DecorationController _controller;
        private HomeService _home;

        public bool IsOpen => _overlay != null && _overlay.gameObject.activeSelf;

        public RectTransform OpenButton => _openButton;

        public DecorationController Controller => _controller;

        public int Tab => _tabs != null ? _tabs.Selected : -1;

        /// <summary>Item ids on the Decorate tab's shelf for the current festival and category.</summary>
        public IReadOnlyList<string> Shelf => _cards.Where(c => c.card.gameObject.activeSelf).Select(c => c.item).ToList();

        public bool LayoutsOpen => _layoutsSheet != null && _layoutsSheet.gameObject.activeSelf;

        private void Awake()
        {
            _controller = FindFirstObjectByType<DecorationController>();
            if (_controller == null)
            {
                _controller = new GameObject("DecorationController").AddComponent<DecorationController>();
            }

            _controller.IsOverUi = IsOverUi;
            _controller.StateChanged += Refresh;
            Build();
        }

        private void OnEnable()
        {
            if (_openButton != null)
            {
                InputBlockers.Register(_openButton);
            }
        }

        private void OnDisable()
        {
            if (_openButton != null)
            {
                InputBlockers.Unregister(_openButton);
            }

            if (IsOpen)
            {
                // Torn down (scene unload, app killed): no save. The journal already holds every edit and replays it on
                // the next start; only Done saves.
                Leave(save: false);
            }
        }

        private void OnDestroy()
        {
            if (_controller != null)
            {
                _controller.StateChanged -= Refresh;
            }
        }

        private void Update()
        {
            if (!IsOpen || Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                return;
            }

            // Android back: close the layouts sheet, then drop the armed item or selection, then leave (saving).
            if (LayoutsOpen)
            {
                _layoutsSheet.gameObject.SetActive(false);
            }
            else if (_controller.Armed != null)
            {
                _controller.Disarm();
            }
            else if (_controller.Selected != 0)
            {
                _controller.Deselect();
            }
            else
            {
                Done();
            }
        }

        public void Open()
        {
            if (IsOpen || !Game.Services.TryGet(out _home) || !_controller.Open())
            {
                return;
            }

            _home.Changed += Refresh;
            _overlay.gameObject.SetActive(true);
            InputBlockers.Register(_overlay);
            _layoutsSheet.gameObject.SetActive(false);
            _tabs.Set(0, true);
            _festival.Set(0, false);
            _category.Set(0, true);
            Refresh();
        }

        /// <summary>Saves and leaves.</summary>
        public void Done() => Leave(save: true);

        private void Leave(bool save)
        {
            if (!IsOpen)
            {
                return;
            }

            if (_controller != null)
            {
                _controller.Close(save);
            }

            _home.Changed -= Refresh;
            InputBlockers.Unregister(_overlay);
            _overlay.gameObject.SetActive(false);
        }

        public void ShowTab(int index)
        {
            _decoratePage.gameObject.SetActive(index == 0);
            _housePage.gameObject.SetActive(index == 1);
            if (index == 1)
            {
                _controller.Disarm();
                _controller.Deselect();
                _controller.SetView(DecorationController.View.House);
            }

            Refresh();
        }

        public void FilterShelf(int festival, int category)
        {
            _festival.Set(festival, false);
            _category.Set(category, false);
            UpdateShelf();
        }

        /// <summary>Saves the current design into a layout slot and writes house.json.</summary>
        public void SaveLayout(int slot)
        {
            if (_home.Editor.SaveLayout(slot, "Layout " + (slot + 1)))
            {
                _home.Save();
                Game.Events.Publish(new HouseLayoutEvent(slot, saved: true));
            }

            Refresh();
        }

        public void LoadLayout(int slot)
        {
            _controller.Deselect();
            if (_home.Editor.LoadLayout(slot))
            {
                Game.Events.Publish(new HouseLayoutEvent(slot, saved: false));
            }

            Refresh();
        }

        public void DeleteLayout(int slot)
        {
            if (_home.Editor.State.DeleteLayout(slot))
            {
                _home.Save();
            }

            Refresh();
        }

        public void ToggleLayouts() => _layoutsSheet.gameObject.SetActive(!LayoutsOpen);

        private bool IsOverUi(Vector2 screen)
        {
            foreach (RectTransform rect in _uiRects)
            {
                if (rect != null && rect.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(rect, screen, null))
                {
                    return true;
                }
            }

            return false;
        }

        private void Refresh()
        {
            if (!IsOpen || _home == null)
            {
                return;
            }

            _undo.interactable = _home.Editor.CanUndo;
            _redo.interactable = _home.Editor.CanRedo;
            _message.text = _controller.Message;
            _message.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(_controller.Message));
            _message.color = _controller.Preview != null && !_controller.Preview.Ok ? new Color(1f, 0.55f, 0.5f) : UiTheme.OffWhite;
            _viewLabel.text = _controller.CurrentView == DecorationController.View.House ? "Floor view" : "House view";
            _selectionBar.gameObject.SetActive(_controller.Selected != 0);
            foreach ((Image card, string item) in _cards)
            {
                card.color = item == _controller.Armed ? UiTheme.Saffron : UiTheme.IndigoLight;
            }

            foreach (Action r in _refreshers)
            {
                r();
            }
        }

        // ---- Building ----

        private void Build()
        {
            // The house button, left of the avatar button in the top-right corner.
            _openButton = UiKit.Rect("DecorateButton", transform);
            _openButton.anchorMin = _openButton.anchorMax = _openButton.pivot = new Vector2(1f, 1f);
            _openButton.sizeDelta = new Vector2(UiTheme.MinTouch, UiTheme.MinTouch);
            _openButton.anchoredPosition = new Vector2(-24f - 2f * (UiTheme.MinTouch + 20f), -24f);
            Image bg = UiKit.Panel(_openButton, UiTheme.Indigo);
            bg.sprite = UiKit.Circle;
            var open = _openButton.gameObject.AddComponent<Button>();
            open.targetGraphic = bg;
            open.onClick.AddListener(Open);
            RectTransform roof = UiKit.Rect("Roof", _openButton);
            roof.sizeDelta = new Vector2(52f, 52f);
            roof.anchoredPosition = new Vector2(0f, 14f);
            roof.localRotation = Quaternion.Euler(0f, 0f, 45f);
            UiKit.Panel(roof, UiTheme.Marigold, raycast: false);
            RectTransform body = UiKit.Rect("Body", _openButton);
            body.sizeDelta = new Vector2(56f, 40f);
            body.anchoredPosition = new Vector2(0f, -18f);
            UiKit.Panel(body, UiTheme.OffWhite, raycast: false);
            RectTransform door = UiKit.Rect("Door", _openButton);
            door.sizeDelta = new Vector2(16f, 24f);
            door.anchoredPosition = new Vector2(0f, -26f);
            UiKit.Panel(door, UiTheme.Indigo, raycast: false);

            // The overlay covers the screen (blocking the lighter) but is transparent over the 3D view.
            _overlay = UiKit.Rect("DecorationOverlay", transform.parent != null ? transform.parent : transform);
            UiKit.Stretch(_overlay);
            Image blocker = UiKit.Panel(_overlay, new Color(0f, 0f, 0f, 0f));
            blocker.raycastTarget = false;

            BuildTopBar();
            BuildDrawer();
            BuildSelectionBar();
            BuildLayoutsSheet();
            _overlay.gameObject.SetActive(false);
        }

        private void BuildTopBar()
        {
            RectTransform bar = UiKit.Rect("TopBar", _overlay);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.sizeDelta = new Vector2(0f, UiTheme.MinTouch + 40f);
            UiKit.Panel(bar, new Color(0.07f, 0.05f, 0.18f, 0.75f));
            _uiRects.Add(bar);
            var row = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(20, 20, 20, 20);
            row.spacing = 14f;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = true;
            Button view = BarButton(bar, "Floor view", UiTheme.IndigoLight, UiTheme.OffWhite, () =>
                _controller.SetView(_controller.CurrentView == DecorationController.View.House ? DecorationController.View.Courtyard : DecorationController.View.House));
            _viewLabel = view.GetComponentInChildren<TextMeshProUGUI>();
            BarButton(bar, "Layouts", UiTheme.IndigoLight, UiTheme.OffWhite, ToggleLayouts);
            _undo = BarButton(bar, "Undo", UiTheme.IndigoLight, UiTheme.OffWhite, () => _controller.Undo());
            _redo = BarButton(bar, "Redo", UiTheme.IndigoLight, UiTheme.OffWhite, () => _controller.Redo());
            BarButton(bar, "Done", UiTheme.Saffron, UiTheme.Indigo, Done);

            // A message line under the bar: hints, and the reason the ghost is red.
            RectTransform line = UiKit.Rect("Message", _overlay);
            line.anchorMin = new Vector2(0f, 1f);
            line.anchorMax = new Vector2(1f, 1f);
            line.pivot = new Vector2(0.5f, 1f);
            line.sizeDelta = new Vector2(-40f, 96f);
            line.anchoredPosition = new Vector2(0f, -UiTheme.MinTouch - 52f);
            UiKit.Panel(line, new Color(0.07f, 0.05f, 0.18f, 0.7f), raycast: false);
            RectTransform text = UiKit.Rect("Text", line);
            UiKit.Stretch(text, 12f);
            _message = UiKit.Text(text, string.Empty, UiTheme.SmallText, TextAlignmentOptions.Center, UiTheme.OffWhite);
            _message.textWrappingMode = TextWrappingModes.Normal;
        }

        private void BuildDrawer()
        {
            _drawer = UiKit.Rect("Drawer", _overlay);
            _drawer.anchorMin = new Vector2(0f, 0f);
            _drawer.anchorMax = new Vector2(1f, 0.36f);
            _drawer.offsetMin = _drawer.offsetMax = Vector2.zero;
            UiKit.Panel(_drawer, new Color(0.09f, 0.06f, 0.2f, 0.96f));
            _uiRects.Add(_drawer);

            _tabs = SegmentedView.Create(_drawer, Tabs);
            Place((RectTransform)_tabs.transform, 1f, -16f);
            _tabs.Changed += ShowTab;

            _decoratePage = Page("DecoratePage");
            _festival = SegmentedView.Create(_decoratePage, Festivals);
            Place((RectTransform)_festival.transform, 1f, 0f);
            _festival.Changed += _ => UpdateShelf();
            _category = SegmentedView.Create(_decoratePage, Categories);
            Place((RectTransform)_category.transform, 1f, -UiTheme.MinTouch - 12f);
            _category.Changed += _ => UpdateShelf();

            RectTransform viewport = UiKit.Rect("Shelf", _decoratePage);
            viewport.anchorMin = new Vector2(0f, 0f);
            viewport.anchorMax = new Vector2(1f, 1f);
            viewport.offsetMin = new Vector2(0f, 8f);
            viewport.offsetMax = new Vector2(0f, -2f * UiTheme.MinTouch - 32f);
            viewport.gameObject.AddComponent<RectMask2D>();
            _itemsRow = UiKit.Rect("Items", viewport);
            _itemsRow.anchorMin = new Vector2(0f, 0f);
            _itemsRow.anchorMax = new Vector2(0f, 1f);
            _itemsRow.pivot = new Vector2(0f, 0.5f);
            var h = _itemsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 14f;
            h.childControlWidth = false;
            h.childControlHeight = true;
            h.childForceExpandHeight = true;
            _itemsRow.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.vertical = false;
            scroll.viewport = viewport;
            scroll.content = _itemsRow;
            foreach (DecorInfo info in HomeCatalog.Decorations)
            {
                _cards.Add((ItemCard(info), info.Id));
            }

            _housePage = Page("HousePage");
            RectTransform rows = Scroller(_housePage);
            foreach ((string label, string[] points, bool optional) in ModuleRows)
            {
                ModuleRow(rows, label, points, optional);
            }

            ColourRow(rows, "Wall colour", HomeCatalog.WallColours, () => _home.Design.wallColour,
                i => _controller.Apply(new SetPaint(i, _home.Design.trimColour, _home.Design.material)));
            ColourRow(rows, "Trim colour", HomeCatalog.TrimColours, () => _home.Design.trimColour,
                i => _controller.Apply(new SetPaint(_home.Design.wallColour, i, _home.Design.material)));
            RectTransform material = Line(rows, "Wall finish");
            TextMeshProUGUI value = Arrows(material,
                () => _controller.Apply(new SetPaint(_home.Design.wallColour, _home.Design.trimColour, (_home.Design.material + HomeCatalog.Materials.Length - 1) % HomeCatalog.Materials.Length)),
                () => _controller.Apply(new SetPaint(_home.Design.wallColour, _home.Design.trimColour, (_home.Design.material + 1) % HomeCatalog.Materials.Length)));
            _refreshers.Add(() => value.text = HomeCatalog.Materials[_home.Design.material]);
        }

        private RectTransform Page(string name)
        {
            RectTransform page = UiKit.Rect(name, _drawer);
            page.anchorMin = Vector2.zero;
            page.anchorMax = Vector2.one;
            page.offsetMin = new Vector2(20f, 16f);
            page.offsetMax = new Vector2(-20f, -UiTheme.MinTouch - 32f);
            return page;
        }

        private static void Place(RectTransform rect, float anchorY, float y)
        {
            rect.anchorMin = new Vector2(0f, anchorY);
            rect.anchorMax = new Vector2(1f, anchorY);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(-24f, UiTheme.MinTouch);
            rect.anchoredPosition = new Vector2(0f, y);
        }

        private Image ItemCard(DecorInfo info)
        {
            RectTransform card = UiKit.Rect(info.Id, _itemsRow);
            card.sizeDelta = new Vector2(CardWidth, 0f);
            Image image = UiKit.Panel(card, UiTheme.IndigoLight);
            image.sprite = UiKit.Rounded;
            image.type = Image.Type.Sliced;
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            string id = info.Id;
            button.onClick.AddListener(() =>
            {
                if (_controller.Armed == id)
                {
                    _controller.Disarm();
                }
                else
                {
                    _controller.Arm(id);
                }
            });

            RectTransform chip = UiKit.Rect("Colour", card);
            chip.anchorMin = new Vector2(0.5f, 1f);
            chip.anchorMax = new Vector2(0.5f, 1f);
            chip.pivot = new Vector2(0.5f, 1f);
            chip.sizeDelta = new Vector2(64f, 64f);
            chip.anchoredPosition = new Vector2(0f, -14f);
            Rgb c = IconColour(info);
            Image dot = UiKit.Panel(chip, new Color(c.R, c.G, c.B), raycast: false);
            dot.sprite = info.Mode == PlacementMode.Path ? UiKit.Rounded : UiKit.Circle;
            RectTransform label = UiKit.Rect("Name", card);
            label.anchorMin = new Vector2(0f, 0f);
            label.anchorMax = new Vector2(1f, 1f);
            label.offsetMin = new Vector2(10f, 6f);
            label.offsetMax = new Vector2(-10f, -84f);
            TextMeshProUGUI text = UiKit.Text(label, info.Name, UiTheme.SmallText, TextAlignmentOptions.Center, UiTheme.OffWhite);
            text.enableAutoSizing = true;
            text.fontSizeMin = 18f;
            text.fontSizeMax = UiTheme.SmallText * UiKit.TextScale;
            text.textWrappingMode = TextWrappingModes.Normal;
            return image;
        }

        private void UpdateShelf()
        {
            Festival festival = _festival.Selected switch
            {
                1 => Festival.Diwali,
                2 => Festival.Tihar,
                3 => Festival.Everyday,
                _ => Festival.None,
            };
            DecorCategory category = (DecorCategory)Mathf.Clamp(_category.Selected, 0, Categories.Length - 1);
            foreach ((Image card, string item) in _cards)
            {
                DecorInfo info = HomeCatalog.Decoration(item);
                card.gameObject.SetActive(info.Category == category && (festival == Festival.None || (info.Festivals & festival) != 0));
            }

            Refresh();
        }

        private void ModuleRow(RectTransform rows, string label, string[] points, bool optional)
        {
            AttachmentPoint point = HomeCatalog.Point(points[0]);
            var choices = new List<string>();
            if (optional)
            {
                choices.Add(string.Empty);
            }

            choices.AddRange(HomeCatalog.Modules.Where(m => m.Type == point.Type).Select(m => m.Id));
            RectTransform line = Line(rows, label);

            void Choose(int step)
            {
                ModuleChoice now = _home.Design.ModuleAt(points[0]);
                int i = Mathf.Max(0, choices.IndexOf(now?.module ?? string.Empty));
                string next = choices[(i + step + choices.Count) % choices.Count];
                ApplyToAll(points, p => new SetModule(p, next, 0));
                Game.Events.Publish(new HouseModuleChangedEvent(points[0], next.Length == 0 ? "none" : next));
            }

            TextMeshProUGUI value = Arrows(line, () => Choose(-1), () => Choose(1), 0.38f, 0.82f);
            RectTransform chipRect = UiKit.Rect("Colour", line);
            chipRect.anchorMin = new Vector2(0.84f, 0.14f);
            chipRect.anchorMax = new Vector2(1f, 0.86f);
            chipRect.offsetMin = chipRect.offsetMax = Vector2.zero;
            Image chip = UiKit.Panel(chipRect, Color.white);
            chip.sprite = UiKit.Rounded;
            var chipButton = chipRect.gameObject.AddComponent<Button>();
            chipButton.targetGraphic = chip;
            chipButton.onClick.AddListener(() =>
            {
                ModuleChoice now = _home.Design.ModuleAt(points[0]);
                ModuleInfo info = HomeCatalog.Module(now?.module);
                if (info != null)
                {
                    int variant = (now.variant + 1) % info.Variants;
                    ApplyToAll(points, p => new SetModule(p, info.Id, variant));
                }
            });
            _refreshers.Add(() =>
            {
                ModuleChoice now = _home.Design.ModuleAt(points[0]);
                ModuleInfo info = HomeCatalog.Module(now?.module);
                value.text = info?.Name ?? "None";
                chipRect.gameObject.SetActive(info != null && info.Variants > 1);
                Rgb c = HousePalette.Accents[Mathf.Clamp(now?.variant ?? 0, 0, HousePalette.Accents.Length - 1)];
                chip.color = new Color(c.R, c.G, c.B);
            });
        }

        private void ApplyToAll(string[] points, Func<string, IHouseCommand> command)
        {
            _controller.Apply(points.Length == 1 ? command(points[0]) : new CommandBatch(points.Select(command).ToArray()));
        }

        private void ColourRow(RectTransform rows, string label, Rgb[] colours, Func<int> get, Action<int> set)
        {
            RectTransform line = UiKit.Rect(label, rows);
            line.sizeDelta = new Vector2(0f, RowHeight + 50f);
            RectTransform text = UiKit.Rect("Label", line);
            text.anchorMin = new Vector2(0f, 0.62f);
            text.anchorMax = new Vector2(1f, 1f);
            text.offsetMin = text.offsetMax = Vector2.zero;
            UiKit.Text(text, label, UiTheme.BodyText, TextAlignmentOptions.Left, UiTheme.OffWhite);
            RectTransform chips = UiKit.Rect("Chips", line);
            chips.anchorMin = new Vector2(0f, 0f);
            chips.anchorMax = new Vector2(1f, 0.6f);
            chips.offsetMin = chips.offsetMax = Vector2.zero;
            var h = chips.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 10f;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = true;
            var outlines = new List<Outline>();
            for (int i = 0; i < colours.Length; i++)
            {
                int index = i;
                RectTransform rect = UiKit.Rect(label == "Wall colour" && i < WallColourNames.Length ? WallColourNames[i] : "Colour " + (i + 1), chips);
                Image chip = UiKit.Panel(rect, new Color(colours[i].R, colours[i].G, colours[i].B));
                chip.sprite = UiKit.Rounded;
                var button = rect.gameObject.AddComponent<Button>();
                button.targetGraphic = chip;
                button.onClick.AddListener(() => set(index));
                Outline outline = rect.gameObject.AddComponent<Outline>();
                outline.effectColor = UiTheme.Marigold;
                outline.effectDistance = new Vector2(5f, 5f);
                outlines.Add(outline);
            }

            _refreshers.Add(() =>
            {
                for (int i = 0; i < outlines.Count; i++)
                {
                    outlines[i].enabled = i == get();
                }
            });
        }

        private void BuildSelectionBar()
        {
            _selectionBar = UiKit.Rect("SelectionBar", _overlay);
            _selectionBar.anchorMin = new Vector2(0f, 0.36f);
            _selectionBar.anchorMax = new Vector2(1f, 0.36f);
            _selectionBar.pivot = new Vector2(0.5f, 0f);
            _selectionBar.sizeDelta = new Vector2(-24f, UiTheme.MinTouch + 24f);
            _selectionBar.anchoredPosition = new Vector2(0f, 12f);
            UiKit.Panel(_selectionBar, new Color(0.07f, 0.05f, 0.18f, 0.9f));
            _uiRects.Add(_selectionBar);
            var row = _selectionBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(12, 12, 12, 12);
            row.spacing = 10f;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = true;
            BarButton(_selectionBar, "Colour", UiTheme.IndigoLight, UiTheme.OffWhite, () => _controller.RecolourSelected());
            BarButton(_selectionBar, "Turn", UiTheme.IndigoLight, UiTheme.OffWhite, () => _controller.RotateSelected());
            BarButton(_selectionBar, "Smaller", UiTheme.IndigoLight, UiTheme.OffWhite, () => _controller.ScaleSelected(0.9f));
            BarButton(_selectionBar, "Bigger", UiTheme.IndigoLight, UiTheme.OffWhite, () => _controller.ScaleSelected(1.1f));
            BarButton(_selectionBar, "Copy", UiTheme.IndigoLight, UiTheme.OffWhite, () => _controller.DuplicateSelected());
            BarButton(_selectionBar, "Delete", UiTheme.Magenta, UiTheme.OffWhite, () => _controller.DeleteSelected());
            _selectionBar.gameObject.SetActive(false);
        }

        private void BuildLayoutsSheet()
        {
            _layoutsSheet = UiKit.Rect("LayoutsSheet", _overlay);
            _layoutsSheet.anchorMin = new Vector2(0.06f, 0.4f);
            _layoutsSheet.anchorMax = new Vector2(0.94f, 0.4f);
            _layoutsSheet.pivot = new Vector2(0.5f, 0f);
            _layoutsSheet.sizeDelta = new Vector2(0f, 3 * (RowHeight + 10f) + 120f);
            _layoutsSheet.anchoredPosition = new Vector2(0f, 20f);
            UiKit.Panel(_layoutsSheet, new Color(0.09f, 0.06f, 0.2f, 0.98f));
            _uiRects.Add(_layoutsSheet);
            RectTransform title = UiKit.Rect("Title", _layoutsSheet);
            title.anchorMin = new Vector2(0f, 1f);
            title.anchorMax = new Vector2(1f, 1f);
            title.pivot = new Vector2(0.5f, 1f);
            title.sizeDelta = new Vector2(-40f, 90f);
            UiKit.Text(title, "Saved layouts (3 free)", UiTheme.BodyText, TextAlignmentOptions.Center, UiTheme.Marigold);
            for (int i = 0; i < HouseState.FreeLayouts; i++)
            {
                int slot = i;
                RectTransform line = UiKit.Rect("Slot" + (i + 1), _layoutsSheet);
                line.anchorMin = new Vector2(0f, 1f);
                line.anchorMax = new Vector2(1f, 1f);
                line.pivot = new Vector2(0.5f, 1f);
                line.sizeDelta = new Vector2(-40f, RowHeight);
                line.anchoredPosition = new Vector2(0f, -100f - i * (RowHeight + 10f));
                var h = line.gameObject.AddComponent<HorizontalLayoutGroup>();
                h.spacing = 10f;
                h.childControlWidth = h.childControlHeight = true;
                h.childForceExpandWidth = h.childForceExpandHeight = true;
                RectTransform nameRect = UiKit.Rect("Name", line);
                TextMeshProUGUI name = UiKit.Text(nameRect, string.Empty, UiTheme.SmallText, TextAlignmentOptions.Left, UiTheme.OffWhite);
                Button save = BarButton(line, "Save", UiTheme.Saffron, UiTheme.Indigo, () => SaveLayout(slot));
                Button load = BarButton(line, "Load", UiTheme.IndigoLight, UiTheme.OffWhite, () => LoadLayout(slot));
                Button delete = BarButton(line, "Delete", UiTheme.IndigoLight, UiTheme.OffWhite, () => DeleteLayout(slot));
                _refreshers.Add(() =>
                {
                    bool used = slot < _home.Editor.State.layouts.Count;
                    name.text = used ? _home.Editor.State.layouts[slot].name : "Empty";
                    save.interactable = slot <= _home.Editor.State.layouts.Count;
                    load.interactable = used;
                    delete.interactable = used;
                });
            }

            _layoutsSheet.gameObject.SetActive(false);
        }

        // ---- Small pieces ----

        private static RectTransform Scroller(RectTransform page)
        {
            page.gameObject.AddComponent<RectMask2D>();
            RectTransform rows = UiKit.Rect("Rows", page);
            rows.anchorMin = new Vector2(0f, 1f);
            rows.anchorMax = new Vector2(1f, 1f);
            rows.pivot = new Vector2(0.5f, 1f);
            rows.sizeDelta = Vector2.zero;
            var layout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            rows.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = page.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.viewport = page;
            scroll.content = rows;
            return rows;
        }

        private static RectTransform Line(RectTransform rows, string label)
        {
            RectTransform line = UiKit.Rect(label, rows);
            line.sizeDelta = new Vector2(0f, RowHeight);
            RectTransform text = UiKit.Rect("Label", line);
            text.anchorMin = new Vector2(0f, 0f);
            text.anchorMax = new Vector2(0.36f, 1f);
            text.offsetMin = text.offsetMax = Vector2.zero;
            TextMeshProUGUI t = UiKit.Text(text, label, UiTheme.SmallText, TextAlignmentOptions.Left, UiTheme.OffWhite);
            t.textWrappingMode = TextWrappingModes.Normal;
            return line;
        }

        private static TextMeshProUGUI Arrows(RectTransform line, Action previous, Action next, float from = 0.38f, float to = 1f)
        {
            RectTransform box = UiKit.Rect("Stepper", line);
            box.anchorMin = new Vector2(from, 0.08f);
            box.anchorMax = new Vector2(to, 0.92f);
            box.offsetMin = box.offsetMax = Vector2.zero;
            UiKit.Panel(box, UiTheme.IndigoLight, raycast: false);
            foreach ((string glyph, float x, Action action) in new[] { ("<", 0f, previous), (">", 1f, next) })
            {
                RectTransform rect = UiKit.Rect(glyph == "<" ? "Previous" : "Next", box);
                rect.anchorMin = new Vector2(x, 0f);
                rect.anchorMax = new Vector2(x, 1f);
                rect.pivot = new Vector2(x, 0.5f);
                rect.sizeDelta = new Vector2(UiTheme.MinTouch * 0.8f, 0f);
                Image image = UiKit.Panel(rect, new Color(1f, 1f, 1f, 0.06f));
                var button = rect.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                button.onClick.AddListener(() => action());
                UiKit.Text(rect, glyph, UiTheme.TitleText, TextAlignmentOptions.Center, UiTheme.Marigold);
            }

            RectTransform valueRect = UiKit.Rect("Value", box);
            UiKit.Stretch(valueRect);
            valueRect.offsetMin = new Vector2(UiTheme.MinTouch * 0.8f, 0f);
            valueRect.offsetMax = new Vector2(-UiTheme.MinTouch * 0.8f, 0f);
            TextMeshProUGUI value = UiKit.Text(valueRect, string.Empty, UiTheme.SmallText, TextAlignmentOptions.Center, UiTheme.OffWhite);
            value.enableAutoSizing = true;
            value.fontSizeMin = 18f;
            value.fontSizeMax = UiTheme.SmallText * UiKit.TextScale;
            return value;
        }

        private static Button BarButton(RectTransform parent, string label, Color background, Color text, Action action)
        {
            RectTransform rect = UiKit.Rect(label, parent);
            Image image = UiKit.Panel(rect, background);
            image.sprite = UiKit.Rounded;
            image.type = Image.Type.Sliced;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            TextMeshProUGUI t = UiKit.Text(rect, label, UiTheme.SmallText, TextAlignmentOptions.Center, text);
            t.enableAutoSizing = true;
            t.fontSizeMin = 18f;
            t.fontSizeMax = UiTheme.SmallText * UiKit.TextScale;
            return button;
        }

        /// <summary>The colour of the paint covering most of an item, for its card.</summary>
        private static Rgb IconColour(DecorInfo info)
        {
            HouseMesh mesh = info.Mode == PlacementMode.Path
                ? DecorGenerator.Path(info.Id, new Core.Avatar.V3(0, 2, 0), new Core.Avatar.V3(1, 2, 0), 0.2f)
                : DecorGenerator.Item(info.Id);
            int best = 0;
            for (int p = 1; p < HouseMesh.PaintCount; p++)
            {
                if (mesh.Triangles[p].Count > mesh.Triangles[best].Count && (HousePaint)p != HousePaint.Iron)
                {
                    best = p;
                }
            }

            return HousePalette.Decor((HousePaint)best, info.Id, 0);
        }
    }
}

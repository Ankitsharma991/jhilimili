using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UtsavNights.Core.Art;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;

namespace UtsavNights.UI
{
    /// <summary>
    /// Avatar creator (F15, PRD 12): a rotating preview, Looks (the presets), Body, Face, Hair and Outfit tabs, Skip always
    /// visible and Save. Edits a draft; Save stores it through <see cref="ProfileService"/>, Skip (or Android back) discards it.
    /// Opens from the person button next to Settings.
    /// </summary>
    public sealed class AvatarCreator : MonoBehaviour
    {
        public const int PreviewWidth = 720, PreviewHeight = 900;
        private const float RowHeight = 128f;

        public static readonly string[] Tabs = { "Looks", "Body", "Face", "Hair", "Outfit" };
        private static readonly string[] FaceShapes = { "Round", "Oval", "Square", "Heart", "Long" };
        private static readonly string[] EyeShapes = { "Almond", "Round", "Narrow", "Hooded" };
        private static readonly string[] EyeColours = { "Dark brown", "Brown", "Hazel", "Green", "Blue", "Grey" };
        private static readonly string[] Noses = { "Medium", "Large", "Small", "Wide" };
        private static readonly string[] Mouths = { "Medium", "Smile", "Small", "Full" };
        private static readonly string[] Brows = { "Medium", "Thick", "Thin", "Arched" };
        private static readonly string[] FacialHair = { "None", "Moustache", "Beard", "Goatee", "Beard and moustache" };
        private static readonly string[] Glasses = { "None", "Round", "Square", "Sunglasses" };
        private static readonly string[] HairStyles = { "Shaved", "Crop", "Side part", "Short curls", "Bob", "Long", "Bun", "Braid", "Big curls", "Ponytail" };
        private static readonly string[] HairColourNames = { "Black", "Dark brown", "Brown", "Light brown", "Grey", "White", "Henna", "Indigo" };

        /// <summary>Outfit rows: which garments each row steps through, and whether it can be empty.</summary>
        private static readonly (string label, Func<GarmentInfo, bool> filter, bool optional)[] OutfitRows =
        {
            ("Top", g => (g.Slots & WardrobeSlot.BaseTop) != 0 && (g.Slots & WardrobeSlot.BaseBottom) == 0, false),
            ("Bottom", g => (g.Slots & WardrobeSlot.BaseBottom) != 0 && (g.Slots & WardrobeSlot.BaseTop) == 0, false),
            ("Full length", g => (g.Slots & WardrobeSlot.BaseTop) != 0 && (g.Slots & WardrobeSlot.BaseBottom) != 0, true),
            ("Mid layer", g => g.Slots == WardrobeSlot.Mid, true),
            ("Outer", g => g.Slots == WardrobeSlot.Outer, true),
            ("Headwear", g => g.Slots == WardrobeSlot.Headwear, true),
            ("Footwear", g => g.Slots == WardrobeSlot.Footwear, true),
        };

        private readonly WardrobeCatalog _catalog = WardrobeCatalog.Default;
        private readonly List<Action> _refreshers = new List<Action>();
        private readonly List<RectTransform> _pages = new List<RectTransform>();
        private RectTransform _overlay;
        private RectTransform _openButton;
        private RawImage _preview;
        private SegmentedView _tabs;
        private TextMeshProUGUI _hairNote;
        private AvatarPreviewStage _stage;
        private ProfileService _profiles;
        private bool _fromPreset;

        public AvatarProfile Draft { get; private set; }

        public bool IsOpen => _overlay != null && _overlay.gameObject.activeSelf;

        public RectTransform OpenButton => _openButton;

        public AvatarPreviewStage Stage => _stage;

        public int Tab => _tabs != null ? _tabs.Selected : -1;

        private void Awake() => Build();

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
                Skip();
            }
        }

        private void Update()
        {
            if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Skip();
            }
        }

        public void Open()
        {
            if (IsOpen || !Game.Services.TryGet(out _profiles))
            {
                return;
            }

            Draft = _profiles.Current.Clone();
            _fromPreset = false;
            Game.Events.Publish(new AvatarCreatorOpenedEvent(!_profiles.Current.created));
            _stage = AvatarPreviewStage.Create(Draft, PreviewWidth, PreviewHeight);
            _preview.texture = _stage.Texture;
            _overlay.gameObject.SetActive(true);
            InputBlockers.Register(_overlay);
            ShowTab(Draft.created ? 1 : 0);
            Refresh();
        }

        /// <summary>Stores the draft as the player's avatar and closes.</summary>
        public void Save()
        {
            if (!IsOpen)
            {
                return;
            }

            _profiles.Save(Draft, _fromPreset);
            CloseOverlay();
        }

        /// <summary>Closes without changing the saved avatar (Skip, Android back).</summary>
        public void Skip()
        {
            if (IsOpen)
            {
                Game.Events.Publish(new AvatarSkippedEvent());
                CloseOverlay();
            }
        }

        public void ShowTab(int index)
        {
            _tabs.Set(index, false);
            for (int i = 0; i < _pages.Count; i++)
            {
                _pages[i].gameObject.SetActive(i == index);
            }
        }

        public void ApplyPreset(int index)
        {
            AvatarProfile preset = AvatarPresets.All[index].Clone();
            preset.name = Draft.name;
            Draft = preset;
            _fromPreset = true;
            Changed();
        }

        /// <summary>Steps the garment in an outfit row (0 Top … 6 Footwear) by +1 or -1, as the arrow buttons do.</summary>
        public void StepGarment(int row, int direction)
        {
            List<GarmentInfo> options = Options(row);
            GarmentInfo current = Worn(row);
            int count = options.Count + (OutfitRows[row].optional ? 1 : 0);
            int index = current != null ? options.IndexOf(current) : options.Count;
            index = ((index + direction) % count + count) % count;
            if (current != null)
            {
                OutfitRules.Unequip(Draft, current.Id);
            }

            if (index < options.Count)
            {
                OutfitRules.Equip(Draft, _catalog, options[index].Id);
            }

            _fromPreset = false;
            Changed();
        }

        public void StepColour(int row, int direction)
        {
            GarmentInfo current = Worn(row);
            EquippedItem item = current != null ? Draft.outfit.Find(e => e.id == current.Id) : null;
            if (item == null)
            {
                return;
            }

            OutfitRules.Equip(Draft, _catalog, item.id, item.variant + direction);
            _fromPreset = false;
            Changed();
        }

        public void ToggleAccessory(string id, bool on)
        {
            if (on)
            {
                OutfitRules.Equip(Draft, _catalog, id);
            }
            else
            {
                OutfitRules.Unequip(Draft, id);
            }

            _fromPreset = false;
            Changed();
        }

        public void Edit(Action<AvatarProfile> change)
        {
            change(Draft);
            _fromPreset = false;
            Changed();
        }

        /// <summary>Sets the optional name. Not a change to the look, so a saved preset still counts as one.</summary>
        public void EditName(string name)
        {
            Draft.name = AvatarProfile.CleanName(name);
            Refresh();
        }

        public string GarmentLabel(int row) => Worn(row)?.Name ?? (OutfitRows[row].optional ? "None" : "—");

        private List<GarmentInfo> Options(int row) => new List<GarmentInfo>(FilterCatalog(OutfitRows[row].filter));

        private IEnumerable<GarmentInfo> FilterCatalog(Func<GarmentInfo, bool> filter)
        {
            foreach (GarmentInfo g in _catalog.All)
            {
                if (filter(g))
                {
                    yield return g;
                }
            }
        }

        private GarmentInfo Worn(int row)
        {
            foreach (EquippedItem item in Draft.outfit)
            {
                GarmentInfo g = _catalog.Find(item.id);
                if (g != null && OutfitRows[row].filter(g))
                {
                    return g;
                }
            }

            return null;
        }

        private void Changed()
        {
            Draft.Sanitize(_catalog);
            _stage.Show(Draft);
            Refresh();
        }

        private void Refresh()
        {
            foreach (Action refresh in _refreshers)
            {
                refresh();
            }

            if (_hairNote != null)
            {
                _hairNote.text = OutfitRules.HairHidden(Draft, _catalog) ? "Hidden under the headwear" : string.Empty;
            }
        }

        private void CloseOverlay()
        {
            _overlay.gameObject.SetActive(false);
            InputBlockers.Unregister(_overlay);
            _preview.texture = null;
            if (_stage != null)
            {
                Destroy(_stage.gameObject);
                _stage = null;
            }
        }

        private void Build()
        {
            // The person button, left of the Settings button in the top-right corner.
            _openButton = UiKit.Rect("AvatarButton", transform);
            _openButton.anchorMin = _openButton.anchorMax = _openButton.pivot = new Vector2(1f, 1f);
            _openButton.sizeDelta = new Vector2(UiTheme.MinTouch, UiTheme.MinTouch);
            _openButton.anchoredPosition = new Vector2(-24f - UiTheme.MinTouch - 20f, -24f);
            Image bg = UiKit.Panel(_openButton, UiTheme.Indigo);
            bg.sprite = UiKit.Circle;
            var open = _openButton.gameObject.AddComponent<Button>();
            open.targetGraphic = bg;
            open.onClick.AddListener(Open);
            RectTransform head = UiKit.Rect("Head", _openButton);
            head.sizeDelta = new Vector2(38f, 38f);
            head.anchoredPosition = new Vector2(0f, 18f);
            UiKit.Panel(head, UiTheme.OffWhite, raycast: false).sprite = UiKit.Circle;
            RectTransform shoulders = UiKit.Rect("Shoulders", _openButton);
            shoulders.sizeDelta = new Vector2(68f, 34f);
            shoulders.anchoredPosition = new Vector2(0f, -26f);
            UiKit.Panel(shoulders, UiTheme.OffWhite, raycast: false).sprite = UiKit.Rounded;

            _overlay = UiKit.Rect("AvatarOverlay", transform.parent != null ? transform.parent : transform);
            UiKit.Stretch(_overlay);
            UiKit.Panel(_overlay, new Color(0.09f, 0.06f, 0.2f, 0.99f));

            // Top bar: Skip (always visible), title, Save.
            Button(_overlay, "Skip", new Vector2(0f, 1f), new Vector2(28f, -20f), 220f, UiTheme.IndigoLight, UiTheme.OffWhite, Skip);
            Button(_overlay, "Save", new Vector2(1f, 1f), new Vector2(-28f, -20f), 220f, UiTheme.Saffron, UiTheme.Indigo, Save);
            RectTransform title = UiKit.Rect("Title", _overlay);
            title.anchorMin = new Vector2(0.25f, 1f);
            title.anchorMax = new Vector2(0.75f, 1f);
            title.pivot = new Vector2(0.5f, 1f);
            title.sizeDelta = new Vector2(0f, UiTheme.MinTouch);
            title.anchoredPosition = new Vector2(0f, -20f);
            UiKit.Text(title, "Your avatar", UiTheme.TitleText, TextAlignmentOptions.Center, UiTheme.Marigold);

            // Preview: drag sideways to turn the avatar.
            RectTransform preview = UiKit.Rect("Preview", _overlay);
            preview.anchorMin = new Vector2(0f, 0.5f);
            preview.anchorMax = new Vector2(1f, 1f);
            preview.offsetMin = new Vector2(16f, 8f);
            preview.offsetMax = new Vector2(-16f, -170f);
            _preview = preview.gameObject.AddComponent<RawImage>();
            preview.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            preview.GetComponent<AspectRatioFitter>().aspectRatio = PreviewWidth / (float)PreviewHeight;
            preview.gameObject.AddComponent<DragToTurn>().Turned += degrees => _stage?.Rotate(degrees);

            _tabs = SegmentedView.Create(_overlay, Tabs);
            var tabsRect = (RectTransform)_tabs.transform;
            tabsRect.anchorMin = new Vector2(0f, 0.5f);
            tabsRect.anchorMax = new Vector2(1f, 0.5f);
            tabsRect.pivot = new Vector2(0.5f, 1f);
            tabsRect.sizeDelta = new Vector2(-48f, UiTheme.MinTouch);
            tabsRect.anchoredPosition = Vector2.zero;
            _tabs.Changed += ShowTab;

            for (int i = 0; i < Tabs.Length; i++)
            {
                _pages.Add(Page(Tabs[i]));
            }

            BuildLooks(_pages[0]);
            BuildBody(_pages[1]);
            BuildFace(_pages[2]);
            BuildHair(_pages[3]);
            BuildOutfit(_pages[4]);
            _overlay.gameObject.SetActive(false);
        }

        private RectTransform Page(string name)
        {
            RectTransform viewport = UiKit.Rect(name + "Page", _overlay);
            viewport.anchorMin = new Vector2(0f, 0f);
            viewport.anchorMax = new Vector2(1f, 0.5f);
            viewport.offsetMin = new Vector2(32f, 24f);
            viewport.offsetMax = new Vector2(-32f, -UiTheme.MinTouch - 16f);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform rows = UiKit.Rect("Rows", viewport);
            rows.anchorMin = new Vector2(0f, 1f);
            rows.anchorMax = new Vector2(1f, 1f);
            rows.pivot = new Vector2(0.5f, 1f);
            rows.sizeDelta = Vector2.zero;
            var layout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            rows.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.viewport = viewport;
            scroll.content = rows;
            return rows;
        }

        private void BuildLooks(RectTransform rows)
        {
            for (int start = 0; start < AvatarPresets.All.Count; start += 4)
            {
                RectTransform line = UiKit.Rect("Looks", rows);
                line.sizeDelta = new Vector2(0f, RowHeight + 20f);
                var h = line.gameObject.AddComponent<HorizontalLayoutGroup>();
                h.spacing = 12f;
                h.childControlWidth = true;
                h.childForceExpandWidth = true;
                h.childControlHeight = true;
                for (int i = start; i < Math.Min(start + 4, AvatarPresets.All.Count); i++)
                {
                    int index = i;
                    RectTransform cell = UiKit.Rect("Look" + (i + 1), line);
                    Rgb outfitColour = GarmentLooks.Main(AvatarPresets.All[i].outfit[0].id, AvatarPresets.All[i].outfit[0].variant);
                    Image image = UiKit.Panel(cell, new Color(outfitColour.R, outfitColour.G, outfitColour.B));
                    var button = cell.gameObject.AddComponent<Button>();
                    button.targetGraphic = image;
                    button.onClick.AddListener(() => ApplyPreset(index));
                    UiKit.Text(cell, "Look " + (i + 1), UiTheme.SmallText, TextAlignmentOptions.Center, UiTheme.OffWhite);
                }
            }
        }

        private void BuildBody(RectTransform rows)
        {
            NameField(rows);
            SliderRow(rows, "Body", 0f, 2f, () => Draft.bodyStyle, v => Edit(p => p.bodyStyle = v));
            Segmented(rows, "Height", new[] { "Short", "Medium", "Tall" }, () => (int)Draft.height, i => Edit(p => p.height = (HeightPreset)i));
            SliderRow(rows, "Skin tone", 0f, 1f, () => Draft.skinTone, v => Edit(p => p.skinTone = v), skinRamp: true);
            SliderRow(rows, "Shoulders", -1f, 1f, () => Draft.shoulders, v => Edit(p => p.shoulders = v));
            SliderRow(rows, "Hips", -1f, 1f, () => Draft.hips, v => Edit(p => p.hips = v));
            SliderRow(rows, "Leg length", -1f, 1f, () => Draft.legLength, v => Edit(p => p.legLength = v));
            SliderRow(rows, "Head size", -1f, 1f, () => Draft.headSize, v => Edit(p => p.headSize = v));
            Segmented(rows, "Moves", new[] { "Calm", "Lively" }, () => (int)Draft.animationStyle, i => Edit(p => p.animationStyle = (AnimationStyle)i));
        }

        private void BuildFace(RectTransform rows)
        {
            Stepper(rows, "Face", FaceShapes, () => Draft.faceShape, v => Edit(p => p.faceShape = v));
            Stepper(rows, "Eyes", EyeShapes, () => Draft.eyes, v => Edit(p => p.eyes = v));
            Stepper(rows, "Eye colour", EyeColours, () => Draft.eyeColour, v => Edit(p => p.eyeColour = v));
            Stepper(rows, "Nose", Noses, () => Draft.nose, v => Edit(p => p.nose = v));
            Stepper(rows, "Mouth", Mouths, () => Draft.mouth, v => Edit(p => p.mouth = v));
            Stepper(rows, "Brows", Brows, () => Draft.brows, v => Edit(p => p.brows = v));
            Stepper(rows, "Facial hair", FacialHair, () => Draft.facialHair, v => Edit(p => p.facialHair = v));
            Stepper(rows, "Glasses", Glasses, () => Draft.glasses, v => Edit(p => p.glasses = v));
        }

        private void BuildHair(RectTransform rows)
        {
            Stepper(rows, "Style", HairStyles, () => Draft.hairStyle, v => Edit(p => p.hairStyle = v));
            Stepper(rows, "Colour", HairColourNames, () => Draft.hairColour, v => Edit(p => p.hairColour = v));
            RectTransform note = UiKit.Rect("Note", rows);
            note.sizeDelta = new Vector2(0f, 60f);
            _hairNote = UiKit.Text(note, string.Empty, UiTheme.SmallText, TextAlignmentOptions.Left, UiTheme.Muted);
        }

        private void BuildOutfit(RectTransform rows)
        {
            for (int r = 0; r < OutfitRows.Length; r++)
            {
                int row = r;
                RectTransform line = Line(rows, OutfitRows[r].label);
                TextMeshProUGUI value = Arrows(line, () => StepGarment(row, -1), () => StepGarment(row, 1), 0.3f, 0.78f);
                Swatch(line, () => StepColour(row, 1), () => Worn(row));
                _refreshers.Add(() => value.text = GarmentLabel(row));
            }

            RectTransform title = UiKit.Rect("Accessories", rows);
            title.sizeDelta = new Vector2(0f, 70f);
            UiKit.Text(title, "Accessories (up to 3)", UiTheme.BodyText, TextAlignmentOptions.BottomLeft, UiTheme.Marigold);
            foreach (GarmentInfo g in _catalog.All)
            {
                if (g.Slots != WardrobeSlot.Accessory)
                {
                    continue;
                }

                string id = g.Id;
                RectTransform line = Line(rows, g.Name);
                SwitchView toggle = SwitchView.Create(line);
                var rect = (RectTransform)toggle.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                toggle.Changed += on => ToggleAccessory(id, on);
                _refreshers.Add(() => toggle.Set(Draft.outfit.Exists(e => e.id == id), false));
            }
        }

        private void NameField(RectTransform rows)
        {
            RectTransform line = Line(rows, "Name");
            RectTransform box = UiKit.Rect("NameInput", line);
            box.anchorMin = new Vector2(0.3f, 0.1f);
            box.anchorMax = new Vector2(1f, 0.9f);
            box.offsetMin = box.offsetMax = Vector2.zero;
            Image bg = UiKit.Panel(box, UiTheme.IndigoLight);
            RectTransform area = UiKit.Rect("TextArea", box);
            UiKit.Stretch(area, 16f);
            area.gameObject.AddComponent<RectMask2D>();
            TextMeshProUGUI placeholder = UiKit.Text(area, "Optional, stays on this phone", UiTheme.SmallText, TextAlignmentOptions.Left, UiTheme.Muted);
            TextMeshProUGUI text = UiKit.Text(area, string.Empty, UiTheme.BodyText, TextAlignmentOptions.Left, UiTheme.OffWhite);
            var input = box.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = bg;
            input.textViewport = area;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.characterLimit = AvatarProfile.MaxNameLength;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.onEndEdit.AddListener(EditName);
            _refreshers.Add(() => input.SetTextWithoutNotify(Draft.name));
        }

        private void SliderRow(RectTransform rows, string label, float min, float max, Func<float> get, Action<float> set, bool skinRamp = false)
        {
            RectTransform line = Line(rows, label);
            Slider slider = SliderFactory.Create(line);
            var rect = (RectTransform)slider.transform;
            rect.anchorMin = new Vector2(0.3f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            slider.minValue = min;
            slider.maxValue = max;
            if (skinRamp)
            {
                // The track shows the skin-tone ramp itself.
                var ramp = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int x = 0; x < 64; x++)
                {
                    Rgb c = SkinTones.Evaluate(x / 63f);
                    ramp.SetPixel(x, 0, new Color(c.R, c.G, c.B));
                }

                ramp.Apply();
                Image track = slider.transform.GetComponentInChildren<Image>();
                if (track != null)
                {
                    track.sprite = Sprite.Create(ramp, new Rect(0, 0, 64, 1), new Vector2(0.5f, 0.5f));
                    track.color = Color.white;
                }
            }

            slider.onValueChanged.AddListener(v => set(v));
            _refreshers.Add(() => slider.SetValueWithoutNotify(get()));
        }

        private void Segmented(RectTransform rows, string label, string[] options, Func<int> get, Action<int> set)
        {
            RectTransform line = Line(rows, label);
            SegmentedView seg = SegmentedView.Create(line, options);
            var rect = (RectTransform)seg.transform;
            rect.anchorMin = new Vector2(0.3f, 0.1f);
            rect.anchorMax = new Vector2(1f, 0.9f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            seg.Changed += set;
            _refreshers.Add(() => seg.Set(get(), false));
        }

        private void Stepper(RectTransform rows, string label, string[] names, Func<int> get, Action<int> set)
        {
            RectTransform line = Line(rows, label);
            TextMeshProUGUI value = Arrows(line, () => set((get() - 1 + names.Length) % names.Length), () => set((get() + 1) % names.Length), 0.3f, 1f);
            _refreshers.Add(() => value.text = names[Mathf.Clamp(get(), 0, names.Length - 1)]);
        }

        private RectTransform Line(RectTransform rows, string label)
        {
            RectTransform line = UiKit.Rect(label, rows);
            line.sizeDelta = new Vector2(0f, RowHeight);
            RectTransform text = UiKit.Rect("Label", line);
            text.anchorMin = new Vector2(0f, 0f);
            text.anchorMax = new Vector2(0.3f, 1f);
            text.offsetMin = text.offsetMax = Vector2.zero;
            UiKit.Text(text, label, UiTheme.BodyText, TextAlignmentOptions.Left, UiTheme.OffWhite);
            return line;
        }

        /// <summary>"&lt; value &gt;" between two anchors of the row; returns the value text.</summary>
        private TextMeshProUGUI Arrows(RectTransform line, Action previous, Action next, float from, float to)
        {
            RectTransform box = UiKit.Rect("Stepper", line);
            box.anchorMin = new Vector2(from, 0.08f);
            box.anchorMax = new Vector2(to, 0.92f);
            box.offsetMin = box.offsetMax = Vector2.zero;
            UiKit.Panel(box, UiTheme.IndigoLight, raycast: false);
            ArrowButton(box, "<", new Vector2(0f, 0.5f), previous);
            ArrowButton(box, ">", new Vector2(1f, 0.5f), next);
            RectTransform valueRect = UiKit.Rect("Value", box);
            UiKit.Stretch(valueRect);
            valueRect.offsetMin = new Vector2(UiTheme.MinTouch, 0f);
            valueRect.offsetMax = new Vector2(-UiTheme.MinTouch, 0f);
            TextMeshProUGUI value = UiKit.Text(valueRect, string.Empty, UiTheme.SmallText, TextAlignmentOptions.Center, UiTheme.OffWhite);
            value.enableAutoSizing = true;
            value.fontSizeMin = 20f;
            value.fontSizeMax = UiTheme.SmallText * UiKit.TextScale;
            return value;
        }

        private void ArrowButton(RectTransform box, string glyph, Vector2 anchor, Action action)
        {
            RectTransform rect = UiKit.Rect(glyph == "<" ? "Previous" : "Next", box);
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.sizeDelta = new Vector2(UiTheme.MinTouch, 0f);
            rect.anchorMin = new Vector2(anchor.x, 0f);
            rect.anchorMax = new Vector2(anchor.x, 1f);
            Image image = UiKit.Panel(rect, new Color(1f, 1f, 1f, 0.06f));
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            UiKit.Text(rect, glyph, UiTheme.TitleText, TextAlignmentOptions.Center, UiTheme.Marigold);
        }

        /// <summary>A colour chip at the end of an outfit row; tap for the next colour.</summary>
        private void Swatch(RectTransform line, Action next, Func<GarmentInfo> worn)
        {
            RectTransform rect = UiKit.Rect("Colour", line);
            rect.anchorMin = new Vector2(0.8f, 0.12f);
            rect.anchorMax = new Vector2(1f, 0.88f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Image chip = UiKit.Panel(rect, Color.white);
            chip.sprite = UiKit.Rounded;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = chip;
            button.onClick.AddListener(() => next());
            _refreshers.Add(() =>
            {
                GarmentInfo g = worn();
                EquippedItem item = g != null ? Draft.outfit.Find(e => e.id == g.Id) : null;
                rect.gameObject.SetActive(item != null);
                if (item != null)
                {
                    Rgb c = GarmentLooks.Main(item.id, item.variant);
                    chip.color = new Color(c.R, c.G, c.B);
                }
            });
        }

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

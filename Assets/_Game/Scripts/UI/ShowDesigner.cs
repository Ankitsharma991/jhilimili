using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UtsavNights.Content;
using UtsavNights.Core.Neighbourhood;
using UtsavNights.Core.Runtime;
using UtsavNights.Core.Shows;
using UtsavNights.Gameplay;

namespace UtsavNights.UI
{
    /// <summary>
    /// The show designer (Phase 9 step 5, PRD 12 "Finale designer: timeline strip, drag items onto launch pads, delay
    /// controls, preview, save"; F22). The lower part of the screen holds the pad map, the cracker palette and the timeline;
    /// the upper part keeps the celebration spot in view. Preview hides the panel and plays the show there.
    /// </summary>
    public sealed class ShowDesigner : MonoBehaviour
    {
        private const float PixelsPerSecond = 60f;
        private const float LaneHeight = 44f;
        private const float RefreshSeconds = 0.2f;

        private readonly List<RectTransform> _padViews = new List<RectTransform>();
        private readonly List<GameObject> _built = new List<GameObject>();
        private RectTransform _launcher;
        private RectTransform _overlay;
        private RectTransform _panel;
        private RectTransform _padMap;
        private RectTransform _lanes;
        private RectTransform _palette;
        private RectTransform _list;
        private RectTransform _listRows;
        private RectTransform _playBar;
        private TextMeshProUGUI _status;
        private TextMeshProUGUI _cueLabel;
        private TextMeshProUGUI _playLabel;
        private TMP_InputField _name;
        private ShowPlayer _player;
        private TrayView _tray;
        private float _nextRefresh;

        public bool IsOpen => _overlay != null && _overlay.gameObject.activeSelf;

        public bool ListOpen => _list != null && _list.gameObject.activeSelf;

        public bool Previewing => _player != null && _player.State == ShowPlayer.PlayerState.Running && IsOpen;

        /// <summary>The show being edited (a copy; saved shows change only on Save).</summary>
        public ShowDef Draft { get; private set; }

        public int SelectedPad { get; private set; }

        public ShowCue SelectedCue { get; private set; }

        public string Status => _status != null ? _status.text : string.Empty;

        /// <summary>Cracker ids in the palette: the catalog's crackers that are unlocked now.</summary>
        public List<string> Palette { get; } = new List<string>();

        public RectTransform Launcher => _launcher;

        public RectTransform PadView(int pad) => pad >= 0 && pad < _padViews.Count ? _padViews[pad] : null;

        private void Start()
        {
            Game.Services.TryGet(out _player);
            _tray = FindFirstObjectByType<TrayView>();
            Build();
            if (_player != null)
            {
                _player.StateChanged += OnPlayerState;
            }

            InputBlockers.Register(_launcher);
        }

        private void OnDestroy()
        {
            if (_player != null)
            {
                _player.StateChanged -= OnPlayerState;
            }

            InputBlockers.Unregister(_launcher);
            InputBlockers.Unregister(_panel);
            InputBlockers.Unregister(_playBar);
        }

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (Previewing)
                {
                    StopPreview();
                }
                else if (ListOpen)
                {
                    CloseList();
                }
                else
                {
                    Close();
                }
            }

            if (Previewing && Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + RefreshSeconds;
                _playLabel.text = $"{_player.Last?.name}  ·  {Mathf.Max(0f, _player.Elapsed):0.0} s  ·  {_player.Lit}/{_player.Last?.cues.Count} lit";
            }
        }

        public void Open()
        {
            if (_player == null)
            {
                return;
            }

            if (Draft == null)
            {
                Load(ShowTemplates.All[0], asCopy: true);
            }

            FillPalette();
            _overlay.gameObject.SetActive(true);
            _overlay.SetAsLastSibling();
            _launcher.gameObject.SetActive(false);
            ShowPanel(true);
            Refresh();
        }

        public void Close()
        {
            StopPreview();
            CloseList();
            InputBlockers.Unregister(_panel);
            InputBlockers.Unregister(_playBar);
            _overlay.gameObject.SetActive(false);
            _launcher.gameObject.SetActive(true);
        }

        /// <summary>Starts editing a show. Templates and pack shows come in as a new show of the player's own (a new id).</summary>
        public void Load(ShowDef source, bool asCopy)
        {
            Draft = source.Clone();
            if (asCopy)
            {
                Draft.id = _player.Library.NewId();
            }

            SelectedPad = 0;
            SelectedCue = null;
            Refresh();
        }

        public void New() => Load(ShowTemplates.Blank(string.Empty, "My show", 4), asCopy: true);

        public void SelectPad(int pad)
        {
            SelectedPad = Mathf.Clamp(pad, 0, Draft.pads.Count - 1);
            Refresh();
        }

        public void SelectCue(ShowCue cue)
        {
            SelectedCue = cue;
            if (cue != null)
            {
                SelectedPad = cue.pad;
            }

            Refresh();
        }

        /// <summary>Adds a cracker to the selected pad (a tap on a palette chip).</summary>
        public ShowCue AddCracker(string cracker) => Drop(cracker, SelectedPad);

        /// <summary>A cracker dropped on a pad: it lights at the pad's next free moment.</summary>
        public ShowCue Drop(string cracker, int pad)
        {
            ShowCue cue = ShowEditing.Add(Draft, cracker, pad, _player.Timing);
            if (cue == null)
            {
                SetMessage(Draft.cues.Count >= ShowRules.MaxCues ? $"A show holds up to {ShowRules.MaxCues} crackers." : "Choose a launch pad first.");
                return null;
            }

            SelectedPad = pad;
            SelectedCue = cue;
            Refresh();
            return cue;
        }

        /// <summary>The delay controls: move the selected cracker earlier or later.</summary>
        public void Nudge(float seconds)
        {
            if (SelectedCue == null)
            {
                return;
            }

            ShowRules.Shift(SelectedCue, seconds);
            Refresh();
        }

        public void RemoveSelected()
        {
            if (SelectedCue == null)
            {
                return;
            }

            Draft.cues.Remove(SelectedCue);
            SelectedCue = null;
            Refresh();
        }

        public void AddPad()
        {
            if (!ShowEditing.AddPad(Draft))
            {
                SetMessage($"Up to {ShowRules.MaxPads} launch pads.");
                return;
            }

            SelectedPad = Draft.pads.Count - 1;
            Refresh();
        }

        public void RemovePad()
        {
            if (!ShowEditing.RemovePad(Draft))
            {
                return;
            }

            if (SelectedCue != null && !Draft.cues.Contains(SelectedCue))
            {
                SelectedCue = null;
            }

            SelectedPad = Mathf.Min(SelectedPad, Draft.pads.Count - 1);
            Refresh();
        }

        public void Rename(string name)
        {
            Draft.name = (name ?? string.Empty).Trim();
            Refresh();
        }

        /// <summary>Every reason the show can't play here now; empty when it can.</summary>
        public List<string> Problems() => _player != null && Draft != null ? _player.Check(Draft) : new List<string> { "no show" };

        public bool Save()
        {
            if (string.IsNullOrWhiteSpace(Draft.name))
            {
                SetMessage("Give the show a name first.");
                return false;
            }

            if (!_player.Save(Draft))
            {
                SetMessage(_player.Library.Find(Draft.id) == null && _player.Library.shows.Count >= ShowRules.MaxSavedShows
                    ? $"You have {ShowRules.MaxSavedShows} saved shows. Delete one to save this."
                    : "Couldn't save the show.");
                return false;
            }

            SetMessage($"Saved \"{Draft.name}\".");
            return true;
        }

        /// <summary>Plays the draft at the spot; the panel steps aside so the show can be watched.</summary>
        public bool Preview()
        {
            List<string> problems = Problems();
            if (problems.Count > 0)
            {
                SetMessage(problems[0]);
                return false;
            }

            if (!_player.Play(Draft))
            {
                SetMessage("The show couldn't start.");
                return false;
            }

            ShowPanel(false);
            return true;
        }

        /// <summary>
        /// Plays the show and records it (F22 "export clip"): 10 to 20 seconds, the show's length plus a moment. The clip's
        /// share sheet opens when it is ready.
        /// </summary>
        public bool RecordClip()
        {
            if (!Game.Services.TryGet(out ClipRecorder recorder) || recorder.State != ClipRecorder.RecorderState.Idle)
            {
                SetMessage("A clip is already recording.");
                return false;
            }

            if (!Preview())
            {
                return false;
            }

            if (!recorder.StartRecording(Core.Clips.ClipRules.ShowClipSeconds(Draft.Length(_player.Timing)), "show"))
            {
                StopPreview();
                SetMessage(recorder.Last.Error);
                return false;
            }

            return true;
        }

        public void StopPreview()
        {
            if (_player != null && _player.State == ShowPlayer.PlayerState.Running)
            {
                _player.Stop();
            }
        }

        /// <summary>The shows list: "New show", the templates (and shows packs), then the player's saved shows.</summary>
        public List<(string label, ShowDef show, bool saved)> Choices()
        {
            var list = new List<(string, ShowDef, bool)>();
            foreach (ShowDef t in _player.Templates())
            {
                list.Add(($"Start from: {t.name}", t, false));
            }

            foreach (ShowDef s in _player.Library.shows)
            {
                list.Add((s.name, s, true));
            }

            return list;
        }

        public void OpenList()
        {
            foreach (Transform child in _listRows)
            {
                Destroy(child.gameObject);
            }

            ListRow("New show", UiTheme.Saffron, UiTheme.Indigo, () =>
            {
                New();
                CloseList();
            }, null);
            foreach ((string label, ShowDef show, bool saved) in Choices())
            {
                ShowDef captured = show;
                ListRow(label, UiTheme.IndigoLight, UiTheme.OffWhite, () =>
                {
                    Load(captured, asCopy: !saved);
                    CloseList();
                }, saved ? () =>
                {
                    _player.Delete(captured.id);
                    OpenList();
                } : (Action)null);
            }

            _list.gameObject.SetActive(true);
            _list.SetAsLastSibling();
        }

        public void CloseList()
        {
            if (_list != null)
            {
                _list.gameObject.SetActive(false);
            }
        }

        private void OnPlayerState(ShowPlayer.PlayerState state)
        {
            if (state == ShowPlayer.PlayerState.Idle && IsOpen)
            {
                ShowPanel(true);
                Refresh();
            }
        }

        private void ShowPanel(bool editing)
        {
            _panel.gameObject.SetActive(editing);
            _playBar.gameObject.SetActive(!editing);
            if (editing)
            {
                InputBlockers.Unregister(_playBar);
                InputBlockers.Register(_panel);
            }
            else
            {
                InputBlockers.Unregister(_panel);
                InputBlockers.Register(_playBar);
            }
        }

        private void FillPalette()
        {
            foreach (Transform child in _palette)
            {
                Destroy(child.gameObject);
            }

            Palette.Clear();
            CrackerCatalog catalog = _tray != null ? _tray.Catalog : null;
            if (catalog == null)
            {
                return;
            }

            Game.Services.TryGet(out ProgressService progress);
            foreach (CrackerCatalog.Entry entry in catalog.Entries)
            {
                CrackerDefinition d = entry?.definition;
                if (d == null || (entry.locked && (progress == null || !progress.IsUnlocked(d.Id, true))))
                {
                    continue;
                }

                Palette.Add(d.Id);
                RectTransform chip = UiKit.Rect(d.Id, _palette);
                Image bg = UiKit.Panel(chip, UiTheme.IndigoLight);
                if (d.TrayIcon != null)
                {
                    RectTransform iconRect = UiKit.Rect("Icon", chip);
                    iconRect.anchorMin = new Vector2(0.2f, 0.32f);
                    iconRect.anchorMax = new Vector2(0.8f, 0.95f);
                    iconRect.offsetMin = iconRect.offsetMax = Vector2.zero;
                    var icon = iconRect.gameObject.AddComponent<Image>();
                    icon.sprite = d.TrayIcon;
                    icon.preserveAspect = true;
                    icon.raycastTarget = false;
                }

                RectTransform labelRect = UiKit.Rect("Label", chip);
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = new Vector2(1f, 0.34f);
                labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
                TextMeshProUGUI label = UiKit.Text(labelRect, d.DisplayName.en, 20f, TextAlignmentOptions.Center, UiTheme.OffWhite);
                label.enableAutoSizing = true;
                label.fontSizeMin = 14f;
                label.fontSizeMax = 20f;
                var button = chip.gameObject.AddComponent<Button>();
                button.targetGraphic = bg;
                string id = d.Id;
                button.onClick.AddListener(() => AddCracker(id));
                var drag = chip.gameObject.AddComponent<ShowChipDrag>();
                drag.Cracker = id;
                drag.Dropped = OnChipDropped;
            }
        }

        private void OnChipDropped(string cracker, Vector2 screen)
        {
            for (int i = 0; i < _padViews.Count; i++)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(_padViews[i], screen, null))
                {
                    Drop(cracker, i);
                    return;
                }
            }

            // A drop on a timeline lane counts as its pad.
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_lanes, screen, null, out Vector2 local) && _lanes.rect.Contains(local))
            {
                int lane = Mathf.FloorToInt(-local.y / LaneHeight);
                if (lane >= 0 && lane < Draft.pads.Count)
                {
                    Drop(cracker, lane);
                    return;
                }
            }

            SetMessage("Drop the cracker on a launch pad.");
        }

        private void Refresh()
        {
            if (_panel == null || Draft == null)
            {
                return;
            }

            foreach (GameObject go in _built)
            {
                Destroy(go);
            }

            _built.Clear();
            _padViews.Clear();
            if (_name != null)
            {
                _name.SetTextWithoutNotify(Draft.name);
            }

            BuildPadMap();
            BuildTimeline();
            _cueLabel.text = SelectedCue != null
                ? $"{CrackerName(SelectedCue.cracker)} on pad {SelectedCue.pad + 1} at {SelectedCue.at:0.0} s"
                : $"Pad {SelectedPad + 1}: tap or drag a cracker onto it";
            List<string> problems = Problems();
            SpotDef spot = ShowPlayer.CurrentSpot();
            _status.text = problems.Count > 0
                ? problems[0]
                : $"Ready at {(string.IsNullOrEmpty(spot.name) ? "your courtyard" : spot.name)}: {Draft.cues.Count} crackers, {Draft.Length(_player.Timing):0} s";
            _status.color = problems.Count > 0 ? UiTheme.Marigold : UiTheme.OffWhite;
        }

        private void SetMessage(string message)
        {
            Refresh();
            _status.text = message;
            _status.color = UiTheme.Marigold;
        }

        private string CrackerName(string id)
        {
            CrackerDefinition d = _tray != null && _tray.Catalog != null ? _tray.Catalog.FindById(id) : null;
            return d != null ? d.DisplayName.en : id;
        }

        private void BuildPadMap()
        {
            SpotDef spot = ShowPlayer.CurrentSpot();
            float size = Mathf.Min(_padMap.rect.width, _padMap.rect.height);
            if (size <= 1f)
            {
                size = 360f;
            }

            // The spot's area as a circle, pads to scale; the camera is below the map.
            float radius = Mathf.Max(spot.radius, 1.2f);
            float scale = size * 0.46f / radius;
            RectTransform area = UiKit.Rect("Area", _padMap);
            area.anchorMin = area.anchorMax = area.pivot = new Vector2(0.5f, 0.5f);
            area.sizeDelta = Vector2.one * (spot.radius * 2f * scale);
            Image areaImage = UiKit.Panel(area, new Color(1f, 0.76f, 0.16f, 0.12f), raycast: false);
            areaImage.sprite = UiKit.Circle;
            _built.Add(area.gameObject);
            for (int i = 0; i < Draft.pads.Count; i++)
            {
                ShowPad pad = Draft.pads[i];
                RectTransform view = UiKit.Rect($"Pad{i + 1}", _padMap);
                view.anchorMin = view.anchorMax = view.pivot = new Vector2(0.5f, 0.5f);
                view.sizeDelta = new Vector2(76f, 76f);
                view.anchoredPosition = new Vector2(pad.dx, pad.dz) * scale;
                bool selected = i == SelectedPad;
                Image image = UiKit.Panel(view, selected ? UiTheme.Saffron : UiTheme.IndigoLight);
                image.sprite = UiKit.Circle;
                int count = Draft.cues.FindAll(c => c.pad == i).Count;
                UiKit.Text(view, count > 0 ? $"{i + 1}\n<size=60%>{count}</size>" : $"{i + 1}", 26f, TextAlignmentOptions.Center, selected ? UiTheme.Indigo : UiTheme.OffWhite);
                var button = view.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                int index = i;
                button.onClick.AddListener(() => SelectPad(index));
                _padViews.Add(view);
                _built.Add(view.gameObject);
            }
        }

        private void BuildTimeline()
        {
            float length = Mathf.Max(20f, Draft.Length(_player.Timing) + 5f);
            _lanes.sizeDelta = new Vector2(length * PixelsPerSecond + 80f, Draft.pads.Count * LaneHeight + 30f);
            for (int s = 0; s <= length; s += 5)
            {
                RectTransform tick = UiKit.Rect($"T{s}", _lanes);
                tick.anchorMin = tick.anchorMax = tick.pivot = new Vector2(0f, 1f);
                tick.sizeDelta = new Vector2(70f, 26f);
                tick.anchoredPosition = new Vector2(s * PixelsPerSecond, -Draft.pads.Count * LaneHeight - 2f);
                UiKit.Text(tick, $"{s} s", 20f, TextAlignmentOptions.Left, UiTheme.Muted);
                _built.Add(tick.gameObject);
            }

            for (int i = 0; i < Draft.pads.Count; i++)
            {
                RectTransform lane = UiKit.Rect($"Lane{i + 1}", _lanes);
                lane.anchorMin = new Vector2(0f, 1f);
                lane.anchorMax = new Vector2(1f, 1f);
                lane.pivot = new Vector2(0f, 1f);
                lane.sizeDelta = new Vector2(0f, LaneHeight - 6f);
                lane.anchoredPosition = new Vector2(0f, -i * LaneHeight);
                UiKit.Panel(lane, i == SelectedPad ? new Color(1f, 0.6f, 0.2f, 0.16f) : new Color(1f, 1f, 1f, 0.05f), raycast: false);
                _built.Add(lane.gameObject);
            }

            foreach (ShowCue cue in Draft.cues)
            {
                CrackerTiming? t = _player.Timing(cue.cracker);
                float span = t != null ? t.Value.Fuse + t.Value.Show : 2f;
                RectTransform block = UiKit.Rect(cue.cracker, _lanes);
                block.anchorMin = block.anchorMax = block.pivot = new Vector2(0f, 1f);
                block.sizeDelta = new Vector2(Mathf.Max(60f, span * PixelsPerSecond), LaneHeight - 10f);
                block.anchoredPosition = new Vector2(cue.at * PixelsPerSecond, -cue.pad * LaneHeight - 2f);
                bool selected = cue == SelectedCue;
                Image image = UiKit.Panel(block, selected ? UiTheme.Saffron : (t?.Aerial ?? false) ? UiTheme.Magenta : new Color(0.3f, 0.55f, 0.95f, 0.95f));
                TextMeshProUGUI label = UiKit.Text(block, CrackerName(cue.cracker), 20f, TextAlignmentOptions.Left, selected ? UiTheme.Indigo : UiTheme.OffWhite);
                ((RectTransform)label.transform).offsetMin = new Vector2(8f, 0f);
                label.textWrappingMode = TextWrappingModes.NoWrap;
                var button = block.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                ShowCue captured = cue;
                button.onClick.AddListener(() => SelectCue(captured));
                _built.Add(block.gameObject);
            }
        }

        private void Build()
        {
            _launcher = UiKit.Rect("ShowsButton", transform);
            _launcher.anchorMin = _launcher.anchorMax = _launcher.pivot = new Vector2(1f, 0f);
            _launcher.sizeDelta = new Vector2(220f, UiTheme.MinTouch);
            _launcher.anchoredPosition = new Vector2(-32f - 280f - 20f, TrayView.TrayHeight + 150f); // left of Finale
            Image launcherImage = UiKit.Panel(_launcher, UiTheme.IndigoLight);
            var launcherButton = _launcher.gameObject.AddComponent<Button>();
            launcherButton.targetGraphic = launcherImage;
            launcherButton.onClick.AddListener(Open);
            UiKit.Text(_launcher, "Shows", UiTheme.BodyText, TextAlignmentOptions.Center, UiTheme.OffWhite);

            _overlay = UiKit.Rect("ShowDesigner", transform.parent != null ? transform.parent : transform);
            UiKit.Stretch(_overlay);

            // The editing panel: the lower 58% of the screen; the spot stays visible above it.
            _panel = UiKit.Rect("Panel", _overlay);
            _panel.anchorMin = Vector2.zero;
            _panel.anchorMax = new Vector2(1f, 0.58f);
            _panel.offsetMin = _panel.offsetMax = Vector2.zero;
            UiKit.Panel(_panel, new Color(0.09f, 0.06f, 0.2f, 0.97f));

            RectTransform header = Area(_panel, "Header", 0f, 1f, 1f, 1f, new Vector2(0f, -UiTheme.MinTouch - 12f), new Vector2(0f, -12f));
            HeaderButton(header, "Close", 0.01f, 0.13f, UiTheme.IndigoLight, UiTheme.OffWhite, Close);
            HeaderButton(header, "Shows", 0.14f, 0.28f, UiTheme.IndigoLight, UiTheme.OffWhite, OpenList);
            BuildName(header, 0.29f, 0.55f);
            HeaderButton(header, "Save", 0.56f, 0.69f, UiTheme.IndigoLight, UiTheme.Marigold, () => Save());
            HeaderButton(header, "Clip", 0.7f, 0.83f, UiTheme.Magenta, UiTheme.OffWhite, () => RecordClip());
            HeaderButton(header, "Play", 0.84f, 0.99f, UiTheme.Saffron, UiTheme.Indigo, () => Preview());

            // Pad map on the left, palette on the right.
            float top = -UiTheme.MinTouch - 24f;
            RectTransform padBox = Area(_panel, "PadMap", 0f, 0.52f, 0.4f, 1f, new Vector2(16f, 0f), new Vector2(0f, top));
            UiKit.Panel(padBox, new Color(1f, 1f, 1f, 0.04f), raycast: false);
            _padMap = Area(padBox, "Pads", 0f, 0f, 1f, 1f, new Vector2(0f, 74f), Vector2.zero);
            RectTransform padButtons = Area(padBox, "PadButtons", 0f, 0f, 1f, 0f, Vector2.zero, new Vector2(0f, 70f));
            HeaderButton(padButtons, "− Pad", 0.02f, 0.48f, UiTheme.IndigoLight, UiTheme.OffWhite, RemovePad);
            HeaderButton(padButtons, "+ Pad", 0.52f, 0.98f, UiTheme.IndigoLight, UiTheme.OffWhite, AddPad);

            RectTransform paletteArea = Area(_panel, "Palette", 0.42f, 0.52f, 1f, 1f, Vector2.zero, new Vector2(-16f, top));
            _palette = UiKit.Rect("Chips", paletteArea);
            UiKit.Stretch(_palette);
            var grid = _palette.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(140f, 140f);
            grid.spacing = new Vector2(10f, 10f);
            grid.childAlignment = TextAnchor.UpperLeft;

            // Timeline strip: one lane per pad, scrolling sideways.
            RectTransform viewport = Area(_panel, "Timeline", 0f, 0f, 1f, 0.5f, new Vector2(16f, UiTheme.MinTouch + 90f), new Vector2(-16f, 0f));
            UiKit.Panel(viewport, new Color(0f, 0f, 0f, 0.25f));
            viewport.gameObject.AddComponent<RectMask2D>();
            _lanes = UiKit.Rect("Lanes", viewport);
            _lanes.anchorMin = _lanes.anchorMax = _lanes.pivot = new Vector2(0f, 1f);
            _lanes.anchoredPosition = new Vector2(10f, -10f);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.vertical = true;
            scroll.horizontal = true;
            scroll.viewport = viewport;
            scroll.content = _lanes;

            // Delay controls for the selected cracker.
            RectTransform controls = Area(_panel, "Delay", 0f, 0f, 1f, 0f, new Vector2(16f, 80f), new Vector2(-16f, 80f + UiTheme.MinTouch));
            HeaderButton(controls, "−1 s", 0f, 0.12f, UiTheme.IndigoLight, UiTheme.OffWhite, () => Nudge(-1f));
            HeaderButton(controls, "−0.1", 0.13f, 0.25f, UiTheme.IndigoLight, UiTheme.OffWhite, () => Nudge(-0.1f));
            RectTransform cueRect = Area(controls, "Cue", 0.26f, 0f, 0.62f, 1f, Vector2.zero, Vector2.zero);
            _cueLabel = UiKit.Text(cueRect, string.Empty, UiTheme.SmallText, TextAlignmentOptions.Center, UiTheme.OffWhite);
            HeaderButton(controls, "+0.1", 0.63f, 0.75f, UiTheme.IndigoLight, UiTheme.OffWhite, () => Nudge(0.1f));
            HeaderButton(controls, "+1 s", 0.76f, 0.88f, UiTheme.IndigoLight, UiTheme.OffWhite, () => Nudge(1f));
            HeaderButton(controls, "Remove", 0.89f, 1f, UiTheme.Magenta, UiTheme.OffWhite, RemoveSelected);

            RectTransform status = Area(_panel, "Status", 0f, 0f, 1f, 0f, new Vector2(24f, 8f), new Vector2(-24f, 74f));
            _status = UiKit.Text(status, string.Empty, UiTheme.SmallText, TextAlignmentOptions.Center, UiTheme.OffWhite);

            // While a preview plays: a slim bar with the show clock and Stop.
            _playBar = UiKit.Rect("PlayBar", _overlay);
            _playBar.anchorMin = new Vector2(0f, 0f);
            _playBar.anchorMax = new Vector2(1f, 0f);
            _playBar.pivot = new Vector2(0.5f, 0f);
            _playBar.sizeDelta = new Vector2(-32f, UiTheme.MinTouch + 24f);
            _playBar.anchoredPosition = new Vector2(0f, 16f);
            UiKit.Panel(_playBar, UiTheme.Indigo);
            RectTransform playText = Area(_playBar, "Clock", 0f, 0f, 0.72f, 1f, new Vector2(24f, 0f), Vector2.zero);
            _playLabel = UiKit.Text(playText, string.Empty, UiTheme.BodyText, TextAlignmentOptions.Left, UiTheme.OffWhite);
            RectTransform stopArea = Area(_playBar, "StopArea", 0.74f, 0.1f, 0.98f, 0.9f, Vector2.zero, Vector2.zero);
            HeaderButton(stopArea, "Stop", 0f, 1f, UiTheme.Magenta, UiTheme.OffWhite, StopPreview);
            _playBar.gameObject.SetActive(false);

            // The shows list, over the panel.
            _list = Area(_overlay, "ShowsList", 0.06f, 0.08f, 0.94f, 0.56f, Vector2.zero, Vector2.zero);
            UiKit.Panel(_list, new Color(0.12f, 0.08f, 0.26f, 0.99f));
            RectTransform listHeader = Area(_list, "Header", 0f, 1f, 1f, 1f, new Vector2(16f, -UiTheme.MinTouch - 12f), new Vector2(-16f, -12f));
            RectTransform listTitle = Area(listHeader, "Title", 0f, 0f, 0.7f, 1f, Vector2.zero, Vector2.zero);
            UiKit.Text(listTitle, "Shows", UiTheme.TitleText, TextAlignmentOptions.Left, UiTheme.Marigold);
            HeaderButton(listHeader, "Back", 0.72f, 1f, UiTheme.IndigoLight, UiTheme.OffWhite, CloseList);
            RectTransform listView = Area(_list, "Rows", 0f, 0f, 1f, 1f, new Vector2(16f, 16f), new Vector2(-16f, -UiTheme.MinTouch - 24f));
            listView.gameObject.AddComponent<RectMask2D>();
            _listRows = UiKit.Rect("Content", listView);
            _listRows.anchorMin = new Vector2(0f, 1f);
            _listRows.anchorMax = new Vector2(1f, 1f);
            _listRows.pivot = new Vector2(0.5f, 1f);
            var layout = _listRows.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            _listRows.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var listScroll = listView.gameObject.AddComponent<ScrollRect>();
            listScroll.horizontal = false;
            listScroll.viewport = listView;
            listScroll.content = _listRows;
            _list.gameObject.SetActive(false);

            _overlay.gameObject.SetActive(false);
        }

        private void BuildName(RectTransform header, float from, float to)
        {
            RectTransform box = Area(header, "Name", from, 0f, to, 1f, Vector2.zero, Vector2.zero);
            Image bg = UiKit.Panel(box, new Color(1f, 1f, 1f, 0.08f));
            RectTransform area = Area(box, "TextArea", 0f, 0f, 1f, 1f, new Vector2(16f, 0f), new Vector2(-16f, 0f));
            area.gameObject.AddComponent<RectMask2D>();
            TextMeshProUGUI placeholder = UiKit.Text(area, "Name your show", UiTheme.SmallText, TextAlignmentOptions.Left, UiTheme.Muted);
            TextMeshProUGUI text = UiKit.Text(area, string.Empty, UiTheme.BodyText, TextAlignmentOptions.Left, UiTheme.OffWhite);
            _name = box.gameObject.AddComponent<TMP_InputField>();
            _name.targetGraphic = bg;
            _name.textViewport = area;
            _name.textComponent = text;
            _name.placeholder = placeholder;
            _name.characterLimit = ShowRules.MaxNameLength;
            _name.lineType = TMP_InputField.LineType.SingleLine;
            _name.onEndEdit.AddListener(Rename);
        }

        private void ListRow(string label, Color background, Color text, Action choose, Action delete)
        {
            RectTransform row = UiKit.Rect(label, _listRows);
            row.sizeDelta = new Vector2(0f, UiTheme.MinTouch);
            HeaderButton(row, label, 0f, delete != null ? 0.76f : 1f, background, text, choose);
            if (delete != null)
            {
                HeaderButton(row, "Delete", 0.78f, 1f, UiTheme.Magenta, UiTheme.OffWhite, delete);
            }
        }

        private static RectTransform Area(RectTransform parent, string name, float x0, float y0, float x1, float y1, Vector2 offsetMin, Vector2 offsetMax)
        {
            RectTransform rect = UiKit.Rect(name, parent);
            rect.anchorMin = new Vector2(x0, y0);
            rect.anchorMax = new Vector2(x1, y1);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        private static void HeaderButton(RectTransform parent, string label, float from, float to, Color background, Color text, Action action)
        {
            RectTransform rect = Area(parent, label, from, 0f, to, 1f, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            Image image = UiKit.Panel(rect, background);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            TextMeshProUGUI t = UiKit.Text(rect, label, UiTheme.SmallText, TextAlignmentOptions.Center, text);
            t.enableAutoSizing = true;
            t.fontSizeMin = 18f;
            t.fontSizeMax = UiTheme.SmallText;
        }
    }
}

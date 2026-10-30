using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UtsavNights.Core.Cloud;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;

namespace UtsavNights.UI
{
    /// <summary>
    /// Home visits (Phase 10 step 4, F23), from the Visit button above Shows.
    /// - Share my home: an optional "Show my avatar", then a code to send (the share sheet) and the player's own codes
    ///   with their visits and Take down.
    /// - Visit a home: type a code; recent visits to open again, offline too.
    /// During a visit a bar across the top says whose code it is, with Report and Leave; the rest of the screen is view
    /// only (nothing can be placed or edited).
    /// </summary>
    public sealed class HomeVisitScreen : MonoBehaviour
    {
        private readonly List<GameObject> _dynamicRows = new List<GameObject>();
        private RectTransform _launcher;
        private RectTransform _overlay;
        private RectTransform _rows;
        private RectTransform _mine;
        private RectTransform _recent;
        private TMP_InputField _code;
        private TextMeshProUGUI _status;
        private TextMeshProUGUI _shareNote;
        private TextMeshProUGUI _avatarLabel;
        private TextMeshProUGUI _lastCode;
        private RectTransform _visitBar;
        private TextMeshProUGUI _visitTitle;
        private TextMeshProUGUI _visitNotes;
        private RectTransform _reasons;
        private CloudService _cloud;
        private HomeVisits _visits;
        private bool _showAvatar;
        private int _busy;

        public bool IsOpen => _overlay != null && _overlay.gameObject.activeSelf;

        public bool Busy => _busy > 0;

        public bool VisitBarVisible => _visitBar != null && _visitBar.gameObject.activeSelf;

        public bool ReasonsVisible => _reasons != null && _reasons.gameObject.activeSelf;

        public string Status => _status != null ? _status.text : string.Empty;

        public string VisitTitle => _visitTitle != null ? _visitTitle.text : string.Empty;

        public string VisitNotes => _visitNotes != null ? _visitNotes.text : string.Empty;

        /// <summary>The code made by the last Share (for the tests and the screen reader).</summary>
        public string LastCode { get; private set; }

        public List<ShareInfo> MyHomes { get; } = new List<ShareInfo>();

        public RectTransform Launcher => _launcher;

        private void Awake() => Build();

        private void Start()
        {
            Game.Services.TryGet(out _cloud);
            if (Game.Services.TryGet(out _visits))
            {
                _visits.Changed += RefreshVisitBar;
            }

            InputBlockers.Register(_launcher);
        }

        private void OnDestroy()
        {
            if (_visits != null)
            {
                _visits.Changed -= RefreshVisitBar;
            }

            InputBlockers.Unregister(_launcher);
            InputBlockers.Unregister(_overlay);
            InputBlockers.Unregister(_visitBar);
        }

        private void Update()
        {
            if (IsOpen && !Busy && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
        }

        public void Open()
        {
            Game.Services.TryGet(out _cloud);
            if (_visits == null && Game.Services.TryGet(out _visits))
            {
                _visits.Changed += RefreshVisitBar;
            }

            _overlay.gameObject.SetActive(true);
            _overlay.SetAsLastSibling();
            InputBlockers.Register(_overlay);
            SetStatus(string.Empty);
            _ = Run(RefreshLists);
        }

        public void Close()
        {
            InputBlockers.Unregister(_overlay);
            _overlay.gameObject.SetActive(false);
        }

        public void SetCode(string code) => _code.text = code ?? string.Empty;

        public void ToggleAvatar()
        {
            _showAvatar = !_showAvatar;
            _avatarLabel.text = _showAvatar ? "Show my avatar: on" : "Show my avatar: off";
        }

        /// <summary>Same as tapping "Share my home".</summary>
        public void ShareMyHome()
        {
            if (_visits == null || Busy)
            {
                return;
            }

            if (_cloud == null || !_cloud.SignedIn)
            {
                SetStatus("Sharing your home needs the Google sign-in, so your codes are yours to take down. Sign in from Settings → Backup and restore.");
                return;
            }

            SetStatus("Sharing…");
            _ = Run(async () =>
            {
                (string code, string error) = await _visits.ShareMyHome(_showAvatar);
                if (code == null)
                {
                    SetStatus(error);
                    return;
                }

                LastCode = code;
                _lastCode.text = ShareCodes.Display(code);
                SetStatus("Your code is ready. Send it to family and friends.");
                await RefreshLists();
            });
        }

        /// <summary>Same as tapping "Send" beside a code: the share sheet with a short invitation (also copied).</summary>
        public void SendCode(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                return;
            }

            TextShare.Share($"Come and see my home in Utsav Nights! Open Visit and type {ShareCodes.Display(code)}");
            SetStatus("Code copied. Choose where to send it.");
        }

        /// <summary>Same as tapping "Visit".</summary>
        public void VisitCode() => Visit(_code.text);

        public void Visit(string code)
        {
            if (_visits == null || Busy)
            {
                return;
            }

            WalkMode walk = FindFirstObjectByType<WalkMode>();
            if (walk != null && walk.IsOpen)
            {
                walk.Home();
            }

            SetStatus("Opening…");
            _ = Run(async () =>
            {
                string error = await _visits.Visit(code);
                if (error.Length > 0)
                {
                    SetStatus(error);
                    await RefreshLists();
                    return;
                }

                Close();
            });
        }

        /// <summary>Same as tapping Leave on the visit bar.</summary>
        public void Leave()
        {
            _reasons.gameObject.SetActive(false);
            _visits?.Leave();
        }

        public void OpenReport() => _reasons.gameObject.SetActive(!_reasons.gameObject.activeSelf);

        /// <summary>Same as choosing a reason after Report.</summary>
        public void Report(ReportReason reason)
        {
            if (_visits == null || !_visits.Visiting || Busy)
            {
                return;
            }

            string code = _visits.Current;
            _reasons.gameObject.SetActive(false);
            _ = Run(async () =>
            {
                string error = await _visits.Report(code, reason);
                _visitNotes.text = error.Length == 0 ? "Thanks. It'll be reviewed, and hidden if others report it too." : error;
            });
        }

        /// <summary>Same as tapping "Take down" beside one of the player's codes.</summary>
        public void TakeDown(string code)
        {
            if (_visits == null || Busy)
            {
                return;
            }

            _ = Run(async () =>
            {
                string error = await _visits.Withdraw(code);
                SetStatus(error.Length == 0 ? $"{ShareCodes.Display(code)} is taken down. Nobody can open it now." : error);
                await RefreshLists();
            });
        }

        private async Task RefreshLists()
        {
            foreach (GameObject row in _dynamicRows)
            {
                Destroy(row);
            }

            _dynamicRows.Clear();
            MyHomes.Clear();
            bool signedIn = _cloud != null && _cloud.SignedIn;
            _shareNote.text = _cloud == null || !_cloud.Configured ? CloudRules.Explain(CloudError.NotConfigured)
                : signedIn ? "Share how your home looks now. Friends see it, view only; they never see your name or your account."
                : "Sharing needs the Google sign-in (Settings → Backup and restore). Visiting a friend's home doesn't.";
            if (signedIn && _visits != null)
            {
                MyHomes.AddRange(await _visits.MyHomes());
            }

            foreach (ShareInfo s in MyHomes)
            {
                RectTransform row = Row(_mine, "Mine " + s.code, 96f);
                UiKit.Panel(row, UiTheme.IndigoLight).sprite = UiKit.Rounded;
                RectTransform label = Area(row, "Label", 0f, 0f, 0.46f, 1f, new Vector2(18f, 0f), Vector2.zero);
                string visits = s.visits == 1 ? "1 visit" : $"{s.visits} visits";
                UiKit.Text(label, $"{ShareCodes.Display(s.code)}\n<size=75%>{visits}</size>", UiTheme.SmallText, TextAlignmentOptions.Left, UiTheme.OffWhite);
                string code = s.code;
                Clickable(Area(row, "Send", 0.48f, 0.15f, 0.72f, 0.85f, Vector2.zero, Vector2.zero), "Send", UiTheme.Saffron, () => SendCode(code));
                Clickable(Area(row, "TakeDown", 0.74f, 0.15f, 0.98f, 0.85f, Vector2.zero, Vector2.zero), "Take down", UiTheme.Magenta, () => TakeDown(code));
                _dynamicRows.Add(row.gameObject);
            }

            if (_visits == null)
            {
                return;
            }

            foreach (VisitRecord v in _visits.History.visits)
            {
                RectTransform row = Row(_recent, "Recent " + v.code, 96f);
                UiKit.Panel(row, UiTheme.IndigoLight).sprite = UiKit.Rounded;
                RectTransform label = Area(row, "Label", 0f, 0f, 0.66f, 1f, new Vector2(18f, 0f), Vector2.zero);
                string when = DateTimeOffset.FromUnixTimeSeconds(v.visitedUnix).ToLocalTime().ToString("d MMM, HH:mm");
                UiKit.Text(label, $"{ShareCodes.Display(v.code)}\n<size=75%>Visited {when} · kept for offline</size>", UiTheme.SmallText, TextAlignmentOptions.Left, UiTheme.OffWhite);
                string code = v.code;
                Clickable(Area(row, "Again", 0.68f, 0.15f, 0.98f, 0.85f, Vector2.zero, Vector2.zero), "Visit again", UiTheme.Saffron, () => Visit(code));
                _dynamicRows.Add(row.gameObject);
            }

            if (_visits.History.visits.Count == 0)
            {
                RectTransform row = Row(_recent, "NoVisits", 56f);
                UiKit.Text(row, "Homes you visit appear here, to see again any time.", UiTheme.SmallText, TextAlignmentOptions.Left, UiTheme.Muted);
                _dynamicRows.Add(row.gameObject);
            }
        }

        private void RefreshVisitBar()
        {
            bool visiting = _visits != null && _visits.Visiting;
            _visitBar.gameObject.SetActive(visiting);
            _launcher.gameObject.SetActive(!visiting);
            if (!visiting)
            {
                InputBlockers.Unregister(_visitBar);
                _reasons.gameObject.SetActive(false);
                return;
            }

            _visitBar.SetAsLastSibling();
            InputBlockers.Register(_visitBar);
            _visitTitle.text = $"Visiting {ShareCodes.Display(_visits.Current)}" + (_visits.OfflineCopy ? " · the copy on this phone" : string.Empty);
            _visitNotes.text = _visits.Notes;
        }

        private async Task Run(Func<Task> work)
        {
            _busy++;
            try
            {
                await work();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Visits] " + e);
                SetStatus("Something went wrong: " + e.Message);
            }
            finally
            {
                _busy--;
            }
        }

        private void SetStatus(string text) => _status.text = text;

        private void Build()
        {
            _launcher = UiKit.Rect("VisitButton", transform);
            _launcher.anchorMin = _launcher.anchorMax = _launcher.pivot = new Vector2(1f, 0f);
            _launcher.sizeDelta = new Vector2(220f, UiTheme.MinTouch);
            _launcher.anchoredPosition = new Vector2(-32f - 280f - 20f, TrayView.TrayHeight + 150f + UiTheme.MinTouch + 20f); // above Shows
            Clickable(_launcher, "Visit", UiTheme.IndigoLight, Open);

            Transform root = transform.parent != null ? transform.parent : transform;
            _overlay = UiKit.Rect("HomeVisits", root);
            UiKit.Stretch(_overlay);
            UiKit.Panel(_overlay, new Color(0.09f, 0.06f, 0.2f, 0.99f));
            UiKit.Text(Area(_overlay, "Title", 0f, 1f, 1f, 1f, new Vector2(150f, -UiTheme.MinTouch - 20f), new Vector2(-150f, -20f)), "Home visits",
                UiTheme.TitleText, TextAlignmentOptions.Center, UiTheme.Marigold);
            RectTransform close = UiKit.Rect("Close", _overlay);
            close.anchorMin = close.anchorMax = close.pivot = new Vector2(0f, 1f);
            close.sizeDelta = new Vector2(220f, UiTheme.MinTouch);
            close.anchoredPosition = new Vector2(28f, -20f);
            Clickable(close, "Close", UiTheme.IndigoLight, Close);

            RectTransform viewport = Area(_overlay, "Page", 0f, 0f, 1f, 1f, new Vector2(32f, 140f), new Vector2(-32f, -UiTheme.MinTouch - 40f));
            viewport.gameObject.AddComponent<RectMask2D>();
            _rows = UiKit.Rect("Rows", viewport);
            _rows.anchorMin = new Vector2(0f, 1f);
            _rows.anchorMax = new Vector2(1f, 1f);
            _rows.pivot = new Vector2(0.5f, 1f);
            _rows.sizeDelta = Vector2.zero;
            Stack(_rows, 16f);
            _rows.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.viewport = viewport;
            scroll.content = _rows;

            Heading("Visit a home");
            RectTransform entry = Row(_rows, "CodeEntry", UiTheme.MinTouch);
            RectTransform box = Area(entry, "Code", 0f, 0f, 0.68f, 1f, Vector2.zero, Vector2.zero);
            Image bg = UiKit.Panel(box, new Color(1f, 1f, 1f, 0.08f));
            RectTransform area = Area(box, "TextArea", 0f, 0f, 1f, 1f, new Vector2(16f, 0f), new Vector2(-16f, 0f));
            area.gameObject.AddComponent<RectMask2D>();
            TextMeshProUGUI placeholder = UiKit.Text(area, "Code, like BCDF-2345", UiTheme.SmallText, TextAlignmentOptions.Left, UiTheme.Muted);
            TextMeshProUGUI text = UiKit.Text(area, string.Empty, UiTheme.BodyText, TextAlignmentOptions.Left, UiTheme.OffWhite);
            _code = box.gameObject.AddComponent<TMP_InputField>();
            _code.targetGraphic = bg;
            _code.textViewport = area;
            _code.textComponent = text;
            _code.placeholder = placeholder;
            _code.characterLimit = 12;
            _code.lineType = TMP_InputField.LineType.SingleLine;
            _code.onEndEdit.AddListener(_ => { });
            Clickable(Area(entry, "Visit", 0.7f, 0f, 1f, 1f, Vector2.zero, Vector2.zero), "Visit", UiTheme.Saffron, VisitCode);
            _recent = Row(_rows, "Recent", 0f);
            Stack(_recent, 10f);
            Destroy(_recent.GetComponent<LayoutElement>());

            Heading("Share my home");
            _shareNote = Paragraph("ShareNote", 110f);
            _avatarLabel = Clickable(Row(_rows, "Avatar", UiTheme.MinTouch), "Show my avatar: off", UiTheme.IndigoLight, ToggleAvatar);
            Clickable(Row(_rows, "Share", UiTheme.MinTouch), "Share my home", UiTheme.Saffron, ShareMyHome);
            RectTransform codeRow = Row(_rows, "NewCode", UiTheme.MinTouch);
            _lastCode = UiKit.Text(Area(codeRow, "Code", 0f, 0f, 0.68f, 1f, Vector2.zero, Vector2.zero), string.Empty, UiTheme.TitleText, TextAlignmentOptions.Center, UiTheme.Marigold);
            Clickable(Area(codeRow, "Send", 0.7f, 0f, 1f, 1f, Vector2.zero, Vector2.zero), "Send", UiTheme.IndigoLight, () => SendCode(LastCode));
            Heading("Your codes");
            _mine = Row(_rows, "Mine", 0f);
            Stack(_mine, 10f);
            Destroy(_mine.GetComponent<LayoutElement>());

            _status = UiKit.Text(Area(_overlay, "Status", 0f, 0f, 1f, 0f, new Vector2(40f, 16f), new Vector2(-40f, 130f)), string.Empty,
                UiTheme.SmallText, TextAlignmentOptions.Center, UiTheme.OffWhite);
            _status.textWrappingMode = TextWrappingModes.Normal;
            _overlay.gameObject.SetActive(false);

            // The visit: a see-through cover (nothing underneath can be tapped) with a bar across the top.
            _visitBar = UiKit.Rect("VisitBar", root);
            UiKit.Stretch(_visitBar);
            Image cover = _visitBar.gameObject.AddComponent<Image>();
            cover.color = new Color(0f, 0f, 0f, 0f);
            RectTransform bar = Area(_visitBar, "Bar", 0f, 1f, 1f, 1f, new Vector2(24f, -UiTheme.MinTouch - 120f), new Vector2(-24f, -16f));
            UiKit.Panel(bar, new Color(0.09f, 0.06f, 0.2f, 0.92f)).sprite = UiKit.Rounded;
            _visitTitle = UiKit.Text(Area(bar, "Title", 0f, 0.45f, 0.6f, 1f, new Vector2(24f, 0f), Vector2.zero), string.Empty, UiTheme.BodyText, TextAlignmentOptions.Left, UiTheme.Marigold);
            _visitNotes = UiKit.Text(Area(bar, "Notes", 0f, 0f, 0.6f, 0.5f, new Vector2(24f, 8f), Vector2.zero), string.Empty, UiTheme.SmallText, TextAlignmentOptions.TopLeft, UiTheme.OffWhite);
            _visitNotes.textWrappingMode = TextWrappingModes.Normal;
            Clickable(Area(bar, "Report", 0.62f, 0.2f, 0.8f, 0.8f, Vector2.zero, Vector2.zero), "Report", UiTheme.IndigoLight, OpenReport);
            Clickable(Area(bar, "Leave", 0.81f, 0.2f, 0.99f, 0.8f, Vector2.zero, Vector2.zero), "Leave", UiTheme.Saffron, Leave);
            _reasons = Area(_visitBar, "Reasons", 0.5f, 1f, 1f, 1f, new Vector2(0f, -UiTheme.MinTouch * 5f - 160f), new Vector2(-24f, -UiTheme.MinTouch - 130f));
            UiKit.Panel(_reasons, new Color(0.14f, 0.09f, 0.3f, 0.98f)).sprite = UiKit.Rounded;
            Stack(_reasons, 8f);
            _reasons.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(16, 16, 16, 16);
            foreach ((ReportReason reason, string label) in new[]
            {
                (ReportReason.Offensive, "Offensive"), (ReportReason.Inappropriate, "Inappropriate"), (ReportReason.Spam, "Spam"), (ReportReason.Other, "Something else"),
            })
            {
                ReportReason r = reason;
                Clickable(Row(_reasons, label, UiTheme.MinTouch), label, UiTheme.IndigoLight, () => Report(r));
            }

            _reasons.gameObject.SetActive(false);
            _visitBar.gameObject.SetActive(false);
        }

        private void Heading(string label) =>
            UiKit.Text(Row(_rows, label, 64f), label, UiTheme.BodyText, TextAlignmentOptions.BottomLeft, UiTheme.Marigold);

        private TextMeshProUGUI Paragraph(string name, float height)
        {
            TextMeshProUGUI t = UiKit.Text(Row(_rows, name, height), string.Empty, UiTheme.SmallText, TextAlignmentOptions.TopLeft, UiTheme.OffWhite);
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        private static void Stack(RectTransform rect, float spacing)
        {
            VerticalLayoutGroup layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        private static RectTransform Row(RectTransform parent, string name, float height)
        {
            RectTransform row = UiKit.Rect(name, parent);
            LayoutElement e = row.gameObject.AddComponent<LayoutElement>();
            e.minHeight = e.preferredHeight = height;
            return row;
        }

        private static TextMeshProUGUI Clickable(RectTransform rect, string label, Color background, Action action)
        {
            Image image = UiKit.Panel(rect, background);
            image.sprite = UiKit.Rounded;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            return UiKit.Text(rect, label, UiTheme.BodyText, TextAlignmentOptions.Center, UiTheme.OffWhite);
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
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UtsavNights.Core.Backup;
using UtsavNights.Core.Cloud;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;

namespace UtsavNights.UI
{
    /// <summary>
    /// Settings → Backup and restore (Phase 10 steps 1 and 3, F24).
    /// - Cloud backup: an optional Google sign-in; Back up now; the newest three backups, each with Restore; Delete my
    ///   cloud data. Made only when the player asks.
    /// - Backup file: a password the player picks; save the file anywhere the phone's file picker reaches; restore it.
    /// Either restore shows what the backup holds and asks first, and can be undone afterwards.
    /// </summary>
    public sealed class BackupScreen : MonoBehaviour
    {
        private readonly List<GameObject> _backupRows = new List<GameObject>();
        private RectTransform _overlay;
        private RectTransform _rows;
        private TMP_InputField _password;
        private TextMeshProUGUI _status;
        private TextMeshProUGUI _account;
        private TextMeshProUGUI _signInLabel;
        private GameObject _backUpNow;
        private GameObject _deleteCloud;
        private RectTransform _backupList;
        private GameObject _undo;
        private RectTransform _confirm;
        private TextMeshProUGUI _confirmTitle;
        private TextMeshProUGUI _summary;
        private TextMeshProUGUI _confirmLabel;
        private Action _confirmAction;
        private BackupService _backup;
        private CloudService _cloud;
        private int _busy;

        public bool IsOpen => _overlay != null && _overlay.gameObject.activeSelf;

        public bool Busy => _busy > 0;

        public bool ConfirmVisible => _confirm != null && _confirm.gameObject.activeSelf;

        public string Status => _status != null ? _status.text : string.Empty;

        public string Summary => _summary != null ? _summary.text : string.Empty;

        public string Account => _account != null ? _account.text : string.Empty;

        public bool UndoVisible => _undo != null && _undo.activeSelf;

        /// <summary>The cloud backups listed (newest first).</summary>
        public List<CloudBackupInfo> CloudBackups { get; } = new List<CloudBackupInfo>();

        public BackupService Service => _backup ??= _cloud != null ? _cloud.Backup : new BackupService();

        private void Awake() => Build();

        private void Start() => Game.Services.TryGet(out _cloud);

        private void OnDestroy()
        {
            if (_overlay != null)
            {
                InputBlockers.Unregister(_overlay);
            }
        }

        private void Update()
        {
            if (IsOpen && !Busy && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (ConfirmVisible)
                {
                    CancelConfirm();
                }
                else
                {
                    Close();
                }
            }
        }

        public void Open()
        {
            if (_cloud == null)
            {
                Game.Services.TryGet(out _cloud);
            }

            _overlay.gameObject.SetActive(true);
            _overlay.SetAsLastSibling();
            InputBlockers.Register(_overlay);
            _confirm.gameObject.SetActive(false);
            _undo.SetActive(Service.CanUndo);
            SetStatus(string.Empty);
            RefreshAccount();
            _ = RefreshBackups();
        }

        public void Close()
        {
            InputBlockers.Unregister(_overlay);
            _overlay.gameObject.SetActive(false);
            _password.text = string.Empty;
            _confirmAction = null;
        }

        public void SetPassword(string password) => _password.text = password ?? string.Empty;

        // ---- Cloud backup ----

        /// <summary>Same as tapping "Sign in with Google" (or "Sign out" when signed in).</summary>
        public void SignInOrOut()
        {
            if (_cloud == null || Busy)
            {
                return;
            }

            if (_cloud.SignedIn)
            {
                _cloud.SignOut();
                RefreshAccount();
                _ = RefreshBackups();
                SetStatus("Signed out. Your backups stay in your account.");
                return;
            }

            _ = Run(async () =>
            {
                string error = await _cloud.SignIn();
                RefreshAccount();
                await RefreshBackups();
                SetStatus(error.Length == 0 ? "Signed in. Back up whenever you like." : error);
            });
        }

        /// <summary>Same as tapping "Back up now".</summary>
        public void BackUpNow()
        {
            if (_cloud == null || Busy)
            {
                return;
            }

            SetStatus("Backing up…");
            _ = Run(async () =>
            {
                string error = await _cloud.BackUpNow();
                await RefreshBackups();
                SetStatus(error.Length == 0 ? "Backed up." : error);
            });
        }

        /// <summary>Same as tapping Restore on a cloud backup: download, check, then confirm.</summary>
        public void RestoreCloud(string id)
        {
            if (_cloud == null || Busy)
            {
                return;
            }

            SetStatus("Downloading the backup…");
            _ = Run(async () =>
            {
                (BackupArchive archive, BackupSummary summary, string error) = await _cloud.Fetch(id);
                if (archive == null)
                {
                    SetStatus(error);
                    return;
                }

                AskToRestore(summary, () => Finish(_cloud.Restore(archive)));
            });
        }

        /// <summary>Same as tapping "Delete my cloud data" (asks first).</summary>
        public void DeleteCloudData()
        {
            if (_cloud == null || Busy || !_cloud.SignedIn)
            {
                return;
            }

            Ask("Delete your cloud data?", "This deletes your cloud backups and the homes and shows you've shared, and signs you out. "
                + "Everything on this phone stays as it is.", "Delete", () => _ = Run(async () =>
            {
                string error = await _cloud.DeleteMyData();
                RefreshAccount();
                await RefreshBackups();
                SetStatus(error.Length == 0 ? "Your cloud data is deleted." : error);
            }));
        }

        // ---- Backup file ----

        /// <summary>Same as tapping "Save backup file".</summary>
        public void SaveFile()
        {
            if (Busy)
            {
                return;
            }

            string password = _password.text;
            if (BackupRules.PasswordProblem(password) is string problem)
            {
                SetStatus("Choose a password for the file. " + problem);
                return;
            }

            BackupArchive archive = Service.Collect();
            SetStatus("Making the backup…");
            _ = Run(async () =>
            {
                byte[] data = await Task.Run(() => BackupCrypto.SealWithPassword(archive, password));
                var saved = new TaskCompletionSource<string>();
                BackupFiles.Save(data, BackupRules.FileName(DateTime.Now), r => saved.TrySetResult(r));
                string error = await saved.Task;
                SetStatus(error == null ? "Not saved." : error.Length == 0 ? "Backup saved. Keep the password: without it the file can't be opened." : error);
            });
        }

        /// <summary>Same as tapping "Restore from file": pick the file, then confirm.</summary>
        public void RestoreFromFile()
        {
            if (Busy)
            {
                return;
            }

            string password = _password.text;
            if (string.IsNullOrEmpty(password))
            {
                SetStatus("Type the backup file's password first.");
                return;
            }

            _ = Run(async () =>
            {
                var picked = new TaskCompletionSource<(byte[], string)>();
                BackupFiles.Open((d, e) => picked.TrySetResult((d, e)));
                (byte[] data, string error) = await picked.Task;
                if (data == null)
                {
                    SetStatus(error ?? string.Empty);
                    return;
                }

                if (!BackupCrypto.LooksLikeBackup(data))
                {
                    SetStatus(BackupRules.Explain(BackupError.NotABackup));
                    return;
                }

                SetStatus("Opening the backup…");
                (BackupError opened, BackupArchive archive) = await Task.Run(() =>
                {
                    BackupError e = BackupCrypto.OpenWithPassword(data, password, out BackupArchive a);
                    return (e, a);
                });
                if (opened != BackupError.None)
                {
                    SetStatus(BackupRules.Explain(opened));
                    return;
                }

                if (Service.Check(archive, out BackupSummary summary) is string problem)
                {
                    SetStatus(problem);
                    return;
                }

                AskToRestore(summary, () => Finish(Service.Apply(archive, out string e) ? string.Empty : e));
            });
        }

        // ---- Confirm and undo ----

        /// <summary>Same as tapping the confirm step's main button.</summary>
        public bool Confirm()
        {
            if (_confirmAction == null)
            {
                return false;
            }

            Action action = _confirmAction;
            _confirmAction = null;
            _confirm.gameObject.SetActive(false);
            action();
            return true;
        }

        public void CancelConfirm()
        {
            _confirmAction = null;
            _confirm.gameObject.SetActive(false);
            SetStatus("Nothing was changed.");
        }

        /// <summary>Same as tapping "Undo last restore".</summary>
        public bool Undo()
        {
            if (!Service.Undo(out string error))
            {
                SetStatus(error);
                return false;
            }

            Reload();
            return true;
        }

        private void AskToRestore(BackupSummary summary, Action restore) =>
            Ask("Restore this backup?", summary.Describe() + "\n\nThis replaces what's on this phone now. You can undo it afterwards.", "Restore", restore);

        private void Ask(string title, string text, string button, Action action)
        {
            _confirmTitle.text = title;
            _summary.text = text;
            _confirmLabel.text = button;
            _confirmAction = action;
            _confirm.gameObject.SetActive(true);
            _confirm.SetAsLastSibling();
            SetStatus(string.Empty);
        }

        private void Finish(string error)
        {
            if (!string.IsNullOrEmpty(error))
            {
                SetStatus(error);
                return;
            }

            Debug.Log("[Backup] restored; reloading the scene");
            Reload();
        }

        private void Reload()
        {
            Close();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
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
                Debug.LogWarning("[Backup] " + e);
                SetStatus("Something went wrong: " + e.Message);
            }
            finally
            {
                _busy--;
            }
        }

        private void RefreshAccount()
        {
            bool configured = _cloud != null && _cloud.Configured;
            bool signedIn = _cloud != null && _cloud.SignedIn;
            _account.text = !configured ? CloudRules.Explain(CloudError.NotConfigured)
                : signedIn ? "Signed in with Google. The game keeps no name or email; your backups are locked with a key only your account can open."
                : "Sign in with Google to keep backups in your account and restore them on a new phone. Optional.";
            _signInLabel.text = signedIn ? "Sign out" : "Sign in with Google";
            _signInLabel.transform.parent.gameObject.SetActive(configured);
            _backUpNow.SetActive(signedIn);
            _deleteCloud.SetActive(signedIn);
        }

        private async Task RefreshBackups()
        {
            CloudBackups.Clear();
            if (_cloud != null && _cloud.SignedIn)
            {
                (List<CloudBackupInfo> list, string _) = await _cloud.List();
                CloudBackups.AddRange(list);
            }

            foreach (GameObject row in _backupRows)
            {
                Destroy(row);
            }

            _backupRows.Clear();
            foreach (CloudBackupInfo b in CloudBackups)
            {
                string when = DateTimeOffset.FromUnixTimeSeconds(b.createdUnix).ToLocalTime().ToString("d MMM yyyy, HH:mm");
                string what = $"{b.decorations} decorations, {b.shows} shows" + (b.avatar ? ", avatar" : string.Empty);
                RectTransform row = Row(_backupList, "Backup " + b.id, 96f);
                UiKit.Panel(row, UiTheme.IndigoLight).sprite = UiKit.Rounded;
                RectTransform label = Area(row, "Label", 0f, 0f, 0.7f, 1f, new Vector2(18f, 0f), Vector2.zero);
                UiKit.Text(label, $"{when}\n<size=75%>{what}</size>", UiTheme.SmallText, TextAlignmentOptions.Left, UiTheme.OffWhite);
                string id = b.id;
                RectTransform restore = Area(row, "Restore", 0.72f, 0.15f, 0.98f, 0.85f, Vector2.zero, Vector2.zero);
                Clickable(restore, "Restore", UiTheme.Saffron, () => RestoreCloud(id));
                _backupRows.Add(row.gameObject);
            }

            if (CloudBackups.Count == 0 && _cloud != null && _cloud.SignedIn)
            {
                RectTransform row = Row(_backupList, "None", 60f);
                UiKit.Text(row, "No cloud backups yet.", UiTheme.SmallText, TextAlignmentOptions.Left, UiTheme.Muted);
                _backupRows.Add(row.gameObject);
            }
        }

        private void SetStatus(string text)
        {
            if (_status != null)
            {
                _status.text = text;
            }
        }

        private void Build()
        {
            _overlay = UiKit.Rect("BackupScreen", transform.parent != null ? transform.parent : transform);
            UiKit.Stretch(_overlay);
            UiKit.Panel(_overlay, new Color(0.09f, 0.06f, 0.2f, 0.99f));
            RectTransform title = Area(_overlay, "Title", 0f, 1f, 1f, 1f, new Vector2(150f, -UiTheme.MinTouch - 20f), new Vector2(-150f, -20f));
            UiKit.Text(title, "Backup and restore", UiTheme.TitleText, TextAlignmentOptions.Center, UiTheme.Marigold);
            RectTransform close = UiKit.Rect("Close", _overlay);
            close.anchorMin = close.anchorMax = close.pivot = new Vector2(0f, 1f);
            close.sizeDelta = new Vector2(220f, UiTheme.MinTouch);
            close.anchoredPosition = new Vector2(28f, -20f);
            Clickable(close, "Close", UiTheme.IndigoLight, Close);

            RectTransform viewport = Area(_overlay, "Page", 0f, 0f, 1f, 1f, new Vector2(32f, 150f), new Vector2(-32f, -UiTheme.MinTouch - 40f));
            viewport.gameObject.AddComponent<RectMask2D>();
            _rows = UiKit.Rect("Rows", viewport);
            _rows.anchorMin = new Vector2(0f, 1f);
            _rows.anchorMax = new Vector2(1f, 1f);
            _rows.pivot = new Vector2(0.5f, 1f);
            _rows.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = _rows.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            _rows.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.viewport = viewport;
            scroll.content = _rows;

            Heading("Cloud backup");
            _account = Paragraph("Account", 150f);
            _signInLabel = ButtonRow("Sign in with Google", UiTheme.IndigoLight, SignInOrOut);
            _backUpNow = ButtonRow("Back up now", UiTheme.Saffron, BackUpNow).transform.parent.gameObject;
            _backupList = Row(_rows, "CloudBackups", 0f);
            VerticalLayoutGroup list = _backupList.gameObject.AddComponent<VerticalLayoutGroup>();
            list.spacing = 10f;
            list.childControlWidth = true;
            list.childControlHeight = true;
            list.childForceExpandWidth = true;
            list.childForceExpandHeight = false;
            Destroy(_backupList.GetComponent<LayoutElement>());
            _deleteCloud = ButtonRow("Delete my cloud data", UiTheme.Magenta, DeleteCloudData).transform.parent.gameObject;

            Heading("Backup file");
            Paragraph("FileIntro", 190f).text = "Save everything to a file you keep, in Downloads or Drive, and restore it on this phone or a new one. "
                + "The file is locked with a password you choose. \"Remove ads\" comes back with Restore purchases, not with a backup.";
            RectTransform box = Row(_rows, "Password", UiTheme.MinTouch);
            Image bg = UiKit.Panel(box, new Color(1f, 1f, 1f, 0.08f));
            RectTransform area = Area(box, "TextArea", 0f, 0f, 1f, 1f, new Vector2(16f, 0f), new Vector2(-16f, 0f));
            area.gameObject.AddComponent<RectMask2D>();
            TextMeshProUGUI placeholder = UiKit.Text(area, $"Backup file password (at least {BackupRules.MinPasswordLength} characters)", UiTheme.SmallText, TextAlignmentOptions.Left, UiTheme.Muted);
            TextMeshProUGUI text = UiKit.Text(area, string.Empty, UiTheme.BodyText, TextAlignmentOptions.Left, UiTheme.OffWhite);
            _password = box.gameObject.AddComponent<TMP_InputField>();
            _password.targetGraphic = bg;
            _password.textViewport = area;
            _password.textComponent = text;
            _password.placeholder = placeholder;
            _password.characterLimit = 64;
            _password.lineType = TMP_InputField.LineType.SingleLine;
            _password.contentType = TMP_InputField.ContentType.Password;
            ButtonRow("Save backup file", UiTheme.Saffron, SaveFile);
            ButtonRow("Restore from file", UiTheme.IndigoLight, RestoreFromFile);
            _undo = ButtonRow("Undo last restore", UiTheme.IndigoLight, () => Undo()).transform.parent.gameObject;

            RectTransform status = Area(_overlay, "Status", 0f, 0f, 1f, 0f, new Vector2(40f, 16f), new Vector2(-40f, 140f));
            _status = UiKit.Text(status, string.Empty, UiTheme.SmallText, TextAlignmentOptions.Center, UiTheme.OffWhite);
            _status.textWrappingMode = TextWrappingModes.Normal;

            _confirm = Area(_overlay, "Confirm", 0.06f, 0.25f, 0.94f, 0.75f, Vector2.zero, Vector2.zero);
            UiKit.Panel(_confirm, new Color(0.14f, 0.09f, 0.3f, 1f));
            RectTransform heading = Area(_confirm, "Heading", 0f, 1f, 1f, 1f, new Vector2(24f, -UiTheme.MinTouch - 20f), new Vector2(-24f, -20f));
            _confirmTitle = UiKit.Text(heading, string.Empty, UiTheme.TitleText, TextAlignmentOptions.Center, UiTheme.Marigold);
            RectTransform summary = Area(_confirm, "Summary", 0f, 0f, 1f, 1f, new Vector2(32f, UiTheme.MinTouch + 48f), new Vector2(-32f, -UiTheme.MinTouch - 40f));
            _summary = UiKit.Text(summary, string.Empty, UiTheme.SmallText, TextAlignmentOptions.TopLeft, UiTheme.OffWhite);
            _summary.textWrappingMode = TextWrappingModes.Normal;
            RectTransform cancel = Area(_confirm, "Cancel", 0f, 0f, 0f, 0f, new Vector2(24f, 24f), new Vector2(284f, 24f + UiTheme.MinTouch));
            Clickable(cancel, "Cancel", UiTheme.IndigoLight, CancelConfirm);
            RectTransform ok = Area(_confirm, "Confirm", 1f, 0f, 1f, 0f, new Vector2(-284f, 24f), new Vector2(-24f, 24f + UiTheme.MinTouch));
            _confirmLabel = Clickable(ok, "Restore", UiTheme.Saffron, () => Confirm());
            _confirm.gameObject.SetActive(false);
            _overlay.gameObject.SetActive(false);
        }

        private void Heading(string label)
        {
            RectTransform row = Row(_rows, label, 64f);
            UiKit.Text(row, label, UiTheme.BodyText, TextAlignmentOptions.BottomLeft, UiTheme.Marigold);
        }

        private TextMeshProUGUI Paragraph(string name, float height)
        {
            RectTransform row = Row(_rows, name, height);
            TextMeshProUGUI t = UiKit.Text(row, string.Empty, UiTheme.SmallText, TextAlignmentOptions.TopLeft, UiTheme.OffWhite);
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        /// <summary>A full-width button row; returns its label (its parent is the button).</summary>
        private TextMeshProUGUI ButtonRow(string label, Color background, Action action) => Clickable(Row(_rows, label, UiTheme.MinTouch), label, background, action);

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

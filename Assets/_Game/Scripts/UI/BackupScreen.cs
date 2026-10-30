using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UtsavNights.Core.Backup;
using UtsavNights.Gameplay;

namespace UtsavNights.UI
{
    /// <summary>
    /// Settings → Backup and restore (Phase 10 step 1, F24). A backup file protected by a password the player picks:
    /// save it anywhere the phone's file picker reaches (Downloads, Drive), and restore from it on this or another phone.
    /// A restore shows what the backup holds and asks first; the files it replaced are kept so it can be undone.
    /// </summary>
    public sealed class BackupScreen : MonoBehaviour
    {
        private RectTransform _overlay;
        private TMP_InputField _password;
        private TextMeshProUGUI _status;
        private RectTransform _confirm;
        private TextMeshProUGUI _summary;
        private GameObject _undo;
        private BackupService _backup;
        private BackupArchive _pendingRestore;
        private Task<byte[]> _sealing;
        private Task<(BackupError, BackupArchive)> _opening;

        public bool IsOpen => _overlay != null && _overlay.gameObject.activeSelf;

        public bool Busy => _sealing != null || _opening != null;

        public bool ConfirmVisible => _confirm != null && _confirm.gameObject.activeSelf;

        public string Status => _status != null ? _status.text : string.Empty;

        public string Summary => _summary != null ? _summary.text : string.Empty;

        public bool UndoVisible => _undo != null && _undo.activeSelf;

        /// <summary>Tests: the data folder the backup reads and writes (the game's own by default).</summary>
        public BackupService Service => _backup ??= new BackupService();

        private void Awake() => Build();

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
                    CancelRestore();
                }
                else
                {
                    Close();
                }
            }

            if (_sealing != null && _sealing.IsCompleted)
            {
                Task<byte[]> done = _sealing;
                _sealing = null;
                if (done.IsFaulted)
                {
                    SetStatus("The backup couldn't be made: " + done.Exception?.GetBaseException().Message);
                }
                else
                {
                    BackupFiles.Save(done.Result, BackupRules.FileName(DateTime.Now), OnSaved);
                }
            }

            if (_opening != null && _opening.IsCompleted)
            {
                Task<(BackupError, BackupArchive)> done = _opening;
                _opening = null;
                (BackupError error, BackupArchive archive) = done.IsFaulted ? (BackupError.WrongKeyOrDamaged, null) : done.Result;
                if (error != BackupError.None)
                {
                    SetStatus(BackupRules.Explain(error));
                    return;
                }

                AskToRestore(archive);
            }
        }

        public void Open()
        {
            _overlay.gameObject.SetActive(true);
            _overlay.SetAsLastSibling();
            InputBlockers.Register(_overlay);
            _confirm.gameObject.SetActive(false);
            _undo.SetActive(Service.CanUndo);
            SetStatus(string.Empty);
        }

        public void Close()
        {
            InputBlockers.Unregister(_overlay);
            _overlay.gameObject.SetActive(false);
            _password.text = string.Empty;
            _pendingRestore = null;
        }

        public void SetPassword(string password) => _password.text = password ?? string.Empty;

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
            _sealing = Task.Run(() => BackupCrypto.SealWithPassword(archive, password));
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

            BackupFiles.Open((data, error) =>
            {
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
                _opening = Task.Run(() =>
                {
                    BackupError e = BackupCrypto.OpenWithPassword(data, password, out BackupArchive a);
                    return (e, a);
                });
            });
        }

        /// <summary>Same as tapping Restore on the confirm step: writes the backup and reloads the scene.</summary>
        public bool ConfirmRestore()
        {
            if (_pendingRestore == null)
            {
                return false;
            }

            BackupArchive archive = _pendingRestore;
            _pendingRestore = null;
            _confirm.gameObject.SetActive(false);
            if (!Service.Apply(archive, out string error))
            {
                SetStatus(error);
                return false;
            }

            Debug.Log("[Backup] restored; reloading the scene");
            Reload();
            return true;
        }

        public void CancelRestore()
        {
            _pendingRestore = null;
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

        private void AskToRestore(BackupArchive archive)
        {
            if (Service.Check(archive, out BackupSummary summary) is string problem)
            {
                SetStatus(problem);
                return;
            }

            _pendingRestore = archive;
            _summary.text = summary.Describe() + "\n\nThis replaces what's on this phone now. You can undo it afterwards.";
            _confirm.gameObject.SetActive(true);
            _confirm.SetAsLastSibling();
            SetStatus(string.Empty);
        }

        private void OnSaved(string error)
        {
            if (error == null)
            {
                SetStatus("Not saved.");
            }
            else if (error.Length == 0)
            {
                SetStatus("Backup saved. Keep the password: without it the file can't be opened.");
            }
            else
            {
                SetStatus(error);
            }
        }

        private void Reload()
        {
            Close();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
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
            MakeButton(_overlay, "Close", new Vector2(0f, 1f), new Vector2(28f, -20f), 220f, UiTheme.IndigoLight, Close);

            RectTransform intro = Area(_overlay, "Intro", 0f, 1f, 1f, 1f, new Vector2(40f, -UiTheme.MinTouch - 230f), new Vector2(-40f, -UiTheme.MinTouch - 50f));
            TextMeshProUGUI introText = UiKit.Text(intro, "Save your avatar, house, decorations, layouts, shows and settings to a file you keep, in Downloads or "
                + "Drive. Restore it on this phone or a new one. The file is locked with a password you choose. "
                + "\"Remove ads\" comes back with Restore purchases, not with a backup.", UiTheme.SmallText, TextAlignmentOptions.TopLeft, UiTheme.OffWhite);
            introText.textWrappingMode = TextWrappingModes.Normal;

            RectTransform box = Area(_overlay, "Password", 0f, 1f, 1f, 1f, new Vector2(40f, -UiTheme.MinTouch * 2f - 260f), new Vector2(-40f, -UiTheme.MinTouch - 260f));
            Image bg = UiKit.Panel(box, new Color(1f, 1f, 1f, 0.08f));
            RectTransform area = Area(box, "TextArea", 0f, 0f, 1f, 1f, new Vector2(16f, 0f), new Vector2(-16f, 0f));
            area.gameObject.AddComponent<RectMask2D>();
            TextMeshProUGUI placeholder = UiKit.Text(area, $"Backup password (at least {BackupRules.MinPasswordLength} characters)", UiTheme.SmallText, TextAlignmentOptions.Left, UiTheme.Muted);
            TextMeshProUGUI text = UiKit.Text(area, string.Empty, UiTheme.BodyText, TextAlignmentOptions.Left, UiTheme.OffWhite);
            _password = box.gameObject.AddComponent<TMP_InputField>();
            _password.targetGraphic = bg;
            _password.textViewport = area;
            _password.textComponent = text;
            _password.placeholder = placeholder;
            _password.characterLimit = 64;
            _password.lineType = TMP_InputField.LineType.SingleLine;
            _password.contentType = TMP_InputField.ContentType.Password;

            float y = -UiTheme.MinTouch * 2f - 290f;
            MakeButton(_overlay, "Save backup file", new Vector2(0.5f, 1f), new Vector2(0f, y), 560f, UiTheme.Saffron, SaveFile);
            MakeButton(_overlay, "Restore from file", new Vector2(0.5f, 1f), new Vector2(0f, y - UiTheme.MinTouch - 24f), 560f, UiTheme.IndigoLight, RestoreFromFile);
            _undo = MakeButton(_overlay, "Undo last restore", new Vector2(0.5f, 1f), new Vector2(0f, y - 2f * (UiTheme.MinTouch + 24f)), 560f, UiTheme.IndigoLight, () => Undo()).gameObject;

            RectTransform status = Area(_overlay, "Status", 0f, 0f, 1f, 0f, new Vector2(40f, 24f), new Vector2(-40f, 160f));
            _status = UiKit.Text(status, string.Empty, UiTheme.SmallText, TextAlignmentOptions.Center, UiTheme.OffWhite);
            _status.textWrappingMode = TextWrappingModes.Normal;

            _confirm = Area(_overlay, "Confirm", 0.06f, 0.25f, 0.94f, 0.75f, Vector2.zero, Vector2.zero);
            UiKit.Panel(_confirm, new Color(0.14f, 0.09f, 0.3f, 1f));
            RectTransform heading = Area(_confirm, "Heading", 0f, 1f, 1f, 1f, new Vector2(24f, -UiTheme.MinTouch - 20f), new Vector2(-24f, -20f));
            UiKit.Text(heading, "Restore this backup?", UiTheme.TitleText, TextAlignmentOptions.Center, UiTheme.Marigold);
            RectTransform summary = Area(_confirm, "Summary", 0f, 0f, 1f, 1f, new Vector2(32f, UiTheme.MinTouch + 48f), new Vector2(-32f, -UiTheme.MinTouch - 40f));
            _summary = UiKit.Text(summary, string.Empty, UiTheme.SmallText, TextAlignmentOptions.TopLeft, UiTheme.OffWhite);
            _summary.textWrappingMode = TextWrappingModes.Normal;
            MakeButton(_confirm, "Cancel", new Vector2(0f, 0f), new Vector2(24f, 24f), 260f, UiTheme.IndigoLight, CancelRestore);
            MakeButton(_confirm, "Restore", new Vector2(1f, 0f), new Vector2(-24f, 24f), 260f, UiTheme.Saffron, () => ConfirmRestore());
            _confirm.gameObject.SetActive(false);
            _overlay.gameObject.SetActive(false);
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

        private static RectTransform MakeButton(RectTransform parent, string label, Vector2 anchor, Vector2 position, float width, Color background, Action action)
        {
            RectTransform rect = UiKit.Rect(label, parent);
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.sizeDelta = new Vector2(width, UiTheme.MinTouch);
            rect.anchoredPosition = position;
            Image image = UiKit.Panel(rect, background);
            image.sprite = UiKit.Rounded;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            UiKit.Text(rect, label, UiTheme.BodyText, TextAlignmentOptions.Center, UiTheme.OffWhite);
            return rect;
        }
    }
}

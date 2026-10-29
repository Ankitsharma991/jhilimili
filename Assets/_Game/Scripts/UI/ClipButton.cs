using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UtsavNights.Core.Clips;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;

namespace UtsavNights.UI
{
    /// <summary>
    /// The clip button and share sheet (Phase 9 step 6, F14). Record starts a clip of up to 20 seconds; Stop is offered
    /// from 10 seconds. When the clip is ready a sheet shows its first frame and shares to WhatsApp, Instagram or anything
    /// else through the share sheet. Show clips from the designer end up here too.
    /// </summary>
    public sealed class ClipButton : MonoBehaviour
    {
        private const float RefreshSeconds = 0.25f;

        private ClipRecorder _recorder;
        private RectTransform _button;
        private Image _background;
        private TextMeshProUGUI _label;
        private RectTransform _sheet;
        private RawImage _preview;
        private AspectRatioFitter _previewFit;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _status;
        private float _nextRefresh;

        public bool SheetOpen => _sheet != null && _sheet.gameObject.activeSelf;

        public string Label => _label != null ? _label.text : string.Empty;

        public string Status => _status != null ? _status.text : string.Empty;

        public RectTransform Button => _button;

        private void Start()
        {
            Game.Services.TryGet(out _recorder);
            Build();
            if (_recorder != null)
            {
                _recorder.Finished += OnFinished;
                _recorder.StateChanged += _ => Refresh();
            }

            InputBlockers.Register(_button);
            Refresh();
        }

        private void OnDestroy()
        {
            if (_recorder != null)
            {
                _recorder.Finished -= OnFinished;
            }

            InputBlockers.Unregister(_button);
            InputBlockers.Unregister(_sheet);
        }

        private void Update()
        {
            if (_recorder != null && _recorder.IsRecording && Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + RefreshSeconds;
                Refresh();
            }
        }

        /// <summary>Same as tapping the button: record, or stop once the clip is long enough.</summary>
        public void Press()
        {
            if (_recorder == null)
            {
                return;
            }

            if (_recorder.IsRecording)
            {
                _recorder.Stop();
            }
            else if (_recorder.State == ClipRecorder.RecorderState.Idle)
            {
                CloseSheet();
                _recorder.StartRecording(ClipRules.MaxSeconds);
            }

            Refresh();
        }

        /// <summary>Shares the last clip; empty on success, else the reason (also shown on the sheet).</summary>
        public string Share(ShareTarget target)
        {
            string error = ClipSharer.Share(_recorder.Last, target);
            _status.text = string.IsNullOrEmpty(error) ? "Shared." : error;
            return error;
        }

        public void DeleteClip()
        {
            ClipRecorder.DeleteClip(_recorder.Last.Path);
            CloseSheet();
        }

        public void CloseSheet()
        {
            if (_sheet != null)
            {
                InputBlockers.Unregister(_sheet);
                _sheet.gameObject.SetActive(false);
            }
        }

        private void OnFinished(ClipResult clip)
        {
            Refresh();
            _sheet.gameObject.SetActive(true);
            _sheet.SetAsLastSibling();
            InputBlockers.Register(_sheet);
            Texture2D thumb = clip.Ok ? _recorder.Thumbnail : null;
            _preview.texture = thumb;
            _preview.color = thumb != null ? Color.white : new Color(1f, 1f, 1f, 0.06f);
            _previewFit.aspectRatio = thumb != null ? thumb.width / (float)thumb.height : 16f / 9f;
            _title.text = clip.Ok ? $"Your clip · {clip.Seconds:0} s" : "No clip this time";
            _status.text = clip.Ok ? "Share it with family and friends." : clip.Error;
            foreach (Transform child in _sheet.Find("Buttons"))
            {
                child.gameObject.SetActive(clip.Ok || child.name == "Close");
            }
        }

        private void Refresh()
        {
            if (_label == null || _recorder == null)
            {
                return;
            }

            if (_recorder.IsRecording)
            {
                int s = Mathf.FloorToInt(_recorder.Elapsed);
                _label.text = _recorder.CanStop ? $"Stop · 0:{s:00}" : $"● 0:{s:00}";
                _background.color = UiTheme.Magenta;
                _label.color = UiTheme.OffWhite;
            }
            else if (_recorder.State == ClipRecorder.RecorderState.Finishing)
            {
                _label.text = "Saving…";
                _background.color = UiTheme.IndigoLight;
                _label.color = UiTheme.Muted;
            }
            else
            {
                _label.text = "● Record";
                _background.color = UiTheme.IndigoLight;
                _label.color = UiTheme.OffWhite;
            }
        }

        private void Build()
        {
            _button = UiKit.Rect("ClipButton", transform);
            _button.anchorMin = _button.anchorMax = _button.pivot = new Vector2(1f, 0f);
            _button.sizeDelta = new Vector2(280f, UiTheme.MinTouch);
            _button.anchoredPosition = new Vector2(-32f, TrayView.TrayHeight + 150f + UiTheme.MinTouch + 20f); // above Finale
            _background = UiKit.Panel(_button, UiTheme.IndigoLight);
            var button = _button.gameObject.AddComponent<Button>();
            button.targetGraphic = _background;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(Press);
            _label = UiKit.Text(_button, "● Record", UiTheme.BodyText, TextAlignmentOptions.Center, UiTheme.OffWhite);

            _sheet = UiKit.Rect("ClipSheet", transform.parent != null ? transform.parent : transform);
            _sheet.anchorMin = new Vector2(0.05f, 0.18f);
            _sheet.anchorMax = new Vector2(0.95f, 0.82f);
            _sheet.offsetMin = _sheet.offsetMax = Vector2.zero;
            UiKit.Panel(_sheet, new Color(0.09f, 0.06f, 0.2f, 0.98f));

            RectTransform title = Area(_sheet, "Title", 0f, 1f, 1f, 1f, new Vector2(24f, -UiTheme.MinTouch - 20f), new Vector2(-24f, -20f));
            _title = UiKit.Text(title, string.Empty, UiTheme.TitleText, TextAlignmentOptions.Center, UiTheme.Marigold);

            RectTransform frame = Area(_sheet, "Preview", 0f, 0.36f, 1f, 1f, new Vector2(32f, 0f), new Vector2(-32f, -UiTheme.MinTouch - 36f));
            RectTransform pic = UiKit.Rect("Picture", frame);
            pic.anchorMin = Vector2.zero;
            pic.anchorMax = Vector2.one;
            pic.offsetMin = pic.offsetMax = Vector2.zero;
            _preview = pic.gameObject.AddComponent<RawImage>();
            _preview.raycastTarget = false;
            _previewFit = pic.gameObject.AddComponent<AspectRatioFitter>();
            _previewFit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

            RectTransform status = Area(_sheet, "Status", 0f, 0.26f, 1f, 0.35f, new Vector2(24f, 0f), new Vector2(-24f, 0f));
            _status = UiKit.Text(status, string.Empty, UiTheme.SmallText, TextAlignmentOptions.Center, UiTheme.OffWhite);

            RectTransform buttons = Area(_sheet, "Buttons", 0f, 0f, 1f, 0.25f, new Vector2(24f, 24f), new Vector2(-24f, 0f));
            SheetButton(buttons, "WhatsApp", 0f, 0.5f, 0.5f, 1f, new Color(0.15f, 0.62f, 0.35f), () => Share(ShareTarget.WhatsApp));
            SheetButton(buttons, "Instagram", 0.5f, 0.5f, 1f, 1f, UiTheme.Magenta, () => Share(ShareTarget.Instagram));
            SheetButton(buttons, "More…", 0f, 0f, 0.34f, 0.5f, UiTheme.IndigoLight, () => Share(ShareTarget.Other));
            SheetButton(buttons, "Delete", 0.34f, 0f, 0.67f, 0.5f, UiTheme.IndigoLight, DeleteClip);
            SheetButton(buttons, "Close", 0.67f, 0f, 1f, 0.5f, UiTheme.Saffron, CloseSheet);
            _sheet.gameObject.SetActive(false);
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

        private static void SheetButton(RectTransform parent, string label, float x0, float y0, float x1, float y1, Color background, System.Action action)
        {
            RectTransform rect = Area(parent, label, x0, y0, x1, y1, new Vector2(6f, 6f), new Vector2(-6f, -6f));
            Image image = UiKit.Panel(rect, background);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            UiKit.Text(rect, label, UiTheme.BodyText, TextAlignmentOptions.Center, UiTheme.OffWhite);
        }
    }
}

using System;
using UnityEngine;
using UnityEngine.UI;
using UtsavNights.Core;
using UtsavNights.Core.Monetisation;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Development stand-in for Google UMP. Simulates a debug geography (EEA or elsewhere, switched from
    /// the developer overlay) and a plain consent form, and stores the choice like UMP does, so the
    /// whole consent path can be tested with no SDK or network.
    /// </summary>
    public sealed class DevConsentBackend : MonoBehaviour, IConsentBackend
    {
        public const string GeographyKey = "utsav.dev.consent.eea";
        public const string ChoiceKey = "utsav.dev.consent.choice";

        private GameObject _form;
        private Action<string> _done;

        public string Name => "development";

        public ConsentStatus Status { get; private set; } = ConsentStatus.Unknown;

        public bool CanRequestAds => Status == ConsentStatus.NotRequired || Status == ConsentStatus.Obtained;

        public bool PrivacyOptionsRequired => InEea;

        /// <summary>Debug geography: true simulates the EEA (a consent choice is required).</summary>
        public bool InEea
        {
            get => PlayerPrefs.GetInt(GeographyKey, 0) == 1;
            set => PlayerPrefs.SetInt(GeographyKey, value ? 1 : 0);
        }

        /// <summary>The stored choice: "agree", "decline" or empty.</summary>
        public string Choice => PlayerPrefs.GetString(ChoiceKey, string.Empty);

        public bool FormVisible => _form != null && _form.activeSelf;

        public void Gather(Action<string> done)
        {
            if (!InEea)
            {
                Status = ConsentStatus.NotRequired;
                done?.Invoke(null);
                return;
            }

            if (!string.IsNullOrEmpty(Choice))
            {
                Status = ConsentStatus.Obtained;
                done?.Invoke(null);
                return;
            }

            Status = ConsentStatus.Required;
            ShowForm(done);
        }

        public void ShowPrivacyOptions(Action<string> done)
        {
            if (!PrivacyOptionsRequired)
            {
                done?.Invoke(null);
                return;
            }

            ShowForm(done);
        }

        public void Reset()
        {
            PlayerPrefs.DeleteKey(ChoiceKey);
            Status = ConsentStatus.Unknown;
        }

        /// <summary>Same as tapping a button on the form.</summary>
        public void Answer(bool agree)
        {
            if (!FormVisible)
            {
                return;
            }

            PlayerPrefs.SetString(ChoiceKey, agree ? "agree" : "decline");
            PlayerPrefs.Save();
            Status = ConsentStatus.Obtained;
            _form.SetActive(false);
            InputBlockers.Unregister((RectTransform)_form.transform);
            Action<string> done = _done;
            _done = null;
            done?.Invoke(null);
        }

        private void ShowForm(Action<string> done)
        {
            if (FormVisible)
            {
                done?.Invoke("form already showing");
                return;
            }

            if (_form == null)
            {
                Build();
            }

            _done = done;
            _form.SetActive(true);
            InputBlockers.Register((RectTransform)_form.transform);
        }

        private void Build()
        {
            _form = new GameObject("DevConsentForm", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _form.transform.SetParent(transform, false);
            var canvas = _form.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 310;
            var scaler = _form.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = SpikeMath.DpToPx(1f, Screen.dpi);

            Image background = Child<Image>("Background", _form.transform, Vector2.zero, Vector2.one, Vector2.zero);
            background.color = new Color(0.95f, 0.95f, 0.95f, 1f);
            Text text = Child<Text>("Text", _form.transform, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.62f), new Vector2(300f, 220f));
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 16;
            text.color = Color.black;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = "Development consent form\n(stands in for Google's consent message in the EEA and UK)\n\n" +
                        "Utsav Nights asks for consent to use your data for ads and measurement.";
            AddButton("Consent", new Vector2(0.5f, 0.32f), () => Answer(true));
            AddButton("Do not consent", new Vector2(0.5f, 0.22f), () => Answer(false));
            _form.SetActive(false);
        }

        private void AddButton(string label, Vector2 anchor, Action onClick)
        {
            Image image = Child<Image>(label, _form.transform, anchor, anchor, new Vector2(240f, 50f));
            image.color = new Color(0.1f, 0.35f, 0.8f, 1f);
            image.gameObject.AddComponent<Button>().onClick.AddListener(() => onClick());
            Text text = Child<Text>("Label", image.transform, Vector2.zero, Vector2.one, Vector2.zero);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = label;
            text.raycastTarget = false;
        }

        private static T Child<T>(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 size) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(T));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.sizeDelta = size;
            return go.GetComponent<T>();
        }
    }
}

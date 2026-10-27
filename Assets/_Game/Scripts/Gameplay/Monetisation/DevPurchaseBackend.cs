using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UtsavNights.Core;
using UtsavNights.Core.Monetisation;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Development store: a plain purchase dialog (Buy, Pending, Cancel) and an "owned" list kept like
    /// Google Play keeps it, so purchase, restore and refund can be tested with no account or network.
    /// Nothing is charged.
    /// </summary>
    public sealed class DevPurchaseBackend : MonoBehaviour, IPurchaseBackend
    {
        public const string OwnedKey = "utsav.dev.store.owned";

        private GameObject _dialog;
        private Action<PurchaseResult> _done;
        private string _sku;

        public string Name => "development";

        public bool IsReady { get; private set; }

        // The development store completes every payment at once, so nothing ever arrives later.
        public event Action<string> PurchaseCompletedLater
        {
            add { }
            remove { }
        }

        public bool DialogVisible => _dialog != null && _dialog.activeSelf;

        /// <summary>Test hook: the next query fails as if offline.</summary>
        public bool FailNextQuery { get; set; }

        /// <summary>What the simulated Google account owns (survives app data clears in real life; here, PlayerPrefs).</summary>
        public bool Owns(string sku) => PlayerPrefs.GetString(OwnedKey, string.Empty) == sku;

        public void Initialize(Action<string> done)
        {
            IsReady = true;
            done?.Invoke(null);
        }

        public string PriceText(string sku) => IsReady ? "INR 99 (test)" : string.Empty;

        public void Buy(string sku, Action<PurchaseResult> done)
        {
            if (Owns(sku))
            {
                done?.Invoke(PurchaseResult.AlreadyOwned);
                return;
            }

            if (DialogVisible)
            {
                done?.Invoke(PurchaseResult.Failed);
                return;
            }

            if (_dialog == null)
            {
                Build();
            }

            _sku = sku;
            _done = done;
            _dialog.SetActive(true);
            InputBlockers.Register((RectTransform)_dialog.transform);
        }

        public void QueryOwned(Action<bool, IReadOnlyCollection<string>> done)
        {
            if (FailNextQuery)
            {
                FailNextQuery = false;
                done?.Invoke(false, null);
                return;
            }

            string owned = PlayerPrefs.GetString(OwnedKey, string.Empty);
            done?.Invoke(true, string.IsNullOrEmpty(owned) ? Array.Empty<string>() : new[] { owned });
        }

        /// <summary>Simulates a refund in Play Console: the account no longer owns it.</summary>
        public void Refund()
        {
            PlayerPrefs.DeleteKey(OwnedKey);
            PlayerPrefs.Save();
        }

        /// <summary>Same as tapping a dialog button.</summary>
        public void Choose(PurchaseResult result)
        {
            if (!DialogVisible)
            {
                return;
            }

            if (result == PurchaseResult.Purchased)
            {
                PlayerPrefs.SetString(OwnedKey, _sku);
                PlayerPrefs.Save();
            }

            _dialog.SetActive(false);
            InputBlockers.Unregister((RectTransform)_dialog.transform);
            Action<PurchaseResult> done = _done;
            _done = null;
            done?.Invoke(result);
        }

        private void Build()
        {
            _dialog = new GameObject("DevStoreDialog", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _dialog.transform.SetParent(transform, false);
            var canvas = _dialog.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 320;
            var scaler = _dialog.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = SpikeMath.DpToPx(1f, Screen.dpi);

            Image dim = Child<Image>("Dim", _dialog.transform, Vector2.zero, Vector2.one, Vector2.zero);
            dim.color = new Color(0f, 0f, 0f, 0.6f);
            Image panel = Child<Image>("Panel", _dialog.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(320f, 360f));
            panel.color = Color.white;
            Text text = Child<Text>("Text", panel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(290f, 130f));
            ((RectTransform)text.transform).anchoredPosition = new Vector2(0f, -75f);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 16;
            text.color = Color.black;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = "Development store (nothing is charged)\n\nRemove ads and unlock all\nINR 99 (test)";
            AddButton(panel.transform, "Buy", -170f, () => Choose(PurchaseResult.Purchased));
            AddButton(panel.transform, "Pay later (pending)", -230f, () => Choose(PurchaseResult.Pending));
            AddButton(panel.transform, "Cancel", -290f, () => Choose(PurchaseResult.Cancelled));
            _dialog.SetActive(false);
        }

        private static void AddButton(Transform parent, string label, float y, Action onClick)
        {
            Image image = Child<Image>(label, parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(260f, 48f));
            ((RectTransform)image.transform).anchoredPosition = new Vector2(0f, y);
            image.color = new Color(0.1f, 0.5f, 0.3f, 1f);
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

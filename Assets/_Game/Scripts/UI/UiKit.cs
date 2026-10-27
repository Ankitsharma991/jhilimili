using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UtsavNights.UI
{
    /// <summary>Small helpers for building UI from code with consistent styling.</summary>
    public static class UiKit
    {
        private static Sprite _rounded;
        private static Sprite _circle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _rounded = null;
            _circle = null;
        }

        /// <summary>9-sliced rounded rectangle, generated once.</summary>
        public static Sprite Rounded => _rounded != null ? _rounded : (_rounded = CreateRounded(64, 24));

        public static Sprite Circle => _circle != null ? _circle : (_circle = CreateRounded(64, 32));

        /// <summary>
        /// New child RectTransform with a zero size offset, so stretching anchors fill the parent exactly
        /// (Unity's default 100 x 100 would overflow a stretched parent and get clipped).
        /// </summary>
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = Vector2.zero;
            return rect;
        }

        public static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        public static Image Panel(RectTransform rect, Color color, bool raycast = true)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = Rounded;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        /// <summary>
        /// The player's Android font size (Settings > Display > Font size), applied to all UI text built after
        /// it is read, capped at 1.3x so layouts still fit (auto-sizing labels shrink if needed).
        /// </summary>
        public static float TextScale
        {
            get
            {
                float scale = 1f;
                try
                {
                    scale = UnityEngine.Accessibility.AccessibilitySettings.fontScale;
                }
                catch (System.Exception)
                {
                    // Not supported on this platform.
                }

                return Mathf.Clamp(float.IsNaN(scale) || scale <= 0f ? 1f : scale, 1f, 1.3f);
            }
        }

        public static TextMeshProUGUI Text(Transform parent, string value, float size, TextAlignmentOptions alignment, Color color)
        {
            size *= TextScale;
            RectTransform rect = Rect("Text", parent);
            Stretch(rect);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            // TMP_Settings.defaultFontAsset throws when the TMP Settings asset is missing.
            if (TMP_Settings.instance != null && TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }

            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private static Sprite CreateRounded(int size, int radius)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    float a = Mathf.Clamp01(radius - d + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
        }
    }
}

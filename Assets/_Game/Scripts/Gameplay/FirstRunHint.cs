using UnityEngine;
using UnityEngine.UI;
using UtsavNights.Core;
using UtsavNights.Core.Runtime;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// First-run teaching hint (PRD 6.1, 12): an animated finger slides up until the flame would sit on the
    /// fuse, a ring pulses on the fuse, and one short line explains it. Dismissed by the first ignition and
    /// never shown again. Never blocks touches.
    /// </summary>
    public sealed class FirstRunHint : MonoBehaviour
    {
        public const string DonePrefKey = "hint.lighter.done";

        [SerializeField] private Camera targetCamera;
        [SerializeField] private string message = "Drag the flame to the fuse and hold";
        [SerializeField] private float fingerOffsetDp = 64f;
        [SerializeField] private float cycleSeconds = 1.8f;
        [SerializeField] private float fadeSeconds = 0.4f;

        private RectTransform _finger;
        private RectTransform _ring;
        private Image _fingerImage;
        private Image _ringImage;
        private Text _label;
        private CanvasGroup _group;
        private Canvas _canvas;
        private float _time;
        private float _fade = -1f;

        public bool IsShowing => _canvas != null && _canvas.enabled;

        public void Configure(Camera cameraToUse) => targetCamera = cameraToUse;

        private void Start()
        {
            if (PlayerPrefs.GetInt(DonePrefKey, 0) == 1)
            {
                enabled = false;
                return;
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            BuildUi();
            Game.Events.Subscribe<CrackerIgnitedEvent>(OnIgnited);
        }

        private void OnDestroy() => Game.Events.Unsubscribe<CrackerIgnitedEvent>(OnIgnited);

        private void OnIgnited(CrackerIgnitedEvent e)
        {
            PlayerPrefs.SetInt(DonePrefKey, 1);
            PlayerPrefs.Save();
            _fade = 0f;
        }

        private void Update()
        {
            if (_canvas == null || targetCamera == null)
            {
                return;
            }

            if (_fade >= 0f)
            {
                _fade += Time.unscaledDeltaTime;
                _group.alpha = 1f - Mathf.Clamp01(_fade / fadeSeconds);
                if (_fade >= fadeSeconds)
                {
                    _canvas.enabled = false;
                    enabled = false;
                }

                return;
            }

            IIgnitionTarget target = FirstIgnitable();
            if (target == null)
            {
                _group.alpha = 0f;
                return;
            }

            Vector3 fuse = targetCamera.WorldToScreenPoint(target.FuseWorldPosition);
            if (fuse.z <= 0f)
            {
                _group.alpha = 0f;
                return;
            }

            _group.alpha = 1f;
            _time += Time.unscaledDeltaTime;
            float t = (_time % cycleSeconds) / cycleSeconds;
            float scale = _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
            float offsetPx = SpikeMath.DpToPx(fingerOffsetDp, Screen.dpi);

            // Finger travels from well below the fuse to where the flame would touch it, then rests.
            Vector2 end = new Vector2(fuse.x, fuse.y - offsetPx);
            Vector2 start = end + new Vector2(-offsetPx * 0.6f, -offsetPx * 2.2f);
            float move = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.6f));
            _finger.anchoredPosition = Vector2.Lerp(start, end, move) / scale;
            _fingerImage.color = new Color(1f, 1f, 1f, t < 0.85f ? 0.55f : Mathf.Lerp(0.55f, 0f, (t - 0.85f) / 0.15f));

            _ring.anchoredPosition = new Vector2(fuse.x, fuse.y) / scale;
            float pulse = 0.5f + 0.5f * Mathf.Sin(_time * 6f);
            _ring.localScale = Vector3.one * (1f + 0.25f * pulse);
            _ringImage.color = new Color(1f, 0.82f, 0.35f, 0.45f + 0.45f * pulse);

            _label.rectTransform.anchoredPosition = new Vector2(0f, (fuse.y - offsetPx * 3.4f) / scale);
        }

        private static IIgnitionTarget FirstIgnitable()
        {
            var all = IgnitionTargets.Shared.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].CanIgnite)
                {
                    return all[i];
                }
            }

            return null;
        }

        private void BuildUi()
        {
            var canvasGo = new GameObject("FirstRunHint", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 50;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0f;
            _group = canvasGo.GetComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _group.alpha = 0f;

            // A thin hollow ring so the fuse it points at stays visible.
            _ring = CreateImage("Ring", canvasGo.transform, CreateCircleSprite(0.8f), 130f, out _ringImage);
            _finger = CreateImage("Finger", canvasGo.transform, CreateCircleSprite(0f), 80f, out _fingerImage);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(Shadow));
            labelGo.transform.SetParent(canvasGo.transform, false);
            _label = labelGo.GetComponent<Text>();
            _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.fontSize = 46;
            _label.alignment = TextAnchor.MiddleCenter;
            _label.color = new Color(1f, 0.96f, 0.88f);
            _label.text = message;
            _label.raycastTarget = false;
            RectTransform labelRect = _label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 0f);
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.sizeDelta = new Vector2(-80f, 120f);
        }

        private static RectTransform CreateImage(string name, Transform parent, Sprite sprite, float size, out Image image)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            return rect;
        }

        /// <param name="hole">0 for a filled dot; 0..1 inner radius for a ring.</param>
        private static Sprite CreateCircleSprite(float hole)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((1f - d) * 6f);
                    if (hole > 0f)
                    {
                        a *= Mathf.Clamp01((d - hole) * 12f);
                    }

                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}

using UnityEngine;

namespace UtsavNights.UI
{
    /// <summary>
    /// Caps a full-width panel at a maximum width (reference units) and centres it, so sheets and tips
    /// do not stretch into long lines on tablets and landscape screens.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class MaxWidth : MonoBehaviour
    {
        [SerializeField] private float maxWidth = 1100f;

        private RectTransform _rect;
        private RectTransform _parent;
        private float _left;
        private float _right;
        private float _lastParentWidth = -1f;

        public static MaxWidth Apply(RectTransform rect, float max)
        {
            var limiter = rect.gameObject.AddComponent<MaxWidth>();
            limiter.maxWidth = max;
            limiter.Capture();
            return limiter;
        }

        private void OnEnable() => Capture();

        private void Capture()
        {
            _rect = (RectTransform)transform;
            _parent = _rect.parent as RectTransform;
            // Remember the designed side margins (offsets from a stretched layout).
            _left = _rect.offsetMin.x;
            _right = -_rect.offsetMax.x;
            _lastParentWidth = -1f;
        }

        private void LateUpdate()
        {
            if (_parent == null)
            {
                return;
            }

            float parentWidth = _parent.rect.width;
            if (Mathf.Approximately(parentWidth, _lastParentWidth))
            {
                return;
            }

            _lastParentWidth = parentWidth;
            float anchorSpan = (_rect.anchorMax.x - _rect.anchorMin.x) * parentWidth;
            float designed = anchorSpan - _left - _right;
            float extra = Mathf.Max(0f, designed - maxWidth) * 0.5f;
            _rect.offsetMin = new Vector2(_left + extra, _rect.offsetMin.y);
            _rect.offsetMax = new Vector2(-(_right + extra), _rect.offsetMax.y);
        }
    }
}

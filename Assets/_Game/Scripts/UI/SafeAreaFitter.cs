using UnityEngine;

namespace UtsavNights.UI
{
    /// <summary>Keeps its RectTransform inside the device safe area (notches, gesture bars; PRD 12).</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private Rect _applied;
        private Vector2Int _screen;

        private void OnEnable() => Apply();

        private void Update()
        {
            if (Screen.safeArea != _applied || Screen.width != _screen.x || Screen.height != _screen.y)
            {
                Apply();
            }
        }

        private void Apply()
        {
            _applied = Screen.safeArea;
            _screen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(_applied.xMin / Screen.width, _applied.yMin / Screen.height);
            rect.anchorMax = new Vector2(_applied.xMax / Screen.width, _applied.yMax / Screen.height);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}

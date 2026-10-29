using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UtsavNights.UI
{
    /// <summary>The joystick's touch area: reports the push as -1..1 on each axis while held, zero when let go.</summary>
    public sealed class StickInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public event Action<Vector2> Moved;

        public void OnPointerDown(PointerEventData e) => Report(e);

        public void OnDrag(PointerEventData e) => Report(e);

        public void OnPointerUp(PointerEventData e) => Moved?.Invoke(Vector2.zero);

        private void Report(PointerEventData e)
        {
            var rect = (RectTransform)transform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera, out Vector2 local))
            {
                Vector2 v = local / (rect.rect.width * 0.5f);
                Moved?.Invoke(Vector2.ClampMagnitude(v, 1f));
            }
        }
    }
}

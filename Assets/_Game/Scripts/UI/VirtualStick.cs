using UnityEngine;
using UnityEngine.EventSystems;

namespace UtsavNights.UI
{
    /// <summary>
    /// A thumbstick for the left thumb (Phase 12 step 1). It follows the thumb: wherever it first touches inside the area
    /// becomes the centre, so the stick never sits under the wrong place. <see cref="Value"/> is -1 to 1 on each axis.
    /// </summary>
    public sealed class VirtualStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public const float Reach = 110f;

        private RectTransform _area;
        private RectTransform _base;
        private RectTransform _knob;
        private Vector2 _centre;
        private int _pointer = int.MinValue;

        public Vector2 Value { get; private set; }

        public bool Held => _pointer != int.MinValue;

        public void Configure(RectTransform area, RectTransform stickBase, RectTransform knob)
        {
            _area = area;
            _base = stickBase;
            _knob = knob;
        }

        public void OnPointerDown(PointerEventData e)
        {
            _pointer = e.pointerId;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, e.position, e.pressEventCamera, out _centre);
            _base.anchoredPosition = _centre;
            Track(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId == _pointer)
            {
                Track(e);
            }
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != _pointer)
            {
                return;
            }

            _pointer = int.MinValue;
            Value = Vector2.zero;
            _knob.anchoredPosition = Vector2.zero;
        }

        /// <summary>Tests and gamepads: sets the push directly.</summary>
        public void Set(Vector2 value)
        {
            Value = Vector2.ClampMagnitude(value, 1f);
            _knob.anchoredPosition = Value * Reach;
        }

        private void Track(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, e.position, e.pressEventCamera, out Vector2 local);
            Vector2 offset = Vector2.ClampMagnitude(local - _centre, Reach);
            _knob.anchoredPosition = offset;
            Value = offset / Reach;
        }
    }
}

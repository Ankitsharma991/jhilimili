using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UtsavNights.UI
{
    /// <summary>The see-through surface over a room: a drag turns the view, a tap places or picks (Phase 11 step 2).</summary>
    public sealed class InteriorTouch : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private const float TapSlop = 18f;
        private Vector2 _down;
        private bool _dragged;

        public event Action<Vector2> Dragged;

        public event Action<Vector2> Tapped;

        public void OnPointerDown(PointerEventData e)
        {
            _down = e.position;
            _dragged = false;
        }

        public void OnDrag(PointerEventData e)
        {
            if ((e.position - _down).magnitude > TapSlop)
            {
                _dragged = true;
            }

            if (_dragged)
            {
                Dragged?.Invoke(e.delta);
            }
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!_dragged)
            {
                Tapped?.Invoke(e.position);
            }
        }
    }
}

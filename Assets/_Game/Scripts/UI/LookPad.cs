using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UtsavNights.UI
{
    /// <summary>The rest of the screen in Explore mode: a drag turns the camera; a pinch with two fingers zooms (Phase 12 step 1).</summary>
    public sealed class LookPad : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        private int _first = int.MinValue;
        private int _second = int.MinValue;
        private Vector2 _a;
        private Vector2 _b;

        public event Action<Vector2> Looked;

        /// <summary>Metres: positive pulls the camera back.</summary>
        public event Action<float> Zoomed;

        public void OnPointerDown(PointerEventData e)
        {
            if (_first == int.MinValue)
            {
                _first = e.pointerId;
                _a = e.position;
            }
            else if (_second == int.MinValue)
            {
                _second = e.pointerId;
                _b = e.position;
            }
        }

        public void OnDrag(PointerEventData e)
        {
            if (_second != int.MinValue && (e.pointerId == _first || e.pointerId == _second))
            {
                float before = (_a - _b).magnitude;
                if (e.pointerId == _first)
                {
                    _a = e.position;
                }
                else
                {
                    _b = e.position;
                }

                Zoomed?.Invoke((before - (_a - _b).magnitude) * 0.01f);
                return;
            }

            if (e.pointerId == _first)
            {
                _a = e.position;
                Looked?.Invoke(e.delta);
            }
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId == _first)
            {
                _first = _second;
                _a = _b;
                _second = int.MinValue;
            }
            else if (e.pointerId == _second)
            {
                _second = int.MinValue;
            }
        }
    }
}

using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UtsavNights.UI
{
    /// <summary>A button that reports press and release, for "hold to sprint" (Phase 12 step 2).</summary>
    public sealed class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public event Action Pressed;

        public event Action Released;

        public bool Held { get; private set; }

        public void OnPointerDown(PointerEventData e)
        {
            Held = true;
            Pressed?.Invoke();
        }

        public void OnPointerUp(PointerEventData e)
        {
            Held = false;
            Released?.Invoke();
        }
    }
}

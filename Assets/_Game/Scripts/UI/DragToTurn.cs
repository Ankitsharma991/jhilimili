using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UtsavNights.UI
{
    /// <summary>Reports sideways drags across a UI element as degrees to turn (the avatar creator's preview).</summary>
    public sealed class DragToTurn : MonoBehaviour, IDragHandler
    {
        public event Action<float> Turned;

        public void OnDrag(PointerEventData eventData) => Turned?.Invoke(-eventData.delta.x * 0.4f);
    }
}

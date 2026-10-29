using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UtsavNights.UI
{
    /// <summary>
    /// A cracker chip in the show designer's palette that can be dragged onto a launch pad (PRD 12 "drag items onto launch
    /// pads"). A copy of the chip follows the finger; on release the designer is told where it was dropped.
    /// </summary>
    public sealed class ShowChipDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private RectTransform _ghost;
        private Canvas _canvas;

        /// <summary>The cracker id this chip adds.</summary>
        public string Cracker { get; set; }

        /// <summary>Called with the cracker id and the screen point it was released at.</summary>
        public Action<string, Vector2> Dropped { get; set; }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _canvas = GetComponentInParent<Canvas>();
            if (_canvas == null)
            {
                return;
            }

            _ghost = (RectTransform)Instantiate(gameObject, _canvas.transform).transform;
            Destroy(_ghost.GetComponent<ShowChipDrag>());
            foreach (UnityEngine.UI.Graphic g in _ghost.GetComponentsInChildren<UnityEngine.UI.Graphic>())
            {
                g.raycastTarget = false;
            }

            _ghost.sizeDelta = ((RectTransform)transform).rect.size;
            _ghost.anchorMin = _ghost.anchorMax = _ghost.pivot = new Vector2(0.5f, 0.5f);
            Follow(eventData);
        }

        public void OnDrag(PointerEventData eventData) => Follow(eventData);

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_ghost != null)
            {
                Destroy(_ghost.gameObject);
                _ghost = null;
            }

            Dropped?.Invoke(Cracker, eventData.position);
        }

        private void Follow(PointerEventData eventData)
        {
            if (_ghost == null)
            {
                return;
            }

            Camera cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_canvas.transform, eventData.position, cam, out Vector2 local))
            {
                _ghost.localPosition = local;
            }
        }
    }
}

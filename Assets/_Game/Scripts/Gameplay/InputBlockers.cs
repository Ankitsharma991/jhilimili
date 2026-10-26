using System.Collections.Generic;
using UnityEngine;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Screen areas where a touch belongs to UI, not the lighter (tray, buttons). UI panels register
    /// their RectTransform; a touch that starts inside one never lights the lighter.
    /// </summary>
    public static class InputBlockers
    {
        private static readonly List<RectTransform> Rects = new List<RectTransform>();
        private static readonly List<Camera> Cameras = new List<Camera>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Rects.Clear();
            Cameras.Clear();
        }

        /// <param name="uiCamera">Null for Screen Space Overlay canvases.</param>
        public static void Register(RectTransform rect, Camera uiCamera = null)
        {
            if (rect != null && !Rects.Contains(rect))
            {
                Rects.Add(rect);
                Cameras.Add(uiCamera);
            }
        }

        public static void Unregister(RectTransform rect)
        {
            int i = Rects.IndexOf(rect);
            if (i >= 0)
            {
                Rects.RemoveAt(i);
                Cameras.RemoveAt(i);
            }
        }

        public static bool IsBlocked(Vector2 screenPoint)
        {
            for (int i = 0; i < Rects.Count; i++)
            {
                RectTransform rect = Rects[i];
                if (rect != null && rect.gameObject.activeInHierarchy &&
                    RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, Cameras[i]))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

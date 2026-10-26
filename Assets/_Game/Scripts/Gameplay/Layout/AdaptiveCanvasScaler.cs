using UnityEngine;
using UnityEngine.UI;
using UtsavNights.Core.Layout;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Sizes a canvas in physical units (dp) so UI looks the same on phones and tablets, capped so the
    /// layout always fits (<see cref="ScreenFit"/>). Re-applies when the resolution or orientation changes.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    [ExecuteAlways]
    public sealed class AdaptiveCanvasScaler : MonoBehaviour
    {
        private CanvasScaler _scaler;
        private int _width;
        private int _height;

        public float Scale { get; private set; } = 1f;

        private void OnEnable()
        {
            _scaler = GetComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            Apply();
        }

        private void Update()
        {
            if (Screen.width != _width || Screen.height != _height)
            {
                Apply();
            }
        }

        public void Apply()
        {
            _width = Screen.width;
            _height = Screen.height;
            Scale = ScreenFit.CanvasScale(_width, _height, Screen.dpi);
            if (_scaler != null)
            {
                _scaler.scaleFactor = Scale;
            }
        }
    }
}

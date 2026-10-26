using UnityEngine;
using UtsavNights.Core.Layout;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Keeps the courtyard's width in view on every aspect ratio: tall phones (9:21) widen the vertical FOV
    /// so the sides are not cropped; tablets and landscape keep the designed FOV.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class AdaptiveCameraFov : MonoBehaviour
    {
        [SerializeField] private float designVerticalFov = 60f;
        [SerializeField] private float minHorizontalFov = 36f;
        [SerializeField] private float maxVerticalFov = 74f;

        private Camera _camera;
        private float _aspect = -1f;

        public void Configure(float designVertical, float minHorizontal, float maxVertical)
        {
            designVerticalFov = designVertical;
            minHorizontalFov = minHorizontal;
            maxVerticalFov = maxVertical;
        }

        private void Awake() => _camera = GetComponent<Camera>();

        private void LateUpdate()
        {
            float aspect = _camera.aspect;
            if (Mathf.Approximately(aspect, _aspect))
            {
                return;
            }

            _aspect = aspect;
            _camera.fieldOfView = ScreenFit.VerticalFov(aspect, designVerticalFov, minHorizontalFov, maxVerticalFov);
        }
    }
}

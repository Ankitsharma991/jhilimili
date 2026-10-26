using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace UtsavNights.Spikes
{
    /// <summary>
    /// Cycles the Low / Mid / High quality levels created by Utsav/Setup. Bloom (camera post-processing)
    /// is enabled on Mid and High only (PRD 12, 13.1). The automatic governor comes in Phase 2.
    /// </summary>
    public sealed class QualityTierSwitcher : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;

        private string[] _names;

        public string CurrentName
        {
            get
            {
                int level = QualitySettings.GetQualityLevel();
                return _names != null && level >= 0 && level < _names.Length ? _names[level] : "?";
            }
        }

        public void Configure(Camera cameraToControl)
        {
            targetCamera = cameraToControl;
        }

        private void Awake()
        {
            _names = QualitySettings.names;
        }

        private void Start()
        {
            Apply(QualitySettings.GetQualityLevel());
        }

        public void Cycle()
        {
            if (_names == null || _names.Length == 0)
            {
                return;
            }

            Apply((QualitySettings.GetQualityLevel() + 1) % _names.Length);
        }

        private void Apply(int level)
        {
            QualitySettings.SetQualityLevel(level, true);
            if (targetCamera != null)
            {
                targetCamera.GetUniversalAdditionalCameraData().renderPostProcessing = level > 0;
            }

            SpikeReport.CurrentTier = CurrentName;
        }
    }
}

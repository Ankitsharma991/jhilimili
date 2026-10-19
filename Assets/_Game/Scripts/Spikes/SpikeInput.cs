using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;

namespace UtsavNights.Spikes
{
    public enum SpikeMode
    {
        Lighter,
        AudioTap,
    }

    /// <summary>Shared input state for the spikes: Enhanced Touch plus the current interaction mode.</summary>
    public static class SpikeInput
    {
        private static bool _enabled;

        public static SpikeMode Mode { get; private set; } = SpikeMode.Lighter;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _enabled = false;
            Mode = SpikeMode.Lighter;
        }

        public static void EnsureEnabled()
        {
            if (_enabled)
            {
                return;
            }

            EnhancedTouchSupport.Enable();
#if UNITY_EDITOR
            // Lets the mouse drive touches in the editor Game view.
            TouchSimulation.Enable();
#endif
            _enabled = true;
        }

        public static SpikeMode CycleMode()
        {
            Mode = Mode == SpikeMode.Lighter ? SpikeMode.AudioTap : SpikeMode.Lighter;
            return Mode;
        }
    }
}

using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;

namespace UtsavNights.Gameplay
{
    /// <summary>Enables Enhanced Touch once; in the editor the mouse simulates touches.</summary>
    public static class TouchInput
    {
        private static bool _enabled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _enabled = false;

        public static void EnsureEnabled()
        {
            if (_enabled)
            {
                return;
            }

            EnhancedTouchSupport.Enable();
#if UNITY_EDITOR
            TouchSimulation.Enable();
#endif
            _enabled = true;
        }
    }
}

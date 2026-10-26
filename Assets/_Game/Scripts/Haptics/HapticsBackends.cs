using UnityEngine;
using UnityEngine.Scripting;
using UtsavNights.Core.Haptics;

namespace UtsavNights.Haptics
{
    /// <summary>Platform vibration output. The service handles policy; backends only vibrate.</summary>
    public interface IHapticsBackend
    {
        bool HasVibrator { get; }

        bool HasAmplitudeControl { get; }

        void Play(HapticWaveform waveform);

        void Cancel();
    }

    /// <summary>Editor, desktop and unsupported devices: records calls, never vibrates.</summary>
    public sealed class NullHapticsBackend : IHapticsBackend
    {
        public bool HasVibrator => false;

        public bool HasAmplitudeControl => false;

        public int PlayCount { get; private set; }

        public HapticWaveform LastPlayed { get; private set; }

        public void Play(HapticWaveform waveform)
        {
            PlayCount++;
            LastPlayed = waveform;
        }

        public void Cancel()
        {
        }
    }

    public static class HapticsBackendFactory
    {
        public static IHapticsBackend CreateDefault()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                var android = new AndroidVibratorBackend();
                if (android.HasVibrator)
                {
                    return android;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Haptics] Android vibrator unavailable: " + e.Message);
            }
#endif
            return new NullHapticsBackend();
        }

        // Never called. Referencing Handheld.Vibrate makes Unity add android.permission.VIBRATE to the
        // manifest, which the JNI backend needs.
        [Preserve]
        private static void RequireVibratePermission() => Handheld.Vibrate();
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    /// <summary>
    /// Android Vibrator through JNI with VibrationEffect waveforms (API 26+). Uses amplitude control when
    /// the hardware has it, otherwise an on/off pattern. Called a few times per cracker, so JNI cost is fine.
    /// </summary>
    internal sealed class AndroidVibratorBackend : IHapticsBackend
    {
        private readonly AndroidJavaObject _vibrator;
        private readonly AndroidJavaClass _effect;

        public AndroidVibratorBackend()
        {
            AndroidJavaObject context = UnityEngine.Android.AndroidApplication.currentContext;
            int sdk;
            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
            {
                sdk = version.GetStatic<int>("SDK_INT");
            }

            if (sdk >= 31)
            {
                using (AndroidJavaObject manager = context.Call<AndroidJavaObject>("getSystemService", "vibrator_manager"))
                {
                    _vibrator = manager?.Call<AndroidJavaObject>("getDefaultVibrator");
                }
            }
            else
            {
                _vibrator = context.Call<AndroidJavaObject>("getSystemService", "vibrator");
            }

            HasVibrator = _vibrator != null && _vibrator.Call<bool>("hasVibrator");
            HasAmplitudeControl = HasVibrator && _vibrator.Call<bool>("hasAmplitudeControl");
            _effect = new AndroidJavaClass("android.os.VibrationEffect");
        }

        public bool HasVibrator { get; }

        public bool HasAmplitudeControl { get; }

        public void Play(HapticWaveform waveform)
        {
            if (!HasVibrator || waveform.PeakAmplitude <= 0)
            {
                return;
            }

            try
            {
                using (AndroidJavaObject effect = HasAmplitudeControl
                           ? _effect.CallStatic<AndroidJavaObject>("createWaveform", waveform.TimingsMs, waveform.Amplitudes, -1)
                           : _effect.CallStatic<AndroidJavaObject>("createWaveform", waveform.OnOffTimingsMs, -1))
                {
                    _vibrator.Call("vibrate", effect);
                }
            }
            catch (System.Exception e)
            {
                // A failed vibration must never break gameplay (PRD 13.3).
                Debug.LogWarning("[Haptics] vibrate failed: " + e.Message);
            }
        }

        public void Cancel()
        {
            if (HasVibrator)
            {
                _vibrator.Call("cancel");
            }
        }
    }
#endif
}

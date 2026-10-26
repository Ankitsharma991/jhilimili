using System;
using UtsavNights.Audio;
using UtsavNights.Content;
using UtsavNights.Core.Runtime;
using UtsavNights.Core.Settings;
using UtsavNights.Effects;
using UtsavNights.Haptics;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Owns the current settings, applies them to every service and saves on change (F07).
    /// UI changes settings through <see cref="Change"/>; services never read the file themselves.
    /// </summary>
    public sealed class SettingsService
    {
        private readonly SettingsStore _store;
        private GameSettings _current;

        public SettingsService(SettingsStore store, GameSettings initial)
        {
            _store = store;
            _current = initial ?? new GameSettings();
            _current.Sanitize();
        }

        /// <summary>A copy of the current settings; edit through <see cref="Change"/>.</summary>
        public GameSettings Current => _current.Clone();

        /// <summary>True when changes have been applied but not yet written to disk.</summary>
        public bool IsDirty { get; private set; }

        /// <summary>
        /// Applies an edit and notifies. Saves immediately unless <paramref name="saveNow"/> is false
        /// (slider drags); call <see cref="SaveIfDirty"/> when the drag or the sheet ends.
        /// Returns false when nothing changed.
        /// </summary>
        public bool Change(Action<GameSettings> edit, bool saveNow = true)
        {
            GameSettings next = _current.Clone();
            edit(next);
            next.Sanitize();
            if (next.Equals(_current))
            {
                return false;
            }

            _current = next;
            IsDirty = true;
            ApplyToRegisteredServices();
            if (saveNow)
            {
                SaveIfDirty();
            }

            Game.Events.Publish(new SettingsChangedEvent(_current.Clone()));
            return true;
        }

        public void SaveIfDirty()
        {
            if (!IsDirty)
            {
                return;
            }

            if (_store == null || _store.Save(_current))
            {
                IsDirty = false;
            }
        }

        public void ApplyToRegisteredServices()
        {
            Game.Services.TryGet(out AudioService audio);
            Game.Services.TryGet(out EffectsService effects);
            Game.Services.TryGet(out HapticsService haptics);
            Game.Services.TryGet(out CameraRig cameraRig);
            Game.Services.TryGet(out QualityManager quality);
            Apply(_current, audio, effects, haptics, cameraRig, quality);
        }

        /// <summary>Applies settings to whichever services exist (any may be null).</summary>
        public static void Apply(GameSettings s, AudioService audio, EffectsService effects, HapticsService haptics, CameraRig cameraRig, QualityManager quality)
        {
            if (audio != null)
            {
                audio.MasterVolume = s.masterVolume;
                audio.EffectsVolume = SettingsRules.EffectsVolume(s);
                audio.QuietMode = s.quietMode;
            }

            if (effects != null)
            {
                effects.SetQuietMode(s.quietMode);
                effects.SetReduceFlash(SettingsRules.ReduceFlash(s));
            }

            if (haptics != null)
            {
                haptics.Enabled = SettingsRules.HapticsOn(s);
                haptics.Intensity = s.hapticsIntensity;
            }

            if (cameraRig != null)
            {
                cameraRig.ReduceMotion = s.reduceShake;
                cameraRig.QuietMode = s.quietMode;
            }

            if (quality != null)
            {
                if (s.qualityTier == GameSettings.AutoTier)
                {
                    if (quality.IsManual)
                    {
                        quality.UseAutomaticTier();
                    }
                }
                else if (quality.Tier != s.qualityTier || !quality.IsManual)
                {
                    quality.SetTierManually(s.qualityTier);
                }

                // A tier change resets effects; reapply the accessibility rules on top.
                effects?.SetQuietMode(s.quietMode);
            }
        }
    }
}

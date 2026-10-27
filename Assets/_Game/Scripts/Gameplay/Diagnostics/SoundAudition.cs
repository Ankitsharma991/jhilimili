using UtsavNights.Audio;
using UtsavNights.Content;
using UtsavNights.Core.Crackers;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Plays one cracker's sounds with no visuals, on the same timeline and with the same loop rules as
    /// <see cref="CrackerRuntime"/> (blind sound test, F03). Ticked by its owner.
    /// </summary>
    public sealed class SoundAudition
    {
        private readonly AudioService _audio;
        private CrackerDefinition _definition;
        private CrackerTimeline _timeline;
        private VoiceHandle _loop;
        private float _elapsed;
        private int _next;

        public SoundAudition(AudioService audio) => _audio = audio;

        public bool IsPlaying => _timeline != null;

        /// <summary>Length of the current cracker's sound in seconds.</summary>
        public float Duration => _timeline != null ? _timeline.StopSeconds : 0f;

        public void Play(CrackerDefinition definition)
        {
            Stop();
            if (definition == null || _audio == null || definition.ValidateTimeline().Count > 0)
            {
                return;
            }

            _definition = definition;
            _timeline = definition.BuildTimeline();
            _elapsed = 0f;
            _next = 0;
            Tick(0f);
        }

        public void Stop()
        {
            _audio?.Stop(_loop, CrackerRuntime.LoopFadeSeconds);
            _loop = VoiceHandle.None;
            _timeline = null;
            _definition = null;
        }

        public void Tick(float deltaSeconds)
        {
            if (_timeline == null)
            {
                return;
            }

            _elapsed += deltaSeconds;
            while (_timeline != null && _next < _timeline.CueCount && _timeline[_next].Time <= _elapsed)
            {
                Fire(_timeline[_next]);
                _next++;
            }
        }

        private void Fire(TimelineCue cue)
        {
            StageDefinition stage = _definition.Stages[cue.StageIndex];
            if (stage.stop)
            {
                Stop();
                return;
            }

            if (stage.endLoopSound && _audio.IsPlaying(_loop))
            {
                _audio.Stop(_loop, CrackerRuntime.LoopFadeSeconds);
                _loop = VoiceHandle.None;
            }

            if (stage.sound == null)
            {
                return;
            }

            if (stage.loopSound && _audio.IsPlaying(_loop))
            {
                _audio.Stop(_loop, CrackerRuntime.LoopFadeSeconds);
            }

            VoiceHandle voice = _audio.Play(stage.sound, stage.volume, CrackerRuntime.PriorityFor(stage), stage.loopSound, stage.duckOthers);
            if (stage.loopSound)
            {
                _loop = voice;
            }
        }
    }
}

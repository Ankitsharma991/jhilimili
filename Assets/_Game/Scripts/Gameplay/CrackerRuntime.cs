using System.Collections.Generic;
using UnityEngine;
using UtsavNights.Audio;
using UtsavNights.Content;
using UtsavNights.Core.Crackers;
using UtsavNights.Core.Runtime;
using UtsavNights.Core.Services;
using UtsavNights.Effects;
using UtsavNights.Haptics;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// One live cracker driven entirely by its <see cref="CrackerDefinition"/> (PRD 7.1 rule: no
    /// cracker-specific code). Registers its fuse with the lighter, runs the state machine from the
    /// central loop, and turns stage cues into effects, sound, haptics, flashes and camera moves.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class CrackerRuntime : MonoBehaviour, ITickable, IIgnitionTarget
    {
        public const string FirstIgnitionPrefKey = "gameplay.first_ignition_done";
        private const float LoopFadeSeconds = 0.35f;

        [SerializeField] private CrackerDefinition definition;
        [SerializeField] private bool placeOnStart = true;

        [Tooltip("Place a fresh one after the spent item clears, so the loop can repeat (Phase 2 has no tray yet).")]
        [SerializeField] private bool respawnWhenFinished = true;

        private CrackerStateMachine _machine;
        private GameObject _model;
        private EffectHandle[] _stageEffects;
        private VoiceHandle _loopVoice;
        private EffectsService _effects;
        private AudioService _audio;
        private HapticsService _haptics;
        private CameraRig _cameraRig;
        private bool _registered;

        public CrackerDefinition Definition => definition;

        public CrackerStateMachine StateMachine => _machine;

        public int TargetId { get; private set; }

        public Vector3 FuseWorldPosition => definition != null ? transform.TransformPoint(definition.FuseAnchor) : transform.position;

        public bool CanIgnite => _machine != null && _machine.State == CrackerState.Placed && isActiveAndEnabled;

        /// <summary>Number of stage effects currently playing (for tests and debugging).</summary>
        public int ActiveStageEffects
        {
            get
            {
                int n = 0;
                if (_stageEffects != null && _effects != null)
                {
                    foreach (EffectHandle h in _stageEffects)
                    {
                        if (_effects.Particles.IsPlaying(h))
                        {
                            n++;
                        }
                    }
                }

                return n;
            }
        }

        public void Configure(CrackerDefinition crackerDefinition) => definition = crackerDefinition;

        private void Awake()
        {
            if (definition == null)
            {
                Debug.LogError("[Cracker] No definition assigned.", this);
                enabled = false;
                return;
            }

            List<string> errors = definition.ValidateTimeline();
            if (errors.Count > 0)
            {
                Debug.LogError($"[Cracker] '{definition.Id}' has an invalid timeline: {string.Join("; ", errors)}", this);
                enabled = false;
                return;
            }

            _machine = new CrackerStateMachine(definition.BuildTimeline());
            _machine.CueReached += OnCue;
            _machine.Finished += OnFinished;
            _stageEffects = new EffectHandle[definition.Stages.Count];
            SpawnModel();
        }

        private void Start()
        {
            if (_machine == null)
            {
                return;
            }

            IgnitionTargets targets = IgnitionTargets.Shared;
            TargetId = targets.NewId();
            targets.Register(this);
            Game.Loop.Add(this);
            Game.Events.Subscribe<AppPausedEvent>(OnAppPaused);
            _registered = true;
            if (placeOnStart)
            {
                Place();
            }
        }

        private void OnDestroy()
        {
            if (_machine != null)
            {
                _machine.CueReached -= OnCue;
                _machine.Finished -= OnFinished;
            }

            if (!_registered)
            {
                return;
            }

            IgnitionTargets.Shared.Unregister(this);
            Game.Loop.Remove(this);
            Game.Events.Unsubscribe<AppPausedEvent>(OnAppPaused);
            StopStageEffects(clear: true);
            _audio?.Stop(_loopVoice);
        }

        public bool Place()
        {
            if (_machine == null || !_machine.Place())
            {
                return false;
            }

            if (_model != null)
            {
                _model.SetActive(true);
            }

            Game.Events.Publish(new CrackerPlacedEvent(definition.Id));
            return true;
        }

        public void Ignite()
        {
            if (!CanIgnite || !_machine.Ignite())
            {
                return;
            }

            bool first = PlayerPrefs.GetInt(FirstIgnitionPrefKey, 0) == 0;
            if (first)
            {
                PlayerPrefs.SetInt(FirstIgnitionPrefKey, 1);
                PlayerPrefs.Save();
            }

            Game.Events.Publish(new CrackerIgnitedEvent(definition.Id, first, (float)Time.realtimeSinceStartupAsDouble));
        }

        public void Tick(float deltaSeconds) => _machine?.Tick(deltaSeconds);

        private void OnCue(int stageIndex)
        {
            ResolveServices();
            StageDefinition stage = definition.Stages[stageIndex];
            if (stage == null)
            {
                return;
            }

            Vector3 position = stage.anchor == StageAnchor.Fuse ? FuseWorldPosition : transform.position;

            if (stage.stop)
            {
                StopStageEffects(clear: false);
                _audio?.Stop(_loopVoice, LoopFadeSeconds);
                _loopVoice = VoiceHandle.None;
                Game.Events.Publish(new PayoffCompleteEvent(definition.Id));
                return;
            }

            if (stage.emitter != null && _effects != null)
            {
                EndEarlierEmitters(stageIndex);
                _stageEffects[stageIndex] = _effects.PlayStage(stage, definition, position, transform.rotation);
            }

            if (stage.sound != null && _audio != null)
            {
                // A new loop replaces the previous loop (fuse hiss into fountain roar); one-shots layer on top.
                if (stage.loopSound && _audio.IsPlaying(_loopVoice))
                {
                    _audio.Stop(_loopVoice, LoopFadeSeconds);
                }

                VoiceHandle voice = _audio.Play(stage.sound, stage.volume, PriorityFor(stage), stage.loopSound, stage.duckOthers);
                if (stage.loopSound)
                {
                    _loopVoice = voice;
                }
            }

            _haptics?.Play(stage.haptic);
            _effects?.FlashStage(stage, definition, position);
            ApplyCameraCue(stage.camera, position);
        }

        private void ApplyCameraCue(CameraCue cue, Vector3 position)
        {
            if (_cameraRig == null)
            {
                return;
            }

            switch (cue)
            {
                case CameraCue.PushInSmall:
                    _cameraRig.PushInToward(position);
                    break;
                case CameraCue.AerialFollow:
                    _cameraRig.FollowAerial(transform.position + Vector3.up * definition.EffectVolume.y);
                    break;
                case CameraCue.Shake:
                    _cameraRig.Shake(1f);
                    break;
            }
        }

        private static int PriorityFor(StageDefinition stage)
        {
            if (stage.duckOthers > 0f)
            {
                return SoundPriority.Bang;
            }

            if (stage.payoff)
            {
                return SoundPriority.Payoff;
            }

            return stage.loopSound ? SoundPriority.Fuse : SoundPriority.Effect;
        }

        private void EndEarlierEmitters(int stageIndex)
        {
            for (int i = 0; i < _stageEffects.Length; i++)
            {
                StageDefinition earlier = definition.Stages[i];
                if (i != stageIndex && earlier != null && earlier.endWithNextEmitter && earlier.time <= definition.Stages[stageIndex].time)
                {
                    _effects.Particles.Stop(_stageEffects[i]);
                    _stageEffects[i] = EffectHandle.None;
                }
            }
        }

        private void StopStageEffects(bool clear)
        {
            if (_stageEffects == null)
            {
                return;
            }

            for (int i = 0; i < _stageEffects.Length; i++)
            {
                _effects?.Particles.Stop(_stageEffects[i], clear);
                _stageEffects[i] = EffectHandle.None;
            }
        }

        private void OnFinished()
        {
            // The spent item has had its cooldown (PRD 6.3: clear spent items after 2 s).
            if (_model != null)
            {
                _model.SetActive(false);
            }

            if (respawnWhenFinished)
            {
                Place();
            }
        }

        private void OnAppPaused(AppPausedEvent e)
        {
            if (e.Paused)
            {
                _machine.Pause();
            }
            else
            {
                _machine.Resume();
            }
        }

        private void ResolveServices()
        {
            if (_effects == null)
            {
                Game.Services.TryGet(out _effects);
            }

            if (_audio == null)
            {
                Game.Services.TryGet(out _audio);
            }

            if (_haptics == null)
            {
                Game.Services.TryGet(out _haptics);
            }

            if (_cameraRig == null)
            {
                Game.Services.TryGet(out _cameraRig);
            }
        }

        private void SpawnModel()
        {
            if (definition.ModelPrefab != null)
            {
                _model = Instantiate(definition.ModelPrefab, transform);
                _model.name = definition.ModelPrefab.name;
                _model.SetActive(false);
            }
        }
    }
}

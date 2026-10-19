using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using UtsavNights.Core;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace UtsavNights.Spikes
{
    /// <summary>
    /// Spike C / Gate B: 8 short synthesised sounds, a 32-voice AudioSource pool, a DSP buffer switch
    /// (256 / 512 / 1024), a tap test and a 16-overlapping-voices test.
    ///
    /// Software latency = touch-to-Play time + mixer buffer latency. Perceived (acoustic) latency must be
    /// measured with a slow-motion video of the on-screen flash square versus the click (see Docs/Phase1).
    /// </summary>
    public sealed class AudioLatencySpike : MonoBehaviour
    {
        public const int VoiceCount = 32;
        public const int OverlapTestVoices = 16;
        private static readonly int[] DspSizes = { 256, 512, 1024 };

        private readonly RollingStats _tapLatencyMs = new RollingStats(64);
        private AudioSource[] _voices;
        private AudioClip[] _clips;
        private int _nextVoice;
        private int _dspIndex;
        private bool _overlapRunning;
        private WaitForSeconds _stagger;
        private WaitForSeconds _tail;
        private SpikeHud _hud;

        public int ActiveVoices { get; private set; }

        public int PeakVoices { get; private set; }

        public RollingStats TapLatencyMs => _tapLatencyMs;

        public int DspBufferLength { get; private set; }

        public int DspNumBuffers { get; private set; }

        public int SampleRate { get; private set; }

        public float BufferLatencyMs => SpikeMath.BufferLatencyMs(DspBufferLength, DspNumBuffers, SampleRate);

        public bool OverlapTestRunning => _overlapRunning;

        private void Awake()
        {
            SpikeInput.EnsureEnabled();
            _hud = GetComponent<SpikeHud>();
            _stagger = new WaitForSeconds(0.03f);
            _tail = new WaitForSeconds(1.5f);
            CreateVoices();
            ApplyDspBuffer(DspSizes[_dspIndex]);
        }

        private void CreateVoices()
        {
            _voices = new AudioSource[VoiceCount];
            for (int i = 0; i < VoiceCount; i++)
            {
                var go = new GameObject("Voice" + i);
                go.transform.SetParent(transform, false);
                AudioSource source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.priority = 128;
                _voices[i] = source;
            }
        }

        private void BuildClips()
        {
            if (_clips != null)
            {
                for (int i = 0; i < _clips.Length; i++)
                {
                    if (_clips[i] != null)
                    {
                        Destroy(_clips[i]);
                    }
                }
            }

            int sampleRate = AudioSettings.outputSampleRate;
            ProceduralSounds.Kind[] kinds = ProceduralSounds.AllKinds;
            _clips = new AudioClip[kinds.Length];
            for (int i = 0; i < kinds.Length; i++)
            {
                float[] samples = ProceduralSounds.Generate(kinds[i], sampleRate, 1000 + i);
                AudioClip clip = AudioClip.Create(kinds[i].ToString(), samples.Length, 1, sampleRate, false);
                clip.SetData(samples, 0);
                _clips[i] = clip;
            }
        }

        public void ApplyDspBuffer(int requestedSize)
        {
            AudioConfiguration config = AudioSettings.GetConfiguration();
            config.dspBufferSize = requestedSize;
            bool ok = AudioSettings.Reset(config);
            if (!ok)
            {
                Debug.LogWarning($"{SpikeReport.LogTag} AudioSettings.Reset failed for buffer {requestedSize}");
            }

            for (int i = 0; i < _voices.Length; i++)
            {
                _voices[i].Stop();
                _voices[i].clip = null;
            }

            BuildClips();
            AudioSettings.GetDSPBufferSize(out int length, out int count);
            DspBufferLength = length;
            DspNumBuffers = count;
            SampleRate = AudioSettings.outputSampleRate;
            _tapLatencyMs.Clear();
            SpikeReport.Record("gateB_audio", $"dsp_{length}x{count}_buffer_latency", BufferLatencyMs, "ms");
        }

        public int CycleDspBuffer()
        {
            _dspIndex = (_dspIndex + 1) % DspSizes.Length;
            ApplyDspBuffer(DspSizes[_dspIndex]);
            return DspBufferLength;
        }

        public AudioSource Play(ProceduralSounds.Kind kind, float volume)
        {
            return Play((int)kind, volume);
        }

        public AudioSource Play(int clipIndex, float volume)
        {
            if (_clips == null || _clips.Length == 0)
            {
                return null;
            }

            AudioSource chosen = null;
            for (int n = 0; n < VoiceCount; n++)
            {
                int i = (_nextVoice + n) % VoiceCount;
                if (!_voices[i].isPlaying)
                {
                    chosen = _voices[i];
                    _nextVoice = (i + 1) % VoiceCount;
                    break;
                }
            }

            if (chosen == null)
            {
                // Pool exhausted: steal round-robin. Priority-based stealing arrives with the Phase 2 audio manager.
                chosen = _voices[_nextVoice];
                _nextVoice = (_nextVoice + 1) % VoiceCount;
            }

            chosen.Stop();
            chosen.clip = _clips[clipIndex % _clips.Length];
            chosen.volume = volume;
            chosen.Play();
            return chosen;
        }

        public void RunOverlapTest()
        {
            if (!_overlapRunning)
            {
                StartCoroutine(OverlapRoutine());
            }
        }

        private IEnumerator OverlapRoutine()
        {
            _overlapRunning = true;
            PeakVoices = 0;
            for (int i = 0; i < OverlapTestVoices; i++)
            {
                // Skip the very short Click (index 0) so all 16 voices really overlap.
                Play(1 + (i % (_clips.Length - 1)), 0.45f);
                yield return _stagger;
            }

            yield return _tail;
            SpikeReport.Record("gateB_audio", "overlap_peak_voices", PeakVoices, "voices");
            _overlapRunning = false;
        }

        private void Update()
        {
            int active = 0;
            for (int i = 0; i < _voices.Length; i++)
            {
                if (_voices[i].isPlaying)
                {
                    active++;
                }
            }

            ActiveVoices = active;
            if (active > PeakVoices)
            {
                PeakVoices = active;
            }

            if (SpikeInput.Mode == SpikeMode.AudioTap)
            {
                HandleTaps();
            }
        }

        private void HandleTaps()
        {
            var touches = ETouch.activeTouches;
            for (int i = 0; i < touches.Count; i++)
            {
                ETouch touch = touches[i];
                if (touch.phase != TouchPhase.Began)
                {
                    continue;
                }

                if (_hud != null && _hud.IsOverHud(touch.screenPosition))
                {
                    continue;
                }

                Play(ProceduralSounds.Kind.Click, 1f);
                if (_hud != null)
                {
                    _hud.Flash();
                }

                float inputToPlayMs = (float)((InputState.currentTime - touch.startTime) * 1000.0);
                _tapLatencyMs.Add(inputToPlayMs);
                if (_tapLatencyMs.Count % 10 == 0)
                {
                    SpikeReport.Record("gateB_audio", "tap_to_play_avg", _tapLatencyMs.Average, "ms", false);
                    SpikeReport.Record(
                        "gateB_audio",
                        "estimated_software_latency",
                        _tapLatencyMs.Average + BufferLatencyMs,
                        "ms");
                }
            }
        }
    }
}

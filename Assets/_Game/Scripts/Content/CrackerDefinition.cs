using System;
using System.Collections.Generic;
using UnityEngine;
using UtsavNights.Core.Crackers;

namespace UtsavNights.Content
{
    [Serializable]
    public struct LocalizedName
    {
        public string en;
        public string ne;
        public string hi;

        public LocalizedName(string english, string nepali, string hindi)
        {
            en = english;
            ne = nepali;
            hi = hindi;
        }
    }

    /// <summary>One timed step after ignition (PRD 7.1 "stages"). Everything is optional except the time.</summary>
    [Serializable]
    public sealed class StageDefinition
    {
        [Tooltip("Seconds after ignition.")]
        [Min(0f)] public float time;

        [Tooltip("Particle effect started at this time (pooled).")]
        public ParticleSystem emitter;

        [Tooltip("Start the emitter at the cracker's base or at its fuse (fuse sparks).")]
        public StageAnchor anchor;

        [Tooltip("Stop this stage's emitter when the next stage that has an emitter starts (fuse sparks into the fountain).")]
        public bool endWithNextEmitter;

        [Range(0f, 1f)] public float volume = 0.8f;

        [Tooltip("Fade out the running loop sound (fuse hiss) even though this stage's sound is a one-shot.")]
        public bool endLoopSound;

        [Tooltip("Hide the cracker's model from this stage on (a rocket leaving its bottle).")]
        public bool hideModel;

        [Tooltip("0..1: how much this sound lowers all other sounds (big bangs).")]
        [Range(0f, 1f)] public float duckOthers;

        public AudioClip sound;

        [Tooltip("Loop the sound until the next stage that has a sound, or the stop stage.")]
        public bool loopSound;

        public HapticPattern haptic;

        [Tooltip("Short flash light at this stage (mid and high tiers, capped at 3 per second).")]
        public bool light;

        public Color lightColor = new Color(1f, 0.7f, 0.28f);

        [Min(0f)] public float lightPeak = 2f;

        public CameraCue camera;

        [Tooltip("Marks the payoff moment (the big part of the effect).")]
        public bool payoff;

        [Tooltip("Ends the effect. Exactly one stop stage, and it must be last.")]
        public bool stop;

        [Header("Repeats (chain lar, cake shots)")]
        [Tooltip("How many times this stage fires. The stop stage never repeats.")]
        [Min(1)] public int repeatCount = 1;

        [Min(0f)] public float repeatInterval = 0.5f;

        [Tooltip("Per-repeat position offset in the cracker's local space.")]
        public Vector3 repeatOffset;

        public RepeatPattern repeatPattern;

        [Header("Motion")]
        public StageMotion motion;

        [Tooltip("Spin speed for Spin motion.")]
        public float spinDegreesPerSecond = 720f;

        [Tooltip("Also spin the cracker's model (chakri).")]
        public bool spinModel;

        [Tooltip("Metres above the base for Launch motion and for the Apex anchor.")]
        [Min(0f)] public float launchHeight = 10f;

        [Tooltip("Seconds to reach the apex for Launch motion.")]
        [Min(0.05f)] public float launchRiseSeconds = 1.2f;

        public CueKind Kind => stop ? CueKind.Stop : payoff ? CueKind.Payoff : CueKind.Normal;

        /// <summary>Times this stage fires (1 for stop stages).</summary>
        public int Repeats => stop ? 1 : Mathf.Max(1, repeatCount);
    }

    [Serializable]
    public struct TierQuality
    {
        [Min(0)] public int maxParticles;
        public bool light;
        public bool bloom;

        public TierQuality(int maxParticles, bool light, bool bloom)
        {
            this.maxParticles = maxParticles;
            this.light = light;
            this.bloom = bloom;
        }
    }

    /// <summary>
    /// Data for one cracker, mirroring the PRD 7.1 schema. Adding a cracker means a new asset, a model,
    /// a particle prefab and audio: never new gameplay code (PRD 7.1 rule).
    /// </summary>
    [CreateAssetMenu(menuName = "Utsav Nights/Cracker Definition", fileName = "Cracker_New")]
    public sealed class CrackerDefinition : ScriptableObject
    {
        public const int CurrentSchema = 1;

        [Header("Identity")]
        [Tooltip("Stable, unique, lowercase id such as flowerpot_gold. Never rename after release.")]
        [SerializeField] private string id = string.Empty;

        [SerializeField] private int schema = CurrentSchema;
        [SerializeField] private LocalizedName displayName;
        [SerializeField] private CrackerCategory category;
        [SerializeField] private string[] festivals = { "diwali", "tihar" };
        [SerializeField] private Intensity intensity = Intensity.Medium;

        [Header("Placement (metres)")]
        [SerializeField] private Vector2 footprint = new Vector2(0.3f, 0.3f);
        [SerializeField] private Vector3 effectVolume = new Vector3(1.2f, 3f, 1.2f);
        [SerializeField] private PlacementSurface surfaces = PlacementSurface.Ground;

        [Header("Ignition")]
        [SerializeField] private IgnitionMethod ignitionMethods = IgnitionMethod.Lighter;
        [Min(0.1f)] [SerializeField] private float fuseSeconds = 2.5f;

        [Tooltip("Fuse anchor relative to the model's pivot; the lighter must reach this point.")]
        [SerializeField] private Vector3 fuseAnchor = new Vector3(0f, 0.3f, 0f);

        [Header("Timeline")]
        [SerializeField] private List<StageDefinition> stages = new List<StageDefinition>();

        [Tooltip("How long the spent item stays before it is cleared (PRD 6.3: 2 s).")]
        [Min(0f)] [SerializeField] private float cooldownSeconds = 2f;

        [Header("Model")]
        [SerializeField] private GameObject modelPrefab;
        [SerializeField] private Sprite trayIcon;

        [Header("Quality per tier")]
        [SerializeField] private TierQuality low = new TierQuality(250, false, false);
        [SerializeField] private TierQuality mid = new TierQuality(500, true, false);
        [SerializeField] private TierQuality high = new TierQuality(900, true, true);

        [Header("Budget")]
        [Min(0)] [SerializeField] private int voices = 3;
        [Min(0)] [SerializeField] private int cost = 2;

        [Header("Packaging and credits")]
        [SerializeField] private string pack = "core";

        [Tooltip("Licence-log id for the model, or 'own'.")]
        [SerializeField] private string creditsModel = "own";

        [Tooltip("Licence-log id(s) for the audio, comma separated (Docs/LICENCE_LOG.md).")]
        [SerializeField] private string creditsAudio = string.Empty;

        public string Id { get => id; internal set => id = value; }

        public int Schema { get => schema; internal set => schema = value; }

        public LocalizedName DisplayName { get => displayName; internal set => displayName = value; }

        public CrackerCategory Category { get => category; internal set => category = value; }

        public IReadOnlyList<string> Festivals => festivals;

        public Intensity Intensity { get => intensity; internal set => intensity = value; }

        public Vector2 Footprint { get => footprint; internal set => footprint = value; }

        public Vector3 EffectVolume { get => effectVolume; internal set => effectVolume = value; }

        public PlacementSurface Surfaces { get => surfaces; internal set => surfaces = value; }

        public IgnitionMethod IgnitionMethods { get => ignitionMethods; internal set => ignitionMethods = value; }

        public float FuseSeconds { get => fuseSeconds; internal set => fuseSeconds = value; }

        public Vector3 FuseAnchor { get => fuseAnchor; internal set => fuseAnchor = value; }

        public IReadOnlyList<StageDefinition> Stages => stages;

        public float CooldownSeconds { get => cooldownSeconds; internal set => cooldownSeconds = value; }

        public GameObject ModelPrefab { get => modelPrefab; internal set => modelPrefab = value; }

        public Sprite TrayIcon { get => trayIcon; internal set => trayIcon = value; }

        public int Voices { get => voices; internal set => voices = value; }

        public int Cost { get => cost; internal set => cost = value; }

        public string Pack { get => pack; internal set => pack = value; }

        public string CreditsModel { get => creditsModel; internal set => creditsModel = value; }

        public string CreditsAudio { get => creditsAudio; internal set => creditsAudio = value; }

        public bool IsAerial => category.IsAerial();

        internal void SetFestivals(params string[] values) => festivals = values;

        internal List<StageDefinition> MutableStages => stages;

        internal void SetQuality(QualityTier tier, TierQuality quality)
        {
            switch (tier)
            {
                case QualityTier.Low: low = quality; break;
                case QualityTier.Mid: mid = quality; break;
                default: high = quality; break;
            }
        }

        public TierQuality GetQuality(QualityTier tier)
        {
            switch (tier)
            {
                case QualityTier.Low: return low;
                case QualityTier.Mid: return mid;
                default: return high;
            }
        }

        /// <summary>
        /// Stage cues in time order (stable for equal times). Invalid timelines are reported, not built:
        /// use <see cref="ValidateTimeline"/> first, or catch the ArgumentException.
        /// </summary>
        public CrackerTimeline BuildTimeline()
        {
            return new CrackerTimeline(fuseSeconds, cooldownSeconds, BuildCues());
        }

        public List<string> ValidateTimeline()
        {
            return CrackerTimeline.Validate(fuseSeconds, cooldownSeconds, BuildCues());
        }

        private List<TimelineCue> BuildCues()
        {
            var cues = new List<TimelineCue>(stages.Count);
            for (int i = 0; i < stages.Count; i++)
            {
                StageDefinition stage = stages[i];
                if (stage == null)
                {
                    continue;
                }

                int repeats = stage.Repeats;
                for (int k = 0; k < repeats; k++)
                {
                    // Only the first repetition of a payoff stage moves the state into Payoff.
                    CueKind kind = k == 0 ? stage.Kind : CueKind.Normal;
                    cues.Add(new TimelineCue(StageMath.RepeatTime(stage.time, k, stage.repeatInterval), kind, i, k));
                }
            }

            // Insertion sort keeps equal times in authoring order; stage lists are short.
            for (int i = 1; i < cues.Count; i++)
            {
                TimelineCue current = cues[i];
                int j = i - 1;
                while (j >= 0 && cues[j].Time > current.Time)
                {
                    cues[j + 1] = cues[j];
                    j--;
                }

                cues[j + 1] = current;
            }

            return cues;
        }
    }
}

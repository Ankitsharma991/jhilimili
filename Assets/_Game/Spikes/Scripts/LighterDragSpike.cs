using UnityEngine;
using UtsavNights.Core;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace UtsavNights.Spikes
{
    /// <summary>
    /// Spike D: a lighter that follows the finger (offset above the touch so the finger does not hide the
    /// fuse, PRD 6.3) and ignites a placeholder flowerpot after a 0.5 s hold near the fuse.
    /// This is a throwaway spike; the real data-driven state machine is Phase 2.
    /// </summary>
    public sealed class LighterDragSpike : MonoBehaviour
    {
        private enum State
        {
            Idle,
            Lit,
            Burning,
            Cooldown,
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform lighter;
        [SerializeField] private Transform fuseAnchor;
        [SerializeField] private Renderer fuseMarker;
        [SerializeField] private ParticleSystem fuseSparkPrefab;
        [SerializeField] private ParticleSystem fountainPrefab;
        [SerializeField] private float fingerOffsetDp = 64f;
        [SerializeField] private float igniteRadius = 0.2f;
        [SerializeField] private float holdSeconds = 0.5f;
        [SerializeField] private float fuseSeconds = 2f;
        [SerializeField] private float burnSeconds = 6f;
        [SerializeField] private float cooldownSeconds = 2f;

        private IgnitionHoldTracker _hold;
        private ParticleSystem _fuse;
        private ParticleSystem _fountain;
        private AudioLatencySpike _audio;
        private PerfMonitor _perf;
        private SpikeHud _hud;
        private MaterialPropertyBlock _markerBlock;
        private State _state;
        private float _stateTimer;
        private int _activeTouchId = -1;

        public int Ignitions { get; private set; }

        public float HoldProgress => _hold != null ? _hold.Progress : 0f;

        public string StateName => _state.ToString();

        public void Configure(
            Camera cameraToUse,
            Transform lighterTransform,
            Transform fuse,
            Renderer marker,
            ParticleSystem fuseSpark,
            ParticleSystem fountain)
        {
            targetCamera = cameraToUse;
            lighter = lighterTransform;
            fuseAnchor = fuse;
            fuseMarker = marker;
            fuseSparkPrefab = fuseSpark;
            fountainPrefab = fountain;
        }

        private void Awake()
        {
            SpikeInput.EnsureEnabled();
            _hold = new IgnitionHoldTracker(holdSeconds);
            _audio = GetComponent<AudioLatencySpike>();
            _perf = GetComponent<PerfMonitor>();
            _hud = GetComponent<SpikeHud>();
            _markerBlock = new MaterialPropertyBlock();
            if (fuseSparkPrefab != null)
            {
                _fuse = Instantiate(fuseSparkPrefab, fuseAnchor.position, fuseSparkPrefab.transform.rotation, transform);
            }

            if (fountainPrefab != null)
            {
                _fountain = Instantiate(fountainPrefab, fuseAnchor.position, fountainPrefab.transform.rotation, transform);
            }

            if (lighter != null)
            {
                lighter.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (targetCamera == null || lighter == null || fuseAnchor == null)
            {
                return;
            }

            float dt = Time.deltaTime;
            TickState(dt);

            bool show = SpikeInput.Mode == SpikeMode.Lighter && TryGetDragPoint(out Vector2 screen) &&
                        MoveLighter(screen);
            if (lighter.gameObject.activeSelf != show)
            {
                lighter.gameObject.SetActive(show);
            }

            bool inRange = show && _state == State.Idle &&
                           (lighter.position - fuseAnchor.position).sqrMagnitude <= igniteRadius * igniteRadius;
            if (_hold.Tick(inRange, dt))
            {
                Ignite();
            }

            UpdateMarker();
        }

        private bool TryGetDragPoint(out Vector2 screen)
        {
            var touches = ETouch.activeTouches;
            bool found = false;
            screen = default;
            for (int i = 0; i < touches.Count; i++)
            {
                ETouch touch = touches[i];
                if (_activeTouchId == -1 && touch.phase == TouchPhase.Began &&
                    (_hud == null || !_hud.IsOverHud(touch.screenPosition)))
                {
                    _activeTouchId = touch.touchId;
                }

                if (touch.touchId != _activeTouchId)
                {
                    continue;
                }

                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    break;
                }

                screen = touch.screenPosition;
                found = true;
                break;
            }

            if (!found)
            {
                _activeTouchId = -1;
            }

            return found;
        }

        private bool MoveLighter(Vector2 screen)
        {
            Vector2 offsetPoint = screen + new Vector2(0f, SpikeMath.DpToPx(fingerOffsetDp, Screen.dpi));
            Ray ray = targetCamera.ScreenPointToRay(offsetPoint);
            var plane = new Plane(-targetCamera.transform.forward, fuseAnchor.position);
            if (!plane.Raycast(ray, out float enter))
            {
                return false;
            }

            lighter.position = ray.GetPoint(enter);
            return true;
        }

        private void Ignite()
        {
            _state = State.Lit;
            _stateTimer = fuseSeconds;
            Ignitions++;
            if (_fuse != null)
            {
                _fuse.Play(true);
            }

            if (_audio != null)
            {
                _audio.Play(ProceduralSounds.Kind.FuseHiss, 0.5f);
            }

            if (Ignitions == 1 && _perf != null && _perf.SceneReadySeconds >= 0d)
            {
                double sinceReady = Time.realtimeSinceStartupAsDouble - _perf.SceneReadySeconds;
                SpikeReport.Record("spikeD_touch", "first_ignition_after_scene_ready", (float)sinceReady, "s");
            }
        }

        private void TickState(float dt)
        {
            if (_state == State.Idle)
            {
                return;
            }

            _stateTimer -= dt;
            if (_stateTimer > 0f)
            {
                return;
            }

            switch (_state)
            {
                case State.Lit:
                    if (_fuse != null)
                    {
                        _fuse.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    }

                    if (_fountain != null)
                    {
                        _fountain.Play(true);
                    }

                    if (_audio != null)
                    {
                        _audio.Play(ProceduralSounds.Kind.Whoosh, 0.8f);
                    }

                    _state = State.Burning;
                    _stateTimer = burnSeconds;
                    break;
                case State.Burning:
                    if (_fountain != null)
                    {
                        _fountain.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    }

                    _state = State.Cooldown;
                    _stateTimer = cooldownSeconds;
                    break;
                default:
                    _state = State.Idle;
                    break;
            }
        }

        private void UpdateMarker()
        {
            if (fuseMarker == null)
            {
                return;
            }

            Color idle = new Color(0.9f, 0.2f, 0.1f);
            Color hot = new Color(1f, 0.85f, 0.3f);
            Color c = _state == State.Idle ? Color.Lerp(idle, hot, HoldProgress) : new Color(0.25f, 0.25f, 0.25f);
            _markerBlock.SetColor(BaseColorId, c);
            fuseMarker.SetPropertyBlock(_markerBlock);
        }
    }
}

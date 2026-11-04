using UnityEngine;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.District;
using UtsavNights.Core.Runtime;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// The bazaar's people (Phase 12 step 4), while the district is part of the world: the crowd simulation
    /// (<see cref="CrowdSim"/>) runs for everyone, and a small pool of avatars is lent to the people nearest the player
    /// (24 on the high tier, 14 on mid). A person keeps their avatar while in range, so nobody changes look in front of
    /// you; the pool is built a few avatars a frame when the district opens and is only moved and switched after that,
    /// so walking allocates nothing. Bursts make people cheer, or cover their ears when a loud one goes off close by.
    /// </summary>
    [DefaultExecutionOrder(-110)]
    public sealed class CrowdView : MonoBehaviour
    {
        public const int Residents = 140;
        public const int HighPool = 24;
        public const int MidPool = 14;
        public const float DrawRange = 30f;
        public const float AssignInterval = 0.25f;

        /// <summary>Phase 12 step 5: the room people give the player on the scooter.</summary>
        public const float RidingSpace = 2.4f;
        public const int Seed = 1207;
        private const int BuildPerFrame = 3;

        private AvatarView[] _views;
        private AvatarAnimator[] _animators;
        private int[] _personOf;
        private PersonState[] _shown;
        private int[] _slotOf;
        private int _built;
        private DistrictView _district;
        private ExploreController _explore;
        private float _clock;
        private bool _registered;

        public CrowdSim Sim { get; private set; }

        /// <summary>Avatars in the pool (built or still to build) while the district is active, else 0.</summary>
        public int PoolSize => _views != null ? _views.Length : 0;

        public int Built => _built;

        /// <summary>Avatars drawn now.</summary>
        public int Drawn { get; private set; }

        /// <summary>The avatar lent to a person now, or null.</summary>
        public AvatarView ViewOf(int person) => _slotOf != null && _slotOf[person] >= 0 ? _views[_slotOf[person]] : null;

        private void Awake()
        {
            if (!Game.Services.IsRegistered<CrowdView>())
            {
                Game.Services.Register(this);
                _registered = true;
            }

            Game.Events.Subscribe<PayoffStartedEvent>(OnPayoff);
        }

        private void OnDestroy()
        {
            Game.Events.Unsubscribe<PayoffStartedEvent>(OnPayoff);
            Unload();
            if (_registered)
            {
                Game.Services.Unregister<CrowdView>();
            }
        }

        private void Update()
        {
            if (_district == null && !Game.Services.TryGet(out _district))
            {
                return;
            }

            if (_explore == null)
            {
                _explore = FindFirstObjectByType<ExploreController>();
            }

            bool active = _district.Active && _explore != null && _explore.Player != null;
            if (active != (Sim != null))
            {
                if (active)
                {
                    Load();
                }
                else
                {
                    Unload();
                }
            }

            if (Sim == null)
            {
                return;
            }

            BuildSome();
            Vector3 player = _explore.Player.position;
            Sim.PlayerSpace = _explore.Riding ? RidingSpace : CrowdSim.PersonalSpace;
            Sim.Tick(Time.deltaTime, player.x, player.z, _explore.Speed > 0.2f);
            _clock -= Time.deltaTime;
            if (_clock <= 0f)
            {
                _clock = AssignInterval;
                Assign(player);
            }

            Draw();
        }

        /// <summary>Gives the free avatars to the nearest people without one, and takes them back from people out of range (now: tests).</summary>
        public void Assign(Vector3 player)
        {
            if (Sim == null)
            {
                return;
            }

            Person[] people = Sim.People;
            float keep = (DrawRange + 3f) * (DrawRange + 3f), reach = DrawRange * DrawRange;
            for (int s = 0; s < _built; s++)
            {
                int i = _personOf[s];
                if (i >= 0 && Distance2(people[i], player) > keep)
                {
                    _slotOf[i] = -1;
                    _personOf[s] = -1;
                }
            }

            for (int s = 0; s < _built; s++)
            {
                if (_personOf[s] >= 0)
                {
                    continue;
                }

                int best = -1;
                float bestD = reach;
                for (int i = 0; i < people.Length; i++)
                {
                    float d = Distance2(people[i], player);
                    if (_slotOf[i] < 0 && d < bestD)
                    {
                        best = i;
                        bestD = d;
                    }
                }

                if (best < 0)
                {
                    break;
                }

                _slotOf[best] = s;
                _personOf[s] = best;
                _shown[s] = people[best].State;
                _views[s].transform.SetPositionAndRotation(new Vector3(people[best].X, 0f, people[best].Z), Quaternion.Euler(0f, people[best].Facing, 0f));
            }
        }

        private void Draw()
        {
            Person[] people = Sim.People;
            int drawn = 0;
            float turn = 1f - Mathf.Exp(-10f * Time.deltaTime);
            for (int s = 0; s < _built; s++)
            {
                int i = _personOf[s];
                GameObject go = _views[s].gameObject;
                if (i < 0)
                {
                    if (go.activeSelf)
                    {
                        go.SetActive(false);
                    }

                    continue;
                }

                if (!go.activeSelf)
                {
                    go.SetActive(true);
                }

                drawn++;
                ref Person p = ref people[i];
                Transform t = _views[s].transform;
                t.SetPositionAndRotation(new Vector3(p.X, 0f, p.Z), Quaternion.Slerp(t.rotation, Quaternion.Euler(0f, p.Facing, 0f), turn));
                AvatarAnimator animator = _animators[s];
                animator.SetLocomotion(p.Moving, true);
                if (p.State != _shown[s])
                {
                    _shown[s] = p.State;
                    if (p.State == PersonState.Cheering)
                    {
                        animator.Play(AvatarClip.Celebrate);
                    }
                    else if (p.State == PersonState.CoveringEars)
                    {
                        animator.Play(AvatarClip.CoverEars);
                    }
                    else if (p.State == PersonState.Waving)
                    {
                        animator.Play(AvatarClip.Wave);
                    }
                }
            }

            Drawn = drawn;
        }

        private void OnPayoff(PayoffStartedEvent e)
        {
            Sim?.Burst(e.Position.x, e.Position.z, e.Loud);
        }

        private void Load()
        {
            int tier = Game.Services.TryGet(out QualityManager quality) ? quality.Tier : 1;
            Sim = new CrowdSim(_district.Plan, Residents, Seed);
            int pool = tier >= 2 ? HighPool : MidPool;
            _views = new AvatarView[pool];
            _animators = new AvatarAnimator[pool];
            _personOf = new int[pool];
            _shown = new PersonState[pool];
            _slotOf = new int[Sim.People.Length];
            for (int i = 0; i < _slotOf.Length; i++)
            {
                _slotOf[i] = -1;
            }

            _built = 0;
            _clock = 0f;
        }

        /// <summary>A few avatars a frame, each a preset with its own skin tone and hair, with a body the player bumps into.</summary>
        private void BuildSome()
        {
            var presets = AvatarPresets.All;
            for (int n = 0; n < BuildPerFrame && _built < _views.Length; n++)
            {
                int s = _built;
                AvatarProfile look = presets[(s * 5 + 2) % presets.Count].Clone();
                look.skinTone = (s * 0.37f + 0.1f) % 1f;
                look.hairColour = s % 5;
                AvatarView view = AvatarView.Create(transform, look, "BazaarPerson" + s);
                var body = view.gameObject.AddComponent<CapsuleCollider>();
                body.center = new Vector3(0f, 0.85f, 0f);
                body.height = 1.7f;
                body.radius = 0.28f;
                _animators[s] = view.gameObject.AddComponent<AvatarAnimator>();
                view.gameObject.SetActive(false);
                _views[s] = view;
                _personOf[s] = -1;
                _built++;
                _clock = 0f;
            }
        }

        private void Unload()
        {
            if (_views != null)
            {
                for (int s = 0; s < _built; s++)
                {
                    if (_views[s] != null)
                    {
                        Destroy(_views[s].gameObject);
                    }
                }
            }

            _views = null;
            _animators = null;
            _personOf = null;
            _shown = null;
            _slotOf = null;
            _built = 0;
            Drawn = 0;
            Sim = null;
        }

        private static float Distance2(in Person p, Vector3 player)
        {
            float dx = p.X - player.x, dz = p.Z - player.z;
            return dx * dx + dz * dz;
        }
    }
}

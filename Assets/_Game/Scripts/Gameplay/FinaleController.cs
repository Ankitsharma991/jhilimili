using System;
using System.Collections.Generic;
using UnityEngine;
using UtsavNights.Content;
using UtsavNights.Core.Finale;
using UtsavNights.Core.Runtime;
using UtsavNights.Core.Services;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Finale mode v1 (F13): lights 3 to 6 placed crackers on a payoff rhythm (<see cref="FinaleSchedule"/>),
    /// and can replay the same layout afterwards.
    /// </summary>
    [DefaultExecutionOrder(-110)]
    public sealed class FinaleController : MonoBehaviour, ITickable
    {
        public enum FinaleState
        {
            Idle,
            Running,
        }

        [SerializeField] private float gapSeconds = FinaleSchedule.DefaultGapSeconds;

        private readonly List<CrackerRuntime> _chosen = new List<CrackerRuntime>();
        private readonly List<FinaleSlot> _slots = new List<FinaleSlot>();
        private readonly List<LayoutItem> _lastLayout = new List<LayoutItem>();
        private CrackerSpawner _spawner;
        private float _elapsed;
        private int _nextSlot;
        private bool _registered;

        public event Action<FinaleState> StateChanged;

        public FinaleState State { get; private set; } = FinaleState.Idle;

        public int Lit => _nextSlot;

        public int Total => _slots.Count;

        public bool CanReplay => State == FinaleState.Idle && _lastLayout.Count >= FinaleSchedule.MinItems;

        public IReadOnlyList<FinaleSlot> Slots => _slots;

        private struct LayoutItem
        {
            public CrackerDefinition Definition;
            public Vector3 Position;
            public Quaternion Rotation;
        }

        private void Awake()
        {
            if (!Game.Services.IsRegistered<FinaleController>())
            {
                Game.Services.Register(this);
                _registered = true;
            }
        }

        private void OnEnable() => Game.Loop.Add(this);

        private void OnDisable() => Game.Loop.Remove(this);

        private void OnDestroy()
        {
            if (_registered)
            {
                Game.Services.Unregister<FinaleController>();
            }
        }

        /// <summary>Placed crackers that could be lit right now (the finale needs at least 3).</summary>
        public int ReadyCount
        {
            get
            {
                ResolveSpawner();
                if (_spawner == null)
                {
                    return 0;
                }

                int n = 0;
                foreach (CrackerRuntime c in _spawner.Active)
                {
                    if (c.CanIgnite)
                    {
                        n++;
                    }
                }

                return n;
            }
        }

        public bool CanStart => State == FinaleState.Idle && ReadyCount >= FinaleSchedule.MinItems;

        public bool StartFinale()
        {
            if (!CanStart)
            {
                return false;
            }

            var candidates = new List<CrackerRuntime>();
            foreach (CrackerRuntime c in _spawner.Active)
            {
                if (c.CanIgnite)
                {
                    candidates.Add(c);
                }
            }

            var items = new List<FinaleItem>(candidates.Count);
            for (int i = 0; i < candidates.Count; i++)
            {
                CrackerDefinition d = candidates[i].Definition;
                items.Add(new FinaleItem(i, d.FuseSeconds, Rank(d.Category)));
            }

            _slots.Clear();
            _slots.AddRange(FinaleSchedule.Build(items, gapSeconds));
            if (_slots.Count == 0)
            {
                return false;
            }

            _chosen.Clear();
            _lastLayout.Clear();
            foreach (FinaleSlot slot in _slots)
            {
                CrackerRuntime c = candidates[slot.Index];
                _chosen.Add(c);
                _lastLayout.Add(new LayoutItem { Definition = c.Definition, Position = c.transform.position, Rotation = c.transform.rotation });
            }

            if (Game.Services.TryGet(out PlacementController placement))
            {
                placement.Cancel();
            }

            _elapsed = 0f;
            _nextSlot = 0;
            SetState(FinaleState.Running);
            Tick(0f);
            return true;
        }

        /// <summary>Places the last finale's crackers again in the same spots and runs it.</summary>
        public bool Replay()
        {
            if (!CanReplay)
            {
                return false;
            }

            ResolveSpawner();
            foreach (LayoutItem item in _lastLayout)
            {
                _spawner.Spawn(item.Definition, item.Position, item.Rotation);
            }

            return StartFinale();
        }

        public void Tick(float deltaSeconds)
        {
            if (State != FinaleState.Running)
            {
                return;
            }

            _elapsed += deltaSeconds;
            while (_nextSlot < _slots.Count && _slots[_nextSlot].IgniteAt <= _elapsed)
            {
                CrackerRuntime c = _chosen[_nextSlot];
                if (c != null && c.CanIgnite)
                {
                    c.Ignite();
                }

                _nextSlot++;
            }

            if (_nextSlot < _slots.Count)
            {
                return;
            }

            // Over only when every lit cracker has been cleared (after its cooldown), so a replay never
            // competes with spent items still counted against the scene limit.
            foreach (CrackerRuntime c in _chosen)
            {
                if (c != null && c.isActiveAndEnabled && !c.CanIgnite)
                {
                    return;
                }
            }

            SetState(FinaleState.Idle);
        }

        /// <summary>Climax order: hand-held and ground pieces first, the bang, then aerials, the cake last.</summary>
        public static int Rank(CrackerCategory category)
        {
            switch (category)
            {
                case CrackerCategory.Sparkler: return 0;
                case CrackerCategory.GroundFountain: return 1;
                case CrackerCategory.GroundSpinner: return 2;
                case CrackerCategory.ChainCrackers: return 3;
                case CrackerCategory.SingleBang: return 4;
                case CrackerCategory.Rocket: return 5;
                case CrackerCategory.Skyshot: return 6;
                default: return 7;
            }
        }

        private void SetState(FinaleState state)
        {
            State = state;
            StateChanged?.Invoke(state);
        }

        private void ResolveSpawner()
        {
            if (_spawner == null)
            {
                Game.Services.TryGet(out _spawner);
            }
        }
    }
}

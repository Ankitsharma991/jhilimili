using System.Collections.Generic;
using UnityEngine;
using UtsavNights.Core.Effects;
using UtsavNights.Core.Services;

namespace UtsavNights.Effects
{
    /// <summary>Opaque reference to a playing effect. Stale handles (effect already recycled) are ignored.</summary>
    public readonly struct EffectHandle
    {
        internal EffectHandle(int slot, int version)
        {
            Slot = slot;
            Version = version;
        }

        internal int Slot { get; }

        internal int Version { get; }

        public bool IsValid => Version > 0;

        public static EffectHandle None => default;
    }

    /// <summary>
    /// Pools Particle System instances per prefab (PRD 13.3: pooled prefabs, per-tier caps).
    /// Each play reserves particles from the shared <see cref="ParticleBudget"/>; finished effects
    /// return to the pool automatically during <see cref="Tick"/>. No allocations after warm-up.
    /// </summary>
    public sealed class EffectPool : ITickable
    {
        /// <summary>Below this many granted particles an effect is skipped rather than shown thin.</summary>
        public const int MinimumParticles = 16;

        private readonly Transform _root;
        private readonly ParticleBudget _budget;
        private readonly Dictionary<ParticleSystem, Stack<ParticleSystem>> _free = new Dictionary<ParticleSystem, Stack<ParticleSystem>>();
        private Active[] _active;
        private int _nextVersion = 1;
        private bool _paused;

        public EffectPool(Transform root, ParticleBudget budget, int initialSlots = 32)
        {
            _root = root;
            _budget = budget;
            _active = new Active[Mathf.Max(4, initialSlots)];
        }

        public int ActiveCount { get; private set; }

        public int InstancesCreated { get; private set; }

        /// <summary>Effects skipped because the particle budget was exhausted (for perf sampling).</summary>
        public int SkippedForBudget { get; private set; }

        private struct Active
        {
            public ParticleSystem Instance;
            public ParticleSystem Prefab;
            public int Version;
            public int Granted;
            public bool InUse;
            public bool Stopping;
        }

        public void Prewarm(ParticleSystem prefab, int count)
        {
            if (prefab == null)
            {
                return;
            }

            Stack<ParticleSystem> stack = FreeStack(prefab);
            for (int i = stack.Count; i < count; i++)
            {
                stack.Push(CreateInstance(prefab));
            }
        }

        /// <param name="maxParticles">Tier allowance from the definition; 0 or less means the prefab's own maximum.</param>
        public EffectHandle Play(ParticleSystem prefab, Vector3 position, Quaternion rotation, int maxParticles)
        {
            if (prefab == null)
            {
                return EffectHandle.None;
            }

            int prefabMax = prefab.main.maxParticles;
            int wanted = maxParticles > 0 ? Mathf.Min(maxParticles, prefabMax) : prefabMax;
            int granted = _budget.Reserve(wanted, MinimumParticles);
            if (granted == 0)
            {
                SkippedForBudget++;
                return EffectHandle.None;
            }

            Stack<ParticleSystem> stack = FreeStack(prefab);
            ParticleSystem instance = stack.Count > 0 ? stack.Pop() : CreateInstance(prefab);
            int slot = FindFreeSlot();
            int version = _nextVersion++;
            _active[slot] = new Active
            {
                Instance = instance,
                Prefab = prefab,
                Version = version,
                Granted = granted,
                InUse = true,
            };
            ActiveCount++;

            ParticleSystem.MainModule main = instance.main;
            main.maxParticles = granted;
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.gameObject.SetActive(true);
            instance.Play(true);
            if (_paused)
            {
                instance.Pause(true);
            }

            return new EffectHandle(slot, version);
        }

        public bool IsPlaying(EffectHandle handle) => Resolve(handle) >= 0;

        /// <summary>Stops emitting; the effect returns to the pool once its particles die. Clear recycles now.</summary>
        public bool Stop(EffectHandle handle, bool clear = false)
        {
            int slot = Resolve(handle);
            if (slot < 0)
            {
                return false;
            }

            if (clear)
            {
                Release(slot);
            }
            else
            {
                _active[slot].Stopping = true;
                _active[slot].Instance.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            return true;
        }

        public void StopAll(bool clear)
        {
            for (int i = 0; i < _active.Length; i++)
            {
                if (!_active[i].InUse)
                {
                    continue;
                }

                if (clear)
                {
                    Release(i);
                }
                else
                {
                    _active[i].Stopping = true;
                    _active[i].Instance.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }

        /// <summary>Freezes or resumes all playing effects (app focus loss, PRD 6.3).</summary>
        public void SetPaused(bool paused)
        {
            if (_paused == paused)
            {
                return;
            }

            _paused = paused;
            for (int i = 0; i < _active.Length; i++)
            {
                if (!_active[i].InUse)
                {
                    continue;
                }

                if (paused)
                {
                    _active[i].Instance.Pause(true);
                }
                else
                {
                    _active[i].Instance.Play(true);
                    if (_active[i].Stopping)
                    {
                        _active[i].Instance.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    }
                }
            }
        }

        public void Tick(float deltaSeconds)
        {
            if (_paused)
            {
                return;
            }

            for (int i = 0; i < _active.Length; i++)
            {
                if (!_active[i].InUse)
                {
                    continue;
                }

                ParticleSystem instance = _active[i].Instance;
                if (instance == null)
                {
                    // Destroyed externally (scene unload): just give the budget back.
                    _budget.Release(_active[i].Granted);
                    _active[i] = default;
                    ActiveCount--;
                    continue;
                }

                if (!instance.IsAlive(true))
                {
                    Release(i);
                }
            }
        }

        private int Resolve(EffectHandle handle)
        {
            if (!handle.IsValid || handle.Slot >= _active.Length)
            {
                return -1;
            }

            Active a = _active[handle.Slot];
            return a.InUse && a.Version == handle.Version ? handle.Slot : -1;
        }

        private void Release(int slot)
        {
            Active a = _active[slot];
            if (a.Instance != null)
            {
                a.Instance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                a.Instance.gameObject.SetActive(false);
                FreeStack(a.Prefab).Push(a.Instance);
            }

            _budget.Release(a.Granted);
            _active[slot] = default;
            ActiveCount--;
        }

        private int FindFreeSlot()
        {
            for (int i = 0; i < _active.Length; i++)
            {
                if (!_active[i].InUse)
                {
                    return i;
                }
            }

            int old = _active.Length;
            System.Array.Resize(ref _active, old * 2);
            return old;
        }

        private Stack<ParticleSystem> FreeStack(ParticleSystem prefab)
        {
            if (!_free.TryGetValue(prefab, out Stack<ParticleSystem> stack))
            {
                stack = new Stack<ParticleSystem>(4);
                _free.Add(prefab, stack);
            }

            return stack;
        }

        private ParticleSystem CreateInstance(ParticleSystem prefab)
        {
            ParticleSystem instance = Object.Instantiate(prefab, _root);
            instance.name = prefab.name;
            ParticleSystem.MainModule main = instance.main;
            main.playOnAwake = false;
            main.stopAction = ParticleSystemStopAction.None;
            instance.gameObject.SetActive(false);
            InstancesCreated++;
            return instance;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UtsavNights.Content;
using UtsavNights.Core.Placement;
using UtsavNights.Core.Runtime;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Creates placed crackers from definitions and recycles them (PRD 13.3 pooling). Spent crackers
    /// return to their pool after the cooldown. The item limit follows the quality tier.
    /// </summary>
    [DefaultExecutionOrder(-120)]
    public sealed class CrackerSpawner : MonoBehaviour
    {
        private readonly Dictionary<CrackerDefinition, Stack<CrackerRuntime>> _pools = new Dictionary<CrackerDefinition, Stack<CrackerRuntime>>();
        private readonly List<CrackerRuntime> _active = new List<CrackerRuntime>();
        private bool _registered;

        public IReadOnlyList<CrackerRuntime> Active => _active;

        public int ActiveCount => _active.Count;

        public int ItemLimit
        {
            get
            {
                int tier = Game.Services.TryGet(out QualityManager quality) ? quality.Tier : 1;
                return PlacementRules.ItemLimit(tier);
            }
        }

        public int InstancesCreated { get; private set; }

        private void Awake()
        {
            if (!Game.Services.IsRegistered<CrackerSpawner>())
            {
                Game.Services.Register(this);
                _registered = true;
            }
        }

        private void OnDestroy()
        {
            if (_registered)
            {
                Game.Services.Unregister<CrackerSpawner>();
            }
        }

        /// <summary>Places a cracker at a world point. Returns null when the limit is reached or the definition is invalid.</summary>
        public CrackerRuntime Spawn(CrackerDefinition definition, Vector3 position, Quaternion rotation)
        {
            if (definition == null || _active.Count >= ItemLimit)
            {
                return null;
            }

            CrackerRuntime runtime = Rent(definition);
            if (runtime == null)
            {
                return null;
            }

            runtime.transform.SetPositionAndRotation(position, rotation);
            runtime.gameObject.SetActive(true);
            if (!runtime.Place())
            {
                Release(runtime);
                return null;
            }

            _active.Add(runtime);
            return runtime;
        }

        /// <summary>Removes a placed, unlit cracker (or any active one) and returns it to its pool.</summary>
        public void Remove(CrackerRuntime runtime)
        {
            if (runtime != null && _active.Remove(runtime))
            {
                Release(runtime);
            }
        }

        public void ClearAll()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Release(_active[i]);
            }

            _active.Clear();
        }

        private CrackerRuntime Rent(CrackerDefinition definition)
        {
            if (_pools.TryGetValue(definition, out Stack<CrackerRuntime> pool) && pool.Count > 0)
            {
                return pool.Pop();
            }

            var go = new GameObject("Cracker_" + definition.Id);
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            CrackerRuntime runtime = go.AddComponent<CrackerRuntime>();
            if (!runtime.Initialize(definition, placeImmediately: false))
            {
                Destroy(go);
                return null;
            }

            runtime.RespawnWhenFinished = false;
            runtime.Spent += OnSpent;
            InstancesCreated++;
            return runtime;
        }

        private void OnSpent(CrackerRuntime runtime)
        {
            if (_active.Remove(runtime))
            {
                Release(runtime);
            }
        }

        private void Release(CrackerRuntime runtime)
        {
            runtime.ResetForReuse();
            runtime.gameObject.SetActive(false);
            if (!_pools.TryGetValue(runtime.Definition, out Stack<CrackerRuntime> pool))
            {
                pool = new Stack<CrackerRuntime>(4);
                _pools.Add(runtime.Definition, pool);
            }

            pool.Push(runtime);
        }
    }
}

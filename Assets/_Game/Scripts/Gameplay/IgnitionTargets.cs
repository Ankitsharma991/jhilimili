using System.Collections.Generic;
using UnityEngine;

namespace UtsavNights.Gameplay
{
    /// <summary>Anything the lighter can light: a placed cracker's fuse.</summary>
    public interface IIgnitionTarget
    {
        int TargetId { get; }

        Vector3 FuseWorldPosition { get; }

        bool CanIgnite { get; }

        void Ignite();
    }

    /// <summary>
    /// Registry of fuses in the scene, and the picking rule: the ignitable fuse nearest the flame in
    /// screen space, within a radius in dp (PRD 12: touch targets 48 dp or more).
    /// </summary>
    public sealed class IgnitionTargets
    {
        private readonly List<IIgnitionTarget> _targets = new List<IIgnitionTarget>();
        private int _nextId = 1;

        public int Count => _targets.Count;

        public IReadOnlyList<IIgnitionTarget> All => _targets;

        public int NewId() => _nextId++;

        public void Register(IIgnitionTarget target)
        {
            if (target != null && !_targets.Contains(target))
            {
                _targets.Add(target);
            }
        }

        public void Unregister(IIgnitionTarget target) => _targets.Remove(target);

        public IIgnitionTarget FindById(int id)
        {
            for (int i = 0; i < _targets.Count; i++)
            {
                if (_targets[i].TargetId == id)
                {
                    return _targets[i];
                }
            }

            return null;
        }

        /// <summary>Nearest ignitable fuse to <paramref name="flameScreen"/> within <paramref name="maxPixels"/>, or null.</summary>
        public IIgnitionTarget PickOnScreen(Camera camera, Vector2 flameScreen, float maxPixels)
        {
            IIgnitionTarget best = null;
            float bestSq = maxPixels * maxPixels;
            for (int i = 0; i < _targets.Count; i++)
            {
                IIgnitionTarget t = _targets[i];
                if (!t.CanIgnite)
                {
                    continue;
                }

                Vector3 screen = camera.WorldToScreenPoint(t.FuseWorldPosition);
                if (screen.z <= 0f)
                {
                    continue;
                }

                float sq = ((Vector2)screen - flameScreen).sqrMagnitude;
                if (sq <= bestSq)
                {
                    bestSq = sq;
                    best = t;
                }
            }

            return best;
        }

        /// <summary>Same rule on precomputed screen points, for tests and non-camera callers.</summary>
        public static int PickNearest(Vector2 flameScreen, IReadOnlyList<Vector2> fuseScreens, IReadOnlyList<bool> canIgnite, float maxPixels)
        {
            int best = -1;
            float bestSq = maxPixels * maxPixels;
            for (int i = 0; i < fuseScreens.Count; i++)
            {
                if (!canIgnite[i])
                {
                    continue;
                }

                float sq = (fuseScreens[i] - flameScreen).sqrMagnitude;
                if (sq <= bestSq)
                {
                    bestSq = sq;
                    best = i;
                }
            }

            return best;
        }
    }
}

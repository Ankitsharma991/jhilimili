using System;
using UnityEngine;
using UtsavNights.Content;
using UtsavNights.Core.Runtime;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Crackers already standing when the scene opens (PRD 6.1: a diya and one cracker are placed, so
    /// the first ignition needs no menu). Spawned through the spawner so they behave like any placed item.
    /// </summary>
    public sealed class StartingCrackers : MonoBehaviour
    {
        [Serializable]
        public struct Item
        {
            public CrackerDefinition definition;
            public Vector3 position;
        }

        [SerializeField] private Item[] items = Array.Empty<Item>();

        public void Configure(params Item[] startItems) => items = startItems;

        private void Start()
        {
            if (!Game.Services.TryGet(out CrackerSpawner spawner))
            {
                Debug.LogWarning("[Start] No CrackerSpawner in the scene; starting crackers skipped.", this);
                return;
            }

            Camera cam = Camera.main;
            foreach (Item item in items)
            {
                if (item.definition == null)
                {
                    continue;
                }

                Quaternion facing = Quaternion.identity;
                if (cam != null)
                {
                    Vector3 away = item.position - cam.transform.position;
                    away.y = 0f;
                    if (away.sqrMagnitude > 0.001f)
                    {
                        facing = Quaternion.LookRotation(away.normalized, Vector3.up);
                    }
                }

                spawner.Spawn(item.definition, item.position, facing);
            }
        }
    }
}

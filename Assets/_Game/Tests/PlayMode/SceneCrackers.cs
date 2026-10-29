using UnityEngine;
using UtsavNights.Content;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;

namespace UtsavNights.Tests.PlayMode
{
    internal static class SceneCrackers
    {
        public static CrackerRuntime Find(string id)
        {
            foreach (CrackerRuntime runtime in Object.FindObjectsByType<CrackerRuntime>(FindObjectsSortMode.None))
            {
                if (runtime.Definition != null && runtime.Definition.Id == id)
                {
                    return runtime;
                }
            }

            return null;
        }

        /// <summary>A catalog cracker's definition, without spawning it.</summary>
        public static CrackerDefinition Definition(string id)
        {
            CrackerCatalog catalog = FindCatalog();
            return catalog != null ? catalog.FindById(id) : null;
        }

        /// <summary>Spawns a catalog cracker through the scene's spawner.</summary>
        public static CrackerRuntime Spawn(string id, Vector3 position)
        {
            CrackerCatalog catalog = Object.FindFirstObjectByType<UtsavNights.UI.TrayView>() != null ? FindCatalog() : null;
            CrackerDefinition def = catalog != null ? catalog.FindById(id) : null;
            return def != null && Game.Services.TryGet(out CrackerSpawner spawner) ? spawner.Spawn(def, position, Quaternion.identity) : null;
        }

        private static CrackerCatalog FindCatalog()
        {
            foreach (CrackerCatalog c in Resources.FindObjectsOfTypeAll<CrackerCatalog>())
            {
                return c;
            }

            return null;
        }
    }
}

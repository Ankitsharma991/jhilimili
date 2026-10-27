using System;
using System.IO;
using UnityEngine;
using UtsavNights.Core.Monetisation;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Loads and saves progress.json (PRD 13.5): purchases cache and unlocks. Missing or corrupt files
    /// fall back to an empty progress; a Restore from the store brings a purchase back.
    /// </summary>
    public sealed class ProgressStore
    {
        public const string FileName = "progress.json";

        public ProgressStore(string path = null)
        {
            Path = path ?? System.IO.Path.Combine(Application.persistentDataPath, FileName);
        }

        public string Path { get; }

        public PlayerProgress Load(long nowUnix)
        {
            if (!File.Exists(Path))
            {
                return new PlayerProgress();
            }

            try
            {
                PlayerProgress loaded = JsonUtility.FromJson<PlayerProgress>(File.ReadAllText(Path)) ?? new PlayerProgress();
                if (loaded.Migrate() | loaded.Sanitize(nowUnix))
                {
                    Save(loaded);
                }

                return loaded;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Progress] {Path} unreadable, starting empty: {e.Message}");
                return new PlayerProgress();
            }
        }

        public bool Save(PlayerProgress progress)
        {
            try
            {
                AtomicFile.WriteAllText(Path, JsonUtility.ToJson(progress, true));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Progress] could not save {Path}: {e.Message}");
                return false;
            }
        }
    }
}

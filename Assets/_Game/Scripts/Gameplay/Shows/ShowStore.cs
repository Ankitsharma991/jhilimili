using System;
using System.IO;
using UnityEngine;
using UtsavNights.Core.Shows;

namespace UtsavNights.Gameplay
{
    /// <summary>Loads and saves the player's shows (shows.json, Phase 9 step 4). A missing or corrupt file gives an empty library, never a crash.</summary>
    public sealed class ShowStore
    {
        public const string FileName = "shows.json";

        public ShowStore(string path = null) => Path = path ?? System.IO.Path.Combine(Application.persistentDataPath, FileName);

        public string Path { get; }

        public ShowLibrary Load()
        {
            if (!File.Exists(Path))
            {
                return new ShowLibrary();
            }

            try
            {
                ShowLibrary loaded = JsonUtility.FromJson<ShowLibrary>(File.ReadAllText(Path));
                if (loaded == null)
                {
                    return new ShowLibrary();
                }

                loaded.shows.RemoveAll(s => s == null || string.IsNullOrEmpty(s.id));
                return loaded;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Shows] {Path} unreadable, starting with no saved shows: {e.Message}");
                return new ShowLibrary();
            }
        }

        public bool Save(ShowLibrary library)
        {
            try
            {
                AtomicFile.WriteAllText(Path, JsonUtility.ToJson(library, true));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Shows] could not save {Path}: {e.Message}");
                return false;
            }
        }

        public void Delete()
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }
    }
}

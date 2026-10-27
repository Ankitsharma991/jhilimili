using System;
using System.IO;
using UnityEngine;
using UtsavNights.Core.Settings;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Loads and saves settings.json (PRD 13.5): missing or corrupt files fall back to defaults, older
    /// versions migrate, values are clamped, and saves are atomic (temp file then replace) so a crash
    /// mid-write never leaves a broken file.
    /// </summary>
    public sealed class SettingsStore
    {
        public const string FileName = "settings.json";

        public SettingsStore(string path = null)
        {
            Path = path ?? System.IO.Path.Combine(Application.persistentDataPath, FileName);
        }

        public string Path { get; }

        /// <summary>Why the last load used defaults ("missing", "corrupt") or null when the file was read.</summary>
        public string LastLoadIssue { get; private set; }

        public GameSettings Load()
        {
            LastLoadIssue = null;
            if (!File.Exists(Path))
            {
                LastLoadIssue = "missing";
                return new GameSettings();
            }

            try
            {
                GameSettings loaded = JsonUtility.FromJson<GameSettings>(File.ReadAllText(Path));
                if (loaded == null)
                {
                    LastLoadIssue = "corrupt";
                    return new GameSettings();
                }

                bool changed = loaded.Migrate();
                changed |= loaded.Sanitize();
                if (changed)
                {
                    Save(loaded);
                }

                return loaded;
            }
            catch (Exception e)
            {
                LastLoadIssue = "corrupt";
                Debug.LogWarning($"[Settings] {Path} unreadable, using defaults: {e.Message}");
                return new GameSettings();
            }
        }

        public bool Save(GameSettings settings)
        {
            try
            {
                AtomicFile.WriteAllText(Path, JsonUtility.ToJson(settings, true));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Settings] could not save {Path}: {e.Message}");
                return false;
            }
        }
    }
}

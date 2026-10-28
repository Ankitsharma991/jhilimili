using System;
using System.IO;
using UnityEngine;
using UtsavNights.Core.Avatar;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Loads and saves profile.json (PRD 13.5): the avatar and its outfit. A missing file gives the default preset,
    /// not yet "created", so the creator still offers itself once; a corrupt file is repaired or replaced, never a crash.
    /// </summary>
    public sealed class ProfileStore
    {
        public const string FileName = "profile.json";

        public ProfileStore(string path = null, WardrobeCatalog catalog = null)
        {
            Path = path ?? System.IO.Path.Combine(Application.persistentDataPath, FileName);
            Catalog = catalog ?? WardrobeCatalog.Default;
        }

        public string Path { get; }

        public WardrobeCatalog Catalog { get; }

        public AvatarProfile Load()
        {
            if (!File.Exists(Path))
            {
                return Fresh();
            }

            try
            {
                AvatarProfile loaded = JsonUtility.FromJson<AvatarProfile>(File.ReadAllText(Path));
                if (loaded == null)
                {
                    return Fresh();
                }

                if (loaded.Migrate() | loaded.Sanitize(Catalog))
                {
                    Save(loaded);
                }

                return loaded;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Avatar] {Path} unreadable, using the default avatar: {e.Message}");
                return Fresh();
            }
        }

        public bool Save(AvatarProfile profile)
        {
            try
            {
                profile.Sanitize(Catalog);
                AtomicFile.WriteAllText(Path, JsonUtility.ToJson(profile, true));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Avatar] could not save {Path}: {e.Message}");
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

        private static AvatarProfile Fresh()
        {
            AvatarProfile profile = AvatarPresets.Default;
            profile.created = false;
            return profile;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Backup;
using UtsavNights.Core.Home;
using UtsavNights.Core.Monetisation;
using UtsavNights.Core.Runtime;
using UtsavNights.Core.Settings;
using UtsavNights.Core.Shows;

namespace UtsavNights.Gameplay
{
    /// <summary>What a backup holds, for the confirm step ("Restore this?").</summary>
    public readonly struct BackupSummary
    {
        public BackupSummary(long createdUnix, bool avatar, int decorations, int layouts, int shows, int unlocks)
        {
            CreatedUnix = createdUnix;
            Avatar = avatar;
            Decorations = decorations;
            Layouts = layouts;
            Shows = shows;
            Unlocks = unlocks;
        }

        public long CreatedUnix { get; }

        public bool Avatar { get; }

        public int Decorations { get; }

        public int Layouts { get; }

        public int Shows { get; }

        public int Unlocks { get; }

        public string Describe()
        {
            var bits = new List<string>();
            if (Avatar)
            {
                bits.Add("your avatar");
            }

            bits.Add(Decorations == 1 ? "1 decoration" : $"{Decorations} decorations");
            if (Layouts > 0)
            {
                bits.Add(Layouts == 1 ? "1 saved layout" : $"{Layouts} saved layouts");
            }

            bits.Add(Shows == 1 ? "1 show" : $"{Shows} shows");
            string when = CreatedUnix > 0 ? DateTimeOffset.FromUnixTimeSeconds(CreatedUnix).ToLocalTime().ToString("d MMM yyyy, HH:mm") : "an unknown date";
            return $"From {when}: {string.Join(", ", bits)}, and your settings.";
        }
    }

    /// <summary>
    /// Makes and restores backups (Phase 10 step 1, PRD 13.5, F24). A backup is the save files as they are on disk
    /// (settings, avatar, house with its layouts, shows) plus the timed cracker unlocks; never the purchase. A restore
    /// checks every part before writing anything, sets the current files aside so it can be undone, then the caller
    /// reloads the scene so every service reads the new files.
    /// </summary>
    public sealed class BackupService
    {
        public const string UndoFolder = "restore_undo";

        private static readonly string[] Files = { SettingsStore.FileName, ProfileStore.FileName, HouseStore.FileName, HouseStore.JournalName, ShowStore.FileName, ProgressStore.FileName };
        private readonly Func<long> _clock;

        /// <summary>The game's own folder: pending changes in the running services are saved first and used.</summary>
        private readonly bool _live;

        /// <param name="dataFolder">Tests: another "phone's" folder, read and written without touching the running game.</param>
        public BackupService(string dataFolder = null, Func<long> clock = null)
        {
            _live = dataFolder == null;
            Folder = dataFolder ?? Application.persistentDataPath;
            _clock = clock ?? (() => DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        public string Folder { get; }

        public bool CanUndo => File.Exists(Path.Combine(Folder, UndoFolder, "stamp"));

        /// <summary>The current saves as an archive. Pending changes are written first.</summary>
        public BackupArchive Collect()
        {
            if (_live && Game.Services.TryGet(out SettingsService settings))
            {
                settings.SaveIfDirty();
            }

            if (_live && Game.Services.TryGet(out HomeService home))
            {
                home.Save();
            }

            var archive = new BackupArchive { createdUnix = _clock(), appVersion = Application.version };
            Add(archive, BackupRules.Settings, SettingsStore.FileName);
            Add(archive, BackupRules.Profile, ProfileStore.FileName);
            Add(archive, BackupRules.House, HouseStore.FileName);
            Add(archive, BackupRules.Shows, ShowStore.FileName);
            PlayerProgress progress = _live && Game.Services.TryGet(out ProgressService p) ? p.Current : new ProgressStore(Path.Combine(Folder, ProgressStore.FileName)).Load(_clock());
            var unlocks = new UnlockList();
            foreach (PlayerProgress.TimedUnlock u in progress.unlocks)
            {
                if (u.expiresUnix > _clock())
                {
                    unlocks.unlocks.Add(new PlayerProgress.TimedUnlock { id = u.id, expiresUnix = u.expiresUnix });
                }
            }

            archive.parts[BackupRules.Unlocks] = JsonUtility.ToJson(unlocks);
            return archive;
        }

        public byte[] Export(string password) => BackupCrypto.SealWithPassword(Collect(), password);

        /// <summary>
        /// Checks every part as the game would load it. Returns the first problem in plain words, or null with what the
        /// backup holds. Nothing is written.
        /// </summary>
        public string Check(BackupArchive archive, out BackupSummary summary)
        {
            summary = default;
            if (archive == null)
            {
                return BackupRules.Explain(BackupError.NotABackup);
            }

            if (Parse(archive, out Restored r) is string problem)
            {
                return problem;
            }

            int decorations = r.House != null ? r.House.design.decorations.Count : 0;
            summary = new BackupSummary(archive.createdUnix, r.Profile != null && r.Profile.created, decorations, r.House?.layouts.Count ?? 0, r.Shows?.shows.Count ?? 0, r.Unlocks.unlocks.Count);
            return null;
        }

        /// <summary>Restores the archive: checks it, sets the current files aside, writes the new ones. The caller reloads the scene.</summary>
        public bool Apply(BackupArchive archive, out string error)
        {
            error = Parse(archive, out Restored r);
            if (error != null)
            {
                return false;
            }

            // Nothing left in memory may write over the restored files when the scene unloads.
            if (_live && Game.Services.TryGet(out SettingsService settings))
            {
                settings.SaveIfDirty();
            }

            try
            {
                SetAside();
                string Target(string name) => Path.Combine(Folder, name);
                Write(Target(SettingsStore.FileName), r.Settings != null ? JsonUtility.ToJson(r.Settings, true) : null);
                Write(Target(ProfileStore.FileName), r.Profile != null ? JsonUtility.ToJson(r.Profile, true) : null);
                Write(Target(HouseStore.FileName), r.House != null ? JsonUtility.ToJson(r.House, true) : null);
                if (r.House != null)
                {
                    Delete(Target(HouseStore.JournalName)); // a journal belongs to the house it was written for
                }
                Write(Target(ShowStore.FileName), r.Shows != null ? JsonUtility.ToJson(r.Shows, true) : null);

                // Unlocks join this phone's progress; the purchase on this phone stays as it is.
                var progressStore = new ProgressStore(Target(ProgressStore.FileName));
                PlayerProgress progress = _live && Game.Services.TryGet(out ProgressService service) ? service.Current : progressStore.Load(_clock());
                foreach (PlayerProgress.TimedUnlock u in r.Unlocks.unlocks)
                {
                    PlayerProgress.TimedUnlock mine = progress.unlocks.Find(x => x.id == u.id);
                    if (mine == null)
                    {
                        progress.unlocks.Add(new PlayerProgress.TimedUnlock { id = u.id, expiresUnix = u.expiresUnix });
                    }
                    else
                    {
                        mine.expiresUnix = Math.Max(mine.expiresUnix, u.expiresUnix);
                    }
                }

                progressStore.Save(progress);
                return true;
            }
            catch (Exception e)
            {
                error = "The backup couldn't be written: " + e.Message;
                Debug.LogWarning("[Backup] " + error);
                Undo(out _);
                return false;
            }
        }

        /// <summary>Puts back the files the last restore set aside. The caller reloads the scene.</summary>
        public bool Undo(out string error)
        {
            error = null;
            string aside = Path.Combine(Folder, UndoFolder);
            if (!CanUndo)
            {
                error = "There's nothing to undo.";
                return false;
            }

            try
            {
                foreach (string name in Files)
                {
                    string kept = Path.Combine(aside, name);
                    string target = Path.Combine(Folder, name);
                    if (File.Exists(kept))
                    {
                        AtomicFile.WriteAllText(target, File.ReadAllText(kept));
                    }
                    else
                    {
                        Delete(target);
                    }
                }

                Directory.Delete(aside, true);
                return true;
            }
            catch (Exception e)
            {
                error = "Couldn't undo: " + e.Message;
                return false;
            }
        }

        private sealed class Restored
        {
            public GameSettings Settings;
            public AvatarProfile Profile;
            public HouseState House;
            public ShowLibrary Shows;
            public UnlockList Unlocks = new UnlockList();
        }

        private string Parse(BackupArchive archive, out Restored r)
        {
            r = new Restored();
            if (archive == null)
            {
                return BackupRules.Explain(BackupError.NotABackup);
            }

            if (archive.version > BackupArchive.CurrentVersion)
            {
                return BackupRules.Explain(BackupError.Newer);
            }

            bool any = false;
            try
            {
                if (archive.Part(BackupRules.Settings) is string s)
                {
                    any = true;
                    r.Settings = JsonUtility.FromJson<GameSettings>(s);
                    if (r.Settings == null || r.Settings.version > GameSettings.CurrentVersion)
                    {
                        return Newer("settings");
                    }

                    r.Settings.Migrate();
                    r.Settings.Sanitize();
                    r.Settings.qualityTier = GameSettings.AutoTier; // the new phone finds its own tier
                }

                if (archive.Part(BackupRules.Profile) is string p)
                {
                    any = true;
                    r.Profile = JsonUtility.FromJson<AvatarProfile>(p);
                    if (r.Profile == null || r.Profile.version > AvatarProfile.CurrentVersion)
                    {
                        return Newer("avatar");
                    }

                    r.Profile.Migrate();
                    r.Profile.Sanitize(WardrobeCatalog.Default);
                }

                if (archive.Part(BackupRules.House) is string h)
                {
                    any = true;
                    r.House = JsonUtility.FromJson<HouseState>(h);
                    if (r.House == null || r.House.version > HouseState.CurrentVersion)
                    {
                        return Newer("house");
                    }

                    r.House.Migrate();
                    r.House.Sanitize();
                }

                if (archive.Part(BackupRules.Shows) is string sh)
                {
                    any = true;
                    r.Shows = JsonUtility.FromJson<ShowLibrary>(sh) ?? new ShowLibrary();
                    r.Shows.shows.RemoveAll(x => x == null || string.IsNullOrEmpty(x.id) || string.IsNullOrWhiteSpace(x.name));
                    if (r.Shows.shows.Count > ShowRules.MaxSavedShows)
                    {
                        r.Shows.shows.RemoveRange(ShowRules.MaxSavedShows, r.Shows.shows.Count - ShowRules.MaxSavedShows);
                    }
                }

                if (archive.Part(BackupRules.Unlocks) is string u)
                {
                    r.Unlocks = JsonUtility.FromJson<UnlockList>(u) ?? new UnlockList();
                    long now = _clock();
                    r.Unlocks.unlocks.RemoveAll(x => x == null || string.IsNullOrEmpty(x.id) || x.expiresUnix <= now || x.expiresUnix > now + PlayerProgress.RewardedUnlockSeconds);
                }
            }
            catch (Exception e)
            {
                return "The backup is damaged: " + e.Message;
            }

            return any ? null : "That backup is empty.";
        }

        private static string Newer(string part) => $"The {part} in that backup is from a newer version of the game. Update the game first.";

        private void Add(BackupArchive archive, string part, string file)
        {
            string path = Path.Combine(Folder, file);
            if (File.Exists(path))
            {
                archive.parts[part] = File.ReadAllText(path);
            }
        }

        private void SetAside()
        {
            string aside = Path.Combine(Folder, UndoFolder);
            if (Directory.Exists(aside))
            {
                Directory.Delete(aside, true);
            }

            Directory.CreateDirectory(aside);
            foreach (string name in Files)
            {
                string from = Path.Combine(Folder, name);
                if (File.Exists(from))
                {
                    File.Copy(from, Path.Combine(aside, name));
                }
            }

            File.WriteAllText(Path.Combine(aside, "stamp"), _clock().ToString());
        }

        /// <summary>A part the backup doesn't have leaves this phone's file as it is.</summary>
        private static void Write(string path, string text)
        {
            if (text != null)
            {
                AtomicFile.WriteAllText(path, text);
            }
        }

        private static void Delete(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        [Serializable]
        private sealed class UnlockList
        {
            public List<PlayerProgress.TimedUnlock> unlocks = new List<PlayerProgress.TimedUnlock>();
        }
    }
}

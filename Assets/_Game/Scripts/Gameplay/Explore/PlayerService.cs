using System;
using System.IO;
using UnityEngine;
using UtsavNights.Core;
using UtsavNights.Core.Explore;
using UtsavNights.Core.Runtime;
using UtsavNights.Core.Services;

namespace UtsavNights.Gameplay
{
    /// <summary>Coins or reputation changed (the HUD shows a pop; analytics in step 8).</summary>
    public readonly struct PlayerRewardEvent
    {
        public PlayerRewardEvent(long coins, int reputation, string reason, int levelUp = 0)
        {
            Coins = coins;
            Reputation = reputation;
            Reason = reason;
            LevelUp = levelUp;
        }

        public long Coins { get; }

        public int Reputation { get; }

        /// <summary>A short id: "job", "show", "midnight".</summary>
        public string Reason { get; }

        /// <summary>The level the reward reached, when it was a level up (else 0).</summary>
        public int LevelUp { get; }
    }

    /// <summary>
    /// The player's profile (Phase 12 step 2): coins, reputation and play stats in player.json, counted from what happens
    /// in the game (exploring, jumps, crackers lit, shows, midnights, homes visited). Saved a few seconds after a change,
    /// when the app pauses and at the end; a damaged file starts a fresh record, never a crash.
    /// </summary>
    public sealed class PlayerService : ITickable, IDisposable
    {
        public const string FileName = "player.json";
        public const float SaveDelay = 4f;

        private readonly string _path;
        private float _dirty = -1f;

        public PlayerService(string path = null, Func<long> clock = null)
        {
            _path = path ?? System.IO.Path.Combine(Application.persistentDataPath, FileName);
            Current = Load();
            long now = (clock ?? (() => DateTimeOffset.UtcNow.ToUnixTimeSeconds()))();
            if (Current.firstPlayedUnix == 0)
            {
                Current.firstPlayedUnix = now;
            }

            Current.sessions++;
            MarkDirty();
            Game.Events.Subscribe<ExploreEvent>(OnExplore);
            Game.Events.Subscribe<CrackerIgnitedEvent>(OnIgnited);
            Game.Events.Subscribe<ShowPlayedEvent>(OnShow);
            Game.Events.Subscribe<MidnightEvent>(OnMidnight);
            Game.Events.Subscribe<HomeVisitEvent>(OnVisit);
            Game.Events.Subscribe<AppPausedEvent>(OnPaused);
        }

        public event Action Changed;

        public PlayerRecord Current { get; private set; }

        public string Path => _path;

        public int Level => Reputation.Level(Current.reputation);

        public string Title => Reputation.Title(Current.reputation);

        /// <summary>Rewards the player (jobs, shows, contests). Announces a level up.</summary>
        public void Reward(long coins, int reputation, string reason)
        {
            int before = Level;
            long added = Current.Earn(coins);
            Current.GainReputation(reputation);
            int after = Level;
            MarkDirty();
            Game.Events.Publish(new PlayerRewardEvent(added, reputation, reason, after > before ? after : 0));
        }

        public bool Spend(long coins)
        {
            if (!Current.Spend(coins))
            {
                return false;
            }

            MarkDirty();
            return true;
        }

        public void Tick(float deltaSeconds)
        {
            if (_dirty < 0f)
            {
                return;
            }

            _dirty -= deltaSeconds;
            if (_dirty <= 0f)
            {
                Save();
            }
        }

        public bool Save()
        {
            _dirty = -1f;
            try
            {
                AtomicFile.WriteAllText(_path, JsonUtility.ToJson(Current, true));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Player] could not save {_path}: {e.Message}");
                return false;
            }
        }

        /// <summary>Reads the file again (after a restore wrote a new one).</summary>
        public void Reload()
        {
            Current = Load();
            Changed?.Invoke();
        }

        public void Dispose()
        {
            Game.Events.Unsubscribe<ExploreEvent>(OnExplore);
            Game.Events.Unsubscribe<CrackerIgnitedEvent>(OnIgnited);
            Game.Events.Unsubscribe<ShowPlayedEvent>(OnShow);
            Game.Events.Unsubscribe<MidnightEvent>(OnMidnight);
            Game.Events.Unsubscribe<HomeVisitEvent>(OnVisit);
            Game.Events.Unsubscribe<AppPausedEvent>(OnPaused);
            if (_dirty >= 0f)
            {
                Save();
            }
        }

        private PlayerRecord Load()
        {
            if (!File.Exists(_path))
            {
                return new PlayerRecord();
            }

            try
            {
                PlayerRecord r = JsonUtility.FromJson<PlayerRecord>(File.ReadAllText(_path)) ?? new PlayerRecord();
                r.Migrate();
                r.Sanitize();
                return r;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Player] {_path} unreadable, starting a fresh record: {e.Message}");
                return new PlayerRecord();
            }
        }

        private void MarkDirty()
        {
            if (_dirty < 0f)
            {
                _dirty = SaveDelay;
            }

            Changed?.Invoke();
        }

        private void OnExplore(ExploreEvent e)
        {
            if (e.Started)
            {
                return;
            }

            Current.metresExplored += e.Metres;
            Current.secondsExplored += e.Seconds;
            Current.jumps += e.Jumps;
            MarkDirty();
        }

        private void OnIgnited(CrackerIgnitedEvent e)
        {
            Current.crackersLit++;
            MarkDirty();
        }

        private void OnShow(ShowPlayedEvent e)
        {
            Current.showsPlayed++;
            MarkDirty();
        }

        private void OnMidnight(MidnightEvent e)
        {
            Current.midnights++;
            MarkDirty();
        }

        private void OnVisit(HomeVisitEvent e)
        {
            if (e.Action == "visited" && e.Ok)
            {
                Current.homesVisited++;
                MarkDirty();
            }
        }

        private void OnPaused(AppPausedEvent e)
        {
            if (e.Paused && _dirty >= 0f)
            {
                Save();
            }
        }
    }
}

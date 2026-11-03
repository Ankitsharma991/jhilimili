using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Core.Backup;
using UtsavNights.Core.Explore;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;
using UtsavNights.UI;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>Phase 12 step 2: the profile counts play and rewards, shows on its screen, travels in backups; the controls follow the settings; pause.</summary>
    public class ProfileSmokeTests
    {
        [UnityTest]
        public IEnumerator Profile_CountsRewardsShowsAndBacksUp()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Assert.IsTrue(Game.Services.TryGet(out PlayerService player));
            PlayerRecord before = player.Current.Clone();

            Game.Events.Publish(new CrackerIgnitedEvent("sparkler_classic", false, 5f));
            Game.Events.Publish(new ExploreEvent(false, 30, 120, 3));
            Assert.AreEqual(before.crackersLit + 1, player.Current.crackersLit);
            Assert.AreEqual(before.metresExplored + 120, player.Current.metresExplored, 1e-3f);
            Assert.AreEqual(before.jumps + 3, player.Current.jumps);

            PlayerRewardEvent? reward = null;
            System.Action<PlayerRewardEvent> listen = e => reward = e;
            Game.Events.Subscribe(listen);
            int toNext = Reputation.ToNext(player.Current.reputation);
            player.Reward(40, toNext, "job");
            Game.Events.Unsubscribe(listen);
            Assert.IsTrue(reward.HasValue);
            Assert.AreEqual(Reputation.Level(before.reputation) + 1, reward.Value.LevelUp, "a level up is announced");
            Assert.AreEqual(before.coins + 40, player.Current.coins);
            Assert.IsTrue(player.Save());
            StringAssert.Contains("\"crackersLit\"", File.ReadAllText(player.Path));

            Object.FindFirstObjectByType<SettingsSheet>().OpenProfile();
            var screen = Object.FindFirstObjectByType<ProfileScreen>();
            Assert.IsTrue(screen.IsOpen);
            Assert.AreEqual(player.Current.coins.ToString("N0", System.Globalization.CultureInfo.InvariantCulture), screen.Value("Coins"));
            StringAssert.Contains(player.Title, screen.TitleLine);
            screen.Close();

            BackupArchive archive = new BackupService().Collect();
            StringAssert.Contains("\"coins\"", archive.Part(BackupRules.Player), "the profile goes in backups");

            // Put the record back as it was.
            player.Current.coins = before.coins;
            player.Current.reputation = before.reputation;
            player.Current.crackersLit = before.crackersLit;
            player.Current.metresExplored = before.metresExplored;
            player.Current.jumps = before.jumps;
            player.Save();
        }

        [UnityTest]
        public IEnumerator Controls_FollowTheSettings_AndPauseStopsTheGame()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Assert.IsTrue(Game.Services.TryGet(out SettingsService settings));
            Core.Settings.GameSettings saved = settings.Current;
            var mode = Object.FindFirstObjectByType<ExploreMode>();
            try
            {
                Assert.IsTrue(mode.Open());
                yield return null;
                ExploreController c = mode.Controller;

                // Invert: a drag up looks the other way.
                float pitch = c.Orbit.Pitch;
                c.Look(new Vector2(0f, 20f));
                float normal = c.Orbit.Pitch - pitch;
                settings.Change(s => s.invertLook = true);
                pitch = c.Orbit.Pitch;
                c.Look(new Vector2(0f, 20f));
                Assert.AreEqual(-normal, c.Orbit.Pitch - pitch, 1e-3f, "inverted");

                // Sensitivity doubles the turn.
                settings.Change(s => { s.invertLook = false; s.lookSensitivity = 2f; });
                float yaw = c.Orbit.Yaw;
                c.Look(new Vector2(10f, 0f));
                Assert.AreEqual(10f * ExploreController.LookSensitivity * 2f, Mathf.DeltaAngle(yaw, c.Orbit.Yaw), 1e-3f);

                // Left-handed puts the stick on the right.
                settings.Change(s => s.leftHanded = true);
                var area = (RectTransform)mode.Stick.transform;
                Assert.AreEqual(0.55f, area.anchorMin.x, 1e-3f);

                // Hold to sprint: only while held.
                settings.Change(s => s.holdToSprint = true);
                var hold = Object.FindFirstObjectByType<HoldButton>();
                hold.OnPointerDown(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current));
                Assert.IsTrue(mode.Sprinting);
                hold.OnPointerUp(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current));
                Assert.IsFalse(mode.Sprinting);

                // Pause stops the game behind the menu.
                mode.Pause();
                Assert.IsTrue(mode.Paused);
                Assert.AreEqual(0f, Time.timeScale);
                mode.Resume();
                Assert.AreEqual(1f, Time.timeScale);
                mode.Exit();
            }
            finally
            {
                Time.timeScale = 1f;
                settings.Change(s =>
                {
                    s.invertLook = saved.invertLook;
                    s.lookSensitivity = saved.lookSensitivity;
                    s.leftHanded = saved.leftHanded;
                    s.holdToSprint = saved.holdToSprint;
                });
            }
        }
    }
}

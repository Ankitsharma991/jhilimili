using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Quality;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>Phase 6 step 6: the saved avatar stands in the courtyard and reacts to the show.</summary>
    public class CourtyardAvatarSmokeTests
    {
        private CourtyardAvatar _courtyard;
        private ProfileService _profiles;

        private IEnumerator Load()
        {
            new ProfileStore().Delete();
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            _courtyard = Object.FindFirstObjectByType<CourtyardAvatar>();
            Assert.IsNotNull(_courtyard);
            Assert.IsTrue(Game.Services.TryGet(out _profiles));
        }

        [TearDown]
        public void TearDown() => new ProfileStore().Delete();

        [UnityTest]
        public IEnumerator HiddenUntilSaved_ThenStandsOnItsSpot()
        {
            yield return Load();
            Assert.IsFalse(_courtyard.Visible, "the R0 courtyard is unchanged until the player makes an avatar");

            _profiles.Save(AvatarPresets.All[3], fromPreset: true);
            yield return null;
            Assert.IsTrue(_courtyard.Visible);
            Assert.AreEqual(CourtyardAvatar.Spot, _courtyard.View.transform.position);

            AvatarProfile changed = _profiles.Current.Clone();
            OutfitRules.Equip(changed, WardrobeCatalog.Default, "turban");
            _profiles.Save(changed, fromPreset: false);
            yield return null;
            Assert.IsNotNull(_courtyard.View.Bone(AvatarSkeleton.Head).Find("Garment turban (head)"), "a saved change shows at once");
        }

        [UnityTest]
        public IEnumerator Reacts_IgniteThenCelebrateWithACooldown()
        {
            yield return Load();
            _profiles.Save(AvatarPresets.All[0], fromPreset: true);
            yield return null;

            Game.Events.Publish(new CrackerIgnitedEvent("flowerpot_gold", false, 1f));
            Assert.AreEqual(AvatarClip.Ignite, _courtyard.Animator.Clip);

            _courtyard.Animator.Play(AvatarClip.Idle);
            Game.Events.Publish(new PayoffStartedEvent("rocket_basic", new Vector3(1f, 8f, 1f)));
            Assert.AreEqual(AvatarClip.Celebrate, _courtyard.Animator.Clip);
            _courtyard.Animator.Tick(0.4f);
            float time = _courtyard.Animator.ClipTime;
            Game.Events.Publish(new PayoffStartedEvent("skyshot_green", new Vector3(-1f, 8f, 1f)));
            Assert.AreEqual(time, _courtyard.Animator.ClipTime, "a second burst inside the cooldown doesn't restart the celebration");

            yield return new WaitForSeconds(0.6f);
            float yaw = _courtyard.View.transform.eulerAngles.y;
            Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(yaw, CourtyardAvatar.RestYaw)), 1f, "turns towards the burst");
        }

        [UnityTest]
        public IEnumerator BlocksPlacementOnItsSpot_AndUsesRigidSkinningOnLowTier()
        {
            yield return Load();
            _profiles.Save(AvatarPresets.All[0], fromPreset: true);
            yield return null;

            Assert.IsTrue(Game.Services.TryGet(out PlacementController placement));
            placement.BeginPlacing(Object.FindFirstObjectByType<CrackerRuntime>().Definition);
            Vector2 onAvatar = Camera.main.WorldToScreenPoint(CourtyardAvatar.Spot + Vector3.up * 0.9f);
            Assert.IsFalse(placement.CheckAt(onAvatar).Valid, "no cracker on the avatar");
            placement.Cancel();

            Assert.IsTrue(Game.Services.TryGet(out QualityManager quality));
            quality.SetTierManually(TierDetection.Low);
            yield return null;
            Assert.AreEqual(SkinQuality.Bone2, _courtyard.View.Body.quality);
            quality.SetTierManually(TierDetection.High);
            yield return null;
            Assert.AreEqual(SkinQuality.Auto, _courtyard.View.Body.quality);
        }
    }
}

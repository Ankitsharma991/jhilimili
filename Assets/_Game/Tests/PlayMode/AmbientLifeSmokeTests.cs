using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Core.Neighbourhood;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>Phase 8 step 5: ambient life by tier, Quiet mode, distant bursts and the Night Timeline in the real Main scene.</summary>
    public class AmbientLifeSmokeTests
    {
        private AmbientLife _life;
        private QualityManager _quality;

        private IEnumerator Load()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Assert.IsTrue(Game.Services.TryGet(out _life), "ambient life in the Main scene");
            _quality = Object.FindFirstObjectByType<QualityManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_quality != null)
            {
                _quality.UseAutomaticTier();
            }
        }

        private int Active(string name)
        {
            int n = 0;
            foreach (AvatarView v in _life.GetComponentsInChildren<AvatarView>(false))
            {
                n += v.name == name ? 1 : 0;
            }

            return n;
        }

        [UnityTest]
        public IEnumerator People_FollowTheTier()
        {
            yield return Load();
            Assert.Greater(_life.StreetLamps, 3, "street lamps along the street");
            Assert.IsNotNull(_life.SparkMaterial, "the distant bursts have a spark material");
            foreach (int tier in new[] { 0, 1, 2 })
            {
                _quality.SetTierManually(tier);
                _life.SetMinutes(10f);
                yield return null;
                AmbientPlan want = AmbientPlan.For(tier, false, NightTimeline.At(10f));
                Assert.AreEqual(want.Neighbours, Active("Neighbour"), $"tier {tier} neighbours");
                Assert.AreEqual(want.Crowd, Active("Crowd"), $"tier {tier} crowd");
            }
        }

        [UnityTest]
        public IEnumerator QuietMode_TurnsTheLifeOff()
        {
            yield return Load();
            Assert.IsTrue(Game.Services.TryGet(out SettingsService settings));
            bool was = settings.Current.quietMode;
            try
            {
                _quality.SetTierManually(2);
                _life.SetMinutes(10f);
                settings.Change(s => s.quietMode = true);
                yield return null;
                Assert.AreEqual(0, Active("Neighbour") + Active("Crowd"), "nobody out in Quiet mode");
                int before = _life.BurstsEmitted;
                _life.AdvanceBursts(120f);
                Assert.AreEqual(before, _life.BurstsEmitted, "no distant bursts in Quiet mode");

                settings.Change(s => s.quietMode = false);
                yield return null;
                _life.AdvanceBursts(120f);
                Assert.Greater(_life.BurstsEmitted, before + 5, "distant bursts back after Quiet mode");
                Assert.Greater(Active("Crowd"), 0);
            }
            finally
            {
                settings.Change(s => s.quietMode = was);
            }
        }

        [UnityTest]
        public IEnumerator Night_DarkensTheSkyOverTheSession()
        {
            yield return Load();
            _life.SetMinutes(0f);
            float dusk = RenderSettings.skybox.GetFloat("_Exposure");
            Color duskAmbient = RenderSettings.ambientLight;
            _life.SetMinutes(24f);
            Assert.Less(RenderSettings.skybox.GetFloat("_Exposure"), dusk, "the sky darkens toward late night");
            Assert.Less(RenderSettings.ambientLight.grayscale, duskAmbient.grayscale);
            Assert.AreEqual(NightPhase.Late, _life.Night.Phase);
            _life.SetMinutes(null);
            yield return null;
        }
    }
}

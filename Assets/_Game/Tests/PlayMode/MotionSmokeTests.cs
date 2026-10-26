using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Content;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>A rocket-shaped test cracker built from the flowerpot's assets: launch motion rises to the apex, repeated apex bursts fire, spin runs.</summary>
    public class MotionSmokeTests
    {
        [UnityTest]
        public IEnumerator LaunchSpinAndRepeats_RunInTheRealScene()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            CrackerDefinition pot = SceneCrackers.Find("flowerpot_gold").Definition;
            Assert.IsTrue(Game.Services.TryGet(out CrackerSpawner spawner));

            var rocket = ScriptableObject.CreateInstance<CrackerDefinition>();
            rocket.Id = "test_rocket";
            rocket.FuseSeconds = 0.5f;
            rocket.CooldownSeconds = 0.5f;
            rocket.EffectVolume = new Vector3(2f, 12f, 2f);
            rocket.Surfaces = PlacementSurface.Ground;
            rocket.IgnitionMethods = IgnitionMethod.Lighter;
            rocket.FuseAnchor = new Vector3(0f, 0.3f, 0f);
            rocket.SetQuality(QualityTier.High, new TierQuality(400, true, true));
            rocket.SetQuality(QualityTier.Mid, new TierQuality(300, true, false));
            rocket.SetQuality(QualityTier.Low, new TierQuality(200, false, false));
            ParticleSystem trail = pot.Stages[0].emitter;
            ParticleSystem burst = pot.Stages[1].emitter;
            rocket.MutableStages.Add(new StageDefinition { time = 0.5f, emitter = trail, motion = StageMotion.Launch, launchHeight = 8f, launchRiseSeconds = 1f });
            rocket.MutableStages.Add(new StageDefinition { time = 0.5f, emitter = trail, motion = StageMotion.Spin, spinDegreesPerSecond = 500f });
            rocket.MutableStages.Add(new StageDefinition { time = 1.5f, emitter = burst, anchor = StageAnchor.Apex, launchHeight = 8f, payoff = true, repeatCount = 3, repeatInterval = 0.3f, repeatOffset = new Vector3(1f, 0f, 0f), repeatPattern = Core.Crackers.RepeatPattern.Fan });
            rocket.MutableStages.Add(new StageDefinition { time = 3.5f, stop = true });
            Assert.IsEmpty(rocket.ValidateTimeline());

            CrackerRuntime runtime = spawner.Spawn(rocket, new Vector3(1.2f, 0f, 0.4f), Quaternion.identity);
            Assert.IsNotNull(runtime);
            runtime.Ignite();

            yield return WaitSeconds(0.6f);
            Assert.AreEqual(2, runtime.ActiveMotions, "launch and spin running");

            yield return WaitSeconds(1.1f);
            Assert.AreEqual(1, runtime.ActiveMotions, "launch finished at the apex; spin continues");

            Vector3 apex = runtime.StagePosition(rocket.Stages[2], 0);
            Assert.AreEqual(8f, apex.y - runtime.transform.position.y, 1e-3f);
            Vector3 second = runtime.StagePosition(rocket.Stages[2], 1);
            Vector3 third = runtime.StagePosition(rocket.Stages[2], 2);
            Assert.AreEqual(1f, second.x - apex.x, 1e-3f, "fan: +1");
            Assert.AreEqual(-1f, third.x - apex.x, 1e-3f, "fan: -1");

            yield return WaitSeconds(2.5f);
            Assert.AreEqual(0, runtime.ActiveMotions, "stop clears motions");
            Object.Destroy(rocket);
        }

        private static IEnumerator WaitSeconds(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                yield return null;
            }
        }
    }
}

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Audio;
using UtsavNights.Core.Crackers;
using UtsavNights.Core.Runtime;
using UtsavNights.Effects;
using UtsavNights.Gameplay;
using UtsavNights.Haptics;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>
    /// Phase 2 smoke test: the real Main scene boots, the flowerpot is placed and pickable, ignition runs
    /// the fuse into the payoff with effects playing, and after the cooldown the spent pot is cleared and pooled.
    /// Any error logged during the run fails the test.
    /// </summary>
    public class MainSceneSmokeTests
    {
        private const float Timeout = 20f;

        [UnityTest]
        public IEnumerator Flowerpot_IgnitesToPayoff_AndIsCleared()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;

            Assert.IsTrue(Game.IsBooted, "core services auto-boot");
            Assert.IsTrue(Game.Services.IsRegistered<EffectsService>(), "effects installed");
            Assert.IsTrue(Game.Services.IsRegistered<AudioService>(), "audio installed");
            Assert.IsTrue(Game.Services.IsRegistered<HapticsService>(), "haptics installed");
            Assert.IsTrue(Game.Services.IsRegistered<QualityManager>(), "quality manager installed");
            Assert.IsTrue(Game.Services.IsRegistered<CameraRig>(), "camera rig installed");

            CrackerRuntime pot = SceneCrackers.Find("flowerpot_gold");
            Assert.IsNotNull(pot, "flowerpot in scene");
            Assert.AreEqual("flowerpot_gold", pot.Definition.Id);
            Assert.AreEqual(CrackerState.Placed, pot.StateMachine.State);
            Assert.IsTrue(pot.CanIgnite);

            Camera camera = Camera.main;
            Vector3 fuseScreen = camera.WorldToScreenPoint(pot.FuseWorldPosition);
            Assert.Greater(fuseScreen.z, 0f, "fuse in front of the camera");
            Assert.That(fuseScreen.y, Is.InRange(Screen.height * 0.15f, Screen.height * 0.75f), "fuse well clear of the bottom edge and the sky");
            Assert.AreSame(pot, IgnitionTargets.Shared.PickOnScreen(camera, fuseScreen, 10f), "the lighter can pick the fuse");

            pot.Ignite();
            Assert.AreEqual(CrackerState.Lit, pot.StateMachine.State);
            Assert.IsFalse(pot.CanIgnite, "a lit cracker cannot be lit again");
            yield return null;
            Assert.GreaterOrEqual(pot.ActiveStageEffects, 1, "fuse sparks playing");

            yield return WaitFor(() => pot.StateMachine.State == CrackerState.Payoff, Timeout);
            Assert.AreEqual(CrackerState.Payoff, pot.StateMachine.State);
            yield return new WaitForSeconds(0.5f);
            Assert.GreaterOrEqual(pot.ActiveStageEffects, 1, "fountain playing");

            yield return WaitFor(() => pot.StateMachine.State == CrackerState.Cooldown, Timeout);
            Assert.AreEqual(CrackerState.Cooldown, pot.StateMachine.State);

            yield return WaitFor(() => !pot.gameObject.activeSelf, Timeout);
            Assert.IsFalse(pot.gameObject.activeSelf, "the spent pot is cleared after the cooldown (PRD 6.3)");
            Assert.IsTrue(Game.Services.TryGet(out CrackerSpawner spawner));
            CrackerRuntime again = spawner.Spawn(pot.Definition, pot.transform.position, Quaternion.identity);
            Assert.AreSame(pot, again, "the pooled pot is reused when the player places another");
            Assert.IsTrue(again.CanIgnite);
        }

        private static IEnumerator WaitFor(System.Func<bool> condition, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
        }
    }
}

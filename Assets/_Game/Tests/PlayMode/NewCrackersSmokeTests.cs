using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Core.Crackers;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>The rocket and the chakri spawned in the real Main scene: launch, apex burst, aerial camera, spin, recycling.</summary>
    public class NewCrackersSmokeTests
    {
        [UnityTest]
        public IEnumerator Rocket_Launches_Bursts_AndRespawns()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            CrackerRuntime rocket = SceneCrackers.Spawn("rocket_basic", new Vector3(0.68f, 0f, 0.95f));
            Assert.IsNotNull(rocket, "rocket spawned from the catalog");
            Assert.IsTrue(Game.Services.TryGet(out CameraRig rig));
            Assert.IsTrue(rocket.CanIgnite);

            rocket.Ignite();
            yield return Wait(2.3f);
            Assert.AreEqual(1, rocket.ActiveMotions, "trail launching");
            Assert.IsFalse(rocket.transform.GetChild(0).gameObject.activeSelf, "model hidden at launch");
            Assert.IsTrue(rig.IsAerialActive, "camera follows the rocket up");

            yield return Wait(1.2f);
            Assert.AreEqual(CrackerState.Payoff, rocket.StateMachine.State, "burst at the apex");
            Assert.AreEqual(0, rocket.ActiveMotions);

            Assert.IsTrue(Game.Services.TryGet(out CrackerSpawner spawner));
            yield return WaitUntil(() => !rocket.gameObject.activeSelf, 10f);
            Assert.IsFalse(rocket.gameObject.activeSelf, "spent rocket cleared after its cooldown");
            CrackerRuntime again = SceneCrackers.Spawn("rocket_basic", new Vector3(0.68f, 0f, 0.95f));
            Assert.AreSame(rocket, again, "pooled rocket reused");
            Assert.IsTrue(again.transform.GetChild(0).gameObject.activeSelf, "model shown again");
        }

        [UnityTest]
        public IEnumerator Chakri_SpinsItsSparksAndModel()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            CrackerRuntime chakri = SceneCrackers.Spawn("chakri_classic", new Vector3(-0.68f, 0f, 0.95f));
            Assert.IsNotNull(chakri, "chakri spawned from the catalog");
            Transform model = chakri.transform.GetChild(0);
            Quaternion before = model.localRotation;

            chakri.Ignite();
            yield return Wait(2.6f);
            Assert.AreEqual(CrackerState.Payoff, chakri.StateMachine.State);
            Assert.AreEqual(1, chakri.ActiveMotions, "spin running");
            Assert.Greater(Quaternion.Angle(before, model.localRotation), 1f, "the coil turns");

            yield return WaitUntil(() => !chakri.gameObject.activeSelf, 12f);
            CrackerRuntime again = SceneCrackers.Spawn("chakri_classic", new Vector3(-0.68f, 0f, 0.95f));
            Assert.AreSame(chakri, again);
            Assert.AreEqual(Quaternion.identity, model.localRotation, "a fresh chakri starts upright");
        }

        private static IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                yield return null;
            }
        }

        private static IEnumerator WaitUntil(System.Func<bool> condition, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
        }
    }
}

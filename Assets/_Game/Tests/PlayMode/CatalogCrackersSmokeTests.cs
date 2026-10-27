using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Core.Crackers;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>All five step-9 crackers in the real Main scene, plus the sparkler wave.</summary>
    public class CatalogCrackersSmokeTests
    {
        private static readonly (string id, Vector3 pos)[] Crackers =
        {
            ("sparkler_classic", new Vector3(-1.6f, 0f, 0.2f)),
            ("chain_lar", new Vector3(0.9f, 0f, -0.4f)),
            ("bomb_single", new Vector3(-0.8f, 0f, 1.6f)),
            ("skyshot_green", new Vector3(1.6f, 0f, 1.2f)),
            ("cake_twelve", new Vector3(0f, 0f, 1.9f)),
        };

        [UnityTest]
        public IEnumerator FiveNewCrackers_ReachPayoff_AndClear()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;

            var runtimes = new List<CrackerRuntime>();
            var reachedPayoff = new HashSet<string>();
            foreach (var (id, pos) in Crackers)
            {
                CrackerRuntime runtime = SceneCrackers.Spawn(id, pos);
                Assert.IsNotNull(runtime, $"{id} spawned from the catalog");
                string captured = id;
                runtime.StateMachine.StateChanged += (from, to) =>
                {
                    if (to == CrackerState.Payoff)
                    {
                        reachedPayoff.Add(captured);
                    }
                };
                runtimes.Add(runtime);
            }

            foreach (CrackerRuntime r in runtimes)
            {
                r.Ignite();
            }

            float end = Time.realtimeSinceStartup + 25f;
            while (runtimes.Exists(r => r.gameObject.activeSelf) && Time.realtimeSinceStartup < end)
            {
                yield return null;
            }

            foreach (var (id, _) in Crackers)
            {
                Assert.IsTrue(reachedPayoff.Contains(id), $"{id} reached its payoff");
            }

            Assert.IsFalse(runtimes.Exists(r => r.gameObject.activeSelf), "all five cleared after their cooldowns");
        }

        [UnityTest]
        public IEnumerator Sparkler_CanBeWaved_AndReturnsHome()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Assert.IsTrue(Game.Services.TryGet(out WaveController wave));
            Vector3 home = new Vector3(-1.2f, 0f, 0.4f);
            CrackerRuntime sparkler = SceneCrackers.Spawn("sparkler_classic", home);
            Assert.IsNotNull(sparkler);
            Assert.IsTrue(sparkler.Definition.Wavable);

            Camera cam = Camera.main;
            Vector2 tip = cam.WorldToScreenPoint(sparkler.FuseWorldPosition);
            Assert.IsFalse(wave.TryClaim(99, tip), "an unlit sparkler cannot be picked up");

            sparkler.Ignite();
            yield return Wait(1.5f);
            Assert.AreEqual(CrackerState.Payoff, sparkler.StateMachine.State);
            Assert.AreEqual(2, sparkler.ActiveMotions, "stars and core follow the tip");

            Assert.IsTrue(wave.TryClaim(99, tip), "a burning sparkler can be picked up by a touch");
            Assert.AreSame(sparkler, wave.Held);
            yield return null;
            Assert.IsFalse(wave.IsWaving, "with no real finger behind that touch, it is let go");

            wave.Grab(sparkler, -1, tip);
            for (int i = 0; i < 30; i++)
            {
                wave.MoveTo(tip + new Vector2(Screen.width * 0.25f, Screen.height * 0.1f), 1f / 30f);
                yield return null;
            }

            Assert.Greater(Vector3.Distance(sparkler.transform.position, home), 0.3f, "it follows the finger");
            wave.Release();
            Assert.AreEqual(home, sparkler.transform.position, "released sparkler returns to its spot");
            Assert.IsFalse(wave.IsWaving);
        }

        private static IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                yield return null;
            }
        }
    }
}

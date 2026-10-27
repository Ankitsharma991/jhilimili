using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Core.Crackers;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;
using UtsavNights.UI;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>Finale mode in the real Main scene (F13): needs 3 crackers, lights them on schedule, ends, replays.</summary>
    public class FinaleSmokeTests
    {
        [UnityTest]
        public IEnumerator Finale_LightsEveryone_Ends_AndReplays()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Assert.IsTrue(Game.Services.TryGet(out FinaleController finale));
            Assert.IsTrue(Game.Services.TryGet(out CrackerSpawner spawner));
            FinaleButton button = Object.FindFirstObjectByType<FinaleButton>();
            TrayView tray = Object.FindFirstObjectByType<TrayView>();
            Assert.IsNotNull(button);

            // Only the starting flowerpot is placed: not enough.
            Assert.AreEqual(1, finale.ReadyCount);
            Assert.IsFalse(finale.CanStart);
            button.Press();
            StringAssert.Contains("Place 2 more", tray.HintText, "the button explains what is missing");

            SceneCrackers.Spawn("chakri_classic", new Vector3(-1.1f, 0f, 0.3f));
            SceneCrackers.Spawn("bomb_single", new Vector3(1.1f, 0f, 0.3f));
            SceneCrackers.Spawn("rocket_basic", new Vector3(0.9f, 0f, 1.6f));
            Assert.AreEqual(4, finale.ReadyCount);

            var lit = new HashSet<CrackerRuntime>();
            var payoffs = new HashSet<CrackerRuntime>();
            foreach (CrackerRuntime c in spawner.Active)
            {
                CrackerRuntime captured = c;
                c.StateMachine.StateChanged += (from, to) =>
                {
                    if (to == CrackerState.Lit)
                    {
                        lit.Add(captured);
                    }

                    if (to == CrackerState.Payoff)
                    {
                        payoffs.Add(captured);
                    }
                };
            }

            button.Press();
            Assert.AreEqual(FinaleController.FinaleState.Running, finale.State);
            Assert.AreEqual(4, finale.Total);
            StringAssert.StartsWith("Finale ", button.Label);

            float end = Time.realtimeSinceStartup + 30f;
            while (finale.State == FinaleController.FinaleState.Running && Time.realtimeSinceStartup < end)
            {
                yield return null;
            }

            Assert.AreEqual(FinaleController.FinaleState.Idle, finale.State, "the finale ends");
            Assert.AreEqual(4, lit.Count, "every chosen cracker was lit");
            Assert.AreEqual(4, payoffs.Count, "every chosen cracker reached its payoff");
            Assert.IsTrue(finale.CanReplay);
            yield return null;
            Assert.AreEqual("Replay", button.Label);

            button.Press();
            Assert.AreEqual(FinaleController.FinaleState.Running, finale.State, "replay places the same crackers again and runs");
            Assert.AreEqual(4, finale.Total);
            spawner.ClearAll();
        }
    }
}

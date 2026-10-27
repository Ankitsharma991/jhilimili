using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Gameplay;
using UtsavNights.UI;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>Phase 3 step 13 tools in the real Main scene: the blind sound test and the stress finale.</summary>
    public class MeasureToolsSmokeTests
    {
        [UnityTest]
        public IEnumerator BlindSoundTest_PlaysScoresAndSaves()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            MeasureTools tools = Object.FindFirstObjectByType<MeasureTools>();
            Assert.IsNotNull(tools, "measure tools in the HUD");
            long before = File.Exists(MeasureTools.FilePath) ? new FileInfo(MeasureTools.FilePath).Length : 0;

            tools.OpenSoundTest();
            Assert.IsTrue(tools.IsOpen);
            Assert.IsTrue(InputBlockers.IsBlocked(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)), "the lighter is blocked while testing");
            tools.BeginTest();
            Assert.AreEqual(8, tools.Test.Count, "all eight crackers");
            Assert.IsTrue(tools.IsPlayingSound, "the first sound starts at once");
            yield return new WaitForSecondsRealtime(0.3f);

            tools.PlayAgain();
            for (int i = 0; i < 8; i++)
            {
                tools.Guess(i < 6 ? tools.Test.Current : null);
                yield return null;
            }

            Assert.IsTrue(tools.Test.IsComplete);
            Assert.AreEqual(6, tools.Test.CorrectCount);
            Assert.IsTrue(tools.Test.Passed);
            Assert.AreEqual(1, tools.Test.Answers[0].Replays);
            Assert.Greater(new FileInfo(MeasureTools.FilePath).Length, before, "results appended");
            StringAssert.Contains("\"type\":\"test_complete\"", File.ReadAllText(MeasureTools.FilePath));
            tools.Close();
            Assert.IsFalse(tools.IsOpen);
        }

        [UnityTest]
        public IEnumerator StressFinale_PlacesSixAndRuns()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            MeasureTools tools = Object.FindFirstObjectByType<MeasureTools>();
            Assert.IsTrue(UtsavNights.Core.Runtime.Game.Services.TryGet(out FinaleController finale));
            Assert.IsTrue(tools.StartStressFinale(), "finale started");
            Assert.AreEqual(FinaleController.FinaleState.Running, finale.State);
            Assert.GreaterOrEqual(finale.Total, 3);
            Assert.IsFalse(tools.StartStressFinale(), "not while one is running");
        }
    }
}

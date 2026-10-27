using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Gameplay;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>Phase 5 step 1: a store build has no developer overlay or tools and keeps no playtest log.</summary>
    public class StoreBuildSmokeTests
    {
        [UnityTest]
        public IEnumerator StoreFlavor_HasNoOverlayToolsOrPlaytestLog()
        {
            long before = File.Exists(PlaytestRecorder.FilePath) ? new FileInfo(PlaytestRecorder.FilePath).Length : 0;
            using (BuildFlavor.OverrideForTests(true))
            {
                SceneManager.LoadScene("Main");
                yield return null;
                yield return null;
                DevOverlay overlay = Object.FindFirstObjectByType<DevOverlay>();
                Assert.IsNotNull(overlay, "the component is in the scene for every build");
                Assert.IsFalse(overlay.enabled, "switched off, so the corner triple-tap does nothing");
                Assert.IsNull(overlay.transform.Find("DevOverlay"), "no overlay canvas, stats or tool buttons were built");
                overlay.Toggle();
                Assert.IsFalse(overlay.Visible);
                overlay.AddTool("Store: refund", () => Assert.Fail("store builds take no tools"));
                Assert.IsNull(overlay.transform.Find("DevOverlay"));

                long after = File.Exists(PlaytestRecorder.FilePath) ? new FileInfo(PlaytestRecorder.FilePath).Length : 0;
                Assert.AreEqual(before, after, "session start and scene ready were not written");
            }
        }

        [UnityTest]
        public IEnumerator DeviceTestBuild_StillHasTheOverlay()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            DevOverlay overlay = Object.FindFirstObjectByType<DevOverlay>();
            Assert.IsTrue(overlay.enabled);
            overlay.Toggle();
            Assert.IsTrue(overlay.Visible);
            overlay.Toggle();
        }
    }
}

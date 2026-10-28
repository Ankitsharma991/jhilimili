#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.UI;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>
    /// Phase 5 step 3: the game allocates almost no managed memory per frame in the real Main scene, idle
    /// and during the six-cracker stress finale, so the garbage collector never causes frame spikes
    /// (PRD 14). Measured with the profiler on the player loop only: in the editor the plain "GC Allocated
    /// In Frame" counter also counts editor tools (Google's dependency manager alone allocates about
    /// 300 KB per editor tick), which never ship. On a phone, gc_kb_per_frame in the playtest log of a
    /// development APK gives the same number.
    /// </summary>
    public class PerformanceGuardTests
    {
        private const float IdleLimitKb = 1f;
        private const float ShowLimitKb = 4f;

        [UnityTest]
        public IEnumerator Idle_AllocatesAlmostNothingPerFrame()
        {
            SceneManager.LoadScene("Main");
            yield return WaitSeconds(2f);
            var result = new float[2];
            yield return Measure(60, result);
            Debug.Log($"[UTSAV_PERF] idle gc_kb_per_frame avg={result[0]:0.00} max={result[1]:0.00}");
            Assert.Less(result[0], IdleLimitKb, "average KB allocated per frame while idle");
        }

        [UnityTest]
        public IEnumerator StressFinale_AllocationsStaySmall()
        {
            SceneManager.LoadScene("Main");
            yield return WaitSeconds(2f);
            MeasureTools tools = Object.FindFirstObjectByType<MeasureTools>();
            Assert.IsTrue(tools.StartStressFinale(), "finale started");

            // Skip the first second: placing and the first ignitions fill the pools once.
            yield return WaitSeconds(1f);
            var result = new float[2];
            yield return Measure(180, result);
            Debug.Log($"[UTSAV_PERF] stress finale gc_kb_per_frame avg={result[0]:0.00} max={result[1]:0.00}");
            Assert.Less(result[0], ShowLimitKb, "average KB allocated per frame during the finale");
        }

        [UnityTest]
        public IEnumerator IdleWithTheCourtyardAvatar_StillAllocatesAlmostNothing()
        {
            new Gameplay.ProfileStore().Delete();
            SceneManager.LoadScene("Main");
            yield return null;
            Assert.IsTrue(Core.Runtime.Game.Services.TryGet(out Gameplay.ProfileService profiles));
            profiles.Save(Core.Avatar.AvatarPresets.All[1], fromPreset: true);
            yield return WaitSeconds(2f);
            Assert.IsTrue(Object.FindFirstObjectByType<Gameplay.CourtyardAvatar>().Visible);
            var result = new float[2];
            yield return Measure(60, result);
            Debug.Log($"[UTSAV_PERF] idle with avatar gc_kb_per_frame avg={result[0]:0.00} max={result[1]:0.00}");
            new Gameplay.ProfileStore().Delete();
            Assert.Less(result[0], IdleLimitKb, "the animated avatar adds no per-frame garbage");
        }

        private static IEnumerator WaitSeconds(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
        }

        /// <summary>Average and largest KB the player loop allocated per frame over the next frames.</summary>
        private static IEnumerator Measure(int frames, float[] result)
        {
            ProfilerDriver.ClearAllFrames();
            ProfilerDriver.profileEditor = false;
            ProfilerDriver.enabled = true;
            for (int i = 0; i < frames + 2; i++)
            {
                yield return null;
            }

            ProfilerDriver.enabled = false;
            double sum = 0;
            float max = 0f;
            int count = 0;
            for (int frame = ProfilerDriver.firstFrameIndex + 1; frame < ProfilerDriver.lastFrameIndex; frame++)
            {
                using (HierarchyFrameDataView view = ProfilerDriver.GetHierarchyFrameDataView(frame, 0,
                           HierarchyFrameDataView.ViewModes.Default, HierarchyFrameDataView.columnGcMemory, false))
                {
                    if (!view.valid)
                    {
                        continue;
                    }

                    float kb = view.GetItemColumnDataAsFloat(view.GetRootItemID(), HierarchyFrameDataView.columnGcMemory) / 1024f;
                    sum += kb;
                    max = Mathf.Max(max, kb);
                    count++;
                }
            }

            Assert.Greater(count, frames / 2, "the profiler recorded the frames");
            result[0] = (float)(sum / count);
            result[1] = max;
        }
    }
}
#endif

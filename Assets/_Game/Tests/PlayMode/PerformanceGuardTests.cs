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

        [UnityTest]
        public IEnumerator DecoratedCourtyardAndDecorationMode_AllocateAlmostNothing()
        {
            new Gameplay.HouseStore().Delete();
            SceneManager.LoadScene("Main");
            yield return null;
            Assert.IsTrue(Core.Runtime.Game.Services.TryGet(out Gameplay.HomeService home));
            HomeLoad.FillToBudget(home.Design, Core.Quality.TierDetection.High);
            home.Refresh();
            yield return WaitSeconds(2f);
            var result = new float[2];
            yield return Measure(60, result);
            Debug.Log($"[UTSAV_PERF] idle decorated gc_kb_per_frame avg={result[0]:0.00} max={result[1]:0.00}");
            Assert.Less(result[0], IdleLimitKb, "placed decorations add no per-frame garbage");

            UI.DecorationMode mode = Object.FindFirstObjectByType<UI.DecorationMode>();
            mode.Open();
            yield return WaitSeconds(1.5f);
            yield return Measure(60, result);
            Debug.Log($"[UTSAV_PERF] decoration mode gc_kb_per_frame avg={result[0]:0.00} max={result[1]:0.00}");
            mode.Done();
            new Gameplay.HouseStore().Delete();
            Assert.Less(result[0], IdleLimitKb, "decoration mode with nothing touched adds no per-frame garbage");
        }

        [UnityTest]
        public IEnumerator WalkingThroughTheSettlement_AllocatesLittle()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            Gameplay.QualityManager quality = Object.FindFirstObjectByType<Gameplay.QualityManager>();
            quality.SetTierManually(2);
            try
            {
                var walk = Object.FindFirstObjectByType<UI.WalkMode>();
                walk.Open();
                yield return WaitSeconds(1.5f);

                // Out of the gate and along the whole street: chunks change detail on the way.
                Assert.IsTrue(walk.Controller.WalkTo(new Vector3(-24f, 0f, 6.8f)));
                yield return WaitSeconds(1f);
                var result = new float[2];
                yield return Measure(120, result);
                Debug.Log($"[UTSAV_PERF] walking gc_kb_per_frame avg={result[0]:0.00} max={result[1]:0.00}");
                Assert.IsTrue(walk.Controller.Agent.Walking || walk.Controller.Agent.Position.X < -10f, "it walked");
                walk.Home();
                Assert.Less(result[0], ShowLimitKb, "streaming chunks while walking stays within the show limit");
            }
            finally
            {
                quality.UseAutomaticTier();
            }
        }

        [UnityTest]
        public IEnumerator RecordingAClip_AllocatesAlmostNothingPerFrame()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Assert.IsTrue(Core.Runtime.Game.Services.TryGet(out Gameplay.ClipRecorder recorder));
            var encoder = new NullClipEncoder();
            Gameplay.ClipRecorder.EncoderFactory = () => encoder;
            Gameplay.ClipRecorder.TestSource = Texture2D.grayTexture;
            try
            {
                Assert.IsTrue(recorder.StartRecording(Core.Clips.ClipRules.MaxSeconds), recorder.Last.Error);
                yield return WaitSeconds(0.5f);
                var result = new float[2];
                yield return Measure(90, result);
                Debug.Log($"[UTSAV_PERF] recording gc_kb_per_frame avg={result[0]:0.00} max={result[1]:0.00} frames={encoder.Frames}");
                Assert.Greater(encoder.Frames, 20, "frames reached the encoder");
                Assert.Less(result[0], IdleLimitKb, "capturing, packing and reading back each frame adds no garbage");
            }
            finally
            {
                recorder.Cancel();
                Gameplay.ClipRecorder.TestSource = null;
                Gameplay.ClipRecorder.EncoderFactory = Gameplay.ClipRecorder.DefaultEncoder;
            }
        }

        [UnityTest]
        public IEnumerator PlayingAShow_AllocationsStaySmall()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Assert.IsTrue(Core.Runtime.Game.Services.TryGet(out Gameplay.ShowPlayer player));
            // Diwali night (with aerials) where the spot allows it, else Gentle start.
            Core.Shows.ShowDef[] templates = Core.Shows.ShowTemplates.All;
            Core.Shows.ShowDef show = (player.Check(templates[1]).Count == 0 ? templates[1] : templates[0]).Clone();
            Assert.IsTrue(player.Play(show), string.Join("; ", player.Check(show)));
            try
            {
                yield return WaitSeconds(2f);
                var result = new float[2];
                yield return Measure(120, result);
                Debug.Log($"[UTSAV_PERF] show gc_kb_per_frame avg={result[0]:0.00} max={result[1]:0.00}");
                Assert.Less(result[0], ShowLimitKb, "a show stays within the finale's limit");
            }
            finally
            {
                player.Stop();
            }
        }

        [UnityTest]
        public IEnumerator InsideARoom_AllocatesAlmostNothingPerFrame()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            var mode = Object.FindFirstObjectByType<InteriorMode>();
            Assert.IsTrue(mode.GoInside());
            yield return WaitSeconds(Gameplay.InteriorController.FadeSeconds * 2f + Gameplay.CameraRig.OverrideSeconds + 0.3f);
            Assert.IsTrue(mode.Inside);
            var result = new float[2];
            yield return Measure(90, result);
            Debug.Log($"[UTSAV_PERF] inside gc_kb_per_frame avg={result[0]:0.00} max={result[1]:0.00} triangles={mode.Controller.Triangles}");
            mode.Leave();
            yield return WaitSeconds(Gameplay.InteriorController.FadeSeconds * 2f + 0.2f);
            Assert.Less(result[0], IdleLimitKb, "a room adds no per-frame garbage");
        }

        [UnityTest]
        public IEnumerator NewYearMidnight_AllocationsStaySmall()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Assert.IsTrue(Core.Runtime.Game.Services.TryGet(out Gameplay.SettingsService settings));
            string before = settings.Current.festival;
            settings.Change(s => s.festival = Core.Festivals.Festivals.NewYearId);
            yield return null;
            var countdown = Object.FindFirstObjectByType<NewYearCountdown>();
            try
            {
                Assert.IsTrue(countdown.Begin());
                countdown.Advance(Core.Festivals.MidnightCountdown.Seconds + 0.05f);
                yield return WaitSeconds(1.5f);
                var result = new float[2];
                yield return Measure(120, result);
                Debug.Log($"[UTSAV_PERF] midnight gc_kb_per_frame avg={result[0]:0.00} max={result[1]:0.00} show={countdown.ShowPlayed}");
                Assert.Less(result[0], ShowLimitKb, "the Midnight show and the surge stay within the finale's limit");
            }
            finally
            {
                if (Core.Runtime.Game.Services.TryGet(out Gameplay.ShowPlayer player))
                {
                    player.Stop();
                }

                settings.Change(s => s.festival = before);
            }
        }

        [UnityTest]
        public IEnumerator ExploringTheBazaar_AllocationsStaySmall()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Gameplay.QualityManager quality = Object.FindFirstObjectByType<Gameplay.QualityManager>();
            quality.SetTierManually(2);
            var mode = Object.FindFirstObjectByType<ExploreMode>();
            try
            {
                Assert.IsTrue(mode.Open());
                yield return null;
                mode.Controller.Teleport(new Vector3(60f, 0f, 6.8f), 90f);
                Object.FindFirstObjectByType<Gameplay.DistrictView>().Refresh();
                mode.SetSprint(true);
                mode.Stick.Set(new Vector2(0f, 1f));
                yield return WaitSeconds(1f);
                var result = new float[2];
                yield return Measure(120, result);
                Debug.Log($"[UTSAV_PERF] bazaar gc_kb_per_frame avg={result[0]:0.00} max={result[1]:0.00} x={mode.Controller.Player.position.x:0}");
                Assert.Less(result[0], ShowLimitKb, "sprinting down the bazaar road, tiles streaming, stays within the show limit");
            }
            finally
            {
                mode.Stick.Set(Vector2.zero);
                mode.Exit();
                quality.UseAutomaticTier();
            }
        }

        [UnityTest]
        public IEnumerator RidingTheBazaar_WithTraffic_AllocationsStaySmall()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Gameplay.QualityManager quality = Object.FindFirstObjectByType<Gameplay.QualityManager>();
            quality.SetTierManually(2);
            var mode = Object.FindFirstObjectByType<ExploreMode>();
            try
            {
                Assert.IsTrue(mode.Open());
                yield return null;
                mode.Controller.Teleport(new Vector3(48f, 0f, 7f), 90f);
                Object.FindFirstObjectByType<Gameplay.DistrictView>().Refresh();
                yield return null;
                yield return null;
                Assert.IsTrue(mode.Controller.Mount());
                mode.Stick.Set(new Vector2(0f, 1f));
                yield return WaitSeconds(1f);
                var result = new float[2];
                yield return Measure(120, result);
                Debug.Log($"[UTSAV_PERF] bazaar ride gc_kb_per_frame avg={result[0]:0.00} max={result[1]:0.00} x={mode.Controller.Player.position.x:0} kmh={Core.Explore.ScooterModel.Kmh(mode.Controller.Speed)}");
                Assert.Less(result[0], ShowLimitKb, "riding down the bazaar road through the traffic and the crowd stays within the show limit");
            }
            finally
            {
                mode.Stick.Set(Vector2.zero);
                mode.Exit();
                quality.UseAutomaticTier();
            }
        }

        [UnityTest]
        public IEnumerator TheDistrictsShow_AllocationsStaySmall()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Gameplay.QualityManager quality = Object.FindFirstObjectByType<Gameplay.QualityManager>();
            quality.SetTierManually(2);
            var mode = Object.FindFirstObjectByType<ExploreMode>();
            try
            {
                Assert.IsTrue(mode.Open());
                yield return null;
                Core.District.TownShowPlace chowk = Core.District.TownShows.Places(Core.District.DistrictGenerator.Bazaar)[1];
                mode.Controller.Teleport(new Vector3(chowk.WatchX - 22f, 0f, chowk.WatchZ - 20f), 45f);
                Object.FindFirstObjectByType<Gameplay.DistrictView>().Refresh();
                yield return WaitSeconds(0.5f);
                Assert.IsTrue(Core.Runtime.Game.Services.TryGet(out Gameplay.TownFireworks town));
                Assert.IsTrue(town.StartShow(1));

                // Past the opening, once each kind of cracker has been made: the show's middle and its finale.
                yield return WaitSeconds(9f);
                var result = new float[2];
                yield return Measure(240, result);
                Debug.Log($"[UTSAV_PERF] town show gc_kb_per_frame avg={result[0]:0.00} max={result[1]:0.00} cues={town.CuesFired}");
                Assert.Less(result[0], ShowLimitKb, "a show on the chowk, with the crowd watching, stays within the show limit");
            }
            finally
            {
                mode.Exit();
                quality.UseAutomaticTier();
            }
        }

        /// <summary>Takes frames and throws them away, so only the recorder's own garbage is measured.</summary>
        private sealed class NullClipEncoder : Gameplay.IClipEncoder
        {
            public int Frames;

            public bool IsFinished => true;

            public string Error => string.Empty;

            public int FramesWritten => Frames;

            public bool Begin(string path, Core.Clips.ClipFormat format, int sampleRate, int channels) => true;

            public bool AddVideo(Unity.Collections.NativeArray<byte> frame, long ptsUs)
            {
                Frames++;
                return true;
            }

            public void AddAudio(byte[] pcm, int length, long ptsUs)
            {
            }

            public void Finish()
            {
            }

            public void Cancel()
            {
            }
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

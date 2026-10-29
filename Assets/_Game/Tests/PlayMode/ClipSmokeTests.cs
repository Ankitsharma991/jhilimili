using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Core.Clips;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;
using UtsavNights.UI;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>
    /// Phase 9 step 6 (F14): frames are packed into I420 on the GPU the right way up and in the right colours, a real MP4 with
    /// H.264 video and AAC sound comes out (ffmpeg stands in for the phone's encoder in the editor), it shares, and the
    /// record button and the designer's Clip button drive it.
    /// </summary>
    public class ClipSmokeTests
    {
        /// <summary>Keeps every frame it is given (no file), for checking what the GPU produced.</summary>
        private sealed class CapturingEncoder : IClipEncoder
        {
            public readonly List<byte[]> Frames = new List<byte[]>();
            public long AudioBytes;
            private bool _done;

            public bool IsFinished => _done;

            public string Error => string.Empty;

            public int FramesWritten => Frames.Count;

            public string Path;

            public bool Begin(string path, ClipFormat format, int sampleRate, int channels)
            {
                Path = path;
                return true;
            }

            public bool AddVideo(NativeArray<byte> frame, long ptsUs)
            {
                Frames.Add(frame.ToArray());
                return true;
            }

            public void AddAudio(byte[] pcm, int length, long ptsUs) => AudioBytes += length;

            public void Finish()
            {
                File.WriteAllBytes(Path, new byte[] { 0 });
                _done = true;
            }

            public void Cancel() => _done = true;
        }

        [TearDown]
        public void TearDown()
        {
            ClipRecorder.EncoderFactory = ClipRecorder.DefaultEncoder;
            ClipRecorder.TestSource = null;
            if (Game.Services.TryGet(out ClipRecorder recorder))
            {
                recorder.Cancel();
            }
        }

        private static IEnumerator Load()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
        }

        private static IEnumerator WaitIdle(ClipRecorder recorder, float seconds = 30f)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (recorder.State != ClipRecorder.RecorderState.Idle && Time.realtimeSinceStartup < end)
            {
                yield return null;
            }

            Assert.AreEqual(ClipRecorder.RecorderState.Idle, recorder.State, "the clip finished");
        }

        [UnityTest]
        public IEnumerator Frames_AreTheRightWayUp_InTheRightColours()
        {
            yield return Load();
            Assert.IsTrue(Game.Services.TryGet(out ClipRecorder recorder));

            // A test card: red across the top half, blue across the bottom (a texture's first row is its bottom).
            var card = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    card.SetPixel(x, y, y >= 32 ? Color.red : Color.blue);
                }
            }

            card.Apply();
            ClipRecorder.TestSource = card;

            var encoder = new CapturingEncoder();
            ClipRecorder.EncoderFactory = () => encoder;
            Assert.IsTrue(recorder.StartRecording(ClipRules.MaxSeconds), recorder.Last.Error);
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.IsFalse(recorder.Stop(), "too short to stop by hand");
            Assert.IsTrue(recorder.Stop(force: true));
            yield return WaitIdle(recorder);
            ClipRecorder.TestSource = null;
            Object.Destroy(card);

            Assert.IsTrue(recorder.Last.Ok, recorder.Last.Error);
            Assert.Greater(recorder.FramesCaptured, 3, "a frame was taken each frame, up to 30 a second");
            Assert.AreEqual(0, recorder.ReadbackErrors, "the GPU readbacks succeeded");
            Assert.Greater(encoder.Frames.Count, 3, "frames arrived from the GPU");
            ClipFormat f = recorder.Format;
            Assert.AreEqual(f.FrameBytes, encoder.Frames[0].Length, "one readback is one I420 frame");
            byte[] rgb = I420.ToRgb(encoder.Frames[encoder.Frames.Count - 1], f.Width, f.Height);
            Color top = Pixel(rgb, f.Width, f.Width / 2, f.Height / 8);
            Color bottom = Pixel(rgb, f.Width, f.Width / 2, f.Height * 7 / 8);
            Assert.Greater(top.r, 0.8f, $"red at the top of the clip, got {top}");
            Assert.Less(top.b, 0.25f, $"red at the top of the clip, got {top}");
            Assert.Greater(bottom.b, 0.8f, $"blue at the bottom, got {bottom}");
            Assert.Less(bottom.r, 0.25f, $"blue at the bottom, got {bottom}");
            Assert.IsNotNull(recorder.Thumbnail, "the share sheet has a picture");
            Color thumbTop = recorder.Thumbnail.GetPixel(recorder.Thumbnail.width / 2, recorder.Thumbnail.height * 7 / 8);
            Assert.Greater(thumbTop.r, 0.8f, "the thumbnail is the right way up too");
        }

        [UnityTest]
        public IEnumerator Clip_IsARealMp4_WithVideoAndSound_AndShares()
        {
            yield return Load();
            if (!FfmpegClipEncoder.Available())
            {
                Assert.Inconclusive("ffmpeg isn't on the PATH; the editor can't make real clips here.");
            }

            Assert.IsTrue(Game.Services.TryGet(out ClipRecorder recorder));
            ClipRecorder.TestSource = Texture2D.grayTexture;
            var source = new GameObject("Tone").AddComponent<AudioSource>();
            source.clip = AudioClip.Create("tone", 48000, 1, 48000, false);
            var samples = new float[48000];
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = 0.3f * Mathf.Sin(i * 0.06f);
            }

            source.clip.SetData(samples, 0);
            source.loop = true;
            source.Play();

            Assert.IsTrue(recorder.StartRecording(ClipRules.MaxSeconds), recorder.Last.Error);
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.IsTrue(recorder.Stop(force: true));
            yield return WaitIdle(recorder);
            Object.Destroy(source.gameObject);
            Assert.IsTrue(recorder.Last.Ok, recorder.Last.Error);
            Assert.IsTrue(File.Exists(recorder.Last.Path));

            string probe = Run("ffprobe", $"-v error -show_entries stream=codec_type,codec_name,width,height -of compact \"{recorder.Last.Path}\"");
            StringAssert.Contains("codec_name=h264", probe);
            StringAssert.Contains($"width={recorder.Format.Width}", probe);
            StringAssert.Contains("codec_type=audio", probe, "the game's sound is in the clip");

            var shared = new List<ClipSharedEvent>();
            System.Action<ClipSharedEvent> onShared = e => shared.Add(e);
            Game.Events.Subscribe(onShared);
            Assert.AreEqual(string.Empty, ClipSharer.Share(recorder.Last, ShareTarget.WhatsApp));
            Game.Events.Unsubscribe(onShared);
            Assert.AreEqual(1, shared.Count);
            Assert.AreEqual(ShareTarget.WhatsApp, shared[0].Target);
            ClipRecorder.DeleteClip(recorder.Last.Path);
            StringAssert.Contains("isn't on the phone", ClipSharer.Share(recorder.Last, ShareTarget.Other));
        }

        [UnityTest]
        public IEnumerator Buttons_RecordAndShowClips()
        {
            yield return Load();
            Assert.IsTrue(Game.Services.TryGet(out ClipRecorder recorder));
            var encoder = new CapturingEncoder();
            ClipRecorder.EncoderFactory = () => encoder;
            var button = Object.FindFirstObjectByType<ClipButton>();
            yield return null;
            Assert.IsNotNull(button.Button, "the Record button is on screen");
            StringAssert.Contains("Record", button.Label);
            button.Press();
            Assert.IsTrue(recorder.IsRecording);
            yield return new WaitForSecondsRealtime(0.3f);
            button.Press();
            Assert.IsTrue(recorder.IsRecording, "Stop waits for 10 seconds");
            Assert.IsTrue(recorder.Stop(force: true));
            yield return WaitIdle(recorder);
            Assert.IsTrue(button.SheetOpen, "the share sheet opens with the clip");
            StringAssert.StartsWith("Your clip", Object.FindFirstObjectByType<ClipButton>().transform.parent.Find("ClipSheet/Title/Text").GetComponent<TMPro.TextMeshProUGUI>().text);
            button.DeleteClip();
            Assert.IsFalse(button.SheetOpen);

            var designer = Object.FindFirstObjectByType<ShowDesigner>();
            designer.Open();
            designer.Load(Core.Shows.ShowTemplates.All[0], asCopy: true);
            Assert.IsTrue(designer.RecordClip(), designer.Status);
            Assert.IsTrue(recorder.IsRecording);
            Assert.AreEqual(ClipRules.ShowClipSeconds(designer.Draft.Length(Game.Services.Get<ShowPlayer>().Timing)), recorder.MaxSeconds, 1e-3f);
            recorder.Cancel();
            Assert.AreEqual("show", recorder.Last.Source, "the designer's clip is a show clip");
            designer.Close();
            Game.Services.Get<CrackerSpawner>().ClearAll();
        }

        private static Color Pixel(byte[] rgb, int w, int x, int y)
        {
            int i = (y * w + x) * 3;
            return new Color(rgb[i] / 255f, rgb[i + 1] / 255f, rgb[i + 2] / 255f);
        }

        private static string Run(string exe, string args)
        {
            using Process p = Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true });
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(10000);
            return output;
        }
    }
}

using System;
using System.IO;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UtsavNights.Core.Clips;
using UtsavNights.Core.Runtime;
using UtsavNights.Core.Services;

namespace UtsavNights.Gameplay
{
    /// <summary>A finished (or failed) clip.</summary>
    public readonly struct ClipResult
    {
        public ClipResult(bool ok, string path, float seconds, string source, string error)
        {
            Ok = ok;
            Path = path;
            Seconds = seconds;
            Source = source;
            Error = error;
        }

        public bool Ok { get; }

        public string Path { get; }

        public float Seconds { get; }

        /// <summary>"free" (the record button) or "show" (a show's clip).</summary>
        public string Source { get; }

        public string Error { get; }
    }

    /// <summary>
    /// Records 10 to 20 seconds of the screen and the game's sound to an MP4 (Phase 9 step 6, F14). Each frame the screen
    /// is copied once rendering ends, packed into I420 on the GPU (ClipYuv.shader) and read back asynchronously; the encoder (MediaCodec on the
    /// phone, ffmpeg in the editor) works off the main thread. Clips live in the cache's clips folder; the newest five stay.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class ClipRecorder : MonoBehaviour
    {
        public enum RecorderState
        {
            Idle,
            Recording,
            Finishing,
        }

        private static readonly int ClipSizeId = Shader.PropertyToID("_ClipSize");

        [SerializeField] private Shader yuvShader;

        private readonly float[] _audio = new float[48000 * ClipAudioTap.Channels];
        private byte[] _pcm = new byte[48000 * ClipAudioTap.Channels * 2];
        private Material _material;
        private RenderTexture _screen;
        private RenderTexture _yuv;
        private IClipEncoder _encoder;
        private ClipAudioTap _tap;
        private ClipFormat _format;
        private string _path;
        private string _source;
        private float _started;
        private float _maxSeconds;
        private float _nextFrame;
        private long _audioSamples;
        private int _sampleRate;
        private int _screenW;
        private int _screenH;
        private bool _registered;
        private int _lastFrame;
        private byte[] _firstFrame;

        /// <summary>
        /// Tests only: frames come from this texture (first row at the bottom, as Unity's textures are) instead of the
        /// screen. The editor in batch mode draws nothing to the screen, so this is how the GPU packing is tested there.
        /// </summary>
        public static Texture TestSource { get; set; }

        /// <summary>Makes the encoder (tests swap in their own).</summary>
        public static Func<IClipEncoder> EncoderFactory { get; set; } = DefaultEncoder;

        public event Action<RecorderState> StateChanged;

        public event Action<ClipResult> Finished;

        public RecorderState State { get; private set; } = RecorderState.Idle;

        public bool IsRecording => State == RecorderState.Recording;

        public float Elapsed => IsRecording ? Time.unscaledTime - _started : 0f;

        public float MaxSeconds => _maxSeconds;

        public bool CanStop => IsRecording && ClipRules.CanStop(Elapsed);

        public ClipFormat Format => _format;

        public ClipResult Last { get; private set; }

        /// <summary>A small picture of the last clip's first frame (the share sheet's preview), or null.</summary>
        public Texture2D Thumbnail { get; private set; }

        /// <summary>Frames requested from the GPU in the current or last recording.</summary>
        public int FramesCaptured { get; private set; }

        /// <summary>GPU readbacks that came back with an error (zero on a working phone).</summary>
        public int ReadbackErrors { get; private set; }

        public void Configure(Shader shader) => yuvShader = shader;

        public static IClipEncoder DefaultEncoder()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return new AndroidClipEncoder();
#else
            return new FfmpegClipEncoder();
#endif
        }

        /// <summary>The folder clips go to (on the phone: the cache path the share provider serves).</summary>
        public static string ClipsFolder()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var share = new AndroidJavaClass("com.ankitsharma.utsavnights.clips.ClipShare");
                return share.CallStatic<string>("clipsDir", activity);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Clips] no clips folder from Android: {e.Message}");
            }
#endif
            string dir = Path.Combine(Application.temporaryCachePath, ClipRules.Folder);
            Directory.CreateDirectory(dir);
            return dir;
        }

        private void Awake()
        {
            if (!Game.Services.IsRegistered<ClipRecorder>())
            {
                Game.Services.Register(this);
                _registered = true;
            }
        }

        private void OnDestroy()
        {
            if (IsRecording)
            {
                _encoder?.Cancel();
                _tap?.End();
            }

            RenderPipelineManager.endContextRendering -= OnRendered;
            Release();
            if (Thumbnail != null)
            {
                Destroy(Thumbnail);
            }

            if (_registered)
            {
                Game.Services.Unregister<ClipRecorder>();
            }
        }

        /// <summary>Starts a clip that stops by itself after <paramref name="maxSeconds"/> (10 to 20). False with a reason in <see cref="Last"/>.</summary>
        public bool StartRecording(float maxSeconds, string source = "free")
        {
            if (State != RecorderState.Idle)
            {
                return false;
            }

            if (yuvShader == null || !yuvShader.isSupported || !SystemInfo.supportsAsyncGPUReadback)
            {
                return Fail("Clips aren't supported on this phone.", source);
            }

            _screenW = Screen.width;
            _screenH = Screen.height;
            _format = ClipRules.FormatFor(_screenW, _screenH);
            _sampleRate = AudioSettings.outputSampleRate;
            _path = Path.Combine(ClipsFolder(), ClipRules.FileName(DateTime.Now));
            _encoder = EncoderFactory();
            if (!_encoder.Begin(_path, _format, _sampleRate, ClipAudioTap.Channels))
            {
                string why = _encoder.Error;
                _encoder = null;
                return Fail(string.IsNullOrEmpty(why) ? "The clip couldn't start." : why, source);
            }

            if (_material == null)
            {
                _material = new Material(yuvShader) { hideFlags = HideFlags.HideAndDontSave };
            }

            _screen = new RenderTexture(_screenW, _screenH, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear) { name = "ClipScreen" };
            _yuv = new RenderTexture(_format.Width, _format.Height * 3 / 2, 0, RenderTextureFormat.R8, RenderTextureReadWrite.Linear) { name = "ClipYuv", filterMode = FilterMode.Point };
            _screen.Create();
            _yuv.Create();

            AudioListener listener = FindFirstObjectByType<AudioListener>();
            _tap = null;
            if (listener != null)
            {
                _tap = listener.GetComponent<ClipAudioTap>();
                if (_tap == null)
                {
                    _tap = listener.gameObject.AddComponent<ClipAudioTap>();
                }
            }

            _tap?.Begin(_sampleRate);
            _audioSamples = 0;
            _maxSeconds = Mathf.Clamp(maxSeconds, ClipRules.MinSeconds, ClipRules.MaxSeconds);
            _source = source;
            _started = Time.unscaledTime;
            _nextFrame = 0f;
            _firstFrame = null;
            FramesCaptured = 0;
            ReadbackErrors = 0;
            _lastFrame = -1;
            SetState(RecorderState.Recording);
            RenderPipelineManager.endContextRendering += OnRendered;
            return true;
        }

        /// <summary>Ends the clip. Refused before 10 seconds unless <paramref name="force"/> (tests, a show's clip).</summary>
        public bool Stop(bool force = false)
        {
            if (!IsRecording || (!force && !CanStop))
            {
                return false;
            }

            float seconds = Elapsed;
            _tap?.End();
            PumpAudio();
            _encoder.Finish();
            Last = new ClipResult(false, _path, seconds, _source, string.Empty);
            SetState(RecorderState.Finishing);
            return true;
        }

        /// <summary>Throws the recording away.</summary>
        public void Cancel()
        {
            if (State == RecorderState.Idle)
            {
                return;
            }

            _tap?.End();
            _encoder?.Cancel();
            _encoder = null;
            Release();
            Last = new ClipResult(false, null, 0f, _source, "cancelled");
            SetState(RecorderState.Idle);
        }

        /// <summary>Deletes a clip file (the share sheet's Delete).</summary>
        public static void DeleteClip(string path)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private void Update()
        {
            if (IsRecording)
            {
                PumpAudio();
                if (Screen.width != _screenW || Screen.height != _screenH)
                {
                    // The phone turned: the clip ends here rather than changing shape.
                    Stop(force: true);
                }
                else if (Elapsed >= _maxSeconds)
                {
                    Stop(force: true);
                }
            }
            else if (State == RecorderState.Finishing && _encoder.IsFinished)
            {
                string error = _encoder.Error;
                bool ok = string.IsNullOrEmpty(error) && File.Exists(_path);
                Last = new ClipResult(ok, ok ? _path : null, Last.Seconds, _source, ok ? string.Empty : (string.IsNullOrEmpty(error) ? "the clip wasn't written" : error));
                _encoder = null;
                MakeThumbnail();
                Release();
                Tidy();
                SetState(RecorderState.Idle);
                Finished?.Invoke(Last);
            }
        }

        /// <summary>
        /// After the render pipeline has drawn the whole frame (cameras and the overlay UI). WaitForEndOfFrame would do on
        /// the phone, but it never resumes in batch mode, where the tests run; this one path serves both.
        /// </summary>
        private void OnRendered(ScriptableRenderContext context, System.Collections.Generic.List<Camera> cameras)
        {
            if (TestSource == null)
            {
                CaptureFrame(null);
            }
        }

        private void LateUpdate()
        {
            if (TestSource != null)
            {
                CaptureFrame(TestSource);
            }
        }

        private void CaptureFrame(Texture source)
        {
            float t = Time.unscaledTime - _started;
            if (!IsRecording || Time.frameCount == _lastFrame || t + 0.004f < _nextFrame)
            {
                return;
            }

            _lastFrame = Time.frameCount;
            _nextFrame = Mathf.Max(_nextFrame + 1f / _format.Fps, t);
            if (source == null)
            {
                ScreenCapture.CaptureScreenshotIntoRenderTexture(_screen);
            }

            _material.SetVector(ClipSizeId, new Vector4(_format.Width, _format.Height, source == null && SystemInfo.graphicsUVStartsAtTop ? 1f : 0f, 0f));
            Graphics.Blit(source != null ? source : _screen, _yuv, _material);
            long pts = (long)(t * 1_000_000.0);
            IClipEncoder encoder = _encoder;
            AsyncGPUReadback.Request(_yuv, 0, request => OnFrame(request, encoder, pts));
            FramesCaptured++;
        }

        private void OnFrame(AsyncGPUReadbackRequest request, IClipEncoder encoder, long pts)
        {
            if (request.hasError)
            {
                ReadbackErrors++;
                return;
            }

            if (encoder == null || encoder != _encoder || State == RecorderState.Idle)
            {
                return;
            }

            NativeArray<byte> data = request.GetData<byte>();
            if (_firstFrame == null)
            {
                _firstFrame = data.ToArray();
            }

            if (State == RecorderState.Recording)
            {
                encoder.AddVideo(data, pts);
            }
        }

        private void PumpAudio()
        {
            if (_tap == null || _encoder == null)
            {
                return;
            }

            int n;
            while ((n = _tap.Drain(_audio)) > 0)
            {
                int bytes = ClipRules.ToPcm16(_audio, n, _pcm);
                _encoder.AddAudio(_pcm, bytes, ClipRules.AudioMicros(_audioSamples, _sampleRate, ClipAudioTap.Channels));
                _audioSamples += n;
            }
        }

        private void MakeThumbnail()
        {
            if (_firstFrame == null || !Last.Ok)
            {
                return;
            }

            // The frame is top row first (as the encoder wants); a texture's first row is its bottom, so flip it.
            byte[] top = I420.Thumbnail(_firstFrame, _format.Width, _format.Height, 4, out int tw, out int th);
            var rgb = new byte[top.Length];
            for (int y = 0; y < th; y++)
            {
                Buffer.BlockCopy(top, y * tw * 3, rgb, (th - 1 - y) * tw * 3, tw * 3);
            }

            if (Thumbnail == null || Thumbnail.width != tw || Thumbnail.height != th)
            {
                if (Thumbnail != null)
                {
                    Destroy(Thumbnail);
                }

                Thumbnail = new Texture2D(tw, th, TextureFormat.RGB24, false) { name = "ClipThumbnail" };
            }

            Thumbnail.SetPixelData(rgb, 0);
            Thumbnail.Apply(false);
        }

        /// <summary>The first frame as recorded (I420, top row first), for tests.</summary>
        public byte[] FirstFrame => _firstFrame;

        private void Tidy()
        {
            try
            {
                string dir = Path.GetDirectoryName(_path);
                var names = new System.Collections.Generic.List<string>();
                foreach (string f in Directory.GetFiles(dir))
                {
                    names.Add(Path.GetFileName(f));
                }

                foreach (string old in ClipRules.ToDelete(names))
                {
                    File.Delete(Path.Combine(dir, old));
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Clips] could not tidy old clips: {e.Message}");
            }
        }

        private bool Fail(string why, string source)
        {
            Last = new ClipResult(false, null, 0f, source, why);
            Finished?.Invoke(Last);
            return false;
        }

        private void Release()
        {
            if (_screen != null)
            {
                _screen.Release();
                Destroy(_screen);
                _screen = null;
            }

            if (_yuv != null)
            {
                _yuv.Release();
                Destroy(_yuv);
                _yuv = null;
            }
        }

        private void SetState(RecorderState state)
        {
            if (state != RecorderState.Recording)
            {
                RenderPipelineManager.endContextRendering -= OnRendered;
            }

            State = state;
            StateChanged?.Invoke(state);
        }
    }
}

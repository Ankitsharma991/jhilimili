using System;
using System.Diagnostics;
using System.IO;
using Unity.Collections;
using UnityEngine;
using UtsavNights.Core.Clips;
using Debug = UnityEngine.Debug;

namespace UtsavNights.Gameplay
{
    /// <summary>Turns I420 frames and 16-bit PCM into an MP4 (Phase 9 step 6). Frames and audio arrive on the main thread.</summary>
    public interface IClipEncoder
    {
        bool IsFinished { get; }

        /// <summary>Empty while all is well.</summary>
        string Error { get; }

        int FramesWritten { get; }

        bool Begin(string path, ClipFormat format, int sampleRate, int channels);

        /// <summary>One frame; the data is only valid during the call. False when the frame was dropped.</summary>
        bool AddVideo(NativeArray<byte> frame, long ptsUs);

        void AddAudio(byte[] pcm, int length, long ptsUs);

        void Finish();

        void Cancel();
    }

    /// <summary>The phone's encoder: MediaCodec and MediaMuxer in the UtsavClips Android library (ClipEncoder.java).</summary>
    public sealed class AndroidClipEncoder : IClipEncoder
    {
        private AndroidJavaObject _encoder;
        private IntPtr _addVideo;
        private string _beginError = string.Empty;

        public bool IsFinished => _encoder == null || _encoder.Call<bool>("isFinished");

        public string Error => _encoder != null ? _encoder.Call<string>("error") : _beginError;

        public int FramesWritten => _encoder != null ? _encoder.Call<int>("framesWritten") : 0;

        public bool Begin(string path, ClipFormat format, int sampleRate, int channels)
        {
            try
            {
                _encoder = new AndroidJavaObject("com.ankitsharma.utsavnights.clips.ClipEncoder", path, format.Width, format.Height, format.Fps, format.Bitrate, sampleRate, channels);
                _addVideo = AndroidJNIHelper.GetMethodID(_encoder.GetRawClass(), "addVideo", "(Ljava/nio/ByteBuffer;J)Z");
                string error = _encoder.Call<string>("error");
                return string.IsNullOrEmpty(error);
            }
            catch (Exception e)
            {
                _beginError = "the phone's video encoder isn't available: " + e.Message;
                _encoder = null;
                return false;
            }
        }

        public bool AddVideo(NativeArray<byte> frame, long ptsUs)
        {
            if (_encoder == null)
            {
                return false;
            }

            // A direct buffer over the readback: Java copies it before the call returns, with no managed copy.
            IntPtr buffer = AndroidJNI.NewDirectByteBuffer(frame);
            try
            {
                var args = new jvalue[2];
                args[0].l = buffer;
                args[1].j = ptsUs;
                return AndroidJNI.CallBooleanMethod(_encoder.GetRawObject(), _addVideo, args);
            }
            finally
            {
                AndroidJNI.DeleteLocalRef(buffer);
            }
        }

        public void AddAudio(byte[] pcm, int length, long ptsUs) => _encoder?.Call("addAudio", pcm, length, ptsUs);

        public void Finish() => _encoder?.Call("finish");

        public void Cancel() => _encoder?.Call("cancel");
    }

    /// <summary>
    /// The editor's encoder: pipes frames to ffmpeg (when it is on the PATH) and writes the sound beside them as a WAV,
    /// then muxes both into the MP4. Without ffmpeg it fails with a clear message; the game never needs it on a phone.
    /// </summary>
    public sealed class FfmpegClipEncoder : IClipEncoder
    {
        private Process _video;
        private Process _mux;
        private FileStream _wav;
        private string _path;
        private string _videoPath;
        private string _wavPath;
        private int _sampleRate;
        private int _channels;
        private long _audioBytes;
        private byte[] _frame;
        private string _error = string.Empty;
        private bool _finishing;
        private bool _done;

        public bool IsFinished
        {
            get
            {
                if (!_finishing)
                {
                    return false;
                }

                if (_done || (_video != null && !_video.HasExited))
                {
                    return _done;
                }

                if (_mux == null)
                {
                    if (string.IsNullOrEmpty(_error) && _video != null && _video.ExitCode != 0)
                    {
                        _error = "ffmpeg could not encode the frames";
                    }

                    if (!string.IsNullOrEmpty(_error))
                    {
                        return Done();
                    }

                    string args = $"-y -loglevel error -i \"{_videoPath}\" -i \"{_wavPath}\" -c:v copy -c:a aac -b:a 128k -shortest \"{_path}\"";
                    _mux = Process.Start(new ProcessStartInfo("ffmpeg", args) { UseShellExecute = false, CreateNoWindow = true });
                    return false;
                }

                if (!_mux.HasExited)
                {
                    return false;
                }

                if (_mux.ExitCode != 0 && string.IsNullOrEmpty(_error))
                {
                    _error = "ffmpeg could not write the clip";
                }

                return Done();
            }
        }

        public string Error => _error;

        public int FramesWritten { get; private set; }

        public static bool Available()
        {
            try
            {
                using Process p = Process.Start(new ProcessStartInfo("ffmpeg", "-version") { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true });
                p.WaitForExit(5000);
                return p.ExitCode == 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool Begin(string path, ClipFormat format, int sampleRate, int channels)
        {
            _path = path;
            _videoPath = path + ".video.mp4";
            _wavPath = path + ".wav";
            _sampleRate = sampleRate;
            _channels = channels;
            _frame = new byte[format.FrameBytes];
            try
            {
                string args = $"-y -loglevel error -f rawvideo -pix_fmt yuv420p -s {format.Width}x{format.Height} -framerate {format.Fps} -i - " +
                              $"-c:v libx264 -preset veryfast -pix_fmt yuv420p \"{_videoPath}\"";
                _video = Process.Start(new ProcessStartInfo("ffmpeg", args) { UseShellExecute = false, RedirectStandardInput = true, CreateNoWindow = true });
                _wav = new FileStream(_wavPath, FileMode.Create, FileAccess.Write);
                _wav.Write(new byte[44], 0, 44);
                return true;
            }
            catch (Exception e)
            {
                _error = "the editor needs ffmpeg on the PATH to make clips: " + e.Message;
                return false;
            }
        }

        public bool AddVideo(NativeArray<byte> frame, long ptsUs)
        {
            if (_video == null || _finishing || frame.Length < _frame.Length)
            {
                return false;
            }

            NativeArray<byte>.Copy(frame, _frame, _frame.Length);
            try
            {
                _video.StandardInput.BaseStream.Write(_frame, 0, _frame.Length);
                FramesWritten++;
                return true;
            }
            catch (IOException e)
            {
                _error = "ffmpeg stopped: " + e.Message;
                return false;
            }
        }

        public void AddAudio(byte[] pcm, int length, long ptsUs)
        {
            if (_wav == null || _finishing)
            {
                return;
            }

            _wav.Write(pcm, 0, length);
            _audioBytes += length;
        }

        public void Finish()
        {
            if (_finishing)
            {
                return;
            }

            _finishing = true;
            try
            {
                _video?.StandardInput.Close();
            }
            catch (IOException)
            {
                // Already closed.
            }

            CloseWav();
            if (FramesWritten == 0 && string.IsNullOrEmpty(_error))
            {
                _error = "no frames were recorded";
            }
        }

        public void Cancel()
        {
            _error = "cancelled";
            Finish();
            try
            {
                if (_video != null && !_video.HasExited)
                {
                    _video.Kill();
                }
            }
            catch (InvalidOperationException)
            {
                // Already exited.
            }

            Done();
        }

        private bool Done()
        {
            Cleanup();
            _done = true;
            return true;
        }

        private void CloseWav()
        {
            if (_wav == null)
            {
                return;
            }

            // The RIFF header, now that the length is known.
            _wav.Position = 0;
            using (var w = new BinaryWriter(_wav, System.Text.Encoding.ASCII, leaveOpen: true))
            {
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                w.Write((int)(36 + _audioBytes));
                w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                w.Write(16);
                w.Write((short)1);
                w.Write((short)_channels);
                w.Write(_sampleRate);
                w.Write(_sampleRate * _channels * 2);
                w.Write((short)(_channels * 2));
                w.Write((short)16);
                w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                w.Write((int)_audioBytes);
            }

            _wav.Dispose();
            _wav = null;
        }

        private void Cleanup()
        {
            foreach (string p in new[] { _videoPath, _wavPath })
            {
                try
                {
                    if (p != null && File.Exists(p))
                    {
                        File.Delete(p);
                    }
                }
                catch (IOException e)
                {
                    Debug.LogWarning($"[Clips] could not delete {p}: {e.Message}");
                }
            }

            if (!string.IsNullOrEmpty(_error) && _path != null && File.Exists(_path))
            {
                File.Delete(_path);
            }
        }
    }
}

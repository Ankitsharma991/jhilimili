using System;
using System.Collections.Generic;

namespace UtsavNights.Core.Assets
{
    /// <summary>How a sound is used, from its folder and name (Docs/Phase5/ASSETS.md).</summary>
    public enum AudioRole
    {
        /// <summary>Bursts, pops, clicks: short, played the instant something happens.</summary>
        OneShot,

        /// <summary>Name ends in "_loop": fuse hiss, fountain roar, chakri whirr.</summary>
        Loop,

        /// <summary>In an Ambience or Music folder, or named amb_ or music_: long beds, streamed.</summary>
        Long,
    }

    /// <summary>Unity's AudioClipLoadType, without the engine.</summary>
    public enum AudioLoad
    {
        DecompressOnLoad,
        CompressedInMemory,
        Streaming,
    }

    /// <summary>The import settings a sound must have for its role.</summary>
    public readonly struct AudioSpec
    {
        public AudioSpec(AudioLoad load, float vorbisQuality, bool forceMono, bool preload, bool loadInBackground, float maxSeconds)
        {
            Load = load;
            VorbisQuality = vorbisQuality;
            ForceMono = forceMono;
            Preload = preload;
            LoadInBackground = loadInBackground;
            MaxSeconds = maxSeconds;
        }

        public AudioLoad Load { get; }

        public float VorbisQuality { get; }

        public bool ForceMono { get; }

        public bool Preload { get; }

        public bool LoadInBackground { get; }

        public float MaxSeconds { get; }
    }

    /// <summary>What the editor reads from an imported sound.</summary>
    public sealed class AudioFacts
    {
        public string Path = string.Empty;
        public AudioLoad Load;
        public bool Vorbis;
        public float Quality;
        public bool ForceMono;
        public bool Preload;
        public bool LoadInBackground;
        public float Seconds;
        public int Frequency;
        public int Channels;

        /// <summary>Seconds before the first sample above about -40 dBFS; -1 when unknown.</summary>
        public float LeadingSilence = -1f;

        /// <summary>Largest absolute sample, 0 to 1; -1 when unknown.</summary>
        public float Peak = -1f;
    }

    /// <summary>What the editor reads from an imported texture.</summary>
    public sealed class TextureFacts
    {
        public string Path = string.Empty;
        public int MaxSize;
        public bool Compressed;
        public bool Mipmaps;
        public bool IsSprite;
    }

    public readonly struct AssetIssue
    {
        public AssetIssue(string path, bool error, string message)
        {
            Path = path;
            Error = error;
            Message = message;
        }

        public string Path { get; }

        public bool Error { get; }

        public string Message { get; }

        public override string ToString() => $"{(Error ? "ERROR" : "warning")} {Path}: {Message}";
    }

    /// <summary>
    /// Import rules for game audio and art (Phase 5 step 2, PRD 19 "texture sizes, audio bitrate"). Sound
    /// settings come from the role: one-shots and loops are decompressed on load, because they must start
    /// on the frame they are triggered (tap-to-sound latency, PRD 9); long beds stream. Vorbis quality
    /// 0.6 is about 130 kbps for mono at 48 kHz [Hypothesis: confirm by ear on the tablet speaker].
    /// Breaking a rule is an error, which fails the build through the content lint. Audio quirks a
    /// person must judge (leading silence, clipping, high sample rates) are warnings.
    /// </summary>
    public static class ImportRules
    {
        public const string AudioRoot = "Assets/_Game/Audio/";
        public const string ArtRoot = "Assets/_Game/Art/";
        public const string IconFolder = "Assets/_Game/Art/Icons/";

        /// <summary>Spike leftovers are archived and not shipped content.</summary>
        public const string SpikeArt = "Assets/_Game/Art/Spike/";

        public const int MaxIconSize = 256;
        public const int MaxArtSize = 1024;

        /// <summary>Decompressed one-shots and loops stay in RAM; this keeps them well inside the 700 MB low tier.</summary>
        public const float DecompressedBudgetMb = 32f;

        /// <summary>The mixer runs at 48 kHz on the lab devices; higher rates only cost memory.</summary>
        public const int MaxUsefulFrequency = 48000;

        public const float MaxLeadingSilence = 0.010f;
        public const float ClippingPeak = 0.999f;

        public static bool IsGameAudio(string path) => path.StartsWith(AudioRoot, StringComparison.Ordinal);

        public static bool IsGameArt(string path) =>
            path.StartsWith(ArtRoot, StringComparison.Ordinal) && !path.StartsWith(SpikeArt, StringComparison.Ordinal);

        public static AudioRole RoleOf(string path)
        {
            string name = FileName(path).ToLowerInvariant();
            if (path.Contains("/Ambience/") || path.Contains("/Music/") || name.StartsWith("amb_", StringComparison.Ordinal) ||
                name.StartsWith("music_", StringComparison.Ordinal))
            {
                return AudioRole.Long;
            }

            return name.EndsWith("_loop", StringComparison.Ordinal) ? AudioRole.Loop : AudioRole.OneShot;
        }

        public static AudioSpec SpecFor(AudioRole role)
        {
            switch (role)
            {
                case AudioRole.Loop:
                    return new AudioSpec(AudioLoad.DecompressOnLoad, 0.6f, forceMono: true, preload: true, loadInBackground: false, maxSeconds: 10f);
                case AudioRole.Long:
                    return new AudioSpec(AudioLoad.Streaming, 0.45f, forceMono: false, preload: false, loadInBackground: true, maxSeconds: 600f);
                default:
                    return new AudioSpec(AudioLoad.DecompressOnLoad, 0.6f, forceMono: true, preload: true, loadInBackground: false, maxSeconds: 4f);
            }
        }

        /// <summary>RAM a clip takes once loaded: 16-bit PCM when decompressed, nothing worth counting otherwise.</summary>
        public static float DecompressedMb(AudioFacts clip) =>
            clip.Load == AudioLoad.DecompressOnLoad ? clip.Seconds * clip.Frequency * Math.Max(1, clip.Channels) * 2f / (1024f * 1024f) : 0f;

        public static List<AssetIssue> CheckAudio(IReadOnlyList<AudioFacts> clips)
        {
            var issues = new List<AssetIssue>();
            float total = 0f;
            foreach (AudioFacts clip in clips)
            {
                AudioRole role = RoleOf(clip.Path);
                AudioSpec spec = SpecFor(role);
                string roleName = role.ToString();
                if (clip.Load != spec.Load)
                {
                    issues.Add(new AssetIssue(clip.Path, true, $"{roleName} sounds load as {spec.Load}, this one as {clip.Load}"));
                }

                if (!clip.Vorbis)
                {
                    issues.Add(new AssetIssue(clip.Path, true, "compression must be Vorbis"));
                }
                else if (Math.Abs(clip.Quality - spec.VorbisQuality) > 0.051f)
                {
                    issues.Add(new AssetIssue(clip.Path, true, $"Vorbis quality {clip.Quality:0.00}, rule {spec.VorbisQuality:0.00}"));
                }

                if (spec.ForceMono && !clip.ForceMono)
                {
                    issues.Add(new AssetIssue(clip.Path, true, "Force To Mono must be on (the phone speaker is mono)"));
                }

                if (clip.Preload != spec.Preload || clip.LoadInBackground != spec.LoadInBackground)
                {
                    issues.Add(new AssetIssue(clip.Path, true,
                        $"preload {(spec.Preload ? "on" : "off")} and load in background {(spec.LoadInBackground ? "on" : "off")} for {roleName} sounds"));
                }

                if (clip.Seconds > spec.MaxSeconds)
                {
                    issues.Add(new AssetIssue(clip.Path, true,
                        $"{clip.Seconds:0.0} s is over {spec.MaxSeconds:0} s for a {roleName} sound; trim it or move it to Audio/Ambience"));
                }

                if (clip.Frequency > MaxUsefulFrequency)
                {
                    issues.Add(new AssetIssue(clip.Path, false, $"{clip.Frequency} Hz; the import rule resamples to 48 kHz only for WAV sources"));
                }

                if (role != AudioRole.Long && clip.LeadingSilence > MaxLeadingSilence)
                {
                    issues.Add(new AssetIssue(clip.Path, false,
                        $"{clip.LeadingSilence * 1000f:0} ms of silence at the start delays the sound after a tap; trim it"));
                }

                if (clip.Peak >= ClippingPeak)
                {
                    issues.Add(new AssetIssue(clip.Path, false, "peaks at full scale, so it may clip; master it to about -1 dBFS"));
                }

                total += DecompressedMb(clip);
            }

            if (total > DecompressedBudgetMb)
            {
                issues.Add(new AssetIssue(AudioRoot, true, $"decompressed sounds take {total:0.0} MB of RAM, budget {DecompressedBudgetMb:0} MB"));
            }

            return issues;
        }

        public static List<AssetIssue> CheckTextures(IReadOnlyList<TextureFacts> textures)
        {
            var issues = new List<AssetIssue>();
            foreach (TextureFacts tex in textures)
            {
                bool icon = tex.Path.StartsWith(IconFolder, StringComparison.Ordinal);
                int max = icon ? MaxIconSize : MaxArtSize;
                if (tex.MaxSize > max)
                {
                    issues.Add(new AssetIssue(tex.Path, true, $"max size {tex.MaxSize}, rule {max}"));
                }

                if (!tex.Compressed)
                {
                    issues.Add(new AssetIssue(tex.Path, true, "must be compressed (ASTC on Android)"));
                }

                if (icon && (!tex.IsSprite || tex.Mipmaps))
                {
                    issues.Add(new AssetIssue(tex.Path, true, "tray icons are sprites without mipmaps"));
                }
            }

            return issues;
        }

        /// <summary>Seconds before the first sample louder than about -40 dBFS, from interleaved samples.</summary>
        public static float LeadingSilence(float[] samples, int channels, int frequency, float threshold = 0.01f)
        {
            if (samples == null || samples.Length == 0 || frequency <= 0)
            {
                return -1f;
            }

            int ch = Math.Max(1, channels);
            for (int i = 0; i < samples.Length; i++)
            {
                if (Math.Abs(samples[i]) > threshold)
                {
                    return (float)(i / ch) / frequency;
                }
            }

            return (float)(samples.Length / ch) / frequency;
        }

        public static float Peak(float[] samples)
        {
            if (samples == null || samples.Length == 0)
            {
                return -1f;
            }

            float peak = 0f;
            foreach (float s in samples)
            {
                peak = Math.Max(peak, Math.Abs(s));
            }

            return peak;
        }

        /// <summary>
        /// Sample rate from the start of a RIFF/WAVE file, or 0 when it isn't one or the "fmt " chunk is past
        /// the bytes given. Walks the chunks, because recordings from sound libraries often put bext or
        /// JUNK chunks before "fmt ".
        /// </summary>
        public static int WavSampleRate(byte[] header)
        {
            if (header == null || header.Length < 12 || !Tag(header, 0, "RIFF") || !Tag(header, 8, "WAVE"))
            {
                return 0;
            }

            int offset = 12;
            while (offset + 8 <= header.Length)
            {
                int size = ReadInt(header, offset + 4);
                if (Tag(header, offset, "fmt "))
                {
                    return offset + 12 <= header.Length ? ReadInt(header, offset + 12) : 0;
                }

                if (size < 0 || size > header.Length)
                {
                    return 0;
                }

                offset += 8 + size + (size & 1);
            }

            return 0;
        }

        private static bool Tag(byte[] b, int at, string tag) =>
            b[at] == tag[0] && b[at + 1] == tag[1] && b[at + 2] == tag[2] && b[at + 3] == tag[3];

        private static int ReadInt(byte[] b, int at) =>
            at + 4 <= b.Length ? b[at] | (b[at + 1] << 8) | (b[at + 2] << 16) | (b[at + 3] << 24) : 0;

        private static string FileName(string path)
        {
            int slash = path.LastIndexOf('/');
            string name = slash >= 0 ? path.Substring(slash + 1) : path;
            int dot = name.LastIndexOf('.');
            return dot > 0 ? name.Substring(0, dot) : name;
        }
    }
}

using System.Linq;
using NUnit.Framework;
using UtsavNights.Core.Assets;

namespace UtsavNights.Tests
{
    /// <summary>Phase 5 step 2: licensed audio and art arrive at the right size and settings.</summary>
    public class ImportRulesTests
    {
        private static AudioFacts Good(string path)
        {
            AudioSpec spec = ImportRules.SpecFor(ImportRules.RoleOf(path));
            return new AudioFacts
            {
                Path = path,
                Load = spec.Load,
                Vorbis = true,
                Quality = spec.VorbisQuality,
                ForceMono = spec.ForceMono,
                Preload = spec.Preload,
                LoadInBackground = spec.LoadInBackground,
                Seconds = 1f,
                Frequency = 48000,
                Channels = 1,
                LeadingSilence = 0f,
                Peak = 0.8f,
            };
        }

        [TestCase("Assets/_Game/Audio/Placeholder/bomb_boom.wav", AudioRole.OneShot)]
        [TestCase("Assets/_Game/Audio/Placeholder/fuse_hiss_loop.wav", AudioRole.Loop)]
        [TestCase("Assets/_Game/Audio/Licensed/Fountain_Roar_LOOP.wav", AudioRole.Loop)]
        [TestCase("Assets/_Game/Audio/Ambience/street_night.ogg", AudioRole.Long)]
        [TestCase("Assets/_Game/Audio/Licensed/amb_crickets.wav", AudioRole.Long)]
        [TestCase("Assets/_Game/Audio/Music/music_title.ogg", AudioRole.Long)]
        public void Role_ComesFromFolderAndName(string path, AudioRole role)
        {
            Assert.AreEqual(role, ImportRules.RoleOf(path));
        }

        [Test]
        public void ShortSounds_DecompressSoTheyStartOnTheTapFrame_LongOnesStream()
        {
            Assert.AreEqual(AudioLoad.DecompressOnLoad, ImportRules.SpecFor(AudioRole.OneShot).Load);
            Assert.AreEqual(AudioLoad.DecompressOnLoad, ImportRules.SpecFor(AudioRole.Loop).Load);
            Assert.AreEqual(AudioLoad.Streaming, ImportRules.SpecFor(AudioRole.Long).Load);
            Assert.IsTrue(ImportRules.SpecFor(AudioRole.OneShot).ForceMono);
            Assert.IsFalse(ImportRules.SpecFor(AudioRole.Long).ForceMono, "ambience may stay stereo for headphones");
        }

        [Test]
        public void ClipsOnTheirRule_HaveNoIssues()
        {
            var clips = new[] { Good("Assets/_Game/Audio/a/boom.wav"), Good("Assets/_Game/Audio/a/hiss_loop.wav"), Good("Assets/_Game/Audio/Ambience/street.ogg") };
            CollectionAssert.IsEmpty(ImportRules.CheckAudio(clips));
        }

        [Test]
        public void WrongSettings_AreErrors()
        {
            AudioFacts clip = Good("Assets/_Game/Audio/a/boom.wav");
            clip.Load = AudioLoad.Streaming;
            clip.Quality = 1f;
            clip.ForceMono = false;
            var issues = ImportRules.CheckAudio(new[] { clip });
            Assert.AreEqual(3, issues.Count(i => i.Error));
        }

        [Test]
        public void TooLongForItsRole_IsAnError()
        {
            AudioFacts clip = Good("Assets/_Game/Audio/a/boom.wav");
            clip.Seconds = 12f;
            StringAssert.Contains("Ambience", ImportRules.CheckAudio(new[] { clip }).Single(i => i.Error).Message);
        }

        [Test]
        public void SilenceClippingAndHighRates_AreWarningsForAPerson()
        {
            AudioFacts clip = Good("Assets/_Game/Audio/a/boom.wav");
            clip.LeadingSilence = 0.050f;
            clip.Peak = 1f;
            clip.Frequency = 96000;
            var issues = ImportRules.CheckAudio(new[] { clip });
            Assert.AreEqual(3, issues.Count);
            Assert.IsTrue(issues.All(i => !i.Error));
            Assert.IsTrue(issues.Any(i => i.Message.Contains("50 ms")));
        }

        [Test]
        public void DecompressedRam_HasABudget()
        {
            // 40 one-shots of 4 s at 48 kHz mono: 40 x 375 KB, about 14.6 MB, inside the budget.
            var clips = Enumerable.Range(0, 40).Select(i => { AudioFacts c = Good($"Assets/_Game/Audio/a/s{i}.wav"); c.Seconds = 4f; return c; }).ToArray();
            Assert.IsFalse(ImportRules.CheckAudio(clips).Any(i => i.Error));
            foreach (AudioFacts c in clips)
            {
                c.Channels = 2;
                c.Frequency = 96000;
            }

            Assert.IsTrue(ImportRules.CheckAudio(clips).Any(i => i.Error && i.Message.Contains("RAM")));
        }

        [Test]
        public void LeadingSilenceAndPeak_AreMeasuredFromSamples()
        {
            var samples = new float[48000];
            samples[960] = 0.5f;
            samples[961] = -0.9f;
            Assert.AreEqual(0.020f, ImportRules.LeadingSilence(samples, 1, 48000), 0.0001f);
            Assert.AreEqual(0.010f, ImportRules.LeadingSilence(samples, 2, 48000), 0.0001f, "interleaved stereo frames");
            Assert.AreEqual(0.9f, ImportRules.Peak(samples), 0.0001f);
            Assert.AreEqual(-1f, ImportRules.LeadingSilence(null, 1, 48000));
        }

        [Test]
        public void WavSampleRate_ReadsTheFmtChunkEvenAfterOtherChunks()
        {
            byte[] plain = Wav(96000, junkBytes: 0);
            Assert.AreEqual(96000, ImportRules.WavSampleRate(plain));
            byte[] broadcast = Wav(44100, junkBytes: 603);
            Assert.AreEqual(44100, ImportRules.WavSampleRate(broadcast), "odd-sized chunks are padded to even");
            Assert.AreEqual(0, ImportRules.WavSampleRate(new byte[] { 1, 2, 3 }));
            Assert.AreEqual(0, ImportRules.WavSampleRate(System.Text.Encoding.ASCII.GetBytes("OggS................................")));
        }

        [Test]
        public void Textures_IconsAreSmallCompressedSprites()
        {
            var good = new TextureFacts { Path = "Assets/_Game/Art/Icons/Icon_a.png", MaxSize = 256, Compressed = true, IsSprite = true };
            var big = new TextureFacts { Path = "Assets/_Game/Art/Icons/Icon_b.png", MaxSize = 2048, Compressed = false, IsSprite = true, Mipmaps = true };
            var art = new TextureFacts { Path = "Assets/_Game/Art/Ground/stone.png", MaxSize = 1024, Compressed = true };
            CollectionAssert.IsEmpty(ImportRules.CheckTextures(new[] { good, art }));
            Assert.AreEqual(3, ImportRules.CheckTextures(new[] { big }).Count);
            Assert.IsFalse(ImportRules.IsGameArt("Assets/_Game/Art/Spike/glow_soft.png"), "archived spike art is not checked");
        }

        private static byte[] Wav(int rate, int junkBytes)
        {
            var bytes = new System.Collections.Generic.List<byte>();
            void Tag(string s) => bytes.AddRange(System.Text.Encoding.ASCII.GetBytes(s));
            void Int(int v) => bytes.AddRange(System.BitConverter.GetBytes(v));
            Tag("RIFF");
            Int(0);
            Tag("WAVE");
            if (junkBytes > 0)
            {
                Tag("bext");
                Int(junkBytes);
                bytes.AddRange(new byte[junkBytes + (junkBytes & 1)]);
            }

            Tag("fmt ");
            Int(16);
            bytes.AddRange(new byte[] { 1, 0, 1, 0 });
            Int(rate);
            Int(rate * 2);
            bytes.AddRange(new byte[] { 2, 0, 16, 0 });
            return bytes.ToArray();
        }
    }
}

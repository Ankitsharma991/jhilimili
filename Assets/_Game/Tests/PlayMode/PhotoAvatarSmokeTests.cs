using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Core.Art;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;
using UtsavNights.UI;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>Phase 11 step 3 (F26): the photo-assisted look in the creator: 18+ only, applied as a suggestion, the pixels cleared.</summary>
    public class PhotoAvatarSmokeTests
    {
        private const int W = 240, H = 300;

        [SetUp]
        public void SetUp() => PlayerPrefs.DeleteKey(PhotoAvatar.AgeKey);

        [TearDown]
        public void TearDown()
        {
            PhotoAvatar.EditorPhoto = null;
            PlayerPrefs.DeleteKey(PhotoAvatar.AgeKey);
        }

        /// <summary>A round face in a given skin tone with black hair, eyes 30 pixels apart.</summary>
        private static byte[] Photo(float tone)
        {
            var rgb = new byte[W * H * 3];
            Rgb skin = SkinTones.Evaluate(tone);
            const float e = 30f, mx = 120f, my = 130f;
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    float dy = (y - my) / e - 0.4f;
                    float half = 1.25f * (float)Math.Sqrt(Math.Max(0, 1 - dy * dy / (1.35f * 1.35f))) * e;
                    Rgb c = new Rgb(0.2f, 0.35f, 0.75f);
                    if (Math.Abs(x - mx) <= half && Math.Abs(dy) <= 1.35f)
                    {
                        c = skin;
                    }

                    float top = (y - my) / e;
                    if (top >= -1.75f && top < -0.9f && Math.Abs(x - mx) <= 1.2f * e)
                    {
                        c = HairColours.All[0];
                    }

                    int i = (y * W + x) * 3;
                    rgb[i] = (byte)(c.R * 255f);
                    rgb[i + 1] = (byte)(c.G * 255f);
                    rgb[i + 2] = (byte)(c.B * 255f);
                }
            }

            return rgb;
        }

        [UnityTest]
        public IEnumerator Adult_ChoosesAPhoto_GetsASuggestion_PhotoCleared()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            var creator = UnityEngine.Object.FindFirstObjectByType<AvatarCreator>();
            creator.Open();
            creator.ShowTab(0);
            Assert.IsTrue(creator.PhotoOffered);

            creator.OpenPhotoSheet();
            Assert.IsTrue(creator.PhotoSheetOpen);
            StringAssert.Contains("18", creator.PhotoMessage, "asks the age first");
            creator.AnswerAge(true);
            StringAssert.Contains("deletes it", creator.PhotoMessage, "says what happens to the photo");

            byte[] photo = Photo(0.8f);
            PhotoAvatar.EditorPhoto = () => (photo, W, H, new FaceBox(120f, 130f, 30f));
            creator.UsePhoto(false);
            Assert.AreEqual(0.8f, creator.Draft.skinTone, 0.07f);
            Assert.AreEqual(0, creator.Draft.hairColour);
            Assert.AreEqual((int)PhotoAnalysis.FaceShapeId.Round, creator.Draft.faceShape);
            StringAssert.Contains("deleted", creator.PhotoMessage);
            Assert.IsTrue(photo.All(b => b == 0), "the photo's pixels are cleared once read");

            // No face: plain words, nothing changed.
            float tone = creator.Draft.skinTone;
            PhotoAvatar.EditorPhoto = () => (new byte[W * H * 3], W, H, new FaceBox(120f, 130f, 6f));
            creator.OpenPhotoSheet();
            creator.UsePhoto(true);
            Assert.AreEqual(PhotoAvatar.NoFace, creator.PhotoMessage);
            Assert.AreEqual(tone, creator.Draft.skinTone);

            // Cancelled: back to the choice.
            PhotoAvatar.EditorPhoto = () => null;
            creator.UsePhoto(true);
            StringAssert.Contains("deletes it", creator.PhotoMessage);
            creator.ClosePhotoSheet();
            creator.Skip();
        }

        [UnityTest]
        public IEnumerator UnderEighteen_NeverSeesIt()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            var creator = UnityEngine.Object.FindFirstObjectByType<AvatarCreator>();
            creator.Open();
            creator.OpenPhotoSheet();
            creator.AnswerAge(false);
            Assert.IsFalse(creator.PhotoOffered, "the button goes");
            Assert.IsFalse(PhotoAvatar.Offered);
            creator.ClosePhotoSheet();
            creator.OpenPhotoSheet();
            Assert.IsFalse(creator.PhotoSheetOpen, "and doesn't come back");
            string error = "unset";
            PhotoAvatar.Request(false, (s, e) => error = e);
            StringAssert.Contains("18", error);
            creator.Skip();
        }
    }
}

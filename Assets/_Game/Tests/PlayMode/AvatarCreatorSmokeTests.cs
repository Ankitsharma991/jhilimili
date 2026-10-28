using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;
using UtsavNights.UI;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>Phase 6 step 5: the avatar creator in the real Main scene (F15: create, save, load; Skip always there).</summary>
    public class AvatarCreatorSmokeTests
    {
        private AvatarCreator _creator;
        private ProfileService _profiles;

        private IEnumerator Load()
        {
            new ProfileStore().Delete();
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            _creator = Object.FindFirstObjectByType<AvatarCreator>();
            Assert.IsNotNull(_creator, "creator in the HUD");
            Assert.IsTrue(Game.Services.TryGet(out _profiles));
        }

        [TearDown]
        public void TearDown() => new ProfileStore().Delete();

        [UnityTest]
        public IEnumerator Open_ShowsAPreviewAndBlocksTheLighter()
        {
            yield return Load();
            Assert.IsFalse(_profiles.Current.created, "nobody has made an avatar yet");
            _creator.Open();
            yield return null;
            Assert.IsTrue(_creator.IsOpen);
            Assert.IsNotNull(_creator.Stage);
            Assert.IsNotNull(_creator.Stage.Texture, "the preview renders into a texture");
            Assert.AreEqual(0, _creator.Tab, "a first-time player starts on the Looks tab");
            Assert.IsTrue(InputBlockers.IsBlocked(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)));
            Assert.Less(_creator.Stage.transform.position.y, -100f, "the studio is out of the courtyard camera's reach");

            float yaw = _creator.Stage.Yaw;
            _creator.Stage.Rotate(45f);
            Assert.AreNotEqual(yaw, _creator.Stage.Yaw, "drag turns the preview");
            _creator.Skip();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Skip_DiscardsChanges()
        {
            yield return Load();
            AvatarProfile before = _profiles.Current.Clone();
            _creator.Open();
            _creator.ApplyPreset(7);
            _creator.Skip();
            yield return null;
            Assert.IsFalse(_creator.IsOpen);
            CollectionAssert.AreEqual(before.outfit.Select(i => i.id), _profiles.Current.outfit.Select(i => i.id));
            Assert.IsNull(GameObject.Find("AvatarPreviewStage"), "the studio is gone");
        }

        [UnityTest]
        public IEnumerator EditAndSave_PersistsAcrossLoads()
        {
            yield return Load();
            _creator.Open();
            _creator.ApplyPreset(3);
            CollectionAssert.Contains(_creator.Draft.outfit.Select(i => i.id), "lehenga");

            _creator.ShowTab(4);
            string top = _creator.GarmentLabel(0);
            _creator.StepGarment(0, 1);
            Assert.AreNotEqual(top, _creator.GarmentLabel(0), "the top stepper changes the top");
            _creator.StepColour(0, 1);
            _creator.StepGarment(5, 1);
            Assert.AreNotEqual("None", _creator.GarmentLabel(5), "headwear can be added");
            _creator.ToggleAccessory("watch", true);
            _creator.Edit(p => { p.bodyStyle = 1.8f; p.name = "आशा"; p.height = HeightPreset.Tall; });
            yield return null;

            SkinnedMeshRenderer body = _creator.Stage.View.Body;
            Assert.Greater(body.GetBlendShapeWeight(body.sharedMesh.GetBlendShapeIndex(BodyGenerator.BlendBroad)), 70f, "the preview follows the sliders");
            AvatarProfile saved = _creator.Draft.Clone();
            _creator.Save();
            yield return null;
            Assert.IsFalse(_creator.IsOpen);
            Assert.IsTrue(_profiles.Current.created);

            AvatarProfile reloaded = new ProfileStore().Load();
            Assert.AreEqual("आशा", reloaded.name);
            Assert.AreEqual(HeightPreset.Tall, reloaded.height);
            CollectionAssert.AreEquivalent(saved.outfit.Select(i => i.id + ":" + i.variant), reloaded.outfit.Select(i => i.id + ":" + i.variant));
        }

        [UnityTest]
        public IEnumerator EveryOutfitRow_StepsThroughItsGarmentsWithoutBreakingTheOutfit()
        {
            yield return Load();
            _creator.Open();
            for (int row = 0; row < 7; row++)
            {
                for (int step = 0; step < 12; step++)
                {
                    _creator.StepGarment(row, 1);
                    Assert.IsFalse(_creator.Draft.Clone().Sanitize(WardrobeCatalog.Default), $"row {row} step {step}: the outfit stays valid");
                }
            }

            yield return null;
            _creator.Skip();
        }
    }
}

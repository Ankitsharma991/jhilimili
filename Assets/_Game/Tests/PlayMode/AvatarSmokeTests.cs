using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UtsavNights.Core.Avatar;
using UtsavNights.Gameplay;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>Phase 6 step 2: a procedural avatar builds in Unity and follows its profile.</summary>
    public class AvatarSmokeTests
    {
        [UnityTest]
        public IEnumerator AvatarView_BuildsEveryPreset()
        {
            var root = new GameObject("AvatarTestRoot");
            try
            {
                AvatarView view = AvatarView.Create(root.transform, AvatarPresets.All[0]);
                yield return null;
                Assert.AreEqual(AvatarSkeleton.Count, view.Body.bones.Length);
                Assert.AreEqual(6, view.Body.sharedMesh.blendShapeCount);
                Assert.IsNotNull(view.Bone(AvatarSkeleton.Head).GetComponentInChildren<MeshFilter>().sharedMesh);

                foreach (AvatarProfile preset in AvatarPresets.All)
                {
                    view.Apply(preset);
                    yield return null;
                    Assert.AreEqual(AvatarSkeleton.HeightScale(preset.height), view.transform.localScale.y, 1e-4f);
                }
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator Outfit_DrawsOneRendererPerGarment_AndUndresses()
        {
            var root = new GameObject("AvatarTestRoot");
            try
            {
                AvatarProfile p = AvatarPresets.All[6].Clone(); // abaya, hijab, sneakers, watch
                AvatarView view = AvatarView.Create(root.transform, p);
                yield return null;
                int count = 0;
                foreach (Renderer r in view.GarmentRenderers)
                {
                    count++;
                }

                Assert.AreEqual(p.outfit.Count, count);
                Assert.IsNotNull(view.Bone(AvatarSkeleton.Head).Find("Garment hijab (head)"), "the hijab sits on the head bone");

                OutfitRules.Equip(p, WardrobeCatalog.Default, "kurta", 2);
                OutfitRules.Unequip(p, "hijab");
                view.Apply(p);
                yield return null;
                Assert.IsNull(view.Bone(AvatarSkeleton.Head).Find("Garment hijab (head)"), "taken off");
                Assert.IsNull(view.transform.Find("Garment abaya"), "the kurta replaced the abaya");
                Assert.IsNotNull(view.transform.Find("Garment kurta"));
                Assert.AreNotEqual(BodyRegion.None, view.DrawnGarmentsCover & BodyRegion.Torso);
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator Animator_PlaysOneShotsAndReturnsToTheLoop()
        {
            var root = new GameObject("AvatarTestRoot");
            try
            {
                AvatarView view = AvatarView.Create(root.transform, AvatarPresets.All[4]); // t-shirt, jeans, hoodie
                var animator = view.gameObject.AddComponent<AvatarAnimator>();
                yield return null;
                Assert.AreEqual(AvatarClip.Idle, animator.Clip);

                animator.Play(AvatarClip.Ignite);
                animator.Sample(AvatarClip.Ignite, 0.9f);
                Assert.Less(view.Bone(AvatarSkeleton.Hips).localPosition.y, view.HipsBase.y - 0.15f, "crouched");
                Assert.Greater(Quaternion.Angle(Quaternion.identity, view.Bone(AvatarSkeleton.RightUpperArm).localRotation), 40f, "reaching");

                // In a sari the gesture bows from the waist instead of squatting (the clipping check's finding).
                view.Apply(AvatarPresets.All[1]);
                animator.Sample(AvatarClip.Ignite, 0.9f);
                Assert.Greater(view.Bone(AvatarSkeleton.Hips).localPosition.y, view.HipsBase.y - 0.05f, "no deep squat in a long hem");
                Assert.Greater(Quaternion.Angle(Quaternion.identity, view.Bone(AvatarSkeleton.Spine).localRotation), 30f, "bows from the waist");

                animator.Play(AvatarClip.Ignite);
                animator.SetWalking(true);
                animator.Tick(AvatarMotion.IgniteLength + 0.01f);
                Assert.AreEqual(AvatarClip.Walk, animator.Clip, "a one-shot returns to the loop that fits");
                animator.SetWalking(false);
                Assert.AreEqual(AvatarClip.Idle, animator.Clip);
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator Apply_SetsBodyStyleSkinAndProportions()
        {
            var root = new GameObject("AvatarTestRoot");
            try
            {
                var p = new AvatarProfile { bodyStyle = 2f, skinTone = 0f, legLength = 1f, headSize = -1f };
                AvatarView view = AvatarView.Create(root.transform, p);
                yield return null;
                Mesh mesh = view.Body.sharedMesh;
                Assert.AreEqual(100f, view.Body.GetBlendShapeWeight(mesh.GetBlendShapeIndex(BodyGenerator.BlendBroad)), 1e-3f);
                Assert.AreEqual(0f, view.Body.GetBlendShapeWeight(mesh.GetBlendShapeIndex(BodyGenerator.BlendSlim)), 1e-3f);
                Color light = view.SkinMaterial.GetColor("_BaseColor");
                Assert.Greater(light.r, 0.9f, "lightest skin tone");
                Assert.Greater(view.Bone(AvatarSkeleton.LeftUpperLeg).localScale.y, 1.05f, "longer legs");
                Assert.Greater(view.Bone(AvatarSkeleton.Hips).localPosition.y, AvatarSkeleton.Rest[AvatarSkeleton.Hips].Y, "hips lifted to keep the feet down");
                Assert.Less(view.Bone(AvatarSkeleton.Head).localScale.x, AvatarSkeleton.HeadScale, "smaller head");

                view.DrawnGarmentsCover = BodyRegion.Torso;
                view.Apply(p);
                Assert.Less(mesh.GetTriangles(0).Length, BodyGenerator.Build().Submeshes[0].Count, "skin under a drawn garment is hidden");
            }
            finally
            {
                Object.Destroy(root);
            }
        }
    }
}

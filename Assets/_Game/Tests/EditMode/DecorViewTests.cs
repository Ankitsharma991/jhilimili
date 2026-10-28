using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UtsavNights.Core.Home;
using UtsavNights.Gameplay;

namespace UtsavNights.Tests
{
    /// <summary>Phase 7 step 3: the decoration view shares meshes, updates only what changed and drops removed items.</summary>
    public class DecorViewTests
    {
        private DecorView _view;

        [SetUp]
        public void SetUp() => _view = DecorView.Create(null);

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_view.gameObject);

        [Test]
        public void SameItem_SharesOneMeshAndVariantsOnlyChangeMaterials()
        {
            var items = new List<PlacedDecor>
            {
                new PlacedDecor { uid = 1, item = "diya_clay", x = 0f, z = 1f },
                new PlacedDecor { uid = 2, item = "diya_clay", x = 0.3f, z = 1f, variant = 1, yaw = 90f, scale = 1.2f },
            };
            _view.Show(items);
            Assert.AreEqual(2, _view.Count);
            Mesh a = _view.ObjectFor(1).GetComponent<MeshFilter>().sharedMesh, b = _view.ObjectFor(2).GetComponent<MeshFilter>().sharedMesh;
            Assert.AreSame(a, b);
            Assert.AreNotEqual(_view.ObjectFor(1).GetComponent<MeshRenderer>().sharedMaterials[0], _view.ObjectFor(2).GetComponent<MeshRenderer>().sharedMaterials[0]);
            Assert.AreEqual(1.2f, _view.ObjectFor(2).transform.localScale.x, 1e-5f);
            Assert.AreEqual(90f, _view.ObjectFor(2).transform.localEulerAngles.y, 1e-3f);
        }

        [Test]
        public void MovingAnItem_KeepsItsObject_RemovingDestroysIt()
        {
            var item = new PlacedDecor { uid = 7, item = "pot_marigold", x = 1f, z = 2f };
            _view.Show(new List<PlacedDecor> { item });
            GameObject go = _view.ObjectFor(7);
            item.x = -1f;
            _view.Show(new List<PlacedDecor> { item });
            Assert.AreSame(go, _view.ObjectFor(7));
            Assert.AreEqual(-1f, go.transform.localPosition.x, 1e-5f);
            _view.Show(new List<PlacedDecor>());
            Assert.AreEqual(0, _view.Count);
            Assert.IsNull(_view.ObjectFor(7));
        }

        [Test]
        public void PathItems_BuildInHouseSpaceAndRebuildWhenReHung()
        {
            var garland = new PlacedDecor { uid = 3, item = "garland_marigold", x = -1f, y = 2.5f, z = 0.1f, ex = 1f, ey = 2.5f, ez = 0.1f, sag = 0.3f };
            _view.Show(new List<PlacedDecor> { garland });
            MeshFilter filter = _view.ObjectFor(3).GetComponent<MeshFilter>();
            Bounds before = filter.sharedMesh.bounds;
            Assert.AreEqual(0f, before.center.x, 0.05f);
            garland.ex = 2f;
            _view.Show(new List<PlacedDecor> { garland });
            Assert.Greater(_view.ObjectFor(3).GetComponent<MeshFilter>().sharedMesh.bounds.max.x, before.max.x + 0.5f);

            garland.ex = garland.x;
            _view.Show(new List<PlacedDecor> { garland });
            Assert.IsNull(_view.ObjectFor(3), "a zero-length strand shows nothing");
        }
    }
}

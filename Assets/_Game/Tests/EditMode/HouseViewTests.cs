using NUnit.Framework;
using UnityEngine;
using UtsavNights.Core.Home;
using UtsavNights.Gameplay;

namespace UtsavNights.Tests
{
    /// <summary>Phase 7 step 2: the house view rebuilds only what changed and recolours without rebuilding.</summary>
    public class HouseViewTests
    {
        private HouseView _view;

        [SetUp]
        public void SetUp() => _view = HouseView.Create(null, new Texture2D[0]);

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_view.gameObject);

        [Test]
        public void Default_ShowsTheShellAndEveryModule()
        {
            _view.Show(HouseDesign.Default());
            Assert.AreEqual(HomeCatalog.Points.Length + 1, _view.PieceCount);
            Assert.AreEqual(HouseGeometry.WorldFrontZ, _view.transform.position.z, 1e-5f);
            foreach (AttachmentPoint p in HomeCatalog.Points)
            {
                MeshRenderer r = _view.RendererAt(p.Id);
                Assert.IsNotNull(r, p.Id);
                Assert.AreEqual(r.GetComponent<MeshFilter>().sharedMesh.subMeshCount, r.sharedMaterials.Length, p.Id);
            }

            Assert.AreEqual(2, _view.GetComponentsInChildren<Light>().Length, "porch lamp and facade uplight");
        }

        [Test]
        public void SwappingAModule_RebuildsOnlyThatPiece()
        {
            HouseDesign d = HouseDesign.Default();
            _view.Show(d);
            MeshRenderer door = _view.RendererAt("door"), roof = _view.RendererAt("roof"), shell = _view.ShellRenderer;
            d.ModuleAt("door").module = "door_carved_arch";
            _view.Show(d);
            Assert.AreNotSame(door, _view.RendererAt("door"));
            Assert.AreSame(roof, _view.RendererAt("roof"));
            Assert.AreSame(shell, _view.ShellRenderer);
            Assert.IsTrue(_view.RendererAt("door").name.EndsWith("door_carved_arch"));
        }

        [Test]
        public void EmptyBalcony_RemovesItAndRebuildsTheShellWithARailing()
        {
            HouseDesign d = HouseDesign.Default();
            _view.Show(d);
            MeshRenderer shell = _view.ShellRenderer;
            int withBalcony = shell.GetComponent<MeshFilter>().sharedMesh.vertexCount;
            d.ModuleAt("balcony").module = string.Empty;
            _view.Show(d);
            Assert.IsNull(_view.RendererAt("balcony"));
            Assert.AreNotSame(shell, _view.ShellRenderer);
            Assert.Greater(_view.ShellRenderer.GetComponent<MeshFilter>().sharedMesh.vertexCount, withBalcony);
        }

        [Test]
        public void Recolouring_KeepsMeshesAndChangesMaterials()
        {
            HouseDesign d = HouseDesign.Default();
            _view.Show(d);
            MeshRenderer door = _view.RendererAt("door");
            Mesh mesh = door.GetComponent<MeshFilter>().sharedMesh;
            Color before = door.sharedMaterials[0].GetColor("_BaseColor");
            d.trimColour = 3;
            d.ModuleAt("door").variant = 2;
            _view.Show(d);
            Assert.AreSame(door, _view.RendererAt("door"));
            Assert.AreSame(mesh, door.GetComponent<MeshFilter>().sharedMesh);
            Assert.AreNotEqual(before, door.sharedMaterials[0].GetColor("_BaseColor"));
            Assert.AreEqual(3, _view.Shown.trimColour);
        }
    }
}

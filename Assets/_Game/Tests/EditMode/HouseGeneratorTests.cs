using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Home;

namespace UtsavNights.Tests
{
    /// <summary>Phase 7 step 2: the procedural house. Every module fits every point it accepts (PRD 9).</summary>
    public class HouseGeneratorTests
    {
        private const int ModuleTriangleBudget = 2500;
        private const int ShellTriangleBudget = 1500;

        private static readonly object[][] Fits =
            (from p in HomeCatalog.Points from m in HomeCatalog.Modules where m.Type == p.Type select new object[] { p.Id, m.Id }).ToArray();

        [TestCaseSource(nameof(Fits))]
        public void Module_StaysInItsPointsVolumeAndIsWellFormed(string pointId, string moduleId)
        {
            HouseMesh mesh = HouseGenerator.Module(pointId, moduleId);
            Assert.IsNotNull(mesh);
            Assert.Greater(mesh.TriangleCount, 0);
            Assert.LessOrEqual(mesh.TriangleCount, ModuleTriangleBudget, "triangle budget");
            AssertWellFormed(mesh, moduleId);
            PointFrame frame = HouseGeometry.Frame(pointId);
            foreach (V3 p in mesh.Positions)
            {
                Assert.IsTrue(frame.Volumes.Any(v => v.Contains(p)), $"{moduleId} at {pointId}: {p} is outside the point's volume");
                foreach (Box3 solid in HouseGeometry.ShellSolids)
                {
                    Assert.IsFalse(Inside(solid, p), $"{moduleId} at {pointId}: {p} is inside the shell at {solid}");
                }
            }
        }

        [TestCaseSource(nameof(Fits))]
        public void OpeningModules_FillTheWholeOpening(string pointId, string moduleId)
        {
            PointFrame frame = HouseGeometry.Frame(pointId);
            if (!frame.IsOpening)
            {
                return;
            }

            // Something lies across the opening behind the wall face (a leaf or the glass), edge to edge.
            Rect2 o = frame.Opening;
            List<V3> inside = HouseGenerator.Module(pointId, moduleId).Positions.Where(p => p.Z < -0.05f && p.Z > -HouseGeometry.WallThickness).ToList();
            Assert.LessOrEqual(inside.Min(p => p.X), o.X0 + 1e-3f, moduleId);
            Assert.GreaterOrEqual(inside.Max(p => p.X), o.X1 - 1e-3f, moduleId);
            Assert.LessOrEqual(inside.Min(p => p.Y), o.Y0 + 1e-3f, moduleId);
            Assert.GreaterOrEqual(inside.Max(p => p.Y), o.Y1 - 0.06f, moduleId);
        }

        [Test]
        public void PointVolumes_NeverOverlapEachOtherOrTheShell()
        {
            PointFrame[] frames = HouseGeometry.Frames;
            CollectionAssert.AreEquivalent(HomeCatalog.Points.Select(p => p.Id), frames.Select(f => f.PointId), "a frame for every point");
            for (int a = 0; a < frames.Length; a++)
            {
                foreach (Box3 va in frames[a].Volumes)
                {
                    for (int b = a + 1; b < frames.Length; b++)
                    {
                        foreach (Box3 vb in frames[b].Volumes)
                        {
                            Assert.IsFalse(va.Overlaps(vb), $"{frames[a].PointId} {va} overlaps {frames[b].PointId} {vb}");
                        }
                    }

                    foreach (Box3 solid in HouseGeometry.ShellSolids)
                    {
                        Assert.IsFalse(va.Overlaps(solid), $"{frames[a].PointId} {va} overlaps the shell at {solid}");
                    }
                }
            }
        }

        [Test]
        public void Shell_HasAHoleForEveryOpeningAndIsWellFormed()
        {
            foreach (bool empty in new[] { false, true })
            {
                HouseMesh shell = HouseGenerator.Shell(empty);
                Assert.LessOrEqual(shell.TriangleCount, ShellTriangleBudget);
                AssertWellFormed(shell, "shell");
                List<int> wall = shell.Triangles[(int)HousePaint.Wall];
                foreach (Rect2 o in HouseGeometry.FrontOpenings())
                {
                    for (int t = 0; t < wall.Count; t += 3)
                    {
                        V3 c = (shell.Positions[wall[t]] + shell.Positions[wall[t + 1]] + shell.Positions[wall[t + 2]]) * (1f / 3f);
                        bool onFace = Math.Abs(c.Z) < 1e-4f;
                        Assert.IsFalse(onFace && c.X > o.X0 && c.X < o.X1 && c.Y > o.Y0 && c.Y < o.Y1, "wall over an opening");
                    }
                }

                // The wall face covers the whole front except the openings.
                float area = 0f;
                for (int t = 0; t < wall.Count; t += 3)
                {
                    V3 a = shell.Positions[wall[t]], b = shell.Positions[wall[t + 1]], c = shell.Positions[wall[t + 2]];
                    if (Math.Abs(a.Z) < 1e-4f && Math.Abs(b.Z) < 1e-4f && Math.Abs(c.Z) < 1e-4f)
                    {
                        area += V3.Cross(b - a, c - a).Length / 2f;
                    }
                }

                float expected = 2f * HouseGeometry.HalfWidth * HouseGeometry.Eaves - HouseGeometry.FrontOpenings().Sum(o => o.Width * o.Height);
                Assert.AreEqual(expected, area, 1e-2f);
            }

            Assert.Greater(HouseGenerator.Shell(true).TriangleCount, HouseGenerator.Shell(false).TriangleCount, "the Juliet railing");
        }

        [Test]
        public void ModulesOfAType_LookDifferentAndBuildTheSameEveryTime()
        {
            foreach (AttachmentPoint p in HomeCatalog.Points)
            {
                var seen = new HashSet<string>();
                foreach (ModuleInfo m in HomeCatalog.Modules.Where(m => m.Type == p.Type))
                {
                    string fp = Fingerprint(HouseGenerator.Module(p.Id, m.Id));
                    Assert.AreEqual(fp, Fingerprint(HouseGenerator.Module(p.Id, m.Id)), "deterministic");
                    Assert.IsTrue(seen.Add(fp), $"{m.Id} looks like another {p.Type}");
                }
            }
        }

        [Test]
        public void WrongTypeOrUnknown_GivesNoMesh()
        {
            Assert.IsNull(HouseGenerator.Module("door", "window_jali"));
            Assert.IsNull(HouseGenerator.Module("door", "nope"));
            Assert.IsNull(HouseGenerator.Module("attic", "door_wooden_panel"));
        }

        [Test]
        public void Palette_CoversEveryVariantAndPaint()
        {
            foreach (ModuleInfo m in HomeCatalog.Modules)
            {
                Assert.LessOrEqual(m.Variants, HousePalette.Accents.Length, m.Id);
            }

            HouseDesign d = HouseDesign.Default();
            d.wallColour = 3;
            d.trimColour = 2;
            Assert.AreEqual(HomeCatalog.WallColours[3].R, HousePalette.Resolve(HousePaint.Wall, d, null).R);
            Assert.AreEqual(HomeCatalog.TrimColours[2].G, HousePalette.Resolve(HousePaint.Trim, d, null).G);
            Assert.AreEqual(HousePalette.Accents[2].B, HousePalette.Resolve(HousePaint.Accent, d, new ModuleChoice("door", "door_double_teal", 2)).B);
            foreach (HousePaint paint in Enum.GetValues(typeof(HousePaint)))
            {
                var c = HousePalette.Resolve(paint, d, d.modules[0]);
                Assert.IsTrue(c.R >= 0f && c.R <= 1f && c.G >= 0f && c.G <= 1f && c.B >= 0f && c.B <= 1f, paint.ToString());
            }
        }

        [Test]
        public void WorldPlacement_PutsTheBoundaryOnTheCourtyardWalls()
        {
            // The Main scene's back wall stands at world z 3.6 and its side walls at x = +-3.6.
            Assert.AreEqual(3.6f, HouseGeometry.ToWorld(new V3(0f, 0f, HouseGeometry.BackWallZ)).Z, 1e-5f);
            Assert.AreEqual(3.6f, HouseGeometry.BoundaryCentre, 1e-5f);
            Assert.Less(HouseGeometry.WorldFrontZ, -2.0f, "the house stays behind the fireworks camera");
        }

        internal static void AssertWellFormed(HouseMesh mesh, string name)
        {
            Assert.AreEqual(mesh.VertexCount, mesh.Normals.Count);
            Assert.AreEqual(mesh.VertexCount, mesh.U.Count);
            for (int i = 0; i < mesh.VertexCount; i++)
            {
                V3 p = mesh.Positions[i];
                Assert.IsFalse(float.IsNaN(p.X + p.Y + p.Z), name);
                Assert.AreEqual(1f, mesh.Normals[i].Length, 1e-3f, name);
            }

            foreach (List<int> tris in mesh.Triangles)
            {
                Assert.AreEqual(0, tris.Count % 3);
                for (int t = 0; t < tris.Count; t += 3)
                {
                    Assert.IsTrue(tris[t] < mesh.VertexCount && tris[t + 1] < mesh.VertexCount && tris[t + 2] < mesh.VertexCount, name);
                    V3 a = mesh.Positions[tris[t]], b = mesh.Positions[tris[t + 1]], c = mesh.Positions[tris[t + 2]];
                    V3 face = V3.Cross(b - a, c - a);
                    Assert.Greater(face.Length, 1e-7f, $"{name}: degenerate triangle at {a}");

                    // Unity winding: the face normal agrees with the vertex normal, so nothing renders inside out.
                    Assert.Greater(V3.Dot(face, mesh.Normals[tris[t]]), 0f, $"{name}: inside-out triangle at {a}");
                }
            }
        }

        private static bool Inside(Box3 b, V3 p, float eps = 1e-3f) =>
            p.X > b.Min.X + eps && p.X < b.Max.X - eps && p.Y > b.Min.Y + eps && p.Y < b.Max.Y - eps && p.Z > b.Min.Z + eps && p.Z < b.Max.Z - eps;

        private static string Fingerprint(HouseMesh m)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(m.VertexCount).Append('|');
            foreach (List<int> t in m.Triangles)
            {
                sb.Append(t.Count).Append(',');
            }

            Box3 b = m.Bounds();
            sb.Append(b);
            double sum = 0;
            foreach (V3 p in m.Positions)
            {
                sum += p.X * 1.3 + p.Y * 2.7 + p.Z * 5.1;
            }

            return sb.Append('|').Append(sum.ToString("F3")).ToString();
        }
    }
}

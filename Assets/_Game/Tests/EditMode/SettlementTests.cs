using System.Linq;
using NUnit.Framework;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Home;
using UtsavNights.Core.Neighbourhood;

namespace UtsavNights.Tests
{
    /// <summary>Phase 8 step 1: settlement layouts, neighbour houses and the layout lint (PRD 10, F19).</summary>
    public class SettlementTests
    {
        private static readonly object[][] Layouts = Environments.All.Select(e => new object[] { e.Id }).ToArray();

        [TestCaseSource(nameof(Layouts))]
        public void BuiltInLayout_PassesTheLint(string environment)
        {
            SettlementLayout layout = SettlementLibrary.For(environment);
            Assert.IsNotNull(layout);
            CollectionAssert.IsEmpty(SettlementLint.Check(layout), string.Join("\n", SettlementLint.Check(layout)));
            Assert.That(layout.lots.Count, Is.InRange(SettlementLint.MinLots, SettlementLint.MaxLots));
            Assert.IsTrue(layout.spots.Any(s => s.kind == SpotKinds.Courtyard));
            Assert.IsTrue(layout.spots.Any(s => s.kind == Environments.Find(environment).OpenKind), "the environment's open space");
        }

        [TestCaseSource(nameof(Layouts))]
        public void HomeSpots_PassWithEveryRoofAndBalcony(string environment)
        {
            SettlementLayout layout = SettlementLibrary.For(environment);
            foreach (string roof in HomeCatalog.Modules.Where(m => m.Type == ModuleType.Roof).Select(m => m.Id))
            {
                foreach (string balcony in new[] { string.Empty, "balcony_iron_rail", "balcony_jharokha", "balcony_wood_lattice" })
                {
                    HouseDesign home = HouseDesign.Default();
                    home.ModuleAt("roof").module = roof;
                    home.ModuleAt("balcony").module = balcony;
                    var problems = SettlementLint.Check(layout, home)
                        .Where(p => !(roof == "roof_tiled_slope" && p.Contains("'rooftop'")))
                        .Where(p => !(balcony == "balcony_jharokha" && p.Contains("'balcony'")))
                        .ToList();
                    CollectionAssert.IsEmpty(problems, $"{roof} + {balcony}: " + string.Join("\n", problems));
                }
            }
        }

        [Test]
        public void Lint_CatchesBrokenLayouts()
        {
            SettlementLayout good = SettlementLibrary.For(Environments.TownLane);

            SettlementLayout overlap = Copy(good);
            overlap.lots.Find(l => l.id == "lotp1").x = 4f;
            StringAssert.Contains("overlap", string.Join("\n", SettlementLint.Check(overlap)));

            SettlementLayout tooFew = Copy(good);
            tooFew.lots.RemoveAll(l => l.id.StartsWith("far") || l.id == "lotm3" || l.id == "lotm2");
            StringAssert.Contains("lots; a settlement has 6 to 12", string.Join("\n", SettlementLint.Check(tooFew)));

            SettlementLayout cut = Copy(good);
            cut.edges.RemoveAll(e => e.b == "open" || e.a == "open");
            StringAssert.Contains("'open' cannot be reached", string.Join("\n", SettlementLint.Check(cut)));

            SettlementLayout throughWall = Copy(good);
            throughWall.edges.Find(e => e.a == "gate_in").kind = EdgeKinds.Walk;
            StringAssert.Contains("goes through a wall", string.Join("\n", SettlementLint.Check(throughWall)), "walking through the closed gate");

            SettlementLayout blocked = Copy(good);
            blocked.lots.Find(l => l.id == "far2").z = 14f;
            blocked.open.z1 = 7.8f;
            StringAssert.Contains("blocks the sky", string.Join("\n", SettlementLint.Check(blocked)), "a house right across the street");

            SettlementLayout padless = Copy(good);
            padless.Spot("street").pads.Clear();
            StringAssert.Contains("from pads but has none", string.Join("\n", SettlementLint.Check(padless)));
        }

        [Test]
        public void HomeLot_IsTheMainScenesHouse()
        {
            foreach (EnvironmentInfo env in Environments.All)
            {
                LotDef home = SettlementLibrary.For(env.Id).Home;
                V3 gate = home.ToWorld(new V3(HouseGeometry.GateX, 0f, HouseGeometry.BackWallZ));
                Assert.AreEqual(1.4f, gate.X, 1e-4f);
                Assert.AreEqual(3.6f, gate.Z, 1e-4f, "the courtyard's gate in the Main scene");
            }

            var far = new LotDef { x = 6f, z = 34f, yaw = 180f };
            V3 front = far.ToWorld(new V3(1f, 0f, 2f));
            Assert.AreEqual(5f, front.X, 1e-5f, "a lot facing back mirrors x");
            Assert.AreEqual(32f, front.Z, 1e-5f, "and its courtyard lies toward -z");
        }

        [Test]
        public void Neighbours_AreSeededValidAndVaried()
        {
            EnvironmentInfo town = Environments.Find(Environments.TownLane);
            HouseDesign a = NeighbourDesigns.Generate(town, 42);
            Assert.AreEqual(a.Fingerprint(), NeighbourDesigns.Generate(town, 42).Fingerprint(), "the same seed, the same house");
            Assert.IsFalse(a.Clone().Sanitize(), "already a clean design");
            var looks = Enumerable.Range(0, 12).Select(s => NeighbourDesigns.Generate(town, s).Fingerprint()).Distinct().Count();
            Assert.GreaterOrEqual(looks, 11, "neighbours differ");

            foreach (EnvironmentInfo env in Environments.All)
            {
                for (int seed = 0; seed < 8; seed++)
                {
                    HouseDesign d = NeighbourDesigns.Generate(env, seed);
                    Assert.GreaterOrEqual(d.decorations.Count, 4, $"{env.Id} {seed}: neighbours decorate for the festival");
                    Assert.LessOrEqual(DecorPlacement.UnitsUsed(d), NeighbourDesigns.Units);
                    CollectionAssert.IsEmpty(DecorPlacement.Audit(DecorPlacement.For(d, 2)).Select(r => r.Reason), $"{env.Id} {seed}");
                }
            }

            HouseDesign village = NeighbourDesigns.Generate(Environments.Find(Environments.HillVillage), 3);
            Assert.IsTrue(village.decorations.Any(d => d.item == "rangoli_mandala_tihar" || d.item == "rangoli_diya_ring"), "Tihar rangoli in the hill village");
        }

        [Test]
        public void Chunks_CoverTheSettlementAlongTheStreet()
        {
            SettlementLayout layout = SettlementLibrary.For(Environments.TownLane);
            layout.ChunkRange(out int first, out int last);
            Assert.AreEqual(0, SettlementLayout.ChunkOf(0f), "the home lot is chunk 0");
            Assert.AreEqual(1, SettlementLayout.ChunkOf(SettlementLibrary.LotSpacing * 2f));
            Assert.LessOrEqual(first, SettlementLayout.ChunkOf(layout.street.x0));
            Assert.GreaterOrEqual(last, SettlementLayout.ChunkOf(layout.street.x1));
            Assert.That(last - first + 1, Is.InRange(3, 6), "a few street segments, not an open world");
        }

        private static SettlementLayout Copy(SettlementLayout s)
        {
            var c = new SettlementLayout { id = s.id, environment = s.environment };
            foreach (LotDef l in s.lots)
            {
                c.lots.Add(new LotDef { id = l.id, x = l.x, z = l.z, yaw = l.yaw, home = l.home, vacant = l.vacant, seed = l.seed });
            }

            c.street = new GroundArea { kind = s.street.kind, x0 = s.street.x0, z0 = s.street.z0, x1 = s.street.x1, z1 = s.street.z1 };
            c.open = new GroundArea { kind = s.open.kind, x0 = s.open.x0, z0 = s.open.z0, x1 = s.open.x1, z1 = s.open.z1 };
            foreach (SpotDef p in s.spots)
            {
                var q = (SpotDef)typeof(object).GetMethod("MemberwiseClone", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(p, null);
                q.pads = p.pads.Select(x => new LaunchPad { x = x.x, y = x.y, z = x.z }).ToList();
                c.spots.Add(q);
            }

            c.nodes.AddRange(s.nodes.Select(n => new WalkNode { id = n.id, x = n.x, y = n.y, z = n.z }));
            c.edges.AddRange(s.edges.Select(e => new WalkEdge { a = e.a, b = e.b, kind = e.kind }));
            return c;
        }
    }
}

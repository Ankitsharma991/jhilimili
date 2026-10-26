using NUnit.Framework;
using UnityEngine;
using UtsavNights.Gameplay;

namespace UtsavNights.Tests
{
    public class IgnitionTargetsTests
    {
        private sealed class FakeTarget : IIgnitionTarget
        {
            public FakeTarget(int id, bool canIgnite = true)
            {
                TargetId = id;
                CanIgnite = canIgnite;
            }

            public int TargetId { get; }

            public Vector3 FuseWorldPosition => Vector3.zero;

            public bool CanIgnite { get; set; }

            public int IgniteCount { get; private set; }

            public void Ignite() => IgniteCount++;
        }

        [Test]
        public void PickNearest_ChoosesClosestIgnitableWithinRadius()
        {
            var fuses = new[] { new Vector2(100, 100), new Vector2(130, 100), new Vector2(105, 100) };
            Assert.AreEqual(2, IgnitionTargets.PickNearest(new Vector2(104, 100), fuses, new[] { true, true, true }, 50f));
            Assert.AreEqual(0, IgnitionTargets.PickNearest(new Vector2(104, 100), fuses, new[] { true, true, false }, 50f), "skips fuses that cannot ignite");
            Assert.AreEqual(-1, IgnitionTargets.PickNearest(new Vector2(400, 400), fuses, new[] { true, true, true }, 50f), "out of radius");
        }

        [Test]
        public void PickNearest_RadiusIsInclusive()
        {
            var fuses = new[] { new Vector2(0, 0) };
            Assert.AreEqual(0, IgnitionTargets.PickNearest(new Vector2(30, 40), fuses, new[] { true }, 50f));
        }

        [Test]
        public void Registry_RegistersOnce_AndFindsById()
        {
            var targets = new IgnitionTargets();
            int id = targets.NewId();
            var t = new FakeTarget(id);
            targets.Register(t);
            targets.Register(t);
            targets.Register(null);
            Assert.AreEqual(1, targets.Count);
            Assert.AreSame(t, targets.FindById(id));
            Assert.IsNull(targets.FindById(id + 100));
            targets.Unregister(t);
            Assert.AreEqual(0, targets.Count);
            Assert.AreNotEqual(id, targets.NewId(), "ids are unique");
        }
    }
}

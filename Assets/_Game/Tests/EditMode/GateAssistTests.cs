using System;
using NUnit.Framework;
using UtsavNights.Core.Explore;

namespace UtsavNights.Tests
{
    /// <summary>The home's narrow gate: a walk at the wall beside it is turned through it, and nothing else changes.</summary>
    public class GateAssistTests
    {
        private const float GateX = 1.4f;
        private const float WallZ = 3.6f;

        [Test]
        public void AWalkAtTheWallBesideTheGate_TurnsTowardsIt()
        {
            // Half a metre from the wall, a metre left of the gate, pushing straight at the wall.
            (float wx, float wz) = GateAssist.Steer(0.4f, 3.1f, 0f, 1f, GateX, WallZ);
            Assert.Greater(wx, 0.3f, "turned towards the gate");
            Assert.Greater(wz, 0f, "still heading out");
            Assert.AreEqual(1f, Math.Sqrt(wx * wx + wz * wz), 1e-4, "the push's speed is kept");

            // From the right too, and from the street side going in.
            Assert.Less(GateAssist.Steer(2.4f, 3.1f, 0f, 1f, GateX, WallZ).wx, -0.3f);
            (float ix, float iz) = GateAssist.Steer(0.6f, 4.3f, 0f, -0.7f, GateX, WallZ);
            Assert.Greater(ix, 0.2f);
            Assert.Less(iz, 0f, "going in");
            Assert.AreEqual(0.7f, Math.Sqrt(ix * ix + iz * iz), 1e-4, "a half push stays a half push");
        }

        [Test]
        public void InTheGate_ItGoesStraightThrough()
        {
            (float wx, float wz) = GateAssist.Steer(GateX, 3.4f, 0f, 1f, GateX, WallZ);
            Assert.AreEqual(0f, wx, 1e-4);
            Assert.AreEqual(1f, wz, 1e-4);
            Assert.Less(Math.Abs(GateAssist.Steer(GateX + 0.15f, 3.5f, 0f, 1f, GateX, WallZ).wx), 0.6f, "a little off centre: eased back");
        }

        [Test]
        public void NothingChanges_AwayFromTheGate_OrWalkingAlongOrAwayFromTheWall()
        {
            Assert.AreEqual((0f, 1f), GateAssist.Steer(-1.5f, 3.1f, 0f, 1f, GateX, WallZ), "too far along the wall");
            Assert.Greater(GateAssist.Steer(-1.5f, 3.1f, 0f, 1f, GateX, WallZ, GateAssist.CourtyardReach).wx, 0.3f, "but inside the courtyard, the whole wall leads to it");
            Assert.AreEqual((0f, 1f), GateAssist.Steer(0.4f, 1.0f, 0f, 1f, GateX, WallZ), "too far from it");
            Assert.AreEqual((1f, 0f), GateAssist.Steer(0.4f, 3.1f, 1f, 0f, GateX, WallZ), "walking along the wall");
            Assert.AreEqual((0f, -1f), GateAssist.Steer(0.4f, 3.1f, 0f, -1f, GateX, WallZ), "walking away from it");
            Assert.AreEqual((0f, 0f), GateAssist.Steer(0.4f, 3.1f, 0f, 0f, GateX, WallZ), "standing still");
            Assert.AreEqual((0f, 1f), GateAssist.Steer(0.4f, 4.5f, 0f, 1f, GateX, WallZ), "out in the street, walking on");
        }
    }
}

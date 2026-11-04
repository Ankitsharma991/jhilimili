using UtsavNights.Core.Home;

namespace UtsavNights.Core.Explore
{
    /// <summary>
    /// The jobs' things in the world (Phase 12 step 6): a clay diya on a little stand, lit or not, and the waypoint beacon,
    /// a thin column of saffron light with a ring on the ground, seen from down the street. Origins on the ground.
    /// </summary>
    public static class JobKit
    {
        public const float BeaconHeight = 7f;

        public static HouseMesh Diya(bool lit)
        {
            var m = new HouseMesh();
            m.Box(-0.16f, 0f, -0.16f, 0.16f, 0.28f, 0.16f, HousePaint.Brick); // a brick to stand it on
            m.Lathe(0f, 0f, new[] { 0.03f, 0.28f, 0.13f, 0.31f, 0.15f, 0.36f }, 10, HousePaint.Clay, capBottom: true, capTop: true);
            if (lit)
            {
                m.Ball(0f, 0.42f, 0f, 0.045f, 6, 4, HousePaint.Flame);
                m.Lathe(0f, 0f, new[] { 0.035f, 0.38f, 0.02f, 0.5f, 0.001f, 0.55f }, 6, HousePaint.Flame, capBottom: true, capTop: false);
            }

            return m;
        }

        public static HouseMesh Beacon()
        {
            var m = new HouseMesh();
            m.Box(-0.06f, 0f, -0.06f, 0.06f, BeaconHeight, 0.06f, HousePaint.Flame);
            m.FlatRing(0f, 0f, 0.7f, 0.85f, 24, 0.03f, HousePaint.Flame);
            m.Ball(0f, BeaconHeight, 0f, 0.22f, 8, 5, HousePaint.Flame);
            return m;
        }
    }
}

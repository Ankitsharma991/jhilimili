using UtsavNights.Core.Avatar;
using UtsavNights.Core.Home;

namespace UtsavNights.Core.District
{
    /// <summary>
    /// Grey-box vehicles for the district's traffic (Phase 12 step 5), built from boxes like the town: a scooter, an auto
    /// rickshaw and a bicycle, each with its rider or driver, four paint variants each, and a lit headlamp. The origin is on
    /// the ground under the vehicle's middle; it faces +z. The player's scooter is the same scooter without a rider.
    /// </summary>
    public static class VehicleKit
    {
        public const HousePaint Tyre = HousePaint.RoofTile;
        public const HousePaint Metal = HousePaint.Iron;
        public const HousePaint Lamp = HousePaint.Flame;
        public const HousePaint Seat = HousePaint.Leaf;
        public const HousePaint Skin = HousePaint.Clay;
        public const HousePaint Trousers = HousePaint.Accent;

        private static readonly HousePaint[] Bodies = { HousePaint.Powder1, HousePaint.Powder2, HousePaint.Powder3, HousePaint.Powder4 };
        private static readonly HousePaint[] Shirts = { HousePaint.Marigold, HousePaint.MarigoldAlt, HousePaint.Fabric, HousePaint.Trim };
        private static readonly HousePaint[] Frames = { HousePaint.Fabric, HousePaint.Accent, HousePaint.Iron, HousePaint.MarigoldAlt };

        public const int Variants = 4;

        public static HouseMesh Build(VehicleKind kind, int variant, bool rider = true)
        {
            var m = new HouseMesh();
            variant = ((variant % Variants) + Variants) % Variants;
            switch (kind)
            {
                case VehicleKind.Auto:
                    Auto(m, variant);
                    break;
                case VehicleKind.Cycle:
                    Cycle(m, variant, rider);
                    break;
                default:
                    Scooter(m, variant, rider);
                    break;
            }

            return m;
        }

        private static void Scooter(HouseMesh m, int variant, bool rider)
        {
            HousePaint body = Bodies[variant];
            Wheel(m, 0f, 0.62f, 0.24f, 0.11f);
            Wheel(m, 0f, -0.6f, 0.24f, 0.11f);
            m.Box(-0.2f, 0.22f, -0.2f, 0.2f, 0.3f, 0.45f, body); // floorboard
            m.Box(-0.23f, 0.3f, -0.78f, 0.23f, 0.68f, -0.1f, body); // the rear body over the engine
            m.Box(-0.17f, 0.68f, -0.7f, 0.17f, 0.78f, -0.08f, Seat);
            m.Box(-0.2f, 0.3f, 0.45f, 0.2f, 1f, 0.58f, body); // leg shield
            m.Box(-0.05f, 0.24f, 0.56f, 0.05f, 0.92f, 0.64f, Metal); // fork and column
            m.Box(-0.34f, 1.02f, 0.46f, 0.34f, 1.07f, 0.54f, Metal); // handlebar
            m.Box(-0.12f, 0.9f, 0.5f, 0.12f, 1.03f, 0.62f, body); // headset
            m.Box(-0.07f, 0.93f, 0.62f, 0.07f, 1f, 0.65f, Lamp);
            m.Box(-0.07f, 0.52f, -0.81f, 0.07f, 0.6f, -0.78f, HousePaint.Fabric); // tail lamp
            if (rider)
            {
                Rider(m, Shirts[variant], seatY: 0.78f, seatZ: -0.38f, barY: 1.05f, barZ: 0.5f, barX: 0.3f, footY: 0.3f, footZ: 0.2f);
            }
        }

        private static void Auto(HouseMesh m, int variant)
        {
            HousePaint body = variant % 2 == 0 ? HousePaint.MarigoldAlt : HousePaint.RoofTile;
            HousePaint canopy = HousePaint.Powder4;
            Wheel(m, 0f, 1f, 0.22f, 0.12f);
            Wheel(m, -0.62f, -0.85f, 0.22f, 0.12f);
            Wheel(m, 0.62f, -0.85f, 0.22f, 0.12f);
            m.Box(-0.7f, 0.25f, -1.3f, 0.7f, 0.42f, 0.7f, body); // floor pan
            m.Box(-0.7f, 0.42f, -1.32f, 0.7f, 0.95f, -1.15f, body); // back panel
            m.Box(-0.7f, 0.42f, -1.15f, -0.62f, 0.9f, -0.3f, body); // sides behind the doorways
            m.Box(0.62f, 0.42f, -1.15f, 0.7f, 0.9f, -0.3f, body);
            m.Box(-0.6f, 0.5f, -1.15f, 0.6f, 0.75f, -0.6f, Seat); // passenger bench
            m.Box(-0.36f, 0.3f, 0.7f, 0.36f, 1f, 1.3f, body); // the nose
            m.Box(-0.08f, 0.82f, 1.3f, 0.08f, 0.92f, 1.34f, Lamp);
            m.Box(-0.72f, 1.6f, -1.36f, 0.72f, 1.72f, 0.95f, canopy); // the canopy
            m.Box(-0.7f, 0.95f, -1.36f, 0.7f, 1.6f, -1.26f, canopy);
            m.Strut(new V3(-0.66f, 1f, 0.88f), new V3(-0.68f, 1.62f, 0.88f), 0.05f, Metal);
            m.Strut(new V3(0.66f, 1f, 0.88f), new V3(0.68f, 1.62f, 0.88f), 0.05f, Metal);
            m.Box(-0.55f, 1.02f, 0.88f, 0.55f, 1.55f, 0.91f, HousePaint.Dark); // windscreen
            m.Box(-0.3f, 0.95f, 0.65f, 0.3f, 1f, 0.75f, Metal); // handlebar
            Rider(m, Shirts[(variant + 1) % Variants], seatY: 0.78f, seatZ: 0.25f, barY: 0.98f, barZ: 0.7f, barX: 0.28f, footY: 0.45f, footZ: 0.7f);
        }

        private static void Cycle(HouseMesh m, int variant, bool rider)
        {
            HousePaint frame = Frames[variant];
            Wheel(m, 0f, 0.55f, 0.33f, 0.05f);
            Wheel(m, 0f, -0.55f, 0.33f, 0.05f);
            m.Strut(new V3(0f, 0.33f, -0.55f), new V3(0f, 0.86f, -0.2f), 0.045f, frame); // seat stay
            m.Strut(new V3(0f, 0.36f, -0.05f), new V3(0f, 0.9f, -0.2f), 0.05f, frame); // seat tube
            m.Strut(new V3(0f, 0.36f, -0.05f), new V3(0f, 0.95f, 0.42f), 0.05f, frame); // down tube
            m.Strut(new V3(0f, 0.88f, -0.2f), new V3(0f, 0.97f, 0.42f), 0.045f, frame); // top tube
            m.Strut(new V3(0f, 0.33f, 0.55f), new V3(0f, 1.02f, 0.42f), 0.045f, Metal); // fork
            m.Strut(new V3(0f, 0.33f, -0.55f), new V3(0f, 0.36f, -0.05f), 0.04f, frame); // chain stay
            m.Box(-0.28f, 1f, 0.38f, 0.28f, 1.04f, 0.44f, Metal);
            m.Box(-0.08f, 0.9f, -0.32f, 0.08f, 0.95f, -0.1f, Seat);
            m.Box(-0.05f, 0.9f, 0.47f, 0.05f, 0.96f, 0.5f, Lamp);
            if (rider)
            {
                Rider(m, Shirts[(variant + 2) % Variants], seatY: 0.95f, seatZ: -0.22f, barY: 1.02f, barZ: 0.4f, barX: 0.26f, footY: 0.3f, footZ: 0f);
            }
        }

        /// <summary>A seated figure: legs to the feet, a body, arms to the handlebar, a head with dark hair.</summary>
        private static void Rider(HouseMesh m, HousePaint shirt, float seatY, float seatZ, float barY, float barZ, float barX, float footY, float footZ)
        {
            float shoulderY = seatY + 0.55f, shoulderZ = seatZ + 0.06f;
            m.Box(-0.19f, seatY, seatZ - 0.13f, 0.19f, shoulderY + 0.05f, seatZ + 0.13f, shirt);
            m.Box(-0.11f, shoulderY + 0.08f, seatZ - 0.1f, 0.11f, shoulderY + 0.32f, seatZ + 0.12f, Skin);
            m.Box(-0.12f, shoulderY + 0.26f, seatZ - 0.12f, 0.12f, shoulderY + 0.36f, seatZ + 0.1f, HousePaint.Dark);
            foreach (float side in new[] { -1f, 1f })
            {
                m.Strut(new V3(side * 0.2f, shoulderY, shoulderZ), new V3(side * barX, barY, barZ - 0.03f), 0.08f, shirt);
                var knee = new V3(side * 0.13f, seatY + 0.05f, (seatZ + footZ) / 2f + 0.22f);
                m.Strut(new V3(side * 0.11f, seatY + 0.05f, seatZ), knee, 0.12f, Trousers);
                m.Strut(knee, new V3(side * 0.13f, footY, footZ), 0.1f, Trousers);
            }
        }

        /// <summary>A wheel seen from the side: a dark tyre square-ish in profile (two crossed boxes), with a hub.</summary>
        private static void Wheel(HouseMesh m, float x, float z, float r, float halfWidth)
        {
            float k = r * 0.72f;
            m.Box(x - halfWidth, r - k, z - r, x + halfWidth, r + k, z + r, Tyre);
            m.Box(x - halfWidth, 0f, z - k, x + halfWidth, 2f * r, z + k, Tyre);
            m.Box(x - halfWidth - 0.01f, r - 0.05f, z - 0.05f, x + halfWidth + 0.01f, r + 0.05f, z + 0.05f, Metal);
        }
    }
}

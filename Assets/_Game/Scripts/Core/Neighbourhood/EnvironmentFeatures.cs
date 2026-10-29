using System;
using System.Collections.Generic;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Home;

namespace UtsavNights.Core.Neighbourhood
{
    /// <summary>
    /// What makes each environment itself (Phase 8 step 6), in Main-scene world space: the riverside's ghat steps, river and
    /// floating diyas; the hill village's chautari (a stone resting platform round a pipal tree) and the hills behind; the
    /// town lane's raised chowk platform. Built beyond or beside the walkable ground, clear of every spot's play area and
    /// the crowd, and low enough to keep the sky open.
    /// </summary>
    public static class EnvironmentFeatures
    {
        /// <summary>The landmark kinds a layout may name (environment packs choose one).</summary>
        public static class Features
        {
            public const string Ghat = "ghat";
            public const string Chautari = "chautari";
            public const string Platform = "platform";

            public static readonly string[] All = { string.Empty, Ghat, Chautari, Platform };
        }

        /// <summary>Solid boxes the features add (for the lint and for placement colliders).</summary>
        public static List<Box3> Solids(SettlementLayout layout)
        {
            var solids = new List<Box3>();
            switch (layout.feature)
            {
                case Features.Chautari:
                {
                    V3 c = ChautariCentre(layout);
                    solids.Add(new Box3(c.X - 1.6f, 0f, c.Z - 1.6f, c.X + 1.6f, 0.55f, c.Z + 1.6f));
                    solids.Add(new Box3(c.X - 0.35f, 0f, c.Z - 0.35f, c.X + 0.35f, 4.5f, c.Z + 0.35f));
                    break;
                }

                case Features.Platform:
                {
                    V3 c = PlatformCentre(layout);
                    solids.Add(new Box3(c.X - 2.2f, 0f, c.Z - 1.4f, c.X + 2.2f, 0.35f, c.Z + 1.4f));
                    break;
                }
            }

            return solids;
        }

        public static HouseMesh Build(SettlementLayout layout)
        {
            var m = new HouseMesh();
            switch (layout.feature)
            {
                case Features.Ghat:
                    Ghat(m, layout);
                    break;
                case Features.Chautari:
                    Chautari(m, layout);
                    Hills(m, layout);
                    break;
                case Features.Platform:
                    Platform(m, layout);
                    break;
            }

            return m;
        }

        /// <summary>The chautari stands on the threshing ground away from its celebration spot and the crowd.</summary>
        public static V3 ChautariCentre(SettlementLayout layout) => new V3(-15f, 0f, (layout.open.z0 + layout.open.z1) / 2f);

        public static V3 PlatformCentre(SettlementLayout layout) => new V3(18f, 0f, layout.open.z1 - 3f);

        private static void Ghat(HouseMesh m, SettlementLayout layout)
        {
            // Stone steps down from the ghat's edge to the water, then the river to the far bank.
            float x0 = layout.open.x0, x1 = layout.open.x1, z = layout.open.z1;
            const int steps = 8;
            for (int i = 0; i < steps; i++)
            {
                float top = -i * 0.22f, front = z + i * 0.45f;
                m.Box(x0, top - 0.22f, front, x1, top, front + 0.45f, HousePaint.Stone, Faces.Bottom | Faces.Back);
            }

            float water = -steps * 0.22f + 0.1f, edge = z + steps * 0.45f;
            m.Quad(new V3(x0 - 60f, water, edge - 0.5f), new V3(x1 + 60f, water, edge - 0.5f), new V3(x1 + 60f, water, edge + 90f), new V3(x0 - 60f, water, edge + 90f),
                new V3(0, 1, 0), HousePaint.Dark);

            // Floating diyas drifting on the river (Dev Deepawali): little glowing leaf boats.
            var rng = new Random(91);
            for (int i = 0; i < 60; i++)
            {
                float x = (float)(x0 + rng.NextDouble() * (x1 - x0)), dz = (float)(1 + rng.NextDouble() * 22);
                m.Box(x - 0.06f, water, edge + dz - 0.04f, x + 0.06f, water + 0.03f, edge + dz + 0.04f, HousePaint.Leaf, Faces.Bottom);
                m.Box(x - 0.015f, water + 0.03f, edge + dz - 0.015f, x + 0.015f, water + 0.08f, edge + dz + 0.015f, HousePaint.Flame, Faces.Bottom);
            }

            // The far bank, a low dark line of trees.
            m.Box(x0 - 60f, water, edge + 90f, x1 + 60f, 3f, edge + 92f, HousePaint.Leaf, Faces.Bottom | Faces.Back);
        }

        private static void Chautari(HouseMesh m, SettlementLayout layout)
        {
            V3 c = ChautariCentre(layout);
            m.Box(c.X - 1.6f, 0f, c.Z - 1.6f, c.X + 1.6f, 0.4f, c.Z + 1.6f, HousePaint.Stone, Faces.Bottom);
            m.Box(c.X - 1.5f, 0.4f, c.Z - 1.5f, c.X + 1.5f, 0.55f, c.Z + 1.5f, HousePaint.Stone, Faces.Bottom);
            m.Lathe(c.X, c.Z, new[] { 0.35f, 0.55f, 0.28f, 2.6f, 0.2f, 3.4f }, 8, HousePaint.Wood, capBottom: false);
            float[][] crown = { new[] { 0f, 4.4f, 0f, 1.7f }, new[] { -1.2f, 3.9f, 0.4f, 1.2f }, new[] { 1.1f, 4.0f, -0.3f, 1.3f }, new[] { 0.2f, 5.2f, 0.3f, 1.2f } };
            foreach (float[] b in crown)
            {
                m.Ball(c.X + b[0], b[1], c.Z + b[2], b[3], 9, 5, HousePaint.Leaf);
            }

            // Diyas round its edge for Tihar.
            for (int i = 0; i < 12; i++)
            {
                double a = 2 * Math.PI * i / 12;
                float x = c.X + 1.45f * (float)Math.Cos(a), z = c.Z + 1.45f * (float)Math.Sin(a);
                m.Lathe(x, z, new[] { 0.03f, 0.55f, 0.05f, 0.58f }, 6, HousePaint.Clay, capBottom: false);
                m.Lathe(x, z, new[] { 0f, 0.58f, 0.012f, 0.6f, 0f, 0.64f }, 4, HousePaint.Flame);
            }
        }

        private static void Hills(HouseMesh m, SettlementLayout layout)
        {
            // Layered ridges far behind the far lots: low on screen, so the sky stays open.
            var rng = new Random(17);
            for (int layer = 0; layer < 2; layer++)
            {
                float z = 110f + layer * 45f, height = 9f + layer * 8f;
                float x = -160f;
                while (x < 160f)
                {
                    float w = (float)(30 + rng.NextDouble() * 40), h = (float)(height * (0.6 + rng.NextDouble() * 0.5));
                    m.TriangleToward(new V3(x, -0.5f, z), new V3(x + w, -0.5f, z), new V3(x + w / 2f, h, z + 1f), new V3(0, 0, -1), HousePaint.Dark);
                    x += w * 0.7f;
                }
            }
        }

        private static void Platform(HouseMesh m, SettlementLayout layout)
        {
            V3 c = PlatformCentre(layout);
            m.Box(c.X - 2.2f, 0f, c.Z - 1.4f, c.X + 2.2f, 0.35f, c.Z + 1.4f, HousePaint.Stone, Faces.Bottom);
            for (int i = 0; i < 4; i++)
            {
                float x = c.X - 1.6f + i * 1.07f;
                m.Lathe(x, c.Z - 1.25f, new[] { 0.035f, 0.35f, 0.05f, 0.38f }, 6, HousePaint.Clay, capBottom: false);
                m.Lathe(x, c.Z - 1.25f, new[] { 0f, 0.38f, 0.012f, 0.4f, 0f, 0.44f }, 4, HousePaint.Flame);
            }
        }
    }
}

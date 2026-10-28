using System;
using UtsavNights.Core.Avatar;

namespace UtsavNights.Core.Home
{
    /// <summary>
    /// Builds the courtyard house (Phase 7 step 2, PRD 9): the shell (front wall cut for every opening, plinth, step,
    /// floor band, cornice, sides and the glazed upper door) and each facade module at its attachment point. Modules are
    /// built in their point's frame from the opening size, so one window module fits every window point. Colour
    /// variants change only materials, never the mesh.
    /// </summary>
    public static class HouseGenerator
    {
        private const int ArchSegments = 12;

        public static HouseMesh Shell(bool balconyEmpty)
        {
            var m = new HouseMesh();
            const float w = HouseGeometry.HalfWidth, top = HouseGeometry.Eaves, d = HouseGeometry.Depth;
            m.WallWithHoles(-w, 0f, w, top, 0f, HouseGeometry.FrontOpenings(), HouseGeometry.WallThickness, HousePaint.Wall);

            // Sides and the flat roof deck; the back is never seen.
            m.Quad(new V3(w, 0, 0), new V3(w, 0, -d), new V3(w, top, -d), new V3(w, top, 0), new V3(1, 0, 0), HousePaint.Wall);
            m.Quad(new V3(-w, 0, 0), new V3(-w, top, 0), new V3(-w, top, -d), new V3(-w, 0, -d), new V3(-1, 0, 0), HousePaint.Wall);
            m.Quad(new V3(-w, top, 0), new V3(w, top, 0), new V3(w, top, -d), new V3(-w, top, -d), new V3(0, 1, 0), HousePaint.Stone);

            foreach (Box3 b in HouseGeometry.ShellSolids)
            {
                Paint(m, b, b.Min.Y >= 3f ? HousePaint.Trim : HousePaint.Stone, Faces.Back);
            }

            // The glazed double door behind the balcony.
            m.Offset = new V3(0f, HouseGeometry.UpperFloor, 0f);
            Rect2 u = HouseGeometry.UpperDoor;
            float uw = u.Width, uh = u.Height;
            Frame(m, uw, uh, HousePaint.Trim, sill: false);
            m.Box(-uw / 2f, 0f, -0.14f, uw / 2f, uh, -0.12f, HousePaint.Glass, Faces.Back);
            m.Box(-0.03f, 0f, -0.12f, 0.03f, uh, -0.08f, HousePaint.Trim, Faces.Back);
            for (int i = 1; i <= 3; i++)
            {
                float y = uh * i / 4f;
                m.Box(-uw / 2f, y - 0.025f, -0.12f, uw / 2f, y + 0.025f, -0.09f, HousePaint.Trim, Faces.Back);
            }

            if (balconyEmpty)
            {
                // A Juliet railing, so the upper door never opens onto a drop.
                RailBars(m, -0.66f, 0.66f, 0f, 0.95f, 0.04f, 0.08f, 0.1f, HousePaint.Iron);
            }

            m.Offset = V3.Zero;
            return m;
        }

        /// <summary>The module at a point, in house space. Null for an empty optional point or a module that does not fit.</summary>
        public static HouseMesh Module(string pointId, string moduleId)
        {
            AttachmentPoint point = HomeCatalog.Point(pointId);
            ModuleInfo module = HomeCatalog.Module(moduleId);
            PointFrame frame = HouseGeometry.Frame(pointId);
            if (point == null || module == null || frame == null || module.Type != point.Type)
            {
                return null;
            }

            var m = new HouseMesh { Offset = frame.Origin };
            switch (module.Type)
            {
                case ModuleType.Door:
                    Door(m, module.Id, frame.OpeningWidth, frame.OpeningHeight);
                    break;
                case ModuleType.Window:
                    Window(m, module.Id, frame.OpeningWidth, frame.OpeningHeight);
                    break;
                case ModuleType.Balcony:
                    Balcony(m, module.Id);
                    break;
                case ModuleType.Roof:
                    Roof(m, module.Id);
                    break;
                case ModuleType.Boundary:
                    Boundary(m, module.Id);
                    break;
                case ModuleType.Gate:
                    Gate(m, module.Id);
                    break;
            }

            return m;
        }

        // ---- Doors (local: opening bottom centre; w x h) ----

        private static void Door(HouseMesh m, string id, float w, float h)
        {
            float hw = w / 2f;
            switch (id)
            {
                case "door_wooden_panel":
                    Frame(m, w, h, HousePaint.Trim, sill: false);
                    m.Box(-hw, 0f, -0.14f, hw, h, -0.08f, HousePaint.Accent, Faces.Back);
                    for (int col = 0; col < 2; col++)
                    {
                        for (int row = 0; row < 3; row++)
                        {
                            float x0 = -hw + 0.1f + col * (w - 0.1f) / 2f, x1 = x0 + (w - 0.3f) / 2f;
                            float y0 = 0.12f + row * (h - 0.2f) / 3f, y1 = y0 + (h - 0.2f) / 3f - 0.12f;
                            m.Box(x0, y0, -0.08f, x1, y1, -0.06f, HousePaint.Accent, Faces.Back);
                        }
                    }

                    m.Box(hw - 0.16f, 1.0f, -0.06f, hw - 0.12f, 1.12f, -0.02f, HousePaint.Iron, Faces.Back);
                    break;

                case "door_carved_arch":
                {
                    float spring = h - hw;
                    Jambs(m, w, spring, HousePaint.Trim);
                    m.ArchBand(0f, spring, hw - 0.04f, hw + 0.08f, -0.06f, 0.06f, ArchSegments, HousePaint.Trim);
                    m.Spandrels(0f, spring, hw, h, 0f, ArchSegments, HousePaint.Wall);
                    m.Box(-0.07f, h - 0.04f, -0.02f, 0.07f, h + 0.12f, 0.08f, HousePaint.Trim, Faces.Back); // keystone
                    m.Box(-hw, 0f, -0.14f, hw, spring, -0.08f, HousePaint.Wood, Faces.Back);
                    m.HalfDisc(0f, spring, -0.08f, hw, ArchSegments, HousePaint.Wood);
                    m.Box(-0.012f, 0f, -0.08f, 0.012f, h - 0.05f, -0.07f, HousePaint.Dark, Faces.Back);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        for (int row = 0; row < 5; row++)
                        {
                            for (int col = 0; col < 3; col++)
                            {
                                float x = side * (0.1f + col * (hw - 0.2f) / 2f), y = 0.2f + row * (spring - 0.4f) / 4f;
                                m.Box(x - 0.022f, y - 0.022f, -0.08f, x + 0.022f, y + 0.022f, -0.05f, HousePaint.Iron, Faces.Back);
                            }
                        }
                    }

                    break;
                }

                case "door_double_teal":
                    Frame(m, w, h, HousePaint.Trim, sill: false);
                    float leafTop = h - 0.28f;
                    m.Box(-hw, leafTop, -0.14f, hw, h, -0.12f, HousePaint.Glass, Faces.Back); // fanlight
                    for (int i = 1; i <= 4; i++)
                    {
                        float x = -hw + w * i / 5f;
                        m.Box(x - 0.012f, leafTop, -0.12f, x + 0.012f, h, -0.1f, HousePaint.Iron, Faces.Back);
                    }

                    m.Box(-hw, leafTop - 0.04f, -0.12f, hw, leafTop, -0.06f, HousePaint.Trim, Faces.Back); // transom
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float x0 = side < 0 ? -hw : 0.01f, x1 = side < 0 ? -0.01f : hw;
                        m.Box(x0, 0f, -0.14f, x1, leafTop - 0.04f, -0.08f, HousePaint.Accent, Faces.Back);
                        m.Box(x0 + 0.08f, leafTop - 0.7f, -0.09f, x1 - 0.08f, leafTop - 0.16f, -0.075f, HousePaint.Glass, Faces.Back);
                        m.Box(x0 + 0.08f, 0.15f, -0.08f, x1 - 0.08f, 0.85f, -0.06f, HousePaint.Accent, Faces.Back);
                        m.Box(x0 + 0.08f, 0.95f, -0.08f, x1 - 0.08f, leafTop - 0.8f, -0.06f, HousePaint.Accent, Faces.Back);
                    }

                    m.Box(-0.08f, 0.95f, -0.06f, -0.05f, 1.1f, -0.02f, HousePaint.Iron, Faces.Back);
                    m.Box(0.05f, 0.95f, -0.06f, 0.08f, 1.1f, -0.02f, HousePaint.Iron, Faces.Back);
                    break;

                case "door_steel_modern":
                    Frame(m, w, h, HousePaint.Iron, sill: false);
                    m.Box(-hw, 0f, -0.14f, hw - 0.22f, h, -0.08f, HousePaint.Accent, Faces.Back);
                    m.Box(hw - 0.2f, 0.1f, -0.14f, hw, h - 0.1f, -0.12f, HousePaint.Glass, Faces.Back); // side light
                    for (int i = 1; i <= 5; i++)
                    {
                        float y = h * i / 6f;
                        m.Box(-hw + 0.06f, y - 0.008f, -0.08f, hw - 0.28f, y + 0.008f, -0.07f, HousePaint.Dark, Faces.Back);
                    }

                    m.Box(hw - 0.36f, 0.85f, -0.07f, hw - 0.33f, 1.25f, -0.03f, HousePaint.Iron, Faces.Back);
                    m.Box(-0.75f, h + 0.12f, -0.02f, 0.75f, h + 0.2f, 0.65f, HousePaint.Trim, Faces.Back); // canopy
                    m.Box(-0.72f, 1.55f, 0f, -0.68f, 1.75f, 0.08f, HousePaint.Glass, Faces.Back); // wall lamp
                    break;
            }

            m.Box(-hw - 0.06f, 0f, -0.25f, hw + 0.06f, 0.025f, 0.02f, HousePaint.Stone, Faces.Back | Faces.Bottom); // threshold
        }

        // ---- Windows (local: opening bottom centre; w x h) ----

        private static void Window(HouseMesh m, string id, float w, float h)
        {
            float hw = w / 2f;
            m.Box(-hw - 0.12f, -0.08f, -0.06f, hw + 0.12f, 0f, 0.12f, HousePaint.Stone, Faces.Back); // sill
            switch (id)
            {
                case "window_shuttered":
                    Frame(m, w, h, HousePaint.Trim, sill: false);
                    Glazing(m, w, h);
                    m.Box(-0.02f, 0f, -0.1f, 0.02f, h, -0.07f, HousePaint.Trim, Faces.Back);
                    m.Box(-hw, h * 0.5f - 0.02f, -0.1f, hw, h * 0.5f + 0.02f, -0.07f, HousePaint.Trim, Faces.Back);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        // Open shutters, folded flat against the wall either side.
                        float inner = side * (hw + 0.09f), outer = side * (hw + 0.09f + 0.45f);
                        m.Box(inner, 0f, 0.005f, outer, h, 0.04f, HousePaint.Accent, Faces.Back);
                        for (int s = 1; s <= 6; s++)
                        {
                            float y = h * s / 7f;
                            m.Box(inner + side * 0.04f, y - 0.012f, 0.04f, outer - side * 0.04f, y + 0.012f, 0.05f, HousePaint.Accent, Faces.Back);
                        }
                    }

                    break;

                case "window_arched":
                {
                    float spring = h - hw;
                    Jambs(m, w, spring, HousePaint.Trim);
                    m.ArchBand(0f, spring, hw - 0.04f, hw + 0.08f, -0.06f, 0.06f, ArchSegments, HousePaint.Trim);
                    m.ArchBand(0f, spring, hw + 0.14f, hw + 0.2f, 0f, 0.1f, ArchSegments, HousePaint.Trim); // hood moulding
                    m.Spandrels(0f, spring, hw, h, 0f, ArchSegments, HousePaint.Wall);
                    m.Box(-hw, 0f, -0.12f, hw, spring, -0.1f, HousePaint.Glass, Faces.Back);
                    m.HalfDisc(0f, spring, -0.1f, hw, ArchSegments, HousePaint.Glass);
                    m.Box(-0.02f, 0f, -0.1f, 0.02f, h - 0.04f, -0.07f, HousePaint.Trim, Faces.Back);
                    m.Box(-hw, spring - 0.02f, -0.1f, hw, spring + 0.02f, -0.07f, HousePaint.Trim, Faces.Back);
                    break;
                }

                case "window_jali":
                    Frame(m, w, h, HousePaint.Trim, sill: false);
                    Glazing(m, w, h);
                    for (int i = 1; i < 7; i++)
                    {
                        float x = -hw + w * i / 7f;
                        m.Box(x - 0.025f, 0f, -0.1f, x + 0.025f, h, -0.04f, HousePaint.Stone, Faces.Back);
                    }

                    for (int j = 1; j < 9; j++)
                    {
                        float y = h * j / 9f;
                        m.Box(-hw, y - 0.025f, -0.1f, hw, y + 0.025f, -0.04f, HousePaint.Stone, Faces.Back);
                    }

                    // Chhajja: a stone sunshade on two brackets.
                    m.Box(-0.75f, h + 0.12f, 0f, 0.75f, h + 0.2f, 0.35f, HousePaint.Trim);
                    m.Box(-0.6f, h + 0.0f, 0f, -0.52f, h + 0.12f, 0.22f, HousePaint.Trim, Faces.Back);
                    m.Box(0.52f, h + 0.0f, 0f, 0.6f, h + 0.12f, 0.22f, HousePaint.Trim, Faces.Back);
                    break;

                case "window_plain":
                    Frame(m, w, h, HousePaint.Trim, sill: false);
                    Glazing(m, w, h);
                    m.Box(-0.015f, 0f, -0.1f, 0.015f, h, -0.08f, HousePaint.Trim, Faces.Back);
                    RailBars(m, -hw, hw, 0f, h, 0.02f, 0.05f, 0.11f, HousePaint.Iron); // security grille
                    break;
            }
        }

        // ---- Balconies (local: the slab's front centre, at the slab bottom) ----

        private static void Balcony(HouseMesh m, string id)
        {
            const float hw = 1f, depth = 0.9f, slab = 0.15f;
            m.Box(-hw, 0f, 0f, hw, slab, depth, HousePaint.Stone, Faces.Back);
            switch (id)
            {
                case "balcony_iron_rail":
                    Corbels(m, HousePaint.Trim);
                    m.Box(-hw, slab + 0.95f, depth - 0.06f, hw, slab + 1.0f, depth, HousePaint.Accent);
                    m.Box(-hw, slab + 0.95f, 0f, -hw + 0.05f, slab + 1.0f, depth, HousePaint.Accent, Faces.Back);
                    m.Box(hw - 0.05f, slab + 0.95f, 0f, hw, slab + 1.0f, depth, HousePaint.Accent, Faces.Back);
                    m.Box(-hw, slab + 0.06f, depth - 0.05f, hw, slab + 0.1f, depth - 0.01f, HousePaint.Accent);
                    for (float x = -hw + 0.06f; x < hw - 0.03f; x += 0.12f)
                    {
                        m.Box(x - 0.012f, slab, depth - 0.045f, x + 0.012f, slab + 0.95f, depth - 0.02f, HousePaint.Accent, Faces.Bottom | Faces.Top);
                    }

                    for (float z = 0.1f; z < depth - 0.05f; z += 0.12f)
                    {
                        m.Box(-hw + 0.01f, slab, z - 0.012f, -hw + 0.035f, slab + 0.95f, z + 0.012f, HousePaint.Accent, Faces.Bottom | Faces.Top);
                        m.Box(hw - 0.035f, slab, z - 0.012f, hw - 0.01f, slab + 0.95f, z + 0.012f, HousePaint.Accent, Faces.Bottom | Faces.Top);
                    }

                    break;

                case "balcony_jharokha":
                {
                    // A covered stone balcony: carved parapet, four slender pillars and a curved canopy with finials.
                    Corbels(m, HousePaint.Trim);
                    const float top = slab + 0.8f;
                    m.Box(-hw, slab, depth - 0.15f, hw, top, depth, HousePaint.Accent);
                    m.Box(-hw, slab, 0f, -hw + 0.12f, top, depth - 0.15f, HousePaint.Accent, Faces.Back);
                    m.Box(hw - 0.12f, slab, 0f, hw, top, depth - 0.15f, HousePaint.Accent, Faces.Back);
                    for (int i = 0; i < 4; i++)
                    {
                        float x = -hw + 0.15f + i * (2f * hw - 0.3f) / 4f + 0.06f;
                        m.Box(x, slab + 0.15f, depth, x + (2f * hw - 0.3f) / 4f - 0.12f, top - 0.15f, depth + 0.02f, HousePaint.Trim, Faces.Back);
                    }

                    const float capital = 2.3f;
                    foreach (float x in new[] { -hw + 0.06f, hw - 0.06f })
                    {
                        foreach (float z in new[] { 0.08f, depth - 0.08f })
                        {
                            m.Box(x - 0.05f, top, z - 0.05f, x + 0.05f, capital, z + 0.05f, HousePaint.Trim, Faces.Bottom | Faces.Top);
                        }
                    }

                    m.Box(-1.05f, capital, 0f, 1.05f, capital + 0.1f, depth + 0.1f, HousePaint.Trim, Faces.Back);
                    float rise = 0.25f, ridge = 0.4f;
                    var outward = new V3(0, 0.6f, 1f);
                    m.QuadToward(new V3(-1.05f, capital + 0.1f, depth + 0.1f), new V3(1.05f, capital + 0.1f, depth + 0.1f),
                        new V3(1.05f, capital + 0.1f + rise, ridge), new V3(-1.05f, capital + 0.1f + rise, ridge), outward, HousePaint.RoofTile);
                    m.QuadToward(new V3(-1.05f, capital + 0.1f + rise, ridge), new V3(1.05f, capital + 0.1f + rise, ridge),
                        new V3(1.05f, capital + 0.1f, 0f), new V3(-1.05f, capital + 0.1f, 0f), new V3(0, 1, -0.3f), HousePaint.RoofTile);
                    foreach (float sx in new[] { -1.05f, 1.05f })
                    {
                        m.TriangleToward(new V3(sx, capital + 0.1f, 0f), new V3(sx, capital + 0.1f, depth + 0.1f), new V3(sx, capital + 0.1f + rise, ridge),
                            new V3(sx, 0, 0), HousePaint.RoofTile);
                    }

                    foreach (float x in new[] { -0.8f, 0f, 0.8f })
                    {
                        m.Box(x - 0.04f, capital + 0.1f + rise - 0.02f, ridge - 0.04f, x + 0.04f, capital + 0.1f + rise + 0.12f, ridge + 0.04f, HousePaint.Trim, Faces.Bottom);
                    }

                    break;
                }

                case "balcony_wood_lattice":
                {
                    m.Box(-0.85f, -0.18f, 0f, -0.73f, 0f, 0.6f, HousePaint.Wood, Faces.Back);
                    m.Box(-0.06f, -0.18f, 0f, 0.06f, 0f, 0.6f, HousePaint.Wood, Faces.Back);
                    m.Box(0.73f, -0.18f, 0f, 0.85f, 0f, 0.6f, HousePaint.Wood, Faces.Back);
                    const float top = slab + 0.9f;
                    m.Box(-hw, top - 0.06f, depth - 0.07f, hw, top, depth, HousePaint.Accent);
                    m.Box(-hw, slab, depth - 0.07f, hw, slab + 0.08f, depth, HousePaint.Accent);
                    m.Box(-hw, top - 0.06f, 0f, -hw + 0.07f, top, depth, HousePaint.Accent, Faces.Back);
                    m.Box(hw - 0.07f, top - 0.06f, 0f, hw, top, depth, HousePaint.Accent, Faces.Back);
                    for (float x = -hw; x <= hw + 1e-3f; x += 0.5f)
                    {
                        m.Box(Math.Max(-hw, x - 0.04f), slab, depth - 0.08f, Math.Min(hw, x + 0.04f), top, depth, HousePaint.Accent, Faces.Bottom | Faces.Top);
                    }

                    // Lattice: slats both ways between the rails.
                    for (float x = -hw + 0.1f; x < hw - 0.05f; x += 0.1f)
                    {
                        m.Box(x - 0.012f, slab + 0.08f, depth - 0.05f, x + 0.012f, top - 0.06f, depth - 0.03f, HousePaint.Wood, Faces.Bottom | Faces.Top);
                    }

                    for (float y = slab + 0.2f; y < top - 0.1f; y += 0.12f)
                    {
                        m.Box(-hw + 0.04f, y - 0.012f, depth - 0.05f, hw - 0.04f, y + 0.012f, depth - 0.03f, HousePaint.Wood, Faces.Left | Faces.Right);
                    }

                    m.Box(-hw + 0.01f, slab, 0f, -hw + 0.06f, top - 0.06f, depth - 0.07f, HousePaint.Wood, Faces.Back);
                    m.Box(hw - 0.06f, slab, 0f, hw - 0.01f, top - 0.06f, depth - 0.07f, HousePaint.Wood, Faces.Back);
                    break;
                }
            }
        }

        private static void Corbels(HouseMesh m, HousePaint paint)
        {
            foreach (float x in new[] { -0.8f, 0f, 0.8f })
            {
                m.Box(x - 0.06f, -0.18f, 0f, x + 0.06f, 0f, 0.5f, paint, Faces.Back | Faces.Top);
                m.Box(x - 0.06f, -0.08f, 0.5f, x + 0.06f, 0f, 0.7f, paint, Faces.Back | Faces.Top);
            }
        }

        // ---- Roofs (local: the front top edge of the walls) ----

        private static void Roof(HouseMesh m, string id)
        {
            const float w = HouseGeometry.HalfWidth, d = HouseGeometry.Depth;
            switch (id)
            {
                case "roof_flat_parapet":
                    Parapet(m, 0.7f, HousePaint.Wall);
                    Coping(m, 0.7f);
                    for (int i = -1; i <= 1; i++)
                    {
                        float x = i * 2.6f;
                        m.Box(x - 0.05f, 0.05f, 0f, x + 0.05f, 0.13f, 0.3f, HousePaint.Stone, Faces.Back); // water spouts
                    }

                    break;

                case "roof_tiled_slope":
                {
                    const float eaveZ = 0.4f, eaveY = 0.08f, ridgeZ = -d / 2f, ridgeY = 1.7f, backZ = -d - 0.4f, hw = w + 0.2f;
                    var front = new V3(0, ridgeY - eaveY, eaveZ - ridgeZ);
                    var back = new V3(0, ridgeY - eaveY, backZ - ridgeZ);
                    m.QuadToward(new V3(-hw, eaveY, eaveZ), new V3(hw, eaveY, eaveZ), new V3(hw, ridgeY, ridgeZ), new V3(-hw, ridgeY, ridgeZ),
                        V3.Cross(front, new V3(1, 0, 0)) * -1f, HousePaint.RoofTile);
                    m.QuadToward(new V3(-hw, ridgeY, ridgeZ), new V3(hw, ridgeY, ridgeZ), new V3(hw, eaveY, backZ), new V3(-hw, eaveY, backZ),
                        new V3(0, 1, -0.5f), HousePaint.RoofTile);

                    // Tile courses: thin ribs across the front slope.
                    for (int c = 1; c < 9; c++)
                    {
                        float t = c / 9f, y = eaveY + (ridgeY - eaveY) * t, z = eaveZ + (ridgeZ - eaveZ) * t;
                        m.Box(-hw, y, z - 0.05f, hw, y + 0.035f, z + 0.01f, HousePaint.RoofTile, Faces.Bottom | Faces.Back);
                    }

                    m.Box(-hw, ridgeY - 0.04f, ridgeZ - 0.1f, hw, ridgeY + 0.08f, ridgeZ + 0.1f, HousePaint.RoofTile, Faces.Bottom);
                    m.Box(-hw, 0f, eaveZ - 0.06f, hw, eaveY + 0.04f, eaveZ, HousePaint.Wood, Faces.Back | Faces.Top); // fascia
                    m.Quad(new V3(-hw, eaveY, eaveZ - 0.06f), new V3(hw, eaveY, eaveZ - 0.06f), new V3(hw, eaveY, 0f), new V3(-hw, eaveY, 0f),
                        new V3(0, -1, 0), HousePaint.Wood); // soffit under the overhang
                    foreach (float sx in new[] { -w, w })
                    {
                        m.TriangleToward(new V3(sx, 0f, 0f), new V3(sx, ridgeY - 0.05f, ridgeZ), new V3(sx, 0f, -d), new V3(sx, 0, 0), HousePaint.Wall);
                    }

                    m.Quad(new V3(-w, 0f, 0f), new V3(w, 0f, 0f), new V3(w, eaveY, 0f), new V3(-w, eaveY, 0f), new V3(0, 0, 1), HousePaint.Wall);
                    break;
                }

                case "roof_terrace_canopy":
                {
                    Parapet(m, 0.45f, HousePaint.Wall);
                    Coping(m, 0.45f);
                    RailBars(m, -w + 0.1f, w - 0.1f, 0.5f, 0.95f, -0.12f, -0.08f, 0.14f, HousePaint.Accent);
                    m.Box(-w + 0.05f, 0.92f, -0.13f, w - 0.05f, 0.97f, -0.07f, HousePaint.Accent);

                    // A chhatri: four pillars, a slab and a dome, at the back left of the terrace.
                    const float cx = 2.5f, cz = -2.4f, half = 0.7f, post = 1.55f;
                    foreach (float x in new[] { cx - half, cx + half })
                    {
                        foreach (float z in new[] { cz - half, cz + half })
                        {
                            m.Box(x - 0.06f, 0f, z - 0.06f, x + 0.06f, post, z + 0.06f, HousePaint.Trim, Faces.Bottom | Faces.Top);
                        }
                    }

                    m.Box(cx - half - 0.15f, post, cz - half - 0.15f, cx + half + 0.15f, post + 0.12f, cz + half + 0.15f, HousePaint.Trim);
                    m.Dome(cx, post + 0.12f, cz, 0.5f, 12, 4, HousePaint.RoofTile);
                    break;
                }
            }
        }

        private static void Parapet(HouseMesh m, float height, HousePaint paint)
        {
            const float w = HouseGeometry.HalfWidth, d = HouseGeometry.Depth, t = 0.15f;
            m.Box(-w, 0f, -t, w, height, 0f, paint, Faces.Bottom);
            m.Box(-w, 0f, -d, -w + t, height, -t, paint, Faces.Bottom);
            m.Box(w - t, 0f, -d, w, height, -t, paint, Faces.Bottom);
            m.Box(-w + t, 0f, -d, w - t, height, -d + t, paint, Faces.Bottom);
        }

        private static void Coping(HouseMesh m, float height)
        {
            const float w = HouseGeometry.HalfWidth, d = HouseGeometry.Depth;
            m.Box(-w - 0.05f, height, -0.2f, w + 0.05f, height + 0.08f, 0.05f, HousePaint.Trim);
            m.Box(-w - 0.05f, height, -d, -w + 0.2f, height + 0.08f, -0.2f, HousePaint.Trim, Faces.Front);
            m.Box(w - 0.2f, height, -d, w + 0.05f, height + 0.08f, -0.2f, HousePaint.Trim, Faces.Front);
        }

        // ---- Boundary walls (house space) and the gate (local: the gate's centre in the back wall) ----

        private static void Boundary(HouseMesh m, string id)
        {
            const float c = HouseGeometry.BoundaryCentre, t = HouseGeometry.BoundaryThickness / 2f, back = HouseGeometry.BackWallZ;
            float gateLeft = HouseGeometry.GateX - 0.61f, gateRight = HouseGeometry.GateX + 0.61f;

            // Side walls from the house front to the back wall; the back wall either side of the gate.
            var walls = new[]
            {
                new Box3(c - t, 0f, 0f, c + t, 1f, back - t),
                new Box3(-c - t, 0f, 0f, -c + t, 1f, back - t),
                new Box3(-c - t, 0f, back - t, gateLeft, 1f, back + t),
                new Box3(gateRight, 0f, back - t, c + t, 1f, back + t),
            };

            switch (id)
            {
                case "boundary_plaster":
                case "boundary_brick":
                {
                    HousePaint body = id == "boundary_brick" ? HousePaint.Brick : HousePaint.Wall;
                    HousePaint top = id == "boundary_brick" ? HousePaint.Stone : HousePaint.Trim;
                    foreach (Box3 b in walls)
                    {
                        WallRun(m, b, HouseGeometry.BoundaryHeight, body, top);
                    }

                    break;
                }

                case "boundary_grill":
                {
                    const float low = 0.7f, rail = 1.45f, pillarTop = 1.6f;
                    foreach (Box3 b in walls)
                    {
                        WallRun(m, b, low, HousePaint.Wall, HousePaint.Trim);
                        bool alongZ = b.Max.Z - b.Min.Z > b.Max.X - b.Min.X;
                        float start = alongZ ? b.Min.Z : b.Min.X, end = alongZ ? b.Max.Z : b.Max.X, length = end - start;
                        int bays = Math.Max(1, (int)Math.Round(length / 1.5f));
                        for (int i = 0; i <= bays; i++)
                        {
                            float s = Math.Max(start, Math.Min(end - 0.3f, start + length * i / bays - 0.15f));
                            if (alongZ)
                            {
                                m.Box(b.Min.X, 0f, s, b.Max.X, pillarTop, s + 0.3f, HousePaint.Wall, Faces.Bottom);
                            }
                            else
                            {
                                m.Box(s, 0f, b.Min.Z, s + 0.3f, pillarTop, b.Max.Z, HousePaint.Wall, Faces.Bottom);
                            }
                        }

                        // The grille: a top rail and bars on the wall's centre line.
                        float cx = (b.Min.X + b.Max.X) / 2f, cz = (b.Min.Z + b.Max.Z) / 2f;
                        if (alongZ)
                        {
                            m.Box(cx - 0.025f, rail, start, cx + 0.025f, rail + 0.05f, end, HousePaint.Accent, Faces.Front | Faces.Back);
                            for (float z = start + 0.1f; z < end - 0.05f; z += 0.15f)
                            {
                                m.Box(cx - 0.012f, low + 0.06f, z - 0.012f, cx + 0.012f, rail, z + 0.012f, HousePaint.Accent, Faces.Top | Faces.Bottom);
                            }
                        }
                        else
                        {
                            m.Box(start, rail, cz - 0.025f, end, rail + 0.05f, cz + 0.025f, HousePaint.Accent, Faces.Left | Faces.Right);
                            for (float x = start + 0.1f; x < end - 0.05f; x += 0.15f)
                            {
                                m.Box(x - 0.012f, low + 0.06f, cz - 0.012f, x + 0.012f, rail, cz + 0.012f, HousePaint.Accent, Faces.Top | Faces.Bottom);
                            }
                        }
                    }

                    break;
                }
            }
        }

        private static void WallRun(HouseMesh m, Box3 b, float height, HousePaint body, HousePaint top)
        {
            m.Box(b.Min.X, 0f, b.Min.Z, b.Max.X, height, b.Max.Z, body, Faces.Bottom);

            // The coping overhangs across the wall only, so its ends stay flush with the gate pillars and the house.
            bool alongZ = b.Max.Z - b.Min.Z > b.Max.X - b.Min.X;
            float ox = alongZ ? 0.02f : 0f, oz = alongZ ? 0f : 0.02f;
            m.Box(b.Min.X - ox, height, b.Min.Z - oz, b.Max.X + ox, height + 0.06f, b.Max.Z + oz, top, Faces.Bottom);
        }

        private static void Gate(HouseMesh m, string id)
        {
            // Pillars either side of a 0.78 m opening, as deep as the wall plus a little.
            const float inner = 0.39f, outer = 0.61f, t = 0.2f;
            float pillar = id == "gate_arch" ? 1.95f : 1.9f;
            foreach (int side in new[] { -1, 1 })
            {
                float x0 = side * inner, x1 = side * outer;
                m.Box(x0, 0f, -t, x1, pillar, t, HousePaint.Wall, Faces.Bottom);
                if (id != "gate_arch")
                {
                    m.Box(x0, pillar, -t, x1, pillar + 0.08f, t, HousePaint.Trim, Faces.Bottom);
                    float mid = side * (inner + outer) / 2f;
                    m.Box(mid - 0.07f, pillar + 0.08f, -0.07f, mid + 0.07f, pillar + 0.2f, 0.07f, HousePaint.Trim, Faces.Bottom);
                }
            }

            switch (id)
            {
                case "gate_iron":
                    IronLeaves(m, inner, 1.25f, spears: true);
                    break;

                case "gate_wooden":
                    foreach (int side in new[] { -1, 1 })
                    {
                        float x0 = side * 0.005f, x1 = side * (inner - 0.005f);
                        m.Box(x0, 0.05f, -0.03f, x1, 1.35f, 0.03f, HousePaint.Accent);
                        for (int p = 1; p < 4; p++)
                        {
                            float x = x0 + (x1 - x0) * p / 4f;
                            m.Box(x - 0.006f, 0.08f, -0.035f, x + 0.006f, 1.32f, 0.035f, HousePaint.Dark, Faces.Top | Faces.Bottom);
                        }

                        foreach (float y in new[] { 0.3f, 1.05f })
                        {
                            m.Box(x0, y - 0.03f, -0.04f, x1, y + 0.03f, 0.04f, HousePaint.Iron);
                        }
                    }

                    break;

                case "gate_arch":
                    m.ArchBand(0f, pillar, inner, outer, -t, t, ArchSegments, HousePaint.Wall);
                    m.Box(-0.06f, pillar + inner - 0.02f, -t - 0.02f, 0.06f, pillar + outer, t + 0.02f, HousePaint.Trim); // keystone
                    IronLeaves(m, inner, 1.2f, spears: false);
                    break;
            }
        }

        private static void IronLeaves(HouseMesh m, float inner, float height, bool spears)
        {
            foreach (int side in new[] { -1, 1 })
            {
                float x0 = side * 0.005f, x1 = side * (inner - 0.005f);
                foreach (float y in new[] { 0.06f, height * 0.5f, height })
                {
                    m.Box(x0, y - 0.025f, -0.02f, x1, y + 0.025f, 0.02f, HousePaint.Accent);
                }

                for (int b = 0; b <= 4; b++)
                {
                    float x = x0 + (x1 - x0) * b / 4f;
                    x = side < 0 ? Math.Min(-0.015f, Math.Max(-inner + 0.015f, x)) : Math.Max(0.015f, Math.Min(inner - 0.015f, x));
                    m.Box(x - 0.012f, 0.06f, -0.012f, x + 0.012f, height + (spears ? 0.12f : 0f), 0.012f, HousePaint.Accent, Faces.Bottom);
                    if (spears)
                    {
                        m.Box(x - 0.025f, height + 0.08f, -0.02f, x + 0.025f, height + 0.11f, 0.02f, HousePaint.Accent);
                    }
                }
            }
        }

        // ---- Shared parts ----

        /// <summary>Jambs and head of an opening frame; the frame laps 8 cm onto the wall face.</summary>
        private static void Frame(HouseMesh m, float w, float h, HousePaint paint, bool sill)
        {
            Jambs(m, w, h, paint);
            float hw = w / 2f;
            m.Box(-hw - 0.08f, h, -0.06f, hw + 0.08f, h + 0.1f, 0.06f, paint, Faces.Back);
            if (sill)
            {
                m.Box(-hw - 0.12f, -0.08f, -0.06f, hw + 0.12f, 0f, 0.12f, HousePaint.Stone, Faces.Back);
            }
        }

        private static void Jambs(HouseMesh m, float w, float h, HousePaint paint)
        {
            float hw = w / 2f;
            m.Box(-hw - 0.08f, 0f, -0.06f, -hw + 0.03f, h, 0.06f, paint, Faces.Back | Faces.Bottom);
            m.Box(hw - 0.03f, 0f, -0.06f, hw + 0.08f, h, 0.06f, paint, Faces.Back | Faces.Bottom);
        }

        private static void Glazing(HouseMesh m, float w, float h) => m.Box(-w / 2f, 0f, -0.12f, w / 2f, h, -0.1f, HousePaint.Glass, Faces.Back);

        /// <summary>Vertical bars between a bottom and a top rail across x0..x1, in the slab z0..z1.</summary>
        private static void RailBars(HouseMesh m, float x0, float x1, float y0, float y1, float z0, float z1, float spacing, HousePaint paint)
        {
            float zc = (z0 + z1) / 2f;
            m.Box(x0, y1 - 0.03f, z0, x1, y1, z1, paint);
            m.Box(x0, y0, z0, x1, y0 + 0.03f, z1, paint);
            int bars = Math.Max(1, (int)Math.Round((x1 - x0) / spacing));
            for (int i = 0; i <= bars; i++)
            {
                float x = x0 + 0.012f + (x1 - x0 - 0.024f) * i / bars;
                m.Box(x - 0.012f, y0 + 0.03f, zc - 0.012f, x + 0.012f, y1 - 0.03f, zc + 0.012f, paint, Faces.Top | Faces.Bottom);
            }
        }

        private static void Paint(HouseMesh m, Box3 b, HousePaint paint, Faces skip) =>
            m.Box(b.Min.X, b.Min.Y, b.Min.Z, b.Max.X, b.Max.Y, b.Max.Z, paint, skip);
    }
}

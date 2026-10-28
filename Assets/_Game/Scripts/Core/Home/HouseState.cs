using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace UtsavNights.Core.Home
{
    /// <summary>The module at one attachment point. An empty module id leaves an optional point bare.</summary>
    [Serializable]
    public sealed class ModuleChoice
    {
        public string point = string.Empty;
        public string module = string.Empty;
        public int variant;

        public ModuleChoice()
        {
        }

        public ModuleChoice(string point, string module, int variant = 0)
        {
            this.point = point;
            this.module = module;
            this.variant = variant;
        }

        public ModuleChoice Clone() => new ModuleChoice(point, module, variant);
    }

    /// <summary>
    /// One placed decoration in house space (metres, y up, the door at the origin facing +z). Path items run from
    /// (x, y, z) to (ex, ey, ez) and hang by <see cref="sag"/>; free and decal items ignore the end point.
    /// </summary>
    [Serializable]
    public sealed class PlacedDecor
    {
        public int uid;
        public string item = string.Empty;
        public int variant;
        public float x;
        public float y;
        public float z;

        /// <summary>Degrees about the vertical (free items) or the surface normal (rangoli).</summary>
        public float yaw;
        public float scale = 1f;
        public Surface surface = Surface.Ground;
        public float ex;
        public float ey;
        public float ez;
        public float sag;

        public PlacedDecor Clone() => (PlacedDecor)MemberwiseClone();

        public void CopyPoseFrom(PlacedDecor other)
        {
            x = other.x;
            y = other.y;
            z = other.z;
            yaw = other.yaw;
            scale = other.scale;
            surface = other.surface;
            ex = other.ex;
            ey = other.ey;
            ez = other.ez;
            sag = other.sag;
            variant = other.variant;
        }

        public string Fingerprint() => string.Join(",", uid.ToString(CultureInfo.InvariantCulture), item, Num(variant), Num(x), Num(y), Num(z),
            Num(yaw), Num(scale), Num((int)surface), Num(ex), Num(ey), Num(ez), Num(sag));

        private static string Num(float v) => v.ToString("R", CultureInfo.InvariantCulture);

        private static string Num(int v) => v.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Everything the player edits: modules, paint and decorations. Layouts store copies of this.</summary>
    [Serializable]
    public sealed class HouseDesign
    {
        public string kitId = HomeCatalog.KitId;
        public List<ModuleChoice> modules = new List<ModuleChoice>();
        public int wallColour;
        public int trimColour;
        public int material;
        public List<PlacedDecor> decorations = new List<PlacedDecor>();

        /// <summary>The uid the next placed decoration gets; uids are never reused within a design.</summary>
        public int nextUid = 1;

        public static HouseDesign Default()
        {
            var d = new HouseDesign();
            foreach (AttachmentPoint p in HomeCatalog.Points)
            {
                d.modules.Add(new ModuleChoice(p.Id, p.DefaultModule));
            }

            return d;
        }

        public HouseDesign Clone()
        {
            var copy = (HouseDesign)MemberwiseClone();
            copy.modules = new List<ModuleChoice>();
            foreach (ModuleChoice m in modules)
            {
                if (m != null)
                {
                    copy.modules.Add(m.Clone());
                }
            }

            copy.decorations = new List<PlacedDecor>();
            foreach (PlacedDecor p in decorations)
            {
                if (p != null)
                {
                    copy.decorations.Add(p.Clone());
                }
            }

            return copy;
        }

        public ModuleChoice ModuleAt(string point) => modules.Find(m => m.point == point);

        public PlacedDecor Find(int uid) => decorations.Find(d => d.uid == uid);

        public int IndexOf(int uid) => decorations.FindIndex(d => d.uid == uid);

        /// <summary>A stable text of every edited value, for exact undo checks and change detection.</summary>
        public string Fingerprint()
        {
            var sb = new StringBuilder();
            sb.Append(kitId).Append('|').Append(wallColour).Append('|').Append(trimColour).Append('|').Append(material).Append('|').Append(nextUid);
            foreach (ModuleChoice m in modules ?? new List<ModuleChoice>())
            {
                sb.Append("|m:").Append(m?.point).Append('=').Append(m?.module).Append(':').Append(m?.variant);
            }

            foreach (PlacedDecor p in decorations ?? new List<PlacedDecor>())
            {
                sb.Append("|d:").Append(p?.Fingerprint());
            }

            return sb.ToString();
        }

        /// <summary>
        /// Repairs a design from an old, edited or corrupt save: unknown modules and items are dropped, every point gets a
        /// module of its type, numbers are clamped and uids made unique. Returns true when anything changed.
        /// </summary>
        public bool Sanitize()
        {
            string before = Fingerprint();
            kitId = HomeCatalog.KitId;
            modules ??= new List<ModuleChoice>();
            decorations ??= new List<PlacedDecor>();
            wallColour = ClampInt(wallColour, 0, HomeCatalog.WallColours.Length - 1);
            trimColour = ClampInt(trimColour, 0, HomeCatalog.TrimColours.Length - 1);
            material = ClampInt(material, 0, HomeCatalog.Materials.Length - 1);

            var repaired = new List<ModuleChoice>();
            foreach (AttachmentPoint p in HomeCatalog.Points)
            {
                ModuleChoice saved = modules.Find(m => m != null && m.point == p.Id);
                ModuleInfo info = HomeCatalog.Module(saved?.module);
                if (saved != null && string.IsNullOrEmpty(saved.module) && p.Optional)
                {
                    repaired.Add(new ModuleChoice(p.Id, string.Empty));
                }
                else if (info != null && info.Type == p.Type)
                {
                    repaired.Add(new ModuleChoice(p.Id, info.Id, ClampInt(saved.variant, 0, info.Variants - 1)));
                }
                else
                {
                    repaired.Add(new ModuleChoice(p.Id, p.DefaultModule));
                }
            }

            modules = repaired;

            var seen = new HashSet<int>();
            int maxUid = 0;
            decorations.RemoveAll(d => d == null || HomeCatalog.Decoration(d.item) == null || !Finite(d) || d.uid <= 0 || !seen.Add(d.uid));
            foreach (PlacedDecor d in decorations)
            {
                DecorInfo info = HomeCatalog.Decoration(d.item);
                d.variant = ClampInt(d.variant, 0, info.Variants - 1);
                d.scale = Math.Max(HomeCatalog.MinScale, Math.Min(info.MaxScale, d.scale));
                d.yaw = NormaliseYaw(d.yaw);
                d.sag = Math.Max(0f, Math.Min(1f, d.sag));
                if ((d.surface & info.Surfaces) == 0 || !SingleFlag(d.surface))
                {
                    d.surface = LowestFlag(info.Surfaces);
                }

                maxUid = Math.Max(maxUid, d.uid);
            }

            nextUid = Math.Max(nextUid, maxUid + 1);
            return Fingerprint() != before;
        }

        public static float NormaliseYaw(float yaw)
        {
            float y = yaw % 360f;
            return y < 0f ? y + 360f : y;
        }

        private static bool Finite(PlacedDecor d) => Ok(d.x) && Ok(d.y) && Ok(d.z) && Ok(d.yaw) && Ok(d.scale) && Ok(d.ex) && Ok(d.ey) && Ok(d.ez) && Ok(d.sag);

        private static bool Ok(float v) => !float.IsNaN(v) && !float.IsInfinity(v) && Math.Abs(v) < 1000f;

        private static bool SingleFlag(Surface s) => s != Surface.None && ((int)s & ((int)s - 1)) == 0;

        private static Surface LowestFlag(Surface s) => (Surface)((int)s & -(int)s);

        private static int ClampInt(int v, int min, int max) => Math.Max(min, Math.Min(max, v));
    }

    [Serializable]
    public sealed class HouseLayout
    {
        public string name = string.Empty;
        public HouseDesign design = new HouseDesign();
    }

    /// <summary>
    /// house.json (PRD 13.5): the design being shown, the saved named layouts and a save serial. The serial ties the
    /// edit journal to the save it started from, so a journal left over from before the last save is never replayed twice.
    /// </summary>
    [Serializable]
    public sealed class HouseState
    {
        public const int CurrentVersion = 1;

        /// <summary>PRD 9.1: three named layouts free.</summary>
        public const int FreeLayouts = 3;
        public const int MaxLayoutName = 24;

        public int version = CurrentVersion;
        public int saveSerial;
        public HouseDesign design = HouseDesign.Default();
        public List<HouseLayout> layouts = new List<HouseLayout>();

        public static HouseState Fresh() => new HouseState();

        public HouseState Clone()
        {
            var copy = new HouseState { version = version, saveSerial = saveSerial, design = design.Clone() };
            foreach (HouseLayout l in layouts)
            {
                copy.layouts.Add(new HouseLayout { name = l.name, design = l.design.Clone() });
            }

            return copy;
        }

        /// <summary>
        /// Older saves to the current version. Version 0 (an unversioned file from a pre-release build) had no layouts and
        /// no uids, so every decoration gets one. Returns true when anything changed.
        /// </summary>
        public bool Migrate()
        {
            if (version >= CurrentVersion)
            {
                return false;
            }

            if (version < 1)
            {
                design ??= HouseDesign.Default();
                design.decorations ??= new List<PlacedDecor>();
                int uid = 1;
                foreach (PlacedDecor d in design.decorations)
                {
                    if (d != null)
                    {
                        d.uid = uid++;
                    }
                }

                design.nextUid = uid;
                layouts = new List<HouseLayout>();
            }

            version = CurrentVersion;
            return true;
        }

        public bool Sanitize(int layoutSlots = FreeLayouts)
        {
            bool changed = false;
            if (design == null)
            {
                design = HouseDesign.Default();
                changed = true;
            }

            changed |= design.Sanitize();
            layouts ??= new List<HouseLayout>();
            int before = layouts.Count;
            layouts.RemoveAll(l => l == null || l.design == null);
            if (layouts.Count > layoutSlots)
            {
                layouts.RemoveRange(layoutSlots, layouts.Count - layoutSlots);
            }

            changed |= layouts.Count != before;
            for (int i = 0; i < layouts.Count; i++)
            {
                string clean = CleanLayoutName(layouts[i].name, i);
                changed |= clean != layouts[i].name;
                layouts[i].name = clean;
                changed |= layouts[i].design.Sanitize();
            }

            return changed;
        }

        /// <summary>Saves the current design into a layout slot (a new slot when <paramref name="slot"/> is past the end).</summary>
        public bool SaveLayout(int slot, string name, int layoutSlots = FreeLayouts)
        {
            if (slot < 0 || slot >= layoutSlots || slot > layouts.Count)
            {
                return false;
            }

            var layout = new HouseLayout { name = CleanLayoutName(name, slot), design = design.Clone() };
            if (slot == layouts.Count)
            {
                layouts.Add(layout);
            }
            else
            {
                layouts[slot] = layout;
            }

            return true;
        }

        public bool DeleteLayout(int slot)
        {
            if (slot < 0 || slot >= layouts.Count)
            {
                return false;
            }

            layouts.RemoveAt(slot);
            return true;
        }

        /// <summary>Trimmed and at most <see cref="MaxLayoutName"/> characters; blank names become "Layout N".</summary>
        public static string CleanLayoutName(string raw, int slot)
        {
            string name = (raw ?? string.Empty).Trim();
            var sb = new StringBuilder(MaxLayoutName);
            foreach (char c in name)
            {
                if (sb.Length >= MaxLayoutName)
                {
                    break;
                }

                if (!char.IsControl(c))
                {
                    sb.Append(c);
                }
            }

            name = sb.ToString().Trim();
            return name.Length == 0 ? "Layout " + (slot + 1).ToString(CultureInfo.InvariantCulture) : name;
        }
    }
}

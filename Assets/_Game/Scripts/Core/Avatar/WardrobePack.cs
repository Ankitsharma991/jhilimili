using System;
using System.Collections.Generic;

namespace UtsavNights.Core.Avatar
{
    /// <summary>One garment in a pack manifest. Enum values are written by name, so a manifest reads as plain text.</summary>
    [Serializable]
    public sealed class PackGarment
    {
        public string id = string.Empty;
        public string name = string.Empty;

        /// <summary><see cref="WardrobeSlot"/> names, for example ["BaseTop"] or ["BaseTop", "BaseBottom"] for a robe.</summary>
        public string[] slots = Array.Empty<string>();

        /// <summary><see cref="BodyRegion"/> names hidden under it (the skin mask).</summary>
        public string[] covers = Array.Empty<string>();

        public string[] occasion = Array.Empty<string>();
        public string[] inspiration = Array.Empty<string>();
        public int variants = 1;
        public bool hidesHair;

        /// <summary>"generator:&lt;id&gt;" for a procedural garment, or "asset:&lt;bundle path&gt;" for a modelled one (Phase 9).</summary>
        public string mesh = string.Empty;

        public int vertices;

        /// <summary>Who reviewed it and when (PRD 8: cultural review before release).</summary>
        public string culturalReview = string.Empty;
    }

    /// <summary>A downloadable wardrobe pack manifest (feeds Phase 9 content packs; PRD 8 and 13).</summary>
    [Serializable]
    public sealed class WardrobePackManifest
    {
        public string id = string.Empty;
        public int version = 1;
        public string title = string.Empty;

        /// <summary>A row id in Docs/LICENCE_LOG.md.</summary>
        public string licence = string.Empty;

        public PackGarment[] garments = Array.Empty<PackGarment>();
    }

    /// <summary>
    /// Validates wardrobe packs (Phase 6 step 7) and turns them into catalogue entries. Enforces PRD 8: garments are tagged by
    /// occasion and inspiration and never locked by gender or region; every garment fits the shared slots and skin mask,
    /// stays within the per-garment vertex budget, names its mesh source, and records a cultural review.
    /// </summary>
    public static class WardrobePacks
    {
        public const int MaxVertices = 3000;
        public const int MaxVariants = 8;

        private static readonly string[] Locks = { "men", "women", "male", "female", "boys", "girls", "gender", "unisex", "only" };

        public static List<string> Validate(WardrobePackManifest pack, IEnumerable<string> existingIds)
        {
            var errors = new List<string>();
            if (pack == null)
            {
                errors.Add("no manifest");
                return errors;
            }

            if (!IsId(pack.id))
            {
                errors.Add($"pack id \"{pack.id}\" must be lowercase letters, digits and underscores");
            }

            if (pack.version < 1)
            {
                errors.Add("version must be 1 or more");
            }

            if (string.IsNullOrWhiteSpace(pack.licence))
            {
                errors.Add("licence row id missing (Docs/LICENCE_LOG.md)");
            }

            var seen = new HashSet<string>(existingIds ?? Array.Empty<string>());
            foreach (PackGarment g in pack.garments ?? Array.Empty<PackGarment>())
            {
                string at = $"garment \"{g?.id}\"";
                if (g == null)
                {
                    errors.Add("empty garment entry");
                    continue;
                }

                if (!IsId(g.id))
                {
                    errors.Add($"{at}: id must be lowercase letters, digits and underscores");
                }
                else if (!seen.Add(g.id))
                {
                    errors.Add($"{at}: id already used");
                }

                if (string.IsNullOrWhiteSpace(g.name))
                {
                    errors.Add($"{at}: name missing");
                }

                foreach (string word in Words(g.name))
                {
                    if (Array.IndexOf(Locks, word) >= 0)
                    {
                        errors.Add($"{at}: \"{word}\" in the name; garments are open to every body (PRD 8)");
                    }
                }

                if (Parse<WardrobeSlot>(g.slots, at, "slot", errors) == WardrobeSlot.None)
                {
                    errors.Add($"{at}: needs at least one slot");
                }

                Parse<BodyRegion>(g.covers, at, "body region", errors);
                if (Parse<Occasion>(g.occasion, at, "occasion", errors) == Occasion.None)
                {
                    errors.Add($"{at}: needs at least one occasion tag");
                }

                if (Parse<Inspiration>(g.inspiration, at, "inspiration", errors) == Inspiration.None)
                {
                    errors.Add($"{at}: needs at least one inspiration tag");
                }

                if (g.variants < 1 || g.variants > MaxVariants)
                {
                    errors.Add($"{at}: variants must be 1 to {MaxVariants}");
                }

                if (!(g.mesh ?? string.Empty).StartsWith("generator:", StringComparison.Ordinal) && !(g.mesh ?? string.Empty).StartsWith("asset:", StringComparison.Ordinal))
                {
                    errors.Add($"{at}: mesh must be \"generator:<id>\" or \"asset:<path>\"");
                }

                if (g.vertices <= 0 || g.vertices > MaxVertices)
                {
                    errors.Add($"{at}: vertices must be 1 to {MaxVertices} (low-tier budget)");
                }

                if (string.IsNullOrWhiteSpace(g.culturalReview))
                {
                    errors.Add($"{at}: cultural review missing (PRD 8)");
                }
            }

            return errors;
        }

        /// <summary>Catalogue entries for a valid pack (call <see cref="Validate"/> first).</summary>
        public static List<GarmentInfo> ToGarments(WardrobePackManifest pack)
        {
            var list = new List<GarmentInfo>();
            var ignore = new List<string>();
            foreach (PackGarment g in pack.garments)
            {
                list.Add(new GarmentInfo(g.id, g.name, Parse<WardrobeSlot>(g.slots, g.id, "slot", ignore), Parse<BodyRegion>(g.covers, g.id, "body region", ignore),
                    Parse<Occasion>(g.occasion, g.id, "occasion", ignore), Parse<Inspiration>(g.inspiration, g.id, "inspiration", ignore), g.variants, g.hidesHair));
            }

            return list;
        }

        /// <summary>The built-in wardrobe as a manifest: the reference example of the format.</summary>
        public static WardrobePackManifest FromCatalog(WardrobeCatalog catalog, string id, string licence)
        {
            var garments = new List<PackGarment>();
            foreach (GarmentInfo g in catalog.All)
            {
                MeshData mesh = GarmentGenerator.Build(g.Id).Skinned ?? GarmentGenerator.Build(g.Id).HeadRigid;
                garments.Add(new PackGarment
                {
                    id = g.Id, name = g.Name, slots = Names(g.Slots), covers = Names(g.Covers), occasion = Names(g.Occasion), inspiration = Names(g.Inspiration),
                    variants = g.Variants, hidesHair = g.HidesHair, mesh = "generator:" + g.Id, vertices = mesh.VertexCount, culturalReview = "pending",
                });
            }

            return new WardrobePackManifest { id = id, version = 1, title = "Built-in wardrobe", licence = licence, garments = garments.ToArray() };
        }

        private static T Parse<T>(string[] names, string at, string what, List<string> errors) where T : struct, Enum
        {
            long value = 0;
            foreach (string n in names ?? Array.Empty<string>())
            {
                if (Enum.TryParse(n, ignoreCase: false, out T parsed) && Convert.ToInt64(parsed) != 0)
                {
                    value |= Convert.ToInt64(parsed);
                }
                else
                {
                    errors.Add($"{at}: unknown {what} \"{n}\"");
                }
            }

            return (T)Enum.ToObject(typeof(T), value);
        }

        private static string[] Names<T>(T flags) where T : struct, Enum
        {
            var names = new List<string>();
            long value = Convert.ToInt64(flags);
            foreach (T v in (T[])Enum.GetValues(typeof(T)))
            {
                long bit = Convert.ToInt64(v);
                // Single-bit values only (skip None and the combined names such as Torso = Chest | Belly).
                if (bit != 0 && (bit & (bit - 1)) == 0 && (value & bit) == bit)
                {
                    names.Add(v.ToString());
                }
            }

            return names.ToArray();
        }

        private static bool IsId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 40)
            {
                return false;
            }

            foreach (char c in id)
            {
                if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '_')
                {
                    return false;
                }
            }

            return true;
        }

        private static IEnumerable<string> Words(string text)
        {
            foreach (string w in (text ?? string.Empty).ToLowerInvariant().Split(' ', '-', '\'', '(', ')', ',', '/'))
            {
                if (w.Length > 0)
                {
                    yield return w;
                }
            }
        }
    }
}

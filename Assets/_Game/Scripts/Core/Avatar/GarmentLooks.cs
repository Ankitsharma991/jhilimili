using UtsavNights.Core.Art;

namespace UtsavNights.Core.Avatar
{
    /// <summary>
    /// Colour variants for every garment (Phase 6 step 3): a festival palette of saturated jewel tones and everyday neutrals,
    /// with a trim colour (borders, buttons, embroidery, soles) that suits each.
    /// </summary>
    public static class GarmentLooks
    {
        public static readonly Rgb Maroon = new Rgb(0.45f, 0.06f, 0.1f);
        public static readonly Rgb Saffron = new Rgb(0.95f, 0.52f, 0.12f);
        public static readonly Rgb Marigold = new Rgb(0.98f, 0.74f, 0.15f);
        public static readonly Rgb Emerald = new Rgb(0.05f, 0.45f, 0.28f);
        public static readonly Rgb Teal = new Rgb(0.05f, 0.42f, 0.45f);
        public static readonly Rgb Royal = new Rgb(0.12f, 0.2f, 0.62f);
        public static readonly Rgb Indigo = new Rgb(0.16f, 0.1f, 0.38f);
        public static readonly Rgb Magenta = new Rgb(0.78f, 0.12f, 0.48f);
        public static readonly Rgb Rose = new Rgb(0.9f, 0.5f, 0.58f);
        public static readonly Rgb Ivory = new Rgb(0.93f, 0.9f, 0.82f);
        public static readonly Rgb Charcoal = new Rgb(0.16f, 0.16f, 0.18f);
        public static readonly Rgb Beige = new Rgb(0.76f, 0.66f, 0.52f);
        public static readonly Rgb Denim = new Rgb(0.2f, 0.3f, 0.48f);
        public static readonly Rgb Black = new Rgb(0.05f, 0.05f, 0.06f);
        public static readonly Rgb White = new Rgb(0.92f, 0.92f, 0.9f);
        public static readonly Rgb Gold = new Rgb(0.85f, 0.65f, 0.2f);
        public static readonly Rgb Silver = new Rgb(0.72f, 0.73f, 0.76f);
        public static readonly Rgb Olive = new Rgb(0.36f, 0.38f, 0.18f);
        public static readonly Rgb Brown = new Rgb(0.36f, 0.22f, 0.12f);
        public static readonly Rgb Red = new Rgb(0.75f, 0.1f, 0.1f);

        public static Rgb Main(string id, int variant)
        {
            Rgb[] set = Set(id);
            return set[((variant % set.Length) + set.Length) % set.Length];
        }

        public static Rgb Trim(string id, int variant)
        {
            switch (id)
            {
                case "tshirt": case "hoodie": case "cap": case "sneakers":
                    return White;
                case "jeans": case "trousers": case "shorts": case "formal_shoes": case "shirt": case "blazer":
                    return Charcoal;
                case "abaya": case "thobe":
                    return id == "abaya" ? Charcoal : Ivory;
                case "ghutra":
                    return Black;
                case "watch":
                    return White;
                case "bangles": case "necklace": case "earrings":
                    return variant % 2 == 0 ? Red : Emerald;
                default:
                    Rgb main = Main(id, variant);
                    return 0.3f * main.R + 0.59f * main.G + 0.11f * main.B > 0.55f ? Maroon : Gold;
            }
        }

        private static Rgb[] Set(string id)
        {
            switch (id)
            {
                case "tshirt": case "hoodie": case "cap":
                    return new[] { Charcoal, Royal, Red, Emerald, Ivory, Marigold };
                case "shirt": return new[] { White, Rose, new Rgb(0.6f, 0.75f, 0.9f), Beige, Charcoal };
                case "jeans": return new[] { Denim, Charcoal, new Rgb(0.45f, 0.55f, 0.7f) };
                case "trousers": return new[] { Charcoal, Beige, Indigo, Olive };
                case "shorts": return new[] { Beige, Olive, Denim, Charcoal };
                case "blazer": return new[] { Charcoal, Indigo, Maroon, Beige };
                case "abaya": return new[] { Black, Indigo, Charcoal, Brown };
                case "thobe": return new[] { White, Beige, new Rgb(0.6f, 0.62f, 0.66f), Brown };
                case "ghutra": return new[] { White, Red, new Rgb(0.88f, 0.86f, 0.8f) };
                case "hijab": return new[] { Black, Rose, Teal, Ivory, Maroon, Indigo };
                case "sneakers": return new[] { White, Charcoal, Red, Royal, Marigold };
                case "formal_shoes": return new[] { Black, Brown, new Rgb(0.45f, 0.25f, 0.12f) };
                case "chappals": return new[] { Brown, Black, Gold, Maroon };
                case "juttis": return new[] { Gold, Maroon, Emerald, Magenta };
                case "watch": return new[] { Silver, Gold, Black };
                case "bangles": case "necklace": case "earrings": return new[] { Gold, Silver, Gold, Red };
                case "dhaka_topi": return new[] { new Rgb(0.6f, 0.2f, 0.15f), Royal, Charcoal };
                case "dupatta": case "lehenga": case "sari_nivi": case "sari_seedha":
                    return new[] { Maroon, Magenta, Emerald, Royal, Saffron, Rose };
                case "dhoti": case "daura": case "suruwal": return new[] { Ivory, Beige, White, Charcoal };
                default:
                    return new[] { Maroon, Saffron, Emerald, Royal, Magenta, Ivory };
            }
        }
    }
}

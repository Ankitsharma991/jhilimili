using System;
using System.Collections.Generic;
using UtsavNights.Core.Art;

namespace UtsavNights.Core.Avatar
{
    /// <summary>
    /// A continuous skin-tone ramp (PRD 8: "wide continuous skin tone"): eleven anchors from very light to very deep,
    /// warm and neutral undertones, interpolated in between so every value from 0 to 1 is a valid tone.
    /// </summary>
    public static class SkinTones
    {
        private static readonly Rgb[] Anchors =
        {
            new Rgb(0.98f, 0.87f, 0.79f),
            new Rgb(0.95f, 0.80f, 0.69f),
            new Rgb(0.90f, 0.73f, 0.60f),
            new Rgb(0.84f, 0.65f, 0.50f),
            new Rgb(0.77f, 0.57f, 0.42f),
            new Rgb(0.69f, 0.49f, 0.35f),
            new Rgb(0.60f, 0.41f, 0.28f),
            new Rgb(0.50f, 0.33f, 0.22f),
            new Rgb(0.41f, 0.27f, 0.18f),
            new Rgb(0.32f, 0.21f, 0.15f),
            new Rgb(0.24f, 0.16f, 0.12f),
        };

        public static int AnchorCount => Anchors.Length;

        public static Rgb Evaluate(float t)
        {
            t = Math.Max(0f, Math.Min(1f, float.IsNaN(t) ? 0.5f : t));
            float scaled = t * (Anchors.Length - 1);
            int i = Math.Min(Anchors.Length - 2, (int)scaled);
            return Rgb.Lerp(Anchors[i], Anchors[i + 1], scaled - i);
        }
    }

    /// <summary>Hair colours: natural blacks and browns, greys and white, and two dyed shades.</summary>
    public static class HairColours
    {
        public static readonly Rgb[] All =
        {
            new Rgb(0.05f, 0.04f, 0.04f),
            new Rgb(0.14f, 0.09f, 0.06f),
            new Rgb(0.30f, 0.18f, 0.10f),
            new Rgb(0.50f, 0.32f, 0.18f),
            new Rgb(0.55f, 0.55f, 0.55f),
            new Rgb(0.88f, 0.87f, 0.84f),
            new Rgb(0.55f, 0.16f, 0.08f),
            new Rgb(0.20f, 0.18f, 0.45f),
        };
    }

    /// <summary>
    /// Ready-made avatars for the creator's first page and for Skip (PRD 8: presets are also the skip path). A spread
    /// of body styles, heights, skin tones and outfits from every inspiration; none is labelled by gender or region.
    /// </summary>
    public static class AvatarPresets
    {
        public static IReadOnlyList<AvatarProfile> All { get; } = Build();

        /// <summary>The preset used when the player skips the creator.</summary>
        public static AvatarProfile Default => All[0].Clone();

        private static List<AvatarProfile> Build()
        {
            var list = new List<AvatarProfile>
            {
                Make(1.0f, HeightPreset.Medium, 0.55f, 3, 0, 1, "kurta:1", "churidar:0", "nehru_jacket:2", "juttis:0"),
                Make(0.6f, HeightPreset.Short, 0.35f, 6, 0, 0, "choli:2", "sari_nivi:1", "chappals:1", "bangles:0", "earrings:1"),
                Make(1.6f, HeightPreset.Tall, 0.75f, 2, 2, 0, "daura:0", "suruwal:0", "dhaka_topi:1", "formal_shoes:0"),
                Make(1.2f, HeightPreset.Medium, 0.45f, 8, 0, 3, "choli:0", "lehenga:3", "dupatta:3", "juttis:2", "necklace:0"),
                Make(0.4f, HeightPreset.Tall, 0.9f, 0, 1, 0, "tshirt:2", "jeans:0", "hoodie:1", "sneakers:2", "cap:0"),
                Make(1.8f, HeightPreset.Medium, 0.6f, 1, 3, 0, "kurta:4", "dhoti:0", "shawl:2", "chappals:0", "turban:2"),
                Make(1.1f, HeightPreset.Short, 0.2f, 5, 0, 0, "abaya:1", "hijab:3", "sneakers:0", "watch:0"),
                Make(1.4f, HeightPreset.Tall, 0.4f, 4, 2, 0, "thobe:0", "ghutra:0", "chappals:2"),
                Make(0.8f, HeightPreset.Medium, 0.15f, 7, 0, 2, "shirt:1", "trousers:2", "blazer:0", "formal_shoes:1", "watch:1"),
                Make(1.0f, HeightPreset.Medium, 0.65f, 3, 4, 0, "kurta:0", "churidar:1", "sherwani:3", "turban:0", "juttis:1"),
                Make(0.9f, HeightPreset.Short, 0.85f, 9, 0, 1, "short_kurta:3", "salwar:2", "dupatta:0", "chappals:3", "earrings:0"),
                Make(1.3f, HeightPreset.Medium, 1.0f, 2, 0, 0, "tshirt:4", "long_skirt:2", "sneakers:1", "bangles:2"),
            };

            for (int i = 0; i < list.Count; i++)
            {
                list[i].created = true;
                list[i].faceShape = i % AvatarOptions.FaceShapes;
                list[i].eyes = i % AvatarOptions.EyeShapes;
                list[i].nose = (i * 3) % AvatarOptions.Noses;
                list[i].mouth = (i * 2) % AvatarOptions.Mouths;
                list[i].brows = (i + 1) % AvatarOptions.Brows;
                list[i].animationStyle = i % 3 == 1 ? AnimationStyle.Lively : AnimationStyle.Calm;
                list[i].Sanitize(WardrobeCatalog.Default);
            }

            return list;
        }

        private static AvatarProfile Make(float body, HeightPreset height, float skin, int hairStyle, int facialHair, int glasses, params string[] items)
        {
            var p = new AvatarProfile
            {
                bodyStyle = body,
                height = height,
                skinTone = skin,
                hairStyle = hairStyle,
                facialHair = facialHair,
                glasses = glasses,
                hairColour = (hairStyle * 3) % 4,
            };
            foreach (string item in items)
            {
                string[] parts = item.Split(':');
                p.outfit.Add(new EquippedItem(parts[0], int.Parse(parts[1])));
            }

            return p;
        }
    }
}

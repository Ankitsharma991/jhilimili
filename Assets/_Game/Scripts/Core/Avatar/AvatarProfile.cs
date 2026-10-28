using System;
using System.Collections.Generic;
using System.Text;

namespace UtsavNights.Core.Avatar
{
    public enum HeightPreset
    {
        Short = 0,
        Medium = 1,
        Tall = 2,
    }

    /// <summary>How the avatar moves in idle, ignite and celebrate animations.</summary>
    public enum AnimationStyle
    {
        Calm = 0,
        Lively = 1,
    }

    /// <summary>One worn garment and its colour variant.</summary>
    [Serializable]
    public sealed class EquippedItem
    {
        public string id = string.Empty;
        public int variant;

        public EquippedItem()
        {
        }

        public EquippedItem(string id, int variant = 0)
        {
            this.id = id;
            this.variant = variant;
        }
    }

    /// <summary>
    /// The player's avatar (PRD 8, 13.5 profile.json). Every option is independent: body style never decides
    /// clothing or hair, and there is no gender or region field. Sliders run -1 to 1 (0 is average); body style runs
    /// 0 (slim) to 2 (broad), blending the three body-shape meshes. The optional name stays on the phone.
    /// </summary>
    [Serializable]
    public sealed class AvatarProfile
    {
        public const int CurrentVersion = 1;
        public const int MaxNameLength = 20;

        public int version = CurrentVersion;

        /// <summary>False until the player saves from the creator or picks a preset (Skip keeps the default).</summary>
        public bool created;

        public string name = string.Empty;

        // Body
        public float bodyStyle = 1f;
        public float shoulders;
        public float hips;
        public float legLength;
        public float headSize;
        public HeightPreset height = HeightPreset.Medium;

        /// <summary>0 (lightest) to 1 (deepest) along <see cref="SkinTones"/>; continuous, not a few buckets.</summary>
        public float skinTone = 0.5f;

        // Face
        public int faceShape;
        public int eyes;
        public int eyeColour;
        public int nose;
        public int mouth;
        public int brows;
        public int facialHair;
        public int glasses;

        // Hair
        public int hairStyle = 1;
        public int hairColour;

        public AnimationStyle animationStyle = AnimationStyle.Calm;

        public List<EquippedItem> outfit = new List<EquippedItem>();

        public AvatarProfile Clone()
        {
            var copy = (AvatarProfile)MemberwiseClone();
            copy.outfit = new List<EquippedItem>();
            foreach (EquippedItem item in outfit)
            {
                copy.outfit.Add(new EquippedItem(item.id, item.variant));
            }

            return copy;
        }

        /// <summary>Older saves to the current version. Returns true when anything changed.</summary>
        public bool Migrate()
        {
            if (version >= CurrentVersion)
            {
                return false;
            }

            version = CurrentVersion;
            return true;
        }

        /// <summary>
        /// Repairs a profile from an old, edited or corrupt save: clamps every value into range, drops unknown
        /// garments, resolves slot clashes and makes sure the body is dressed. Returns true when anything changed.
        /// </summary>
        public bool Sanitize(WardrobeCatalog catalog)
        {
            string before = Fingerprint();
            outfit ??= new List<EquippedItem>();
            name = CleanName(name);
            bodyStyle = Clamp(bodyStyle, 0f, 2f);
            shoulders = Clamp(shoulders, -1f, 1f);
            hips = Clamp(hips, -1f, 1f);
            legLength = Clamp(legLength, -1f, 1f);
            headSize = Clamp(headSize, -1f, 1f);
            height = (HeightPreset)ClampInt((int)height, 0, 2);
            skinTone = Clamp(skinTone, 0f, 1f);
            faceShape = ClampInt(faceShape, 0, AvatarOptions.FaceShapes - 1);
            eyes = ClampInt(eyes, 0, AvatarOptions.EyeShapes - 1);
            eyeColour = ClampInt(eyeColour, 0, AvatarOptions.EyeColours - 1);
            nose = ClampInt(nose, 0, AvatarOptions.Noses - 1);
            mouth = ClampInt(mouth, 0, AvatarOptions.Mouths - 1);
            brows = ClampInt(brows, 0, AvatarOptions.Brows - 1);
            facialHair = ClampInt(facialHair, 0, AvatarOptions.FacialHair - 1);
            glasses = ClampInt(glasses, 0, AvatarOptions.Glasses - 1);
            hairStyle = ClampInt(hairStyle, 0, AvatarOptions.HairStyles - 1);
            hairColour = ClampInt(hairColour, 0, AvatarOptions.HairColours - 1);
            animationStyle = (AnimationStyle)ClampInt((int)animationStyle, 0, 1);
            OutfitRules.Repair(this, catalog);
            return Fingerprint() != before;
        }

        /// <summary>Trimmed, at most <see cref="MaxNameLength"/> characters, letters, digits, spaces, apostrophes and hyphens only.</summary>
        public static string CleanName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(MaxNameLength);
            foreach (char c in raw.Trim())
            {
                if (sb.Length >= MaxNameLength)
                {
                    break;
                }

                // Devanagari vowel signs and other combining marks are kept, so Hindi and Nepali names survive.
                System.Globalization.UnicodeCategory category = char.GetUnicodeCategory(c);
                if (char.IsLetterOrDigit(c) || c == ' ' || c == '\'' || c == '-' ||
                    category == System.Globalization.UnicodeCategory.NonSpacingMark ||
                    category == System.Globalization.UnicodeCategory.SpacingCombiningMark)
                {
                    sb.Append(c);
                }
            }

            return sb.ToString().Trim();
        }

        private string Fingerprint()
        {
            var sb = new StringBuilder();
            sb.Append(name).Append('|').Append(bodyStyle).Append('|').Append(shoulders).Append('|').Append(hips).Append('|')
                .Append(legLength).Append('|').Append(headSize).Append('|').Append((int)height).Append('|').Append(skinTone).Append('|')
                .Append(faceShape).Append(eyes).Append(eyeColour).Append(nose).Append(mouth).Append(brows).Append(facialHair)
                .Append(glasses).Append(hairStyle).Append(hairColour).Append((int)animationStyle);
            foreach (EquippedItem item in outfit ?? new List<EquippedItem>())
            {
                sb.Append('|').Append(item?.id).Append(':').Append(item?.variant);
            }

            return sb.ToString();
        }

        private static float Clamp(float v, float min, float max) => float.IsNaN(v) ? (min + max) / 2f : Math.Max(min, Math.Min(max, v));

        private static int ClampInt(int v, int min, int max) => Math.Max(min, Math.Min(max, v));
    }

    /// <summary>How many choices each face and hair option has; the generators build exactly this many.</summary>
    public static class AvatarOptions
    {
        public const int FaceShapes = 5;
        public const int EyeShapes = 4;
        public const int EyeColours = 6;
        public const int Noses = 4;
        public const int Mouths = 4;
        public const int Brows = 4;

        /// <summary>0 is none.</summary>
        public const int FacialHair = 5;

        /// <summary>0 is none.</summary>
        public const int Glasses = 4;

        /// <summary>0 is shaved or very short.</summary>
        public const int HairStyles = 10;

        public const int HairColours = 8;
    }
}

using System;
using System.Collections.Generic;
using UtsavNights.Core.Art;

namespace UtsavNights.Core.Avatar
{
    /// <summary>Where the face detector found a face: the point between the eyes and the distance between them, in pixels (y down).</summary>
    public readonly struct FaceBox
    {
        public FaceBox(float midX, float midY, float eyeDistance)
        {
            MidX = midX;
            MidY = midY;
            EyeDistance = eyeDistance;
        }

        public float MidX { get; }

        public float MidY { get; }

        public float EyeDistance { get; }
    }

    /// <summary>What a photo suggests for the avatar. Nothing else from the photo is kept.</summary>
    public readonly struct PhotoSuggestion
    {
        public PhotoSuggestion(float skinTone, int hairColour, int faceShape)
        {
            SkinTone = skinTone;
            HairColour = hairColour;
            FaceShape = faceShape;
        }

        /// <summary>0 to 1 along <see cref="SkinTones"/>.</summary>
        public float SkinTone { get; }

        /// <summary>An index into <see cref="HairColours.All"/>, or -1 when no hair could be seen (covered, shaved, cropped).</summary>
        public int HairColour { get; }

        /// <summary>An index into the creator's face shapes (round, oval, square, heart, long).</summary>
        public int FaceShape { get; }

        public void ApplyTo(AvatarProfile profile)
        {
            profile.skinTone = SkinTone;
            if (HairColour >= 0)
            {
                profile.hairColour = HairColour;
            }

            profile.faceShape = FaceShape;
        }
    }

    /// <summary>
    /// The photo-assisted avatar's analysis (Phase 11 step 3, F26), on the phone and nothing more: given a small RGB copy of
    /// the photo and the face Android's face detector found, it samples the cheeks for skin tone, the band above the
    /// forehead for hair colour, and the face's widths and height for its shape, and returns the nearest options of the
    /// creator. The caller clears the pixels at once.
    /// </summary>
    public static class PhotoAnalysis
    {
        public const int MinimumAge = 18;

        /// <summary>The detector's eye distance must be at least this many pixels for a usable face.</summary>
        public const float MinEyeDistance = 16f;

        public enum FaceShapeId
        {
            Round = 0,
            Oval = 1,
            Square = 2,
            Heart = 3,
            Long = 4,
        }

        /// <summary>The suggestion, or null when the face is too small or too close to the edge to read.</summary>
        public static PhotoSuggestion? Analyse(byte[] rgb, int width, int height, FaceBox face)
        {
            if (rgb == null || rgb.Length < width * height * 3 || face.EyeDistance < MinEyeDistance)
            {
                return null;
            }

            float e = face.EyeDistance;
            float cheekY = face.MidY + 0.55f * e;
            Rgb? left = Sample(rgb, width, height, face.MidX - 0.55f * e, cheekY, 0.18f * e);
            Rgb? right = Sample(rgb, width, height, face.MidX + 0.55f * e, cheekY, 0.18f * e);
            Rgb? forehead = Sample(rgb, width, height, face.MidX, face.MidY - 0.55f * e, 0.15f * e);
            var samples = new List<Rgb>();
            foreach (Rgb? s in new[] { left, right, forehead })
            {
                if (s.HasValue)
                {
                    samples.Add(s.Value);
                }
            }

            if (samples.Count < 2)
            {
                return null;
            }

            Rgb skin = MedianOf(samples);

            // The face's outline from the skin colour: widths at the forehead, cheeks and jaw, and the height from the top
            // of the forehead to the chin.
            float foreheadW = RowWidth(rgb, width, height, face.MidX, face.MidY - 0.5f * e, skin, e);
            float cheekW = RowWidth(rgb, width, height, face.MidX, cheekY, skin, e);
            float jawW = RowWidth(rgb, width, height, face.MidX, face.MidY + 1.45f * e, skin, e);
            float top = Edge(rgb, width, height, face.MidX, face.MidY - 0.4f * e, -1, skin, e);
            float bottom = Edge(rgb, width, height, face.MidX, face.MidY + 0.9f * e, 1, skin, e);
            int shape = (int)ShapeFrom(foreheadW, cheekW, jawW, bottom - top);

            // Hair: above the hairline at a fixed height over the eyes (brown hair can be close enough to skin that the
            // forehead's edge runs on into it). Only a colour near one of the creator's hair colours counts: a wall or a
            // head covering behind keeps the avatar's own.
            int hair = -1;
            Rgb? above = Sample(rgb, width, height, face.MidX, face.MidY - 1.45f * e, 0.2f * e);
            if (above.HasValue && SkinDistance(above.Value, skin) > 0.05f)
            {
                hair = NearestHair(above.Value, MaxHairDistance);
            }

            return new PhotoSuggestion(NearestSkinTone(skin), hair, shape);
        }

        /// <summary>The point on the skin ramp closest to a colour, comparing brightness and warmth separately (photos are lit unevenly).</summary>
        public static float NearestSkinTone(Rgb c)
        {
            float best = 0.5f, bestD = float.MaxValue;
            for (int i = 0; i <= 100; i++)
            {
                float t = i / 100f;
                Rgb r = SkinTones.Evaluate(t);
                float d = 1.6f * Sq(Luma(r) - Luma(c)) + Sq(Cb(r) - Cb(c)) + Sq(Cr(r) - Cr(c));
                if (d < bestD)
                {
                    bestD = d;
                    best = t;
                }
            }

            return best;
        }

        /// <summary>How far (squared, RGB) a sample may be from a hair colour and still count as hair.</summary>
        public const float MaxHairDistance = 0.035f;

        /// <summary>The nearest hair colour, or -1 when none is within <paramref name="maxDistance"/>.</summary>
        public static int NearestHair(Rgb c, float maxDistance = float.MaxValue)
        {
            int best = -1;
            float bestD = maxDistance;
            for (int i = 0; i < HairColours.All.Length; i++)
            {
                Rgb h = HairColours.All[i];
                float d = Sq(h.R - c.R) + Sq(h.G - c.G) + Sq(h.B - c.B);
                if (d <= bestD)
                {
                    bestD = d;
                    best = i;
                }
            }

            return best;
        }

        /// <summary>A face shape from its widths (forehead, cheeks, jaw) and height.</summary>
        public static FaceShapeId ShapeFrom(float forehead, float cheeks, float jaw, float faceHeight)
        {
            if (cheeks <= 0f || faceHeight <= 0f)
            {
                return FaceShapeId.Oval;
            }

            float tall = faceHeight / cheeks;
            if (tall >= 1.5f)
            {
                return FaceShapeId.Long;
            }

            if (jaw > 0f && forehead / jaw >= 1.25f)
            {
                return FaceShapeId.Heart;
            }

            if (jaw / cheeks >= 0.9f && forehead / cheeks >= 0.9f)
            {
                return FaceShapeId.Square;
            }

            return tall <= 1.2f ? FaceShapeId.Round : FaceShapeId.Oval;
        }

        /// <summary>The median colour of a round patch, or null when the patch is off the picture.</summary>
        public static Rgb? Sample(byte[] rgb, int width, int height, float cx, float cy, float radius)
        {
            int r = Math.Max(1, (int)radius);
            if (cx - r < 0 || cy - r < 0 || cx + r >= width || cy + r >= height)
            {
                return null;
            }

            var rs = new List<float>();
            var gs = new List<float>();
            var bs = new List<float>();
            for (int y = (int)cy - r; y <= (int)cy + r; y++)
            {
                for (int x = (int)cx - r; x <= (int)cx + r; x++)
                {
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) > r * r)
                    {
                        continue;
                    }

                    int i = (y * width + x) * 3;
                    rs.Add(rgb[i] / 255f);
                    gs.Add(rgb[i + 1] / 255f);
                    bs.Add(rgb[i + 2] / 255f);
                }
            }

            return rs.Count == 0 ? (Rgb?)null : new Rgb(Median(rs), Median(gs), Median(bs));
        }

        /// <summary>How far a colour is from the skin, in chroma mostly (shadows on skin stay skin).</summary>
        public static float SkinDistance(Rgb c, Rgb skin) =>
            (float)Math.Sqrt(Sq(Cb(c) - Cb(skin)) + Sq(Cr(c) - Cr(skin)) + 0.15f * Sq(Luma(c) - Luma(skin)));

        private static float RowWidth(byte[] rgb, int width, int height, float cx, float y, Rgb skin, float e)
        {
            int row = (int)y;
            if (row < 0 || row >= height)
            {
                return 0f;
            }

            float l = Walk(rgb, width, height, cx, row, -1, skin, e);
            float r = Walk(rgb, width, height, cx, row, 1, skin, e);
            return r - l;
        }

        /// <summary>The last skin pixel from <paramref name="cx"/> towards one side, allowing short gaps (eyes, brows, a fringe).</summary>
        private static float Walk(byte[] rgb, int width, int height, float cx, int row, int dir, Rgb skin, float e)
        {
            int gapLimit = Math.Max(2, (int)(e * 0.12f));
            int gap = 0;
            int x = (int)cx;
            int lastSkin = x;
            int limit = (int)(cx + dir * e * 2.2f);
            while (x >= 0 && x < width && (dir < 0 ? x >= limit : x <= limit))
            {
                if (IsSkin(rgb, width, x, row, skin))
                {
                    lastSkin = x;
                    gap = 0;
                }
                else if (++gap > gapLimit)
                {
                    break;
                }

                x += dir;
            }

            return lastSkin;
        }

        private static float Edge(byte[] rgb, int width, int height, float cx, float startY, int dir, Rgb skin, float e)
        {
            int gapLimit = Math.Max(2, (int)(e * 0.12f));
            int gap = 0;
            int y = (int)startY;
            int lastSkin = y;
            int limit = (int)(startY + dir * e * 2.5f);
            int x = (int)cx;
            while (y >= 0 && y < height && (dir < 0 ? y >= limit : y <= limit))
            {
                if (IsSkin(rgb, width, x, y, skin))
                {
                    lastSkin = y;
                    gap = 0;
                }
                else if (++gap > gapLimit)
                {
                    break;
                }

                y += dir;
            }

            return lastSkin;
        }

        private static bool IsSkin(byte[] rgb, int width, int x, int y, Rgb skin)
        {
            int i = (y * width + x) * 3;
            return SkinDistance(new Rgb(rgb[i] / 255f, rgb[i + 1] / 255f, rgb[i + 2] / 255f), skin) < 0.08f;
        }

        private static Rgb MedianOf(List<Rgb> colours)
        {
            var rs = new List<float>();
            var gs = new List<float>();
            var bs = new List<float>();
            foreach (Rgb c in colours)
            {
                rs.Add(c.R);
                gs.Add(c.G);
                bs.Add(c.B);
            }

            return new Rgb(Median(rs), Median(gs), Median(bs));
        }

        private static float Median(List<float> values)
        {
            values.Sort();
            int n = values.Count;
            return n % 2 == 1 ? values[n / 2] : (values[n / 2 - 1] + values[n / 2]) / 2f;
        }

        // BT.601 luma and chroma, 0 to 1.
        private static float Luma(Rgb c) => 0.299f * c.R + 0.587f * c.G + 0.114f * c.B;

        private static float Cb(Rgb c) => -0.169f * c.R - 0.331f * c.G + 0.5f * c.B;

        private static float Cr(Rgb c) => 0.5f * c.R - 0.419f * c.G - 0.081f * c.B;

        private static float Sq(float v) => v * v;
    }
}

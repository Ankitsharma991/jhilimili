using System;
using System.Collections.Generic;

namespace UtsavNights.Core.Shows
{
    /// <summary>A launch pad, as an offset in metres from the celebration spot's centre (so a show plays at any spot).</summary>
    [Serializable]
    public sealed class ShowPad
    {
        public float dx;
        public float dz;
    }

    /// <summary>One cracker on the timeline: which, on which pad, lit at which second.</summary>
    [Serializable]
    public sealed class ShowCue
    {
        public string cracker = string.Empty;
        public int pad;
        public float at;
    }

    /// <summary>A fireworks show (PRD 12 "Finale designer": a timeline strip, items on launch pads, delays; F22).</summary>
    [Serializable]
    public sealed class ShowDef
    {
        public string id = string.Empty;
        public string name = string.Empty;
        public List<ShowPad> pads = new List<ShowPad>();
        public List<ShowCue> cues = new List<ShowCue>();

        public ShowDef Clone()
        {
            var c = new ShowDef { id = id, name = name };
            foreach (ShowPad p in pads)
            {
                c.pads.Add(new ShowPad { dx = p.dx, dz = p.dz });
            }

            foreach (ShowCue q in cues)
            {
                c.cues.Add(new ShowCue { cracker = q.cracker, pad = q.pad, at = q.at });
            }

            return c;
        }

        /// <summary>Seconds from the first cue to the last payoff.</summary>
        public float Length(Func<string, CrackerTiming?> lookup)
        {
            float end = 0f;
            foreach (ShowCue c in cues)
            {
                CrackerTiming? t = lookup(c.cracker);
                end = Math.Max(end, c.at + (t?.Fuse ?? 0f) + (t?.Show ?? 0f));
            }

            return end;
        }
    }

    /// <summary>Saved shows (shows.json on the phone, and the file a shows pack carries).</summary>
    [Serializable]
    public sealed class ShowLibrary
    {
        public int version = 1;
        public List<ShowDef> shows = new List<ShowDef>();

        public ShowDef Find(string id) => shows.Find(s => s.id == id);

        /// <summary>Saves a copy of the show (replacing one with its id). False when the library is full.</summary>
        public bool Put(ShowDef show)
        {
            int i = shows.FindIndex(s => s.id == show.id);
            if (i >= 0)
            {
                shows[i] = show.Clone();
                return true;
            }

            if (shows.Count >= ShowRules.MaxSavedShows)
            {
                return false;
            }

            shows.Add(show.Clone());
            return true;
        }

        public bool Remove(string id) => shows.RemoveAll(s => s.id == id) > 0;

        /// <summary>An id not yet used ("show_1", "show_2"...).</summary>
        public string NewId()
        {
            for (int n = 1; ; n++)
            {
                string id = "show_" + n;
                if (Find(id) == null)
                {
                    return id;
                }
            }
        }
    }

    /// <summary>What the show rules need to know about a cracker: its fuse, how long its effect lasts, and whether it flies.</summary>
    public readonly struct CrackerTiming
    {
        public CrackerTiming(float fuse, float show, bool aerial)
        {
            Fuse = fuse;
            Show = show;
            Aerial = aerial;
        }

        public float Fuse { get; }

        /// <summary>Seconds from ignition's payoff until the pad is free again.</summary>
        public float Show { get; }

        public bool Aerial { get; }
    }

    /// <summary>The rules a show must keep (Phase 9 step 4): the same limits as playing by hand, applied ahead of time.</summary>
    public static class ShowRules
    {
        public const int MaxPads = 8;
        public const float MaxLength = 120f;
        public const float MinPadSpacing = 0.6f;
        public const int MaxSavedShows = 12;
        public const int MaxNameLength = 24;
        public const int MaxCues = 40;

        /// <summary>Seconds before its cue a cracker is set on its pad (the player places, then lights).</summary>
        public const float Lead = 1f;

        /// <summary>
        /// Every problem with a show at a spot: known crackers, pads inside the play area and apart, a pad free before its next
        /// cracker, aerials only where the spot allows them, length, and no more crackers out at once than the phone's item limit
        /// (<see cref="Placement.PlacementRules.ItemLimit"/>). Empty when it can play.
        /// </summary>
        public static List<string> Check(ShowDef show, Func<string, CrackerTiming?> lookup, int itemLimit, float areaRadius, bool aerialsAllowed)
        {
            var p = new List<string>();
            if (show == null)
            {
                p.Add("no show");
                return p;
            }

            if (string.IsNullOrWhiteSpace(show.name) || show.name.Length > MaxNameLength)
            {
                p.Add($"a show needs a name of up to {MaxNameLength} characters");
            }

            if (show.pads.Count == 0 || show.pads.Count > MaxPads)
            {
                p.Add($"a show uses 1 to {MaxPads} launch pads");
            }

            for (int i = 0; i < show.pads.Count; i++)
            {
                ShowPad a = show.pads[i];
                if (a.dx * a.dx + a.dz * a.dz > areaRadius * areaRadius + 1e-4f)
                {
                    p.Add($"pad {i + 1} is outside the celebration area");
                }

                for (int j = i + 1; j < show.pads.Count; j++)
                {
                    ShowPad b = show.pads[j];
                    float dx = a.dx - b.dx, dz = a.dz - b.dz;
                    if (dx * dx + dz * dz < MinPadSpacing * MinPadSpacing)
                    {
                        p.Add($"pads {i + 1} and {j + 1} are too close together");
                    }
                }
            }

            if (show.cues.Count == 0)
            {
                p.Add("a show needs at least one cracker");
            }
            else if (show.cues.Count > MaxCues)
            {
                p.Add($"a show holds up to {MaxCues} crackers");
            }

            var spans = new List<(float from, float to)>();
            var byPad = new Dictionary<int, List<(float from, float to)>>();
            foreach (ShowCue c in show.cues)
            {
                CrackerTiming? t = lookup(c.cracker);
                if (t == null)
                {
                    p.Add($"'{c.cracker}' isn't a cracker in the catalog");
                    continue;
                }

                if (c.pad < 0 || c.pad >= show.pads.Count)
                {
                    p.Add($"'{c.cracker}' is on a pad that doesn't exist");
                    continue;
                }

                if (c.at < 0f || c.at > MaxLength)
                {
                    p.Add($"'{c.cracker}' is lit at {c.at:0.0} s; shows run 0 to {MaxLength:0} s");
                }

                if (t.Value.Aerial && !aerialsAllowed)
                {
                    p.Add($"'{c.cracker}' flies, and aerials can't go up from this spot");
                }

                float start = c.at - Lead, end = c.at + t.Value.Fuse + t.Value.Show;
                spans.Add((start, end));
                if (!byPad.TryGetValue(c.pad, out List<(float, float)> used))
                {
                    byPad[c.pad] = used = new List<(float, float)>();
                }

                foreach ((float from, float to) in used)
                {
                    if (start < to && from < end)
                    {
                        p.Add($"pad {c.pad + 1} is still busy at {c.at:0.0} s");
                        break;
                    }
                }

                used.Add((start, end));
            }

            int peak = Peak(spans);
            if (peak > itemLimit)
            {
                p.Add($"this phone shows up to {itemLimit} crackers at once; the show has {peak} out together");
            }

            if (show.Length(lookup) > MaxLength + 30f)
            {
                p.Add("the show is too long");
            }

            return p;
        }

        /// <summary>The most spans open at one moment (a span closes before one opening at the same time).</summary>
        public static int Peak(List<(float from, float to)> spans)
        {
            var edges = new List<(float t, int d)>(spans.Count * 2);
            foreach ((float from, float to) in spans)
            {
                edges.Add((from, 1));
                edges.Add((to, -1));
            }

            edges.Sort((a, b) => a.t != b.t ? a.t.CompareTo(b.t) : a.d.CompareTo(b.d));
            int open = 0, peak = 0;
            foreach ((float _, int d) in edges)
            {
                open += d;
                peak = Math.Max(peak, open);
            }

            return peak;
        }

        /// <summary>The cues in lighting order (the player walks this list).</summary>
        public static List<ShowCue> Order(ShowDef show)
        {
            var list = new List<ShowCue>(show.cues);
            list.Sort((a, b) => a.at != b.at ? a.at.CompareTo(b.at) : a.pad.CompareTo(b.pad));
            return list;
        }

        /// <summary>Moves a cue by a delay (the designer's delay controls), kept within the show's length.</summary>
        public static void Shift(ShowCue cue, float seconds) => cue.at = Math.Max(0f, Math.Min(MaxLength, (float)Math.Round((cue.at + seconds) * 10f) / 10f));
    }

    /// <summary>The built-in template shows (Phase 9 step 4): a starting point to edit and save. Free crackers only.</summary>
    public static class ShowTemplates
    {
        public static readonly ShowDef[] All =
        {
            Make("gentle_start", "Gentle start", 4, ("sparkler_classic", 0, 0f), ("flowerpot_gold", 1, 1.5f), ("sparkler_classic", 2, 3f), ("flowerpot_gold", 3, 5f)),
            Make("diwali_night", "Diwali night", 6, ("flowerpot_gold", 0, 0f), ("chakri_classic", 1, 2f), ("chain_lar", 2, 4f), ("rocket_basic", 3, 7f),
                ("skyshot_green", 4, 9f), ("cake_twelve", 5, 12f)),
            Make("tihar_lights", "Tihar lights", 4, ("sparkler_classic", 0, 0f), ("flowerpot_gold", 1, 1f), ("chakri_classic", 2, 3.5f), ("sparkler_classic", 3, 6f)),
        };

        /// <summary>A fresh, empty show with its pads laid out.</summary>
        public static ShowDef Blank(string id, string name, int pads = 4) => Make(id, name, pads);

        /// <summary>
        /// Pads in rows of three, 0.7 m apart, front row first: up to six fit the rooftop's 1.6 m area, all eight the
        /// courtyard's. dx is to the camera's right, dz away from it.
        /// </summary>
        public static List<ShowPad> Layout(int count)
        {
            var list = new List<ShowPad>();
            for (int i = 0; i < count; i++)
            {
                int row = i / 3, inRow = Math.Min(3, count - row * 3), col = i % 3;
                list.Add(new ShowPad { dx = (col - (inRow - 1) * 0.5f) * 0.7f, dz = row * 0.7f - 0.35f * ((count - 1) / 3) });
            }

            return list;
        }

        private static ShowDef Make(string id, string name, int pads, params (string cracker, int pad, float at)[] cues)
        {
            var s = new ShowDef { id = id, name = name, pads = Layout(pads) };
            foreach ((string cracker, int pad, float at) in cues)
            {
                s.cues.Add(new ShowCue { cracker = cracker, pad = pad, at = at });
            }

            return s;
        }
    }
}

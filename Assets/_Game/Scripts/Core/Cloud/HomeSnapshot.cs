using System;
using System.Collections.Generic;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Home;

namespace UtsavNights.Core.Cloud
{
    /// <summary>
    /// A shared home (Phase 10 step 4, F23): the house design with its decorations, the neighbourhood it stands in, and
    /// the avatar's look only when the player chose to show it. No free text at all: no avatar name, no layout names,
    /// so there's nothing to moderate but the arrangement itself.
    /// </summary>
    [Serializable]
    public sealed class HomeSnapshot
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public string environment = string.Empty;
        public HouseDesign design = HouseDesign.Default();
        public bool hasAvatar;
        public AvatarProfile avatar = new AvatarProfile();

        public static HomeSnapshot From(HouseDesign design, string environment, AvatarProfile avatarOrNull)
        {
            var s = new HomeSnapshot { environment = environment ?? string.Empty, design = design.Clone() };
            if (avatarOrNull != null && avatarOrNull.created)
            {
                s.hasAvatar = true;
                s.avatar = avatarOrNull.Clone();
            }

            s.Clean();
            return s;
        }

        /// <summary>Repairs what came off the network the way a save file is repaired, and drops every bit of free text.</summary>
        public void Clean()
        {
            design ??= HouseDesign.Default();
            design.Sanitize();
            avatar ??= new AvatarProfile();
            avatar.name = string.Empty;
            avatar.Migrate();
            avatar.Sanitize(WardrobeCatalog.Default);
            if (!hasAvatar)
            {
                avatar = new AvatarProfile();
            }
        }

        /// <summary>Problems that make a snapshot unusable (empty when it can be shown).</summary>
        public List<string> Problems()
        {
            var p = new List<string>();
            if (version < 1 || version > CurrentVersion)
            {
                p.Add(version > CurrentVersion ? "made by a newer version of the game" : "not a home");
            }

            if (design == null)
            {
                p.Add("no house");
            }

            if (!string.IsNullOrEmpty(environment) && !Analytics.AnalyticsSchema.IsSafeId(environment))
            {
                p.Add("an unknown neighbourhood");
            }

            if (hasAvatar && avatar != null && !string.IsNullOrEmpty(avatar.name))
            {
                p.Add("an avatar name (never shared)");
            }

            return p;
        }

        /// <summary>
        /// The decorations this phone shows on a visit: in the owner's order, while they fit this phone's decoration
        /// budget (a high-end phone's courtyard on a low-end phone shows its first, most deliberate pieces).
        /// </summary>
        public static HouseDesign ForTier(HouseDesign design, int tier)
        {
            HouseDesign shown = design.Clone();
            int budget = DecorBudget.Units[Math.Max(0, Math.Min(DecorBudget.Units.Length - 1, tier))];
            int used = 0;
            var kept = new List<PlacedDecor>();
            foreach (PlacedDecor d in shown.decorations)
            {
                DecorInfo info = HomeCatalog.Decoration(d.item);
                if (info == null)
                {
                    continue;
                }

                int cost = DecorBudget.CostOf(info);
                if (used + cost > budget)
                {
                    continue;
                }

                used += cost;
                kept.Add(d);
            }

            shown.decorations = kept;
            return shown;
        }
    }

    /// <summary>A visited home kept on the phone, so it can be seen again offline (the newest ten).</summary>
    [Serializable]
    public sealed class VisitRecord
    {
        public string code = string.Empty;
        public long visitedUnix;
        public string payload = string.Empty;
    }

    [Serializable]
    public sealed class VisitHistory
    {
        public const int Kept = 10;

        public List<VisitRecord> visits = new List<VisitRecord>();

        /// <summary>Adds or refreshes a visit at the top and keeps the newest ten.</summary>
        public void Remember(string code, string payload, long nowUnix)
        {
            visits.RemoveAll(v => v == null || v.code == code);
            visits.Insert(0, new VisitRecord { code = code, visitedUnix = nowUnix, payload = payload });
            if (visits.Count > Kept)
            {
                visits.RemoveRange(Kept, visits.Count - Kept);
            }
        }

        public VisitRecord Find(string code) => visits.Find(v => v != null && v.code == code);

        public bool Forget(string code) => visits.RemoveAll(v => v != null && v.code == code) > 0;
    }
}

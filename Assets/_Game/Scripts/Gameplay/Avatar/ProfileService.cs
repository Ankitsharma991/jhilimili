using System;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Runtime;

namespace UtsavNights.Gameplay
{
    /// <summary>Published when the player saves an avatar from the creator (analytics avatar_saved; never the name, body or skin).</summary>
    public readonly struct AvatarSavedEvent
    {
        public AvatarSavedEvent(bool fromPreset, int garments, string top, string bottom, string full, string headwear)
        {
            FromPreset = fromPreset;
            Garments = garments;
            Top = top;
            Bottom = bottom;
            Full = full;
            Headwear = headwear;
        }

        public bool FromPreset { get; }

        public int Garments { get; }

        /// <summary>Catalogue ids of the garments in these layers, or "none".</summary>
        public string Top { get; }

        public string Bottom { get; }

        public string Full { get; }

        public string Headwear { get; }
    }

    /// <summary>The creator opened (analytics avatar_creator_opened). First is true until an avatar has been saved.</summary>
    public readonly struct AvatarCreatorOpenedEvent
    {
        public AvatarCreatorOpenedEvent(bool first) => First = first;

        public bool First { get; }
    }

    /// <summary>The creator closed without saving (analytics avatar_skipped).</summary>
    public readonly struct AvatarSkippedEvent
    {
    }

    /// <summary>The player's avatar for this session (profile.json): loaded once, saved by the creator, watched by the courtyard avatar.</summary>
    public sealed class ProfileService
    {
        private readonly ProfileStore _store;

        public ProfileService(ProfileStore store)
        {
            _store = store;
            Current = store.Load();
        }

        public event Action Changed;

        public AvatarProfile Current { get; private set; }

        public void Save(AvatarProfile profile, bool fromPreset)
        {
            Current = profile.Clone();
            Current.created = true;
            _store.Save(Current);
            Changed?.Invoke();
            WardrobeCatalog catalog = WardrobeCatalog.Default;
            Game.Events.Publish(new AvatarSavedEvent(fromPreset, Current.outfit.Count,
                Id(Current, catalog, g => (g.Slots & WardrobeSlot.BaseTop) != 0 && (g.Slots & WardrobeSlot.BaseBottom) == 0),
                Id(Current, catalog, g => (g.Slots & WardrobeSlot.BaseBottom) != 0 && (g.Slots & WardrobeSlot.BaseTop) == 0),
                Id(Current, catalog, g => (g.Slots & WardrobeSlot.BaseTop) != 0 && (g.Slots & WardrobeSlot.BaseBottom) != 0),
                Id(Current, catalog, g => g.Slots == WardrobeSlot.Headwear)));
        }

        private static string Id(AvatarProfile profile, WardrobeCatalog catalog, Func<GarmentInfo, bool> layer)
        {
            foreach (EquippedItem item in profile.outfit)
            {
                GarmentInfo g = catalog.Find(item.id);
                if (g != null && layer(g))
                {
                    return g.Id;
                }
            }

            return "none";
        }
    }
}

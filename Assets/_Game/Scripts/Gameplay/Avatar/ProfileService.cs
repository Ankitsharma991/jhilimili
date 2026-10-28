using System;
using UnityEngine;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Runtime;

namespace UtsavNights.Gameplay
{
    /// <summary>Published when the player saves an avatar from the creator (analytics in Phase 6 step 8; never carries the name).</summary>
    public readonly struct AvatarSavedEvent
    {
        public AvatarSavedEvent(bool fromPreset, int garments)
        {
            FromPreset = fromPreset;
            Garments = garments;
        }

        public bool FromPreset { get; }

        public int Garments { get; }
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
            Game.Events.Publish(new AvatarSavedEvent(fromPreset, Current.outfit.Count));
        }
    }
}

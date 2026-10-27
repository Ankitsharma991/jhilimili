using System;
using UtsavNights.Core.Monetisation;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// One ad network (AdMob behind UTSAV_ADMOB, or the development card). Implementations must call
    /// <c>done</c> exactly once, on the main thread, and never throw into gameplay.
    /// </summary>
    public interface IAdBackend
    {
        string Name { get; }

        bool IsReady(AdKind kind);

        /// <summary>Start loading the next ad of this kind (preload; PRD 15.2).</summary>
        void Load(AdKind kind);

        void Show(AdKind kind, string placement, Action<AdOutcome> done);
    }
}

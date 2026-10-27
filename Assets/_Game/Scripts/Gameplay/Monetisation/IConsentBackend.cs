using System;
using UtsavNights.Core.Monetisation;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Consent provider: Google UMP from the UtsavNights.Ads assembly, or the development form. Callbacks run on the main
    /// thread exactly once; a non-null string is an error, and the game carries on without ads.
    /// </summary>
    public interface IConsentBackend
    {
        string Name { get; }

        ConsentStatus Status { get; }

        /// <summary>UMP canRequestAds: true once a choice is made or none is needed.</summary>
        bool CanRequestAds { get; }

        /// <summary>The region requires a way to change the choice later (the Settings entry).</summary>
        bool PrivacyOptionsRequired { get; }

        /// <summary>Updates consent info and shows the consent form if one is required and not yet answered.</summary>
        void Gather(Action<string> done);

        void ShowPrivacyOptions(Action<string> done);

        /// <summary>Forgets the stored choice (testing with debug geography only).</summary>
        void Reset();
    }
}

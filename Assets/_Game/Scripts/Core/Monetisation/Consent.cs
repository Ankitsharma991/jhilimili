namespace UtsavNights.Core.Monetisation
{
    /// <summary>Consent state as Google's User Messaging Platform reports it.</summary>
    public enum ConsentStatus
    {
        /// <summary>Not gathered yet this launch.</summary>
        Unknown,

        /// <summary>A choice is needed (EEA, UK and similar) and has not been made.</summary>
        Required,

        /// <summary>No consent message applies in this region.</summary>
        NotRequired,

        /// <summary>The player made a choice (agree or decline; declining still allows non-personalised ads).</summary>
        Obtained,
    }

    /// <summary>
    /// How consent gates ads and analytics (PRD 15.2, 16). Ads follow UMP's canRequestAds. Analytics needs
    /// the "Share usage statistics" setting and, where a consent choice is required, that choice made.
    /// </summary>
    public static class ConsentRules
    {
        public static bool AnalyticsAllowed(bool shareUsageStats, ConsentStatus status) =>
            shareUsageStats && (status == ConsentStatus.NotRequired || status == ConsentStatus.Obtained);

        /// <summary>Settings line under "Privacy options".</summary>
        public static string Describe(ConsentStatus status, bool privacyOptionsRequired)
        {
            switch (status)
            {
                case ConsentStatus.Obtained:
                    return privacyOptionsRequired ? "Change your ad consent choices" : "Ad consent choice saved";
                case ConsentStatus.NotRequired:
                    return "No ad consent is needed in your region";
                case ConsentStatus.Required:
                    return "Choose how ads use your data";
                default:
                    return "Checking ad consent";
            }
        }
    }
}

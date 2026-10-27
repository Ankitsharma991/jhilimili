using System.Collections.Generic;

namespace UtsavNights.Core.Release
{
    /// <summary>Google Play testing and release tracks (PRD 19, Phase 5).</summary>
    public enum ReleaseTrack
    {
        /// <summary>Internal testing: up to 100 named testers, no review wait. Test ad IDs are acceptable.</summary>
        Internal,

        /// <summary>Closed testing: the PRD's 30 to 100 testers. Live ad IDs from here on (Phase 4 exit gate).</summary>
        Closed,

        Production,
    }

    public enum CheckLevel
    {
        /// <summary>Worth knowing; the build goes ahead.</summary>
        Warning,

        /// <summary>The build stops.</summary>
        Blocker,
    }

    public readonly struct ReleaseCheck
    {
        public ReleaseCheck(string id, CheckLevel level, string message)
        {
            Id = id;
            Level = level;
            Message = message;
        }

        public string Id { get; }

        public CheckLevel Level { get; }

        public string Message { get; }

        public override string ToString() => $"{(Level == CheckLevel.Blocker ? "BLOCKER" : "warning")} {Id}: {Message}";
    }

    /// <summary>What the editor knows about the project when a store build starts.</summary>
    public sealed class ReleaseInputs
    {
        public ReleaseTrack Track;
        public string VersionName = string.Empty;
        public int VersionCode;

        /// <summary>The version code of the last successful store build (Play rejects a code it has seen).</summary>
        public int LastStoreVersionCode;

        public bool KeystoreFound;
        public bool KeystorePasswordsSet;
        public bool AdsSdkInstalled;
        public bool PurchasingSdkInstalled;

        /// <summary>Firebase analytics and Crashlytics; until then events stay on the phone.</summary>
        public bool AnalyticsSdkInstalled;

        public bool AdsUseTestIds;
        public bool DebugGeographyEea;
        public bool HasPrivacyPolicy;
        public int TargetSdk;
    }

    /// <summary>
    /// Preflight for a Google Play upload. Blockers are things Play rejects or that would harm players
    /// (fake ads or free purchases from the development backends, a debug consent region). Warnings are
    /// fine on the internal track and become blockers from the closed test on, where the PRD needs live
    /// ads and the privacy policy.
    /// </summary>
    public static class ReleaseChecks
    {
        /// <summary>Play's minimum target API for new apps and updates. [Verify] each year: Google raises it every August.</summary>
        public const int MinimumTargetSdk = 35;

        public static List<ReleaseCheck> Evaluate(ReleaseInputs input)
        {
            var checks = new List<ReleaseCheck>();
            bool publicTrack = input.Track != ReleaseTrack.Internal;
            CheckLevel laterBlocker = publicTrack ? CheckLevel.Blocker : CheckLevel.Warning;

            if (!IsVersionName(input.VersionName))
            {
                checks.Add(new ReleaseCheck("version_name", CheckLevel.Blocker,
                    $"version name \"{input.VersionName}\" is not of the form 1.2.3"));
            }

            if (input.VersionCode <= input.LastStoreVersionCode)
            {
                checks.Add(new ReleaseCheck("version_code", CheckLevel.Blocker,
                    $"version code {input.VersionCode} must be higher than the last store build ({input.LastStoreVersionCode})"));
            }

            if (!input.KeystoreFound)
            {
                checks.Add(new ReleaseCheck("upload_key", CheckLevel.Blocker,
                    "upload keystore not found; create it with tools/make_upload_key.sh"));
            }
            else if (!input.KeystorePasswordsSet)
            {
                checks.Add(new ReleaseCheck("upload_key", CheckLevel.Blocker, "keystore or key password missing"));
            }

            if (!input.AdsSdkInstalled)
            {
                checks.Add(new ReleaseCheck("ads_sdk", CheckLevel.Blocker,
                    "Google Mobile Ads is not installed; the build would show development test-ad cards"));
            }

            if (!input.PurchasingSdkInstalled)
            {
                checks.Add(new ReleaseCheck("purchasing_sdk", CheckLevel.Blocker,
                    "Unity IAP is not installed; the build would unlock Remove ads for free"));
            }

            if (input.DebugGeographyEea)
            {
                checks.Add(new ReleaseCheck("debug_geography", CheckLevel.Blocker,
                    "Debug Geography Eea is ticked in AdsConfig; untick it for store builds"));
            }

            if (input.AdsUseTestIds)
            {
                checks.Add(new ReleaseCheck("test_ad_ids", laterBlocker,
                    "AdsConfig still has Google's test ad IDs; the closed test needs your live AdMob IDs"));
            }

            if (!input.HasPrivacyPolicy)
            {
                checks.Add(new ReleaseCheck("privacy_policy", laterBlocker,
                    "no privacy policy URL in AppLinks; Play needs it before the closed test"));
            }

            if (!input.AnalyticsSdkInstalled)
            {
                checks.Add(new ReleaseCheck("analytics_sdk", CheckLevel.Warning,
                    "Firebase is not connected; crash and usage data come only from Play Console's Android vitals"));
            }

            if (input.TargetSdk < MinimumTargetSdk)
            {
                checks.Add(new ReleaseCheck("target_sdk", CheckLevel.Blocker,
                    $"target API {input.TargetSdk} is below Play's minimum of {MinimumTargetSdk}"));
            }

            return checks;
        }

        public static bool HasBlocker(IReadOnlyList<ReleaseCheck> checks)
        {
            for (int i = 0; i < checks.Count; i++)
            {
                if (checks[i].Level == CheckLevel.Blocker)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Three dot-separated numbers without leading zeros, for example 0.1.0 or 1.12.3.</summary>
        public static bool IsVersionName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            string[] parts = name.Split('.');
            if (parts.Length != 3)
            {
                return false;
            }

            foreach (string part in parts)
            {
                if (part.Length == 0 || part.Length > 4 || (part.Length > 1 && part[0] == '0'))
                {
                    return false;
                }

                foreach (char c in part)
                {
                    if (c < '0' || c > '9')
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}

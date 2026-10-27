using System;
using UtsavNights.Core.Analytics;

namespace UtsavNights.Gameplay
{
    /// <summary>Analytics provider: Firebase Analytics once added (see MonetisationProviders), or the local development log.</summary>
    public interface IAnalyticsBackend
    {
        string Name { get; }

        /// <summary>Consent and the usage-statistics switch; off means nothing is collected or sent.</summary>
        void SetCollectionEnabled(bool enabled);

        void Log(string eventName, EventParams parameters);
    }

    /// <summary>Crash reporter: Firebase Crashlytics once added, or the local development log.</summary>
    public interface ICrashReporter
    {
        string Name { get; }

        void SetCollectionEnabled(bool enabled);

        /// <summary>Records a caught exception that did not crash the app (non-fatal).</summary>
        void RecordException(Exception exception);
    }
}

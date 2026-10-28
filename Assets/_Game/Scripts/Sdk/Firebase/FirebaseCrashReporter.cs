using System;
using Firebase.Crashlytics;
using UtsavNights.Gameplay;

namespace UtsavNights.FirebaseSdk
{
    /// <summary>
    /// Firebase Crashlytics. Uncaught exceptions and native crashes are reported by the SDK itself once collection is
    /// on; it starts off (manifest meta-data) and follows the same switch as analytics. Native stack traces need the
    /// symbols zip from the store build uploaded with the Firebase CLI (Docs/Phase5/FIREBASE.md).
    /// </summary>
    internal sealed class FirebaseCrashReporter : ICrashReporter
    {
        public string Name => "crashlytics";

        public void SetCollectionEnabled(bool enabled) => FirebaseStartup.Run(() =>
        {
            Crashlytics.IsCrashlyticsCollectionEnabled = enabled;
            Crashlytics.ReportUncaughtExceptionsAsFatal = true;
        });

        public void RecordException(Exception exception) => FirebaseStartup.Run(() => Crashlytics.LogException(exception));
    }
}

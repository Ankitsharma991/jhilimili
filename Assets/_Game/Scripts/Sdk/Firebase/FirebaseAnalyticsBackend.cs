using System.Collections.Generic;
using Firebase.Analytics;
using UtsavNights.Core.Analytics;
using UtsavNights.Gameplay;

namespace UtsavNights.FirebaseSdk
{
    /// <summary>
    /// Firebase Analytics for the PRD 17 events. Collection starts off (AndroidManifest meta-data in
    /// UtsavFirebaseConfig.androidlib) and follows <see cref="SetCollectionEnabled"/>, which the analytics
    /// service calls only once consent and the "Share usage statistics" switch allow it. Consent mode: analytics
    /// storage follows that switch; the advertising signals stay denied, because this project never links
    /// analytics to ads.
    /// </summary>
    internal sealed class FirebaseAnalyticsBackend : IAnalyticsBackend
    {
        public string Name => "firebase";

        public void SetCollectionEnabled(bool enabled) => FirebaseStartup.Run(() =>
        {
            FirebaseAnalytics.SetConsent(new Dictionary<ConsentType, ConsentStatus>
            {
                { ConsentType.AnalyticsStorage, enabled ? ConsentStatus.Granted : ConsentStatus.Denied },
                { ConsentType.AdStorage, ConsentStatus.Denied },
                { ConsentType.AdUserData, ConsentStatus.Denied },
                { ConsentType.AdPersonalization, ConsentStatus.Denied },
            });
            FirebaseAnalytics.SetAnalyticsCollectionEnabled(enabled);
        });

        public void Log(string eventName, EventParams parameters)
        {
            Parameter[] converted = Convert(parameters);
            FirebaseStartup.Run(() => FirebaseAnalytics.LogEvent(eventName, converted));
        }

        private static Parameter[] Convert(EventParams parameters)
        {
            if (parameters == null || parameters.Count == 0)
            {
                return new Parameter[0];
            }

            var result = new Parameter[parameters.Count];
            for (int i = 0; i < parameters.Count; i++)
            {
                KeyValuePair<string, object> item = parameters.Items[i];
                switch (item.Value)
                {
                    case string s:
                        result[i] = new Parameter(item.Key, s);
                        break;
                    case double d:
                        result[i] = new Parameter(item.Key, d);
                        break;
                    default:
                        result[i] = new Parameter(item.Key, (long)item.Value);
                        break;
                }
            }

            return result;
        }
    }
}

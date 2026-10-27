using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace UtsavNights.Core.Analytics
{
    /// <summary>Parameters of one analytics event: short ids and numbers only (see <see cref="AnalyticsSchema"/>).</summary>
    public sealed class EventParams
    {
        private readonly List<KeyValuePair<string, object>> _items = new List<KeyValuePair<string, object>>(4);

        public IReadOnlyList<KeyValuePair<string, object>> Items => _items;

        public int Count => _items.Count;

        public EventParams Add(string key, string value)
        {
            _items.Add(new KeyValuePair<string, object>(key, value ?? string.Empty));
            return this;
        }

        public EventParams Add(string key, long value)
        {
            _items.Add(new KeyValuePair<string, object>(key, value));
            return this;
        }

        public EventParams Add(string key, double value)
        {
            _items.Add(new KeyValuePair<string, object>(key, value));
            return this;
        }

        public EventParams Add(string key, bool value) => Add(key, value ? 1L : 0L);

        /// <summary>One-line JSON object, for the local log.</summary>
        public string ToJson(string eventName)
        {
            var sb = new StringBuilder(96);
            sb.Append("{\"event\":\"").Append(eventName).Append('"');
            foreach (KeyValuePair<string, object> item in _items)
            {
                sb.Append(",\"").Append(item.Key).Append("\":");
                switch (item.Value)
                {
                    case string s:
                        sb.Append('"').Append(s).Append('"');
                        break;
                    case double d:
                        sb.Append(d.ToString("0.###", CultureInfo.InvariantCulture));
                        break;
                    default:
                        sb.Append(((long)item.Value).ToString(CultureInfo.InvariantCulture));
                        break;
                }
            }

            return sb.Append('}').ToString();
        }
    }
}

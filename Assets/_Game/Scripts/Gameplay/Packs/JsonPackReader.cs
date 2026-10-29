using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Neighbourhood;
using UtsavNights.Core.Packs;

namespace UtsavNights.Gameplay
{
    /// <summary>Reads pack content files with JsonUtility for the pack linter (editor, CI and the phone).</summary>
    public sealed class JsonPackReader : IPackContentReader
    {
        /// <summary>Checks a shows file; set by the show model (step 4). Until then any shows file is accepted.</summary>
        public static Func<string, List<string>> ShowsCheck { get; set; } = _ => new List<string>();

        public EnvironmentDef ReadEnvironment(byte[] json) => Read<EnvironmentDef>(json);

        public SettlementLayout ReadLayout(byte[] json) => Read<SettlementLayout>(json);

        public WardrobePackManifest ReadWardrobe(byte[] json) => Read<WardrobePackManifest>(json);

        public List<string> CheckShows(byte[] json) => ShowsCheck(Text(json));

        public static string Text(byte[] json) => json == null ? string.Empty : Encoding.UTF8.GetString(json);

        public static byte[] Bytes(object value) => Encoding.UTF8.GetBytes(JsonUtility.ToJson(value, true));

        private static T Read<T>(byte[] json) where T : class
        {
            try
            {
                return JsonUtility.FromJson<T>(Text(json));
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}

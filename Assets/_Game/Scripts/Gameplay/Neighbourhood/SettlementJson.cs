using System.Collections.Generic;
using UnityEngine;
using UtsavNights.Core.Neighbourhood;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Settlement layout JSON (PRD 10 "per-settlement layout JSON", PRD 13.4 packs): written from a layout and read back with
    /// the lint applied, so a damaged or hand-edited layout never reaches the scene.
    /// </summary>
    public static class SettlementJson
    {
        public static string Write(SettlementLayout layout) => JsonUtility.ToJson(layout, true);

        /// <summary>The layout, or null with the reasons when the text is not a usable layout.</summary>
        public static SettlementLayout Read(string json, out List<string> problems)
        {
            problems = new List<string>();
            SettlementLayout layout;
            try
            {
                layout = JsonUtility.FromJson<SettlementLayout>(json);
            }
            catch (System.ArgumentException e)
            {
                problems.Add("not JSON: " + e.Message);
                return null;
            }

            if (layout == null || layout.version > SettlementLayout.CurrentVersion)
            {
                problems.Add(layout == null ? "empty layout" : $"layout version {layout.version} is newer than this app");
                return null;
            }

            problems = SettlementLint.Check(layout);
            return problems.Count == 0 ? layout : null;
        }
    }
}

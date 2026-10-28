using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
using UtsavNights.Core.Avatar;

namespace UtsavNights.EditorTools
{
    /// <summary>Writes the Phase 6 clipping test report (PRD 19 deliverable) to Docs/Phase6/CLIPPING.md.</summary>
    public static class ClippingReport
    {
        public const string Path = "Docs/Phase6/CLIPPING.md";

        [MenuItem("Utsav/Phase 6/Write Clipping Report", priority = 91)]
        public static void Write()
        {
            var results = ClippingCheck.RunMatrix();
            File.WriteAllText(Path, ClippingCheck.Report(results, DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
            Debug.Log($"[Clipping] {results.Count} combinations written to {Path}");
        }

        public static void CiWrite()
        {
            Write();
            EditorApplication.Exit(0);
        }
    }
}

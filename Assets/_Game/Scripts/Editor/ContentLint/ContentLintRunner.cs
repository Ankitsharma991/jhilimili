using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UtsavNights.Content;

namespace UtsavNights.EditorTools.ContentLint
{
    /// <summary>
    /// Runs the content linter from the menu, before every player build, and from batch mode:
    ///   Unity -batchmode -projectPath . -executeMethod UtsavNights.EditorTools.ContentLint.ContentLintRunner.CiValidate -quit
    /// </summary>
    public sealed class ContentLintRunner : IPreprocessBuildWithReport
    {
        public const string LogTag = "[UTSAV_LINT]";

        public int callbackOrder => -100;

        [MenuItem("Utsav/Content/Validate All Definitions", priority = 60)]
        public static void ValidateFromMenu()
        {
            List<LintIssue> issues = RunAndLog(out int count);
            int errors = CrackerDefinitionLinter.CountErrors(issues);
            string summary = $"{count} definitions checked: {errors} errors, {issues.Count - errors} warnings.";
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Content lint", summary + (errors > 0 ? "\n\nSee the Console for details." : string.Empty), "OK");
            }
        }

        public static void CiValidate()
        {
            List<LintIssue> issues = RunAndLog(out _);
            EditorApplication.Exit(CrackerDefinitionLinter.CountErrors(issues) > 0 ? 1 : 0);
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            List<LintIssue> issues = RunAndLog(out _);
            int errors = CrackerDefinitionLinter.CountErrors(issues);
            if (errors > 0)
            {
                throw new BuildFailedException($"{LogTag} {errors} content error(s). Fix them or run Utsav/Content/Validate All Definitions.");
            }
        }

        public static List<CrackerDefinition> FindAllDefinitions()
        {
            var result = new List<CrackerDefinition>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(CrackerDefinition)))
            {
                var def = AssetDatabase.LoadAssetAtPath<CrackerDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (def != null)
                {
                    result.Add(def);
                }
            }

            return result;
        }

        private static List<LintIssue> RunAndLog(out int count)
        {
            List<CrackerDefinition> definitions = FindAllDefinitions();
            count = definitions.Count;
            List<LintIssue> issues = CrackerDefinitionLinter.LintAll(definitions);
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(CrackerCatalog)))
            {
                issues.AddRange(CatalogLinter.Lint(AssetDatabase.LoadAssetAtPath<CrackerCatalog>(AssetDatabase.GUIDToAssetPath(guid))));
                count++;
            }

            // Phase 5 step 2: every game sound and texture against its import rule.
            issues.AddRange(AssetImportLinter.Lint());
            foreach (LintIssue issue in issues)
            {
                CrackerDefinition context = definitions.Find(d => d != null && (d.Id == issue.AssetName || d.name == issue.AssetName));
                string line = $"{LogTag} {issue}";
                if (issue.Severity == LintSeverity.Error)
                {
                    Debug.LogError(line, context);
                }
                else
                {
                    Debug.LogWarning(line, context);
                }
            }

            int errors = CrackerDefinitionLinter.CountErrors(issues);
            Debug.Log($"{LogTag} {count} assets (definitions and catalogs), {errors} errors, {issues.Count - errors} warnings");
            return issues;
        }
    }
}

using System.Collections.Generic;
using UtsavNights.Content;

namespace UtsavNights.EditorTools.ContentLint
{
    /// <summary>Rules for <see cref="CrackerCatalog"/> assets: no empty slots, no duplicates, locked items explain how to unlock.</summary>
    public static class CatalogLinter
    {
        public static List<LintIssue> Lint(CrackerCatalog catalog)
        {
            var issues = new List<LintIssue>();
            if (catalog == null)
            {
                issues.Add(new LintIssue(LintSeverity.Error, "catalog.null", "(null)", "catalog is missing"));
                return issues;
            }

            string name = catalog.name;
            if (catalog.Count == 0)
            {
                issues.Add(new LintIssue(LintSeverity.Error, "catalog.empty", name, "catalog has no entries; the tray would be empty"));
            }

            var seen = new HashSet<CrackerDefinition>();
            int unlocked = 0;
            for (int i = 0; i < catalog.Count; i++)
            {
                CrackerCatalog.Entry entry = catalog.Entries[i];
                if (entry?.definition == null)
                {
                    issues.Add(new LintIssue(LintSeverity.Error, "catalog.slot_empty", name, $"entry {i} has no definition"));
                    continue;
                }

                if (!seen.Add(entry.definition))
                {
                    issues.Add(new LintIssue(LintSeverity.Error, "catalog.duplicate", name, $"'{entry.definition.Id}' appears more than once"));
                }

                if (entry.locked && string.IsNullOrWhiteSpace(entry.unlockHint))
                {
                    issues.Add(new LintIssue(LintSeverity.Warning, "catalog.unlock_hint", name, $"locked '{entry.definition.Id}' has no unlock hint (PRD 12: show the unlock path)"));
                }

                if (!entry.locked)
                {
                    unlocked++;
                }
            }

            if (catalog.Count > 0 && unlocked == 0)
            {
                issues.Add(new LintIssue(LintSeverity.Error, "catalog.all_locked", name, "every entry is locked; the free core loop needs crackers (PRD 15.1)"));
            }

            return issues;
        }
    }
}

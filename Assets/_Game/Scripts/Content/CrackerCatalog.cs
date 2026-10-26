using System;
using System.Collections.Generic;
using UnityEngine;

namespace UtsavNights.Content
{
    /// <summary>
    /// Ordered list of crackers for the tray (F04). The tray, finale and unlock flow read this; gameplay
    /// never hard-codes which crackers exist. Locked entries show a subtle lock and their unlock path
    /// (PRD 12); unlocking (rewarded ad, purchase) arrives in Phase 4.
    /// </summary>
    [CreateAssetMenu(menuName = "Utsav Nights/Cracker Catalog", fileName = "Catalog_New")]
    public sealed class CrackerCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public CrackerDefinition definition;

            [Tooltip("Shown with a lock until unlocked (Phase 4: rewarded ad or purchase).")]
            public bool locked;

            [Tooltip("Short unlock path shown on a locked item, e.g. 'Watch an ad'.")]
            public string unlockHint = string.Empty;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;

        public int Count => entries.Count;

        internal List<Entry> MutableEntries => entries;

        public CrackerDefinition FindById(string id)
        {
            int index = IndexOf(id);
            return index >= 0 ? entries[index].definition : null;
        }

        public int IndexOf(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return -1;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i]?.definition != null && entries[i].definition.Id == id)
                {
                    return i;
                }
            }

            return -1;
        }

        public bool IsLocked(string id)
        {
            int index = IndexOf(id);
            return index >= 0 && entries[index].locked;
        }

        /// <summary>Unlocked, non-empty entries in tray order (allocates; call when the tray rebuilds).</summary>
        public List<CrackerDefinition> Available()
        {
            var result = new List<CrackerDefinition>(entries.Count);
            foreach (Entry e in entries)
            {
                if (e?.definition != null && !e.locked)
                {
                    result.Add(e.definition);
                }
            }

            return result;
        }

        /// <summary>Sets an entry's definition and lock state, adding it if missing (editor builders).</summary>
        internal void Upsert(CrackerDefinition definition, bool locked = false, string unlockHint = "")
        {
            int index = IndexOf(definition.Id);
            if (index < 0)
            {
                entries.Add(new Entry { definition = definition, locked = locked, unlockHint = unlockHint });
                return;
            }

            entries[index].definition = definition;
            entries[index].locked = locked;
            entries[index].unlockHint = unlockHint;
        }
    }
}

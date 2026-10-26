using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UtsavNights.Content;
using UtsavNights.EditorTools.ContentLint;

namespace UtsavNights.Tests
{
    public class CrackerCatalogTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _created)
            {
                Object.DestroyImmediate(o);
            }

            _created.Clear();
        }

        private CrackerDefinition Def(string id)
        {
            var d = ScriptableObject.CreateInstance<CrackerDefinition>();
            d.Id = id;
            _created.Add(d);
            return d;
        }

        private CrackerCatalog Catalog()
        {
            var c = ScriptableObject.CreateInstance<CrackerCatalog>();
            c.name = "TestCatalog";
            _created.Add(c);
            return c;
        }

        [Test]
        public void Upsert_KeepsOrder_AndUpdatesInPlace()
        {
            CrackerCatalog c = Catalog();
            c.Upsert(Def("flowerpot_gold"));
            c.Upsert(Def("rocket_basic"), locked: true, unlockHint: "Watch an ad");
            c.Upsert(Def("flowerpot_gold"));
            Assert.AreEqual(2, c.Count);
            Assert.AreEqual(0, c.IndexOf("flowerpot_gold"));
            Assert.AreEqual(1, c.IndexOf("rocket_basic"));
            Assert.IsTrue(c.IsLocked("rocket_basic"));
            Assert.AreEqual(-1, c.IndexOf("missing"));
            Assert.IsNull(c.FindById(null));
        }

        [Test]
        public void Available_SkipsLockedAndEmpty()
        {
            CrackerCatalog c = Catalog();
            c.Upsert(Def("a"));
            c.Upsert(Def("b"), locked: true, unlockHint: "x");
            c.MutableEntries.Add(new CrackerCatalog.Entry());
            List<CrackerDefinition> available = c.Available();
            Assert.AreEqual(1, available.Count);
            Assert.AreEqual("a", available[0].Id);
        }

        [Test]
        public void Linter_FlagsEmptyDuplicateAndAllLocked()
        {
            Assert.IsTrue(CatalogLinter.Lint(Catalog()).Exists(i => i.Rule == "catalog.empty"));

            CrackerCatalog dup = Catalog();
            CrackerDefinition a = Def("a");
            dup.MutableEntries.Add(new CrackerCatalog.Entry { definition = a });
            dup.MutableEntries.Add(new CrackerCatalog.Entry { definition = a });
            dup.MutableEntries.Add(new CrackerCatalog.Entry());
            List<LintIssue> issues = CatalogLinter.Lint(dup);
            Assert.IsTrue(issues.Exists(i => i.Rule == "catalog.duplicate"));
            Assert.IsTrue(issues.Exists(i => i.Rule == "catalog.slot_empty"));

            CrackerCatalog locked = Catalog();
            locked.Upsert(Def("b"), locked: true);
            issues = CatalogLinter.Lint(locked);
            Assert.IsTrue(issues.Exists(i => i.Rule == "catalog.all_locked" && i.Severity == LintSeverity.Error));
            Assert.IsTrue(issues.Exists(i => i.Rule == "catalog.unlock_hint" && i.Severity == LintSeverity.Warning));
        }

        [Test]
        public void ValidCatalog_IsClean()
        {
            CrackerCatalog c = Catalog();
            c.Upsert(Def("a"));
            c.Upsert(Def("b"), locked: true, unlockHint: "Watch an ad");
            CollectionAssert.IsEmpty(CatalogLinter.Lint(c));
        }
    }
}

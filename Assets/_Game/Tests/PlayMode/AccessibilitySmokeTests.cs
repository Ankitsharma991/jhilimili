using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Accessibility;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UtsavNights.UI;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>PRD 14 accessibility in the real Main scene: 48 dp touch targets and the TalkBack hierarchy.</summary>
    public class AccessibilitySmokeTests
    {
        [UnityTest]
        public IEnumerator EveryControlIsAtLeast48Dp()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            SettingsSheet sheet = Object.FindFirstObjectByType<SettingsSheet>();
            var failures = new List<string>();
            Audit(failures);
            sheet.Open();
            yield return null;
            Audit(failures);
            sheet.Close();
            Assert.IsEmpty(failures, "touch targets under 48 dp (" + UiTheme.MinTouch + " canvas units): " + string.Join("; ", failures));
        }

        [UnityTest]
        public IEnumerator ScreenReaderListsMainAndSettingsControls()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            ScreenReaderSupport reader = Object.FindFirstObjectByType<ScreenReaderSupport>();
            Assert.IsNotNull(reader, "screen reader support in the HUD");
            SettingsSheet sheet = Object.FindFirstObjectByType<SettingsSheet>();
            TrayView tray = Object.FindFirstObjectByType<TrayView>();

            reader.Rebuild(true);
            List<AccessibilityNode> main = reader.Hierarchy.rootNodes.ToList();
            string[] labels = main.Select(n => n.label).ToArray();
            CollectionAssert.Contains(labels, "Settings");
            CollectionAssert.Contains(labels, tray.ItemLabel(0));
            Assert.IsTrue(labels.Any(l => l.StartsWith("Finale") || l == "Replay"), "finale button: " + string.Join(", ", labels));
            Assert.IsTrue(main.Where(n => n.role == AccessibilityRole.Button).All(n => n.frame.width > 0f && n.frame.height > 0f), "buttons have screen frames");

            sheet.Open();
            yield return null;
            reader.Rebuild(true);
            List<AccessibilityNode> settings = reader.Hierarchy.rootNodes.ToList();
            AccessibilityNode quiet = settings.First(n => n.label == "Quiet mode");
            Assert.AreEqual(AccessibilityRole.Toggle, quiet.role);
            Assert.IsTrue(quiet.value == "On" || quiet.value == "Off");
            Assert.AreEqual(AccessibilityRole.Slider, settings.First(n => n.label == "Volume").role);
            StringAssert.EndsWith("percent", settings.First(n => n.label == "Volume").value);
            Assert.AreEqual(4, settings.Count(n => n.role == AccessibilityRole.TabButton), "Auto, Low, Mid, High");
            Assert.AreEqual(1, settings.Count(n => n.role == AccessibilityRole.TabButton && n.state == AccessibilityState.Selected));
            CollectionAssert.Contains(settings.Select(n => n.label).ToArray(), "Done");
            sheet.Close();
        }

        private static void Audit(List<string> failures)
        {
            foreach (Selectable control in Object.FindObjectsByType<Selectable>(FindObjectsSortMode.None))
            {
                if (!control.isActiveAndEnabled || control.targetGraphic == null || !control.targetGraphic.raycastTarget)
                {
                    continue;
                }

                var rect = (RectTransform)control.transform;
                Canvas canvas = control.GetComponentInParent<Canvas>().rootCanvas;
                float toCanvas = 1f / canvas.transform.lossyScale.x;
                Vector2 size = rect.rect.size * rect.lossyScale.x * toCanvas;
                if (size.x < UiTheme.MinTouch - 1f || size.y < UiTheme.MinTouch - 1f)
                {
                    failures.Add($"{Path(rect)} {size.x:F0}x{size.y:F0}");
                }
            }
        }

        private static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
    }
}

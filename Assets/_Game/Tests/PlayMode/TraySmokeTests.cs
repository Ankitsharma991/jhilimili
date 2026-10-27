using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;
using UtsavNights.UI;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>The tray in the real Main scene (F04): items from the catalog, select and cancel, place, height limit, blocks the lighter.</summary>
    public class TraySmokeTests
    {
        [UnityTest]
        public IEnumerator Tray_SelectPlaceAndCancel()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            TrayView tray = Object.FindFirstObjectByType<TrayView>();
            Assert.IsNotNull(tray, "tray in the HUD");
            Assert.IsTrue(Game.Services.TryGet(out PlacementController placement));
            Assert.AreEqual(9, tray.ItemCount, "eight free R0 crackers and one premium");
            Assert.AreEqual("peacock_fountain", tray.Catalog.Entries[8].definition.Id, "premium item last");

            // PRD 12: tray 20 % of the screen or less. Designed for portrait phones (width-scaled canvas);
            // the batch test window is landscape, so check the design ratio and, when portrait, the real one.
            Assert.LessOrEqual(TrayView.TrayHeight / UiTheme.ReferenceHeight, 0.2f, "design height");
            if (Screen.height > Screen.width)
            {
                Assert.LessOrEqual(tray.Panel.rect.height * tray.Panel.lossyScale.y, Screen.height * 0.2f + 1f, "on-screen height");
            }

            Vector3[] corners = new Vector3[4];
            tray.Panel.GetWorldCorners(corners);
            Vector2 trayCenter = (corners[0] + corners[2]) * 0.5f;
            Assert.IsTrue(InputBlockers.IsBlocked(trayCenter), "touches on the tray never reach the lighter");

            tray.Select(2);
            Assert.IsTrue(placement.IsPlacing);
            Assert.AreEqual(2, tray.SelectedIndex);
            StringAssert.Contains("Tap the ground", tray.HintText);

            tray.Select(2);
            Assert.IsFalse(placement.IsPlacing, "tapping the selected item again cancels");
            Assert.AreEqual(-1, tray.SelectedIndex);

            tray.Select(1);
            CrackerRuntime placed = placement.PlaceAt(Camera.main.WorldToScreenPoint(new Vector3(1.1f, 0f, 0.4f)));
            Assert.IsNotNull(placed, "placed from the tray");
            Assert.AreEqual("sparkler_classic", placed.Definition.Id, "slot 1 in tray order: flowerpot, sparkler, chakri...");
            Assert.AreEqual(-1, tray.SelectedIndex, "selection clears after placing");
        }
    }
}

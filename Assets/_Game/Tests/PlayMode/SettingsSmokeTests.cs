using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UtsavNights.Audio;
using UtsavNights.Core.Runtime;
using UtsavNights.Core.Settings;
using UtsavNights.Effects;
using UtsavNights.Gameplay;
using UtsavNights.UI;

namespace UtsavNights.Tests.PlayMode
{
    /// <summary>The settings sheet in the real Main scene (F07): open, change, apply live, save on close, tier override.</summary>
    public class SettingsSmokeTests
    {
        [UnityTest]
        public IEnumerator SettingsSheet_AppliesLiveAndSaves()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            SettingsSheet sheet = Object.FindFirstObjectByType<SettingsSheet>();
            Assert.IsNotNull(sheet, "settings sheet in the HUD");
            Assert.IsTrue(Game.Services.TryGet(out SettingsService settings));
            Assert.IsTrue(Game.Services.TryGet(out EffectsService effects));
            Assert.IsTrue(Game.Services.TryGet(out AudioService audio));
            Assert.IsTrue(Game.Services.TryGet(out QualityManager quality));
            Assert.IsTrue(Game.Services.TryGet(out PlacementController placement));
            GameSettings original = settings.Current;
            int originalTier = quality.Tier;
            bool originalManual = quality.IsManual;

            try
            {
                placement.BeginPlacing(SceneCrackers.Find("flowerpot_gold").Definition);
                sheet.Open();
                Assert.IsTrue(sheet.IsOpen);
                Assert.IsFalse(placement.IsPlacing, "opening settings cancels placement");
                Assert.IsTrue(InputBlockers.IsBlocked(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)), "the lighter is blocked while the sheet is open");

                sheet.SetQuietMode(true);
                Assert.IsTrue(settings.Current.quietMode);
                Assert.IsTrue(audio.QuietMode, "quiet mode reaches audio");
                Assert.IsFalse(effects.Flashes.Enabled, "quiet mode removes flashes");

                sheet.SetMasterVolume(0.3f);
                Assert.AreEqual(0.3f, audio.MasterVolume, 1e-4f, "slider applies live");
                Assert.IsTrue(settings.IsDirty, "slider drags are not saved every frame");

                sheet.SetTier(1);
                Assert.AreEqual(0, quality.Tier, "Low chosen manually");
                Assert.IsTrue(quality.IsManual);
                sheet.SetTier(0);
                Assert.IsFalse(quality.IsManual, "back to automatic");

                sheet.Close();
                Assert.IsFalse(sheet.IsOpen);
                Assert.IsFalse(settings.IsDirty, "closing saves");
                Assert.IsFalse(InputBlockers.IsBlocked(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)), "the scene is touchable again");
            }
            finally
            {
                settings.Change(s =>
                {
                    s.quietMode = original.quietMode;
                    s.masterVolume = original.masterVolume;
                    s.qualityTier = original.qualityTier;
                });

                // Leave the editor's saved tier exactly as it was (other tests depend on the item limit).
                quality.SetTierManually(originalTier);
                if (!originalManual)
                {
                    quality.UseAutomaticTier();
                }
            }
        }
    }
}

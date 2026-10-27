using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UtsavNights.Tests
{
    /// <summary>
    /// F09: the game works fully offline. Fails when game code starts using the network, so the change
    /// is a deliberate one (Phase 4 ads and analytics will add an allowlist entry with a reason).
    /// </summary>
    public class OfflineGuardTests
    {
        private static readonly Regex NetworkApi = new Regex(
            @"\bUnityWebRequest\b|\bSystem\.Net\b|\bHttpClient\b|\bWebClient\b|\bTcpClient\b|\bUdpClient\b|\bSocket\b|\bWWW\b|Application\.OpenURL|java\.net\.|\bUnity\.Services\.",
            RegexOptions.Compiled);

        // Files allowed to reach outside the app, each with its reason:
        // - UI/ExternalLinks.cs opens the privacy policy in the phone's browser (PRD 16); the game itself
        //   stays offline. The SDK assemblies (Scripts/Sdk) use the network only inside Google's and
        //   Unity's libraries, never through these APIs, so they need no entry.
        private static readonly string[] Allowed = { "/Scripts/UI/ExternalLinks.cs" };

        [Test]
        public void GameCodeMakesNoNetworkCalls()
        {
            string root = Path.Combine(Application.dataPath, "_Game");
            string[] offenders = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith("OfflineGuardTests.cs"))
                .Where(f => !Allowed.Any(a => f.Replace('\\', '/').Contains(a)))
                .Where(f => NetworkApi.IsMatch(StripComments(File.ReadAllText(f))))
                .Select(f => f.Substring(Application.dataPath.Length))
                .ToArray();
            Assert.IsEmpty(offenders, "network API used in game code (F09 offline): " + string.Join(", ", offenders));
        }

        [Test]
        public void InternetPermissionIsNotForced()
        {
            Assert.IsFalse(PlayerSettings.Android.forceInternetPermission, "Android INTERNET permission should only appear when needed");
        }

        private static string StripComments(string source) =>
            Regex.Replace(source, @"//[^\n]*|/\*.*?\*/", string.Empty, RegexOptions.Singleline);
    }
}

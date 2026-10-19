using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UtsavNights.EditorTools;

namespace UtsavNights.Tests
{
    /// <summary>Guards the Android player settings required by PRD Phase 1 and Appendix A.</summary>
    public class ProjectSetupTests
    {
        [OneTimeSetUp]
        public void Apply() => ProjectSetup.ApplyPlayerSettings();

        [Test]
        public void UsesIl2CppArm64()
        {
            Assert.AreEqual(ScriptingImplementation.IL2CPP, PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android));
            Assert.AreEqual(AndroidArchitecture.ARM64, PlayerSettings.Android.targetArchitectures);
        }

        [Test]
        public void PackageIdAndMinSdk()
        {
            Assert.AreEqual(ProjectSetup.ApplicationId, PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android));
            Assert.AreEqual(ProjectSetup.MinSdk, PlayerSettings.Android.minSdkVersion);
        }

        [Test]
        public void PortraitIncrementalGcAndAab()
        {
            Assert.AreEqual(UIOrientation.Portrait, PlayerSettings.defaultInterfaceOrientation);
            Assert.IsTrue(PlayerSettings.gcIncremental);
            Assert.IsTrue(EditorUserBuildSettings.buildAppBundle);
            Assert.AreEqual(ManagedStrippingLevel.Medium, PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.Android));
        }

        [Test]
        public void ThreeQualityTiersExist()
        {
            ProjectSetup.CreateQualityTiers();
            CollectionAssert.AreEqual(new[] { "Low", "Mid", "High" }, UnityEngine.QualitySettings.names);
        }
    }
}

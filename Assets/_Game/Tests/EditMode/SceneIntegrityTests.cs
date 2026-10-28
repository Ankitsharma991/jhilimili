using System.IO;
using NUnit.Framework;

namespace UtsavNights.Tests
{
    /// <summary>
    /// Scenes reference every component through its own script file. A component declared in a file with another name is
    /// saved as an embedded MonoScript that does not load (the avatar installer did this in Phase 6 and vanished at runtime).
    /// </summary>
    public class SceneIntegrityTests
    {
        [TestCase("Assets/_Game/Scenes/Main.unity")]
        [TestCase("Assets/_Game/Scenes/Boot.unity")]
        public void Scenes_HaveNoEmbeddedScripts(string path)
        {
            string text = File.ReadAllText(path);
            Assert.IsFalse(text.Contains("\nMonoScript:"), $"{path} embeds a script: move that component into a file named after its class");
        }
    }
}

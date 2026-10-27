using System.IO;

namespace UtsavNights.Gameplay
{
    /// <summary>Writes a whole file through a temp file and a replace, so a crash mid-write never leaves a broken file.</summary>
    public static class AtomicFile
    {
        public static void WriteAllText(string path, string text)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string temp = path + ".tmp";
            File.WriteAllText(temp, text);
            if (File.Exists(path))
            {
                File.Replace(temp, path, null);
            }
            else
            {
                File.Move(temp, path);
            }
        }
    }
}

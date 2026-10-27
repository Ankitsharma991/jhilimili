using System.IO;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Appends JSON lines to a log in persistentDataPath with a size cap, so local logs never grow without
    /// limit on a player's phone. Past the cap the file moves to "name.old", replacing the previous one:
    /// each log keeps at most about twice the cap. The pull scripts read the .old file first.
    /// </summary>
    public static class LocalLog
    {
        public const long DefaultMaxBytes = 4L * 1024 * 1024;

        public static string OldPath(string path) => path + ".old";

        /// <summary>Throws what File throws; callers keep their own catch so logging never breaks play.</summary>
        public static void Append(string path, string line, long maxBytes = DefaultMaxBytes)
        {
            var info = new FileInfo(path);
            if (info.Exists && info.Length + line.Length + 1 > maxBytes)
            {
                string old = OldPath(path);
                File.Delete(old);
                File.Move(path, old);
            }

            File.AppendAllText(path, line + "\n");
        }
    }
}

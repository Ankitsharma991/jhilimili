using System;
using System.IO;
using UnityEngine;
using UtsavNights.Core.Backup;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Saves and opens backup files (Phase 10 step 1) through Android's file picker (BackupFileActivity in the UtsavBackup
    /// library): no storage permission, and the game only sees the one file the player picks. In the editor a save is
    /// copied to the cache's backups/exported folder and an open reads <see cref="EditorPick"/> (tests set it).
    /// </summary>
    public static class BackupFiles
    {
        /// <summary>Editor and tests: the file an open "picks" (null cancels).</summary>
        public static string EditorPick;

        public static string CacheFolder()
        {
            string dir = Path.Combine(Application.temporaryCachePath, BackupRules.Folder);
            Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>Writes the bytes to the cache and offers "save as". The callback gets "" when saved, null when cancelled, else the reason.</summary>
        public static void Save(byte[] data, string fileName, Action<string> done)
        {
            string path = Path.Combine(CacheFolder(), fileName);
            try
            {
                File.WriteAllBytes(path, data);
            }
            catch (Exception e)
            {
                done("The backup couldn't be written: " + e.Message);
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            BackupFileReceiver.Expect(result =>
            {
                TryDelete(path);
                done(result.StartsWith("ok:") ? string.Empty : result == "cancelled" ? null : "Couldn't save the file: " + result.Substring(Math.Min(6, result.Length)));
            });
            Call("save", path, fileName);
#else
            string exported = Path.Combine(CacheFolder(), "exported");
            Directory.CreateDirectory(exported);
            File.Copy(path, Path.Combine(exported, fileName), true);
            TryDelete(path);
            Debug.Log($"[Backup] saved {fileName} to {exported}");
            done(string.Empty);
#endif
        }

        /// <summary>Lets the player pick a file. The callback gets its bytes, or null when cancelled, with the reason when it failed.</summary>
        public static void Open(Action<byte[], string> done)
        {
            string path = Path.Combine(CacheFolder(), "import" + BackupRules.Extension);
            TryDelete(path);
#if UNITY_ANDROID && !UNITY_EDITOR
            BackupFileReceiver.Expect(result =>
            {
                if (!result.StartsWith("ok:"))
                {
                    done(null, result == "cancelled" ? null : "Couldn't open the file: " + result.Substring(Math.Min(6, result.Length)));
                    return;
                }

                Read(path, done);
            });
            Call("open", path, null);
#else
            if (string.IsNullOrEmpty(EditorPick))
            {
                done(null, null);
                return;
            }

            File.Copy(EditorPick, path, true);
            Read(path, done);
#endif
        }

        private static void Read(string path, Action<byte[], string> done)
        {
            try
            {
                byte[] data = File.ReadAllBytes(path);
                TryDelete(path);
                done(data, null);
            }
            catch (Exception e)
            {
                done(null, "Couldn't read the file: " + e.Message);
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void Call(string mode, string path, string fileName)
        {
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            using var files = new AndroidJavaClass("com.ankitsharma.utsavnights.backup.BackupFileActivity");
            if (mode == "save")
            {
                files.CallStatic("save", activity, path, fileName, BackupFileReceiver.ObjectName, nameof(BackupFileReceiver.OnFileResult));
            }
            else
            {
                files.CallStatic("open", activity, path, BackupFileReceiver.ObjectName, nameof(BackupFileReceiver.OnFileResult));
            }
        }
#endif

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
            }
        }
    }
}

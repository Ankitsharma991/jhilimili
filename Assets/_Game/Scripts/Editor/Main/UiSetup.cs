using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UtsavNights.EditorTools.Main
{
    /// <summary>
    /// One-time UI prerequisites: TextMeshPro essential resources (font, shaders, TMP Settings).
    /// AssetDatabase.ImportPackage is asynchronous and never finishes in batch mode, so the
    /// .unitypackage (a gzipped tar of GUID folders) is extracted directly, keeping the original GUIDs.
    /// </summary>
    public static class UiSetup
    {
        public const string TmpFolder = "Assets/TextMesh Pro";
        public const string SettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        private const string EssentialsPackage = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";

        [MenuItem("Utsav/Setup/Import TextMeshPro Essentials", priority = 24)]
        public static void EnsureTmpEssentials()
        {
            if (File.Exists(SettingsPath))
            {
                return;
            }

            string package = Path.GetFullPath(EssentialsPackage);
            if (!File.Exists(package))
            {
                Debug.LogError($"[Utsav setup] TMP essentials not found at {package}");
                return;
            }

            int files = ExtractUnityPackage(package, Directory.GetCurrentDirectory());
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"[Utsav setup] TextMeshPro essential resources extracted ({files} assets).");
        }

        /// <summary>Extracts a .unitypackage into the project folder. Returns the number of assets written.</summary>
        public static int ExtractUnityPackage(string packagePath, string projectRoot)
        {
            var entries = new Dictionary<string, Dictionary<string, byte[]>>();
            using (var file = File.OpenRead(packagePath))
            using (var gzip = new GZipStream(file, CompressionMode.Decompress))
            {
                var header = new byte[512];
                while (ReadFully(gzip, header) == 512)
                {
                    string name = ReadString(header, 0, 100);
                    if (string.IsNullOrEmpty(name))
                    {
                        break;
                    }

                    string prefix = ReadString(header, 345, 155);
                    if (!string.IsNullOrEmpty(prefix))
                    {
                        name = prefix + "/" + name;
                    }

                    long size = System.Convert.ToInt64(ReadString(header, 124, 12).Trim(), 8);
                    var data = new byte[size];
                    ReadFully(gzip, data);
                    long padding = (512 - size % 512) % 512;
                    if (padding > 0)
                    {
                        ReadFully(gzip, new byte[padding]);
                    }

                    name = name.TrimStart('.', '/');
                    int slash = name.IndexOf('/');
                    if (header[156] == (byte)'5' || slash < 0)
                    {
                        continue;
                    }

                    string guid = name.Substring(0, slash);
                    string part = name.Substring(slash + 1);
                    if (!entries.TryGetValue(guid, out var parts))
                    {
                        parts = new Dictionary<string, byte[]>();
                        entries.Add(guid, parts);
                    }

                    parts[part] = data;
                }
            }

            int written = 0;
            foreach (var parts in entries.Values)
            {
                if (!parts.TryGetValue("pathname", out byte[] pathBytes))
                {
                    continue;
                }

                string relative = Encoding.UTF8.GetString(pathBytes).Split('\n')[0].Trim();
                string target = Path.Combine(projectRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                if (parts.TryGetValue("asset", out byte[] asset))
                {
                    File.WriteAllBytes(target, asset);
                    written++;
                }
                else
                {
                    Directory.CreateDirectory(target);
                }

                if (parts.TryGetValue("asset.meta", out byte[] meta))
                {
                    File.WriteAllBytes(target + ".meta", meta);
                }
            }

            return written;
        }

        private static int ReadFully(Stream stream, byte[] buffer)
        {
            int total = 0;
            while (total < buffer.Length)
            {
                int read = stream.Read(buffer, total, buffer.Length - total);
                if (read <= 0)
                {
                    break;
                }

                total += read;
            }

            return total;
        }

        private static string ReadString(byte[] buffer, int offset, int length)
        {
            int end = offset;
            while (end < offset + length && buffer[end] != 0)
            {
                end++;
            }

            return Encoding.ASCII.GetString(buffer, offset, end - offset);
        }
    }
}

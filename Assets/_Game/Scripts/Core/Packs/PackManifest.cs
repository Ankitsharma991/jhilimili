using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace UtsavNights.Core.Packs
{
    /// <summary>Pack types (PRD 13.4). Each has its own content files and budget caps.</summary>
    public static class PackTypes
    {
        public const string Environment = "environment";
        public const string Wardrobe = "wardrobe";
        public const string Shows = "shows";

        public static readonly string[] All = { Environment, Wardrobe, Shows };
    }

    [Serializable]
    public sealed class PackRequirement
    {
        public string pack = string.Empty;
        public string min = "1.0.0";
    }

    [Serializable]
    public sealed class PackTags
    {
        public string[] geo = new string[0];
        public string[] style = new string[0];
        public string[] festival = new string[0];
    }

    /// <summary>What the pack may cost on a phone (PRD 13.4; PRD 6: the build tool refuses oversize content).</summary>
    [Serializable]
    public sealed class PackBudget
    {
        public int maxTriangles;
        public float maxTextureMB;
        public int drawCalls;
    }

    /// <summary>The cultural reviewer and date (PRD 16: every environment pack stores them).</summary>
    [Serializable]
    public sealed class PackReview
    {
        public string advisor = string.Empty;
        public string date = string.Empty;
    }

    /// <summary>One file of a pack, hash-pinned (PRD 16: refuse mismatched files).</summary>
    [Serializable]
    public sealed class PackFile
    {
        public string path = string.Empty;
        public long sizeBytes;
        public string sha256 = string.Empty;
    }

    /// <summary>
    /// A content pack's manifest (PRD 13.4). One change from the PRD's sketch: <see cref="files"/> lists every file with its
    /// size and SHA-256, and <see cref="sha256"/> is the digest of that list, so the whole pack and each file are pinned.
    /// </summary>
    [Serializable]
    public sealed class PackManifest
    {
        public string packId = string.Empty;
        public string version = "1.0.0";
        public string type = PackTypes.Environment;
        public string title = string.Empty;
        public string description = string.Empty;
        public List<PackRequirement> requires = new List<PackRequirement>();
        public long sizeBytes;
        public string sha256 = string.Empty;
        public PackTags tags = new PackTags();
        public PackBudget budget = new PackBudget();
        public PackReview review = new PackReview();
        public List<PackFile> files = new List<PackFile>();

        public PackFile File(string path) => files.Find(f => f.path == path);
    }

    /// <summary>The catalog on the server: every pack's manifest (PRD 13.4 "a catalog hosted on static HTTPS storage").</summary>
    [Serializable]
    public sealed class PackCatalog
    {
        public int version = 1;
        public List<PackManifest> packs = new List<PackManifest>();

        public PackManifest Find(string packId) => packs.Find(p => p.packId == packId);
    }

    /// <summary>A semantic version, major.minor.patch.</summary>
    public readonly struct SemVer : IComparable<SemVer>
    {
        public SemVer(int major, int minor, int patch)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
        }

        public int Major { get; }

        public int Minor { get; }

        public int Patch { get; }

        public static bool TryParse(string text, out SemVer version)
        {
            version = default;
            string[] parts = (text ?? string.Empty).Split('.');
            if (parts.Length != 3)
            {
                return false;
            }

            var n = new int[3];
            for (int i = 0; i < 3; i++)
            {
                if (parts[i].Length == 0 || parts[i].Length > 6 || !int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out n[i]))
                {
                    return false;
                }
            }

            version = new SemVer(n[0], n[1], n[2]);
            return true;
        }

        public int CompareTo(SemVer o) => Major != o.Major ? Major.CompareTo(o.Major) : Minor != o.Minor ? Minor.CompareTo(o.Minor) : Patch.CompareTo(o.Patch);

        public override string ToString() => $"{Major}.{Minor}.{Patch}";
    }

    public static class PackHash
    {
        public static string Sha256(byte[] data)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(data ?? new byte[0]);
                var sb = new StringBuilder(64);
                foreach (byte b in hash)
                {
                    sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                }

                return sb.ToString();
            }
        }

        /// <summary>The pack digest: SHA-256 of "path size sha256" lines, one per file in order.</summary>
        public static string Digest(IEnumerable<PackFile> files)
        {
            var sb = new StringBuilder();
            foreach (PackFile f in files)
            {
                sb.Append(f.path).Append(' ').Append(f.sizeBytes.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(f.sha256).Append('\n');
            }

            return Sha256(Encoding.UTF8.GetBytes(sb.ToString()));
        }
    }

    /// <summary>Fills a manifest's file list, sizes and hashes from the files themselves (the editor's pack builder and CI).</summary>
    public static class PackBuilder
    {
        public static PackManifest Seal(PackManifest manifest, IDictionary<string, byte[]> files)
        {
            manifest.files = new List<PackFile>();
            long total = 0;
            var paths = new List<string>(files.Keys);
            paths.Sort(StringComparer.Ordinal);
            foreach (string path in paths)
            {
                byte[] data = files[path];
                manifest.files.Add(new PackFile { path = path, sizeBytes = data.LongLength, sha256 = PackHash.Sha256(data) });
                total += data.LongLength;
            }

            manifest.sizeBytes = total;
            manifest.sha256 = PackHash.Digest(manifest.files);
            return manifest;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Neighbourhood;

namespace UtsavNights.Core.Packs
{
    /// <summary>A pack's files, wherever they are (a folder on the build machine, the staging folder on a phone, memory in tests).</summary>
    public interface IPackFiles
    {
        bool Exists(string path);

        byte[] Read(string path);
    }

    /// <summary>Reads a pack's content files. JSON needs the engine, so the game supplies this; the structural checks do not.</summary>
    public interface IPackContentReader
    {
        EnvironmentDef ReadEnvironment(byte[] json);

        SettlementLayout ReadLayout(byte[] json);

        WardrobePackManifest ReadWardrobe(byte[] json);

        /// <summary>Problems with a shows file (Phase 9 step 4); empty when it is fine.</summary>
        List<string> CheckShows(byte[] json);
    }

    public sealed class MemoryPackFiles : IPackFiles
    {
        public Dictionary<string, byte[]> Files { get; } = new Dictionary<string, byte[]>();

        public bool Exists(string path) => Files.ContainsKey(path);

        public byte[] Read(string path) => Files.TryGetValue(path, out byte[] b) ? b : null;
    }

    /// <summary>
    /// The pack linter (Phase 9 step 1, PRD 6 "every content pack declares a performance budget; the build tool refuses
    /// oversize content", PRD 16 hash-pinned packs and reviewed environments). Runs in the editor, in CI, and on the phone
    /// before a downloaded pack is installed.
    /// </summary>
    public static class PackLint
    {
        /// <summary>Content files each type must have.</summary>
        public const string EnvironmentFile = "environment.json";
        public const string LayoutFile = "layout.json";
        public const string PreviewFile = "preview.png";
        public const string WardrobeFile = "wardrobe.json";
        public const string ShowsFile = "shows.json";

        /// <summary>Largest a single pack may be, in bytes (data packs are small; Play Asset Delivery is for big ones).</summary>
        public const long MaxPackBytes = 40L * 1024 * 1024;

        private static readonly Regex IdPattern = new Regex("^[a-z][a-z0-9_]{2,39}$");
        private static readonly Regex PathPattern = new Regex("^[a-z0-9_\\-]+(/[a-z0-9_\\-]+)*\\.(json|png|ogg|glb)$");
        private static readonly Regex DatePattern = new Regex("^\\d{4}-\\d{2}-\\d{2}$");

        /// <summary>The most a pack of each type may declare: triangles, texture megabytes, draw calls.</summary>
        public static (int triangles, float textureMB, int drawCalls) Caps(string type) => type switch
        {
            PackTypes.Environment => (400000, 48f, 140),
            PackTypes.Wardrobe => (60000, 16f, 40),
            _ => (0, 4f, 0),
        };

        public static List<string> Check(PackManifest m, IPackFiles files, IPackContentReader reader)
        {
            var p = new List<string>();
            if (m == null)
            {
                p.Add("no manifest");
                return p;
            }

            if (!IdPattern.IsMatch(m.packId ?? string.Empty))
            {
                p.Add($"pack id '{m.packId}' must be 3 to 40 lowercase letters, digits or '_', starting with a letter");
            }

            if (!SemVer.TryParse(m.version, out _))
            {
                p.Add($"version '{m.version}' must be major.minor.patch");
            }

            if (Array.IndexOf(PackTypes.All, m.type) < 0)
            {
                p.Add($"unknown pack type '{m.type}'");
                return p;
            }

            if (string.IsNullOrWhiteSpace(m.title))
            {
                p.Add("a pack needs a title for the store and the storage manager");
            }

            foreach (PackRequirement r in m.requires ?? new List<PackRequirement>())
            {
                if (!IdPattern.IsMatch(r.pack ?? string.Empty) || !SemVer.TryParse(r.min, out _) || r.pack == m.packId)
                {
                    p.Add($"requirement '{r.pack} {r.min}' is not a pack id and version (or requires itself)");
                }
            }

            CheckFiles(m, files, p);
            CheckBudget(m, files, p);
            if (p.Count == 0)
            {
                CheckContent(m, files, reader, p);
            }

            return p;
        }

        private static void CheckFiles(PackManifest m, IPackFiles files, List<string> p)
        {
            if (m.files == null || m.files.Count == 0)
            {
                p.Add("a pack needs files");
                return;
            }

            var seen = new HashSet<string>();
            long total = 0;
            foreach (PackFile f in m.files)
            {
                if (!PathPattern.IsMatch(f.path ?? string.Empty))
                {
                    p.Add($"file path '{f.path}' must be a lowercase relative path to a json, png, ogg or glb file");
                    continue;
                }

                if (!seen.Add(f.path))
                {
                    p.Add($"file '{f.path}' listed twice");
                }

                total += f.sizeBytes;
                byte[] data = files?.Read(f.path);
                if (data == null)
                {
                    p.Add($"file '{f.path}' is missing");
                }
                else if (data.LongLength != f.sizeBytes || PackHash.Sha256(data) != f.sha256)
                {
                    p.Add($"file '{f.path}' does not match its size and hash (tampered or damaged)");
                }
            }

            if (total != m.sizeBytes)
            {
                p.Add($"sizeBytes {m.sizeBytes} is not the sum of the files ({total})");
            }

            if (m.sizeBytes > MaxPackBytes)
            {
                p.Add($"the pack is {m.sizeBytes / 1048576f:0.0} MB; data packs stay under {MaxPackBytes / 1048576} MB");
            }

            if (PackHash.Digest(m.files) != m.sha256)
            {
                p.Add("the pack digest does not match its file list");
            }
        }

        private static void CheckBudget(PackManifest m, IPackFiles files, List<string> p)
        {
            (int triangles, float textureMB, int drawCalls) cap = Caps(m.type);
            PackBudget b = m.budget ?? new PackBudget();
            if (b.maxTriangles < 0 || b.maxTriangles > cap.triangles || b.maxTextureMB < 0f || b.maxTextureMB > cap.textureMB || b.drawCalls < 0 || b.drawCalls > cap.drawCalls)
            {
                p.Add($"budget {b.maxTriangles} triangles, {b.maxTextureMB} MB of textures, {b.drawCalls} draw calls is over the {m.type} cap " +
                      $"({cap.triangles}, {cap.textureMB} MB, {cap.drawCalls})");
            }

            float textures = 0f;
            foreach (PackFile f in m.files ?? new List<PackFile>())
            {
                byte[] data = f.path != null && f.path.EndsWith(".png", StringComparison.Ordinal) ? files?.Read(f.path) : null;
                if (data != null && TryPngSize(data, out int w, out int h))
                {
                    textures += w * h * 4f / 1048576f;
                }
                else if (data != null)
                {
                    p.Add($"'{f.path}' is not a PNG image");
                }
            }

            if (textures > b.maxTextureMB + 1e-4f)
            {
                p.Add($"textures take {textures:0.0} MB in memory; the budget declares {b.maxTextureMB} MB");
            }
        }

        private static void CheckContent(PackManifest m, IPackFiles files, IPackContentReader reader, List<string> p)
        {
            switch (m.type)
            {
                case PackTypes.Environment:
                {
                    if (string.IsNullOrWhiteSpace(m.review?.advisor) || !DatePattern.IsMatch(m.review?.date ?? string.Empty))
                    {
                        p.Add("an environment pack needs its cultural reviewer and review date (PRD 16)");
                    }

                    if (!Need(files, p, EnvironmentFile) || !Need(files, p, LayoutFile))
                    {
                        return;
                    }

                    EnvironmentDef def = reader.ReadEnvironment(files.Read(EnvironmentFile));
                    SettlementLayout layout = reader.ReadLayout(files.Read(LayoutFile));
                    if (def == null || layout == null)
                    {
                        p.Add("environment.json or layout.json cannot be read");
                        return;
                    }

                    foreach (string problem in def.Problems())
                    {
                        p.Add("environment.json: " + problem);
                    }

                    if (Environments.IsBuiltIn(def.id))
                    {
                        p.Add($"'{def.id}' is a built-in environment; a pack adds a new one");
                    }

                    if (layout.environment != def.id)
                    {
                        p.Add("layout.json belongs to another environment");
                    }

                    if (p.Count > 0)
                    {
                        return;
                    }

                    EnvironmentInfo env = def.ToInfo(m.packId, m.sizeBytes / 1048576f);
                    foreach (string problem in SettlementLint.Check(layout, null, env))
                    {
                        p.Add("layout.json: " + problem);
                    }

                    long worst = ChunkPlanner.WorstTriangles(layout, env);
                    if (worst > m.budget.maxTriangles)
                    {
                        p.Add($"the settlement draws up to {worst} triangles; the budget declares {m.budget.maxTriangles}");
                    }

                    break;
                }

                case PackTypes.Wardrobe:
                {
                    if (!Need(files, p, WardrobeFile))
                    {
                        return;
                    }

                    WardrobePackManifest wardrobe = reader.ReadWardrobe(files.Read(WardrobeFile));
                    if (wardrobe == null)
                    {
                        p.Add("wardrobe.json cannot be read");
                        return;
                    }

                    var built = new List<string>();
                    foreach (GarmentInfo g in WardrobeCatalog.Default.All)
                    {
                        built.Add(g.Id);
                    }

                    foreach (string problem in WardrobePacks.Validate(wardrobe, built))
                    {
                        p.Add("wardrobe.json: " + problem);
                    }

                    break;
                }

                case PackTypes.Shows:
                {
                    if (!Need(files, p, ShowsFile))
                    {
                        return;
                    }

                    foreach (string problem in reader.CheckShows(files.Read(ShowsFile)))
                    {
                        p.Add("shows.json: " + problem);
                    }

                    break;
                }
            }
        }

        private static bool Need(IPackFiles files, List<string> p, string path)
        {
            if (files != null && files.Exists(path))
            {
                return true;
            }

            p.Add($"this pack type needs '{path}'");
            return false;
        }

        /// <summary>Width and height from a PNG's IHDR chunk, without decoding the image.</summary>
        public static bool TryPngSize(byte[] png, out int width, out int height)
        {
            width = height = 0;
            byte[] sig = { 137, 80, 78, 71, 13, 10, 26, 10 };
            if (png == null || png.Length < 24)
            {
                return false;
            }

            for (int i = 0; i < 8; i++)
            {
                if (png[i] != sig[i])
                {
                    return false;
                }
            }

            width = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
            height = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
            return width > 0 && height > 0 && width <= 8192 && height <= 8192;
        }

        public static string Format(long bytes) =>
            bytes >= 1048576 ? (bytes / 1048576f).ToString("0.0", CultureInfo.InvariantCulture) + " MB" : (bytes / 1024f).ToString("0", CultureInfo.InvariantCulture) + " KB";
    }
}

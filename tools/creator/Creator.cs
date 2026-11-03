// The creator pack tool (Phase 11 step 4): make, check, review and submit a content pack outside Unity, with the game's
// own pack format, builder and linter (Assets/_Game/Scripts/Core/Packs). Run through tools/creator/pack.sh.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Neighbourhood;
using UtsavNights.Core.Packs;
using UtsavNights.Core.Placement;
using UtsavNights.Core.Shows;

namespace UtsavNights.Creator
{
    /// <summary>What a creator edits: the manifest's own fields (the files, sizes and hashes are filled in by build).</summary>
    public sealed class CreatorPack
    {
        public string packId = string.Empty;
        public string version = "1.0.0";
        public string type = PackTypes.Shows;
        public string title = string.Empty;
        public string description = string.Empty;
        public List<PackRequirement> requires = new List<PackRequirement>();
        public PackTags tags = new PackTags();
        public PackReview review = new PackReview();
    }

    public sealed class CrackerRow
    {
        public string id = string.Empty;
        public float fuse;
        public float show;
        public bool aerial;
    }

    public sealed class CrackerTable
    {
        public List<CrackerRow> crackers = new List<CrackerRow>();
    }

    /// <summary>Reads pack content outside Unity, checking shows exactly as the game does (with the exported cracker timings).</summary>
    public sealed class CreatorReader : IPackContentReader
    {
        private readonly Dictionary<string, CrackerTiming> _timings;

        public CreatorReader(Dictionary<string, CrackerTiming> timings) => _timings = timings;

        public EnvironmentDef ReadEnvironment(byte[] json) => Try<EnvironmentDef>(json);

        public SettlementLayout ReadLayout(byte[] json) => Try<SettlementLayout>(json);

        public WardrobePackManifest ReadWardrobe(byte[] json) => Try<WardrobePackManifest>(json);

        public List<string> CheckShows(byte[] json)
        {
            var problems = new List<string>();
            ShowLibrary library = Try<ShowLibrary>(json);
            if (library == null || library.shows.Count == 0)
            {
                problems.Add("a shows pack needs at least one show");
                return problems;
            }

            foreach (ShowDef s in library.shows)
            {
                foreach (string p in ShowRules.Check(s, id => _timings.TryGetValue(id ?? string.Empty, out CrackerTiming t) ? t : (CrackerTiming?)null, PlacementRules.ItemLimit(2), 3.5f, true))
                {
                    problems.Add($"{s.name}: {p}");
                }
            }

            return problems;
        }

        private static T Try<T>(byte[] json) where T : class, new()
        {
            try
            {
                return Json.Read<T>(Encoding.UTF8.GetString(json));
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    public static class CreatorTool
    {
        public const string PackFile = "pack.json";
        public const string ReviewFile = "REVIEW.md";
        public const string ContentFolder = "content";

        private static string Here => AppDomain.CurrentDomain.GetData("CREATOR_HOME") as string ?? Environment.GetEnvironmentVariable("CREATOR_HOME") ?? ".";

        public static int Main(string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    return Usage();
                }

                switch (args[0])
                {
                    case "new":
                        return args.Length >= 3 ? New(args[1], args[2], args.Length > 3 ? args[3] : args[2]) : Usage();
                    case "build":
                        return args.Length >= 2 ? Report(Build(args[1], Option(args, "--out"), out _)) : Usage();
                    case "lint":
                        return args.Length >= 2 ? Report(Build(args[1], null, out _, write: false)) : Usage();
                    case "review":
                        return args.Length >= 2 ? Review(args[1], Option(args, "--advisor"), Option(args, "--date") ?? DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)) : Usage();
                    case "submit":
                        return args.Length >= 2 ? Submit(args[1]) : Usage();
                    case "catalog":
                        return args.Length >= 2 ? Catalog(args[1]) : Usage();
                    case "selftest":
                        return SelfTest(args.Length >= 2 ? args[1] : null);
                    default:
                        return Usage();
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("error: " + e.Message);
                return 2;
            }
        }

        private static int Usage()
        {
            Console.WriteLine(@"Utsav Nights pack tool
  pack.sh new <environment|wardrobe|shows> <pack_id> [folder]   start a pack from a template
  pack.sh lint <folder>                                          check it, as the game will
  pack.sh build <folder> [--out <hosting folder>]                seal it (sizes, hashes) in the hosting layout
  pack.sh review <folder> --advisor ""Name"" [--date YYYY-MM-DD]   the advisor signs the review (every box ticked)
  pack.sh submit <folder>                                        check, build and zip it for the game's owner
  pack.sh catalog <hosting folder>                               (owner) rebuild catalog.json from the packs there");
            return 1;
        }

        // ---- new ----

        public static int New(string type, string packId, string folder)
        {
            if (Array.IndexOf(PackTypes.All, type) < 0)
            {
                throw new ArgumentException($"type must be one of: {string.Join(", ", PackTypes.All)}");
            }

            if (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any())
            {
                throw new ArgumentException($"{folder} isn't empty");
            }

            string content = Path.Combine(folder, ContentFolder);
            Directory.CreateDirectory(content);
            var pack = new CreatorPack { packId = packId, type = type, title = Title(packId) };
            switch (type)
            {
                case PackTypes.Shows:
                    pack.description = "Fireworks shows to start from.";
                    pack.tags.festival = new[] { "diwali", "tihar" };
                    var library = new ShowLibrary();
                    ShowDef show = ShowTemplates.All[0].Clone();
                    show.id = packId + "_1";
                    show.name = "My first show";
                    library.shows.Add(show);
                    File.WriteAllText(Path.Combine(content, PackLint.ShowsFile), Json.Write(library));
                    break;
                case PackTypes.Wardrobe:
                    pack.description = "Clothes for the avatar.";
                    var wardrobe = new WardrobePackManifest
                    {
                        id = packId, version = 1, title = pack.title, licence = "creator-001",
                        garments = new[]
                        {
                            new PackGarment
                            {
                                id = packId + "_kurta", name = "Festive kurta", slots = new[] { "BaseTop" }, covers = new[] { "Chest", "Belly", "UpperArms", "LowerArms" },
                                occasion = new[] { "Festive" }, inspiration = new[] { "SouthAsian" }, variants = 2, mesh = "generator:kurta", vertices = 1800,
                                culturalReview = "pending",
                            },
                        },
                    };
                    File.WriteAllText(Path.Combine(content, PackLint.WardrobeFile), Json.Write(wardrobe));
                    break;
                default:
                    pack.description = "A neighbourhood to celebrate in.";
                    pack.tags.festival = new[] { "diwali" };
                    NewEnvironment(packId, content);
                    break;
            }

            File.WriteAllText(Path.Combine(folder, PackFile), Json.Write(pack));
            File.WriteAllText(Path.Combine(folder, ReviewFile), ReviewTemplate(pack));
            Console.WriteLine($"Made {folder}: edit {PackFile} and the files in {ContentFolder}/, then run: pack.sh lint {folder}");
            return 0;
        }

        /// <summary>The sample neighbourhood (Docs/Phase9/sample_packs) as a starting point, renamed to the new pack.</summary>
        private static void NewEnvironment(string packId, string content)
        {
            string envId = packId.StartsWith("env_", StringComparison.Ordinal) ? packId.Substring(4) : packId;
            string sample = Path.Combine(Here, "..", "..", "Docs", "Phase9", "sample_packs", "packs", SamplePacks.BlueCityPack, "1.0.0");
            var env = (Dictionary<string, object>)Json.Parse(File.ReadAllText(Path.Combine(sample, PackLint.EnvironmentFile)));
            env["id"] = envId;
            env["name"] = Title(envId);
            File.WriteAllText(Path.Combine(content, PackLint.EnvironmentFile), Json.Write(env));
            var layout = (Dictionary<string, object>)Json.Parse(File.ReadAllText(Path.Combine(sample, PackLint.LayoutFile)));
            layout["id"] = envId;
            layout["environment"] = envId;
            File.WriteAllText(Path.Combine(content, PackLint.LayoutFile), Json.Write(layout));
            File.Copy(Path.Combine(sample, PackLint.PreviewFile), Path.Combine(content, PackLint.PreviewFile));
        }

        private static string ReviewTemplate(CreatorPack pack)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Review: {pack.title} ({pack.packId} {pack.version})");
            sb.AppendLine();
            sb.AppendLine("Tick every box (`[x]`) before the advisor signs with `pack.sh review`. A pack can't be submitted until then.");
            sb.AppendLine();
            sb.AppendLine("## Content and culture (the advisor)");
            sb.AppendLine("- [ ] Names, places and clothing are described respectfully and accurately, as \"inspired by\", never as the one true version.");
            sb.AppendLine("- [ ] No sacred imagery is used as decoration or effect art; optional religious items are marked so the hide filter can remove them.");
            sb.AppendLine("- [ ] No real brand names of crackers, shops or products.");
            sb.AppendLine("- [ ] Nothing mocks, stereotypes or excludes a community, caste, religion, region or body.");
            if (pack.type == PackTypes.Environment)
            {
                sb.AppendLine("- [ ] The houses, streets and landmarks fit the region named in the tags.");
            }

            if (pack.type == PackTypes.Wardrobe)
            {
                sb.AppendLine("- [ ] Every garment is open to every body; none is tied to a gender or a region in its name or tags.");
            }

            if (pack.type == PackTypes.Shows)
            {
                sb.AppendLine("- [ ] The shows are safe to imitate: no tricks a viewer could copy dangerously with real fireworks.");
            }

            sb.AppendLine();
            sb.AppendLine("## Rights (the creator)");
            sb.AppendLine("- [ ] Everything in the pack is my own work or clearly licensed for this use, and listed in the notes below.");
            sb.AppendLine("- [ ] The pack contains no personal data: no real people's names, faces, addresses or photos.");
            sb.AppendLine();
            sb.AppendLine("## Notes");
            sb.AppendLine();
            sb.AppendLine("(Sources, licences, anything the advisor changed.)");
            sb.AppendLine();
            return sb.ToString();
        }

        // ---- build and lint ----

        /// <summary>Seals the pack (and writes it in the hosting layout unless <paramref name="write"/> is false); returns the problems.</summary>
        public static List<string> Build(string folder, string outFolder, out PackManifest manifest, bool write = true)
        {
            CreatorPack pack = Json.Read<CreatorPack>(File.ReadAllText(Path.Combine(folder, PackFile)));
            string content = Path.Combine(folder, ContentFolder);
            var files = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (string path in Directory.GetFiles(content, "*", SearchOption.AllDirectories))
            {
                string rel = path.Substring(content.Length + 1).Replace('\\', '/');
                files[rel] = File.ReadAllBytes(path);
            }

            manifest = new PackManifest
            {
                packId = pack.packId, version = pack.version, type = pack.type, title = pack.title, description = pack.description,
                requires = pack.requires ?? new List<PackRequirement>(), tags = pack.tags ?? new PackTags(), review = pack.review ?? new PackReview(),
            };
            (int triangles, float textureMB, int drawCalls) caps = PackLint.Caps(pack.type);
            manifest.budget = new PackBudget { maxTriangles = caps.triangles, maxTextureMB = caps.textureMB, drawCalls = caps.drawCalls };
            PackBuilder.Seal(manifest, files);
            var memory = new MemoryPackFiles();
            foreach (KeyValuePair<string, byte[]> f in files)
            {
                memory.Files[f.Key] = f.Value;
            }

            List<string> problems = PackLint.Check(manifest, memory, new CreatorReader(Timings()));
            if (write && problems.Count == 0)
            {
                string root = outFolder ?? Path.Combine(folder, "build");
                string dir = Path.Combine(root, "packs", manifest.packId, manifest.version);
                Directory.CreateDirectory(dir);
                foreach (KeyValuePair<string, byte[]> f in files)
                {
                    string target = Path.Combine(dir, f.Key);
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.WriteAllBytes(target, f.Value);
                }

                File.WriteAllText(Path.Combine(dir, "manifest.json"), Json.Write(manifest));
                Console.WriteLine($"Built {manifest.packId} {manifest.version}: {manifest.files.Count} files, {PackLint.Format(manifest.sizeBytes)}, in {dir}");
            }

            return problems;
        }

        private static int Report(List<string> problems)
        {
            if (problems.Count == 0)
            {
                Console.WriteLine("No problems: the game will accept this pack.");
                return 0;
            }

            Console.WriteLine($"{problems.Count} problem(s):");
            foreach (string p in problems)
            {
                Console.WriteLine("  - " + p);
            }

            return 1;
        }

        private static Dictionary<string, CrackerTiming> Timings()
        {
            string path = Path.Combine(Here, "crackers.json");
            var table = Json.Read<CrackerTable>(File.ReadAllText(path));
            return table.crackers.ToDictionary(c => c.id, c => new CrackerTiming(c.fuse, c.show, c.aerial));
        }

        // ---- review and submit ----

        public static int Review(string folder, string advisor, string date)
        {
            if (string.IsNullOrWhiteSpace(advisor))
            {
                throw new ArgumentException("the advisor's name is needed: --advisor \"Name\"");
            }

            if (!System.Text.RegularExpressions.Regex.IsMatch(date ?? string.Empty, "^\\d{4}-\\d{2}-\\d{2}$"))
            {
                throw new ArgumentException("the date must be YYYY-MM-DD");
            }

            string reviewPath = Path.Combine(folder, ReviewFile);
            string text = File.ReadAllText(reviewPath);
            List<string> open = Unticked(text);
            if (open.Count > 0)
            {
                Console.WriteLine($"{open.Count} box(es) not ticked yet:");
                foreach (string line in open)
                {
                    Console.WriteLine("  " + line.Trim());
                }

                return 1;
            }

            string packPath = Path.Combine(folder, PackFile);
            CreatorPack pack = Json.Read<CreatorPack>(File.ReadAllText(packPath));
            pack.review = new PackReview { advisor = advisor.Trim(), date = date };
            File.WriteAllText(packPath, Json.Write(pack));
            File.AppendAllText(reviewPath, $"\nSigned: {advisor.Trim()}, {date}\n");
            Console.WriteLine($"Reviewed by {advisor.Trim()} on {date}.");
            return 0;
        }

        public static List<string> Unticked(string reviewText) =>
            reviewText.Split('\n').Where(l => l.TrimStart().StartsWith("- [ ]", StringComparison.Ordinal)).ToList();

        public static int Submit(string folder)
        {
            CreatorPack pack = Json.Read<CreatorPack>(File.ReadAllText(Path.Combine(folder, PackFile)));
            if (string.IsNullOrWhiteSpace(pack.review?.advisor) || string.IsNullOrWhiteSpace(pack.review?.date) || pack.review.advisor.StartsWith("placeholder", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Not reviewed yet: an advisor signs with pack.sh review first.");
                return 1;
            }

            if (Unticked(File.ReadAllText(Path.Combine(folder, ReviewFile))).Count > 0)
            {
                Console.WriteLine("REVIEW.md has boxes not ticked.");
                return 1;
            }

            string stage = Path.Combine(folder, "submission", "staging");
            if (Directory.Exists(stage))
            {
                Directory.Delete(stage, true);
            }

            List<string> problems = Build(folder, stage, out PackManifest manifest);
            if (problems.Count > 0)
            {
                return Report(problems);
            }

            File.Copy(Path.Combine(folder, ReviewFile), Path.Combine(stage, ReviewFile));
            string zip = Path.Combine(folder, "submission", $"{manifest.packId}-{manifest.version}.zip");
            if (File.Exists(zip))
            {
                File.Delete(zip);
            }

            ZipFile.CreateFromDirectory(stage, zip);
            Directory.Delete(stage, true);
            Console.WriteLine($"Ready: {zip} (digest {manifest.sha256.Substring(0, 12)}). Send it to the game's owner.");
            return 0;
        }

        // ---- catalog (the owner) ----

        public static int Catalog(string hosting)
        {
            var catalog = new PackCatalog();
            string packs = Path.Combine(hosting, "packs");
            foreach (string manifestPath in Directory.GetFiles(packs, "manifest.json", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
            {
                PackManifest m = Json.Read<PackManifest>(File.ReadAllText(manifestPath));
                PackManifest newer = catalog.Find(m.packId);
                if (newer == null)
                {
                    catalog.packs.Add(m);
                }
                else if (SemVer.TryParse(m.version, out SemVer a) && SemVer.TryParse(newer.version, out SemVer b) && a.CompareTo(b) > 0)
                {
                    catalog.packs[catalog.packs.IndexOf(newer)] = m;
                }
            }

            File.WriteAllText(Path.Combine(hosting, "catalog.json"), Json.Write(catalog));
            Console.WriteLine($"catalog.json: {catalog.packs.Count} pack(s).");
            return 0;
        }

        /// <summary>
        /// Makes one pack of each type, checks the review gate, signs, builds, submits and catalogues them (compile-check runs
        /// this). With a folder, the hosting layout is left there: Docs/Phase11/creator_sample is made this way, and the game's
        /// own reader checks it (CreatorSampleTests).
        /// </summary>
        public static int SelfTest(string keep)
        {
            string work = Path.Combine(Path.GetTempPath(), "utsav_creator_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(work);
            var failures = new List<string>();
            void Expect(bool ok, string what)
            {
                if (!ok)
                {
                    failures.Add(what);
                }
            }

            TextWriter console = Console.Out;
            Console.SetOut(TextWriter.Null);
            try
            {
                string hosting = keep ?? Path.Combine(work, "hosting");
                foreach ((string type, string id) in new[] { (PackTypes.Shows, "sample_shows"), (PackTypes.Wardrobe, "sample_wardrobe"), (PackTypes.Environment, "env_sample_lakeside") })
                {
                    string folder = Path.Combine(work, id);
                    Expect(New(type, id, folder) == 0, $"new {type}");
                    Expect(Submit(folder) == 1, $"{type}: submit refused before the review");
                    Expect(Review(folder, "Sample Advisor", "2026-11-03") == 1, $"{type}: review refused with boxes unticked");
                    string review = Path.Combine(folder, ReviewFile);
                    File.WriteAllText(review, File.ReadAllText(review).Replace("- [ ]", "- [x]"));
                    Expect(Review(folder, "Sample Advisor", "2026-11-03") == 0, $"{type}: review");
                    List<string> problems = Build(folder, null, out _, write: false);
                    Expect(problems.Count == 0, $"{type}: lint: {string.Join("; ", problems)}");
                    Expect(Submit(folder) == 0 && Directory.GetFiles(Path.Combine(folder, "submission"), "*.zip").Length == 1, $"{type}: submit");
                    Expect(Build(folder, hosting, out _).Count == 0, $"{type}: build");
                }

                // A damaged show and a tampered file are caught as the game would.
                string shows = Path.Combine(work, "sample_shows", ContentFolder, PackLint.ShowsFile);
                File.WriteAllText(shows, File.ReadAllText(shows).Replace("\"sparkler_classic\"", "\"no_such_cracker\""));
                Expect(Build(Path.Combine(work, "sample_shows"), null, out _, write: false).Count > 0, "an unknown cracker is refused");
                Expect(Catalog(hosting) == 0 && Json.Read<PackCatalog>(File.ReadAllText(Path.Combine(hosting, "catalog.json"))).packs.Count == 3, "catalog");
            }
            catch (Exception e)
            {
                failures.Add(e.ToString());
            }
            finally
            {
                Console.SetOut(console);
                Directory.Delete(work, true);
            }

            if (failures.Count > 0)
            {
                Console.WriteLine("CREATOR SELF-TEST FAILED\n  " + string.Join("\n  ", failures));
                return 1;
            }

            Console.WriteLine("creator self-test passed");
            return 0;
        }

        private static string Option(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        private static string Title(string id)
        {
            string words = (id.StartsWith("env_", StringComparison.Ordinal) ? id.Substring(4) : id).Replace('_', ' ');
            return words.Length == 0 ? id : char.ToUpperInvariant(words[0]) + words.Substring(1);
        }
    }
}

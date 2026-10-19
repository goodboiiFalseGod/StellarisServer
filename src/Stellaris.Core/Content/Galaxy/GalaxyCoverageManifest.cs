using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Stellaris.Core.Scripting;

namespace Stellaris.Core.Content.Galaxy;

public enum GalaxyCoverageLevel { NotAssessed, NotImplemented, Partial, Complete, Failed }
public enum GalaxyInventoryEntryKind { Definition, Variable, Metadata, InlineTemplateEntry, Configuration }
public sealed record GalaxyCoverageFile(string Source, string Sha256, long Bytes, bool Parsed, string? Diagnostic);
public sealed record GalaxyCoverageArchive(string Source, string Sha256, long Bytes, int Entries, int ScriptFiles);
public sealed record GalaxyConstructCoverage(string Name, string Operator, int Count, int FirstLine);
public sealed record GalaxyDlcGate(string Name, int Line);
public sealed record GalaxyReferenceCoverage(string Family, string Target, int Line, string Status);
public sealed record GalaxyCoverageDefinition(string Id, string Family, string Source, int Line, int TraversalIndex,
    GalaxyInventoryEntryKind Kind, GalaxyCoverageLevel Parsed, GalaxyCoverageLevel Linked,
    GalaxyCoverageLevel ConditionEvaluable, GalaxyCoverageLevel Executable,
    IReadOnlyList<GalaxyConstructCoverage> Constructs, IReadOnlyList<GalaxyDlcGate> DlcGates,
    IReadOnlyList<GalaxyReferenceCoverage> References);
public sealed record GalaxyCoverageStage(string Id, GalaxyCoverageLevel Coverage, string Note);

/// <summary>Analytical inventory: не resolved catalog и не копия vanilla scripts.</summary>
public sealed class GalaxyCoverageManifest
{
    public const string Schema = "galaxy-coverage-v1";
    public bool ResolvedCatalog => false;
    public bool VanillaContentComplete => false;
    public string Version => "v4.5.2";
    public GalaxySourceFile VersionMarker { get; }
    public string TraversalOrder => "roots(map,common,events,prescripted_countries), then dlc; recursive ordinal siblings/entry-order; не native load order";
    public IReadOnlyList<string> MissingRoots { get; }
    public IReadOnlyList<GalaxyCoverageFile> Files { get; }
    public IReadOnlyList<GalaxyCoverageArchive> Archives { get; }
    public IReadOnlyList<GalaxyCoverageDefinition> Definitions { get; }
    public IReadOnlyList<GalaxyCoverageStage> Stages { get; }
    public IReadOnlyList<string> DuplicateIds { get; }
    private readonly string root;
    private readonly IReadOnlyList<GalaxySourceFile> sourceFiles;

    private GalaxyCoverageManifest(string root, List<string> missing, List<GalaxyCoverageFile> files,
        List<GalaxyCoverageArchive> archives, List<GalaxyCoverageDefinition> definitions, List<string> duplicates,
        List<GalaxySourceFile> sourceFiles)
    {
        this.root = root;
        this.sourceFiles = sourceFiles.AsReadOnly();
        VersionMarker = sourceFiles[0];
        MissingRoots = missing.AsReadOnly(); Files = files.AsReadOnly(); Archives = archives.AsReadOnly();
        Definitions = definitions.AsReadOnly(); DuplicateIds = duplicates.AsReadOnly();
        Stages = Array.AsReadOnly(new GalaxyCoverageStage[]
        {
            new("scenario-shape-import", GalaxyCoverageLevel.Partial, "Base scenario/shape AST и used-field projections; не resolved DLC catalog."),
            new("placement-system-objects", GalaxyCoverageLevel.Complete, "Изолированный supplied-seed fresh-manager pipeline; без world integration."),
            new("relaxation", GalaxyCoverageLevel.Complete, "Triangle native-contract до2047sites и ordinary restore; без spatial caches."),
            new("candidate-hyperlane-graph", GalaxyCoverageLevel.Complete, "M13 supplied groups, actual Triangle и existing filters; не final hyperlanes."),
            new("partition-cluster-mapping", GalaxyCoverageLevel.Partial, "Cluster count есть; partition/group assignment отсутствует."),
            new("hyperlane-pruning-bridges", GalaxyCoverageLevel.NotImplemented, "Нет окончательного связного hyperlane graph."),
            new("resolved-vanilla-dlc-catalog", GalaxyCoverageLevel.NotImplemented, "Inventory не применяет DLC/override/replace_path и не доказывает closure membership."),
            new("initializer-selection-recursion", GalaxyCoverageLevel.NotImplemented, "Solar initializers/neighbor recursion/use counts."),
            new("system-content", GalaxyCoverageLevel.NotImplemented, "Star/planet/moon/orbit/anomaly/deposit content не исполняется galaxy module."),
            new("generation-script-evaluator", GalaxyCoverageLevel.NotImplemented, "Construct inventory не evaluator условий/effects."),
            new("empire-startup-late-stages", GalaxyCoverageLevel.NotImplemented, "Startup/regions/precursors/bypasses/events/spatial finalization; planet prototype не засчитывается.")
        });
    }

    public static GalaxyCoverageManifest Scan(string installationRoot)
    {
        var root = Path.GetFullPath(installationRoot);
        SafePath(root, "launcher-settings.json");
        var versionBytes = File.ReadAllBytes(Path.Combine(root, "launcher-settings.json"));
        using var version = JsonDocument.Parse(versionBytes);
        var versions = version.RootElement.EnumerateObject().Where(p => p.NameEquals("rawVersion")).ToArray();
        if (versions.Length != 1 || versions[0].Value.ValueKind != JsonValueKind.String || versions[0].Value.GetString() != "v4.5.2")
            throw new InvalidDataException("Coverage требует vanilla installation v4.5.2.");
        var sources = new List<GalaxySourceFile> { new("launcher-settings.json", Hash(versionBytes), versionBytes.LongLength) };
        var missing = new List<string>();
        var files = new List<GalaxyCoverageFile>();
        var archives = new List<GalaxyCoverageArchive>();
        var definitions = new List<GalaxyCoverageDefinition>();
        foreach (var folder in new[] { "map", "common", "events", "prescripted_countries" })
        {
            if (!Directory.Exists(Path.Combine(root, folder))) { missing.Add(folder); continue; }
            foreach (var path in Walk(root, folder).Where(p => p.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)))
            {
                var bytes = File.ReadAllBytes(SafePath(root, path));
                sources.Add(new(path, Hash(bytes), bytes.LongLength));
                Parse(path, path, bytes, files, definitions);
            }
        }
        if (!Directory.Exists(Path.Combine(root, "dlc"))) missing.Add("dlc");
        else foreach (var path in Walk(root, "dlc").Where(p => p.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)))
        {
            var absolute = SafePath(root, path);
            using var archiveStream = File.OpenRead(absolute);
            var archiveHash = Convert.ToHexString(SHA256.HashData(archiveStream));
            sources.Add(new(path, archiveHash, archiveStream.Length));
            archiveStream.Position = 0;
            using var zip = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: true);
            var scriptCount = 0;
            foreach (var entry in zip.Entries.OrderBy(e => e.FullName, StringComparer.Ordinal))
            {
                var name = entry.FullName.Replace('\\', '/');
                if (!IsScript(name)) continue;
                if (name.Split('/').Any(p => p is "" or "." or "..") || Path.IsPathRooted(name) || name.Contains(':'))
                    throw new InvalidDataException($"Некорректный DLC entry path: {path}!{name}");
                // Ограничение reader, не silent skip: oversized text явно прерывает inventory.
                if (entry.Length > 64 * 1024 * 1024) throw new InvalidDataException($"Слишком большой script: {path}!{name}");
                using var textStream = entry.Open();
                using var memory = new MemoryStream();
                textStream.CopyTo(memory);
                Parse(path + "!" + name, name, memory.ToArray(), files, definitions);
                scriptCount++;
            }
            archives.Add(new(path, archiveHash, archiveStream.Length, zip.Entries.Count, scriptCount));
        }
        var ids = definitions.Where(d => d.Kind == GalaxyInventoryEntryKind.Definition)
            .GroupBy(d => (d.Family, d.Id)).ToDictionary(g => g.Key, g => g.Count());
        var duplicates = ids.Where(p => p.Value > 1).Select(p => p.Key.Family + ":" + p.Key.Id)
            .Order(StringComparer.Ordinal).ToList();
        var scripted = definitions.Where(d => d.Family is "common/scripted_effects" or "common/scripted_triggers")
            .GroupBy(d => d.Id).ToDictionary(g => g.Key, g => g.Select(d => d.Family).Distinct().ToArray());
        for (var i = 0; i < definitions.Count; i++)
        {
            var definition = definitions[i];
            var refs = definition.References.Select(r => r with { Status =
                ids.TryGetValue((r.Family, r.Target), out var count) ? count == 1 ? "present-unresolved-profile" : "ambiguous-duplicate" : "unresolved-literal-target" }).ToList();
            foreach (var construct in definition.Constructs)
                if (scripted.TryGetValue(construct.Name, out var families))
                    foreach (var family in families) refs.Add(new(family, construct.Name, construct.FirstLine, "potential-script-call; semantic-context-unassessed"));
            definitions[i] = definition with { References = refs.AsReadOnly(),
                Linked = refs.Count == 0 ? GalaxyCoverageLevel.NotAssessed : GalaxyCoverageLevel.Partial };
        }
        var result = new GalaxyCoverageManifest(root, missing, files, archives, definitions, duplicates, sources);
        result.VerifyStable();
        return result;
    }

    public void VerifyStable()
    {
        var now = new List<string> { "launcher-settings.json" };
        var missing = new List<string>();
        foreach (var folder in new[] { "map", "common", "events", "prescripted_countries", "dlc" })
        {
            if (!Directory.Exists(Path.Combine(root, folder))) { missing.Add(folder); continue; }
            now.AddRange(Walk(root, folder).Where(p => p.EndsWith(folder == "dlc" ? ".zip" : ".txt", StringComparison.OrdinalIgnoreCase)));
        }
        if (!missing.SequenceEqual(MissingRoots) || !now.Order(StringComparer.Ordinal)
            .SequenceEqual(sourceFiles.Select(f => f.Path).Order(StringComparer.Ordinal)))
            throw new IOException("Coverage source file set изменился.");
        foreach (var source in sourceFiles)
        {
            using var stream = File.OpenRead(SafePath(root, source.Path));
            if (stream.Length != source.Bytes || Convert.ToHexString(SHA256.HashData(stream)) != source.Sha256)
                throw new IOException($"Coverage source изменился: {source.Path}");
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions
    {
        WriteIndented = true, Converters = { new JsonStringEnumConverter() }
    });

    private static bool IsScript(string name) => name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) &&
        new[] { "map/", "common/", "events/", "prescripted_countries/" }.Any(p => name.StartsWith(p, StringComparison.Ordinal));

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static IEnumerable<string> Walk(string root, string relative)
    {
        foreach (var path in Directory.EnumerateFileSystemEntries(SafePath(root, relative)).Order(StringComparer.Ordinal))
        {
            var child = Path.GetRelativePath(root, path).Replace('\\', '/');
            var absolute = SafePath(root, child);
            if (Directory.Exists(absolute)) foreach (var nested in Walk(root, child)) yield return nested;
            else yield return child;
        }
    }

    private static string SafePath(string root, string relative)
    {
        if (Path.IsPathRooted(relative) || relative.Contains(':') ||
            relative.Replace('\\', '/').Split('/').Any(p => p is "" or "." or ".."))
            throw new ArgumentException("Ожидается relative inventory path.");
        for (DirectoryInfo? parent = new(root); parent is not null; parent = parent.Parent)
            if ((parent.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Reparse inventory root запрещён.");
        var current = root;
        foreach (var part in relative.Replace('\\', '/').Split('/'))
        {
            current = Path.Combine(current, part);
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException($"Reparse inventory path запрещён: {relative}");
        }
        return current;
    }

    private static void Parse(string source, string logicalPath, byte[] bytes, List<GalaxyCoverageFile> files,
        List<GalaxyCoverageDefinition> definitions)
    {
        ScriptBlock parsed;
        try { parsed = ParadoxParser.Parse(new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF'), source); }
        catch (Exception error) when (error is FormatException or DecoderFallbackException)
        {
            files.Add(new(source, Hash(bytes), bytes.LongLength, false, error.Message));
            return;
        }
        files.Add(new(source, Hash(bytes), bytes.LongLength, true, null));
        var pieces = logicalPath.Split('/');
        var family = pieces[0] == "common" || pieces[0] == "map" ? string.Join('/', pieces.Take(2)) : pieces[0];
        foreach (var entry in parsed.Entries)
        {
            var kind = entry.Key.StartsWith('@') ? GalaxyInventoryEntryKind.Variable :
                entry.Block is null ? GalaxyInventoryEntryKind.Metadata : GalaxyInventoryEntryKind.Definition;
            if (family == "common/inline_scripts") kind = GalaxyInventoryEntryKind.InlineTemplateEntry;
            if (family == "common/defines") kind = GalaxyInventoryEntryKind.Configuration;
            var id = entry.Key;
            if (family == "map/setup_scenarios" && entry.Block is not null)
                id = entry.Block.Entries.FirstOrDefault(e => e.Key == "name")?.Scalar ?? entry.Key;
            else if (family == "events" && entry.Block is not null)
                id = entry.Block.Entries.FirstOrDefault(e => e.Key == "id")?.Scalar ?? entry.Key;
            var constructs = new Dictionary<(string Name, string Operator), (int Count, int FirstLine)>();
            var gates = new List<GalaxyDlcGate>();
            var refs = new List<GalaxyReferenceCoverage>();
            void Visit(ScriptBlock block)
            {
                foreach (var child in block.Entries)
                {
                    var key = (child.Key, child.Operator);
                    if (constructs.TryGetValue(key, out var prior)) constructs[key] = (prior.Count + 1, prior.FirstLine);
                    else constructs[key] = (1, child.Line);
                    if (child.Key is "has_dlc" or "host_has_dlc" && child.Scalar is not null)
                        gates.Add(new(child.Scalar, child.Line));
                    string? targetFamily = child.Key switch
                    {
                        "supports_shape" when IsLiteral(child.Scalar) => "map/galaxy",
                        "initializer" when IsLiteral(child.Scalar) => "common/solar_system_initializers",
                        "class" or "planet_class" when child.Scalar?.StartsWith("pc_", StringComparison.Ordinal) == true => "common/planet_classes",
                        "class" or "star_class" when child.Scalar?.StartsWith("sc_", StringComparison.Ordinal) == true => "common/star_classes",
                        "deposit" or "add_deposit" or "remove_deposit" when child.Scalar?.StartsWith("d_", StringComparison.Ordinal) == true => "common/deposits",
                        _ => null
                    };
                    if (targetFamily is not null) refs.Add(new(targetFamily, child.Scalar!, child.Line, "pending"));
                    if (child.Block is not null) Visit(child.Block);
                }
            }
            if (entry.Block is not null) Visit(entry.Block);
            definitions.Add(new(id, family, source, entry.Line, definitions.Count, kind,
                GalaxyCoverageLevel.Complete, GalaxyCoverageLevel.NotAssessed, GalaxyCoverageLevel.NotAssessed,
                kind == GalaxyInventoryEntryKind.Definition && family is "map/setup_scenarios" or "map/galaxy" ?
                    GalaxyCoverageLevel.Partial : GalaxyCoverageLevel.NotImplemented,
                Array.AsReadOnly(constructs.Select(c => new GalaxyConstructCoverage(c.Key.Name, c.Key.Operator, c.Value.Count, c.Value.FirstLine)).ToArray()),
                gates.AsReadOnly(), refs.AsReadOnly()));
        }
    }

    private static bool IsLiteral(string? value) => !string.IsNullOrEmpty(value) &&
        value is not ("random" or "none" or "this" or "root" or "prev") && !value.Contains(':') &&
        !value.Contains('$') && !value.StartsWith('@');
}

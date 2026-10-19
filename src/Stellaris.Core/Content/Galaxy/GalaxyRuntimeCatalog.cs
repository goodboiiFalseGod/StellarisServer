using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Stellaris.Core.Content.Galaxy;

public enum GalaxyRuntimeFamily { Initializer, StarClass, PlanetClass, PlanetRandomList, Variable, GlobalVariable, InlineScript }
public enum GalaxyLookupStatus { Missing, Unique, Ambiguous }
public sealed record GalaxyRuntimeDefinition(string Id, GalaxyRuntimeFamily Family,
    GalaxySourceFile File, int SourceOrder, GalaxySyntaxEntry Syntax)
{
    public GalaxySourceLocation Location => Syntax.Location;
}
public sealed record GalaxyRuntimeLookup(GalaxyLookupStatus Status, IReadOnlyList<GalaxyRuntimeDefinition> Candidates);
public sealed record GalaxyCatalogDiagnostic(string Code, string Message, GalaxySourceLocation Location);
public sealed record GalaxyContentReference(string Field, string Target, GalaxyRuntimeFamily? Family,
    string Status, GalaxySourceLocation Location);
public sealed record GalaxyRuntimeDlcGate(string Field, string Product, bool? Entitled, GalaxySourceLocation Location);

/// <summary>Профиль задаётся явно; наличие установленного файла не доказывает entitlement.</summary>
public sealed class GalaxyEntitlements
{
    public IReadOnlySet<string> Host { get; }
    public IReadOnlySet<string> Local { get; }
    public GalaxyEntitlements(IEnumerable<string> host, IEnumerable<string> local)
    {
        ArgumentNullException.ThrowIfNull(host); ArgumentNullException.ThrowIfNull(local);
        Host = host.ToFrozenSet(StringComparer.Ordinal); Local = local.ToFrozenSet(StringComparer.Ordinal);
    }
}

/// <summary>Immutable source catalog. Parsed не означает linked/executable; engine merge order не заявлен.</summary>
public sealed class GalaxyRuntimeCatalog
{
    private readonly string root;
    private static readonly string[] MembershipRoots = ["common/solar_system_initializers", "common/star_classes", "common/planet_classes", "common/scripted_variables"];
    private readonly IReadOnlySet<string> presentRoots;
    public GalaxyEntitlements Entitlements { get; }
    public IReadOnlyList<GalaxySourceFile> Files { get; }
    public IReadOnlyList<GalaxyRuntimeDefinition> Definitions { get; }
    public IReadOnlyList<GalaxyCatalogDiagnostic> Diagnostics { get; }
    public IReadOnlyList<GalaxyContentReference> References { get; }
    public IReadOnlyList<GalaxyRuntimeDlcGate> DlcGates { get; }
    public IReadOnlyList<GalaxyRuntimeDefinition> Initializers { get; }
    public IReadOnlyList<GalaxyRuntimeDefinition> StarClasses { get; }
    public IReadOnlyList<GalaxyRuntimeDefinition> StarRandomLists { get; }
    public IReadOnlyList<GalaxyRuntimeDefinition> PlanetClasses { get; }
    public IReadOnlyList<GalaxyRuntimeDefinition> PlanetRandomLists { get; }
    public IReadOnlyList<GalaxyRuntimeDefinition> Variables { get; }
    public IReadOnlyList<GalaxyRuntimeDefinition> GlobalVariables { get; }
    public IReadOnlyList<GalaxyRuntimeDefinition> InlineScripts { get; }
    public bool AllFilesParsed => !Diagnostics.Any(d => d.Code is "parse-error" or "missing-root" or "invalid-top-level" or "invalid-aggregator");
    public bool ConditionsExecutable => false;

    private GalaxyRuntimeCatalog(string root, GalaxyEntitlements entitlements, List<GalaxySourceFile> files,
        List<GalaxyRuntimeDefinition> definitions, List<GalaxyCatalogDiagnostic> diagnostics,
        List<GalaxyContentReference> references, List<GalaxyRuntimeDlcGate> gates)
    {
        this.root = root; Entitlements = entitlements;
        presentRoots = MembershipRoots.Where(folder => Directory.Exists(SafePath(root, folder))).ToFrozenSet(StringComparer.Ordinal);
        Files = Freeze(files); Definitions = Freeze(definitions); Diagnostics = Freeze(diagnostics);
        References = Freeze(references); DlcGates = Freeze(gates);
        Initializers = Family(GalaxyRuntimeFamily.Initializer); StarClasses = Family(GalaxyRuntimeFamily.StarClass);
        StarRandomLists = Freeze(StarClasses.Where(d => d.Syntax.Value.Entries.Any(e => e.Key == "stars")));
        PlanetClasses = Family(GalaxyRuntimeFamily.PlanetClass); PlanetRandomLists = Family(GalaxyRuntimeFamily.PlanetRandomList);
        Variables = Family(GalaxyRuntimeFamily.Variable); InlineScripts = Family(GalaxyRuntimeFamily.InlineScript);
        GlobalVariables = Family(GalaxyRuntimeFamily.GlobalVariable);
        IReadOnlyList<GalaxyRuntimeDefinition> Family(GalaxyRuntimeFamily family) => Freeze(definitions.Where(d => d.Family == family));
    }
    private static IReadOnlyList<T> Freeze<T>(IEnumerable<T> items) => Array.AsReadOnly(items.ToArray());

    public GalaxyRuntimeLookup Lookup(GalaxyRuntimeFamily family, string id, string? variableSource = null)
    {
        var found = Freeze(Definitions.Where(d => d.Family == family && d.Id == id &&
            (family != GalaxyRuntimeFamily.Variable || variableSource is null || d.File.Path == variableSource)));
        if (family == GalaxyRuntimeFamily.Variable && variableSource is not null && found.Count == 0)
            found = Freeze(GlobalVariables.Where(d => d.Id == id));
        return new(found.Count switch { 0 => GalaxyLookupStatus.Missing, 1 => GalaxyLookupStatus.Unique, _ => GalaxyLookupStatus.Ambiguous }, found);
    }

    public static GalaxyRuntimeCatalog Import(string installationRoot, GalaxyEntitlements entitlements)
    {
        ArgumentNullException.ThrowIfNull(entitlements);
        var root = Path.GetFullPath(installationRoot);
        var files = new List<GalaxySourceFile>(); var definitions = new List<GalaxyRuntimeDefinition>();
        var diagnostics = new List<GalaxyCatalogDiagnostic>(); var references = new List<GalaxyContentReference>();
        var gates = new List<GalaxyRuntimeDlcGate>(); var loaded = new HashSet<string>(StringComparer.Ordinal);
        var pendingInline = new SortedSet<string>(StringComparer.Ordinal);
        byte[] Read(string path)
        {
            var bytes = File.ReadAllBytes(SafePath(root, path));
            files.Add(new(path, Convert.ToHexString(SHA256.HashData(bytes)), bytes.LongLength));
            return bytes;
        }
        using (var settings = JsonDocument.Parse(Read("launcher-settings.json")))
        {
            var versions = settings.RootElement.EnumerateObject().Where(property => property.NameEquals("rawVersion")).ToArray();
            if (versions.Length != 1 || versions[0].Value.ValueKind != JsonValueKind.String || versions[0].Value.GetString() != "v4.5.2")
                throw new InvalidDataException("Galaxy runtime catalog требует Stellaris v4.5.2.");
        }
        void Observe(GalaxySyntaxEntry entry)
        {
            if (entry.Value.Kind == GalaxySyntaxKind.Scalar && entry.Value.Text is { } variable &&
                variable.StartsWith('@') && !variable.StartsWith("@[", StringComparison.Ordinal))
                references.Add(new(entry.Key ?? "<list-item>", variable, GalaxyRuntimeFamily.Variable, "pending", entry.Location));
            if (entry.Key is "host_has_dlc" or "local_has_dlc")
            {
                var product = entry.Value.Text ?? "<non-scalar>";
                bool? entitled = entry.Value.Kind == GalaxySyntaxKind.Scalar && !Dynamic(product)
                    ? (entry.Key == "host_has_dlc" ? entitlements.Host : entitlements.Local).Contains(product) : null;
                gates.Add(new(entry.Key, product, entitled, entry.Location));
                diagnostics.Add(new("condition-not-evaluated", "DLC gate observation не вычисляет enclosing condition.", entry.Location));
            }
            if (entry.Key == "inline_script")
            {
                var scripts = entry.Value.Kind == GalaxySyntaxKind.Scalar ? new[] { entry.Value } :
                    entry.Value.Entries.Where(e => e.Key == "script").Select(e => e.Value).ToArray();
                if (scripts.Length != 1 || scripts[0].Kind != GalaxySyntaxKind.Scalar)
                    references.Add(new("inline_script", "<ambiguous-or-non-scalar>", GalaxyRuntimeFamily.InlineScript, "unsupported-reference-form", entry.Location));
                else
                {
                    var target = scripts[0].Text!;
                    var status = Dynamic(target) ? "dynamic" : ValidInline(target) ? "pending" : "invalid-path";
                    references.Add(new("inline_script", target, GalaxyRuntimeFamily.InlineScript, status, entry.Location));
                    if (status == "pending") pendingInline.Add(target);
                }
            }
            var targetFamily = entry.Key switch
            {
                "initializer" => GalaxyRuntimeFamily.Initializer,
                "star_class" => GalaxyRuntimeFamily.StarClass,
                "planet_class" => GalaxyRuntimeFamily.PlanetClass,
                "class" when entry.Value.Text?.StartsWith("sc_", StringComparison.Ordinal) == true => GalaxyRuntimeFamily.StarClass,
                "class" when entry.Value.Text?.StartsWith("pc_", StringComparison.Ordinal) == true => GalaxyRuntimeFamily.PlanetClass,
                "class" when entry.Value.Text?.StartsWith("rl_", StringComparison.Ordinal) == true => GalaxyRuntimeFamily.PlanetRandomList,
                _ => (GalaxyRuntimeFamily?)null
            };
            if (targetFamily is not null)
                RecordValue(entry.Value, entry.Key!, targetFamily.Value, entry.Location);
            foreach (var child in entry.Value.Entries) Observe(child);
        }
        void RecordValue(GalaxySyntaxValue value, string field, GalaxyRuntimeFamily family, GalaxySourceLocation location)
        {
            if (value.Kind == GalaxySyntaxKind.Scalar)
                references.Add(new(field, value.Text!, family, Dynamic(value.Text!) ? "dynamic" : "pending", location));
            else
                references.Add(new(field, "<structured>", family, "structured-see-ast", location));
        }
        void Load(string path, GalaxyRuntimeFamily family, string? inlineId = null)
        {
            if (!loaded.Add(path)) return;
            var bytes = Read(path); var file = files[^1];
            GalaxySyntaxValue parsed;
            try { parsed = GalaxyScriptReader.Parse(new UTF8Encoding(false, true).GetString(bytes), path); }
            catch (Exception error) when (error is FormatException or DecoderFallbackException)
            {
                diagnostics.Add(new("parse-error", error.Message, new(path, 1, 1))); return;
            }
            if (family == GalaxyRuntimeFamily.InlineScript)
                definitions.Add(new(inlineId!, family, file, definitions.Count,
                    new(inlineId, "=", parsed, parsed.Location)));
            foreach (var entry in parsed.Entries)
            {
                if (family != GalaxyRuntimeFamily.InlineScript)
                {
                    if (entry.Key is null || entry.Operator != "=")
                        diagnostics.Add(new("invalid-top-level", "Ожидалось именованное top-level assignment.", entry.Location));
                    else
                    {
                        var kind = entry.Key.StartsWith('@') && family != GalaxyRuntimeFamily.GlobalVariable ? GalaxyRuntimeFamily.Variable : family;
                        var id = entry.Key;
                        if (family == GalaxyRuntimeFamily.PlanetClass && id == "random_list")
                        {
                            kind = GalaxyRuntimeFamily.PlanetRandomList;
                            var names = entry.Value.Entries.Where(e => e.Key == "name").ToArray();
                            if (names.Length == 1 && names[0].Value.Kind == GalaxySyntaxKind.Scalar) id = names[0].Value.Text!;
                            else { id = $"<invalid-random-list:{path}:{entry.Location.Line}>"; diagnostics.Add(new("invalid-aggregator", "random_list требует один scalar name.", entry.Location)); }
                        }
                        if (kind == GalaxyRuntimeFamily.GlobalVariable && !id.StartsWith('@'))
                            diagnostics.Add(new("invalid-top-level", "Global variable требует @ ID.", entry.Location));
                        if (kind is not (GalaxyRuntimeFamily.Variable or GalaxyRuntimeFamily.GlobalVariable) && entry.Value.Kind != GalaxySyntaxKind.Block)
                            diagnostics.Add(new("invalid-top-level", "Definition требует block.", entry.Location));
                        definitions.Add(new(id, kind, file, definitions.Count, entry));
                        if (kind == GalaxyRuntimeFamily.PlanetRandomList || kind == GalaxyRuntimeFamily.StarClass)
                            foreach (var planets in entry.Value.Entries.Where(e => e.Key == (kind == GalaxyRuntimeFamily.PlanetRandomList ? "planets" : "stars")))
                                foreach (var planet in planets.Value.Entries)
                                    RecordValue(planet.Value, planets.Key!, kind == GalaxyRuntimeFamily.PlanetRandomList ? GalaxyRuntimeFamily.PlanetClass : GalaxyRuntimeFamily.StarClass, planet.Location);
                    }
                }
                Observe(entry);
            }
        }
        foreach (var (folder, family) in new[] {
            ("common/solar_system_initializers", GalaxyRuntimeFamily.Initializer),
            ("common/star_classes", GalaxyRuntimeFamily.StarClass),
            ("common/planet_classes", GalaxyRuntimeFamily.PlanetClass),
            ("common/scripted_variables", GalaxyRuntimeFamily.GlobalVariable) })
        {
            var directory = SafePath(root, folder);
            if (!Directory.Exists(directory)) { diagnostics.Add(new("missing-root", folder, new(folder, 1, 1))); continue; }
            foreach (var path in EnumerateSafe(root, directory).Order(StringComparer.Ordinal)) Load(path, family);
        }
        while (pendingInline.Count > 0)
        {
            var id = pendingInline.Min!; pendingInline.Remove(id);
            var path = "common/inline_scripts/" + id + ".txt";
            if (loaded.Contains(path)) continue;
            if (File.Exists(SafePath(root, path))) Load(path, GalaxyRuntimeFamily.InlineScript, id);
        }
        foreach (var group in definitions.GroupBy(d => (d.Family, d.Id,
            Source: d.Family == GalaxyRuntimeFamily.Variable ? d.File.Path : "" )).Where(g => g.Count() > 1))
            foreach (var duplicate in group) diagnostics.Add(new("duplicate-id", $"Неоднозначный {group.Key.Family}: {group.Key.Id}", duplicate.Location));
        for (var i = 0; i < references.Count; i++)
        {
            var reference = references[i];
            if (reference.Status != "pending") continue;
            var candidates = definitions.Where(d => d.Id == reference.Target &&
                (d.Family == reference.Family || reference.Field == "class" && reference.Target.StartsWith("rl_", StringComparison.Ordinal) && d.Family == GalaxyRuntimeFamily.StarClass) &&
                (d.Family != GalaxyRuntimeFamily.Variable || d.File.Path == reference.Location.Source)).ToArray();
            if (reference.Family == GalaxyRuntimeFamily.Variable && candidates.Length == 0)
                candidates = definitions.Where(d => d.Id == reference.Target && d.Family == GalaxyRuntimeFamily.GlobalVariable).ToArray();
            references[i] = reference with { Family = candidates.Length == 1 ? candidates[0].Family : reference.Family,
                Status = candidates.Length switch { 0 => "unresolved-literal", 1 => "resolved", _ => "ambiguous" } };
        }
        foreach (var reference in references.Where(r => r.Status is "unresolved-literal" or "ambiguous" or "invalid-path" or "unsupported-reference-form"))
            diagnostics.Add(new("reference-" + reference.Status, $"{reference.Field}: {reference.Target}", reference.Location));
        var catalog = new GalaxyRuntimeCatalog(root, entitlements, files, definitions, diagnostics, references, gates);
        catalog.VerifyStable(); return catalog;
    }
    private static bool Dynamic(string value) => value.Contains('$') || value.StartsWith('@') || value.Contains(':') || value is "random" or "random_list";
    private static bool ValidInline(string value) => value.Length > 0 && value.Split('/').All(p => p.Length > 0 && p is not ("." or "..") && p.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-'));
    private static IEnumerable<string> EnumerateSafe(string root, string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*.txt").Order(StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/'); SafePath(root, relative); yield return relative;
        }
        foreach (var child in Directory.EnumerateDirectories(directory).Order(StringComparer.Ordinal))
        {
            SafePath(root, Path.GetRelativePath(root, child));
            foreach (var file in EnumerateSafe(root, child)) yield return file;
        }
    }
    private static string SafePath(string root, string relative)
    {
        if (Path.IsPathRooted(relative) || relative.Contains(':')) throw new ArgumentException("Ожидался relative path.");
        var current = root;
        for (var ancestor = new DirectoryInfo(root); ancestor is not null; ancestor = ancestor.Parent)
            if (ancestor.Exists && (ancestor.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Reparse point запрещён: " + ancestor.FullName);
        foreach (var part in relative.Replace('\\', '/').Split('/'))
        {
            if (part is "" or "." or ".." || part.EndsWith('.') || part.EndsWith(' ') || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("Некорректный relative path.");
            current = Path.Combine(current, part);
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Reparse point запрещён: " + current);
        }
        return current;
    }
    public void VerifyStable()
    {
        var currentPaths = new List<string>();
        foreach (var folder in MembershipRoots)
        {
            var directory = SafePath(root, folder);
            if (Directory.Exists(directory) != presentRoots.Contains(folder)) throw new IOException("Membership root изменился: " + folder);
            if (Directory.Exists(directory)) currentPaths.AddRange(EnumerateSafe(root, directory));
        }
        var originalPaths = Files.Where(file => MembershipRoots.Any(folder => file.Path.StartsWith(folder + "/", StringComparison.Ordinal)))
            .Select(file => file.Path).Order(StringComparer.Ordinal);
        if (!currentPaths.Order(StringComparer.Ordinal).SequenceEqual(originalPaths, StringComparer.Ordinal))
            throw new IOException("Source file membership изменился после import.");
        foreach (var reference in References.Where(r => r.Field == "inline_script" && ValidInline(r.Target)))
        {
            var path = "common/inline_scripts/" + reference.Target + ".txt";
            if (File.Exists(SafePath(root, path)) != Files.Any(file => file.Path == path))
                throw new IOException("Referenced inline membership изменился: " + path);
        }
        foreach (var file in Files)
        {
            var bytes = File.ReadAllBytes(SafePath(root, file.Path));
            if (bytes.LongLength != file.Bytes || Convert.ToHexString(SHA256.HashData(bytes)) != file.Sha256)
                throw new IOException("Источник изменился после import: " + file.Path);
        }
    }
}

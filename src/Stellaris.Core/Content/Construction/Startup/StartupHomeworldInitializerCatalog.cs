using System.Security.Cryptography;
using System.Text;
using Stellaris.Core.Scripting;

namespace Stellaris.Core.Content.Construction.Startup;

public sealed record StartupHomeworldSelection(string InitializerId, string PlanetClass, int PlanetSize);

public sealed class StartupHomeworldInitializerCatalog
{
    private readonly IReadOnlyList<Candidate> _candidates;
    public IReadOnlyList<StartupSourceFile> Sources { get; }
    public int MinimumSize { get; }
    public int MaximumSize { get; }
    public string SelectionBasis => "Равномерный выбор среди полных source-кандидатов без usage_odds; это вывод из источника.";

    internal StartupHomeworldInitializerCatalog(IEnumerable<Candidate> candidates, IEnumerable<StartupSourceFile> sources)
    {
        _candidates = Array.AsReadOnly(candidates.ToArray());
        MinimumSize = _candidates.Min(item => item.MinimumSize);
        MaximumSize = _candidates.Max(item => item.MaximumSize);
        Sources = Array.AsReadOnly(sources.OrderBy(item => item.RelativePath, StringComparer.Ordinal).ToArray());
    }

    public StartupHomeworldSelection Select(string planetClass, int seed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planetClass);
        if (_candidates.Count == 0) { throw new InvalidOperationException("The imported initializer pool is empty."); }
        var random = new Random(seed);
        var candidate = _candidates[random.Next(_candidates.Count)];
        var size = random.Next(candidate.MinimumSize, checked(candidate.MaximumSize + 1));
        return new(candidate.Id, planetClass, size);
    }

    internal sealed record Candidate(string Id, int MinimumSize, int MaximumSize);
}

/// <summary>Импортирует полный fallback pool empire_init и bounds домашней планеты из оригинала.</summary>
public sealed class StartupHomeworldInitializerImporter
{
    private const int MaxFileBytes = 2 * 1024 * 1024;
    private readonly Func<CancellationToken, Task>? _beforeVerification;

    public StartupHomeworldInitializerImporter() { }
    internal StartupHomeworldInitializerImporter(Func<CancellationToken, Task> beforeVerification) => _beforeVerification = beforeVerification ?? throw new ArgumentNullException(nameof(beforeVerification));

    public async Task<StartupHomeworldInitializerCatalog> ImportAsync(string gameRoot, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameRoot);
        var root = Path.GetFullPath(gameRoot);
        var sourcePath = Path.Combine(root, "common", "solar_system_initializers", "empire_initializers.txt");
        if (!File.Exists(sourcePath)) { throw new StartupBuildingImportException("Required empire initializer source is missing."); }
        var paths = new[] { sourcePath };
        var files = new List<(string Relative, byte[] Bytes, string Hash, ScriptBlock Ast)>();
        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            if (bytes.Length > MaxFileBytes) { throw new StartupBuildingImportException("Initializer source exceeds bounded import size."); }
            string text;
            try { text = new UTF8Encoding(false, true).GetString(bytes); }
            catch (DecoderFallbackException exception) { throw new StartupBuildingImportException("Initializer source is not UTF-8: " + exception.Message); }
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            ScriptBlock ast;
            try { ast = ParadoxParser.Parse(text, relative); }
            catch (FormatException exception) { throw new StartupBuildingImportException("Malformed initializer source: " + exception.Message); }
            files.Add((relative, bytes, Convert.ToHexString(SHA256.HashData(bytes)), ast));
        }

        var variables = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var entry in files.SelectMany(file => file.Ast.Entries).Where(entry => entry.Key.StartsWith("@", StringComparison.Ordinal) && entry.Operator == "=" && entry.Scalar is not null))
        {
            var key = entry.Key[1..];
            if (!int.TryParse(entry.AsScalar(), out var value) || !variables.TryAdd(key, value)) { throw new StartupBuildingImportException("Malformed or duplicate initializer variable " + entry.Key + "."); }
        }
        var candidates = new List<StartupHomeworldInitializerCatalog.Candidate>();
        foreach (var file in files)
        {
            foreach (var entry in file.Ast.Entries.Where(entry => entry.Operator == "=" && entry.Block is not null))
            {
                var usage = entry.Block!.Named("usage").Where(item => item.Operator == "=" && item.Scalar is not null).ToArray();
                if (!usage.Any(item => item.AsScalar() == "empire_init")) { continue; }
                if (usage.Length != 1) { throw new StartupBuildingImportException($"{file.Relative}:{entry.Line}: ambiguous initializer usage."); }
                if (entry.Block.Named("usage_odds").Any()) { throw new StartupBuildingImportException("Fallback pool has a new explicit usage_odds; review selection policy."); }
                var planets = Descendants(entry.Block).Where(item => item.Key is "planet" or "moon" && item.Block is not null && item.Block.Optional("home_planet")?.AsScalar() == "yes").ToArray();
                if (planets.Length != 1) { throw new StartupBuildingImportException($"{file.Relative}:{entry.Line}: expected one home planet in empire_init candidate."); }
                var planet = planets[0].Block!;
                if (planet.Optional("class")?.AsScalar() != "ideal_planet_class") { throw new StartupBuildingImportException($"{file.Relative}:{entry.Line}: unsupported homeworld class selector."); }
                var size = planet.Optional("size")?.AsBlock() ?? throw new StartupBuildingImportException($"{file.Relative}:{entry.Line}: missing homeworld size bounds.");
                var min = ResolveBound(size.Required("min").AsScalar(), variables);
                var max = ResolveBound(size.Required("max").AsScalar(), variables);
                if (min is < 1 or > 100 || max < min || max > 100) { throw new StartupBuildingImportException($"{file.Relative}:{entry.Line}: invalid homeworld size bounds."); }
                candidates.Add(new(entry.Key, min, max));
            }
        }
        if (candidates.Count != 6 || candidates.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != 6) { throw new StartupBuildingImportException("Expected the complete six-candidate 4.5.2 empire_init fallback pool."); }
        if (candidates.Any(item => item.MinimumSize != candidates[0].MinimumSize || item.MaximumSize != candidates[0].MaximumSize)) { throw new StartupBuildingImportException("Fallback homeworld candidates no longer share size bounds."); }

        if (_beforeVerification is not null) { await _beforeVerification(cancellationToken); }
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = await File.ReadAllBytesAsync(Path.Combine(root, file.Relative.Replace('/', Path.DirectorySeparatorChar)), cancellationToken);
            if (current.LongLength != file.Bytes.LongLength || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(current), Convert.FromHexString(file.Hash))) { throw new StartupBuildingImportException("Initializer source changed during import: " + file.Relative); }
        }
        return new StartupHomeworldInitializerCatalog(candidates, files.Select(file => new StartupSourceFile(file.Relative, file.Bytes.LongLength, file.Hash)));
    }

    private static int ResolveBound(string raw, IReadOnlyDictionary<string, int> variables)
    {
        var name = raw.StartsWith('@') ? raw[1..] : raw;
        if (int.TryParse(name, out var literal)) { return literal; }
        return variables.TryGetValue(name, out var value) ? value : throw new StartupBuildingImportException("Unresolved homeworld size variable " + raw + ".");
    }

    private static IEnumerable<ScriptEntry> Descendants(ScriptBlock block)
    {
        foreach (var entry in block.Entries)
        {
            yield return entry;
            if (entry.Block is not null)
            {
                foreach (var nested in Descendants(entry.Block)) { yield return nested; }
            }
        }
    }
}

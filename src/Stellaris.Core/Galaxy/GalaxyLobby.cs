using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Stellaris.Core.Content.Galaxy;
using Stellaris.Core.Scripting;

namespace Stellaris.Core.Galaxy;

public enum GalaxyContentProfile { PrototypeOrdinaryV1, VanillaComplete }
public sealed record GalaxyPlayerSlot(int ParticipantId, string StartupProfileId, string IdealPlanetClass,
    string? InitializerId = null);
public sealed record GalaxySpecialContent(int AiEmpires = 0, int AdvancedEmpires = 0, int FallenEmpires = 0,
    int MarauderEmpires = 0, int NomadEmpires = 0, decimal PrimitiveOdds = 0, decimal Gateways = 0,
    int WormholePairs = 0, int Nebulae = 0, int GuaranteedColonies = 0, bool LGates = false, bool Caravaneers = false)
{
    internal bool Disabled => this == new GalaxySpecialContent();
}
public sealed class GalaxyLobbyRequest
{
    public string? Seed { get; }
    public string ScenarioId { get; }
    public string ShapeId { get; }
    public decimal HyperlaneDensity { get; }
    public decimal HabitableMultiplier { get; }
    public GalaxyContentProfile Profile { get; }
    public IReadOnlyList<GalaxyPlayerSlot> Players { get; }
    public GalaxySpecialContent SpecialContent { get; }
    public GalaxyLobbyRequest(string? seed, string scenarioId, string shapeId, decimal hyperlaneDensity,
        IEnumerable<GalaxyPlayerSlot> players, decimal habitableMultiplier = 1,
        GalaxyContentProfile profile = GalaxyContentProfile.PrototypeOrdinaryV1, GalaxySpecialContent? specialContent = null)
    {
        Seed = seed; ScenarioId = scenarioId; ShapeId = shapeId; HyperlaneDensity = hyperlaneDensity;
        HabitableMultiplier = habitableMultiplier; Profile = profile;
        Players = Array.AsReadOnly(players.ToArray()); SpecialContent = specialContent ?? new();
    }
}
public sealed record GalaxyOptionRange(decimal Minimum, decimal Maximum, decimal Default);
public sealed record GalaxyScenarioOptions(string Id, int Stars, decimal Radius, IReadOnlyList<string> Shapes,
    GalaxyOptionRange Empires, GalaxyOptionRange HyperlaneDensity, GalaxyOptionRange Gateways,
    GalaxyOptionRange WormholePairs, int FallenDefault, int MarauderDefault, int NomadDefault,
    int AdvancedDefault, int NebulaeDefault);
public sealed record GalaxyApiFailure(string Stage, string Code, string Detail);
public sealed record GalaxyResolveResult(ResolvedGalaxyRequest? Request, GalaxyApiFailure? Failure)
{
    public bool Success => Request is not null && Failure is null;
}
public sealed class GalaxySeedBundle
{
    public const string Version = "galaxy-seed-bundle-v1";
    public string UserSeed { get; }
    public int Galaxy { get; }
    public uint PruneA { get; }
    public uint PruneB { get; }
    public int Script0 { get; }
    public int Script1 { get; }
    public int Names { get; }
    public int Content { get; }
    public int GatewayPosition { get; }
    public int WormholeSelection { get; }
    public int WormholePlacement { get; }
    public GalaxySeedBundle(string userSeed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userSeed);
        if (Encoding.UTF8.GetByteCount(userSeed) > 4096) throw new ArgumentException("Seed превышает4096 UTF8 bytes.");
        UserSeed = userSeed;
        int Domain(string domain)
        {
            using var stream = new MemoryStream();
            Span<byte> length = stackalloc byte[4];
            foreach (var part in new[] { Version, domain, userSeed })
            {
                var bytes = Encoding.UTF8.GetBytes(part);
                BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length); stream.Write(length); stream.Write(bytes);
            }
            return (int)(BinaryPrimitives.ReadUInt32LittleEndian(SHA256.HashData(stream.ToArray())) & 0x7FFFFFFF);
        }
        Galaxy = Domain("galaxy"); PruneA = (uint)Domain("prune-a"); PruneB = (uint)Domain("prune-b");
        Script0 = Domain("script-0"); Script1 = Domain("script-1"); Names = Domain("names"); Content = Domain("content");
        GatewayPosition = Domain("gateway-position-v1");
        WormholeSelection = Domain("wormhole-selection-v1");
        WormholePlacement = Domain("wormhole-placement-v1");
    }
}
public sealed class ResolvedGalaxyRequest
{
    public GalaxyLobbyRequest Settings { get; }
    public GalaxySetupPlan Plan { get; }
    public GalaxySeedBundle Seeds { get; }
    public string ContentFingerprint { get; }
    public string SettingsFingerprint { get; }
    internal ResolvedGalaxyRequest(GalaxyLobbyRequest settings, GalaxySetupPlan plan, string content)
    {
        Settings = settings; Plan = plan; Seeds = new(settings.Seed!); ContentFingerprint = content;
        SettingsFingerprint = GalaxyLobbyCatalog.Hash(JsonSerializer.Serialize(new {
            version = "galaxy-settings-v2", seed = Seeds.UserSeed, settings.ScenarioId, settings.ShapeId,
            density = settings.HyperlaneDensity.ToString("G29", CultureInfo.InvariantCulture),
            habitable = settings.HabitableMultiplier.ToString("G29", CultureInfo.InvariantCulture),
            profile = settings.Profile.ToString(), players = settings.Players, settings.SpecialContent, content }));
    }
}

/// <summary>Source options и explicit prototype preset; special defaults не выдаются за исполненные.</summary>
public sealed class GalaxyLobbyCatalog
{
    private readonly string root;
    private readonly string[] scenarioPaths, shapePaths;
    private readonly GalaxySourceFile definesFile;
    private readonly GalaxyNebulaNamesSource nebulaNames;
    public GalaxyBeltSource Belts { get; }
    public GalaxyWormholeSource Wormholes { get; }
    public GalaxySourceFile? NebulaNamesFile => nebulaNames.File;
    public GalaxyCatalog Geometry { get; }
    public GalaxyRuntimeCatalog Content { get; }
    public IReadOnlyList<GalaxyScenarioOptions> Scenarios { get; }
    public string ContentFingerprint { get; }
    public RelaxationAdapterSettings Relaxation { get; }
    public long RemovalPercent { get; }
    public int DegreeBase { get; }
    public static IReadOnlyList<string> OrdinaryInitializers { get; } = Array.AsReadOnly(
        Enumerable.Range(1, 6).Select(i => $"basic_init_{i:00}")
        .Concat(["asteroid_init_01", "binary_init_01", "binary_init_02", "trinary_init_01", "trinary_init_02"]).ToArray());
    public static IReadOnlyList<string> HomeInitializers { get; } = Array.AsReadOnly(
        Enumerable.Range(1, 6).Select(i => $"random_empire_init_{i:00}").ToArray());

    private GalaxyLobbyCatalog(string installationRoot, GalaxyEntitlements entitlements)
    {
        root = Path.GetFullPath(installationRoot);
        scenarioPaths = Paths("map/setup_scenarios"); shapePaths = Paths("map/galaxy");
        Geometry = GalaxyCatalog.Import(root, scenarioPaths, shapePaths);
        Content = GalaxyRuntimeCatalog.Import(root, entitlements);
        nebulaNames = new(root);
        Belts = new(root);
        Wormholes = new(root);
        var defines = ReadDefines(); definesFile = defines.File;
        var syntax = GalaxyScriptReader.Parse(new UTF8Encoding(false, true).GetString(defines.Bytes), definesFile.Path);
        decimal Define(string name)
        {
            IEnumerable<GalaxySyntaxEntry> Walk(GalaxySyntaxValue node) => node.Entries.SelectMany(e => new[] { e }.Concat(Walk(e.Value)));
            var entries = Walk(syntax).Where(e => e.Key == name).ToArray();
            if (entries.Length != 1) throw new InvalidDataException("Define неоднозначен: " + name);
            return decimal.Parse(entries[0].Value.Text!, NumberStyles.Number, CultureInfo.InvariantCulture);
        }
        Relaxation = new(checked((int)Define("GALAXY_GENERATION_CENTER_DUMMY_POINTS")),
            checked((int)Define("GALAXY_GENERATION_RELAX_ITERATIONS")));
        RemovalPercent = Fixed(Define("HYPERLANE_GEN_REMOVE_PERC"));
        DegreeBase = checked((int)Define("HYPERLANE_GEN_REMOVE_TOO_MANY_EDGES"));
        Scenarios = Array.AsReadOnly(Geometry.Scenarios.OrderBy(s => GalaxyCatalog.Number(s.Fields.Required("priority"))).ThenBy(s => s.Id, StringComparer.Ordinal).Select(s => {
            var f = s.Fields;
            decimal N(string key) => GalaxyCatalog.Number(f.Required(key));
            int NebulaCount()
            {
                var value = N("num_nebulas");
                if (value < 0 || value != decimal.Truncate(value)) throw new InvalidDataException("num_nebulas должен быть неотрицательным целым.");
                return checked((int)value);
            }
            GalaxyOptionRange R(string key, string defaultKey)
            {
                var b = f.Required(key).AsBlock();
                return new(GalaxyCatalog.Number(b.Required("min")), GalaxyCatalog.Number(b.Required("max")), N(defaultKey));
            }
            return new GalaxyScenarioOptions(s.Id, checked((int)N("num_stars")), N("radius"),
                Array.AsReadOnly(f.Named("supports_shape").Select(GalaxyCatalog.Scalar).ToArray()),
                R("num_empires", "num_empire_default"), R("num_hyperlanes", "num_hyperlanes_default"),
                R("num_gateways", "num_gateways_default"), R("num_wormhole_pairs", "num_wormhole_pairs_default"),
                (int)N("fallen_empire_default"), (int)N("marauder_empire_default"), (int)N("nomad_empire_default"),
                (int)N("advanced_empire_default"), NebulaCount());
        }).ToArray());
        var files = Geometry.Files.Concat(Content.Files).Append(definesFile).Concat(nebulaNames.File is { } namesFile ? [namesFile] : []).Concat(Belts.File is { } beltsFile ? [beltsFile] : []).Concat(Wormholes.Files).GroupBy(f => f.Path, StringComparer.Ordinal)
            .Select(g => g.First()).OrderBy(f => f.Path, StringComparer.Ordinal);
        ContentFingerprint = Hash(JsonSerializer.Serialize(new { version = "galaxy-content-v1", files,
            host = entitlements.Host.Order(StringComparer.Ordinal), local = entitlements.Local.Order(StringComparer.Ordinal) }));
        VerifyStable();
    }
    public static GalaxyLobbyCatalog Import(string root, GalaxyEntitlements entitlements) => new(root, entitlements);

    public GalaxyResolveResult Resolve(GalaxyLobbyRequest request)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(request); VerifyStable();
            if (request.Profile != GalaxyContentProfile.PrototypeOrdinaryV1)
                return Fail("unsupported-profile", "VanillaComplete требует ещё не реализованных effect/startup/special stages.");
            if (!(request.SpecialContent with { Nebulae = 0, GuaranteedColonies = 0, Gateways = 0, WormholePairs = 0 }).Disabled) return Fail("unsupported-special-content", "PrototypeOrdinaryV1 требует отключённых прочих special families.");
            var option = Scenarios.SingleOrDefault(s => s.Id == request.ScenarioId);
            if (option is null || !option.Shapes.Contains(request.ShapeId, StringComparer.Ordinal))
                return Fail("scenario-shape", "Неизвестный scenario или несовместимая shape.");
            if (request.SpecialContent.Nebulae < 0 || request.SpecialContent.Nebulae > option.NebulaeDefault)
                return Fail("nebula-count", "Count вне source range0..num_nebulas.");
            if (request.SpecialContent.GuaranteedColonies is < 0 or > 2)
                return Fail("guaranteed-colonies", "GuaranteedColonies допускает только0,1,2.");
            if (request.SpecialContent.Gateways is < 0 or > 5)
                return Fail("gateway-factor", "Gateway frequency должна быть в диапазоне0..5.");
            if (request.SpecialContent.WormholePairs is < 0 or > 5)
                return Fail("wormhole-factor", "Wormhole pairs должны быть в диапазоне0..5.");
            var plan = GalaxyPreparation.Prepare(Geometry, request.ScenarioId, request.ShapeId);
            try
            {
                _ = NebulaParameters(plan, request.SpecialContent.Nebulae > 0);
                if (request.SpecialContent.Nebulae > 0) _ = nebulaNames.RequireNames();
            }
            catch (Exception error) when (error is IOException or InvalidDataException or FormatException or ArgumentException or OverflowException)
            { return Fail("invalid-nebula-source", error.Message); }
            if (request.HyperlaneDensity < option.HyperlaneDensity.Minimum || request.HyperlaneDensity > option.HyperlaneDensity.Maximum)
                return Fail("density", "Density вне source range.");
            if (request.SpecialContent.Gateways < option.Gateways.Minimum || request.SpecialContent.Gateways > option.Gateways.Maximum)
                return Fail("gateway-factor", "Gateway frequency вне source range сценария.");
            if (request.SpecialContent.WormholePairs < option.WormholePairs.Minimum || request.SpecialContent.WormholePairs > option.WormholePairs.Maximum)
                return Fail("wormhole-factor", "Wormhole pairs вне source range сценария.");
            if (request.SpecialContent.WormholePairs > 0)
            {
                try { Wormholes.Require(); }
                catch (Exception error) when (error is IOException or InvalidDataException or FormatException or ArgumentException or OverflowException)
                { return Fail("invalid-wormhole-source", error.Message); }
            }
            _ = Fixed(request.SpecialContent.Gateways);
            _ = Fixed(request.HyperlaneDensity); _ = Fixed(request.HabitableMultiplier);
            if (request.HabitableMultiplier < 0 || request.HabitableMultiplier > 100)
                return Fail("habitable-multiplier", "Prototype domain0..100; required fixed precision5.");
            if (request.Players.Count < 1 || request.Players.Count > option.Stars ||
                request.Players.Any(p => p is null || p.ParticipantId <= 0 || string.IsNullOrWhiteSpace(p.StartupProfileId)) ||
                request.Players.Select(p => p.ParticipantId).Distinct().Count() != request.Players.Count)
                return Fail("players", "Нужны уникальные положительные slots и nonempty startup profile; число игроков должно быть от 1 до числа систем сценария.");
            foreach (var player in request.Players)
            {
                var planet = Content.Lookup(GalaxyRuntimeFamily.PlanetClass, player.IdealPlanetClass);
                if (planet.Status != GalaxyLookupStatus.Unique || GalaxyInitializerExecution.Scalar(planet.Candidates[0].Syntax.Value, "initial") != "yes" ||
                    !Potential(planet.Candidates[0])) return Fail("ideal-class", "Недоступный initial planet class.");
                if (player.InitializerId is not null && !HomeInitializers.Contains(player.InitializerId, StringComparer.Ordinal))
                    return Fail("home-initializer", "Prototype поддерживает шесть generic home initializers.");
            }
            ValidateGenericNeighborTree();
            var seed = string.IsNullOrWhiteSpace(request.Seed) ? Guid.NewGuid().ToString("N") : request.Seed;
            var settings = new GalaxyLobbyRequest(seed, request.ScenarioId, request.ShapeId, request.HyperlaneDensity,
                request.Players, request.HabitableMultiplier, request.Profile, request.SpecialContent);
            return new(new(settings, plan, ContentFingerprint), null);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or ArgumentException or FormatException or OverflowException or NotSupportedException or GalaxyStructureException)
        { return Fail("invalid-request-or-source", error.Message); }
        static GalaxyResolveResult Fail(string code, string detail) => new(null, new("resolve", code, detail));
    }
    private void ValidateGenericNeighborTree()
    {
        var pending = new Queue<string>(HomeInitializers);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        while (pending.TryDequeue(out var id))
        {
            if (!visited.Add(id)) continue;
            if (visited.Count > 32) throw new NotSupportedException("neighbor-tree-limit: generic home initializer closure exceeds32 nodes.");
            var compiled = GalaxyInitializerExecution.Compile(Content, id);
            foreach (var descriptor in GalaxyGuaranteedNeighbors.Compile(Content, compiled))
            {
                var target = Content.Lookup(GalaxyRuntimeFamily.Initializer, descriptor.InitializerId);
                if (target.Status != GalaxyLookupStatus.Unique)
                    throw new GalaxyStructureException("neighbor-initializer", descriptor.InitializerId + ": " + target.Status, descriptor.Source);
                pending.Enqueue(descriptor.InitializerId);
            }
        }
    }
    internal bool Potential(GalaxyRuntimeDefinition definition)
    {
        var potential = GalaxyInitializerExecution.Optional(definition.Syntax.Value, "potential");
        if (potential is null) return true;
        if (potential.Value.Kind != GalaxySyntaxKind.Block || potential.Value.Entries.Any(e => e.Operator != "=" || e.Value.Kind != GalaxySyntaxKind.Scalar))
            throw new NotSupportedException("Class potential должен быть literal predicate block.");
        return potential.Value.Entries.All(e => e.Key switch {
            "host_has_dlc" => Content.Entitlements.Host.Contains(e.Value.Text!),
            "local_has_dlc" => Content.Entitlements.Local.Contains(e.Value.Text!),
            _ => throw new NotSupportedException($"Class potential {e.Key} ({e.Location})") });
    }
    public void VerifyStable()
    {
        Geometry.VerifyStable(); Content.VerifyStable();
        nebulaNames.VerifyStable();
        Belts.VerifyStable();
        Wormholes.VerifyStable();
        if (!scenarioPaths.SequenceEqual(Paths("map/setup_scenarios")) || !shapePaths.SequenceEqual(Paths("map/galaxy")) || ReadDefines().File != definesFile)
            throw new IOException("Galaxy source membership/defines изменились после импорта.");
    }
    internal IReadOnlyList<string> NebulaNames => nebulaNames.RequireNames();
    internal IReadOnlyList<string> StarNames => nebulaNames.RequireStarNames();
    internal static (long Radius, long MinimumSeparation) NebulaParameters(GalaxySetupPlan plan, bool active = true)
    {
        var radius = Fixed(GalaxyCatalog.Number(plan.Scenario.Fields.Required("nebula_size")));
        var separation = Fixed(GalaxyCatalog.Number(plan.Scenario.Fields.Required("nebula_min_dist")));
        if (radius < 0 || active && radius == 0 || separation < 0) throw new InvalidDataException("Неверные nebula_size/nebula_min_dist.");
        return (radius, separation);
    }
    private string[] Paths(string directory) => Directory.EnumerateFiles(Path.Combine(root, directory), "*.txt")
        .Select(p => Path.GetRelativePath(root, p).Replace('\\', '/')).Order(StringComparer.Ordinal).ToArray();
    private (byte[] Bytes, GalaxySourceFile File) ReadDefines()
    {
        const string relative = "common/defines/00_defines.txt";
        var path = Path.Combine(root, relative);
        for (FileSystemInfo? part = new FileInfo(path); part is not null; part = part is FileInfo f ? f.Directory : ((DirectoryInfo)part).Parent)
            if ((part.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Reparse defines path запрещён.");
        var bytes = File.ReadAllBytes(path);
        return (bytes, new(relative, Convert.ToHexString(SHA256.HashData(bytes)), bytes.LongLength));
    }
    internal static long Fixed(decimal value)
    {
        var raw = checked(value * 100000);
        if (raw != decimal.Truncate(raw)) throw new ArgumentException("Требуется fixed precision≤5.");
        return checked((long)raw);
    }
    internal static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}

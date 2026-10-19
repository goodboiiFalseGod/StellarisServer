using System.Collections.Frozen;
using System.Text.RegularExpressions;
using Stellaris.Core.Content.Galaxy;

namespace Stellaris.Core.Galaxy;

public enum GalaxyBodyKind { Planet, Moon, Star }
public enum GalaxyDescriptorClassKind { Missing, Explicit }
public sealed record GalaxyInitializerDraw(int Index, string Purpose, GalaxySourceLocation Source,
    NativeRandomState Before, uint Raw, decimal Result);
public sealed record GalaxyDeferredRequest(string Phase, int? BodyId, GalaxySyntaxEntry Syntax);
public sealed record GalaxyOrbitMetadata(int? ParentId, decimal Radius, GalaxySyntaxEntry Syntax);
public sealed record GalaxyBeltMetadata(string Type, decimal Radius, GalaxySourceLocation Source, decimal Width = 1, decimal Density = 1, string? Id = null);
public sealed record GalaxyLocalBody(int Id, int? ParentId, GalaxyBodyKind Kind, string ClassId,
    decimal OrbitDistance, decimal OrbitAngle, int Size, bool HomeCandidate, bool StartingPlanet,
    string? Name, IReadOnlyList<string> Flags, GalaxySyntaxValue Source);

public sealed class GalaxyCompiledDescriptor
{
    public GalaxySyntaxEntry Source { get; }
    public GalaxyDescriptorClassKind ClassKind { get; }
    public string? ClassToken { get; }
    public bool IsOrbitChange => Source.Key == "change_orbit";
    public int StarOrdinal { get; }
    public IReadOnlyList<GalaxyCompiledDescriptor> Children { get; }
    public IReadOnlyList<GalaxyCompiledDescriptor> Moons { get; }
    public IReadOnlyList<GalaxyCompiledDescriptor> Planets { get; }
    internal GalaxyCompiledDescriptor(GalaxySyntaxEntry source, GalaxyDescriptorClassKind kind, string? token,
        IEnumerable<GalaxyCompiledDescriptor> children, int starOrdinal = -1)
    {
        Source = source; ClassKind = kind; ClassToken = token; StarOrdinal = starOrdinal; Children = Freeze(children);
        Moons = Freeze(Children.Where(c => c.Source.Key == "moon"));
        Planets = Freeze(Children.Where(c => c.Source.Key is "planet" or "change_orbit"));
    }
    internal static IReadOnlyList<T> Freeze<T>(IEnumerable<T> value) => Array.AsReadOnly(value.ToArray());
}
public sealed class GalaxyCompiledInitializer
{
    public GalaxyRuntimeDefinition Definition { get; }
    public GalaxySyntaxValue Fields { get; }
    public IReadOnlyList<GalaxyCompiledDescriptor> Descriptors { get; }
    public IReadOnlyList<GalaxySyntaxEntry> Neighbors { get; }
    internal GalaxyCompiledInitializer(GalaxyRuntimeDefinition definition, GalaxySyntaxValue fields,
        IEnumerable<GalaxyCompiledDescriptor> descriptors)
    {
        Definition = definition; Fields = fields; Descriptors = GalaxyCompiledDescriptor.Freeze(descriptors);
        Neighbors = GalaxyCompiledDescriptor.Freeze(fields.Entries.Where(e => e.Key == "neighbor_system"));
    }
}

/// <summary>Все недоказанные defaults/policies задаёт caller, а не executor.</summary>
public sealed record GalaxyInitializerDefaults(string MissingBodyClass, string MissingSystemClass,
    GalaxyNumericRange Count, GalaxyNumericRange Distance, GalaxyNumericRange Angle,
    Func<GalaxyRuntimeDefinition, bool, GalaxyNumericRange> MissingClassSize,
    decimal InitialOrbit, decimal InitialAngle, long FixedScale)
{
    /// <summary>Descriptor ctor 4.5.2: random, count 1, distance [10,20), angle [90,270).
    /// System class, absent class-size и initial accumulators остаются explicit context.</summary>
    public static GalaxyInitializerDefaults VanillaDescriptor(string missingSystemClass,
        Func<GalaxyRuntimeDefinition, bool, GalaxyNumericRange> missingClassSize,
        decimal initialOrbit, decimal initialAngle) => new("random", missingSystemClass,
            GalaxyNumericRange.Constant(1), new(10, 20), new(90, 270), missingClassSize, initialOrbit, initialAngle, 100000);
}
public sealed record GalaxyInitializerContext(GalaxyInitializerDefaults Defaults, string IdealPlanetClass,
    string IdealDesignClass, decimal HabitableMultiplier,
    Func<GalaxyRuntimeDefinition, bool> ClassPotential,
    Func<GalaxyRuntimeDefinition, decimal, bool> AdditionalRandomFilter,
    int MaximumBodies = 10000, int MaximumDepth = 64);

public sealed class GalaxyInitializerResult
{
    public string InitializerId { get; }
    public string StarClassId { get; }
    public string? Name { get; }
    public IReadOnlyList<string> Flags { get; }
    public IReadOnlyList<GalaxyLocalBody> Bodies { get; }
    public IReadOnlyList<int> HomeCandidates { get; }
    public IReadOnlyList<GalaxyBeltMetadata> Belts { get; }
    public IReadOnlyList<GalaxyOrbitMetadata> OrbitalLines { get; }
    public IReadOnlyList<GalaxyDeferredRequest> Deferred { get; }
    public IReadOnlyList<GalaxySyntaxEntry> Neighbors { get; }
    public IReadOnlyList<GalaxyInitializerDraw> Draws { get; }
    public NativeRandomState NextState { get; }
    public bool GameplayComplete => false;
    public IReadOnlyList<string> DeclaredPolicies { get; } = Array.AsReadOnly(new[] {
        "Planet weights encoded at explicit context FixedScale; star factor and habitable multiplier truncate after each fixed multiply.",
        "Star random-list selection uses weighted source spawn_odds and the same fixed-scale reducer; native star selector not certified.",
        "Planet class potential and additional requirement predicates are supplied by caller.",
        "Belts sampled after body/orbital-line construction; native metadata RNG interleaving not certified.",
        "Random modifiers/deposits/anomalies and script effects remain deferred; rings are collected for downstream generation and names are applied by the world naming phase." });
    internal GalaxyInitializerResult(GalaxyCompiledInitializer compiled, string starClass, IEnumerable<GalaxyLocalBody> bodies,
        IEnumerable<GalaxyBeltMetadata> belts, IEnumerable<GalaxyOrbitMetadata> lines,
        IEnumerable<GalaxyDeferredRequest> deferred, IEnumerable<GalaxyInitializerDraw> draws, NativeRandomState next)
    {
        InitializerId = compiled.Definition.Id; StarClassId = starClass;
        Name = GalaxyInitializerExecution.Scalar(compiled.Fields, "name");
        Flags = GalaxyInitializerExecution.Flags(compiled.Fields);
        Bodies = GalaxyCompiledDescriptor.Freeze(bodies); HomeCandidates = GalaxyCompiledDescriptor.Freeze(Bodies.Where(b => b.HomeCandidate).Select(b => b.Id));
        Belts = GalaxyCompiledDescriptor.Freeze(belts); OrbitalLines = GalaxyCompiledDescriptor.Freeze(lines);
        Deferred = GalaxyCompiledDescriptor.Freeze(deferred); Draws = GalaxyCompiledDescriptor.Freeze(draws);
        Neighbors = compiled.Neighbors; NextState = next;
    }
}

public static class GalaxyInitializerExecution
{
    private static readonly IReadOnlySet<string> SystemFields = new[] {
        "planet", "change_orbit", "class", "flags", "init_effect", "asteroid_belt", "usage", "usage_odds",
        "neighbor_system", "max_instances", "name", "scaled_spawn_chance", "spawn_chance", "prevent_anomalies",
        "namelist", "primitive_system", "mandatory_neighbors", "orbital_line", "scaled_spawn_settings",
        "inner_radius_offset", "outer_radius_offset" }.ToFrozenSet(StringComparer.Ordinal);
    private static readonly IReadOnlySet<string> BodyFields = new[] {
        "class", "count", "orbit_distance", "orbit_angle", "size", "has_ring", "has_independent_orbital_line",
        "name", "namelist", "satellite_naming_policy", "entity", "flags", "home_planet", "starting_planet",
        "modifiers", "modifier", "deposit_blockers", "deposits", "anomaly", "init_effect", "moon", "planet",
        "change_orbit", "orbital_line" }.ToFrozenSet(StringComparer.Ordinal);
    private static readonly string[] DeferredBodyOrder = ["modifiers", "modifier", "deposits", "deposit_blockers", "anomaly", "has_ring", "flags", "name", "namelist", "entity", "init_effect"];

    public static GalaxyCompiledInitializer Compile(GalaxyRuntimeCatalog catalog, string initializerId)
    {
        var definition = Unique(catalog, GalaxyRuntimeFamily.Initializer, initializerId, new("<initializer>", 1, 1));
        var fields = Expand(catalog, definition.Syntax.Value, new Dictionary<string, string>(StringComparer.Ordinal), [], 0);
        var numeric = new GalaxyNumericValues(catalog);
        var rootStarOrdinal = 0;
        void Validate(GalaxySyntaxValue value, IReadOnlySet<string> allowed)
        {
            if (value.Kind != GalaxySyntaxKind.Block) throw Error("structural-block", "Ожидался block.", value.Location);
            foreach (var entry in value.Entries)
                if (entry.Key is null || entry.Operator != "=" || !allowed.Contains(entry.Key))
                    throw Error("structural-field", "Не поддержано поле: " + entry.Key, entry.Location);
        }
        GalaxyCompiledDescriptor Descriptor(GalaxySyntaxEntry entry, int depth)
        {
            if (depth > 64) throw Error("structural-depth", "Descriptor depth превышает 64.", entry.Location);
            if (entry.Key == "change_orbit")
            { numeric.Range(entry.Value, definition.File.Path); return new(entry, GalaxyDescriptorClassKind.Explicit, "none", []); }
            var expanded = Expand(catalog, entry.Value, new Dictionary<string, string>(StringComparer.Ordinal), [], 0);
            Validate(expanded, BodyFields);
            foreach (var key in new[] { "count", "orbit_distance", "orbit_angle", "size" })
                if (Optional(expanded, key) is { } field) numeric.Range(field.Value, definition.File.Path);
            var token = Scalar(expanded, "class");
            var updated = entry with { Value = expanded };
            var ordinal = depth == 0 && token == "star" ? rootStarOrdinal++ : -1;
            var children = expanded.Entries.Where(e => e.Key is "moon" or "planet" or "change_orbit").Select(e => Descriptor(e, depth + 1)).ToArray();
            ValidateLines(expanded, numeric, definition.File.Path);
            return new(updated, token is null ? GalaxyDescriptorClassKind.Missing : GalaxyDescriptorClassKind.Explicit, token, children, ordinal);
        }
        Validate(fields, SystemFields);
        ValidateLines(fields, numeric, definition.File.Path);
        foreach (var belt in fields.Entries.Where(e => e.Key == "asteroid_belt"))
        {
            Validate(belt.Value, new[] { "type", "radius" }.ToFrozenSet(StringComparer.Ordinal));
            _ = Scalar(belt.Value, "type") ?? throw Error("belt-type", "Нет type.", belt.Location);
            numeric.Range(Required(belt.Value, "radius").Value, definition.File.Path);
        }
        return new(definition, fields, fields.Entries.Where(e => e.Key is "planet" or "change_orbit").Select(e => Descriptor(e, 0)));
    }

    internal static GalaxySyntaxValue ExpandSource(GalaxyRuntimeCatalog catalog, GalaxySyntaxValue value,
        IReadOnlyDictionary<string, string> parameters) => Expand(catalog, value, parameters, [], 0);

    private static void ValidateLines(GalaxySyntaxValue fields, GalaxyNumericValues numeric, string source)
    {
        foreach (var line in fields.Entries.Where(e => e.Key == "orbital_line"))
        {
            if (line.Value.Kind != GalaxySyntaxKind.Block || line.Value.Entries.Count != 1)
                throw Error("orbital-line", "Ожидался один orbit_distance_from_parent.", line.Location);
            numeric.Range(Required(line.Value, "orbit_distance_from_parent").Value, source);
        }
    }

    public static GalaxyInitializerResult Execute(GalaxyRuntimeCatalog catalog, GalaxyCompiledInitializer compiled,
        GalaxyInitializerContext context, NativeRandomState state)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Defaults.FixedScale <= 0 || context.HabitableMultiplier < 0 || context.MaximumBodies <= 0 || context.MaximumDepth <= 0)
            throw new ArgumentException("Некорректный execution context.", nameof(context));
        var numeric = new GalaxyNumericValues(catalog); var bodies = new List<GalaxyLocalBody>();
        var belts = new List<GalaxyBeltMetadata>(); var lines = new List<GalaxyOrbitMetadata>();
        var deferred = new List<GalaxyDeferredRequest>(); var ledger = new List<GalaxyInitializerDraw>();
        var source = compiled.Definition.File.Path;
        uint Draw(string purpose, GalaxySourceLocation location, Func<uint, decimal> evaluate)
        {
            var before = state; var draw = NativeRandom.Next(state); state = draw.NextState;
            ledger.Add(new(ledger.Count, purpose, location, before, draw.Value, evaluate(draw.Value)));
            return draw.Value;
        }
        decimal Sample(GalaxyNumericRange range, bool integer, string purpose, GalaxySourceLocation location)
        {
            decimal value = 0;
            Draw(purpose, location, raw =>
            {
                if (integer)
                {
                    if (range.Minimum != decimal.Truncate(range.Minimum) || range.Maximum != decimal.Truncate(range.Maximum) ||
                        range.Minimum < int.MinValue || range.Minimum > int.MaxValue ||
                        range.Maximum < int.MinValue || range.Maximum > int.MaxValue) throw Error("integer-range", purpose, location);
                    var delta = (long)range.Maximum - (long)range.Minimum;
                    value = range.Minimum + (raw % (Math.Abs(delta) + 1)) * Math.Sign(delta);
                }
                else
                {
                    var min = checked((long)(range.Minimum * context.Defaults.FixedScale));
                    var max = checked((long)(range.Maximum * context.Defaults.FixedScale));
                    if (min / (decimal)context.Defaults.FixedScale != range.Minimum || max / (decimal)context.Defaults.FixedScale != range.Maximum)
                        throw Error("fixed-precision", purpose, location);
                    value = NativeRandom.EvaluateRawFixedRange(min, checked(max - min), raw) / (decimal)context.Defaults.FixedScale;
                }
                return value;
            });
            return value;
        }
        GalaxyNumericRange Range(GalaxySyntaxValue fields, string key, GalaxyNumericRange fallback) =>
            Optional(fields, key) is { } field ? numeric.Range(field.Value, source) : fallback;
        decimal Number(GalaxyRuntimeDefinition definition, string key, decimal fallback) =>
            Optional(definition.Syntax.Value, key) is { } field ? numeric.Evaluate(field.Value) : fallback;
        string? Weighted(IReadOnlyList<(GalaxyRuntimeDefinition Definition, decimal Weight)> choices, string purpose, GalaxySourceLocation location)
        {
            var eligible = choices.Where(c => c.Weight > 0).ToArray(); var total = eligible.Sum(c => c.Weight);
            if (total <= 0) return null;
            var totalRaw = checked((long)(total * context.Defaults.FixedScale));
            if (totalRaw <= 0 || totalRaw / (decimal)context.Defaults.FixedScale != total)
                throw Error("class-weight-precision", "Weight не представим в context fixed scale.", location);
            decimal selection = 0;
            Draw(purpose, location, raw => selection = raw % totalRaw / (decimal)context.Defaults.FixedScale);
            foreach (var choice in eligible)
            { selection -= choice.Weight; if (selection <= 0) return choice.Definition.Id; }
            throw Error("class-weight", "Не удалось выбрать class.", location);
        }
        string Star(string token, GalaxySourceLocation location, HashSet<string> active)
        {
            if (!active.Add(token)) throw Error("star-list-cycle", token, location);
            try
            {
                var definition = Unique(catalog, GalaxyRuntimeFamily.StarClass, token, location);
                var stars = Optional(definition.Syntax.Value, "stars");
                if (stars is null) return token;
                var choices = stars.Value.Entries.Select(e => Unique(catalog, GalaxyRuntimeFamily.StarClass,
                    ListScalar(e), e.Location)).Select(d => (d, Number(d, "spawn_odds", 0))).ToArray();
                var chosen = Weighted(choices, "system-star-class", location) ?? throw Error("star-empty", token, location);
                return Star(chosen, location, active);
            }
            finally { active.Remove(token); }
        }
        var starClassId = Star(Scalar(compiled.Fields, "class") ?? context.Defaults.MissingSystemClass, compiled.Fields.Location, []);
        var starDefinition = Unique(catalog, GalaxyRuntimeFamily.StarClass, starClassId, compiled.Fields.Location);
        var starPlanets = starDefinition.Syntax.Value.Entries.Where(e => e.Key == "planet").ToArray();
        string? Class(string token, decimal orbit, bool moon, GalaxySourceLocation location, HashSet<string> active, int starOrdinal)
        {
            if (!active.Add(token)) throw Error("planet-list-cycle", token, location);
            try
            {
                if (token == "none") return null;
                if (token == "star")
                {
                    var index = starPlanets.Length == 1 ? 0 : starOrdinal;
                    if (index < 0 || index >= starPlanets.Length) return null;
                    return Scalar(starPlanets[index].Value, "key") ?? throw Error("star-key", "Нет key.", location);
                }
                if (token == "ideal_planet_class") return context.IdealPlanetClass;
                if (token == "ideal_design_class") return context.IdealDesignClass;
                var literal = catalog.Lookup(GalaxyRuntimeFamily.PlanetClass, token);
                if (literal.Status == GalaxyLookupStatus.Unique) return token;
                if (literal.Status == GalaxyLookupStatus.Ambiguous) throw Error("class-ambiguous", token, location);
                var list = catalog.Lookup(GalaxyRuntimeFamily.PlanetRandomList, token);
                if (list.Status == GalaxyLookupStatus.Ambiguous) throw Error("class-ambiguous", token, location);
                if (list.Status == GalaxyLookupStatus.Unique)
                {
                    var members = Required(list.Candidates[0].Syntax.Value, "planets").Value.Entries.Select(ListScalar).ToArray();
                    if (members.Length == 0) return null;
                    var chosen = 0; Draw("planet-random-list", location, raw => chosen = (int)(raw % members.Length));
                    return Class(members[chosen], orbit, moon, location, active, starOrdinal);
                }
                if (token is not ("random" or "random_colonizable" or "random_non_colonizable" or "random_non_ideal" or "random_asteroid"))
                    throw Error("class-token", "Неизвестный class token: " + token, location);
                var choices = new List<(GalaxyRuntimeDefinition, decimal)>();
                foreach (var definition in catalog.PlanetClasses)
                {
                    var fields = definition.Syntax.Value; var colonizable = Bool(fields, "colonizable"); var asteroid = Bool(fields, "asteroid");
                    if (token == "random_colonizable" && !colonizable || token == "random_non_colonizable" && colonizable ||
                        token == "random_non_ideal" && definition.Id == context.IdealPlanetClass || token == "random_asteroid" && !asteroid) continue;
                    if (token != "random_asteroid" && asteroid) continue;
                    if (token != "random_asteroid" && (orbit < Number(definition, "min_distance_from_sun", 0) || orbit > Number(definition, "max_distance_from_sun", 0))) continue;
                    if (token != "random_asteroid" && (!context.ClassPotential(definition) || !context.AdditionalRandomFilter(definition, orbit))) continue;
                    var weightRaw = checked((long)(Number(definition, "spawn_odds", 0) * context.Defaults.FixedScale));
                    if (Optional(starDefinition.Syntax.Value, definition.Id) is { } overrideField)
                    {
                        var factorRaw = Optional(overrideField.Value, "spawn_odds") is { } odds
                            ? checked((long)(numeric.Evaluate(odds.Value) * context.Defaults.FixedScale)) : context.Defaults.FixedScale;
                        weightRaw = checked((long)(weightRaw * (decimal)factorRaw / context.Defaults.FixedScale));
                    }
                    if (colonizable && token != "random_asteroid")
                    {
                        var habitableRaw = checked((long)(context.HabitableMultiplier * context.Defaults.FixedScale));
                        weightRaw = checked((long)(weightRaw * (decimal)habitableRaw / context.Defaults.FixedScale));
                    }
                    choices.Add((definition, weightRaw / (decimal)context.Defaults.FixedScale));
                }
                if (token == "random_asteroid" && !choices.Any(c => c.Item2 > 0))
                {
                    Draw("planet-class:random_asteroid", location, raw => raw);
                    throw Error("asteroid-empty", "Native random_asteroid с total=0 недопустим.", location);
                }
                return Weighted(choices, "planet-class:" + token, location);
            }
            finally { active.Remove(token); }
        }
        void Lines(GalaxySyntaxValue fields, int? parent)
        {
            foreach (var entry in fields.Entries.Where(e => e.Key == "orbital_line"))
                lines.Add(new(parent, Sample(numeric.Range(Required(entry.Value, "orbit_distance_from_parent").Value, source), false, "orbital-line", entry.Location), entry));
        }
        void Batch(IReadOnlyList<GalaxyCompiledDescriptor> descriptors, int? parent, int depth)
        {
            if (depth > context.MaximumDepth) throw Error("execution-depth", "Превышен context depth.", compiled.Fields.Location);
            var orbit = context.Defaults.InitialOrbit; var angle = context.Defaults.InitialAngle;
            foreach (var descriptor in descriptors)
            {
                var fields = descriptor.Source.Value; var location = descriptor.Source.Location;
                var moon = descriptor.Source.Key == "moon";
                var count = (int)Sample(descriptor.IsOrbitChange ? GalaxyNumericRange.Constant(1) : Range(fields, "count", context.Defaults.Count), true, "count", location);
                if (count <= 0) continue;
                if (count > context.MaximumBodies) throw Error("execution-limit", "Descriptor count превышает limit.", location);
                for (var instance = 0; instance < count; instance++)
                {
                    var token = descriptor.ClassToken ?? context.Defaults.MissingBodyClass;
                    var classId = Class(token, orbit, moon, location, [], descriptor.StarOrdinal);
                    orbit += Sample(descriptor.IsOrbitChange ? numeric.Range(fields, source) : Range(fields, "orbit_distance", context.Defaults.Distance), false, "orbit-distance", location);
                    if (classId is null) continue;
                    var planetClass = Unique(catalog, GalaxyRuntimeFamily.PlanetClass, classId, location);
                    var sizeField = Optional(fields, "size");
                    var classSize = Optional(planetClass.Syntax.Value, moon ? "moon_size" : "planet_size");
                    var explicitSize = sizeField is not null ? numeric.Range(sizeField.Value, source) : (GalaxyNumericRange?)null;
                    var sizeRange = explicitSize is { Minimum: >= 0 } suppliedSize ? suppliedSize : classSize is not null ? numeric.Range(classSize.Value) : context.Defaults.MissingClassSize(planetClass, moon);
                    var size = (int)Sample(sizeRange, true, "size", location);
                    if (bodies.Count == context.MaximumBodies) throw Error("execution-limit", "Body count превышает limit.", location);
                    var id = bodies.Count;
                    var home = false;
                    foreach (var alias in fields.Entries.Where(e => e.Key is "home_planet" or "starting_planet"))
                        home = alias.Value.Kind == GalaxySyntaxKind.Scalar && alias.Value.Text is "yes" or "no"
                            ? alias.Value.Text == "yes" : throw Error("bool-field", alias.Key!, alias.Location);
                    angle += Sample(Range(fields, "orbit_angle", context.Defaults.Angle), false, "orbit-angle", location);
                    bodies.Add(new(id, parent, token == "star" || Bool(planetClass.Syntax.Value, "star") ? GalaxyBodyKind.Star : moon ? GalaxyBodyKind.Moon : GalaxyBodyKind.Planet,
                        classId, orbit, angle, size, home, home, Scalar(fields, "name"), Flags(fields), fields));
                    deferred.Add(new("factory-random-modifiers", id, descriptor.Source));
                    foreach (var key in DeferredBodyOrder)
                    {
                        if (key == "deposits") deferred.Add(new("random-deposits-policy", id, descriptor.Source));
                        foreach (var request in fields.Entries.Where(e => e.Key == key)) deferred.Add(new(key, id, request));
                        if (key == "has_ring" && Optional(fields, key) is null) deferred.Add(new("class-ring-probability", id, planetClass.Syntax));
                    }
                    Batch(descriptor.Children, id, depth + 1);
                    Lines(fields, id);
                }
            }
        }
        Batch(compiled.Descriptors, null, 0);
        Lines(compiled.Fields, null);
        foreach (var entry in compiled.Fields.Entries.Where(e => e.Key == "asteroid_belt"))
            belts.Add(new(Scalar(entry.Value, "type")!, Sample(numeric.Range(Required(entry.Value, "radius").Value, source), false, "asteroid-belt-radius", entry.Location), entry.Location));
        foreach (var key in new[] { "flags", "name", "namelist", "prevent_anomalies", "init_effect" })
            foreach (var request in compiled.Fields.Entries.Where(e => e.Key == key)) deferred.Add(new("system-" + key, null, request));
        return new(compiled, starClassId, bodies, belts, lines, deferred, ledger, state);
    }

    private static GalaxySyntaxValue Expand(GalaxyRuntimeCatalog catalog, GalaxySyntaxValue block,
        IReadOnlyDictionary<string, string> parameters, IReadOnlyList<string> calls, int depth)
    {
        if (depth > 64) throw Error("inline-depth", "Inline depth превышает 64.", block.Location);
        if (block.Kind != GalaxySyntaxKind.Block) throw Error("structural-block", "Ожидался block.", block.Location);
        var output = new List<GalaxySyntaxEntry>();
        foreach (var original in block.Entries)
        {
            var entry = Substitute(original, parameters);
            if (entry.Value.Kind == GalaxySyntaxKind.Conditional && entry.Key is null)
            {
                var header = entry.Value.Text!; var negative = header.StartsWith("[[!", StringComparison.Ordinal);
                var name = header[(negative ? 3 : 2)..^1];
                if (parameters.ContainsKey(name) != negative)
                    output.AddRange(Expand(catalog, new(GalaxySyntaxKind.Block, null, false, entry.Value.Location, entry.Value.Entries), parameters, calls, depth + 1).Entries);
                continue;
            }
            if (entry.Key != "inline_script")
            {
                output.Add(entry.Key is "planet" or "moon" ? entry with { Value = Expand(catalog, entry.Value, parameters, calls, depth + 1) } : entry);
                continue;
            }
            var script = entry.Value.Kind == GalaxySyntaxKind.Scalar ? entry.Value.Text : Scalar(entry.Value, "script");
            if (script is null) throw Error("inline-script", "Нет scalar script.", entry.Location);
            if (calls.Contains(script, StringComparer.Ordinal)) throw Error("inline-cycle", string.Join(" -> ", calls.Append(script)), entry.Location);
            var bindings = new Dictionary<string, string>(StringComparer.Ordinal);
            if (entry.Value.Kind != GalaxySyntaxKind.Scalar)
                foreach (var argument in entry.Value.Entries.Where(e => e.Key != "script"))
                {
                    if (argument.Key is null || argument.Operator != "=" || argument.Value.Kind != GalaxySyntaxKind.Scalar || !bindings.TryAdd(argument.Key, argument.Value.Text!))
                        throw Error("inline-parameter", "Требуется unique scalar parameter.", argument.Location);
                }
            var template = Unique(catalog, GalaxyRuntimeFamily.InlineScript, script, entry.Location);
            output.AddRange(Expand(catalog, template.Syntax.Value, bindings, calls.Append(script).ToArray(), depth + 1).Entries);
        }
        return new(GalaxySyntaxKind.Block, null, false, block.Location, output);
    }
    private static GalaxySyntaxEntry Substitute(GalaxySyntaxEntry entry, IReadOnlyDictionary<string, string> bindings)
    {
        string? Text(string? value) => value is null ? null : Regex.Replace(value, "\\$([A-Za-z0-9_]+)\\$", match =>
            bindings.TryGetValue(match.Groups[1].Value, out var replacement) ? replacement : match.Value);
        GalaxySyntaxValue Value(GalaxySyntaxValue value) => new(value.Kind, Text(value.Text), value.Quoted, value.Location,
            value.Entries.Select(child => Substitute(child, bindings)));
        return entry with { Key = Text(entry.Key), Value = Value(entry.Value) };
    }
    internal static GalaxySyntaxEntry? Optional(GalaxySyntaxValue fields, string key)
    {
        var entries = fields.Entries.Where(e => e.Key == key).ToArray();
        return entries.LastOrDefault();
    }
    private static GalaxySyntaxEntry Required(GalaxySyntaxValue fields, string key) => Optional(fields, key) ?? throw Error("missing-field", key, fields.Location);
    internal static string? Scalar(GalaxySyntaxValue fields, string key)
    {
        var entry = Optional(fields, key); if (entry is null) return null;
        if (entry.Value.Kind != GalaxySyntaxKind.Scalar) throw Error("scalar-field", key, entry.Location);
        return entry.Value.Text;
    }
    private static string ListScalar(GalaxySyntaxEntry entry) => entry.Key is null && entry.Value.Kind == GalaxySyntaxKind.Scalar
        ? entry.Value.Text! : throw Error("list-item", "Ожидался scalar list item.", entry.Location);
    private static bool Bool(GalaxySyntaxValue fields, string key) => Scalar(fields, key) switch
    { null or "no" => false, "yes" => true, var value => throw Error("bool-field", key + "=" + value, fields.Location) };
    internal static IReadOnlyList<string> Flags(GalaxySyntaxValue fields)
    {
        var flags = Optional(fields, "flags");
        if (flags is null) return Array.Empty<string>();
        if (flags.Value.Kind != GalaxySyntaxKind.Block) throw Error("flags-form", "Flags требует list block.", flags.Location);
        return GalaxyCompiledDescriptor.Freeze(flags.Value.Entries.Select(ListScalar));
    }
    private static GalaxyRuntimeDefinition Unique(GalaxyRuntimeCatalog catalog, GalaxyRuntimeFamily family, string id, GalaxySourceLocation location)
    {
        var found = catalog.Lookup(family, id);
        return found.Status == GalaxyLookupStatus.Unique ? found.Candidates[0] : throw Error("catalog-reference", $"{family}:{id}: {found.Status}", location);
    }
    private static GalaxyStructureException Error(string code, string message, GalaxySourceLocation location) => new(code, message, location);
}

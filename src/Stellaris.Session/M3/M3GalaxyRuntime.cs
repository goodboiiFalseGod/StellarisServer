using System.Collections.ObjectModel;
using System.Globalization;
using Stellaris.Core.Construction;
using Stellaris.Core.Content.Construction.Startup;
using Stellaris.Core.Content.Galaxy;
using Stellaris.Core.Galaxy;

namespace Stellaris.Session.M3;

public sealed record M3GalaxySettings(string Seed, string ScenarioId, string ShapeId, decimal Density,
    int? Nebulae = null, int? GuaranteedColonies = null, decimal? GatewayFrequency = null, decimal? HabitableMultiplier = null, int? WormholePairs = null);
public sealed record M3GalaxyPlayer(int ParticipantId, int EconomyPlanetId, string TemplateId);
public sealed record M3GalaxyVisualBinding(string VisualId, string ClassId, string Kind);
public sealed record M3GalaxyGatewayBinding(string ObjectId, string VisualId);
public sealed record M3GalaxyBodySemantic(string BodyId, string ClassId, string? ParentBodyId, string Kind, int? PlanetSize = null,
    IReadOnlyList<GalaxyWorldBodyModifier>? Modifiers = null);
public sealed class M3GalaxyStart
{
    public GalaxyWorldSnapshot Generated { get; }
    public SpaceWorld Space { get; }
    public IReadOnlyDictionary<string, M3StartupColony> Startup { get; }
    public IReadOnlyList<M3GalaxyVisualBinding> Visuals { get; }
    public IReadOnlyList<M3GalaxyGatewayBinding> GatewayObjects { get; }
    public IReadOnlyList<M3GalaxyGatewayBinding> WormholeObjects { get; }
    public IReadOnlyList<M3GalaxyBodySemantic> BodySemantics { get; }
    public IReadOnlyList<string> ClassIds { get; }
    public IReadOnlyList<string> ObjectIds { get; }
    internal M3GalaxyStart(GalaxyWorldSnapshot generated, SpaceWorld space, Dictionary<string, M3StartupColony> startup,
        IEnumerable<M3GalaxyVisualBinding> visuals, IEnumerable<M3GalaxyBodySemantic> semantics,
        IEnumerable<M3GalaxyGatewayBinding> gatewayObjects, IEnumerable<M3GalaxyGatewayBinding>? wormholeObjects = null)
    {
        Generated = generated; Space = space; Startup = new ReadOnlyDictionary<string, M3StartupColony>(startup);
        Visuals = Array.AsReadOnly(visuals.ToArray()); BodySemantics = Array.AsReadOnly(semantics.ToArray());
        GatewayObjects = Array.AsReadOnly(gatewayObjects.ToArray());
        WormholeObjects = Array.AsReadOnly((wormholeObjects ?? []).ToArray());
        ClassIds = Array.AsReadOnly(Visuals.Select(v => v.ClassId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
        ObjectIds = Array.AsReadOnly(GatewayObjects.Concat(WormholeObjects).Select(v => v.ObjectId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
    }
}

/// <summary>Граница M3: staged geometry и exact home startup, без изменения economy/construction rules.</summary>
public sealed class M3GalaxyRuntime(GalaxyLobbyCatalog catalog, NativeTriangleBackend backend)
{
    public GalaxyLobbyCatalog Catalog { get; } = catalog;
    public M3GalaxySettings DefaultSettings { get; } = new(Guid.NewGuid().ToString("N"), "tiny", "elliptical", 1,
        catalog.Scenarios.Single(s => s.Id == "tiny").NebulaeDefault, 0,
        catalog.Scenarios.Single(s => s.Id == "tiny").Gateways.Default, 1, checked((int)catalog.Scenarios.Single(s => s.Id == "tiny").WormholePairs.Default));
    public M3GalaxyView Project(string stage, M3GalaxySettings settings, bool canPrepare, M3GalaxyStart? prepared = null) => new(
        stage, settings, Array.AsReadOnly(Catalog.Scenarios.Select(s => new M3GalaxyScenarioView(s.Id, s.Stars, s.Shapes,
            s.HyperlaneDensity.Minimum, s.HyperlaneDensity.Maximum, s.HyperlaneDensity.Default, s.NebulaeDefault,
            GatewayFrequency: s.Gateways, WormholePairs: s.WormholePairs)).ToArray()),
        canPrepare, "PrototypeOrdinaryV1", "Обычные системы, редкие звёзды, Trappist, домашние миры, туманности, заброшенные врата и ограниченные пары червоточин; переходы и исследования червоточин пока не моделируются.",
        prepared?.Generated.WorldFingerprint, prepared?.Space.Systems.Count, prepared?.Generated.Bodies.Count, prepared?.Space.Hyperlanes.Count);
    public M3GalaxySettings ResolveSettings(M3GalaxySettings settings)
    {
        var resolved = Resolve(settings, [new(1, 1, "economic")]);
        return new(resolved.Seeds.UserSeed, settings.ScenarioId, settings.ShapeId, settings.Density,
            resolved.Settings.SpecialContent.Nebulae, resolved.Settings.SpecialContent.GuaranteedColonies,
            resolved.Settings.SpecialContent.Gateways, resolved.Settings.HabitableMultiplier, resolved.Settings.SpecialContent.WormholePairs);
    }
    public M3GalaxyStart Prepare(M3GalaxySettings settings, IReadOnlyList<M3GalaxyPlayer> players,
        Func<string, GalaxyHomeAllocation, StartupBuildingPlan> startupPlan)
    {
        ArgumentNullException.ThrowIfNull(startupPlan);
        ArgumentNullException.ThrowIfNull(players);
        players = Array.AsReadOnly(players.ToArray());
        if (players.Count == 0 || players.Any(p => p.ParticipantId <= 0 || p.EconomyPlanetId <= 0) ||
            players.Select(p => p.ParticipantId).Distinct().Count() != players.Count || players.Select(p => p.EconomyPlanetId).Distinct().Count() != players.Count)
            throw new InvalidOperationException("M3 требует хотя бы одного игрока с уникальными положительными player/economy bindings.");
        var resolved = Resolve(settings, players);
        var result = new GalaxyWorldGenerator(Catalog, backend).Generate(resolved);
        if (!result.Success) throw new InvalidOperationException("Генерация: " + result.Failure!.Stage + "/" + result.Failure.Code + ": " + result.Failure.Detail);
        var generated = result.World!; var startup = new Dictionary<string, M3StartupColony>(StringComparer.Ordinal);
        foreach (var home in generated.Homes)
        {
            var player = players.Single(p => p.ParticipantId == home.ParticipantId);
            var plan = startupPlan(player.TemplateId, home);
            if (plan.Context.PlanetClass != home.ClassId || plan.Context.PlanetSize != home.Size || plan.FinalPlanetSize <= 0 || plan.Gates.Count != 0)
                throw new InvalidOperationException("Startup plan не соответствует generated home или содержит gates.");
            var colonyId = "colony/" + player.ParticipantId.ToString(CultureInfo.InvariantCulture);
            startup.Add(colonyId, M3StartupColony.Create(colonyId, player.ParticipantId,
                new(home.InitializerId, home.ClassId, home.Size), plan));
        }
        var visuals = new Dictionary<string, M3GalaxyVisualBinding>(StringComparer.Ordinal);
        var semantics = new List<M3GalaxyBodySemantic>();
        var numeric = new GalaxyNumericValues(Catalog.Content);
        double EntityScale(string classId)
        {
            var lookup = Catalog.Content.Lookup(GalaxyRuntimeFamily.PlanetClass, classId);
            if (lookup.Status != GalaxyLookupStatus.Unique) throw new InvalidOperationException("Неоднозначный source visual class: " + classId);
            var definition = lookup.Candidates[0];
            var field = definition.Syntax.Value.Entries.LastOrDefault(e => e.Key == "entity_scale");
            if (field is null) throw new GalaxyStructureException("entity-scale-missing", classId + ": нет entity_scale", definition.Location);
            try
            {
                var value = (double)numeric.Evaluate(field.Value, definition.File.Path);
                if (!double.IsFinite(value) || value <= 0) throw new GalaxyStructureException("entity-scale-invalid", classId + ": требуется positive finite entity_scale", field.Location);
                return value;
            }
            catch (GalaxyStructureException error)
            { throw new GalaxyStructureException("entity-scale-invalid", classId + ": " + error.Message, field.Location); }
        }
        var planetBaseline = EntityScale("pc_continental");
        var relativeScales = new Dictionary<string, double>(StringComparer.Ordinal);
        var homeSizes = generated.Homes.ToDictionary(h => h.BodyId,
            h => startup["colony/" + h.ParticipantId.ToString(CultureInfo.InvariantCulture)].Plan.FinalPlanetSize, StringComparer.Ordinal);
        var explicitEntities = generated.Deferred.Where(d => d.Phase == "entity").Select(d => d.BodyId).ToHashSet(StringComparer.Ordinal);
        foreach (var body in generated.Bodies)
        {
            var definition = Catalog.Content.Lookup(GalaxyRuntimeFamily.PlanetClass, body.ClassId);
            if (definition.Status != GalaxyLookupStatus.Unique) throw new InvalidOperationException("Неизвестный visual class: " + body.ClassId);
            if (explicitEntities.Contains(body.Id))
                throw new InvalidOperationException("SourceFirstNumberedEntityV1 не поддерживает descriptor entity override.");
            var kind = body.Kind == GalaxyBodyKind.Star ? "star" : "planet";
            var visualId = "world." + body.ClassId;
            visuals.TryAdd(visualId, new(visualId, body.ClassId, kind));
            if (!relativeScales.ContainsKey(body.ClassId)) relativeScales.Add(body.ClassId, EntityScale(body.ClassId) / planetBaseline);
            semantics.Add(new(body.Id, body.ClassId, body.ParentId, body.Kind.ToString().ToLowerInvariant(),
                body.Kind == GalaxyBodyKind.Star ? null : homeSizes.GetValueOrDefault(body.Id, body.Size), body.Modifiers));
        }
        foreach (var belt in generated.Systems.SelectMany(s => s.Belts))
        {
            var classId = BeltClass(belt);
            var visualId = "world." + classId;
            visuals.TryAdd(visualId, new(visualId, classId, "planet"));
        }
        var space = SpaceWorld.FromGenerated(generated, players, startup, relativeScales);
        var gatewayObjects = generated.Gateways.Select(g => new M3GalaxyGatewayBinding(g.Type, "object." + g.Type)).ToArray();
        var wormholeObjects = generated.Wormholes.Select(_ => new M3GalaxyGatewayBinding("natural_wormhole", "object.natural_wormhole")).ToArray();
        return new(generated, space, startup, visuals.Values.OrderBy(v => v.VisualId, StringComparer.Ordinal), semantics, gatewayObjects, wormholeObjects);
    }
    internal static string BeltClass(GalaxyBeltMetadata belt) => belt.Type switch {
        "rocky_asteroid_belt" => "pc_asteroid",
        "icy_asteroid_belt" => "pc_ice_asteroid",
        _ => throw new GalaxyStructureException("unsupported-belt-visual", belt.Type + ": нет RepresentativeAsteroidBeltV1 binding", belt.Source) };
    private ResolvedGalaxyRequest Resolve(M3GalaxySettings settings, IReadOnlyList<M3GalaxyPlayer> players)
    {
        ArgumentNullException.ThrowIfNull(settings); ArgumentNullException.ThrowIfNull(players);
        var habitableMultiplier = settings.HabitableMultiplier ?? 1m;
        if (habitableMultiplier < 0 || habitableMultiplier > 5 || decimal.Truncate(habitableMultiplier * 100_000m) != habitableMultiplier * 100_000m)
            throw new InvalidOperationException("Множитель пригодных миров M3 должен быть от 0 до 5 с точностью до пяти знаков.");
        var request = new GalaxyLobbyRequest(settings.Seed, settings.ScenarioId, settings.ShapeId, settings.Density,
            players.Select(p => new GalaxyPlayerSlot(p.ParticipantId, p.TemplateId, ConstructionOrdinaryTemplate.Get(p.TemplateId).IdealPlanetClass)),
            habitableMultiplier: habitableMultiplier,
            specialContent: new(Nebulae: settings.Nebulae ?? Catalog.Scenarios.SingleOrDefault(s => s.Id == settings.ScenarioId)?.NebulaeDefault ?? 0,
                GuaranteedColonies: settings.GuaranteedColonies ?? 0,
                Gateways: settings.GatewayFrequency ?? Catalog.Scenarios.SingleOrDefault(s => s.Id == settings.ScenarioId)?.Gateways.Default ?? 0,
                WormholePairs: settings.WormholePairs ?? checked((int)(Catalog.Scenarios.SingleOrDefault(s => s.Id == settings.ScenarioId)?.WormholePairs.Default ?? 0))));
        var resolved = Catalog.Resolve(request);
        return resolved.Request ?? throw new InvalidOperationException("Настройки галактики: " + resolved.Failure!.Code + ": " + resolved.Failure.Detail);
    }
}

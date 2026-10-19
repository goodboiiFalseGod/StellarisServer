using System.Globalization;
using System.Text.Json;
using Stellaris.Core.Content.Galaxy;

namespace Stellaris.Core.Galaxy;

public sealed record GalaxyWorldBody(string Id, string SystemId, string? ParentId, GalaxyBodyKind Kind,
    string ClassId, int Size, decimal OrbitDistance, decimal OrbitAngle, double X, double Y,
    string? Name, IReadOnlyList<string> Flags, string? NameKey = null, string NameSuffix = "", string? SatelliteNamingPolicy = null, bool HasRing = false,
    bool PreventAnomaly = false, IReadOnlyList<GalaxyWorldBodyModifier>? Modifiers = null);
public sealed record GalaxyWorldBodyModifier(string Id, int Days);
public sealed record GalaxyWorldGateway(string Id, string SystemId, double X, double Z, decimal AngleDegrees,
    string Type, string PlacementPolicy, double SurfaceRadius = 6, double EnvelopeRadiusProxy = 0);
public sealed record GalaxyWorldWormhole(string Id, string SystemId, double X, double Z, decimal AngleDegrees,
    string PairId, string PlacementPolicy, double SurfaceRadius = 6, double EnvelopeRadiusProxy = 0);
public sealed record GalaxyWorldSystem(string Id, uint NativeHandle, NativeFixedPoint Position, int Group,
    string InitializerId, string StarClassId, string? Name, IReadOnlyList<string> Flags,
    IReadOnlyList<GalaxyBeltMetadata> Belts, IReadOnlyList<GalaxyOrbitMetadata> OrbitalLines, string? NameKey = null, string NameSuffix = "");
public sealed record GalaxyWorldLane(string Id, string A, string B, long Length, byte Flag, string? RepairReason);
public sealed record GalaxyHomeAllocation(int ParticipantId, string StartupProfileId, string SystemId,
    string BodyId, string StarId, string InitializerId, string ClassId, int Size, uint SelectionDraw);
public sealed record GalaxyWorldDeferred(string Id, string SystemId, string? BodyId, string Phase,
    GalaxySourceLocation Source, GalaxySyntaxEntry Syntax);
public sealed record GalaxyStageLedger(string Stage, long Count, NativeRandomState? NextState);
public sealed record GalaxyGenerationResult(GalaxyWorldSnapshot? World, GalaxyApiFailure? Failure)
{
    public bool Success => World is not null && Failure is null;
}

/// <summary>Структурный мир готов к consumer bootstrap; GameplayComplete не заявляется.</summary>
public sealed class GalaxyWorldSnapshot
{
    public string Readiness => "ReadyForBootstrap";
    public ResolvedGalaxyRequest Request { get; }
    public string WorldFingerprint { get; }
    public IReadOnlyList<GalaxyWorldSystem> Systems { get; }
    public IReadOnlyList<GalaxyWorldBody> Bodies { get; }
    public IReadOnlyList<GalaxyWorldGateway> Gateways { get; }
    public IReadOnlyList<GalaxyWorldWormhole> Wormholes { get; }
    public IReadOnlyList<GalaxyWorldLane> Lanes { get; }
    public IReadOnlyList<GalaxyHomeAllocation> Homes { get; }
    public IReadOnlyList<GalaxyNeighborAllocation> GuaranteedAllocations { get; }
    public IReadOnlyList<GalaxyWorldDeferred> Deferred { get; }
    public IReadOnlyList<GalaxyStageLedger> Stages { get; }
    public IReadOnlyList<string> Policies { get; }
    public GalaxyNebulaResult Nebulae { get; }
    public GalaxyMiscAdmissionSummary MiscAdmission { get; }
    internal GalaxyWorldSnapshot(ResolvedGalaxyRequest request, IEnumerable<GalaxyWorldSystem> systems,
        IEnumerable<GalaxyWorldBody> bodies, IEnumerable<GalaxyWorldGateway> gateways, IEnumerable<GalaxyWorldWormhole> wormholes, IEnumerable<GalaxyWorldLane> lanes,
        IEnumerable<GalaxyHomeAllocation> homes, IEnumerable<GalaxyNeighborAllocation> guaranteedAllocations, IEnumerable<GalaxyWorldDeferred> deferred,
        IEnumerable<GalaxyStageLedger> stages, IEnumerable<string> policies, GalaxyNebulaResult nebulae, GalaxyMiscAdmissionSummary miscAdmission)
    {
        Request = request; Systems = Freeze(systems); Bodies = Freeze(bodies); Gateways = Freeze(gateways); Wormholes = Freeze(wormholes); Lanes = Freeze(lanes);
        Nebulae = nebulae; MiscAdmission = miscAdmission;
        Homes = Freeze(homes); GuaranteedAllocations = Freeze(guaranteedAllocations); Deferred = Freeze(deferred); Stages = Freeze(stages); Policies = Freeze(policies.Distinct(StringComparer.Ordinal));
        WorldFingerprint = GalaxyLobbyCatalog.Hash(ToMetadataJson());
    }
    /// <summary>Экспорт metadata без оригинальных script bodies/assets.</summary>
    public string ToMetadataJson() => JsonSerializer.Serialize(new {
        version = "galaxy-world-v4", Readiness, Request.SettingsFingerprint, Request.ContentFingerprint,
        seeds = Request.Seeds, policies = Policies, systems = Systems.Select(s => new {
            s.Id, s.NativeHandle, s.Position, s.Group, s.InitializerId, s.StarClassId, s.Name, s.NameKey, s.NameSuffix, s.Flags,
            belts = s.Belts.Select(b => new { b.Id, b.Type, b.Radius, b.Width, b.Density, b.Source }),
            lines = s.OrbitalLines.Select(l => new { l.ParentId, l.Radius, l.Syntax.Location }) }),
        bodies = Bodies, gateways = Gateways, wormholes = Wormholes, lanes = Lanes, homes = Homes, guaranteedAllocations = GuaranteedAllocations, nebulae = Nebulae,
        miscAdmission = MiscAdmission,
        deferred = Deferred.Select(d => new { d.Id, d.SystemId, d.BodyId, d.Phase, d.Source }), stages = Stages });
    internal static IReadOnlyList<T> Freeze<T>(IEnumerable<T> items) => Array.AsReadOnly(items.ToArray());
}

public sealed class GalaxyWorldGenerator(GalaxyLobbyCatalog catalog, NativeTriangleBackend backend)
{
    public GalaxyGenerationResult Generate(ResolvedGalaxyRequest request)
    {
        var stage = "preflight";
        try
        {
            ArgumentNullException.ThrowIfNull(request); catalog.VerifyStable();
            if (request.ContentFingerprint != catalog.ContentFingerprint) throw new InvalidDataException("Resolved request принадлежит другому snapshot.");
            stage = "names-source";
            var starNames = catalog.StarNames;
            var content = catalog.Content; var ledger = new List<GalaxyStageLedger>();
            var policies = new List<string> { "SpatialBfsPartitionsV1", "FarthestHomeSpreadV1", "ContentLocalStreamV1", "GenericHomeUniformV1",
                GalaxyMiscAdmission.PolicyId,
                "PrototypeContentOmissionsV1: required deferred requests remain explicit until consumer disposition",
                "LocalOrbitProjectionV1: Math.Sin/Cos presentation coordinates; semantic polar values retained" };
            var count = request.Plan.TargetStars;
            stage = "materialization";
            var allocator = SystemAllocatorSnapshot.CreateFresh(count, (count + 1023) / 1024, 0, 0,
                SystemResolverBinding.SameManager, false, false, new(uint.MaxValue));
            var inputs = Enumerable.Range(0, count).Select(i => new SystemCreationInput(SystemId(i), (ulong)i)).ToArray();
            var materialized = SystemMaterialization.Generate(request.Plan, request.Seeds.Galaxy, allocator, inputs);
            if (materialized.Result.Status != SystemMaterializationStatus.TargetReached)
                throw new InvalidDataException("Placement/allocation не достиг target: " + materialized.Result.Status);
            ledger.Add(new(stage, materialized.Result.Systems.Count, materialized.NextState));
            var handles = materialized.Result.Systems.Select(s => s.Handle.Value).ToArray();
            var shape = GalaxyShapeProjection.Read(request.Plan);
            stage = "relaxation";
            var prepared = RelaxationAdapters.PrepareInput(materialized.Result.Systems.Select(s => s.Position).ToArray(), shape.CoreRadius, catalog.Relaxation);
            var relaxed = NativeGalaxyRelaxation.Run(prepared, backend);
            var points = relaxed.OrdinaryCoordinates.ToArray();
            if (points.Distinct().Count() != count) throw new InvalidDataException("Relaxation создал совпадающие координаты систем.");
            ledger.Add(new(stage, relaxed.TessellationCalls, null));
            var selectionState = NativeRandom.FromSpecifiedSeed(request.Seeds.Content);
            uint Next()
            {
                var draw = NativeRandom.Next(selectionState); selectionState = draw.NextState; return draw.Value;
            }
            stage = "nebulae";
            var namesState = NativeRandom.FromSpecifiedSeed(request.Seeds.Names);
            uint NextName() { var draw = NativeRandom.Next(namesState); namesState = draw.NextState; return draw.Value; }
            var nebulaCount = request.Settings.SpecialContent.Nebulae;
            var nebulaParameters = nebulaCount > 0 ? GalaxyLobbyCatalog.NebulaParameters(request.Plan) : (Radius: 0L, MinimumSeparation: 0L);
            var nebulae = GalaxyNebulaGeneration.Generate(points.Select((p, i) => new GalaxyNebulaSite(SystemId(i), p)).ToArray(),
                nebulaCount, nebulaParameters.Radius, nebulaParameters.MinimumSeparation, nebulaCount > 0 ? catalog.NebulaNames : [], Next, NextName);
            ledger.Add(new("nebula-placement", nebulae.PlacementDraws, selectionState));
            ledger.Add(new("nebula-names", nebulae.NameDraws, namesState));
            policies.Add("NebulaSuppliedSeedV1: Content before homes; separate Names; empty pool consumes no draw; no native global RNG claim");
            stage = "groups-and-homes";
            var homeSites = new List<int> { (int)(Next() % count) };
            while (homeSites.Count < request.Settings.Players.Count)
            {
                var site = Enumerable.Range(0, count).Except(homeSites).OrderByDescending(i => homeSites.Min(h => Squared(points[i], points[h])))
                    .ThenBy(i => i).First(); homeSites.Add(site);
            }
            var initialGraph = CandidateHyperlaneGraph.Build(points, handles, new int[count], [new(0, 0)], backend);
            var adjacency = Enumerable.Range(0, count).Select(_ => new List<int>()).ToArray();
            foreach (var edge in initialGraph.Candidates.SameGroup.Concat(initialGraph.Candidates.DifferentGroup))
            { adjacency[edge.A].Add(edge.B); adjacency[edge.B].Add(edge.A); }
            var mapped = Enumerable.Repeat(-1, count).ToArray(); var targets = new List<HyperlaneGroupTarget>();
            foreach (var seed in homeSites.Concat(Enumerable.Range(0, count)))
            {
                if (mapped[seed] >= 0) continue;
                var home = homeSites.Contains(seed);
                var cfg = request.Plan.Scenario.Fields.Required(home ? "home_system_partitions" : "open_space_partitions").AsBlock();
                int N(string key) => checked((int)GalaxyCatalog.Number(cfg.Required(key)));
                var min = N("min_systems"); var max = N("max_systems");
                if (min < 1 || max < min) throw new InvalidDataException("Неверный prototype partition range.");
                var size = min + (max > min ? (int)(Next() % (max - min)) : 0);
                var bridgeMin = N("min_bridges"); var bridgeMax = checked((int)decimal.Round(N("max_bridges") * request.Settings.HyperlaneDensity, 0, MidpointRounding.AwayFromZero));
                if (bridgeMin < 0) throw new InvalidDataException("Отрицательный bridge target.");
                var target = bridgeMin + (bridgeMax > bridgeMin ? (int)(Next() % (bridgeMax - bridgeMin + 1)) : 0);
                var group = targets.Count; targets.Add(new(group, target));
                var queue = new Queue<int>(); queue.Enqueue(seed); var queued = new HashSet<int> { seed }; var added = 0;
                while (queue.TryDequeue(out var site) && added < size)
                {
                    if (mapped[site] >= 0 || homeSites.Contains(site) && site != seed) continue;
                    mapped[site] = group; added++;
                    foreach (var next in adjacency[site].OrderBy(i => Squared(points[site], points[i])).ThenBy(i => i))
                        if (mapped[next] < 0 && queued.Add(next)) queue.Enqueue(next);
                }
            }
            ledger.Add(new(stage, targets.Count, selectionState));
            stage = "hyperlanes";
            var graph = CandidateHyperlaneGraph.Build(points, handles, mapped, targets.Select(t => new CandidateGroupInput(t.Id, 0)).ToArray(), backend);
            var options = catalog.Scenarios.Single(s => s.Id == request.Settings.ScenarioId);
            var maxDistance = GalaxyLobbyCatalog.Fixed(GalaxyCatalog.Number(request.Plan.Scenario.Fields.Required("max_hyperlane_distance")));
            var pruned = HyperlanePruning.Run(graph, targets.ToArray(), new(GalaxyLobbyCatalog.Fixed(request.Settings.HyperlaneDensity),
                GalaxyLobbyCatalog.Fixed(options.HyperlaneDensity.Minimum), GalaxyLobbyCatalog.Fixed(options.HyperlaneDensity.Maximum),
                maxDistance, catalog.RemovalPercent, catalog.DegreeBase), request.Seeds.PruneA, request.Seeds.PruneB);
            var connected = HyperlaneConnectivity.Complete(graph, pruned, maxDistance, HyperlaneConnectivityPolicy.RestoreCandidatesThenNearestV1);
            if (!connected.IsConnected) throw new InvalidDataException("Graph disconnected.");
            policies.Add("RestoreCandidatesThenNearestV1: repairs=" + connected.Repairs.Count.ToString(CultureInfo.InvariantCulture));
            var repairs = connected.Repairs.ToDictionary(r => (r.Edge.A, r.Edge.B), r => r.Reason);
            var lanes = connected.Edges.Select(e => new GalaxyWorldLane("lane/" + e.A.ToString("D4", CultureInfo.InvariantCulture) + "/" + e.B.ToString("D4", CultureInfo.InvariantCulture), SystemId(e.A), SystemId(e.B), e.Length, e.Flag,
                repairs.GetValueOrDefault((e.A, e.B)))).ToArray();
            ledger.Add(new(stage, lanes.Length, null));
            stage = "initializers";
            var systems = new List<GalaxyWorldSystem>(); var bodies = new List<GalaxyWorldBody>();
            var gateways = new List<GalaxyWorldGateway>();
            var gatewaySites = new List<(int Site, NativeFixedPoint Position)>();
            var gatewayAngleState = NativeRandom.FromSpecifiedSeed(request.Seeds.GatewayPosition);
            long gatewayPositionDraws = 0;
            var wormholeSelectionState = NativeRandom.FromSpecifiedSeed(request.Seeds.WormholeSelection);
            var wormholePlacementState = NativeRandom.FromSpecifiedSeed(request.Seeds.WormholePlacement);
            long wormholeSelectionDraws = 0, wormholePlacementDraws = 0;
            var homes = new List<GalaxyHomeAllocation>(); var allocations = new List<GalaxyNeighborAllocation>(); var deferred = new List<GalaxyWorldDeferred>();
            var systemFlags = Enumerable.Range(0, count).Select(_ => new List<string>()).ToArray();
            var numeric = new GalaxyNumericValues(content);
            var ringState = NativeRandom.FromSpecifiedSeed(request.Seeds.Script0);
            long ringDraws = 0;
            uint NextRing() { var draw = NativeRandom.Next(ringState); ringState = draw.NextState; ringDraws++; return draw.Value; }
            var miscAdmission = new GalaxyMiscAdmission(content, request);
            long miscAdmissionDraws = 0;
            GalaxyCompiledInitializer AdmitMisc(int site)
            {
                var adjacent = lanes.Where(lane => lane.A == SystemId(site) || lane.B == SystemId(site))
                    .Select(lane => ParseSystemOrdinal(lane.A == SystemId(site) ? lane.B : lane.A)).ToHashSet();
                var result = miscAdmission.Select(Next, systemFlags[site].Contains("empire_cluster", StringComparer.Ordinal),
                    points[site], gatewaySites, adjacent);
                miscAdmissionDraws = checked(miscAdmissionDraws + result.SelectionDraws + result.SpawnDraws);
                return result.Initializer;
            }
            var guaranteedCountForExecution = request.Settings.SpecialContent.GuaranteedColonies;
            var executed = new Dictionary<int, (GalaxyCompiledInitializer Compiled, GalaxyInitializerResult Result, GalaxyPlayerSlot? Player, int RootSite, string RootIdeal)>();
            var systemIndices = new Dictionary<string, int>(StringComparer.Ordinal);
            var adjacencyById = Enumerable.Range(0, count).ToDictionary(SystemId, _ => new List<string>(), StringComparer.Ordinal);
            foreach (var lane in lanes) { adjacencyById[lane.A].Add(lane.B); adjacencyById[lane.B].Add(lane.A); }

            GalaxyCompiledInitializer SelectInitializer(int site, GalaxyPlayerSlot? player)
            {
                if (player is not null)
                    return GalaxyInitializerExecution.Compile(content, player.InitializerId ?? GalaxyLobbyCatalog.HomeInitializers[(int)(Next() % GalaxyLobbyCatalog.HomeInitializers.Count)]);
                return AdmitMisc(site);
            }

            void ExecuteSite(int site, GalaxyCompiledInitializer compiled, GalaxyPlayerSlot? player, int rootSite, string rootIdeal, bool neighbor)
            {
                var defaults = GalaxyInitializerDefaults.VanillaDescriptor("rl_standard_stars",
                    (definition, moon) => throw new GalaxyStructureException("missing-class-size", definition.Id + (moon ? ":moon" : ":planet"), definition.Location), 0, 0);
                var context = new GalaxyInitializerContext(defaults, rootIdeal, rootIdeal, request.Settings.HabitableMultiplier, catalog.Potential, (_, _) => true);
                var result = GalaxyInitializerExecution.Execute(content, compiled, context, selectionState); selectionState = result.NextState;
                policies.AddRange(result.DeclaredPolicies);
                var systemId = SystemId(site);
                var local = new Dictionary<int, GalaxyWorldBody>();
                var resolvedMiscEffects = new HashSet<int>();
                foreach (var body in result.Bodies)
                {
                    if (body.Size <= 0 || body.OrbitDistance < 0) throw new InvalidDataException("Body size/orbit invalid: " + compiled.Definition.Id);
                    var radians = (double)(body.OrbitAngle % 360) * Math.PI / 180;
                    var parent = body.ParentId is { } p ? local[p] : null;
                    var x = (parent?.X ?? 0) + (double)body.OrbitDistance * Math.Cos(radians);
                    var y = (parent?.Y ?? 0) + (double)body.OrbitDistance * Math.Sin(radians);
                    if (!double.IsFinite(x) || !double.IsFinite(y)) throw new InvalidDataException("Body position invalid.");
                    var guaranteed = neighbor ? GuaranteedBodyEffects(body.Source) : null;
                    var miscEffects = MiscBodyEffects(compiled.Definition.Id, body);
                    if (miscEffects.Resolved) resolvedMiscEffects.Add(body.Id);
                    var flags = guaranteed is null ? body.Flags : GalaxyCompiledDescriptor.Freeze(body.Flags.Append(guaranteed.Value.Flag));
                    var dto = new GalaxyWorldBody(BodyId(site, body.Id), systemId, parent?.Id, body.Kind, body.ClassId,
                        body.Size, body.OrbitDistance, body.OrbitAngle, x, y, body.Name, flags,
                        SatelliteNamingPolicy: GalaxyInitializerExecution.Scalar(body.Source, "satellite_naming_policy"),
                        HasRing: GalaxyRings.Resolve(content, numeric, body, NextRing), PreventAnomaly: guaranteed?.PreventAnomaly ?? miscEffects.PreventAnomaly,
                        Modifiers: miscEffects.Modifiers.Count == 0 ? null : GalaxyCompiledDescriptor.Freeze(miscEffects.Modifiers));
                    local.Add(body.Id, dto); bodies.Add(dto);
                }
                var star = local.Values.FirstOrDefault(b => b.Kind == GalaxyBodyKind.Star) ?? throw new InvalidDataException("Initializer без звезды: " + result.InitializerId);
                if (player is not null)
                {
                    if (result.HomeCandidates.Count == 0) throw new InvalidDataException("Initializer без home candidate.");
                    var draw = Next(); var home = local[result.HomeCandidates[(int)(draw % result.HomeCandidates.Count)]];
                    if (home.Kind == GalaxyBodyKind.Star || home.ClassId != player.IdealPlanetClass) throw new InvalidDataException("Неподдержанный home carrier/class.");
                    homes.Add(new(player.ParticipantId, player.StartupProfileId, systemId, home.Id, star.Id,
                        result.InitializerId, home.ClassId, home.Size, draw));
                }
                systemIndices.Add(systemId, systems.Count);
                systems.Add(new(systemId, handles[site], points[site], mapped[site], result.InitializerId, result.StarClassId,
                    result.Name, GalaxyCompiledDescriptor.Freeze(result.Flags.Concat(systemFlags[site]).Distinct(StringComparer.Ordinal)), Array.AsReadOnly(result.Belts.Select((belt, ordinal) => catalog.Belts.Resolve(belt, systemId, ordinal)).ToArray()), result.OrbitalLines));
                if (result.InitializerId == "abandoned_gateways_01")
                {
                    var effect = GalaxyInitializerExecution.Optional(compiled.Fields, "init_effect");
                    var (type, maxAngle) = GatewayPlacementRules.Read(effect?.Value ?? throw new GalaxyStructureException("gateway-effect", "Ожидался системный gateway init_effect.", compiled.Definition.Location));
                    var envelope = local.Values.Max(body => Math.Sqrt(body.X * body.X + body.Y * body.Y) + body.Size / 2d);
                    var radius = envelope + 40;
                    if (!double.IsFinite(radius) || radius <= 0) throw new InvalidDataException("Некорректный gateway orbital envelope.");
                    var draw = NativeRandom.Next(gatewayAngleState); gatewayAngleState = draw.NextState; gatewayPositionDraws++;
                    var angle = (decimal)draw.Value * maxAngle / 2147483648m;
                    var radians = (double)angle * Math.PI / 180;
                    var gateway = new GalaxyWorldGateway("gateway/" + site.ToString("D4", CultureInfo.InvariantCulture), systemId,
                        Math.Cos(radians) * radius, Math.Sin(radians) * radius, angle, type,
                        GatewayPlacementRules.PolicyId, EnvelopeRadiusProxy: envelope);
                    gateways.Add(gateway); gatewaySites.Add((site, points[site]));
                }
                void Defer(string? bodyId, string phase, GalaxySyntaxEntry syntax) => deferred.Add(new(
                    systemId + "/request/" + deferred.Count.ToString("D6", CultureInfo.InvariantCulture), systemId, bodyId, phase, syntax.Location, syntax));
                foreach (var item in result.Deferred.Where(d => d.Phase is not ("has_ring" or "class-ring-probability") &&
                    !((player is not null || neighbor) && d.Phase == "system-init_effect") &&
                    !(compiled.Definition.Id == "abandoned_gateways_01" && d.Phase == "system-init_effect") &&
                    !(d.Phase == "init_effect" && d.BodyId is { } effectBody && resolvedMiscEffects.Contains(effectBody))))
                    Defer(item.BodyId is { } b ? BodyId(site, b) : null, item.Phase, item.Syntax);
                if (!neighbor && player is null)
                    foreach (var source in result.Neighbors) Defer(null, "neighbor-system", source);
                if (player is not null && GalaxyInitializerExecution.Optional(compiled.Fields, "init_effect") is { } homeEffect)
                    foreach (var economy in homeEffect.Value.Entries.Where(e => e.Key == "generate_home_system_resources"))
                        Defer(null, "home-system-economy", economy);
                executed.Add(site, (compiled, result, player, rootSite, rootIdeal));
            }

            void ApplyClusterEffects(IEnumerable<int> batchSites)
            {
                foreach (var site in batchSites)
                {
                    if (!executed.TryGetValue(site, out var execution)) continue;
                    var effect = GalaxyInitializerExecution.Optional(execution.Compiled.Fields, "init_effect");
                    if (effect is null) continue;
                    GalaxyEmpireClusterEffects.Apply(SystemId(site), effect.Value, adjacencyById, (targetId, flag) =>
                    {
                        var targetSite = ParseSystemOrdinal(targetId);
                        if (!systemFlags[targetSite].Contains(flag, StringComparer.Ordinal)) systemFlags[targetSite].Add(flag);
                        if (systemIndices.TryGetValue(targetId, out var index))
                        {
                            var target = systems[index];
                            if (!target.Flags.Contains(flag, StringComparer.Ordinal))
                                systems[index] = target with { Flags = GalaxyCompiledDescriptor.Freeze(target.Flags.Append(flag)) };
                        }
                    });
                }
            }

            IReadOnlyList<int> RootSitesInOrder() => homeSites.OrderBy(site => site).ToArray();
            var neighborDraws = 0L;
            if (guaranteedCountForExecution == 0)
            {
                foreach (var site in homeSites.Order())
                {
                    var homeIndex = homeSites.IndexOf(site); var player = request.Settings.Players[homeIndex];
                    ExecuteSite(site, SelectInitializer(site, player), player, site, player.IdealPlanetClass, false);
                }
                ApplyClusterEffects(RootSitesInOrder());
                for (var site = 0; site < count; site++)
                {
                    if (homeSites.Contains(site)) continue;
                    ExecuteSite(site, AdmitMisc(site), null, site, request.Settings.Players[0].IdealPlanetClass, false);
                }
            }
            else
            {
                var roots = RootSitesInOrder();
                foreach (var site in roots)
                {
                    var homeIndex = homeSites.IndexOf(site); var player = request.Settings.Players[homeIndex];
                    ExecuteSite(site, SelectInitializer(site, player), player, site, player.IdealPlanetClass, false);
                }
                ApplyClusterEffects(roots);
                var scheduled = roots.ToHashSet();
                var parentBatch = roots.Select(site => (Site: site, Root: site, Player: request.Settings.Players[homeSites.IndexOf(site)], Ideal: request.Settings.Players[homeSites.IndexOf(site)].IdealPlanetClass)).ToList();
                while (parentBatch.Count > 0)
                {
                    var nextBatch = new List<(int Site, int Root, GalaxyPlayerSlot Player, string Ideal, string Initializer)>();
                    foreach (var parent in parentBatch)
                    {
                        var sourceInitializer = executed[parent.Site].Compiled;
                        foreach (var descriptor in GalaxyGuaranteedNeighbors.Compile(content, sourceInitializer))
                        {
                            neighborDraws++;
                            if (Next() % 100 > descriptor.SpawnChance) continue;
                            var parentId = SystemId(parent.Site);
                            var hopMap = GalaxyGuaranteedNeighbors.BreadthFirst(parentId, adjacencyById, descriptor.MaximumHops);
                            var candidates = Enumerable.Range(0, count)
                                .Where(site => !scheduled.Contains(site) && hopMap.ContainsKey(SystemId(site)) &&
                                    hopMap[SystemId(site)] >= descriptor.MinimumHops && hopMap[SystemId(site)] <= descriptor.MaximumHops)
                                .OrderBy(site => Squared(points[parent.Site], points[site])).ThenBy(site => site).ToArray();
                            var triggerAllowed = guaranteedCountForExecution >= descriptor.MinimumColonies;
                            var picked = false;
                            foreach (var candidate in candidates)
                            {
                                if (descriptor.HasTrigger) { _ = Next(); neighborDraws++; }
                                if (!triggerAllowed) continue;
                                scheduled.Add(candidate);
                                nextBatch.Add((candidate, parent.Root, parent.Player, parent.Ideal, descriptor.InitializerId));
                                var hops = hopMap[SystemId(candidate)];
                                allocations.Add(new(parent.Player.ParticipantId, SystemId(parent.Root), parentId, SystemId(candidate), descriptor.InitializerId, hops, null));
                                picked = true; break;
                            }
                            if (triggerAllowed && !picked)
                                throw new GalaxyStructureException("neighbors/neighbor-underfill",
                                    $"root={SystemId(parent.Root)} parent={parentId} initializer={descriptor.InitializerId}", descriptor.Source);
                        }
                    }
                    foreach (var item in nextBatch)
                    {
                        var compiled = GalaxyInitializerExecution.Compile(content, item.Initializer);
                        ExecuteSite(item.Site, compiled, null, item.Root, item.Ideal, true);
                        var childSystem = SystemId(item.Site);
                        var guaranteedBodies = bodies.Where(body => body.SystemId == childSystem && body.Flags.Contains("prescripted_ideal", StringComparer.Ordinal)).ToArray();
                        var expectedRange = item.Initializer switch
                        {
                            "neighbor_t1_first_colony" => (Minimum: 12, Maximum: 18),
                            "neighbor_t2_second_colony" => (Minimum: 14, Maximum: 24),
                            _ => ((int Minimum, int Maximum)?)null
                        };
                        if (expectedRange is { } range)
                        {
                            if (guaranteedBodies.Length != 1 || guaranteedBodies[0].ClassId != item.Ideal ||
                                guaranteedBodies[0].Size < range.Minimum || guaranteedBodies[0].Size > range.Maximum || !guaranteedBodies[0].PreventAnomaly)
                                throw new GalaxyStructureException("neighbor-guaranteed-body", $"initializer={item.Initializer} root={SystemId(item.Root)} expected ideal {item.Ideal} size {range.Minimum}..{range.Maximum}", compiled.Definition.Location);
                            var allocationIndex = allocations.FindIndex(a => a.ChildSystemId == childSystem);
                            if (allocationIndex >= 0) allocations[allocationIndex] = allocations[allocationIndex] with { GuaranteedBodyId = guaranteedBodies[0].Id };
                        }
                        else if (guaranteedBodies.Length != 0)
                            throw new GalaxyStructureException("neighbor-unexpected-guaranteed-body", item.Initializer, compiled.Definition.Location);
                    }
                    ApplyClusterEffects(nextBatch.Select(item => item.Site));
                    parentBatch = nextBatch.Select(item => (item.Site, item.Root, item.Player, item.Ideal)).ToList();
                }
                for (var site = 0; site < count; site++)
                {
                    if (executed.ContainsKey(site)) continue;
                    ExecuteSite(site, SelectInitializer(site, null), null, site, request.Settings.Players[0].IdealPlanetClass, false);
                }
                policies.Add("GuaranteedNeighborBatchV1: exact source generic-home closure; existing uninitialized unique sites; inclusive spawn gate; no mandatory synthetic fallback");
                ledger.Add(new("guaranteed-neighbor-draws", neighborDraws, selectionState));
            }
            ledger.Add(new(stage, bodies.Count, selectionState));
            ledger.Add(new("misc-admission", miscAdmissionDraws, selectionState));
            ledger.Add(new("gateway-placement", gatewayPositionDraws, gatewayAngleState));
            policies.Add(GatewayPlacementRules.PolicyId + ": локальный proxy envelope=max(гипотенуза координат центра тела + Size/2); gateway radius=envelope+40 local units; отдельный supplied-seed domain; native random_pos координаты не утверждаются.");
            policies.Add("GatewayEuclideanMapDistanceV1: локальная proximity metric для исходного max_distance без явного type; не доказанный native metric.");
            var wormholes = GalaxyWormholeGeneration.Generate(systems, bodies, gateways, homes.Select(home => home.SystemId).ToHashSet(StringComparer.Ordinal), request.Settings.SpecialContent.WormholePairs,
                ref wormholeSelectionState, ref wormholePlacementState, ref wormholeSelectionDraws, ref wormholePlacementDraws);
            ledger.Add(new("wormhole-attempts-requested", request.Settings.SpecialContent.WormholePairs * 5L, null));
            ledger.Add(new("wormhole-pairs-created", wormholes.Count / 2, null));
            ledger.Add(new("wormhole-selection", wormholeSelectionDraws, wormholeSelectionState));
            ledger.Add(new("wormhole-placement", wormholePlacementDraws, wormholePlacementState));
            policies.Add(GalaxyWormholeGeneration.PolicyId + ": WormholeAttemptsFactorFiveV1 local attempt policy (factor×5; get_galaxy_setup_value conversion is not proven); raw fixed/100000 Euclidean distance ≥500; exclusions and endpoint uniqueness; no orphan; separate seed domains; underfill is permitted; native random_pos and metric conversion are not asserted.");
            ledger.Add(new("rings", ringDraws, ringState));
            policies.Add("RingSuppliedSeedV1: Script0 creation-order; explicit no draw; strict uint31 modulo100000 < fixed5 chance; no native global interleaving claim");
            policies.Add("MissingBrokenRingChanceV1: pc_broken without chance_of_ring uses chance0 and one ring draw; local source policy, not native default claim");
            stage = "names";
            var naming = GalaxyNaming.Assign(systems, bodies, starNames, NextName);
            systems = naming.Systems.ToList(); bodies = naming.Bodies.ToList();
            policies.Add("SpatialNamesV1: ordered source pool; separate Names stream; creation-order suffixes; native allocator/ties not certified");
            policies.AddRange(naming.Diagnostics);
            ledger.Add(new(stage, naming.Draws, namesState));
            stage = "publish"; catalog.VerifyStable();
            if (allocations.Select(a => a.ChildSystemId).Distinct(StringComparer.Ordinal).Count() != allocations.Count)
                throw new InvalidDataException("Duplicate guaranteed neighbor allocation.");
            catalog.VerifyStable();
            var world = new GalaxyWorldSnapshot(request, systems, bodies, gateways, wormholes, lanes, homes.OrderBy(h => request.Settings.Players.ToList().FindIndex(p => p.ParticipantId == h.ParticipantId)), allocations, deferred, ledger, policies, nebulae, miscAdmission.Snapshot());
            return new(world, null);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or ArgumentException or FormatException or OverflowException or
            GalaxyStructureException or TriangleBackendException or NotSupportedException)
        { return new(null, new(stage, error is GalaxyStructureException s ? s.Code : stage == "names-source" ? "invalid-names-source" : "generation-failed", error.Message)); }
    }
    private static decimal Squared(NativeFixedPoint a, NativeFixedPoint b) =>
        (decimal)(a.X - b.X) * (a.X - b.X) + (decimal)(a.Y - b.Y) * (a.Y - b.Y);
    private static string SystemId(int site) => "system/" + site.ToString("D4", CultureInfo.InvariantCulture);
    private static string BodyId(int site, int body) => SystemId(site) + "/body/" + body.ToString("D4", CultureInfo.InvariantCulture);
    private static int ParseSystemOrdinal(string systemId) => int.Parse(systemId.AsSpan("system/".Length), NumberStyles.None, CultureInfo.InvariantCulture);
    private static (string Flag, bool PreventAnomaly)? GuaranteedBodyEffects(GalaxySyntaxValue source)
    {
        var effect = GalaxyInitializerExecution.Optional(source, "init_effect");
        if (effect is null) return null;
        if (effect.Value.Kind != GalaxySyntaxKind.Block) throw new GalaxyStructureException("neighbor-body-effect", "Expected body init_effect block.", effect.Value.Location);
        string? flag = null; bool prevent = false;
        foreach (var entry in effect.Value.Entries)
        {
            if (entry.Operator != "=" || entry.Value.Kind != GalaxySyntaxKind.Scalar)
                throw new GalaxyStructureException("neighbor-body-effect", "Unsupported body effect form.", entry.Location);
            if (entry.Key == "set_planet_flag" && entry.Value.Text == "prescripted_ideal") flag = entry.Value.Text;
            else if (entry.Key == "prevent_anomaly" && entry.Value.Text == "yes") prevent = true;
            else throw new GalaxyStructureException("neighbor-body-effect", "Unsupported active body effect " + entry.Key, entry.Location);
        }
        return flag is not null && prevent ? (flag, true) : throw new GalaxyStructureException("neighbor-body-effect", "Expected prescripted_ideal and prevent_anomaly=yes.", effect.Value.Location);
    }

    internal static (bool Resolved, bool PreventAnomaly, IReadOnlyList<GalaxyWorldBodyModifier> Modifiers) MiscBodyEffects(string initializerId, GalaxyLocalBody body)
    {
        var effect = GalaxyInitializerExecution.Optional(body.Source, "init_effect");
        if (effect is null) return (false, false, []);
        if (initializerId == "special_init_01" && body.ClassId == "pc_broken")
        {
            if (effect.Value.Kind != GalaxySyntaxKind.Block || effect.Value.Entries.Count != 1 ||
                effect.Value.Entries[0].Key != "prevent_anomaly" || effect.Value.Entries[0].Operator != "=" ||
                effect.Value.Entries[0].Value.Kind != GalaxySyntaxKind.Scalar || effect.Value.Entries[0].Value.Text != "yes")
                throw new GalaxyStructureException("misc-body-effect", "Не поддержан init_effect разрушенной планеты.", effect.Value.Location);
            return (true, true, []);
        }
        if (initializerId == "trappist_initializer")
        {
            if (effect.Value.Kind != GalaxySyntaxKind.Block || effect.Value.Entries.Count != 1 || effect.Value.Entries[0].Key != "add_modifier" ||
                effect.Value.Entries[0].Operator != "=" || effect.Value.Entries[0].Value.Kind != GalaxySyntaxKind.Block)
                throw new GalaxyStructureException("misc-body-effect", "Не поддержан init_effect Trappist.", effect.Value.Location);
            var modifier = effect.Value.Entries[0].Value;
            var id = GalaxyInitializerExecution.Scalar(modifier, "modifier");
            var days = GalaxyInitializerExecution.Scalar(modifier, "days");
            if (modifier.Entries.Count != 2 || id != "terraforming_candidate" || days != "-1")
                throw new GalaxyStructureException("misc-body-effect", "Ожидался бессрочный terraforming_candidate (days=-1).", modifier.Location);
            return (true, false, [new(id, -1)]);
        }
        return (false, false, []);
    }
}

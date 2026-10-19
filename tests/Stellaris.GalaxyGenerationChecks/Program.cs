using System.Security.Cryptography;
using System.Text.Json;
using Stellaris.Core.Assets;
using Stellaris.Core.Content.Galaxy;
using Stellaris.Core.Galaxy;
using Stellaris.Core.Scripting;

if (args is ["--wormhole-only", var wormholeRoot, var wormholeWorker, var wormholeWorkerSha])
{
    GalaxyWormholeChecks.RunFocused(wormholeRoot, wormholeWorker, wormholeWorkerSha);
    return 0;
}

if (args is ["--wormhole-assets-only", var wormholeAssetsRoot])
{
    var requirements = new GeneratedWorldVisualRequirements(["pc_continental"], ["gateway_ruined", "natural_wormhole"]);
    var manifest = await new M3AssetManifestBuilder().BuildAsync(Path.GetFullPath(wormholeAssetsRoot), requirements);
    var gateway = manifest.Visuals.SingleOrDefault(visual => visual.VisualId == "object.gateway_ruined");
    var wormhole = manifest.Visuals.SingleOrDefault(visual => visual.VisualId == "object.natural_wormhole");
    if (gateway is null || !gateway.MaterialProfile.Contains("SourceMip512V1/StaticGatewayBindPoseV1", StringComparison.Ordinal) ||
        wormhole is null || !wormhole.MaterialProfile.Contains("WormholeNativeMip0V1/StaticWormholeRingsV1", StringComparison.Ordinal) ||
        !manifest.Files.Any(file => file.RelativePath == "gfx/models/add_ons/wormhole_rings.dds") ||
        !manifest.Files.Any(file => file.RelativePath == "gfx/models/add_ons/wormhole_stretch.dds") ||
        !manifest.Files.Any(file => file.RelativePath == "gfx/models/ships/megastructures/gateway/gateway.mesh"))
        throw new InvalidOperationException("C# mixed gateway/wormhole manifest omitted source visuals, policy, or closure paths.");
    var output = Path.Combine(Environment.CurrentDirectory, "artifacts", "galaxy-wormholes", "mixed-manifest.json");
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    await File.WriteAllTextAsync(output, JsonSerializer.Serialize(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    Console.WriteLine($"PASS C# mixed source manifest visuals={manifest.Visuals.Count} files={manifest.Files.Count} profile={manifest.ProfileId}");
    Console.WriteLine("Wrote ignored manifest metadata: artifacts/galaxy-wormholes/mixed-manifest.json");
    return 0;
}

// Проверка полного мира с production worker, которому не нужны тестовые companion executables.
if (args is ["--names-only", var namesRoot, var namesWorker, var namesWorkerSha])
{
    var namesBackend = new NativeTriangleBackend(Path.GetFullPath(namesWorker), namesWorkerSha,
        Path.Combine(AppContext.BaseDirectory, "names-triangle-scratch"), TimeSpan.FromSeconds(30));
    GalaxyNamingChecks.Run(namesRoot, namesBackend);
    return 0;
}

if (args is ["--guaranteed-checks", var guaranteedRoot])
{
    var catalog = GalaxyLobbyCatalog.Import(Path.GetFullPath(guaranteedRoot), new([], []));
    GalaxyGuaranteedNeighborChecks.Run(catalog.Content);
    Console.WriteLine("PASS guaranteed neighbor source/graph checks");
    return 0;
}

if (args is ["--world-checks", var worldRoot, var worldWorker, var worldWorkerSha])
{
    var worldChecks = 0;
    var worldBackend = new NativeTriangleBackend(Path.GetFullPath(worldWorker), worldWorkerSha,
        Path.Combine(AppContext.BaseDirectory, "world-triangle-scratch"), TimeSpan.FromSeconds(30));
    GalaxyWorldChecks.Run(worldRoot, worldBackend, (name, body) => { body(); worldChecks++; Console.WriteLine("PASS " + name); });
    Console.WriteLine($"PASS {worldChecks} world checks");
    return 0;
}

if (args is ["--coverage", var inventoryRoot, var inventoryOutput])
{
    var manifest = GalaxyCoverageManifest.Scan(inventoryRoot);
    File.WriteAllText(inventoryOutput, manifest.ToJson());
    Console.WriteLine($"Coverage files={manifest.Files.Count}, parsed={manifest.Files.Count(f => f.Parsed)}, archives={manifest.Archives.Count}, definitions={manifest.Definitions.Count(d => d.Kind == GalaxyInventoryEntryKind.Definition)}, duplicates={manifest.DuplicateIds.Count}, missingRoots={manifest.MissingRoots.Count}");
    foreach (var family in manifest.Definitions.Where(d => d.Kind == GalaxyInventoryEntryKind.Definition).GroupBy(d => d.Family))
        Console.WriteLine($"{family.Key}: {family.Count()}");
    Console.WriteLine($"Unparsed={manifest.Files.Count(f => !f.Parsed)}, unresolved-literal-links={manifest.Definitions.Sum(d => d.References.Count(r => r.Status == "unresolved-literal-target"))}; resolved={manifest.ResolvedCatalog}; complete={manifest.VanillaContentComplete}");
    return 0;
}

if (args is ["--expect-reparse", var reparseRoot])
{
    try
    {
        GalaxyCatalog.Import(reparseRoot, ["map/setup_scenarios/a.txt"], ["map/galaxy/shapes.txt"]);
    }
    catch (IOException error) when (error.Message.Contains("Reparse point", StringComparison.Ordinal))
    {
        Console.WriteLine("PASS reparse source path rejected");
        return 0;
    }
    throw new Exception("Ожидался отказ на reparse point.");
}

// Собственные synthetic definitions; оригинальные scripts не копируются в fixtures.
const string scenario = "setup_scenario = { name = alpha num_stars = 17 radius = 21.5 supports_shape = round supports_shape = ring extra = { token = preserved } }";
const string shapes = "round = { stars_min_dist = 7 } ring = { ring = { width = 0.4 } }";
var fixture = Path.Combine(AppContext.BaseDirectory, "synthetic-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(Path.Combine(fixture, "map/setup_scenarios"));
Directory.CreateDirectory(Path.Combine(fixture, "map/galaxy"));
var checks = 0;
void Check(string name, Action body)
{
    body();
    checks++;
    Console.WriteLine($"PASS {name}");
}
void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Ожидалось {expected}, получено {actual}");
}
void Throws<T>(Action body) where T : Exception
{
    try { body(); }
    catch (T) { return; }
    throw new Exception($"Ожидалось исключение {typeof(T).Name}");
}
void Write(string path, string value) => File.WriteAllText(Path.Combine(fixture, path), value);
void Reset()
{
    Write("launcher-settings.json", "{\"rawVersion\":\"v4.5.2\"}");
    Write("map/setup_scenarios/a.txt", scenario);
    Write("map/setup_scenarios/b.txt", scenario.Replace("alpha", "beta").Replace("17", "23"));
    Write("map/galaxy/shapes.txt", shapes);
}
GalaxyCatalog Import() => GalaxyCatalog.Import(fixture,
    ["map/setup_scenarios/b.txt", "map/setup_scenarios/a.txt"], ["map/galaxy/shapes.txt"]);
SystemAllocatorSnapshot FreshSystems(int capacity, int preparedChunks = 1) =>
    SystemAllocatorSnapshot.CreateFresh(capacity, preparedChunks, 0, 0, SystemResolverBinding.SameManager,
        false, false, new NativeSystemHandle(uint.MaxValue));
SystemCreationInput[] SystemInputs(int count) => Enumerable.Range(0, count)
    .Select(i => new SystemCreationInput($"supplied_name_{i}", (ulong)(9000 + i))).ToArray();
SystemAllocatorSnapshot ChangeSystems(SystemAllocatorSnapshot original, uint? freeHead = null, uint? highest = null,
    int? active = null, byte? stop34 = null, byte? stop35 = null, SystemResolverBinding? resolver = null,
    IEnumerable<SystemSlotSnapshot>? slots = null, int? capacity = null) =>
    new(capacity ?? original.Capacity, freeHead ?? original.FreeHead, highest ?? original.HighestActive,
        active ?? original.ActiveCount, stop34 ?? original.Stop34, stop35 ?? original.Stop35,
        original.PreparedChunkCount, resolver ?? original.Resolver, original.ManagerDirty, original.WorldDirty,
        original.LastReturnedHandle, slots ?? original.Slots);

NativeTriangleBackend? triangleBackend = null;

try
{
    if (args is ["--triangle-only", var trianglePath, var triangleSha])
    {
        var isolatedBackend = new NativeTriangleBackend(Path.GetFullPath(trianglePath), triangleSha,
            Path.Combine(Path.GetTempPath(), "triangle-player-check-" + Guid.NewGuid().ToString("N")), TimeSpan.FromSeconds(30));
        TriangleBackendChecks.Run(isolatedBackend, Check, Path.GetFullPath(trianglePath));
        Console.WriteLine("PASS focused Triangle backend checks");
        return 0;
    }
    if (args.Length == 4 && args[1] == "--triangle-worker")
    {
        triangleBackend = new NativeTriangleBackend(Path.GetFullPath(args[2]), args[3],
            Path.Combine(AppContext.BaseDirectory, "triangle-scratch"), TimeSpan.FromSeconds(30));
        TriangleBackendChecks.Run(triangleBackend, Check, Path.GetFullPath(args[2]));
        CandidateGraphChecks.Run(triangleBackend, Check);
        HyperlanePruningChecks.RunWithBackend(triangleBackend, Check);
        args = [args[0]];
    }
    else if (args.Contains("--triangle-worker"))
        throw new ArgumentException("Требуется: game-root --triangle-worker absolute-exe sha256.");
    Reset();
    GalaxyCoverageChecks.Run(Check);
    GalaxyCatalogReadyChecks.Run(Check);
    HyperlanePruningChecks.Run(Check);
    GalaxyInitializerChecks.Run(Check);
    GalaxyNebulaChecks.Run(Check);
    Check("source order, setup values, preservation, manifest", () =>
    {
        var catalog = Import();
        Equal("beta,alpha", string.Join(',', catalog.Scenarios.Select(item => item.Id)));
        Equal("round,ring", string.Join(',', catalog.Shapes.Select(item => item.Id)));
        var plan = GalaxyPreparation.Prepare(catalog, "alpha", "ring");
        Equal(17, plan.TargetStars);
        Equal(21.5m, plan.Radius);
        Equal("preserved", plan.Scenario.Fields.Required("extra").AsBlock().Required("token").AsScalar());
        Equal(4, catalog.Files.Count);
        foreach (var file in catalog.Files)
        {
            var bytes = File.ReadAllBytes(Path.Combine(fixture, file.Path));
            Equal(Convert.ToHexString(SHA256.HashData(bytes)), file.Sha256);
            Equal(bytes.LongLength, file.Bytes);
        }
        Equal("launcher-settings.json,map/setup_scenarios/b.txt,map/setup_scenarios/a.txt,map/galaxy/shapes.txt",
            string.Join(',', catalog.Files.Select(file => file.Path)));
        Equal(string.Join(',', catalog.Files), string.Join(',', Import().Files));
    });
    Check("immutable snapshot", () =>
    {
        var catalog = Import();
        Throws<NotSupportedException>(() => ((IList<GalaxyDefinition>)catalog.Scenarios).Clear());
        Throws<NotSupportedException>(() => ((IList<GalaxySourceFile>)catalog.Files).Clear());
        Throws<NotSupportedException>(() => ((IList<ScriptEntry>)catalog.Scenarios[0].Fields.Entries).Clear());
    });
    Check("explicit unsupported stages", () =>
    {
        var plan = GalaxyPreparation.Prepare(Import(), "alpha", "round");
        var gate = GalaxyPreparation.GenerateCoordinates(plan);
        Equal(GalaxyStage.Coordinates, gate.Stage);
        if (gate.RequiredContracts.Contains("M01") || !gate.RequiredContracts.Contains("M06")) throw new Exception("Неверный seed/shape coverage gate");
        foreach (var stage in Enum.GetValues<GalaxyStage>())
            if (GalaxyPreparation.Unsupported(stage).RequiredContracts.Count == 0) throw new Exception("Пустой gate");
        Throws<ArgumentOutOfRangeException>(() => GalaxyPreparation.Unsupported((GalaxyStage)999));
    });
    Check("setup errors without defaults", () =>
    {
        var catalog = Import();
        Throws<KeyNotFoundException>(() => GalaxyPreparation.Prepare(catalog, "absent", "round"));
        Throws<KeyNotFoundException>(() => GalaxyPreparation.Prepare(catalog, "alpha", "absent"));
        Write("map/setup_scenarios/a.txt", scenario.Replace("supports_shape = ring", "supports_shape = other"));
        Throws<InvalidDataException>(() => GalaxyPreparation.Prepare(Import(), "alpha", "ring"));
        Reset();
    });
    Check("stability changes and disappearance", () =>
    {
        var catalog = Import();
        Write("map/galaxy/shapes.txt", shapes + " # changed");
        Throws<IOException>(catalog.VerifyStable);
        Throws<IOException>(() => GalaxyPreparation.Prepare(catalog, "alpha", "round"));
        Reset();
        catalog = Import();
        File.Delete(Path.Combine(fixture, "map/setup_scenarios/a.txt"));
        Throws<FileNotFoundException>(catalog.VerifyStable);
        Reset();
    });
    Check("missing, malformed, duplicates and version", () =>
    {
        Throws<FileNotFoundException>(() => GalaxyCatalog.Import(fixture, ["map/setup_scenarios/missing.txt"], ["map/galaxy/shapes.txt"]));
        Write("map/setup_scenarios/a.txt", "setup_scenario = {");
        Throws<FormatException>(() => Import());
        Reset();
        Write("map/setup_scenarios/a.txt", scenario.Replace("alpha", "beta"));
        Throws<InvalidDataException>(() => Import());
        Reset();
        Write("map/galaxy/shapes.txt", shapes + " round = {}");
        Throws<InvalidDataException>(() => Import());
        Reset();
        Write("launcher-settings.json", "{\"rawVersion\":\"v4.5.3\"}");
        Throws<InvalidDataException>(() => Import());
        Write("launcher-settings.json", "{\"rawVersion\":\"v4.5.3\",\"rawVersion\":\"v4.5.2\"}");
        Throws<InvalidDataException>(() => Import());
        Write("launcher-settings.json", "{");
        Throws<System.Text.Json.JsonException>(() => Import());
        Reset();
        Throws<InvalidDataException>(() => GalaxyCatalog.Import(fixture,
            ["map/setup_scenarios/a.txt", "map/setup_scenarios/a.txt"], ["map/galaxy/shapes.txt"]));
        Throws<InvalidDataException>(() => GalaxyCatalog.Import(fixture, [], ["map/galaxy/shapes.txt"]));
    });
    Check("used-field validation, static gate", () =>
    {
        foreach (var invalid in new[] { "0", "-2", "1.5", "2147483648", "@unknown" })
        {
            Write("map/setup_scenarios/a.txt", scenario.Replace("num_stars = 17", "num_stars = " + invalid));
            Throws<InvalidDataException>(() => GalaxyPreparation.Prepare(Import(), "alpha", "round"));
        }
        foreach (var invalid in new[] { "num_stars = 17 num_stars = 3", "num_stars > 17" })
        {
            Write("map/setup_scenarios/a.txt", scenario.Replace("num_stars = 17", invalid));
            if (invalid.Contains('>')) Throws<InvalidDataException>(() => GalaxyPreparation.Prepare(Import(), "alpha", "round"));
            else Throws<FormatException>(() => GalaxyPreparation.Prepare(Import(), "alpha", "round"));
        }
        Write("map/setup_scenarios/a.txt", scenario.Replace("radius = 21.5", "radius = 0"));
        Throws<InvalidDataException>(() => GalaxyPreparation.Prepare(Import(), "alpha", "round"));
        Write("map/setup_scenarios/a.txt", "static_galaxy_scenario = { name = alpha }");
        Throws<NotSupportedException>(() => Import());
        Reset();
    });
    Check("path boundaries", () =>
    {
        foreach (var path in new[] { "map/setup_scenarios/../../../outside.txt", "map/setup_scenarios/./a.txt",
                     "map/setup_scenarios//a.txt", "map/setup_scenarios/a.txt:stream.txt", "map/setup_scenarios/.. /a.txt",
                     Path.Combine(fixture, "map/setup_scenarios/a.txt") })
            Throws<ArgumentException>(() => GalaxyCatalog.Import(fixture, [path], ["map/galaxy/shapes.txt"]));
    });
    Check("native system handle, chunk, unsigned highest and growth arithmetic vectors", () =>
    {
        foreach (var v in new (uint Slot, byte OldGeneration, uint Expected)[]
                 { (0, 0xFF, 0), (1, 0xFF, 1), (1023, 0xFF, 0x3FF), (1024, 0xFF, 0x400),
                     (7, 0x12, 0x13000007), (7, 0x7F, 0x80000007), (7, 0xFE, 0xFF000007), (7, 0xFF, 7) })
            Equal(v.Expected, NativeSystemHandles.ComposeAllocated(v.Slot, v.OldGeneration).Value);
        Equal(new SystemChunkLocation(0, 5155920), NativeSystemHandles.Locate(1023));
        Equal(new SystemChunkLocation(1, 0), NativeSystemHandles.Locate(1024));
        Equal(false, new NativeSystemHandle(0x80000007).IsSentinel);
        Equal((byte)0x80, new NativeSystemHandle(0x80000007).Generation);
        Equal(true, new NativeSystemHandle(uint.MaxValue).IsSentinel);
        Equal(0u, NativeSystemHandles.HighestAfterAllocation(uint.MaxValue, 0));
        Equal(1u, NativeSystemHandles.HighestAfterAllocation(0, 1));
        Equal(1u, NativeSystemHandles.HighestAfterAllocation(1, 0));
        Equal(0x80000000u, NativeSystemHandles.HighestAfterAllocation(0x80000000, 7));
        Equal(2048u, NativeSystemHandles.GrowthTarget(1024));
        Equal(0x00FFFFFEu, NativeSystemHandles.GrowthTarget(0x00FFFFFE));
        Throws<ArgumentOutOfRangeException>(() => NativeSystemHandles.ComposeAllocated(0x00FFFFFE, 0));
        Throws<ArgumentOutOfRangeException>(() => NativeSystemHandles.Locate(0x00FFFFFF));
        Throws<ArgumentOutOfRangeException>(() => NativeSystemHandles.GrowthTarget(0));
    });
    Check("system objects inline acceptance, known ctor fields, raw clamp and registry identity", () =>
    {
        var original = FreshSystems(4);
        var payload = SystemInputs(3);
        var result = SystemMaterialization.Replay(new PreRelaxationCandidate[]
            { new(0, 0, 0, 0, -1), new(0, 0, 0, 0, -1), new(51000000, 0, 51000000, 0, 2),
                new(-51000000, 0, 51000000, 0, -1) }, 3, 1000000, original, payload);
        Equal(SystemMaterializationStatus.TargetReached, result.Status);
        Equal(3, result.Systems.Count);
        Equal(4L, result.EvaluatedCandidates);
        Equal(1L, result.RejectedCandidates);
        for (var i = 0; i < result.Systems.Count; i++)
        {
            var system = result.Systems[i];
            Equal((uint)i, system.Handle.Value);
            Equal(payload[i].NameInput, system.NameInput);
            Equal<ulong?>(payload[i].CreationContext, system.CreationContext);
            Equal(0L, system.PositionAuxiliary);
            Equal(uint.MaxValue, system.PositionSentinel);
            Equal(false, system.Randomized);
            Equal(true, system.PositionHelperInvoked && system.IsFrozen);
            Equal(NativeSystemObjectPhase.Appended, system.Phase);
            Equal(true, ReferenceEquals(system, result.Registry[system.Handle]));
            Equal(true, result.Allocator.Slots[i].IsRegistered);
            Equal<uint?>((uint)i, result.Allocator.Slots[i].ObjectHandleWord);
            Equal(NativeSystemHandles.ReservedSlot, result.Allocator.Slots[i].NextFree);
        }
        Equal(2, result.Systems[1].ArmIndex);
        Equal(-1, result.Systems[2].ArmIndex);
        Equal<NativeFixedPoint?>(new(51000000, 0), result.Systems[1].RawPosition);
        Equal(new NativeFixedPoint(50000000, 0), result.Systems[1].Position);
        Equal(new NativeFixedPoint(-50000000, 0), result.Systems[2].Position);
        Equal(3, result.Allocator.ActiveCount);
        Equal(2u, result.Allocator.HighestActive);
        Equal(3u, result.Allocator.FreeHead);
        Equal(NativeSystemHandles.ReservedSlot, result.Allocator.Slots[3].PreviousFree);
        Equal(new NativeSystemHandle(2), result.Allocator.LastReturnedHandle);
        Equal(true, result.Allocator.WorldDirty && result.Allocator.ManagerDirty && result.NativeSpatialRefreshPending);
        Equal(0, original.ActiveCount);
        Equal<uint?>(uint.MaxValue, original.Slots[0].ObjectHandleWord);
        Throws<NotSupportedException>(() => ((IList<NativeSystemObject>)result.Systems).Clear());
        Throws<NotSupportedException>(() => ((IDictionary<NativeSystemHandle, NativeSystemObject>)result.Registry).Clear());
    });
    Check("system materialization fresh slot1023/1024 boundary and prepared storage gate", () =>
    {
        var candidates = new PreRelaxationCandidate[1025];
        var result = SystemMaterialization.Replay(candidates, 1025, 0, FreshSystems(1025, 2), SystemInputs(1025));
        Equal(SystemMaterializationStatus.TargetReached, result.Status);
        Equal(1025, result.Systems.Count);
        Equal(0x3FFu, result.Systems[1023].Handle.Value);
        Equal(new SystemChunkLocation(0, 5155920), result.Systems[1023].Location);
        Equal(0x400u, result.Systems[1024].Handle.Value);
        Equal(new SystemChunkLocation(1, 0), result.Systems[1024].Location);
        Equal(NativeSystemHandles.ReservedSlot, result.Allocator.FreeHead);
        var unavailable = FreshSystems(1025, 1);
        var blocked = SystemMaterialization.Replay(candidates, 1025, 0, unavailable, SystemInputs(1025));
        Equal(SystemMaterializationFailureKind.ChunkStorageUnavailable, blocked.Failure!.Kind);
        Equal(SystemFailureStage.PreflightBeforeAllocation, blocked.Failure.Stage);
        Equal(false, blocked.Failure.IdConsumed);
        Equal(0, blocked.Systems.Count);
        Equal(0L, blocked.EvaluatedCandidates);
        Equal(true, ReferenceEquals(unavailable, blocked.Allocator));
    });
    Check("system allocator explicit snapshot corruption and unsupported failure paths", () =>
    {
        var basis = FreshSystems(2);
        SystemMaterializationResult Run(SystemAllocatorSnapshot snapshot, int target = 2) =>
            SystemMaterialization.Replay(new PreRelaxationCandidate[2], target, 0, snapshot, SystemInputs(target));
        var cycle = basis.Slots.ToArray();
        cycle[1] = cycle[1] with { NextFree = 0 };
        var wrongPrevious = basis.Slots.ToArray();
        wrongPrevious[0] = wrongPrevious[0] with { PreviousFree = uint.MaxValue };
        foreach (var invalid in new[] { ChangeSystems(basis, freeHead: 2), ChangeSystems(basis, slots: cycle),
                     ChangeSystems(basis, slots: wrongPrevious), ChangeSystems(basis, slots: basis.Slots.Take(1)),
                     ChangeSystems(basis, resolver: (SystemResolverBinding)99), ChangeSystems(basis, capacity: 0) })
        {
            var result = Run(invalid);
            Equal(SystemMaterializationStatus.InvalidInitialState, result.Status);
            Equal(SystemMaterializationFailureKind.InvalidSnapshot, result.Failure!.Kind);
            Equal(0, result.Systems.Count);
        }
        foreach (var flags in new[] { ChangeSystems(basis, stop34: 1), ChangeSystems(basis, stop35: 255) })
            Equal(SystemMaterializationFailureKind.AllocationStopped, Run(flags).Failure!.Kind);
        foreach (var resolver in new[] { SystemResolverBinding.MissingManager, SystemResolverBinding.DifferentManager })
        {
            var blocked = Run(ChangeSystems(basis, resolver: resolver));
            Equal(SystemMaterializationFailureKind.UnsafeResolverBinding, blocked.Failure!.Kind);
            Equal(SystemFailureStage.PreflightBeforeAllocation, blocked.Failure.Stage);
            Equal(false, blocked.Failure.IdConsumed); // Это safe preflight, НЕ native postallocation failure.
            Equal(0L, blocked.EvaluatedCandidates);
        }
        Equal(SystemMaterializationFailureKind.CapacityGrowthUnsupported, Run(FreshSystems(1)).Failure!.Kind);
        Equal(SystemMaterializationFailureKind.NonFreshStateUnsupported, Run(ChangeSystems(basis, active: 1, highest: 0)).Failure!.Kind);
        var reused = basis.Slots.ToArray();
        reused[0] = reused[0] with { ObjectHandleWord = 0x12FFFFFF };
        Equal(SystemMaterializationFailureKind.NonFreshStateUnsupported, Run(ChangeSystems(basis, slots: reused)).Failure!.Kind);
        var copied = ChangeSystems(basis, slots: reused);
        reused[0] = reused[0] with { ObjectHandleWord = 0 };
        Equal<uint?>(0x12FFFFFF, copied.Slots[0].ObjectHandleWord);
        Throws<NotSupportedException>(() => ((IList<SystemSlotSnapshot>)copied.Slots).Clear());
        Throws<ArgumentException>(() => SystemMaterialization.Replay(Array.Empty<PreRelaxationCandidate>(), 2, 0, basis, SystemInputs(1)));
    });
    Check("system retry failure preserves consumed prefix and rejects consume no ID", () =>
    {
        var result = SystemMaterialization.Replay(new PreRelaxationCandidate[10003], 2, 1000000, FreshSystems(2), SystemInputs(2));
        Equal(SystemMaterializationStatus.RetryBudgetExhausted, result.Status);
        Equal(1, result.Systems.Count);
        Equal(1, result.Allocator.ActiveCount);
        Equal(0u, result.Allocator.HighestActive);
        Equal(1u, result.Allocator.FreeHead);
        Equal(10002L, result.EvaluatedCandidates);
        Equal(10001L, result.RejectedCandidates);
        Equal(false, result.Allocator.Slots[1].IsRegistered);
        Equal<uint?>(uint.MaxValue, result.Allocator.Slots[1].ObjectHandleWord);
        var prefix = SystemMaterialization.Replay(new PreRelaxationCandidate[1], 2, 1000000, FreshSystems(2), SystemInputs(2));
        Equal(SystemMaterializationStatus.InputExhausted, prefix.Status);
        Equal(1, prefix.Systems.Count);
    });
    Check("system seeded pipeline RNG equivalence and safe preflight consumes no draws", () =>
    {
        Write("map/galaxy/shapes.txt", "round = { core_radius_perc = 0.25 num_stars_core_perc = 0 stars_min_dist = 100 } ");
        var plan = GalaxyPreparation.Prepare(Import(), "alpha", "round") with { TargetStars = 2 };
        var journal = PreRelaxationPlacementJournal.GenerateJournal(plan, 1);
        var result = SystemMaterialization.Generate(plan, 1, FreshSystems(2), SystemInputs(2));
        Equal(SystemMaterializationStatus.RetryBudgetExhausted, result.Result.Status);
        Equal(journal.NextState, result.NextState);
        Equal(journal.Journal.EvaluatedCandidates, result.Result.EvaluatedCandidates);
        Equal(journal.Journal.Accepted[0].StoredPosition, result.Result.Systems[0].Position);
        var blocked = SystemMaterialization.Generate(plan, 1, FreshSystems(1), SystemInputs(2));
        Equal(NativeRandom.FromSpecifiedSeed(1), blocked.NextState);
        Equal<long?>(null, blocked.Rotation);
        Equal(0, blocked.Result.Systems.Count);
        Reset();
    });
    Check("relaxation ordinary input independent integer-before-float vectors", () =>
    {
        foreach (var v in new (long Raw, uint Bits)[] { (999, 0), (-999, 0), (1000, 0x3F800000),
                     (-1000, 0xBF800000), (2500000, 0x451C4000), (-2500000, 0xC51C4000), (50000000, 0x47435000) })
            Equal(v.Bits, (uint)BitConverter.SingleToInt32Bits(RelaxationAdapters.ConvertInputCoordinate(v.Raw)));
        Equal(0f, RelaxationAdapters.ConvertInputCoordinate(-999)); // Direct float(raw/1000) дал бы −0.999.
        Throws<ArgumentOutOfRangeException>(() => RelaxationAdapters.ConvertInputCoordinate(50000001));
        Throws<ArgumentOutOfRangeException>(() => RelaxationAdapters.ConvertInputCoordinate(-50000001));
    });
    Check("relaxation dummy independent axis/order/bits vectors and immutable counts", () =>
    {
        var stored = new NativeFixedPoint[] { new(2500000, -2500000), new(1000, -999) };
        var input = RelaxationAdapters.PrepareInput(stored, 2500000, new(256, 1));
        Equal(2, input.OrdinaryCount);
        Equal(256, input.DummyCount);
        Equal(258, input.Sites.Count);
        Equal(new NativeFloatPoint(2500, -2500), input.Sites[0]);
        Equal(new NativeFloatPoint(1, 0), input.Sites[1]);
        foreach (var v in new (int Index, uint X, uint Y)[]
                 { (0, 0x00000000, 0x450D4000), (1, 0x425C0000, 0x450D2000), (255, 0xC2640000, 0x450D2000) })
        {
            var point = input.Sites[input.OrdinaryCount + v.Index];
            Equal(v.X, (uint)BitConverter.SingleToInt32Bits(point.X));
            Equal(v.Y, (uint)BitConverter.SingleToInt32Bits(point.Y));
        }
        stored[0] = new(0, 0);
        Equal(new NativeFloatPoint(2500, -2500), input.Sites[0]);
        Throws<NotSupportedException>(() => ((IList<NativeFloatPoint>)input.Sites).Clear());
        var noDummy = RelaxationAdapters.PrepareInput(stored, 2500000, new(0, 0));
        Equal(2, noDummy.Sites.Count);
        Equal(new RelaxationAdapterSettings(0, 0), noDummy.Settings);
        Equal(0, RelaxationAdapters.PrepareInput(Array.Empty<NativeFixedPoint>(), 2500000, new(0, 2)).Sites.Count);
    });
    Check("relaxation output independent cvtt-before-division vectors and dummy exclusion", () =>
    {
        foreach (var v in new (float Site, long Fixed)[] { (1.999f, 1000), (-1.999f, -1000),
                     (0.999f, 0), (-0.999f, 0), (50000, 50000000), (-50000, -50000000) })
            Equal(v.Fixed, RelaxationAdapters.ConvertOutputCoordinate(v.Site));
        var prepared = RelaxationAdapters.PrepareInput(new NativeFixedPoint[] { new(0, 0), new(0, 0) }, 2500000, new(1, 1));
        var supplied = new NativeFloatPoint[] { new(1.999f, -1.999f), new(50000, -50000), new(9999, 9999) };
        var restored = RelaxationAdapters.RestoreOrdinaryCoordinates(prepared, supplied);
        Equal(2, restored.Count);
        Equal(new NativeFixedPoint(1000, -1000), restored[0]);
        Equal(new NativeFixedPoint(50000000, -50000000), restored[1]);
        supplied[0] = new(0, 0);
        Equal(new NativeFixedPoint(1000, -1000), restored[0]);
        Throws<NotSupportedException>(() => ((IList<NativeFixedPoint>)restored).Clear());
    });
    Check("relaxation explicit invalid/unsupported domain and capacity", () =>
    {
        foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, 50000.5f, -50000.5f, float.MaxValue })
            Throws<ArgumentOutOfRangeException>(() => RelaxationAdapters.ConvertOutputCoordinate(invalid));
        Throws<ArgumentOutOfRangeException>(() => RelaxationAdapters.PrepareInput(Array.Empty<NativeFixedPoint>(), 0, new(0, 0)));
        Throws<ArgumentOutOfRangeException>(() => RelaxationAdapters.PrepareInput(Array.Empty<NativeFixedPoint>(), -1, new(0, 0)));
        Throws<ArgumentOutOfRangeException>(() => RelaxationAdapters.PrepareInput(Array.Empty<NativeFixedPoint>(), 2500000, new(-1, 0)));
        Throws<ArgumentOutOfRangeException>(() => RelaxationAdapters.PrepareInput(Array.Empty<NativeFixedPoint>(), 2500000, new(0, -1)));
        Throws<OverflowException>(() => RelaxationAdapters.PrepareInput(new NativeFixedPoint[1], 2500000, new(int.MaxValue, 1)));
        Throws<OverflowException>(() => RelaxationAdapters.PrepareInput(Array.Empty<NativeFixedPoint>(), long.MaxValue, new(1, 1)));
        Throws<ArgumentOutOfRangeException>(() => RelaxationAdapters.PrepareInput(Array.Empty<NativeFixedPoint>(), 100000000, new(1, 1)));
        var prepared = RelaxationAdapters.PrepareInput(new NativeFixedPoint[1], 2500000, new(1, 1));
        Throws<ArgumentException>(() => RelaxationAdapters.RestoreOrdinaryCoordinates(prepared, new NativeFloatPoint[1]));
        Throws<ArgumentOutOfRangeException>(() => RelaxationAdapters.RestoreOrdinaryCoordinates(prepared,
            new NativeFloatPoint[] { new(0, 0), new(float.NaN, 0) }));
    });
    Check("relaxation adapters compose with existing supplied pass, without backend/RNG", () =>
    {
        var stored = new NativeFixedPoint[] { new(100000, 100000), new(200000, 200000), new(300000, 300000), new(400000, 400000) };
        var input = RelaxationAdapters.PrepareInput(stored, 2500000, new(1, 1));
        var result = SuppliedTessellationPass.Apply(input.Sites.ToArray(),
            new NativeFloatPoint[] { new(0, 0), new(6, 0), new(0, 3) },
            new NativeIncidentEdge[] { new(0, 1, 0, 1), new(0, 3, 2, 0), new(2, 0, 2, 1) });
        var restored = RelaxationAdapters.RestoreOrdinaryCoordinates(input, result.ToArray());
        Equal(4, restored.Count);
        Equal(new NativeFixedPoint(2000, 1000), restored[0]);
        for (var i = 1; i < stored.Length; i++) Equal(stored[i], restored[i]);
        Equal(new NativeFloatPoint(100, 100), input.Sites[0]);
        Equal(true, GalaxyPreparation.Unsupported(GalaxyStage.Coordinates).RequiredContracts.Contains("M11"));
    });
    Check("collision independent ordinary/scaled boundaries and acceptance clamp", () =>
    {
        var origin = new NativeFixedPoint[] { new(0, 0) };
        Equal(true, NativePlacementRules.HasCollision(new(999999, 0), origin, 1000000));
        Equal(false, NativePlacementRules.HasCollision(new(1000000, 0), origin, 1000000));
        Equal(false, NativePlacementRules.HasCollision(new(1000001, 0), origin, 1000000));
        Equal(true, NativePlacementRules.HasCollision(new(49999, 0), origin, 50000));
        Equal(false, NativePlacementRules.HasCollision(new(50000, 0), origin, 50000));
        Equal(true, NativePlacementRules.HasCollision(new(99999, 99999), origin, 1000000));
        Equal(false, NativePlacementRules.HasCollision(new(0, 0), origin, 0));
        Equal(new NativeFixedPoint(-50000000, 50000000), NativePlacementRules.ClampAccepted(new(-50000001, 50000001)));
        Equal(new NativeFixedPoint(-50000000, 50000000), NativePlacementRules.ClampAccepted(new(-50000000, 50000000)));
        Throws<ArgumentOutOfRangeException>(() => NativePlacementRules.HasCollision(new(0, 0), origin, -1));
        Throws<OverflowException>(() => NativePlacementRules.HasCollision(new(long.MaxValue, 0), new NativeFixedPoint[] { new(long.MinValue, 0) }, 1));
    });
    Check("placement retry 10000/10001 boundary, reset and explicit partial status", () =>
    {
        var retryCandidates = new PreRelaxationCandidate[20003];
        retryCandidates[10001] = new(1000000, 0, 1000000, 0, -1);
        retryCandidates[20002] = new(2000000, 0, 2000000, 0, -1);
        var reset = PreRelaxationPlacementJournal.Replay(retryCandidates, 3, 1000000);
        Equal(PlacementJournalStatus.TargetReached, reset.Status);
        Equal(3, reset.Accepted.Count);
        Equal(20003L, reset.EvaluatedCandidates);
        Equal(20000L, reset.RejectedCandidates);
        Equal(10000, reset.RemainingRetryBudget);
        var exhausted = PreRelaxationPlacementJournal.Replay(new PreRelaxationCandidate[10003], 2, 1000000);
        Equal(PlacementJournalStatus.RetryBudgetExhausted, exhausted.Status);
        Equal(10002L, exhausted.EvaluatedCandidates);
        Equal(10001L, exhausted.RejectedCandidates);
        Equal(0, exhausted.RemainingRetryBudget);
        Equal(1, exhausted.Accepted.Count);
        var inputEnded = PreRelaxationPlacementJournal.Replay(new PreRelaxationCandidate[1], 2, 1000000);
        Equal(PlacementJournalStatus.InputExhausted, inputEnded.Status);
        Equal(PlacementJournalStatus.TargetReached, PreRelaxationPlacementJournal.Replay(Array.Empty<PreRelaxationCandidate>(), 0, 0).Status);
        // Collision использует raw candidate против stored clamp: post-clamp duplicates допустимы.
        var clamped = PreRelaxationPlacementJournal.Replay(new PreRelaxationCandidate[]
            { new(50000000, 0, 50000000, 0, -1), new(51000000, 0, 51000000, 0, -1) }, 2, 1000000);
        Equal(2, clamped.Accepted.Count);
        Equal(clamped.Accepted[0].StoredPosition, clamped.Accepted[1].StoredPosition);
        Throws<NotSupportedException>(() => ((IList<AcceptedCoordinate>)clamped.Accepted).Clear());
    });
    Check("seeded placement journal failed-draw ledger and deterministic source plan", () =>
    {
        Write("map/galaxy/shapes.txt", "round = { core_radius_perc = 0 num_stars_core_perc = 0 stars_min_dist = 100 } ");
        var plan = GalaxyPreparation.Prepare(Import(), "alpha", "round") with { TargetStars = 2 };
        var start = NativeRandom.FromSpecifiedSeed(1);
        var result = PreRelaxationPlacementJournal.GenerateJournal(plan, 1);
        Equal(PlacementJournalStatus.RetryBudgetExhausted, result.Journal.Status);
        Equal(1, result.Journal.Accepted.Count);
        Equal(10002L, result.Journal.EvaluatedCandidates);
        Equal(start with { Counter = unchecked(start.Counter + 40008) }, result.NextState);
        var repeated = PreRelaxationPlacementJournal.GenerateJournal(plan, 1);
        Equal(result.NextState, repeated.NextState);
        Equal(result.Journal.Accepted[0], repeated.Journal.Accepted[0]);
        Throws<ArgumentOutOfRangeException>(() => PreRelaxationPlacementJournal.GenerateJournal(plan, -1));
        Reset();
    });
    Check("M11 independent centroid vectors, orientation and native zero-area", () =>
    {
        Equal(new NativePolygonCentroid(new(2, 1), 9), NativeGeometryPrimitives.Centroid(
            new NativeFloatPoint[] { new(0, 0), new(6, 0), new(0, 3) }));
        Equal(new NativePolygonCentroid(new(2, 1), -9), NativeGeometryPrimitives.Centroid(
            new NativeFloatPoint[] { new(0, 0), new(0, 3), new(6, 0) }));
        Equal(new NativePolygonCentroid(new(0, 0), 4), NativeGeometryPrimitives.Centroid(
            new NativeFloatPoint[] { new(-1, -1), new(1, -1), new(1, 1), new(-1, 1) }));
        var degenerate = NativeGeometryPrimitives.Centroid(new NativeFloatPoint[] { new(0, 0), new(2, 2) });
        Equal(0d, degenerate.SignedArea);
        Equal(true, float.IsNaN(degenerate.Position.X) && float.IsNaN(degenerate.Position.Y));
        Throws<ArgumentException>(() => NativeGeometryPrimitives.Centroid(new NativeFloatPoint[] { new(0, 0) }));
        Throws<ArgumentOutOfRangeException>(() => NativeGeometryPrimitives.Centroid(new NativeFloatPoint[] { new(float.NaN, 0), new(1, 1) }));
    });
    Check("M13 input truncation before binary32 and composite gates", () =>
    {
        Equal(new NativeFloatPoint(0, 0), NativeGeometryPrimitives.QuantizeTriangleInput(new(99999, -99999)));
        Equal(new NativeFloatPoint(1, -1), NativeGeometryPrimitives.QuantizeTriangleInput(new(199999, -199999)));
        Equal(new NativeFloatPoint(10, 10), NativeGeometryPrimitives.QuantizeTriangleInput(new(1000000, 1000000)));
        Equal(true, GalaxyPreparation.Unsupported(GalaxyStage.Coordinates).RequiredContracts.Contains("M11"));
        Equal(true, GalaxyPreparation.Unsupported(GalaxyStage.Topology).RequiredContracts.Contains("M13"));
    });
    Check("M11 distinguishing staged-float centroid vector", () =>
    {
        var result = NativeGeometryPrimitives.Centroid(new NativeFloatPoint[]
            { new(-784, -889), new(842, 157), new(-400, 621), new(-580, 322) });
        Equal(0xC313A93Au, (uint)BitConverter.SingleToInt32Bits(result.Position.X));
        Equal(0xC2046071u, (uint)BitConverter.SingleToInt32Bits(result.Position.Y));
        Equal(1105290d, result.SignedArea);
    });
    Check("M11 supplied tessellation orientation, stitching, pinning and simultaneous output", () =>
    {
        var sites = new NativeFloatPoint[] { new(100, 100), new(200, 200), new(300, 300), new(400, 400) };
        var vertices = new NativeFloatPoint[] { new(0, 0), new(6, 0), new(0, 3), new(10, 10), new(50001, 0) };
        var edges = new NativeIncidentEdge[] { new(0, 1, 0, 1), new(0, 3, 2, 0), new(2, 0, 2, 1) };
        var result = SuppliedTessellationPass.Apply(sites, vertices, edges);
        Equal(new NativeFloatPoint(2, 1), result[0]);
        Equal(new NativeFloatPoint(100, 100), sites[0]);
        for (var i = 1; i < sites.Length; i++) Equal(sites[i], result[i]);
        Throws<NotSupportedException>(() => ((IList<NativeFloatPoint>)result).Clear());
        // Native не проверяет closure: последний endpoint не передаётся в centroid, но bbox проверяется.
        var open = new NativeIncidentEdge[] { new(0, 1, 0, 1), new(0, 2, 1, 2), new(0, 3, 2, 3) };
        Equal(new NativeFloatPoint(2, 1), SuppliedTessellationPass.Apply(sites, vertices, open)[0]);
        open[2] = new(0, 3, 2, 4);
        Equal(sites[0], SuppliedTessellationPass.Apply(sites, vertices, open)[0]);
        edges[1] = new(0, 3, -1, 0);
        Equal(sites[0], SuppliedTessellationPass.Apply(sites, vertices, edges)[0]);
        edges[1] = new(0, 3, 3, 0);
        Equal(sites[0], SuppliedTessellationPass.Apply(sites, vertices, edges)[0]);
        var boundaryVertices = new NativeFloatPoint[] { new(50000, 0), new(50000, 3), new(49997, 0) };
        var boundaryEdges = new NativeIncidentEdge[] { new(0, 1, 0, 1), new(0, 2, 1, 2), new(0, 3, 2, 0) };
        Equal(true, SuppliedTessellationPass.Apply(sites, boundaryVertices, boundaryEdges)[0] != sites[0]);
        Throws<ArgumentException>(() => SuppliedTessellationPass.Apply(sites, vertices, new NativeIncidentEdge[] { new(0, 99, 0, 1) }));
    });
    Check("M13 independent annulus strict bounds, signed midpoint and whole-unit lengths", () =>
    {
        var lower = new NativeFixedPoint[] { new(100000, 100000), new(100000, -100000), new(-100000, 0) };
        Equal(0, SuppliedTopologyPostprocess.FilterAndSplit(lower, new int[] { 0, 0, 0 }, new NativeTriangleEdge[] { new(0, 1) }).SameGroup.Count);
        var interior = new NativeFixedPoint[] { new(200000, 100000), new(200000, -100000), new(100000, 0), new(300000, 0) };
        Equal(1, SuppliedTopologyPostprocess.FilterAndSplit(interior, new int[] { 0, 0, 0, 0 }, new NativeTriangleEdge[] { new(0, 1) }).SameGroup.Count);
        var upper = new NativeFixedPoint[] { new(300000, 0), new(300000, 0), new(100000, 0) };
        Equal(0, SuppliedTopologyPostprocess.FilterAndSplit(upper, new int[] { 0, 0, 0 }, new NativeTriangleEdge[] { new(0, 1) }).SameGroup.Count);
        Equal(new NativeFixedPoint(-50000, 0), SuppliedTopologyPostprocess.Midpoint(new(-100001, 0), new(0, 0)));
        foreach (var v in new (long Dx, long Dy, long Length)[] { (99999, 0, 0), (100000, 0, 100000), (1000000, 1000000, 1400000) })
        {
            var points = new NativeFixedPoint[] { new(0, 0), new(v.Dx, v.Dy), new(0, 100000) };
            Equal(v.Length, SuppliedTopologyPostprocess.FilterAndSplit(points, new int[] { 0, 0, 0 }, new NativeTriangleEdge[] { new(0, 1) }).SameGroup[0].StoredLength);
        }
        Equal(1414213L, SuppliedTopologyPostprocess.RawDistance(new(0, 0), new(1000000, 1000000)));
        Throws<OverflowException>(() => SuppliedTopologyPostprocess.Midpoint(new(long.MaxValue, 0), new(1, 0)));
        Throws<OverflowException>(() => SuppliedTopologyPostprocess.RawDistance(new(long.MinValue, 0), new(long.MaxValue, 0)));
    });
    Check("M13 supplied edge order/split and N2 raw adjacency bypass", () =>
    {
        var points = new NativeFixedPoint[] { new(0, 0), new(1000000, 0), new(1000000, 1000000), new(0, 1000000) };
        var edges = new NativeTriangleEdge[] { new(3, 0), new(0, 1), new(1, 3), new(1, 2), new(2, 3) };
        var split = SuppliedTopologyPostprocess.FilterAndSplit(points, new int[] { 0, 1, 1, 0 }, edges);
        Equal("3:0,1:2", string.Join(',', split.SameGroup.Select(e => $"{e.A}:{e.B}")));
        Equal("0:1,1:3,2:3", string.Join(',', split.DifferentGroup.Select(e => $"{e.A}:{e.B}")));
        Throws<NotSupportedException>(() => ((IList<NativeCandidateEdge>)split.SameGroup).Clear());
        var direct = SuppliedTopologyPostprocess.DirectSmallGraph(new NativeFixedPoint[] { new(0, 0), new(1000000, 1000000) }, new uint[] { 42, 99 });
        Equal(new NativeAdjacency(42, 99, 1414213, 0), direct[0]);
        Equal(new NativeAdjacency(99, 42, 1414213, 0), direct[1]);
        Equal(0, SuppliedTopologyPostprocess.DirectSmallGraph(Array.Empty<NativeFixedPoint>(), Array.Empty<uint>()).Count);
        Throws<NotSupportedException>(() => SuppliedTopologyPostprocess.DirectSmallGraph(points, new uint[] { 1, 2, 3, 4 }));
        Throws<ArgumentException>(() => SuppliedTopologyPostprocess.FilterAndSplit(points, new int[] { 0 }, edges));
    });
    Check("pre-relaxation fixed/trig independent vectors and wide domain", () =>
    {
        foreach (var v in new (long Angle, long Sin, long Cos)[]
                 { (0, 0, 100453), (157080, 100453, -1), (314159, 1, -100453), (-157080, -100453, 0) })
        {
            Equal(v.Sin, NativeFixedMath.Sin(v.Angle));
            Equal(v.Cos, NativeFixedMath.Cos(v.Angle));
            Equal(v.Sin, NativeFixedMath.Sin(v.Angle + NativeFixedMath.Period));
        }
        Equal(0L, NativeFixedMath.Multiply(-1, 50000));
        Equal(long.MaxValue, NativeFixedMath.Multiply(long.MaxValue, 100000));
        Equal(470371L, NativeFixedMath.DegreesToRadians(26950320));
        Throws<OverflowException>(() => NativeFixedMath.Multiply(long.MaxValue, long.MaxValue));
        Throws<DivideByZeroException>(() => NativeFixedMath.Divide(1, 0));
    });
    Check("pre-relaxation independent candidates A/B and raw branch domains", () =>
    {
        var elliptical = new PreRelaxationShape(17, 2500000, 7500000, 1000000, null, null);
        Equal(new PreRelaxationCandidate(-47914, -5543946, 5520100, 470371, -1),
            PreRelaxationCandidates.Evaluate(elliptical, 0, 0, new uint[] { 11, 12, 13, 14 }));
        var spiral = new PreRelaxationShape(17, 2000000, 8000000, 1000000,
            new(2, 110000, 8000000, 2500000, 18000000), null);
        Equal(241160L, PreRelaxationCandidates.EvaluateArmsRotation(8, 9));
        Equal(new PreRelaxationCandidate(3420268, -6546586, 7383840, 519400, 1),
            PreRelaxationCandidates.Evaluate(spiral, 0, 241160, new uint[] { 1, 2, 3, 4, 5, 6, 7 }));
        Equal(0L, PreRelaxationCandidates.Evaluate(elliptical, 17, 0, new uint[] { 0, 0, 0, 0 }).Radius);
        var ring = elliptical with { Ring = new GalaxyRing(5000000, 4000000) };
        Equal(6500000L, PreRelaxationCandidates.Evaluate(ring, 0, 0, new uint[] { 0, 0, 0, 0 }).Radius);
        Equal(6500000L, PreRelaxationCandidates.Evaluate(ring, 17, 0, new uint[] { 0, 0, 0, 0 }).Radius);
        Throws<ArgumentException>(() => PreRelaxationCandidates.Evaluate(elliptical, 0, 1, new uint[] { 0, 0, 0, 0 }));
        Throws<ArgumentException>(() => PreRelaxationCandidates.Evaluate(elliptical, 0, 0, new uint[] { 0, 0 }));
        Throws<ArgumentOutOfRangeException>(() => PreRelaxationCandidates.Evaluate(elliptical, -1, 0, new uint[] { 0, 0, 0, 0 }));
        Throws<ArgumentOutOfRangeException>(() => PreRelaxationCandidates.Evaluate(elliptical, 0, 0, new uint[] { uint.MaxValue, 0, 0, 0 }));
        Throws<ArgumentOutOfRangeException>(() => PreRelaxationCandidates.Evaluate(spiral with { Arms = spiral.Arms! with { Count = 0 } }, 0, 0,
            new uint[] { 0, 0, 0, 0, 0, 0, 0 }));
    });
    Check("pre-relaxation exact draw budget and explicit state", () =>
    {
        var state = NativeRandom.FromSpecifiedSeed(123456789);
        var elliptical = new PreRelaxationShape(17, 2500000, 7500000, 1000000, null, null);
        Equal(new PreRelaxationRotation(0, state), PreRelaxationCandidates.InitializeRotation(elliptical, state));
        var next = PreRelaxationCandidates.Next(elliptical, 0, 0, state);
        Equal(state with { Counter = state.Counter + 4 }, next.NextState);
        Equal(next, PreRelaxationCandidates.Next(elliptical, 0, 0, state));
        var spiral = elliptical with { Arms = new(2, 110000, 8000000, 2500000, 18000000) };
        var rotation = PreRelaxationCandidates.InitializeRotation(spiral, state);
        Equal(state with { Counter = state.Counter + 2 }, rotation.NextState);
        var armsNext = PreRelaxationCandidates.Next(spiral, 0, rotation.Angle, rotation.NextState);
        Equal(state with { Counter = state.Counter + 9 }, armsNext.NextState);
    });
    Check("source shape projection exact scales and missing/unknown descriptor gates", () =>
    {
        var shape = "round = { core_radius_perc = 0.25 num_stars_core_perc = 0.5 stars_min_dist = 10 }";
        Write("map/galaxy/shapes.txt", shape);
        var projected = GalaxyShapeProjection.Read(GalaxyPreparation.Prepare(Import(), "alpha", "round"));
        Equal(9, projected.OuterStars);
        Equal(537500L, projected.CoreRadius);
        Equal(1612500L, projected.OuterSpan);
        Equal(1000000L, projected.MinimumDistance);
        foreach (var invalid in new[] { "0.000001", "1.1", "@unknown" })
        {
            Write("map/galaxy/shapes.txt", shape.Replace("0.25", invalid));
            Throws<InvalidDataException>(() => GalaxyShapeProjection.Read(GalaxyPreparation.Prepare(Import(), "alpha", "round")));
        }
        Write("map/galaxy/shapes.txt", shape.Replace("stars_min_dist = 10", "stars_min_dist = { min = 5 max = 10 }"));
        Throws<FormatException>(() => GalaxyShapeProjection.Read(GalaxyPreparation.Prepare(Import(), "alpha", "round")));
        Reset();
    });
    Check("M16 independent vectors, cap, minimum and checked domain", () =>
    {
        Equal(4, NativeGenerationRules.CountClusters(new(ClusterCountMethod.OneEveryXEmpire, 3, -1), 10));
        Equal(2, NativeGenerationRules.CountClusters(new(ClusterCountMethod.OneEveryXEmpire, 3, 2), 10));
        Equal(1, NativeGenerationRules.CountClusters(new(ClusterCountMethod.OneEveryXEmpire, 3, -1), 0));
        Equal(5, NativeGenerationRules.CountClusters(new(ClusterCountMethod.Constant, 7, 5), 99));
        Equal(1, NativeGenerationRules.CountClusters(new(ClusterCountMethod.Constant, 0, -1), 99));
        Equal(1, NativeGenerationRules.CountClusters(new(ClusterCountMethod.Constant, -4, -2), 0));
        Throws<ArgumentOutOfRangeException>(() => NativeGenerationRules.CountClusters(new(ClusterCountMethod.OneEveryXEmpire, 0, -1), 5));
        Throws<ArgumentOutOfRangeException>(() => NativeGenerationRules.CountClusters(new(ClusterCountMethod.Constant, 1, -1), -1));
        Throws<ArgumentOutOfRangeException>(() => NativeGenerationRules.CountClusters(new((ClusterCountMethod)99, 1, -1), 1));
        Throws<OverflowException>(() => NativeGenerationRules.CountClusters(new(ClusterCountMethod.OneEveryXEmpire, 2, -1), int.MaxValue));
    });
    Check("M16 scenario projection and malformed fields", () =>
    {
        var cluster = "cluster_count = { method = one_every_x_empire value = 3 max = 2 }";
        void SetCluster(string text) => Write("map/setup_scenarios/a.txt", scenario.Replace("extra = { token = preserved }", text));
        SetCluster(cluster);
        Equal(2, GalaxyPreparation.PrepareClusterCount(GalaxyPreparation.Prepare(Import(), "alpha", "round"), 10));
        SetCluster(cluster.Replace("one_every_x_empire", "constant"));
        Equal(2, GalaxyPreparation.PrepareClusterCount(GalaxyPreparation.Prepare(Import(), "alpha", "round"), 0));
        SetCluster(cluster.Replace("one_every_x_empire", "unknown"));
        Throws<NotSupportedException>(() => GalaxyPreparation.PrepareClusterCount(GalaxyPreparation.Prepare(Import(), "alpha", "round"), 10));
        foreach (var malformed in new[] { "value = 1.5", "value = 2147483648", "value > 3", "value = @missing" })
        {
            SetCluster(cluster.Replace("value = 3", malformed));
            Throws<InvalidDataException>(() => GalaxyPreparation.PrepareClusterCount(GalaxyPreparation.Prepare(Import(), "alpha", "round"), 10));
        }
        SetCluster(cluster.Replace("max = 2", ""));
        Throws<FormatException>(() => GalaxyPreparation.PrepareClusterCount(GalaxyPreparation.Prepare(Import(), "alpha", "round"), 10));
        SetCluster(cluster.Replace("max = 2", "max = 2 max = 5"));
        Throws<FormatException>(() => GalaxyPreparation.PrepareClusterCount(GalaxyPreparation.Prepare(Import(), "alpha", "round"), 10));
        Reset();
    });
    Check("M20 independent vectors, both roundings, no clamp and raw roll", () =>
    {
        Equal(17L, NativeGenerationRules.EvaluateSpawnChance(17, 0, 100, 100000));
        Equal(2L, NativeGenerationRules.EvaluateSpawnChance(0, 25, 3, 150000));
        Equal(1L, NativeGenerationRules.EvaluateSpawnChance(0, 25, 2, 50000));
        Equal(-3L, NativeGenerationRules.EvaluateSpawnChance(5, 0, 10, -50000));
        Equal(0L, NativeGenerationRules.EvaluateSpawnChance(0, 10, 4, 100000));
        Equal(17L, NativeGenerationRules.EvaluateSpawnChance(17, -1, 0, 100000));
        Equal(150L, NativeGenerationRules.EvaluateSpawnChance(100, 0, 1, 150000));
        Equal(3_000_000_000L, NativeGenerationRules.EvaluateSpawnChance(1_000_000_000, 0, 1, 300000));
        Equal(false, NativeGenerationRules.PassesSpawnChance(0, 0));
        Equal(false, NativeGenerationRules.PassesSpawnChance(-3, 0));
        Equal(true, NativeGenerationRules.PassesSpawnChance(100, 99));
        Equal(true, NativeGenerationRules.PassesSpawnChance(17, 116));
        Equal(false, NativeGenerationRules.PassesSpawnChance(17, 117));
        Equal(true, NativeGenerationRules.PassesSpawnChance(long.MaxValue, 0x7FFFFFFF));
        Throws<ArgumentOutOfRangeException>(() => NativeGenerationRules.PassesSpawnChance(100, uint.MaxValue));
        Throws<ArgumentOutOfRangeException>(() => NativeGenerationRules.EvaluateSpawnChance(0, 1, -1, 100000));
        Throws<OverflowException>(() => NativeGenerationRules.EvaluateSpawnChance(0, int.MaxValue, 2, 100000));
        Throws<OverflowException>(() => NativeGenerationRules.EvaluateSpawnChance(2, 0, 1, long.MaxValue));
    });
    Check("M01 explicit seed vectors, high-bit counter and input boundary", () =>
    {
        // Независимые vectors reverse-партнёра, addendum §10.1, не вычисляются SUT.
        var vectors = new (int Seed, uint Counter, uint FirstDraw)[]
        {
            (0, 0xEDE419C6, 0x4BAE8956),
            (1, 0xE5F10AFA, 0x65AF1864),
            (123456789, 0x52268A8E, 0x3E3059B9),
            (int.MaxValue, 0x3A430740, 0x49605B98)
        };
        foreach (var v in vectors)
        {
            var state = NativeRandom.FromSpecifiedSeed(v.Seed);
            Equal(new NativeRandomState(0, v.Counter), state);
            Equal(state, NativeRandom.FromSpecifiedSeed(v.Seed));
            var first = NativeRandom.Next(state);
            Equal(v.FirstDraw, first.Value);
            Equal(new NativeRandomState(0, v.Counter + 1), first.NextState);
            // Чередование с чужой последовательностью не меняет immutable explicit state.
            NativeRandom.Next(NativeRandom.FromSpecifiedSeed(42));
            Equal(first, NativeRandom.Next(state));
        }
        Throws<ArgumentOutOfRangeException>(() => NativeRandom.FromSpecifiedSeed(-1));
        Throws<ArgumentOutOfRangeException>(() => NativeRandom.FromSpecifiedSeed(int.MinValue));
    });
    Check("M02 independent reverse vectors and uint32 counter wrap", () =>
    {
        var vectors = new (uint Seed, uint Counter, uint Value, uint Next)[]
        {
            (0, 0, 0x6FBE2FBA, 1),
            (0x12345678, 1, 0x12DE7D1D, 2),
            (0x89ABCDEF, 0x10203040, 0x40AD9A83, 0x10203041),
            (0xFFFFFFFF, 0xFFFFFFFF, 0x450383EF, 0)
        };
        foreach (var v in vectors)
        {
            var result = NativeRandom.Next(new(v.Seed, v.Counter));
            Equal(v.Value, result.Value);
            Equal(new NativeRandomState(v.Seed, v.Next), result.NextState);
        }
        var state = new NativeRandomState(0x12345678, 1);
        Equal(NativeRandom.Next(state), NativeRandom.Next(state));
    });
    Check("M04 raw fixed range grid, signs, draw bounds and overflow gate", () =>
    {
        Equal(1_315_600L, NativeRandom.EvaluateRawFixedRange(1_000_000, 400_000, 123456789));
        Equal(1_000_000L, NativeRandom.EvaluateRawFixedRange(1_000_000, 0, 123456789));
        Equal(684_400L, NativeRandom.EvaluateRawFixedRange(1_000_000, -400_001, 123456789));
        Equal(7L, NativeRandom.EvaluateRawFixedRange(7, -1, 999));
        Equal(999L, NativeRandom.EvaluateRawFixedRange(0, 1000, 999));
        Equal(0L, NativeRandom.EvaluateRawFixedRange(0, 1000, 1000));
        Throws<ArgumentOutOfRangeException>(() => NativeRandom.EvaluateRawFixedRange(0, 0, uint.MaxValue));
        Throws<OverflowException>(() => NativeRandom.EvaluateRawFixedRange(0, long.MaxValue, 2));
        Throws<OverflowException>(() => NativeRandom.EvaluateRawFixedRange(long.MaxValue, 1000, 1));
        for (uint draw = 0; draw < 1000; draw++)
        {
            var value = NativeRandom.EvaluateRawFixedRange(50, 2000, draw);
            if (value < 50 || value >= 2050) throw new Exception("Нарушен positive half-open range");
        }
    });

    if (args.Length == 1)
    {
        Check("genuine 4.5.2 import and stability", () =>
        {
            var root = args[0];
            GalaxyCatalogReadyChecks.RunVanilla(root, Check);
            GalaxyInitializerChecks.RunVanilla(GalaxyRuntimeCatalog.Import(root, new GalaxyEntitlements([], [])), Check);
            if (triangleBackend is not null) GalaxyWorldChecks.Run(root, triangleBackend, Check);
            // Это выбранный caller order, не заявление о native loader order.
            var paths = Directory.EnumerateFiles(Path.Combine(root, "map/setup_scenarios"), "*.txt")
                .Order(StringComparer.Ordinal).Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'));
            var catalog = GalaxyCatalog.Import(root, paths, ["map/galaxy/galaxy_shapes.txt"]);
            Equal(5, catalog.Scenarios.Count);
            var plan = GalaxyPreparation.Prepare(catalog, "tiny", "elliptical");
            Equal(200, plan.TargetStars);
            Equal(200m, plan.Radius);
            Equal(10m, decimal.Parse(plan.Shape.Fields.Required("stars_min_dist").AsScalar(), System.Globalization.CultureInfo.InvariantCulture));
            var journal = PreRelaxationPlacementJournal.GenerateJournal(plan, 1);
            Equal(PlacementJournalStatus.TargetReached, journal.Journal.Status);
            Equal(200, journal.Journal.Accepted.Count);
            var repeatedJournal = PreRelaxationPlacementJournal.GenerateJournal(plan, 1);
            Equal(journal.NextState, repeatedJournal.NextState);
            Equal(true, journal.Journal.Accepted.SequenceEqual(repeatedJournal.Journal.Accepted));
            var materialized = SystemMaterialization.Generate(plan, 1, FreshSystems(plan.TargetStars), SystemInputs(plan.TargetStars));
            Equal(SystemMaterializationStatus.TargetReached, materialized.Result.Status);
            Equal(200, materialized.Result.Systems.Count);
            Equal(journal.NextState, materialized.NextState);
            for (var i = 0; i < materialized.Result.Systems.Count; i++)
            {
                var system = materialized.Result.Systems[i];
                Equal((uint)i, system.Handle.Value);
                Equal(journal.Journal.Accepted[i].StoredPosition, system.Position);
                Equal<NativeFixedPoint?>(new(journal.Journal.Accepted[i].RawCandidate.X, journal.Journal.Accepted[i].RawCandidate.Y), system.RawPosition);
                Equal(true, ReferenceEquals(system, materialized.Result.Registry[system.Handle]));
            }
            var stateBeforeAdapters = journal.NextState;
            var expectedNextDraw = NativeRandom.Next(stateBeforeAdapters);
            var adapterInput = RelaxationAdapters.PrepareInput(journal.Journal.Accepted.Select(p => p.StoredPosition).ToArray(),
                GalaxyShapeProjection.Read(plan).CoreRadius, new(256, 1));
            Equal(456, adapterInput.Sites.Count);
            if (triangleBackend is not null)
            {
                Check("genuine 200 systems plus 256 dummy Triangle relaxation", () =>
                {
                    var result = NativeGalaxyRelaxation.Run(adapterInput, triangleBackend);
                    Equal(456, result.Sites.Count);
                    Equal(200, result.OrdinaryCoordinates.Count);
                    Equal(2, result.TessellationCalls);
                    Equal(true, result.Sites.SequenceEqual(result.FinalTessellation.Sites));
                    Equal(expectedNextDraw, NativeRandom.Next(stateBeforeAdapters));
                    Equal(true, adapterInput.Sites.All(p => float.IsFinite(p.X) && float.IsFinite(p.Y)));
                    Check("genuine relaxed200 actual candidate hyperlane graph", () =>
                    {
                        // Явный supplied test mapping, не заявление о реализованных native partitions.
                        var groups = Enumerable.Range(0, 200).Select(i => i / 20).ToArray();
                        var handles = materialized.Result.Systems.Select(s => s.Handle.Value).ToArray();
                        var groupRecords = Enumerable.Range(0, 10).Select(i => new CandidateGroupInput(i, 0)).ToArray();
                        var graph = CandidateHyperlaneGraph.Build(result.OrdinaryCoordinates.ToArray(), handles, groups, groupRecords, triangleBackend);
                        Equal(200, graph.Positions.Count);
                        Equal(1, graph.TriangleCalls);
                        Equal(true, graph.OrderedTriangleEdges.Count > 200);
                        Equal(true, graph.Candidates.SameGroup.Count > 0 && graph.Candidates.DifferentGroup.Count > 0);
                        Equal(2 * (graph.Candidates.SameGroup.Count + graph.Candidates.DifferentGroup.Count), graph.DiagnosticCandidateAdjacency.Count);
                        Equal(0, graph.DirectAdjacency.Count);
                        Equal(2 * graph.Candidates.DifferentGroup.Count, graph.InterGroupCounterDeltas.Values.Sum());
                        Equal(true, graph.ComponentBySite.All(c => c >= 0));
                        Equal(expectedNextDraw, NativeRandom.Next(stateBeforeAdapters));
                        Console.WriteLine($"Candidate graph: raw={graph.OrderedTriangleEdges.Count}, same={graph.Candidates.SameGroup.Count}, different={graph.Candidates.DifferentGroup.Count}, components={graph.ComponentCount}; supplied test groups");
                        Check("genuine200 pruning densities, connected handoff and repeat", () =>
                        {
                            foreach (var density in new[] { 50000L, 100000L, 300000L })
                            {
                                var options = new HyperlanePruningOptions(density, 50000, 300000, 5000000, 15000, 4);
                                var targets = graph.GroupCounters.Keys.Select(id => new HyperlaneGroupTarget(id, 3)).ToArray();
                                var pruned = HyperlanePruning.Run(graph, targets, options, 123, 456);
                                var connected = HyperlaneConnectivity.Complete(graph, pruned, options.MaximumDistance,
                                    HyperlaneConnectivityPolicy.RestoreCandidatesThenNearestV1);
                                var repeated = HyperlanePruning.Run(graph, targets, options, 123, 456);
                                Equal(true, pruned.Edges.SequenceEqual(repeated.Edges));
                                Equal(true, pruned.Draws.SequenceEqual(repeated.Draws));
                                Equal(519, pruned.Draws.Count(d => d.Pass == "B"));
                                Equal(2, pruned.GlobalDrawSites); Equal(true, connected.IsConnected);
                                Equal(true, connected.Edges.Count >= 199);
                                Equal(expectedNextDraw, NativeRandom.Next(stateBeforeAdapters));
                                Console.WriteLine($"Pruning h={density}: native={pruned.Edges.Count}, repairs={connected.Repairs.Count}, connected={connected.Edges.Count}");
                            }
                        });
                    });
                });
            }
            var adapterRestored = RelaxationAdapters.RestoreOrdinaryCoordinates(adapterInput, adapterInput.Sites.ToArray());
            Equal(200, adapterRestored.Count);
            for (var i = 0; i < adapterRestored.Count; i++)
            {
                var storedPoint = journal.Journal.Accepted[i].StoredPosition;
                Equal(new NativeFixedPoint(storedPoint.X / 1000 * 1000, storedPoint.Y / 1000 * 1000), adapterRestored[i]);
            }
            Equal(stateBeforeAdapters, journal.NextState);
            Equal(expectedNextDraw, NativeRandom.Next(journal.NextState));
            foreach (var definition in catalog.Scenarios)
            {
                var rule = GalaxyClusterProjection.Read(definition);
                var defaultEmpires = int.Parse(definition.Fields.Required("num_empire_default").AsScalar());
                var prepared = GalaxyPreparation.Prepare(catalog, definition.Id, definition.Fields.Named("supports_shape").First().AsScalar());
                var clusters = GalaxyPreparation.PrepareClusterCount(prepared, defaultEmpires);
                if (clusters < 1 || (rule.Cap >= 1 && clusters > rule.Cap)) throw new Exception("Cluster count вне source cap/minimum");
            foreach (var supported in definition.Fields.Named("supports_shape"))
                GalaxyPreparation.Prepare(catalog, definition.Id, supported.AsScalar());
            }
            catalog.VerifyStable();
            foreach (var definition in catalog.Shapes)
            {
                var rawShape = GalaxyShapeProjection.Read(plan with { Shape = definition });
                var state = NativeRandom.FromSpecifiedSeed(1);
                var rotation = PreRelaxationCandidates.InitializeRotation(rawShape, state);
                PreRelaxationCandidates.Next(rawShape, 0, rotation.Angle, rotation.NextState);
            }
            Console.WriteLine($"Genuine: {catalog.Scenarios.Count} scenarios, {catalog.Shapes.Count} shapes, {catalog.Files.Count} hashed files");
        });
    }
    Console.WriteLine($"PASS {checks} checks");
    return 0;
}
finally
{
    // Генерируется только наш уникальный child внутри bin check project.
    if (Path.GetDirectoryName(fixture) == AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))
        Directory.Delete(fixture, true);
}

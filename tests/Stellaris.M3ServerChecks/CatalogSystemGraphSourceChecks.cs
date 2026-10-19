using System.Reflection;
using System.Security.Cryptography;
using Stellaris.Core.Construction;
using Stellaris.Core.Content;
using Stellaris.Core.Content.Construction;
using Stellaris.Core.Content.Construction.Startup;
using Stellaris.Core.Content.PlanetEconomy;
using Stellaris.Core.Content.Galaxy;
using Stellaris.Core.Economy;
using Stellaris.Core.Galaxy;
using Stellaris.Core.Research;
using Stellaris.Core.Scripting;
using Stellaris.Session.M3;
using Stellaris.Session.M3.Construction;

// Проверяет реальные bindings сгенерированной галактики и EconomyWorld без fixture-состояния.
internal static class CatalogSystemGraphSourceChecks
{
    public static async Task RunAsync(string root, Action<bool, string> check)
    {
        var economy = await new PlanetEconomyImporter().ImportAsync(root);
        var content = await M3ConstructionContent.ImportAsync(root, economy);
        var full = await new ConstructionDefinitionImporter().ImportFullCatalogAsync(root);
        var startups = await M3StartupCatalog.ImportAsync(root);
        var recipes = new StartupFounderSpeciesImporter().Import(root);
        var profile = new M3BaselineEconomyProfile(economy, new StartupPopulationResourceImporter().Import(root),
            new StartupTraitWorkforceImporter().Import(root).BonusesByTemplate,
            new TechnologyImporter().Import(root).CategoryParents, recipes);

        var worker = Path.GetFullPath("artifacts/triangle-backend/build/stellaris-triangle-worker.exe");
        var workerBytes = await File.ReadAllBytesAsync(worker);
        var workerSha = Convert.ToHexString(SHA256.HashData(workerBytes));
        var galaxyRuntime = new M3GalaxyRuntime(GalaxyLobbyCatalog.Import(root, new([], [])),
            new NativeTriangleBackend(worker, workerSha,
                Path.GetFullPath("artifacts/catalog-system-graph/triangle-scratch"), TimeSpan.FromSeconds(30)));
        var world = profile.CreateWorld();
        var templates = new[] { "economic", "scientific", "military" };
        var players = new List<M3GalaxyPlayer>();
        var economyBindings = new Dictionary<int, (EntityId Country, EntityId Planet)>();
        for (var index = 0; index < templates.Length; index++)
        {
            var country = world.CreateCountry();
            var planet = world.CreatePlanet(country);
            players.Add(new(index + 1, planet.Index, templates[index]));
            economyBindings.Add(index + 1, (country, planet));
        }
        var generated = galaxyRuntime.Prepare(new("catalog-system-graph-452", "tiny", "elliptical", 1), players,
            (template, home) => startups.Exact(template, home.ClassId, home.Size));
        check(generated.Generated.Homes.Count == 3 && generated.Space.Colonies.Count == 3
            && generated.Generated.Readiness == "ReadyForBootstrap",
            "three real native startup allocations are generated and bound to SpaceWorld without fixtures");

        var runtimes = new Dictionary<string, ConstructionLiveRuntime>(StringComparer.Ordinal);
        var rawContexts = new Dictionary<string, ConstructionContext>(StringComparer.Ordinal);
        foreach (var home in generated.Generated.Homes)
        {
            var colony = generated.Space.Colonies.Single(item => item.BodyId == home.BodyId);
            var startup = generated.Startup[colony.Id];
            var templateId = players.Single(item => item.ParticipantId == colony.OwnerParticipantId).TemplateId;
            var binding = economyBindings[colony.OwnerParticipantId];
            profile.InitializeColony(world, binding.Country, binding.Planet, templateId, startup.Plan);
            var runtime = content.Create(startup, profile.CreateConstructionContributions(startup));
            var template = ConstructionOrdinaryTemplate.Get(templateId);
            var species = world.GetOwnedSpeciesSnapshot(binding.Country);
            var founder = world.GetFounderSpecies(binding.Country);
            var report = world.PreviewPlanetReport(binding.Planet);
            bool? usesConsumerGoods = report.Upkeep.GetValueOrDefault("consumer_goods") > 0 ? true : null;
            var context = content.Context(startup, template, runtime,
                M3ConstructionContent.FromBinding.ActingCountry(startup.Plan.Context.IsNomadic),
                template.StartingTechnologies, profile.InitialPopulation, usesConsumerGoods,
                ownerSpecies: species, ownerFounderSpeciesId: founder?.Id, ownerCountryEntityId: binding.Country,
                foreignOwnership: world.GetPlanetForeignOwnership(binding.Planet), planetEntityId: binding.Planet);
            check(binding.Planet.Index == colony.EconomyPlanetId && species is { Count: > 0 }
                && founder is not null && species.Any(item => item.Id == founder.Id),
                $"{templateId}: raw context uses actual generated home, initialized planet and founder group");
            runtimes.Add(colony.Id, runtime);
            rawContexts.Add(colony.Id, template.CreateInitialConstructionContext(context));
        }
        var worldHash = world.StateHash();
        var boundContexts = new Dictionary<string, ConstructionContext>(StringComparer.Ordinal);
        var boundGraphs = new Dictionary<string, ConstructionSystemGraph>(StringComparer.Ordinal);
        foreach (var colony in generated.Space.Colonies)
        {
            var graph = M3ConstructionSystemGraph.Bind(generated.Generated, generated.Space, colony.Id, rawContexts)
                ?? throw new InvalidOperationException("Actual generated system graph binding unexpectedly returned null.");
            var raw = rawContexts[colony.Id];
            var bound = raw.WithSystemGraph(graph, graph.RootBodyId);
            boundGraphs.Add(colony.Id, graph);
            boundContexts.Add(colony.Id, bound);
            var expectedBodies = generated.Generated.Bodies.Where(body => body.SystemId == graph.SystemId)
                .Select(body => body.Id).ToHashSet(StringComparer.Ordinal);
            var expectedStars = generated.Generated.Bodies.Where(body => body.SystemId == graph.SystemId
                && body.Kind == GalaxyBodyKind.Star).Select(body => body.Id).ToHashSet(StringComparer.Ordinal);
            check(graph.RootBodyId == colony.BodyId && graph.SystemId == generated.Space.Bodies.Single(body => body.Id == colony.BodyId).SystemId
                && graph.Planets.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expectedBodies)
                && graph.StarBodies is { } starBodies && starBodies.SetEquals(expectedStars)
                && graph.Planets[graph.RootBodyId] is not null && graph.PlanetsComplete == false
                && graph.Planets.Keys.All(id => !id.StartsWith("station/", StringComparison.Ordinal)),
                $"{colony.Id}: bound graph includes all generated system bodies, exact star-kind membership, and no stations");
            var knownColonyBodies = generated.Space.Colonies.Select(item => item.BodyId).ToHashSet(StringComparer.Ordinal);
            var unownedSystemBodies = graph.Planets.Where(pair => !knownColonyBodies.Contains(pair.Key)
                && generated.Generated.Bodies.Single(body => body.Id == pair.Key).Kind != GalaxyBodyKind.Star).ToArray();
            var materializedUnownedBodies = unownedSystemBodies.Where(pair => pair.Value is not null).ToArray();
            var unownedFactsMatch = materializedUnownedBodies.Length > 0 && materializedUnownedBodies.All(pair =>
                pair.Value is not null && string.Equals(pair.Value.PlanetClass,
                    generated.Generated.Bodies.Single(body => body.Id == pair.Key).ClassId, StringComparison.OrdinalIgnoreCase)
                && pair.Value.OwnerExists is null && pair.Value.BuildingCounts is null && pair.Value.SourceFacts is null);
            check(unownedFactsMatch,
                $"{colony.Id}: unowned generated bodies expose only actual class facts; owner/building/source facts remain unknown; observed="
                    + string.Join(";", unownedSystemBodies.Select(pair => $"{pair.Key}:{pair.Value?.PlanetClass ?? "<null>"}/expected:{generated.Generated.Bodies.Single(body => body.Id == pair.Key).ClassId} owner={pair.Value?.OwnerExists} buildings={pair.Value?.BuildingCounts is null} source={pair.Value?.SourceFacts is null}")));
        }
        var firstColony = generated.Space.Colonies[0];
        var firstRaw = rawContexts[firstColony.Id];
        var firstGraph = boundGraphs[firstColony.Id];
        var otherBody = generated.Generated.Bodies.First(body => body.Id != firstGraph.RootBodyId).Id;
        var wrongBodyRejected = false;
        try { _ = firstRaw.WithSystemGraph(firstGraph, otherBody); }
        catch (ArgumentException) { wrongBodyRejected = true; }
        var fork = world.Fork();
        check(wrongBodyRejected && world.StateHash() == worldHash && fork.StateHash() == worldHash,
            "wrong expected root body rejects without changing source world; Fork preserves hash");

        var sourceFiles = full.Files.ToDictionary(file => file.RelativePath, StringComparer.Ordinal);
        var conditionCtor = typeof(PlanetEconomyCondition).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null, [typeof(string), typeof(string), typeof(string), typeof(IEnumerable<PlanetEconomyCondition>),
                typeof(PlanetEconomyProvenance)], modifiers: null)
            ?? throw new MissingMethodException("PlanetEconomyCondition source constructor is unavailable.");
        PlanetEconomyCondition ToPlanetCondition(ConstructionField field)
        {
            var origin = sourceFiles[field.Origin.Source];
            return (PlanetEconomyCondition)conditionCtor.Invoke([field.Key, field.Operator, field.Scalar,
                field.Children.Select(ToPlanetCondition).ToArray(),
                new PlanetEconomyProvenance(field.Origin.Source, origin.Sha256, field.Origin.Line, field.Origin.Column)]);
        }
        var citadel = full.Definitions["building_citadel_uplink"];
        var allow = citadel.Fields.Single(field => field.Key == "allow");
        var existsSystem = allow.Children.Single(field => field.Key == "exists" && field.Scalar == "solar_system");
        var existsSystemCondition = ToPlanetCondition(existsSystem);
        var allowCondition = ToPlanetCondition(allow);
        var conditionSource = await new ConstructionDefinitionImporter().ImportCatalogConditionsAsync(root, full);
        check(existsSystemCondition.Provenance.RelativePath == existsSystem.Origin.Source
                && existsSystemCondition.Provenance.Line == existsSystem.Origin.Line
                && existsSystemCondition.Provenance.Column == existsSystem.Origin.Column
                && existsSystemCondition.Provenance.Sha256 == sourceFiles[existsSystem.Origin.Source].Sha256,
            "native building_citadel_uplink exists=solar_system node retains source provenance");
        check(ConstructionCatalogAssessor.EvaluatePlanetEconomyCondition(existsSystemCondition,
                    firstRaw, conditionSource).Truth == ConstructionTruth.Unknown
                && ConstructionCatalogAssessor.EvaluatePlanetEconomyCondition(existsSystemCondition,
                    boundContexts[firstColony.Id], conditionSource).Truth == ConstructionTruth.True,
            "exact native exists=solar_system resolves Unknown before graph binding and True after actual root binding");
        check(ConstructionCatalogAssessor.EvaluatePlanetEconomyCondition(allowCondition,
                    boundContexts[firstColony.Id], conditionSource, evaluateBody: true).Truth == ConstructionTruth.Unknown,
            "complete native citadel allow remains Unknown because actual ship/starbase readiness is not modeled");

        check(conditionSource.ScriptedConditions.ContainsKey("system_has_storm_repelling_tech"),
            "native source-closure system helper is imported: system_has_storm_repelling_tech");
        var sourceSession = new ContentSession(root);
        var stormSourcePath = "common/scripted_triggers/00_scripted_triggers.txt";
        var stormSource = sourceSession.LoadFile(stormSourcePath);
        var negatingStormSource = stormSource.Root.Named("system_has_negating_storm_building").Single();
        var starSourcePath = "common/inline_scripts/events/nomads_scientific_observation_internal_event.txt";
        var starSource = sourceSession.LoadFile(starSourcePath);
        var sourceHashes = sourceSession.Manifest("catalog-system-graph-source-v1", []).Files
            .ToDictionary(file => file.Path, file => file.Sha256, StringComparer.Ordinal);
        var economyConditionCtor = typeof(PlanetEconomyCondition).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null, [typeof(string), typeof(string), typeof(string), typeof(IEnumerable<PlanetEconomyCondition>),
                typeof(PlanetEconomyProvenance)], modifiers: null)
            ?? throw new MissingMethodException("PlanetEconomyCondition source constructor is unavailable.");
        var provenanceCtor = typeof(PlanetEconomyProvenance).GetConstructor([typeof(string), typeof(string), typeof(int), typeof(int)])
            ?? throw new MissingMethodException("PlanetEconomyProvenance source constructor is unavailable.");
        PlanetEconomyCondition ConvertNativeCondition(ScriptEntry entry)
        {
            var provenance = (PlanetEconomyProvenance)provenanceCtor.Invoke([entry.Source, sourceHashes[entry.Source], entry.Line, entry.Column]);
            var children = entry.Block?.Entries.Select(ConvertNativeCondition).ToArray() ?? [];
            return (PlanetEconomyCondition)economyConditionCtor.Invoke([entry.Key, entry.Operator, entry.Scalar, children, provenance]);
        }
        var nativeNegatingStorm = ConvertNativeCondition(negatingStormSource);
        check(nativeNegatingStorm.Provenance.RelativePath == stormSourcePath
                && nativeNegatingStorm.Provenance.Line == negatingStormSource.Line
                && nativeNegatingStorm.Children.Count == negatingStormSource.Block!.Entries.Count,
            "unused native helper is evaluated from its exact parsed AST and source provenance");
        var negatingStormRaw = ConstructionCatalogAssessor.EvaluatePlanetEconomyCondition(nativeNegatingStorm,
            firstRaw, conditionSource, evaluateBody: true);
        var negatingStorm = ConstructionCatalogAssessor.EvaluatePlanetEconomyCondition(nativeNegatingStorm,
            boundContexts[firstColony.Id], conditionSource, evaluateBody: true);
        var stormRepellingCall = full.Definitions["building_storm_attraction_center"].Fields
            .Single(field => field.Key == "allow").Children.Single(field => field.Key == "custom_tooltip")
            .Children.Single(field => field.Key == "solar_system?");
        check(stormRepellingCall.Origin.Source == "common/buildings/19_cosmic_storm_buildings.txt"
                && stormRepellingCall.Children.Single().Key == "system_has_storm_repelling_tech",
            "exact native storm attraction system wrapper contains the scripted helper call");
        var stormRepellingCondition = ToPlanetCondition(stormRepellingCall);
        var stormRepellingRaw = ConstructionCatalogAssessor.EvaluatePlanetEconomyCondition(stormRepellingCondition,
            firstRaw, conditionSource);
        var stormRepelling = ConstructionCatalogAssessor.EvaluatePlanetEconomyCondition(stormRepellingCondition,
            boundContexts[firstColony.Id], conditionSource);
        IEnumerable<ConstructionField> FlattenFields(IEnumerable<ConstructionField> fields)
            => fields.SelectMany(field => new[] { field }.Concat(FlattenFields(field.Children)));
        var moltenSystemWrapper = FlattenFields(full.Definitions["building_offworld_expedition_hub"].Fields)
            .Single(field => field.Key == "solar_system?" && FlattenFields(field.Children)
                .Any(child => child.Key == "is_planet_class" && child.Scalar == "pc_molten"));
        check(moltenSystemWrapper.Origin.Source == "common/buildings/04_manufacturing_buildings.txt"
                && FlattenFields(moltenSystemWrapper.Children).Any(field => field.Key == "any_system_planet"),
            "exact native offworld hub wrapper preserves system any_planet_class source tree");
        var moltenCondition = ToPlanetCondition(moltenSystemWrapper);
        var moltenSystemResult = ConstructionCatalogAssessor.EvaluatePlanetEconomyCondition(moltenCondition,
            boundContexts[firstColony.Id], conditionSource);
        var actualMoltenWitness = generated.Generated.Bodies.Any(body => body.SystemId == firstGraph.SystemId
            && body.Kind != GalaxyBodyKind.Star && body.ClassId == "pc_molten");
        check(actualMoltenWitness ? moltenSystemResult.Truth == ConstructionTruth.True
                : moltenSystemResult.Truth == ConstructionTruth.Unknown,
            "native system class predicate resolves only from the actual generated molten witness, otherwise partial membership stays Unknown");
        IEnumerable<ScriptEntry> FlattenScript(ScriptBlock block)
            => block.Entries.SelectMany(entry => new[] { entry }.Concat(entry.Block is null ? [] : FlattenScript(entry.Block)));
        var nativeStar = FlattenScript(starSource.Root).Single(entry => entry.Source == starSourcePath
            && entry.Line == 11 && entry.Key == "is_star" && entry.Operator == "=" && entry.Scalar == "yes");
        var starCondition = ConvertNativeCondition(nativeStar);
        check(starCondition.Provenance.RelativePath == starSourcePath && starCondition.Provenance.Line == nativeStar.Line
                && ConstructionCatalogAssessor.EvaluatePlanetEconomyCondition(starCondition,
                    boundContexts[firstColony.Id], conditionSource).Truth == ConstructionTruth.False,
            "exact native is_star=yes source node resolves false for the bound generated home planet");
        check(negatingStormRaw.Truth == ConstructionTruth.Unknown && negatingStorm.Truth == ConstructionTruth.Unknown
                && negatingStorm.Gates.Any(gate => gate.DependencyId is "system-planet-membership" or "system-planet-context")
                && negatingStormRaw.Gates.Any(gate => gate.DependencyId == "system-graph-context")
                && stormRepellingRaw.Truth == ConstructionTruth.Unknown && stormRepelling.Truth == ConstructionTruth.Unknown
                && stormRepelling.Gates.Any(gate => gate.DependencyId is "system-planet-membership" or "system-planet-context"),
            $"native system helper checks use actual graph gates; negating raw={negatingStormRaw.Truth}[{string.Join(',', negatingStormRaw.Gates.Select(gate => gate.DependencyId))}] bound={negatingStorm.Truth}[{string.Join(',', negatingStorm.Gates.Select(gate => gate.DependencyId))}], storm raw={stormRepellingRaw.Truth}[{string.Join(',', stormRepellingRaw.Gates.Select(gate => gate.DependencyId))}] bound={stormRepelling.Truth}[{string.Join(',', stormRepelling.Gates.Select(gate => gate.DependencyId))}]");
        sourceSession.VerifyStable();

        var beforePotential = new Dictionary<ConstructionTruth, int>();
        var beforeEligibility = new Dictionary<ConstructionTruth, int>();
        var afterPotential = new Dictionary<ConstructionTruth, int>();
        var afterEligibility = new Dictionary<ConstructionTruth, int>();
        var knownLosses = new List<string>();
        var unresolvedQuoteFailures = 0;
        foreach (var colony in generated.Space.Colonies)
        foreach (var pair in content.Content.Catalogs)
        {
            var beforeP = ConstructionCatalogAssessor.Visibility(pair.Value, pair.Key,
                rawContexts[colony.Id], conditionSource).Truth;
            var beforeE = ConstructionCatalogAssessor.Eligibility(pair.Value, pair.Key,
                rawContexts[colony.Id], conditionSource).Truth;
            var afterP = ConstructionCatalogAssessor.Visibility(pair.Value, pair.Key,
                boundContexts[colony.Id], conditionSource).Truth;
            var afterE = ConstructionCatalogAssessor.Eligibility(pair.Value, pair.Key,
                boundContexts[colony.Id], conditionSource).Truth;
            beforePotential[beforeP] = beforePotential.GetValueOrDefault(beforeP) + 1;
            beforeEligibility[beforeE] = beforeEligibility.GetValueOrDefault(beforeE) + 1;
            afterPotential[afterP] = afterPotential.GetValueOrDefault(afterP) + 1;
            afterEligibility[afterE] = afterEligibility.GetValueOrDefault(afterE) + 1;
            if (beforeP != ConstructionTruth.Unknown && beforeP != afterP) knownLosses.Add($"{colony.Id}/{pair.Key}/potential");
            if (beforeE != ConstructionTruth.Unknown && beforeE != afterE) knownLosses.Add($"{colony.Id}/{pair.Key}/eligibility");
            if (afterE == ConstructionTruth.Unknown)
            {
                var quote = content.Content.Quote(pair.Key, boundContexts[colony.Id], null, null);
                if (quote.Quote is not null || quote.Code != "eligibility-unresolved") unresolvedQuoteFailures++;
            }
        }
        check(content.Content.Catalogs.Count == 648 && generated.Space.Colonies.Count == 3
                && beforePotential.Values.Sum() == 648 * 3 && beforeEligibility.Values.Sum() == 648 * 3
                && afterPotential.Values.Sum() == 648 * 3 && afterEligibility.Values.Sum() == 648 * 3
                && knownLosses.Count == 0 && unresolvedQuoteFailures == 0,
            $"generated actual catalog before/after 648x3; before P[{Format(beforePotential)}] E[{Format(beforeEligibility)}], after P[{Format(afterPotential)}] E[{Format(afterEligibility)}], lost-known={knownLosses.Count}");
        var dtoUnknownCount = 0;
        foreach (var colony in generated.Space.Colonies)
        {
            var startup = generated.Startup[colony.Id];
            var templateId = players.Single(item => item.ParticipantId == colony.OwnerParticipantId).TemplateId;
            var template = ConstructionOrdinaryTemplate.Get(templateId);
            var building = content.PresetModifiers(template, ConstructionKind.Building);
            var district = content.PresetModifiers(template, ConstructionKind.District);
            var evaluation = new M3ConstructionEvaluation(boundContexts[colony.Id], M3ConstructionContent.EconomyFacts(startup),
                new("catalog-system-graph-source-v1", building.Cost, building.Speed, district.Speed),
                M3ConstructionContent.FromBinding.ActingCountry(startup.Plan.Context.IsNomadic));
            var view = new M3ConstructionAdapter(content, startup, template).Project(runtimes[colony.Id],
                colony.EconomyPlanetId, evaluation);
            var ids = view.Catalog.Select(item => item.DefinitionId).ToHashSet(StringComparer.Ordinal);
            var hidden = content.Content.Catalogs.Keys.Where(id => ConstructionCatalogAssessor.Visibility(
                content.Content.Catalogs[id], id, boundContexts[colony.Id], conditionSource).Truth == ConstructionTruth.False).ToArray();
            var unknown = content.Content.Catalogs.Keys.Where(id => ConstructionCatalogAssessor.Visibility(
                content.Content.Catalogs[id], id, boundContexts[colony.Id], conditionSource).Truth == ConstructionTruth.Unknown).ToHashSet(StringComparer.Ordinal);
            var unknownEligibility = content.Content.Catalogs.Keys.Where(id => ConstructionCatalogAssessor.Eligibility(
                content.Content.Catalogs[id], id, boundContexts[colony.Id], conditionSource).Truth == ConstructionTruth.Unknown).ToHashSet(StringComparer.Ordinal);
            var unresolved = view.Catalog.Where(item => unknown.Contains(item.DefinitionId)).ToArray();
            var unresolvedEligibility = view.Catalog.Where(item => unknownEligibility.Contains(item.DefinitionId)).ToArray();
            check(hidden.Length > 0 && hidden.All(id => !ids.Contains(id)), $"{colony.Id}: DTO omits potential=False rows");
            check(unresolved.Length == unknown.Count && unknown.All(ids.Contains)
                    && unresolved.All(item => item.Quote is null && item.Eligibility == "unavailable"),
                $"{colony.Id}: DTO retains all potential=Unknown rows disabled and unquoted");
            check(unresolvedEligibility.Length == unknownEligibility.Count
                    && unresolvedEligibility.All(item => item.Gates.Any(gate => gate.Code == "dependency-unresolved")),
                $"{colony.Id}: eligibility=Unknown rows retain dependency gates");
            dtoUnknownCount += unresolved.Length;
        }
        check(dtoUnknownCount > 0 && world.StateHash() == worldHash,
            "graph catalog quotes/DTO checks preserve unresolved rows and leave EconomyWorld hash unchanged");

        static string Format(IReadOnlyDictionary<ConstructionTruth, int> groups)
            => string.Join(",", groups.OrderBy(pair => pair.Key).Select(pair => pair.Key + ":" + pair.Value));
    }
}

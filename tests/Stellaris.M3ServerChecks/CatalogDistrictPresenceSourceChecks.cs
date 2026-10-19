using System.Reflection;
using System.Security.Cryptography;
using Stellaris.Core.Construction;
using Stellaris.Core.Content;
using Stellaris.Core.Content.Construction;
using Stellaris.Core.Content.Construction.Startup;
using Stellaris.Core.Content.PlanetEconomy;
using Stellaris.Core.Economy;
using Stellaris.Core.Research;
using Stellaris.Core.Scripting;
using Stellaris.Session.M3;
using Stellaris.Session.M3.Construction;

// Проверяет native district membership только по завершённым source startup districts.
internal static class CatalogDistrictPresenceSourceChecks
{
    private const string TriggerPath = "common/scripted_triggers/00_scripted_triggers.txt";
    private static readonly (string Id, string[] Districts)[] HelperCases =
    [
        ("has_any_planet_farming_district", ["district_farming", "district_rw_farming", "district_orchard_forests"]),
        ("has_any_capped_planet_farming_district", ["district_farming", "district_rw_farming", "district_orchard_forests"]),
        ("has_any_capped_planet_mining_district", ["district_mining", "district_melting", "district_hollow_mountains"]),
        ("has_any_capped_planet_generator_district", ["district_generator", "district_geothermal", "district_rw_generator"])
    ];

    private static readonly (string Id, string HelperId, string[] Districts)[] CriticalBuildings =
    [
        ("building_farming_districts_1", "has_any_capped_planet_farming_district", ["district_farming", "district_rw_farming", "district_orchard_forests"]),
        ("building_mining_districts_1", "has_any_capped_planet_mining_district", ["district_mining", "district_melting", "district_hollow_mountains"]),
        ("building_generator_districts_1", "has_any_capped_planet_generator_district", ["district_generator", "district_geothermal", "district_rw_generator"])
    ];

    private static readonly string[] ExpectedEligibilityUnknownIds =
    [
        "building_astral_siphon_1", "building_astral_siphon_2", "building_astral_siphon_3",
        "building_gaiaseeders_2", "building_gaiaseeders_3", "building_mine_generic", "building_mining_districts_1",
        "building_solarpunk_gaiaseeder", "building_solarpunk_organic_paradise", "building_solarpunk_ranger_lodge", "building_solarpunk_sapling",
        "holding_aid_agency", "holding_communal_housing_outreach", "holding_distributed_processing", "holding_dread_outpost",
        "holding_emporium", "holding_energy_requisitorium", "holding_experimental_crater", "holding_franchise_headquarters",
        "holding_idyllic_bloom", "holding_knight_commandery", "holding_material_requisitorium", "holding_noble_vacation_homes",
        "holding_offspring_nest", "holding_offworld_foundry", "holding_orbital_assembly_complex", "holding_organic_sanctuary",
        "holding_overlord_vigil_command", "holding_parasitic_algorithms", "holding_park_ranger_lodge", "holding_produce_requisitorium",
        "holding_propaganda_office", "holding_recruitment_office", "holding_reemployment_center", "holding_sacrificial_shrine",
        "holding_satellite_campus", "holding_splinter_hive", "holding_transcendental_retreat", "holding_tree_of_life_sapling",
        "holding_wilderness_glade", "hunting_grounds", "zone_default"
    ];

    private static readonly CatalogConditionTransition[] ExpectedPotentialChanges =
    [
        new("building_farming_districts_1", ConstructionTruth.Unknown, ConstructionTruth.True),
        new("building_farming_districts_2", ConstructionTruth.Unknown, ConstructionTruth.True),
        new("building_farming_districts_3", ConstructionTruth.Unknown, ConstructionTruth.True),
        new("building_farming_districts_4", ConstructionTruth.Unknown, ConstructionTruth.True),
        new("building_generator_districts_1", ConstructionTruth.Unknown, ConstructionTruth.True),
        new("building_generator_districts_2", ConstructionTruth.Unknown, ConstructionTruth.True),
        new("building_generator_districts_3", ConstructionTruth.Unknown, ConstructionTruth.True),
        new("building_generator_districts_4", ConstructionTruth.Unknown, ConstructionTruth.True)
    ];

    public static async Task RunAsync(string root, Action<bool, string> check)
    {
        CatalogConditionTransitions.VerifyAccountingChecks(check);
        var economy = await new PlanetEconomyImporter().ImportAsync(root);
        var content = await M3ConstructionContent.ImportAsync(root, economy);
        var full = await new ConstructionDefinitionImporter().ImportFullCatalogAsync(root);
        var startups = await M3StartupCatalog.ImportAsync(root);
        var founderRecipes = new StartupFounderSpeciesImporter().Import(root);
        var populationResources = new StartupPopulationResourceImporter().Import(root);
        var technologies = new TechnologyImporter().Import(root);
        var profile = new M3BaselineEconomyProfile(economy, populationResources,
            new StartupTraitWorkforceImporter().Import(root).BonusesByTemplate, technologies.CategoryParents, founderRecipes);
        var world = profile.CreateWorld();
        var source = new ContentSession(root);
        var nativeTriggers = source.LoadFile(TriggerPath);
        var sourceHash = source.Manifest("catalog-district-presence-source-v1", []).Files
            .Single(file => file.Path == TriggerPath).Sha256;
        var conditionCtor = typeof(PlanetEconomyCondition).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null, [typeof(string), typeof(string), typeof(string), typeof(IEnumerable<PlanetEconomyCondition>),
                typeof(PlanetEconomyProvenance)], modifiers: null)
            ?? throw new MissingMethodException("PlanetEconomyCondition source constructor is unavailable.");
        var provenanceCtor = typeof(PlanetEconomyProvenance).GetConstructor(
            [typeof(string), typeof(string), typeof(int), typeof(int)])
            ?? throw new MissingMethodException("PlanetEconomyProvenance source constructor is unavailable.");
        PlanetEconomyCondition ToPlanetCondition(ScriptEntry entry)
        {
            var provenance = (PlanetEconomyProvenance)provenanceCtor.Invoke([entry.Source, sourceHash, entry.Line, entry.Column]);
            var children = entry.Block?.Entries.Select(ToPlanetCondition).ToArray() ?? [];
            return (PlanetEconomyCondition)conditionCtor.Invoke([entry.Key, entry.Operator, entry.Scalar, children, provenance]);
        }

        var helperSources = new Dictionary<string, ScriptEntry>(StringComparer.Ordinal);
        foreach (var (helperId, _) in HelperCases)
        {
            var native = nativeTriggers.Root.Named(helperId).Single();
            helperSources.Add(helperId, native);
            check(content.Content.Conditions.ScriptedConditions.TryGetValue(helperId, out var imported)
                    && imported.Kind == "scriptedCondition" && imported.Origin.Source == TriggerPath
                    && imported.Origin.Line == native.Line && native.Block is not null
                    && native.Block.Entries.Count == imported.Fields.Count
                    && native.Block.Entries.Select((entry, index) => Matches(entry, imported.Fields[index])).All(match => match),
                $"{helperId}: imported scripted helper preserves the exact native AST and provenance");
        }

        var runs = new List<(M3StartupColony Startup, ConstructionLiveRuntime Runtime, ConstructionOrdinaryTemplate Template,
            ConstructionContext Before, ConstructionContext After, EntityId Country, EntityId Planet,
            IReadOnlyDictionary<string, decimal> ExpectedDistricts)>();
        foreach (var preset in new[] { "economic", "scientific", "military" })
        {
            var home = startups.Select(preset, 123);
            var startup = M3StartupColony.Create("district-presence-" + preset, 1, home.Homeworld, home.Plan);
            var runtime = content.Create(startup, profile.CreateConstructionContributions(startup));
            var country = world.CreateCountry();
            var planet = world.CreatePlanet(country);
            profile.InitializeColony(world, country, planet, preset, home.Plan);
            var foreignOwnership = world.GetPlanetForeignOwnership(planet)
                ?? throw new InvalidOperationException($"{preset}: missing initialized foreign ownership snapshot.");
            var ownerSpecies = world.GetOwnedSpeciesSnapshot(country);
            var founder = world.GetFounderSpecies(country)
                ?? throw new InvalidOperationException($"{preset}: missing initialized founder species.");
            var report = world.PreviewPlanetReport(planet);
            bool? usesConsumerGoods = report.Upkeep.GetValueOrDefault("consumer_goods") > 0 ? true : null;
            var template = ConstructionOrdinaryTemplate.Get(preset);
            var runtimeContext = content.Context(startup, template, runtime,
                M3ConstructionContent.FromBinding.ActingCountry(startup.Plan.Context.IsNomadic), template.StartingTechnologies,
                profile.InitialPopulation, usesConsumerGoods, ownerSpecies: ownerSpecies,
                ownerFounderSpeciesId: founder.Id, ownerCountryEntityId: country,
                foreignOwnership: foreignOwnership, planetEntityId: planet);
            var withoutDistricts = new ConstructionContext(ownerExists: runtimeContext.OwnerExists,
                ownerFacts: runtimeContext.OwnerFacts, fromNomadic: runtimeContext.FromNomadic, planetClass: runtimeContext.PlanetClass,
                modifiers: runtimeContext.Modifiers, carrierFlags: runtimeContext.CarrierFlags, technologies: runtimeContext.Technologies,
                buildingCounts: runtimeContext.BuildingCounts, sourceFacts: runtimeContext.SourceFacts, capitalTier: runtimeContext.CapitalTier,
                foreignOwnership: runtimeContext.ForeignOwnership, expectedPlanet: foreignOwnership.Planet,
                expectedPlanetOwner: foreignOwnership.PlanetOwner, systemGraph: runtimeContext.SystemGraph,
                expectedSystemBodyId: runtimeContext.SystemGraph?.RootBodyId, districtCounts: null);
            var before = template.CreateInitialConstructionContext(withoutDistricts);
            var after = template.CreateInitialConstructionContext(runtimeContext);

            var expectedDistricts = startup.Plan.Districts.GroupBy(item => item.DefinitionId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => (decimal)group.Sum(item => item.Level), StringComparer.Ordinal);
            var runtimeDistricts = runtime.Installed.Values.Where(item => item.Kind == ConstructionKind.District)
                .GroupBy(item => item.DefinitionId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => (decimal)group.Count(), StringComparer.Ordinal);
            check(SameCounts(expectedDistricts, runtimeDistricts) && SameCounts(expectedDistricts, after.DistrictCounts)
                    && before.DistrictCounts is null && SameCounts(expectedDistricts, runtimeContext.DistrictCounts),
                $"{preset}: actual source plan levels, installed runtime and bound district snapshot agree; unbound snapshot remains null");
            check(ownerSpecies is { Count: > 0 } && ownerSpecies.Any(item => item.Id == founder.Id)
                    && runtimeContext.Technologies?.SetEquals(template.StartingTechnologies) == true
                    && runtimeContext.SourceFacts?.SapientPopulation == profile.InitialPopulation
                    && runtimeContext.SourceFacts.OwnerFounderSpeciesId == founder.Id
                    && runtimeContext.OwnerFacts.GetValueOrDefault(ConstructionOwnerPredicate.UsesConsumerGoods) == usesConsumerGoods
                    && foreignOwnership.Planet == planet && foreignOwnership.PlanetOwner == country,
                $"{preset}: actual initialized owner/founder/population/technology/CG/foreign bindings are retained");
            check(after.DistrictCounts is not null
                    && template.CreateInitialConstructionContext(runtimeContext).DistrictCounts is { } copied
                    && SameCounts(after.DistrictCounts, copied),
                $"{preset}: ordinary template context copy preserves the immutable district snapshot");
            runs.Add((startup, runtime, template, before, after, country, planet, expectedDistricts));
        }

        foreach (var run in runs)
        foreach (var (helperId, districtIds) in HelperCases)
        {
            var helper = content.Content.Conditions.ScriptedConditions[helperId];
            var native = helperSources[helperId];
            foreach (var districtNode in Flatten(native.Block!).Where(entry => entry.Key == "has_district"
                         && entry.Operator == "=" && entry.Scalar is not null))
            {
                var actualPresence = run.ExpectedDistricts.GetValueOrDefault(districtNode.Scalar!) > 0;
                var before = ConstructionCatalogAssessor.EvaluatePlanetEconomyCondition(ToPlanetCondition(districtNode),
                    run.Before, content.Content.Conditions);
                var after = ConstructionCatalogAssessor.EvaluatePlanetEconomyCondition(ToPlanetCondition(districtNode),
                    run.After, content.Content.Conditions);
                check(districtIds.Contains(districtNode.Scalar!, StringComparer.Ordinal)
                        && districtNode.Source == TriggerPath && before.Truth == ConstructionTruth.Unknown
                        && before.Gates.Any(gate => gate.DependencyId == "district-count-context")
                        && after.Truth == (actualPresence ? ConstructionTruth.True : ConstructionTruth.False),
                    $"{run.Template.Id}/{helperId}/{districtNode.Scalar}: exact native literal has_district is Unknown without snapshot and matches actual source count");
            }
            var helperBefore = ConstructionCatalogAssessor.EvaluatePlanetHelper(content.Content.Conditions, helperId, run.Before);
            var helperAfter = ConstructionCatalogAssessor.EvaluatePlanetHelper(content.Content.Conditions, helperId, run.After);
            var expectedHelperAfter = helperId switch
            {
                "has_any_planet_farming_district" => ConstructionTruth.True,
                "has_any_capped_planet_farming_district" => ConstructionTruth.True,
                "has_any_capped_planet_mining_district" => ConstructionTruth.Unknown,
                "has_any_capped_planet_generator_district" => ConstructionTruth.True,
                _ => throw new InvalidOperationException($"Unexpected district helper {helperId}.")
            };
            check(helperBefore.Truth == ConstructionTruth.Unknown && helperAfter.Truth == expectedHelperAfter,
                $"{run.Template.Id}/{helperId}: native helper {helperBefore.Truth}->{helperAfter.Truth}, expected Unknown->{expectedHelperAfter}");
        }

        var worldHash = world.StateHash();
        var summary = new List<string>();
        string[]? sharedPotentialChangedIds = null;
        var knownLossCount = 0;
        var actualUnknownCount = 0;
        foreach (var run in runs)
        {
            var potentialTransitions = new List<CatalogConditionTransition>();
            var eligibilityTransitions = new List<CatalogConditionTransition>();
            var buildingModifiers = content.PresetModifiers(run.Template, ConstructionKind.Building);
            var districtModifiers = content.PresetModifiers(run.Template, ConstructionKind.District);
            var dto = new M3ConstructionEvaluation(run.After, M3ConstructionContent.EconomyFacts(run.Startup),
                new("catalog-district-presence-source-v1", checked(buildingModifiers.Cost),
                    checked(buildingModifiers.Speed), checked(districtModifiers.Speed)),
                M3ConstructionContent.FromBinding.ActingCountry(run.Startup.Plan.Context.IsNomadic));
            var view = new M3ConstructionAdapter(content, run.Startup, run.Template)
                .Project(run.Runtime, run.Planet.Index, dto);
            var visibleIds = view.Catalog.Select(item => item.DefinitionId).ToHashSet(StringComparer.Ordinal);
            var falsePotentialIds = new List<string>();
            var unknownPotentialIds = new HashSet<string>(StringComparer.Ordinal);
            var unknownEligibilityIds = new HashSet<string>(StringComparer.Ordinal);
            var unknownQuotesRefused = 0;
            foreach (var pair in content.Content.Catalogs)
            {
                var pBefore = ConstructionCatalogAssessor.Visibility(pair.Value, pair.Key, run.Before, content.Content.Conditions);
                var eBefore = ConstructionCatalogAssessor.Eligibility(pair.Value, pair.Key, run.Before, content.Content.Conditions);
                var pAfter = ConstructionCatalogAssessor.Visibility(pair.Value, pair.Key, run.After, content.Content.Conditions);
                var eAfter = ConstructionCatalogAssessor.Eligibility(pair.Value, pair.Key, run.After, content.Content.Conditions);
                potentialTransitions.Add(new(pair.Key, pBefore.Truth, pAfter.Truth));
                eligibilityTransitions.Add(new(pair.Key, eBefore.Truth, eAfter.Truth));
                if (pAfter.Truth == ConstructionTruth.False) falsePotentialIds.Add(pair.Key);
                if (pAfter.Truth == ConstructionTruth.Unknown) unknownPotentialIds.Add(pair.Key);
                if (eAfter.Truth == ConstructionTruth.Unknown)
                {
                    unknownEligibilityIds.Add(pair.Key);
                    var refusal = content.Content.Quote(pair.Key, run.After, null, null);
                    if (refusal.Quote is null && refusal.Code == "eligibility-unresolved") unknownQuotesRefused++;
                }
            }
            var dtoUnknown = view.Catalog.Where(item => unknownPotentialIds.Contains(item.DefinitionId)).ToArray();
            var dtoUnknownEligibility = view.Catalog.Where(item => unknownEligibilityIds.Contains(item.DefinitionId)).ToArray();
            var expectedIds = content.Content.Catalogs.Keys.Order(StringComparer.Ordinal).ToArray();
            var isMilitary = run.Template.Id == "military";
            var potentialBeforeExpected = TruthCounts(isMilitary ? 395 : 396, 63, isMilitary ? 190 : 189);
            var potentialAfterExpected = TruthCounts(isMilitary ? 395 : 396, 55, isMilitary ? 198 : 197);
            var eligibilityBeforeExpected = TruthCounts(504, 44, 100);
            var eligibilityAfterExpected = TruthCounts(504, 42, 102);
            var expectedEligibilityChanges = new[]
            {
                new CatalogConditionTransition("building_farming_districts_1", ConstructionTruth.Unknown, ConstructionTruth.True),
                new CatalogConditionTransition("building_generator_districts_1", ConstructionTruth.Unknown, ConstructionTruth.True)
            };
            var potentialChanged = potentialTransitions.Where(row => row.Before != row.After)
                .OrderBy(row => row.Id, StringComparer.Ordinal).ToArray();
            var potentialChangedIds = potentialChanged.Select(row => row.Id).ToArray();
            var potentialShapeValid = potentialChanged.SequenceEqual(ExpectedPotentialChanges.OrderBy(row => row.Id, StringComparer.Ordinal));
            check(potentialShapeValid && (sharedPotentialChangedIds is null
                    || sharedPotentialChangedIds.SequenceEqual(potentialChangedIds, StringComparer.Ordinal)),
                $"{run.Template.Id}: potential changed IDs=[{string.Join(",", potentialChanged.Select(row => $"{row.Id}:{row.Before}->{row.After}"))}], match exact native-backed expectation and other presets");
            sharedPotentialChangedIds ??= potentialChangedIds;
            var potentialAccounted = CatalogConditionTransitions.TryReconcile(expectedIds, potentialTransitions, ExpectedPotentialChanges,
                potentialBeforeExpected, potentialAfterExpected, out var potentialResult, out var potentialError);
            var eligibilityAccounted = CatalogConditionTransitions.TryReconcile(expectedIds, eligibilityTransitions,
                expectedEligibilityChanges, eligibilityBeforeExpected, eligibilityAfterExpected,
                out var eligibilityResult, out var eligibilityError);
            check(potentialAccounted && eligibilityAccounted,
                $"{run.Template.Id}: complete transition maps reconcile without duplicate/missing IDs, unexpected changes, known losses, or totals mismatch; P={potentialError}; E={eligibilityError}");
            knownLossCount += potentialResult.KnownLosses.Count + eligibilityResult.KnownLosses.Count;
            check(unknownQuotesRefused == unknownEligibilityIds.Count,
                $"{run.Template.Id}: direct quote blocks every actual eligibility Unknown");
            check(unknownEligibilityIds.SetEquals(ExpectedEligibilityUnknownIds),
                $"{run.Template.Id}: actual eligibility Unknown IDs match the exact 42-ID baseline");
            check(falsePotentialIds.All(id => !visibleIds.Contains(id))
                    && dtoUnknown.Length == unknownPotentialIds.Count && unknownPotentialIds.All(visibleIds.Contains)
                    && dtoUnknown.All(item => item.Quote is null && item.Eligibility == "unavailable")
                    && dtoUnknownEligibility.Length == unknownEligibilityIds.Count
                    && unknownEligibilityIds.All(visibleIds.Contains)
                    && dtoUnknownEligibility.All(item => item.Quote is null && item.Eligibility == "unavailable"
                        && item.Gates.Any(gate => gate.Code == "dependency-unresolved")),
                $"{run.Template.Id}: DTO omits potential False and retains all potential/eligibility Unknown rows disabled and unquoted");
            actualUnknownCount += dtoUnknown.Length;
            summary.Add($"{run.Template.Id}: P[{Format(potentialResult.BeforeCounts)}]->[{Format(potentialResult.AfterCounts)}] changed=[{string.Join(",", potentialResult.Changed.Select(row => $"{row.Id}:{row.Before}->{row.After}"))}] E[{Format(eligibilityResult.BeforeCounts)}]->[{Format(eligibilityResult.AfterCounts)}] changed=[{string.Join(",", eligibilityResult.Changed.Select(row => $"{row.Id}:{row.Before}->{row.After}"))}]");

            foreach (var (id, helperId, districtIds) in CriticalBuildings)
            {
                var expectedRelevantDistricts = id switch
                {
                    "building_farming_districts_1" => new Dictionary<string, decimal>(StringComparer.Ordinal) { ["district_farming"] = 4m },
                    "building_mining_districts_1" => new Dictionary<string, decimal>(StringComparer.Ordinal) { ["district_mining"] = 2m },
                    "building_generator_districts_1" => new Dictionary<string, decimal>(StringComparer.Ordinal) { ["district_generator"] = 3m },
                    _ => throw new InvalidOperationException($"Unexpected critical building {id}.")
                };
                var relevantCounts = districtIds.ToDictionary(district => district,
                    district => run.ExpectedDistricts.GetValueOrDefault(district), StringComparer.Ordinal);
                var expectedAfter = id switch
                {
                    "building_farming_districts_1" => ConstructionTruth.True,
                    "building_mining_districts_1" => ConstructionTruth.Unknown,
                    "building_generator_districts_1" => ConstructionTruth.True,
                    _ => throw new InvalidOperationException($"Unexpected critical building {id}.")
                };
                var relevantDistrictCounts = districtIds
                    .Select(district => (Id: district, Count: run.ExpectedDistricts.GetValueOrDefault(district)))
                    .Where(item => item.Count > 0)
                    .Select(item => $"{item.Id}:{item.Count}")
                    .ToArray();
                var before = ConstructionCatalogAssessor.Eligibility(content.Content.Catalogs[id], id, run.Before,
                    content.Content.Conditions).Truth;
                var after = ConstructionCatalogAssessor.Eligibility(content.Content.Catalogs[id], id, run.After,
                    content.Content.Conditions).Truth;
                check(expectedRelevantDistricts.All(pair => relevantCounts.GetValueOrDefault(pair.Key) == pair.Value)
                        && before == ConstructionTruth.Unknown && after == expectedAfter,
                    $"{run.Template.Id}/{id}: helper={helperId}; relevantDistricts=[{string.Join(",", relevantDistrictCounts)}]; eligibility={before}->{after}");
            }
        }
        check(content.Content.Catalogs.Count == 648 && economy.JobClosure.Count == 59
                && runs.Count == 3 && knownLossCount == 0 && actualUnknownCount > 0,
            $"native catalog 648/jobs 59; actual contexts cover 3 presets; known truth losses={knownLossCount}; {string.Join("; ", summary)}");
        check(world.StateHash() == worldHash, "district source assessment and DTO projection leave EconomyWorld unchanged");
        source.VerifyStable();
    }

    private static bool SameCounts(IReadOnlyDictionary<string, decimal>? left, IReadOnlyDictionary<string, decimal>? right)
        => left is not null && right is not null && left.Count == right.Count
            && left.All(pair => right.TryGetValue(pair.Key, out var value) && value == pair.Value);

    private static IReadOnlyDictionary<ConstructionTruth, int> TruthCounts(int falseCount, int unknownCount, int trueCount)
        => new Dictionary<ConstructionTruth, int>
        {
            [ConstructionTruth.False] = falseCount,
            [ConstructionTruth.Unknown] = unknownCount,
            [ConstructionTruth.True] = trueCount
        };

    private static string Format(IReadOnlyDictionary<ConstructionTruth, int> counts)
        => string.Join(",", counts.OrderBy(pair => pair.Key).Select(pair => pair.Key + ":" + pair.Value));

    private static IEnumerable<ScriptEntry> Flatten(ScriptBlock block)
        => block.Entries.SelectMany(entry => new[] { entry }.Concat(entry.Block is null ? [] : Flatten(entry.Block)));

    private static bool Matches(ScriptEntry native, ConstructionField imported)
        => native.Key == imported.Key && native.Operator == imported.Operator && native.Scalar == imported.Scalar
            && native.Source == imported.Origin.Source && native.Line == imported.Origin.Line && native.Column == imported.Origin.Column
            && (native.Block?.Entries.Count ?? 0) == imported.Children.Count
            && (native.Block is null || native.Block.Entries.Select((entry, index) => Matches(entry, imported.Children[index])).All(match => match));
}

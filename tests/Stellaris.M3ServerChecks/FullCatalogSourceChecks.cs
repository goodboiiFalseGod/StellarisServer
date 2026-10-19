using System.Security.Cryptography;
using System.Reflection;
using Stellaris.Core.Content;
using Stellaris.Core.Construction;
using Stellaris.Core.Content.Construction;
using Stellaris.Core.Content.PlanetEconomy;
using Stellaris.Core.Content.Construction.Startup;
using Stellaris.Core.Research;
using Stellaris.Core.Economy;
using Stellaris.Core.Scripting;
using Stellaris.Session.M3;
using Stellaris.Session.M3.Construction;

// Проверяет только настоящий установленный source Stellaris 4.5.2; синтетические AST не используются.
internal static class FullCatalogSourceChecks
{
    public static async Task RunAsync(string root, Action<bool, string> check)
    {
        var economy = await new PlanetEconomyImporter().ImportAsync(root);
        var full = await new ConstructionDefinitionImporter().ImportFullCatalogAsync(root);
        var content = await M3ConstructionContent.ImportAsync(root, economy);
        check(economy.Buildings.Count == 165 && economy.Zones.Count == 29,
            $"native economy inventory remains buildings={economy.Buildings.Count}, zones={economy.Zones.Count}");
        check(economy.DefinitionCoverage.Count(item => item.Status == PlanetEconomyDefinitionCoverageStatus.Included) == 194,
            "normal economy importer retains its 194 supported definitions");
        check(economy.JobClosure.Count == 59, $"native job closure remains {economy.JobClosure.Count}");
        check(full.Definitions.Values.Count(definition => definition.Kind == "building") == 498
            && full.Definitions.Values.Count(definition => definition.Kind == "zone") == 146,
            "full source catalog has exactly 498 buildings and 146 zones");
        var nativeSource = new ContentSession(root);
        var nativeIds = new List<string>();
        foreach (var directory in new[] { "buildings", "zones" })
        foreach (var path in Directory.EnumerateFiles(Path.Combine(root, "common", directory), "*.txt", SearchOption.AllDirectories))
        {
            var parsed = nativeSource.LoadFile(Path.GetRelativePath(root, path).Replace('\\', '/'));
            nativeIds.AddRange(parsed.Root.Entries.Where(entry => !entry.Key.StartsWith('@')).Select(entry => entry.Key));
        }
        nativeSource.VerifyStable();
        check(full.Definitions.Count == 644 && nativeIds.Count == 644 && full.Definitions.Keys.ToHashSet(StringComparer.Ordinal)
            .SetEquals(nativeIds), "full catalog IDs exactly match independently read native building and zone inventory");
        check(content.Content.Catalogs.Count == 648
            && content.Content.Catalogs.Count(pair => pair.Value.Definitions[pair.Key].Kind == "district") == 4,
            "M3 registry retains full 644 catalog plus four native districts");

        var sourceFiles = full.Files.ToDictionary(file => file.RelativePath, StringComparer.Ordinal);
        check(sourceFiles.Count != 0 && sourceFiles.Values.All(file => file.ByteLength > 0 && file.Sha256.Length == 64),
            "full catalog carries complete source snapshot metadata");
        foreach (var file in sourceFiles.Values)
        {
            var path = Path.Combine(root, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            var bytes = await File.ReadAllBytesAsync(path);
            check(bytes.LongLength == file.ByteLength && Convert.ToHexString(SHA256.HashData(bytes)).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase),
                "full catalog source SHA/length matches installed source: " + file.RelativePath);
        }
        IEnumerable<ConstructionField> Flatten(IEnumerable<ConstructionField> fields) => fields.SelectMany(field => new[] { field }.Concat(Flatten(field.Children)));
        IEnumerable<ScriptEntry> FlattenScript(IEnumerable<ScriptEntry> fields) => fields.SelectMany(field => new[] { field }.Concat(
            field.Block is null ? [] : FlattenScript(field.Block.Entries)));
        var allFields = full.Definitions.Values.SelectMany(definition => Flatten(definition.Fields)).ToArray();
        check(allFields.Length > 0 && allFields.All(field => sourceFiles.ContainsKey(field.Origin.Source)
            && field.Origin.Line > 0 && field.Origin.Column > 0), "expanded catalog fields retain source file and line/column origins");
        check(allFields.SelectMany(field => field.InlineCallSites).All(origin => sourceFiles.ContainsKey(origin.Source)
            && origin.Line > 0 && origin.Column > 0), "expanded inline fields retain call-site provenance");
        check(allFields.Any(field => field.Key == "country_uses_consumer_goods" && field.Scalar is "yes" or "no"
                && sourceFiles.ContainsKey(field.Origin.Source)),
            "full native catalog retains source-backed country_uses_consumer_goods predicates");

        var startups = await M3StartupCatalog.ImportAsync(root);
        var profile = new M3BaselineEconomyProfile(economy, new StartupPopulationResourceImporter().Import(root),
            new StartupTraitWorkforceImporter().Import(root).BonusesByTemplate, new TechnologyImporter().Import(root).CategoryParents);
        var presetRuns = new List<(M3StartupColony Startup, ConstructionLiveRuntime Runtime, ConstructionOrdinaryTemplate Template, ConstructionContext Context)>();
        foreach (var templateId in new[] { "economic", "scientific", "military" })
        {
            var home = startups.Select(templateId, 123);
            var startup = M3StartupColony.Create("full-catalog-" + templateId, 1, home.Homeworld, home.Plan);
            var runtime = content.Create(startup, profile.CreateConstructionContributions(startup));
            var template = ConstructionOrdinaryTemplate.Get(templateId);
            var context = content.Context(startup, template, runtime,
                M3ConstructionContent.FromBinding.ActingCountry(startup.Plan.Context.IsNomadic));
            presetRuns.Add((startup, runtime, template, template.CreateInitialConstructionContext(context)));
        }
        check(presetRuns.Count == 3, "visibility contexts are built from three native M3 startup presets");

        // Отдельный проход повторяет живой ConstructionEvaluationFor на фактическом EconomyWorld.
        var founderRecipes = new StartupFounderSpeciesImporter().Import(root);
        var actualProfile = new M3BaselineEconomyProfile(economy, new StartupPopulationResourceImporter().Import(root),
            new StartupTraitWorkforceImporter().Import(root).BonusesByTemplate, new TechnologyImporter().Import(root).CategoryParents, founderRecipes);
        var actualWorld = actualProfile.CreateWorld();
        var actualRuns = new List<(M3StartupColony Startup, ConstructionLiveRuntime Runtime, ConstructionOrdinaryTemplate Template,
            ConstructionContext Context, ConstructionContext BoundContext, PlanetForeignOwnershipSnapshot ForeignOwnership,
            EntityId Country, EntityId Planet)>();
        foreach (var templateId in new[] { "economic", "scientific", "military" })
        {
            var home = startups.Select(templateId, 123);
            var startup = M3StartupColony.Create("full-catalog-actual-" + templateId, 1, home.Homeworld, home.Plan);
            var runtime = content.Create(startup, actualProfile.CreateConstructionContributions(startup));
            var country = actualWorld.CreateCountry();
            var planet = actualWorld.CreatePlanet(country);
            actualProfile.InitializeColony(actualWorld, country, planet, templateId, home.Plan);
            var foreignOwnership = actualWorld.GetPlanetForeignOwnership(planet)
                ?? throw new InvalidOperationException("Source colony initialization did not register foreign ownership.");
            var species = actualWorld.GetOwnedSpeciesSnapshot(country);
            var founder = actualWorld.GetFounderSpecies(country);
            var report = actualWorld.PreviewPlanetReport(planet);
            bool? usesConsumerGoods = report.Upkeep.GetValueOrDefault("consumer_goods") > 0 ? true : null;
            var template = ConstructionOrdinaryTemplate.Get(templateId);
            var context = content.Context(startup, template, runtime,
                M3ConstructionContent.FromBinding.ActingCountry(startup.Plan.Context.IsNomadic), template.StartingTechnologies,
                actualProfile.InitialPopulation, usesConsumerGoods, ownerSpecies: species,
                ownerFounderSpeciesId: founder?.Id, ownerCountryEntityId: country);
            var boundContext = content.Context(startup, template, runtime,
                M3ConstructionContent.FromBinding.ActingCountry(startup.Plan.Context.IsNomadic), template.StartingTechnologies,
                actualProfile.InitialPopulation, usesConsumerGoods, ownerSpecies: species,
                ownerFounderSpeciesId: founder?.Id, ownerCountryEntityId: country,
                foreignOwnership: foreignOwnership, planetEntityId: planet);
            check(species is { Count: > 0 } && founder is not null && species.Any(item => item.Id == founder.Id)
                && species.All(item => item.OwnerCountry == country),
                $"{templateId}: actual owner/founder species comes from initialized population group");
            check(context.Technologies?.SetEquals(template.StartingTechnologies) == true
                && actualProfile.InitialPopulation > 0
                && context.SourceFacts?.OwnerSpecies?.Any(item => item.Id == founder!.Id) == true
                && context.SourceFacts.OwnerFounderSpeciesId == founder!.Id,
                $"{templateId}: construction context binds native starting technologies, sapient population, owner and founder");
            check(context.OwnerFacts.GetValueOrDefault(ConstructionOwnerPredicate.UsesConsumerGoods) == usesConsumerGoods,
                $"{templateId}: country_uses_consumer_goods uses positive actual preview upkeep or remains Unknown");
            check(foreignOwnership.Planet == planet && foreignOwnership.PlanetOwner == country
                && foreignOwnership.BranchOfficeOwner is null && foreignOwnership.Holdings.Count == 0,
                $"{templateId}: InitializeColony registers actual owner-bound empty foreign ownership snapshot");
            actualRuns.Add((startup, runtime, template, template.CreateInitialConstructionContext(context),
                template.CreateInitialConstructionContext(boundContext), foreignOwnership, country, planet));
        }
        var actualWorldHash = actualWorld.StateHash();
        check(actualRuns.Count == 3 && actualWorldHash.Length == 64,
            "actual economy contexts cover all three native presets with canonical world hash");
        var actualFork = actualWorld.Fork();
        check(actualFork.StateHash() == actualWorldHash && actualRuns.All(run =>
                actualFork.GetPlanetForeignOwnership(run.Planet) is { } snapshot
                && snapshot.Planet == run.ForeignOwnership.Planet && snapshot.PlanetOwner == run.ForeignOwnership.PlanetOwner
                && snapshot.BranchOfficeOwner == run.ForeignOwnership.BranchOfficeOwner
                && snapshot.Holdings.SequenceEqual(run.ForeignOwnership.Holdings)),
            "Fork preserves all three immutable planet foreign ownership snapshots and canonical hash");
        var duplicateInitializationRejected = false;
        try { actualWorld.InitializePlanetForeignOwnership(actualRuns[0].Planet); }
        catch (InvalidOperationException) { duplicateInitializationRejected = true; }
        var invalidExpectedPlanetRejected = false;
        var invalidExpectedOwnerRejected = false;
        try
        {
            _ = new ConstructionContext(ownerExists: true, foreignOwnership: actualRuns[0].ForeignOwnership,
                expectedPlanet: actualRuns[1].Planet, expectedPlanetOwner: actualRuns[0].Country);
        }
        catch (ArgumentException) { invalidExpectedPlanetRejected = true; }
        try
        {
            _ = new ConstructionContext(ownerExists: true, foreignOwnership: actualRuns[0].ForeignOwnership,
                expectedPlanet: actualRuns[0].Planet, expectedPlanetOwner: actualRuns[1].Country);
        }
        catch (ArgumentException) { invalidExpectedOwnerRejected = true; }
        check(duplicateInitializationRejected && invalidExpectedPlanetRejected && invalidExpectedOwnerRejected
            && actualWorld.StateHash() == actualWorldHash,
            "duplicate initialization and mismatched expected planet/owner reject without mutating actual worlds");

        var genericWorld = actualProfile.CreateWorld();
        var genericCountry = genericWorld.CreateCountry();
        var genericPlanet = genericWorld.CreatePlanet(genericCountry);
        check(genericWorld.GetPlanetForeignOwnership(genericPlanet) is null,
            "fresh generic world keeps foreign ownership snapshot absent before explicit initialization");
        var genericHome = startups.Select("economic", 123);
        var genericStartup = M3StartupColony.Create("full-catalog-generic-uninitialized", 1, genericHome.Homeworld, genericHome.Plan);
        var genericRuntime = content.Create(genericStartup, actualProfile.CreateConstructionContributions(genericStartup));
        var genericTemplate = ConstructionOrdinaryTemplate.Get("economic");
        var genericContext = content.Context(genericStartup, genericTemplate, genericRuntime,
            M3ConstructionContent.FromBinding.ActingCountry(false), genericTemplate.StartingTechnologies,
            actualProfile.InitialPopulation, ownerCountryEntityId: genericCountry, planetEntityId: genericPlanet);
        var privateConsortium = "building_private_mining_consortium";
        check(Flatten(full.Definitions[privateConsortium].Fields)
                .Any(field => field.Key == "has_branch_office" && field.Operator == "=" && field.Scalar == "yes")
            && actualRuns.All(run => ConstructionCatalogAssessor.Visibility(full, privateConsortium,
                run.Context, content.Content.Conditions).Truth == ConstructionTruth.Unknown)
            && actualRuns.All(run => ConstructionCatalogAssessor.Visibility(full, privateConsortium,
                run.BoundContext, content.Content.Conditions).Truth == ConstructionTruth.False)
            && ConstructionCatalogAssessor.Visibility(full, privateConsortium, genericContext, content.Content.Conditions).Truth == ConstructionTruth.Unknown,
            "native private mining consortium has_branch_office resolves Unknown without snapshot, False with actual empty snapshot");
        var nativeTriggerSource = new ContentSession(root).LoadFile("common/scripted_triggers/01_scripted_triggers_jobs.txt").Root;
        var merchantHelper = nativeTriggerSource.Named("politician_is_merchant").Single().AsBlock();
        var nativeHoldingNode = FlattenScript(merchantHelper.Entries).Single(field => field.Key == "has_holding"
            && field.Block is { Entries: var children }
            && children.Any(child => child.Key == "holding" && child.Scalar == "building_imperial_concession_port")
            && children.Any(child => child.Key == "owner" && child.Scalar == "branch_office_owner"));
        check(nativeHoldingNode.Operator == "=" && nativeHoldingNode.Scalar is null
                && nativeHoldingNode.Block!.Entries.Count == 2
                && nativeHoldingNode.Block.Entries.All(field => field.Operator == "=" && field.Block is null),
            "native politician_is_merchant preserves actual has_holding exact-ID and branch_office_owner source shape");
        var triggerPath = "common/scripted_triggers/01_scripted_triggers_jobs.txt";
        var triggerBytes = await File.ReadAllBytesAsync(Path.Combine(root, triggerPath.Replace('/', Path.DirectorySeparatorChar)));
        var triggerSha = Convert.ToHexString(SHA256.HashData(triggerBytes));
        var planetConditionCtor = typeof(PlanetEconomyCondition).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null, [typeof(string), typeof(string), typeof(string), typeof(IEnumerable<PlanetEconomyCondition>),
                typeof(PlanetEconomyProvenance)], modifiers: null)
            ?? throw new MissingMethodException("PlanetEconomyCondition source constructor is unavailable.");
        PlanetEconomyCondition ToPlanetCondition(ScriptEntry entry)
        {
            var children = entry.Block?.Entries.Select(ToPlanetCondition).ToArray() ?? [];
            return (PlanetEconomyCondition)planetConditionCtor.Invoke([entry.Key, entry.Operator, entry.Scalar, children,
                new PlanetEconomyProvenance(entry.Source, triggerSha, entry.Line, entry.Column)]);
        }
        var nativeHoldingCondition = ToPlanetCondition(nativeHoldingNode);
        check(nativeHoldingCondition.Key == nativeHoldingNode.Key
                && nativeHoldingCondition.Value is null
                && nativeHoldingCondition.Provenance.RelativePath == nativeHoldingNode.Source
                && nativeHoldingCondition.Provenance.Line == nativeHoldingNode.Line
                && nativeHoldingCondition.Provenance.Column == nativeHoldingNode.Column
                && nativeHoldingCondition.Provenance.Sha256 == triggerSha
                && nativeHoldingCondition.Children.Select(child => (child.Key, child.Operator, child.Value))
                    .SequenceEqual(nativeHoldingNode.Block!.Entries.Select(child => (child.Key, child.Operator, child.Scalar))),
            "native has_holding subtree converts losslessly with original source provenance");
        check(actualRuns.All(run => ConstructionCatalogAssessor.EvaluatePlanetEconomyCondition(
                    nativeHoldingCondition, run.Context, content.Content.Conditions).Truth == ConstructionTruth.Unknown)
                && actualRuns.All(run => ConstructionCatalogAssessor.EvaluatePlanetEconomyCondition(
                    nativeHoldingCondition, run.BoundContext, content.Content.Conditions).Truth == ConstructionTruth.False)
                && ConstructionCatalogAssessor.EvaluatePlanetEconomyCondition(
                    nativeHoldingCondition, genericContext, content.Content.Conditions).Truth == ConstructionTruth.Unknown,
            "exact native has_holding condition remains Unknown without snapshot and resolves False only for actual initialized empty snapshots");

        var visibility = new HashSet<ConstructionTruth>();
        foreach (var run in presetRuns)
        foreach (var definition in full.Definitions.Values)
            visibility.Add(ConstructionCatalogAssessor.Visibility(full, definition.Id, run.Context, content.Content.Conditions).Truth);
        check(visibility.Contains(ConstructionTruth.True) && visibility.Contains(ConstructionTruth.False)
            && visibility.Contains(ConstructionTruth.Unknown), "native preset conditions exercise visible, hidden and unresolved potential");

        var actualVisibilityGroups = new Dictionary<ConstructionTruth, int>();
        var actualEligibilityGroups = new Dictionary<ConstructionTruth, int>();
        var boundVisibilityGroups = new Dictionary<ConstructionTruth, int>();
        var boundEligibilityGroups = new Dictionary<ConstructionTruth, int>();
        var actualUnknownGroups = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var actualPresetSummaries = new List<string>();
        var lostKnownFacts = new List<string>();
        foreach (var run in actualRuns)
        {
            var perPresetVisibility = new Dictionary<ConstructionTruth, int>();
            var perPresetEligibility = new Dictionary<ConstructionTruth, int>();
            var perPresetBoundVisibility = new Dictionary<ConstructionTruth, int>();
            var perPresetBoundEligibility = new Dictionary<ConstructionTruth, int>();
            var perPresetUnknownGates = new SortedDictionary<string, int>(StringComparer.Ordinal);
            var unresolvedQuotesRefused = 0;
            var boundUnresolvedQuotesRefused = 0;
            foreach (var pair in content.Content.Catalogs)
            {
                var potential = ConstructionCatalogAssessor.Visibility(pair.Value, pair.Key, run.Context, content.Content.Conditions);
                var eligibility = ConstructionCatalogAssessor.Eligibility(pair.Value, pair.Key, run.Context, content.Content.Conditions);
                var boundPotential = ConstructionCatalogAssessor.Visibility(pair.Value, pair.Key, run.BoundContext, content.Content.Conditions);
                var boundEligibility = ConstructionCatalogAssessor.Eligibility(pair.Value, pair.Key, run.BoundContext, content.Content.Conditions);
                perPresetVisibility[potential.Truth] = perPresetVisibility.GetValueOrDefault(potential.Truth) + 1;
                perPresetEligibility[eligibility.Truth] = perPresetEligibility.GetValueOrDefault(eligibility.Truth) + 1;
                perPresetBoundVisibility[boundPotential.Truth] = perPresetBoundVisibility.GetValueOrDefault(boundPotential.Truth) + 1;
                perPresetBoundEligibility[boundEligibility.Truth] = perPresetBoundEligibility.GetValueOrDefault(boundEligibility.Truth) + 1;
                if (potential.Truth != ConstructionTruth.Unknown && boundPotential.Truth != potential.Truth)
                    lostKnownFacts.Add($"{run.Template.Id}/{pair.Key}/potential:{potential.Truth}->{boundPotential.Truth}");
                if (eligibility.Truth != ConstructionTruth.Unknown && boundEligibility.Truth != eligibility.Truth)
                    lostKnownFacts.Add($"{run.Template.Id}/{pair.Key}/eligibility:{eligibility.Truth}->{boundEligibility.Truth}");
                if (eligibility.Truth == ConstructionTruth.Unknown)
                foreach (var gate in eligibility.Gates)
                {
                    perPresetUnknownGates[gate.DependencyId] = perPresetUnknownGates.GetValueOrDefault(gate.DependencyId) + 1;
                    actualUnknownGroups[gate.DependencyId] = actualUnknownGroups.GetValueOrDefault(gate.DependencyId) + 1;
                }
                if (eligibility.Truth == ConstructionTruth.Unknown)
                {
                    var refusal = content.Content.Quote(pair.Key, run.Context, null, null);
                    if (refusal.Quote is null && refusal.Code == "eligibility-unresolved") unresolvedQuotesRefused++;
                }
                if (boundEligibility.Truth == ConstructionTruth.Unknown)
                {
                    var refusal = content.Content.Quote(pair.Key, run.BoundContext, null, null);
                    if (refusal.Quote is null && refusal.Code == "eligibility-unresolved") boundUnresolvedQuotesRefused++;
                }
            }
            foreach (var pair in perPresetVisibility) actualVisibilityGroups[pair.Key] = actualVisibilityGroups.GetValueOrDefault(pair.Key) + pair.Value;
            foreach (var pair in perPresetEligibility) actualEligibilityGroups[pair.Key] = actualEligibilityGroups.GetValueOrDefault(pair.Key) + pair.Value;
            foreach (var pair in perPresetBoundVisibility) boundVisibilityGroups[pair.Key] = boundVisibilityGroups.GetValueOrDefault(pair.Key) + pair.Value;
            foreach (var pair in perPresetBoundEligibility) boundEligibilityGroups[pair.Key] = boundEligibilityGroups.GetValueOrDefault(pair.Key) + pair.Value;
            check(perPresetVisibility.Values.Sum() == 648 && perPresetEligibility.Values.Sum() == 648
                && perPresetBoundVisibility.Values.Sum() == 648 && perPresetBoundEligibility.Values.Sum() == 648,
                $"{run.Template.Id}: before/after potential and eligibility each cover all 648 definitions");
            check(unresolvedQuotesRefused == perPresetEligibility.GetValueOrDefault(ConstructionTruth.Unknown),
                $"{run.Template.Id}: direct quotes refuse every actual eligibility=Unknown definition");
            check(boundUnresolvedQuotesRefused == perPresetBoundEligibility.GetValueOrDefault(ConstructionTruth.Unknown),
                $"{run.Template.Id}: bound quotes refuse every actual eligibility=Unknown definition");
            actualPresetSummaries.Add($"{run.Template.Id} before potential[{Format(perPresetVisibility)}] eligibility[{Format(perPresetEligibility)}]; after potential[{Format(perPresetBoundVisibility)}] eligibility[{Format(perPresetBoundEligibility)}]; unknownGates[{string.Join(",", perPresetUnknownGates.Take(6).Select(pair => pair.Key + ":" + pair.Value))}]");
        }
        check(actualRuns.Count == 3 && content.Content.Catalogs.Count == 648
            && actualVisibilityGroups.Values.Sum() == 648 * 3
            && actualEligibilityGroups.Values.Sum() == 648 * 3
            && boundVisibilityGroups.Values.Sum() == 648 * 3
            && boundEligibilityGroups.Values.Sum() == 648 * 3
            && lostKnownFacts.Count == 0
            && actualVisibilityGroups.GetValueOrDefault(ConstructionTruth.Unknown) > 0
            && actualEligibilityGroups.GetValueOrDefault(ConstructionTruth.Unknown) > 0
            && boundVisibilityGroups.GetValueOrDefault(ConstructionTruth.Unknown) > 0
            && boundEligibilityGroups.GetValueOrDefault(ConstructionTruth.Unknown) > 0,
            $"actual catalog before/after totals 648 x 3; {string.Join("; ", actualPresetSummaries)}; before potential[{Format(actualVisibilityGroups)}] eligibility[{Format(actualEligibilityGroups)}]; after potential[{Format(boundVisibilityGroups)}] eligibility[{Format(boundEligibilityGroups)}]; lost-known={lostKnownFacts.Count}");
        check(actualWorld.StateHash() == actualWorldHash,
            "full catalog assessment does not mutate actual economy world");

        foreach (var run in presetRuns)
        {
            var view = new M3ConstructionAdapter(content, run.Startup, run.Template).Project(run.Runtime, 1);
            var projectedIds = view.Catalog.Select(item => item.DefinitionId).ToHashSet(StringComparer.Ordinal);
            var hiddenIds = full.Definitions.Values.Where(definition => ConstructionCatalogAssessor.Visibility(full,
                definition.Id, run.Context, content.Content.Conditions).Truth == ConstructionTruth.False).Select(definition => definition.Id).ToArray();
            var unresolvedIds = full.Definitions.Values.Where(definition => ConstructionCatalogAssessor.Visibility(full,
                definition.Id, run.Context, content.Content.Conditions).Truth == ConstructionTruth.Unknown).Select(definition => definition.Id).ToArray();
            check(hiddenIds.Length > 0 && hiddenIds.All(id => !projectedIds.Contains(id)), "M3 catalog DTO omits source definitions with potential=False");
            check(unresolvedIds.Length > 0 && unresolvedIds.All(projectedIds.Contains), "M3 catalog DTO retains source definitions with potential=Unknown");
        }

        var actualUnknownDtoCount = 0;
        foreach (var run in actualRuns)
        {
            var building = content.PresetModifiers(run.Template, ConstructionKind.Building);
            var district = content.PresetModifiers(run.Template, ConstructionKind.District);
            var evaluation = new M3ConstructionEvaluation(run.BoundContext, M3ConstructionContent.EconomyFacts(run.Startup),
                new("full-catalog-actual-context-v1", checked(building.Cost), checked(building.Speed), checked(district.Speed)),
                M3ConstructionContent.FromBinding.ActingCountry(run.Startup.Plan.Context.IsNomadic));
            var view = new M3ConstructionAdapter(content, run.Startup, run.Template).Project(run.Runtime, 1, evaluation);
            var ids = view.Catalog.Select(item => item.DefinitionId).ToHashSet(StringComparer.Ordinal);
            var hidden = full.Definitions.Values.Where(definition => ConstructionCatalogAssessor.Visibility(full, definition.Id,
                run.BoundContext, content.Content.Conditions).Truth == ConstructionTruth.False).Select(definition => definition.Id).ToArray();
            var unknown = full.Definitions.Values.Where(definition => ConstructionCatalogAssessor.Visibility(full, definition.Id,
                run.BoundContext, content.Content.Conditions).Truth == ConstructionTruth.Unknown).Select(definition => definition.Id).ToHashSet(StringComparer.Ordinal);
            var unknownEligibility = full.Definitions.Values.Where(definition => ConstructionCatalogAssessor.Eligibility(full, definition.Id,
                run.BoundContext, content.Content.Conditions).Truth == ConstructionTruth.Unknown).Select(definition => definition.Id).ToHashSet(StringComparer.Ordinal);
            var unresolved = view.Catalog.Where(item => unknown.Contains(item.DefinitionId)).ToArray();
            check(hidden.Length > 0 && hidden.All(id => !ids.Contains(id)),
                $"{run.Template.Id}: actual-context DTO omits potential=False definitions");
            check(unresolved.Length == unknown.Count && unknown.All(ids.Contains),
                $"{run.Template.Id}: DTO retains every potential=Unknown definition");
            check(unresolved.All(item => item.Quote is null && item.Eligibility == "unavailable"),
                $"{run.Template.Id}: every potential=Unknown row is disabled and unquoted");
            var unresolvedDisabled = view.Catalog.Where(item => unknownEligibility.Contains(item.DefinitionId)).ToArray();
            check(unresolvedDisabled.Length == unknownEligibility.Count && unknownEligibility.All(ids.Contains)
                    && unresolvedDisabled.All(item => item.Quote is null && item.Eligibility == "unavailable"
                    && item.Gates.Any(gate => gate.Code == "dependency-unresolved")),
                $"{run.Template.Id}: eligibility=Unknown remains visible, disabled and unquoted");
            actualUnknownDtoCount += unresolved.Length;
        }
        check(actualUnknownDtoCount > 0, "all actual-context DTOs preserve at least one unresolved catalog row");
        check(actualWorld.StateHash() == actualWorldHash,
            "DTO projection with actual construction evaluations leaves EconomyWorld unchanged");

        var metadataOnly = full.Definitions.Values.Where(definition => !economy.Buildings.ContainsKey(definition.Id)
            && !economy.Zones.ContainsKey(definition.Id)).ToArray();
        check(metadataOnly.Length > 0 && metadataOnly.All(definition => definition.Fields.Count > 0
            && definition.Origin.Line > 0 && sourceFiles.ContainsKey(definition.Origin.Source)),
            "definitions excluded from economy remain source-backed catalog metadata");
        var metadataQuoteCandidate = metadataOnly.SelectMany(definition => presetRuns.Select(run => (Definition: definition, run.Context)))
            .FirstOrDefault(candidate => ConstructionCatalogAssessor.Visibility(full, candidate.Definition.Id,
                candidate.Context, content.Content.Conditions).Truth != ConstructionTruth.False);
        var metadataDefinition = metadataQuoteCandidate.Definition
            ?? throw new InvalidOperationException("No metadata-only definition is visible or unresolved in a native preset context.");
        check(true, "source-backed metadata-only definition is visible or unresolved in a native preset context");
        var candidateContext = metadataQuoteCandidate.Context;
        var quote = content.Content.Quote(metadataDefinition.Id, candidateContext, null, null);
        var candidateEligibility = ConstructionCatalogAssessor.Eligibility(full, metadataDefinition.Id,
            candidateContext, content.Content.Conditions).Truth;
        var expectedRefusal = candidateEligibility switch
        {
            ConstructionTruth.False => "not-eligible",
            ConstructionTruth.Unknown => "eligibility-unresolved",
            _ => "economy-definition-unavailable"
        };
        check(quote.Quote is null && quote.Code == expectedRefusal,
            "manual quote of source-backed metadata-only definition is refused under its actual eligibility result");

        var techDefinitions = full.Definitions.Values.Where(definition => definition.Fields.Any(field => field.Key == "prerequisites"
            && field.Operator == "=" && field.Scalar is null && field.Children.Count != 0
            && field.Children.All(child => child.Operator == "" && child.Scalar is not null))).ToArray();
        check(techDefinitions.Length != 0, "native full catalog contains declared technology prerequisites");
        var unresearchedPrerequisiteCount = 0;
        foreach (var definition in techDefinitions)
        {
            foreach (var run in presetRuns)
            {
                var assessment = ConstructionCatalogAssessor.Assess(full, definition.Id, run.Context, content.Content.Conditions);
                if (assessment.Prerequisites.Truth == ConstructionTruth.False
                    && ConstructionCatalogAssessor.Visibility(full, definition.Id, run.Context, content.Content.Conditions).Truth != ConstructionTruth.False)
                {
                    unresearchedPrerequisiteCount++;
                    check(ConstructionCatalogAssessor.Eligibility(full, definition.Id, run.Context, content.Content.Conditions).Truth == ConstructionTruth.False
                        && new M3ConstructionAdapter(content, run.Startup, run.Template).Project(run.Runtime, 1).Catalog.Any(item => item.DefinitionId == definition.Id && item.Quote is null),
                        "actual unmet native prerequisite blocks eligibility while potential visibility remains separate: " + definition.Id + "/" + run.Template.Id);
                    break;
                }
            }
            if (unresearchedPrerequisiteCount > 0) break;
        }
        check(unresearchedPrerequisiteCount > 0, "native preset contexts contain at least one actually unmet declared prerequisite");
        var typedConditions = await new ConstructionDefinitionImporter().ImportAuthorityConditionCatalogAsync(root);
        check(content.Content.Conditions.Definitions.Count == typedConditions.Definitions.Count
            && content.Content.Conditions.ScriptedConditions.Count > 0,
            "scripted dependency closure extends actual checks without changing typed predicate inventory");
        foreach (var helper in content.Content.Conditions.ScriptedConditions.Values)
            check(helper.Kind == "scriptedCondition" && content.Content.Conditions.Files.Any(file => file.RelativePath == helper.Origin.Source),
                "scripted helper preserves native source origin: " + helper.Id);
        var economic = presetRuns[0];
        foreach (var run in presetRuns)
        {
            var capital = run.Runtime.Installed.Values.Single(item => item.Kind == ConstructionKind.Building
                && full.Definitions[item.DefinitionId].Fields.Any(field => field.Key == "capital" && field.Scalar == "yes"));
            var sourceDefinition = full.Definitions[capital.DefinitionId];
            var nativeCapital = nativeSource.LoadFile(sourceDefinition.Origin.Source).Root.Named(capital.DefinitionId).Single().AsBlock();
            check(run.Context.CapitalTier == int.Parse(nativeCapital.Required("capital_tier").AsScalar()) && run.Context.CapitalTier == 2,
                "capital tier comes from actual installed native capital: " + run.Template.Id);
            check(ConstructionCatalogAssessor.EvaluatePlanetHelper(content.Content.Conditions, "has_major_upgraded_capital", run.Context).Truth == ConstructionTruth.False,
                "native major-capital helper rejects current tier2 without a fabricated upgrade: " + run.Template.Id);
        }
        var military = presetRuns.Single(run => run.Template.Id == "military");
        check(ConstructionCatalogAssessor.Visibility(full, "zone_resort_proving_grounds", economic.Context, typedConditions).Truth == ConstructionTruth.Unknown
            && ConstructionCatalogAssessor.Visibility(full, "zone_resort_proving_grounds", economic.Context, content.Content.Conditions).Truth == ConstructionTruth.False
            && ConstructionCatalogAssessor.Visibility(full, "zone_resort_proving_grounds", military.Context, content.Content.Conditions).Truth == ConstructionTruth.True,
            "native militarist helper resolves false/true from actual economic and military preset ethics");
        check(ConstructionCatalogAssessor.Visibility(full, "zone_resort_hunting_ground", economic.Context, typedConditions).Truth == ConstructionTruth.Unknown
            && ConstructionCatalogAssessor.Visibility(full, "zone_resort_hunting_ground", economic.Context, content.Content.Conditions).Truth == ConstructionTruth.True,
            "native xenophile=no helper preserves negative-call polarity from actual ethics");
        check(ConstructionCatalogAssessor.Visibility(full, "zone_subterranean_urban", economic.Context, typedConditions).Truth == ConstructionTruth.Unknown
            && ConstructionCatalogAssessor.Visibility(full, "zone_subterranean_urban", economic.Context, content.Content.Conditions).Truth == ConstructionTruth.False,
            "native subterranean helper reads actual origin and closes a previously unknown condition");
        var beforeUnknown = 0; var afterUnknown = 0;
        foreach (var pair in content.Content.Catalogs)
        {
            if (ConstructionCatalogAssessor.Eligibility(pair.Value, pair.Key, economic.Context, typedConditions).Truth == ConstructionTruth.Unknown) beforeUnknown++;
            if (ConstructionCatalogAssessor.Eligibility(pair.Value, pair.Key, economic.Context, content.Content.Conditions).Truth == ConstructionTruth.Unknown) afterUnknown++;
        }
        check(afterUnknown < beforeUnknown, $"actual eligibility Unknown decreases: {beforeUnknown} -> {afterUnknown}; helpers={content.Content.Conditions.ScriptedConditions.Count}");

        static string Format(IReadOnlyDictionary<ConstructionTruth, int> groups)
            => string.Join(",", groups.OrderBy(pair => pair.Key).Select(pair => pair.Key + ":" + pair.Value));
    }
}

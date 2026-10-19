using System.Collections.ObjectModel;
using Stellaris.Core.Construction;
using Stellaris.Core.Content.Construction;
using Stellaris.Core.Content.Construction.Startup;
using Stellaris.Core.Content.PlanetEconomy;
using Stellaris.Core.Economy;
using Stellaris.Core.Research;
using Stellaris.Session.M3.Construction;

namespace Stellaris.Session.M3;

/// <summary>Связывает импортированный baseline source catalog с экспериментальным M3 EconomyWorld.</summary>
public sealed class M3BaselineEconomyProfile
{
    private static readonly string[] RareResourceIds = ["volatile_motes", "exotic_gases", "rare_crystals", "sr_zro", "sr_dark_matter", "sr_living_metal", "nanites"];
    private readonly PlanetEconomyCatalog _catalog;
    private readonly StartupPopulationResourcePlan _start;
    private readonly StartupFounderSpeciesCatalog? _founderSpecies;
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, decimal>> _traitBonuses;
    private readonly ResourceDefinition[] _resources;
    private readonly JobDefinition[] _jobs;
    private readonly CategoryDefinition[] _categories;
    private readonly IReadOnlyDictionary<string, M3BaselineSourceResource> _sourceResources;
    private readonly IReadOnlyList<M3BaselineCoverageOmission> _coverageOmissions;

    public string ProfileId => _catalog.ProfileId + ":m3-start-v1";
    public decimal InitialPopulation => _start.Population;
    public IReadOnlyDictionary<string, decimal> StartingStocks => _start.StartingStocks;
    public PlanetEconomyCatalog Catalog => _catalog;
    public IReadOnlyList<JobDefinition> RuntimeJobs => Array.AsReadOnly(_jobs);

    /// <summary>Заменяет research overlay; installed effects хранятся отдельно, trait Workforce берётся из чистого baseline.</summary>
    public void ApplyResearchEffects(EconomyWorld world, EntityId planet, string templateId, ResearchEffectProjection effects)
    {
        world.SetModifiers(planet, effects.JobModifiers);
        var baseline = _traitBonuses.GetValueOrDefault(templateId);
        foreach (var job in _jobs)
        {
            world.SetJobBonusWorkforceMultiplier(planet, job.Id,
                checked((baseline?.GetValueOrDefault(job.Id) ?? 0) + effects.JobBonusWorkforce.GetValueOrDefault(job.Id)));
        }
        // Проверяем будущие потоки до публикации кандидата, а не при следующем чтении snapshot.
        var preview = world.PreviewPlanetReport(planet);
        foreach (var area in ResearchCountry.AreaIds)
        { _ = checked(preview.Net.GetValueOrDefault(area + "_research") * effects.AreaResearchMultipliers[area]); }
    }

    public M3BaselineEconomyProfile(PlanetEconomyCatalog catalog, StartupPopulationResourcePlan start,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, decimal>>? traitBonuses = null,
        IReadOnlyDictionary<string, string?>? categoryParents = null, StartupFounderSpeciesCatalog? founderSpecies = null)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _start = start ?? throw new ArgumentNullException(nameof(start));
        _founderSpecies = founderSpecies;
        if (founderSpecies is not null && (founderSpecies.Sources.Profile != "m3-founder-species-HUM-452-v1"
            || founderSpecies.Sources.Version != "v4.5.2"
            || !founderSpecies.Definitions.Keys.Order(StringComparer.Ordinal).SequenceEqual(new[] { "economic", "military", "scientific" }, StringComparer.Ordinal)))
            throw new InvalidDataException("Founder species source profile is unresolved.");
        if (catalog.GameVersion != "v4.5.2" || catalog.ProfileId != "planet-economy-basic-452-v1" || start.Population <= 0)
        {
            throw new InvalidDataException("M3 требует принятый economy baseline и положительный source start population.");
        }
        _traitBonuses = traitBonuses ?? new Dictionary<string, IReadOnlyDictionary<string, decimal>>(StringComparer.Ordinal);
        _jobs = catalog.Jobs.Values.OrderBy(job => job.Id, StringComparer.Ordinal)
            .Where(job => JobGate(catalog, job) is null).Select(CompileJob).ToArray();
        _coverageOmissions = Array.AsReadOnly(catalog.Buildings.Values.Concat(catalog.Districts.Values).Concat(catalog.Zones.Values)
            .SelectMany(definition => definition.Terms.Where(IsLeaderDependentSoldierTerm)
                .Select(term => new M3BaselineCoverageOmission(definition.Id, term.TargetId, term.Kind.ToString(), term.SourceKey,
                    term.SourceExpression, term.Provenance.RelativePath, term.Provenance.Line,
                    "Исключено только governor/leader-dependent добавление soldier; базовая ёмкость soldier и остальные job flows сохраняются.")))
            .Concat(catalog.Jobs.Values.Where(job => JobGate(catalog, job) is not null)
                .Select(job => new M3BaselineCoverageOmission(job.Id, job.Id, "JobRecipe", null, null,
                    job.Provenance.RelativePath, job.Provenance.Line, JobGate(catalog, job)!))).ToArray());
        var usedResourceIds = new HashSet<string>(_start.StartingStocks.Keys.Concat(RareResourceIds), StringComparer.Ordinal);
        foreach (var job in _jobs)
        {
            usedResourceIds.UnionWith(job.Production.Keys);
            usedResourceIds.UnionWith(job.Upkeep.Keys);
        }
        foreach (var definition in catalog.Buildings.Values.Concat(catalog.Districts.Values).Concat(catalog.Zones.Values))
        {
            foreach (var term in definition.Terms.Where(term => term.ConditionDisposition is PlanetEconomyConditionDisposition.Unconditional
                or PlanetEconomyConditionDisposition.ActiveInOrdinaryContext
                && term.Kind is PlanetEconomyTermKind.BuildingUpkeep or PlanetEconomyTermKind.DistrictUpkeep or PlanetEconomyTermKind.ZoneUpkeep or PlanetEconomyTermKind.ConstructionCost)
                .Where(term => catalog.Resources.ContainsKey(term.TargetId)))
            {
                usedResourceIds.Add(term.TargetId);
            }
        }
        var usedResources = usedResourceIds.Order(StringComparer.Ordinal).ToDictionary(id => id,
            id => catalog.Resources.TryGetValue(id, out var resource) ? resource : null, StringComparer.Ordinal);
        foreach (var stock in start.StartingStocks.Keys.Where(stock => !usedResources.ContainsKey(stock)))
        {
            if (!start.StartingResourceMaximums.TryGetValue(stock, out var maximum))
            {
                throw new InvalidDataException($"Start stock {stock} отсутствует в baseline resource catalog и не имеет source cap.");
            }
            usedResources.Add(stock, null);
        }
        _resources = usedResources.Select(pair => pair.Value is null
            ? new ResourceDefinition(pair.Key, start.StartingResourceMaximums[pair.Key])
            : pair.Value.StoragePolicy switch
        {
            PlanetEconomyStoragePolicy.SourceCapped when pair.Value.SourceMaximum is { } maximum => new ResourceDefinition(pair.Key, maximum),
            PlanetEconomyStoragePolicy.UnlimitedReserve when pair.Value.SourceMaximum is null => new ResourceDefinition(pair.Key, 0, ResourceStoragePolicy.UnlimitedReserve),
            PlanetEconomyStoragePolicy.Unspecified when pair.Value.SourceMaximum is null => new ResourceDefinition(pair.Key, null, ResourceStoragePolicy.Unspecified),
            _ => throw new InvalidDataException($"Resource storage policy не разрешена: {pair.Key} ({pair.Value.StoragePolicy}).")
        }).ToArray();
        _sourceResources = new ReadOnlyDictionary<string, M3BaselineSourceResource>(usedResources.Select(pair => pair.Value is null
            ? new M3BaselineSourceResource(pair.Key, "source-capped", start.StartingResourceMaximums[pair.Key])
            : pair.Value.StoragePolicy switch
            {
                PlanetEconomyStoragePolicy.SourceCapped when pair.Value.SourceMaximum is { } maximum => new M3BaselineSourceResource(pair.Key, "source-capped", maximum),
                PlanetEconomyStoragePolicy.UnlimitedReserve when pair.Value.SourceMaximum is null => new M3BaselineSourceResource(pair.Key, "unlimited-reserve", null),
                PlanetEconomyStoragePolicy.Unspecified when pair.Value.SourceMaximum is null => new M3BaselineSourceResource(pair.Key, "unspecified", null),
                _ => throw new InvalidDataException($"Resource storage policy не разрешена: {pair.Key} ({pair.Value.StoragePolicy}).")
            }).ToDictionary(resource => resource.Id, StringComparer.Ordinal));
        if (categoryParents is null || _jobs.Any(job => job.SourceEconomicCategory is { } category && !categoryParents.ContainsKey(category)))
        { throw new InvalidDataException("Source economic category closure требуется для baseline jobs."); }
        _categories = categoryParents.Select(pair => new CategoryDefinition(pair.Key, pair.Value))
            .Concat(_jobs.Select(job => new CategoryDefinition(job.Category, null))).ToArray();
    }

    public EconomyWorld CreateWorld() => new(_resources, _jobs, _categories);

    public void InitializeColony(EconomyWorld world, EntityId country, EntityId planet, string templateId, StartupBuildingPlan plan)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(plan);
        var template = ConstructionOrdinaryTemplate.Get(templateId);
        if (plan.Context.Origin != template.OriginId || plan.Context.Authority != StartupAuthority.Regular
            || plan.Context.PlanetClass != "pc_continental" || plan.Gates.Count != 0)
        {
            throw new InvalidDataException("Поддерживается только полный source plan обычного continental origin_default preset.");
        }
        EntityId? speciesId = null;
        if (_founderSpecies is not null)
        {
            var recipe = _founderSpecies.Definitions[templateId];
            var actual = world.GetFounderSpecies(country);
            if (actual is null) speciesId = world.CreateFounderSpecies(country, recipe);
            else
            {
                if (actual.Definition.ClassId != recipe.ClassId || actual.Definition.ArchetypeId != recipe.ArchetypeId
                    || !actual.Definition.Traits.SetEquals(recipe.Traits)) throw new InvalidDataException("Existing founder species differs from source recipe.");
                speciesId = actual.Id;
            }
        }
        world.AddGroup(planet, _start.Population, 1m, speciesId);
        foreach (var stock in _start.StartingStocks) { world.SetBalance(country, stock.Key, stock.Value); }

        var capacity = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var buildings = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var districts = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var zones = new Dictionary<string, decimal>(StringComparer.Ordinal);
        decimal sourceJobOutputMultiplier = 0;
        decimal zoneBuildingSlotsAdd = 0;
        var categoryModifiers = new List<Modifier>();
        var jobBonuses = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var structuralSources = new List<StructuralProductionSource>();
        foreach (var district in plan.Districts)
        {
            var definition = GetDefinition(_catalog.Districts, district.DefinitionId);
            for (var level = 0; level < district.Level; level++) { ApplyDefinition(definition, capacity, districts, ref sourceJobOutputMultiplier, ref zoneBuildingSlotsAdd, categoryModifiers: categoryModifiers, jobBonuses: jobBonuses, modifierResources: _sourceResources.Keys); }
        }
        foreach (var zone in plan.Zones)
        {
            if (!_catalog.Zones.TryGetValue(zone.ZoneId, out var definition))
            {
                if (!HasVerifiedNoRecurringEconomyProof(plan, zone.ZoneId))
                {
                    throw new InvalidDataException($"Installed startup zone definition is absent and lacks source-confirmed zero-flow proof: {zone.ZoneId}.");
                }
                continue;
            }
            var attachedDistricts = plan.Districts.Where(item => item.DefinitionId == zone.DistrictId).ToArray();
            if (attachedDistricts.Length != 1 || attachedDistricts[0].Level <= 0)
            {
                throw new InvalidDataException($"Zone не имеет однозначного установленного district level: {zone.ZoneId}/{zone.DistrictId}.");
            }
            ApplyDefinition(definition, capacity, zones, ref sourceJobOutputMultiplier, ref zoneBuildingSlotsAdd, attachedDistricts[0].Level, categoryModifiers, jobBonuses, _sourceResources.Keys);
        }
        foreach (var building in plan.Buildings) { ApplyDefinition(GetDefinition(_catalog.Buildings, building.DefinitionId), capacity, buildings, ref sourceJobOutputMultiplier, ref zoneBuildingSlotsAdd, categoryModifiers: categoryModifiers, jobBonuses: jobBonuses, modifierResources: _sourceResources.Keys, structuralSources: structuralSources, structuralSourceId: "startup/" + building.Order); }

        RequireRuntimeJobs(capacity);
        world.SetInstalledCapacity(planet, capacity, 0, zoneBuildingSlotsAdd);
        world.SetStructuralProductionSources(planet, structuralSources);
        world.SetBuildingUpkeep(planet, buildings);
        world.SetDistrictUpkeep(planet, districts);
        world.SetZoneUpkeep(planet, zones);
        world.SetInstalledJobEconomyEffects(planet, new InstalledJobEconomyEffects(sourceJobOutputMultiplier, 0m, categoryModifiers, RuntimeJobBonuses(jobBonuses)));
        var bonuses = _traitBonuses.GetValueOrDefault(templateId) ?? new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var job in _jobs)
        {
            var rawCapacity = capacity.GetValueOrDefault(job.Id);
            var slots = rawCapacity / job.WorkforcePerJob;
            world.SetJobs(planet, job.Id, slots);
            world.SetJobBonusWorkforceMultiplier(planet, job.Id, bonuses.GetValueOrDefault(job.Id));
        }
        // Новая source колония начинает собственный реестр; существующий снимок не сбрасывается.
        if (world.GetPlanetForeignOwnership(planet) is null) world.InitializePlanetForeignOwnership(planet);
    }

    /// <summary>Пересобирает structural flows и capacities из текущего immutable graph, заменяя startup aggregate целиком.</summary>
    public IReadOnlyDictionary<string, decimal> RefreshInstalledGraph(EconomyWorld world, EntityId planet, M3StartupColony startup, ConstructionLiveRuntime runtime, string templateId,
        ConstructionContext? context = null, ConstructionCatalogProjection? conditionSource = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(startup);
        ArgumentNullException.ThrowIfNull(runtime);
        if (runtime.ColonyId != startup.ColonyId || runtime.OwnerId != startup.OwnerParticipantId) { throw new InvalidDataException("Construction graph identity differs from source startup colony."); }

        var capacity = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var buildings = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var districts = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var zones = new Dictionary<string, decimal>(StringComparer.Ordinal);
        decimal output = 0;
        decimal slots = 0;
        var categoryModifiers = new List<Modifier>();
        var jobBonuses = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var structuralSources = new List<StructuralProductionSource>();
        foreach (var item in runtime.Installed.Values.OrderBy(item => item.SlotId, StringComparer.Ordinal))
        {
            if (item.Kind == ConstructionKind.Building && item.Enabled)
            {
                var original = startup.Buildings.FirstOrDefault(building => building.SlotInstanceId == item.SlotId);
                var sourceId = original is null ? "slot/" + item.SlotId : "startup/" + original.Order;
                var definition = GetDefinition(_catalog.Buildings, item.DefinitionId);
                ApplyDefinition(definition, capacity, buildings, ref output, ref slots, categoryModifiers: categoryModifiers, jobBonuses: jobBonuses, modifierResources: _sourceResources.Keys, structuralSources: structuralSources, structuralSourceId: sourceId,
                    conditionEvaluator: M3ConstructionEconomyConditions.Create(definition, context, conditionSource));
            }
            else if (item.Kind == ConstructionKind.District)
            {
                ApplyDefinition(GetDefinition(_catalog.Districts, item.DefinitionId), capacity, districts, ref output, ref slots, categoryModifiers: categoryModifiers, jobBonuses: jobBonuses, modifierResources: _sourceResources.Keys);
            }
        }
        foreach (var zone in M3ZoneGraph.Current(startup, runtime))
        {
            if (!_catalog.Zones.TryGetValue(zone.ZoneId, out var definition))
            {
                if (!HasVerifiedNoRecurringEconomyProof(startup.Plan, zone.ZoneId)) { throw new InvalidDataException("Installed zone has no economy definition or verified zero-flow proof: " + zone.ZoneId); }
                continue;
            }
            var level = M3ZoneGraph.AttachedLevel(startup, runtime, zone.DistrictInstanceId);
            if (level <= 0) { throw new InvalidDataException("Installed zone is detached from an active district: " + zone.InstanceId); }
            ApplyDefinition(definition, capacity, zones, ref output, ref slots, level, categoryModifiers, jobBonuses, _sourceResources.Keys);
        }

        RequireRuntimeJobs(capacity);
        world.SetInstalledCapacity(planet, capacity, 0, slots);
        world.SetStructuralProductionSources(planet, structuralSources);
        RefreshInstalledCountryResourceCapacities(world, planet, runtime, context, conditionSource);
        world.SetBuildingUpkeep(planet, buildings);
        world.SetDistrictUpkeep(planet, districts);
        world.SetZoneUpkeep(planet, zones);
        world.SetInstalledJobEconomyEffects(planet, new InstalledJobEconomyEffects(output, 0m, categoryModifiers, RuntimeJobBonuses(jobBonuses)));
        var bonuses = _traitBonuses.GetValueOrDefault(templateId) ?? new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var job in _jobs)
        {
            world.SetJobs(planet, job.Id, capacity.GetValueOrDefault(job.Id) / job.WorkforcePerJob);
            world.SetJobBonusWorkforceMultiplier(planet, job.Id, bonuses.GetValueOrDefault(job.Id));
        }
        return new ReadOnlyDictionary<string, decimal>(capacity);
    }

    /// <summary>Полностью заменяет country storage вклад enabled graph, в том числе после research.</summary>
    public void RefreshInstalledCountryResourceCapacities(EconomyWorld world, EntityId planet, ConstructionLiveRuntime runtime,
        ConstructionContext? context, ConstructionCatalogProjection? conditionSource)
    {
        var amounts = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var item in runtime.Installed.Values.Where(item => item.Kind == ConstructionKind.Building && item.Enabled
            && item.DefinitionId is "building_resource_silo" or "building_archaeostudies_faculty"))
        {
            var resolved = ResolveCountryResourceCapacityAdditions(item.DefinitionId, context, conditionSource);
            foreach (var pair in resolved) amounts[pair.Key] = checked(amounts.GetValueOrDefault(pair.Key) + pair.Value);
        }
        world.SetInstalledCountryResourceCapacityAdditions(planet, amounts);
    }

    /// <summary>Отдельный source storage вклад здания; не подтверждает потребление остальных effects или разрешение quote.</summary>
    public IReadOnlyDictionary<string, decimal> ResolveCountryResourceCapacityAdditions(string definitionId,
        ConstructionContext? context, ConstructionCatalogProjection? conditionSource)
    {
        var definition = GetDefinition(_catalog.Buildings, definitionId);
        var amounts = new Dictionary<string, decimal>(StringComparer.Ordinal);
        if (definitionId == "building_resource_silo")
        {
            var addition = M3ResourceSilo.Resolve(definition, context, conditionSource)!.GenericMaximumAddition;
            foreach (var resource in _resources.Where(resource => resource.StoragePolicy == ResourceStoragePolicy.SourceCapped))
            {
                if (!_catalog.Resources.TryGetValue(resource.Id, out var source) || source.MaximumKind == PlanetEconomyMaximumKind.Unknown)
                    throw new InvalidDataException("resource-maximum-kind-unresolved:" + resource.Id);
                if (source.MaximumKind == PlanetEconomyMaximumKind.Generic) amounts.Add(resource.Id, addition);
            }
        }
        else if (definitionId == "building_archaeostudies_faculty")
        {
            if (context is null || conditionSource is null || !_resources.Any(resource => resource.Id == "minor_artifacts"
                && resource.StoragePolicy == ResourceStoragePolicy.SourceCapped)
                || !_catalog.Resources.TryGetValue("minor_artifacts", out var source)
                || source.MaximumKind != PlanetEconomyMaximumKind.Special) throw new InvalidDataException("faculty-country-capacity-unresolved");
            amounts.Add("minor_artifacts", M3FacultyCountryCapacity.Resolve(definition, context, conditionSource));
        }
        else throw new InvalidDataException("building-country-capacity-consumer-unresolved:" + definitionId);
        return ReadOnly(amounts);
    }

    public IReadOnlyDictionary<string, ConstructionInstalled> CreateConstructionContributions(M3StartupColony startup)
    {
        ArgumentNullException.ThrowIfNull(startup);
        var contributions = new Dictionary<string, ConstructionInstalled>(StringComparer.Ordinal);
        foreach (var building in startup.Buildings)
        {
            var recipe = GetDefinition(_catalog.Buildings, building.DefinitionId);
            contributions.Add(building.InstanceId, CreateContribution(recipe, ConstructionKind.Building));
        }
        foreach (var district in startup.Districts)
        {
            var recipe = GetDefinition(_catalog.Districts, district.DefinitionId);
            contributions.Add(district.InstanceId, CreateContribution(recipe, ConstructionKind.District));
        }
        return new ReadOnlyDictionary<string, ConstructionInstalled>(contributions);

        ConstructionInstalled CreateContribution(PlanetEconomyDefinition recipe, ConstructionKind kind)
        {
            var capacity = new Dictionary<string, decimal>(StringComparer.Ordinal);
            var upkeep = new Dictionary<string, decimal>(StringComparer.Ordinal);
            decimal output = 0;
            decimal slots = 0;
            ApplyDefinition(recipe, capacity, upkeep, ref output, ref slots);
            return new("startup-contribution/" + recipe.Id, recipe.Id, kind,
                new ReadOnlyDictionary<string, decimal>(upkeep), new ReadOnlyDictionary<string, decimal>(capacity));
        }
    }

    public IReadOnlyDictionary<string, M3BaselineSourceResource> Resources => _sourceResources;
    public IReadOnlyDictionary<string, PlanetEconomyJob> SourceJobs => _catalog.Jobs;
    public IReadOnlyDictionary<string, PlanetEconomyJob> Jobs => _catalog.Jobs.Where(pair => _jobs.Any(job => job.Id == pair.Key))
        .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    private void RequireRuntimeJobs(IReadOnlyDictionary<string, decimal> capacity)
    {
        var missing = capacity.FirstOrDefault(pair => pair.Value != 0 && !_jobs.Any(job => job.Id == pair.Key));
        if (missing.Key is not null) { throw new InvalidDataException("Installed capacity requires a gated runtime job: " + missing.Key); }
    }

    private static string? JobGate(PlanetEconomyCatalog catalog, PlanetEconomyJob job)
    {
        try
        {
            var compiled = CompileJob(job);
            var unsupported = compiled.Production.Keys.Concat(compiled.Upkeep.Keys).FirstOrDefault(id =>
                !catalog.Resources.ContainsKey(id));
            return unsupported is null ? null : $"job-resource-storage-unresolved:{job.Id}/{unsupported}";
        }
        catch (InvalidDataException) { return "job-source-recipe-unresolved:" + job.Id; }
    }

    internal static string? DefinitionGate(PlanetEconomyCatalog catalog, PlanetEconomyDefinition definition, bool includeEvidence = false,
        Func<PlanetEconomyCondition?, bool?>? conditionEvaluator = null)
    {
        try
        {
            var unsupportedFlowModifier = definition.Terms.FirstOrDefault(term =>
                ResolveTermDisposition(term, conditionEvaluator) != PlanetEconomyConditionDisposition.InactiveInOrdinaryContext
                && (term.Kind == PlanetEconomyTermKind.ResourceUpkeep
                    || term.Kind == PlanetEconomyTermKind.PlanetModifier && term.TargetId != "planet_jobs_produces_mult" && ConstructionJobEconomyMapping.Find(term.TargetId) is null
                    && (term.TargetId.Contains("produces", StringComparison.Ordinal) || term.TargetId.Contains("upkeep", StringComparison.Ordinal))));
            if (unsupportedFlowModifier is not null)
            { return $"structure-flow-modifier-unresolved:{unsupportedFlowModifier.TargetId}" + (includeEvidence ? $"@{unsupportedFlowModifier.Provenance.RelativePath}:{unsupportedFlowModifier.Provenance.Line}" : ""); }
            var missingFlowResource = definition.Terms.Where(term => ResolveTermDisposition(term, conditionEvaluator) != PlanetEconomyConditionDisposition.InactiveInOrdinaryContext)
                .Select(term => ConstructionJobEconomyMapping.Find(term.TargetId)?.Resource).FirstOrDefault(resource => resource is not null && !catalog.Resources.ContainsKey(resource));
            if (missingFlowResource is not null) { return "modifier-resource-storage-unresolved:" + missingFlowResource; }
            var capacity = new Dictionary<string, decimal>(StringComparer.Ordinal);
            var upkeep = new Dictionary<string, decimal>(StringComparer.Ordinal);
            decimal output = 0, slots = 0;
            ApplyDefinition(definition, capacity, upkeep, ref output, ref slots, conditionEvaluator: conditionEvaluator);
            foreach (var id in capacity.Keys)
            {
                if (!catalog.Jobs.TryGetValue(id, out var job)) { return "job-definition-unresolved:" + id; }
                if (JobGate(catalog, job) is { } gate) { return gate; }
            }
            foreach (var id in upkeep.Keys.Concat(definition.Terms.Where(term => term.Kind == PlanetEconomyTermKind.ConstructionCost
                && (conditionEvaluator is null ? term.ConditionDisposition : ResolveTermDisposition(term, conditionEvaluator))
                    != PlanetEconomyConditionDisposition.InactiveInOrdinaryContext).Select(term => term.TargetId)))
            {
                if (!catalog.Resources.ContainsKey(id))
                { return "resource-storage-unresolved:" + id; }
            }
            return null;
        }
        catch (InvalidDataException exception) { return includeEvidence ? exception.Message : "definition-source-economy-unresolved:" + definition.Id; }
    }
    public IReadOnlyList<M3BaselineCoverageOmission> CoverageOmissions => _coverageOmissions;

    private static bool IsLeaderDependentSoldierTerm(PlanetEconomyTerm term) => ConstructionEconomyTermPolicy.IsLeaderDependentSoldierTerm(term);

    public string? GetJobImageDefinitionKey(string jobId, Stellaris.Core.Assets.M3AssetManifest manifest)
    {
        return _catalog.Jobs.ContainsKey(jobId)
            ? Stellaris.Core.Assets.M3AssetManifestBuilder.ResolvePlanetImageKey(manifest, "job", jobId) : null;
    }

    private static JobDefinition CompileJob(PlanetEconomyJob job)
    {
        var production = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var upkeep = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var term in job.Terms)
        {
            if (term.Kind is not PlanetEconomyTermKind.ResourceProduction and not PlanetEconomyTermKind.ResourceUpkeep) { continue; }
            var disposition = ResolveStartingOrdinaryDisposition(term.ConditionDisposition, term.Condition);
            if (disposition == PlanetEconomyConditionDisposition.InactiveInOrdinaryContext) { continue; }
            if (disposition == PlanetEconomyConditionDisposition.RequiresRuntimeContext)
            {
                throw new InvalidDataException($"Active job flow requires unsupported source context: {job.Id}/{term.TargetId}@{term.Provenance.RelativePath}:{term.Provenance.Line}.");
            }
            var value = term.Value ?? throw new InvalidDataException($"Job flow has no source decimal: {job.Id}/{term.TargetId}.");
            var target = term.Kind == PlanetEconomyTermKind.ResourceProduction ? production : upkeep;
            target[term.TargetId] = checked(target.GetValueOrDefault(term.TargetId) + value);
        }
        var unsupportedMultiplier = job.Terms.FirstOrDefault(term => term.Kind == PlanetEconomyTermKind.ResourceMultiplier
            && term.ConditionDisposition != PlanetEconomyConditionDisposition.InactiveInOrdinaryContext);
        if (unsupportedMultiplier is not null)
        {
            throw new InvalidDataException($"Job resource multiplier requires source modifier evaluation: {job.Id}/{unsupportedMultiplier.TargetId}@{unsupportedMultiplier.Provenance.RelativePath}:{unsupportedMultiplier.Provenance.Line}.");
        }
        return new JobDefinition(job.Id, job.Id, 100m, ReadOnly(production), ReadOnly(upkeep), job.EconomicCategory);
    }

    private static PlanetEconomyConditionDisposition ResolveStartingOrdinaryDisposition(
        PlanetEconomyConditionDisposition disposition, PlanetEconomyCondition? condition)
    {
        if (disposition == PlanetEconomyConditionDisposition.Unconditional || condition is null) { return disposition; }
        var value = EvaluateStartingCondition(condition);
        return value switch
        {
            true => PlanetEconomyConditionDisposition.ActiveInOrdinaryContext,
            false => PlanetEconomyConditionDisposition.InactiveInOrdinaryContext,
            null => disposition
        };
    }

    private static bool? EvaluateStartingCondition(PlanetEconomyCondition condition)
    {
        if (condition.Key is "AND" or "OR" or "NOT" or "NOR")
        {
            var values = condition.Children.Select(EvaluateStartingCondition).ToArray();
            if (condition.Key == "AND") { return values.Any(value => value == false) ? false : values.Any(value => value is null) ? null : true; }
            if (condition.Key == "OR") { return values.Any(value => value == true) ? true : values.Any(value => value is null) ? null : false; }
            bool? nested = condition.Key == "NOT"
                ? values.Any(value => value == false) ? false : values.Any(value => value is null) ? null : true
                : values.Any(value => value == true) ? true : values.Any(value => value is null) ? null : false;
            return nested is null ? null : !nested.Value;
        }
        if (condition.Key is "owner" or "owner?" or "planet" or "planet?" or "colony" or "colony?" or "hidden_trigger"
            or "capital_scope.solar_system?" or "capital_scope")
        {
            var values = condition.Children.Select(EvaluateStartingCondition).ToArray();
            return values.Any(value => value == false) ? false : values.Any(value => value is null) ? null : true;
        }
        if (condition.Key == "exists" && condition.Value is "owner" or "planet" or "colony" or "solar_system") { return true; }
        if (condition.Key == "has_carrier_flag") { return false; }
        if (condition.Key == "has_country_flag" && condition.Value == "maverick_citizens_flag") { return false; }
        if (condition.Key == "has_modifier" && condition.Value == "slave_colony") { return false; }
        if (condition.Key == "has_knights_zone") { return false; }
        if (condition.Key == "has_valid_civic" && condition.Value is "civic_civil_education" or "civic_warrior_culture" or "civic_agrarian_idyll" or "civic_anglers" or "civic_reanimated_armies") { return false; }
        if (condition.Key == "has_carrier_flag" && condition.Value == "has_produce_requisitorium") { return false; }
        if (condition.Key == "has_modifier" && condition.Value == "eat_the_titans") { return false; }
        if (condition.Key == "has_technology" && condition.Value is "tech_capital_productivity_1" or "tech_capital_productivity_2" or "tech_capital_productivity_3") { return false; }
        if (condition.Key == "has_policy_flag" && condition.Value is "identity_fusion" or "identity_initialization") { return false; }
        if (condition.Key == "has_corporate_government") { return false; }
        if (condition.Key == "is_capital") { return true; }
        if (condition.Key == "has_planet_flag" && condition.Value == "toxic_knights_habitat") { return false; }
        if (condition.Key == "has_origin" && condition.Value == "origin_toxic_knights") { return false; }
        if (condition.Key == "has_active_building" && condition.Value is "building_nanolab_1" or "building_nanolab_2"
            or "xeno_geology_holomuseum" or "aesthetic_wonders_holomuseum" or "galactic_history_holomuseum" or "building_genomic_facility") { return false; }
        if (condition.Key is "has_tradition" or "has_completed_genetic_situation") { return false; }
        if (condition.Key == "is_same_value" && condition.Operator == "=" && condition.Value == "ROOT.solar_system") { return true; }
        if (condition.Operator != "=" || condition.Value is not ("yes" or "no")) { return null; }
        bool? actual = condition.Key switch
        {
            // Этот профиль обслуживает проверенные ordinary continental startup-планы.
            "planet_is_habitat_equivalent" or "planet_is_ecu_equivalent" or "planet_is_ring_world_equivalent" => false,
            "is_tankbound_empire" => false,
            "industrial_jobs_are_catalytic_trigger" => false,
            "is_gestalt" => false,
            "is_individual_machine" => false,
            "is_hive_empire" => false,
            "is_machine_empire" => false,
            "is_worker_coop_empire" => false,
            "enforcer_is_educator_trigger" => false, // Selected civic is absent from all three ordinary templates.
            "engineer_is_nanotech_researcher_trigger" => false, // Source start plans contain no nanolab.
            "entertainer_is_duelist_trigger" => false,
            "entertainer_is_curator_trigger" => false,
            "entertainer_is_storm_dancer_trigger" => false,
            "farmer_is_titan_hunter_trigger" => false,
            "farmer_is_puddle_technician_trigger" => false,
            "farmer_is_angler_trigger" => false,
            "healthcare_is_genomic_researcher_trigger" => false,
            "soldier_is_necromancer" => false,
            "is_imperial_authority" => true,
            "is_democratic_authority" => false,
            "is_cyber_imperial" or "is_synth_imperial" or "is_purity_authority" or "is_cloning_authority"
                or "is_mutation_authority" or "is_transcendent_authority" or "is_corporeal_authority" => false,
            _ => null
        };
        return actual is null ? null : condition.Value == "yes" ? actual : !actual.Value;
    }

    private static PlanetEconomyConditionDisposition ResolveTermDisposition(PlanetEconomyTerm term, Func<PlanetEconomyCondition?, bool?>? evaluator)
        => evaluator is null ? ResolveStartingOrdinaryDisposition(term.ConditionDisposition, term.Condition) : evaluator(term.Condition) switch
        {
            true => PlanetEconomyConditionDisposition.ActiveInOrdinaryContext,
            false => PlanetEconomyConditionDisposition.InactiveInOrdinaryContext,
            _ => PlanetEconomyConditionDisposition.RequiresRuntimeContext
        };

    private static void ApplyDefinition(PlanetEconomyDefinition definition, Dictionary<string, decimal> capacity,
        Dictionary<string, decimal> structuralUpkeep, ref decimal sourceJobOutputMultiplier, ref decimal zoneBuildingSlotsAdd,
        int attachedDistrictLevel = 1, List<Modifier>? categoryModifiers = null, Dictionary<string, decimal>? jobBonuses = null, IEnumerable<string>? modifierResources = null,
        List<StructuralProductionSource>? structuralSources = null, string? structuralSourceId = null,
        Func<PlanetEconomyCondition?, bool?>? conditionEvaluator = null)
    {
        var production = M3StructuralProduction.Resolve(definition, structuralSourceId ?? "catalog/" + definition.Id,
            M3RuntimeOwnerFacts.AscensionPerks, term => ResolveTermDisposition(term, conditionEvaluator));
        foreach (var term in definition.Terms)
        {
            if (IsLeaderDependentSoldierTerm(term)) { continue; }
            if (term.TargetId == "planet_housing_add") { continue; }
            if (term.Kind is not PlanetEconomyTermKind.WorkforceCapacity and not PlanetEconomyTermKind.BuildingUpkeep
                and not PlanetEconomyTermKind.DistrictUpkeep and not PlanetEconomyTermKind.ZoneUpkeep
                and not PlanetEconomyTermKind.ResourceProduction and not PlanetEconomyTermKind.ResourceUpkeep
                and not PlanetEconomyTermKind.WorkforceCapacityMultiplier and not PlanetEconomyTermKind.ResourceMultiplier
                and not PlanetEconomyTermKind.PlanetModifier and not PlanetEconomyTermKind.PlanetModifierMultiplier)
            {
                continue;
            }
            var disposition = ResolveTermDisposition(term, conditionEvaluator);
            if (disposition == PlanetEconomyConditionDisposition.InactiveInOrdinaryContext) { continue; }
            if (disposition == PlanetEconomyConditionDisposition.RequiresRuntimeContext)
            {
                throw new InvalidDataException($"Installed structure term requires unsupported source context: {definition.Id}/{term.TargetId}@{term.Provenance.RelativePath}:{term.Provenance.Line} [{DescribeEvaluated(term.Condition!)}].");
            }
            if (production.Consumed.Contains(term)) { continue; }
            if (term.ApplicationScope == PlanetEconomyApplicationScope.PerAttachedDistrictLevel
                && (definition.Kind != PlanetEconomyDefinitionKind.Zone || attachedDistrictLevel <= 0))
            {
                throw new InvalidDataException($"Source term требует attached district scaling вне zone context: {definition.Id}/{term.TargetId}.");
            }
            var applications = term.ApplicationScope == PlanetEconomyApplicationScope.PerAttachedDistrictLevel ? attachedDistrictLevel : 1;
            if (term.Kind == PlanetEconomyTermKind.WorkforceCapacity)
            {
                var value = term.Value ?? throw new InvalidDataException($"Capacity term has no source decimal: {definition.Id}/{term.TargetId}.");
                capacity[term.TargetId] = checked(capacity.GetValueOrDefault(term.TargetId) + value * applications);
            }
            else if (term.Kind is PlanetEconomyTermKind.BuildingUpkeep or PlanetEconomyTermKind.DistrictUpkeep or PlanetEconomyTermKind.ZoneUpkeep)
            {
                var value = term.Value ?? throw new InvalidDataException($"Upkeep term has no source decimal: {definition.Id}/{term.TargetId}.");
                structuralUpkeep[term.TargetId] = checked(structuralUpkeep.GetValueOrDefault(term.TargetId) + value * applications);
            }
            else if (term.Kind == PlanetEconomyTermKind.ResourceProduction)
            {
                throw new InvalidDataException($"Structural production requires a dedicated source flow channel: {definition.Id}/{term.TargetId}.");
            }
            else if (term.Kind == PlanetEconomyTermKind.PlanetModifier && term.TargetId == "planet_jobs_produces_mult")
            {
                var value = term.Value ?? throw new InvalidDataException($"Source job production modifier has no decimal: {definition.Id}/{term.TargetId}.");
                sourceJobOutputMultiplier = checked(sourceJobOutputMultiplier + value * applications);
            }
            else if (term.Kind == PlanetEconomyTermKind.PlanetModifier && term.TargetId == "zone_building_slots_add")
            {
                var value = term.Value ?? throw new InvalidDataException($"Source zone building slots modifier has no decimal: {definition.Id}/{term.TargetId}.");
                zoneBuildingSlotsAdd = checked(zoneBuildingSlotsAdd + value * applications);
            }
            else if (term.Kind == PlanetEconomyTermKind.PlanetModifier && ConstructionJobEconomyMapping.Find(term.TargetId) is { } target)
            {
                var value = term.Value ?? throw new InvalidDataException($"Source job modifier has no decimal: {definition.Id}/{term.TargetId}.");
                if (categoryModifiers is not null)
                {
                    var resources = target.Resource is null ? modifierResources ?? throw new InvalidDataException("Job modifier resource closure is missing.") : new[] { target.Resource };
                    var scaled = checked(value * applications);
                    foreach (var resource in resources)
                    { categoryModifiers.Add(new(target.Category, resource, target.Operation, target.Add ? scaled : 0, target.Add ? 0 : scaled)); }
                }
            }
            else if (term.Kind == PlanetEconomyTermKind.PlanetModifier && ConstructionJobWorkforceMapping.Find(term.TargetId) is { } jobs)
            {
                var value = term.Value ?? throw new InvalidDataException($"Source job Workforce modifier has no decimal: {definition.Id}/{term.TargetId}.");
                if (value < 0) { throw new InvalidDataException($"Negative source job Workforce modifier: {definition.Id}/{term.TargetId}."); }
                if (jobBonuses is not null)
                {
                    foreach (var job in jobs) { jobBonuses[job] = checked(jobBonuses.GetValueOrDefault(job) + value * applications); }
                }
            }
            else if (term.Kind is PlanetEconomyTermKind.WorkforceCapacityMultiplier or PlanetEconomyTermKind.ResourceMultiplier or PlanetEconomyTermKind.PlanetModifierMultiplier)
            {
                throw new InvalidDataException($"Modifier requires source context evaluation: {definition.Id}/{term.TargetId}@{term.Provenance.RelativePath}:{term.Provenance.Line}.");
            }
        }
        if (production.Source is not null && structuralSources is not null)
        {
            if (structuralSourceId is null) { throw new InvalidDataException("Structural source instance is unavailable."); }
            structuralSources.Add(production.Source);
        }
    }

    private IReadOnlyDictionary<string, decimal> RuntimeJobBonuses(IReadOnlyDictionary<string, decimal> bonuses) =>
        bonuses.Where(pair => _jobs.Any(job => job.Id == pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);


    private static PlanetEconomyDefinition GetDefinition(IReadOnlyDictionary<string, PlanetEconomyDefinition> definitions, string id) =>
        definitions.TryGetValue(id, out var definition) ? definition : throw new InvalidDataException($"Installed startup definition missing from baseline economy catalog: {id}.");

    private static bool HasVerifiedNoRecurringEconomyProof(StartupBuildingPlan plan, string zoneId)
    {
        var proofs = plan.ZoneRecurringEconomyProofs.Where(proof => proof.ZoneId == zoneId).ToArray();
        if (proofs.Length != 1) { return false; }
        var proof = proofs[0];
        return plan.Sources.Any(source => source.RelativePath == proof.DefinitionPath && source.Sha256 == proof.DefinitionSha256)
            && plan.Sources.Any(source => source.RelativePath == proof.InlinePath && source.Sha256 == proof.InlineSha256);
    }

    private static IReadOnlyDictionary<string, decimal> ReadOnly(Dictionary<string, decimal> values) =>
        new ReadOnlyDictionary<string, decimal>(values);

    private static string DescribeEvaluated(PlanetEconomyCondition condition) => condition.Key + (condition.Value is null ? "" : "=" + condition.Value)
        + "?" + (EvaluateStartingCondition(condition)?.ToString() ?? "?")
        + (condition.Children.Count == 0 ? "" : "(" + string.Join(",", condition.Children.Select(DescribeEvaluated)) + ")");
}

public sealed record M3BaselineSourceResource(string Id, string StoragePolicy, decimal? SourceMaximum);
public sealed record M3BaselineCoverageOmission(string DefinitionId, string TargetId, string TermKind, string? SourceKey,
    string? SourceExpression, string SourcePath, int SourceLine, string Reason);

using System.Collections.Frozen;
using System.Globalization;
using Stellaris.Core.Construction;
using Stellaris.Core.Content.Construction;
using Stellaris.Core.Content.PlanetEconomy;
using Stellaris.Core.Economy;

namespace Stellaris.Session.M3.Construction;

public sealed class M3ConstructionContent
{
    private readonly string[] _buildingCountIds;
    /// <summary>Отдельная доверенная привязка optional from к действующему actor страны; не выводится из authority.</summary>
    public sealed record FromBinding(string PolicyId, bool IsNomadic)
    {
        public static FromBinding ActingCountry(bool isNomadic) => new("m3-authenticated-acting-country-v1", isNomadic);
    }
    public ConstructionLiveContent Content { get; }
    public ConstructionCatalogProjection Zones { get; }
    public ConstructionPlacementProjection Placement { get; }
    public ConstructionCatalogProjection Civic { get; }
    public ConstructionCatalogProjection DistrictCapacityConditions { get; }
    public IReadOnlyDictionary<string, string> RuntimeGates { get; }
    public IReadOnlyDictionary<string, string> RuntimeGateEvidence { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<string>> UpgradePredecessors { get; }
    public IReadOnlyList<string> FixedCityZones { get; }

    private M3ConstructionContent(ConstructionLiveContent content, ConstructionCatalogProjection zones, ConstructionPlacementProjection placement, ConstructionCatalogProjection civic, ConstructionCatalogProjection districtCapacityConditions)
    {
        Content = content; Zones = zones; Placement = placement; Civic = civic; DistrictCapacityConditions = districtCapacityConditions;
        static IEnumerable<ConstructionField> Flatten(IEnumerable<ConstructionField> fields)
            => fields.SelectMany(field => new[] { field }.Concat(Flatten(field.Children)));
        _buildingCountIds = content.ContentIds().Concat(content.Catalogs.Values.Distinct().Append(content.Conditions)
            .SelectMany(catalog => catalog.Definitions.Values).SelectMany(definition => Flatten(definition.Fields))
            .Where(field => field.Key == "has_building" && field.Operator == "=" && field.Children.Count == 0
                && field.Scalar is { Length: > 0 and <= 160 } id && id.All(character => char.IsAsciiLetterOrDigit(character) || character == '_'))
            .Select(field => field.Scalar!)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (content.Catalogs.Values.Distinct().Append(content.Conditions).Append(districtCapacityConditions).SelectMany(catalog => catalog.Files)
            .GroupBy(file => file.RelativePath, StringComparer.Ordinal).Any(group => group.Select(file => (file.ByteLength, file.Sha256)).Distinct().Count() != 1))
        { throw new ArgumentException("District capacity source snapshot mismatch."); }
        FixedCityZones = Array.AsReadOnly(content.Economy.Zones.Keys.Where(id => M3ZoneGraph.HasFixedCitySlots(content, id)).Order(StringComparer.Ordinal).ToArray());
        RuntimeGates = content.Economy.Buildings.Values.Concat(content.Economy.Zones.Values)
            .Select(definition => (definition.Id, Gate: M3BaselineEconomyProfile.DefinitionGate(content.Economy, definition)))
            .Where(item => item.Gate is not null).ToFrozenDictionary(item => item.Id, item => item.Gate!, StringComparer.Ordinal);
        RuntimeGateEvidence = content.Economy.Buildings.Values.Concat(content.Economy.Zones.Values)
            .Select(definition => (definition.Id, Gate: M3BaselineEconomyProfile.DefinitionGate(content.Economy, definition, includeEvidence: true)))
            .Where(item => item.Gate is not null).ToFrozenDictionary(item => item.Id, item => item.Gate!, StringComparer.Ordinal);
        UpgradePredecessors = content.Catalogs.Where(pair => pair.Value.Definitions[pair.Key].Kind == "building")
            .SelectMany(pair => pair.Value.Definitions[pair.Key].Fields.Where(field => field.Key == "upgrades")
                .SelectMany(field => field.Children).Select(child => (Target: child.Scalar ?? child.Key, Source: pair.Key)))
            .GroupBy(item => item.Target, StringComparer.Ordinal).ToFrozenDictionary(group => group.Key,
                group => (IReadOnlyList<string>)Array.AsReadOnly(group.Select(item => item.Source).Order(StringComparer.Ordinal).ToArray()), StringComparer.Ordinal);
    }

    public static async Task<M3ConstructionContent> ImportAsync(string root, PlanetEconomyCatalog economy, CancellationToken cancellationToken = default)
    {
        var importer = new ConstructionDefinitionImporter();
        var catalogs = new List<ConstructionCatalogProjection>();
        var full = await importer.ImportFullCatalogAsync(root, cancellationToken);
        catalogs.Add(full);
        catalogs.Add(await importer.ImportCityCatalogAsync(root, cancellationToken));
        catalogs.Add(await importer.ImportRuralCatalogAsync(root, cancellationToken));
        return new(new(catalogs, economy, await importer.ImportCatalogConditionsAsync(root, full, cancellationToken)),
            await importer.ImportZoneCatalogAsync(root, cancellationToken), await importer.ImportPlacementAsync(root, cancellationToken), await importer.ImportFunctionalCivicCatalogAsync(root, cancellationToken),
            await importer.ImportDistrictCapacityCatalogAsync(root, cancellationToken));
    }

    public Func<PlanetEconomyCondition?, bool?>? EconomyConditionEvaluator(string id, ConstructionContext context)
    {
        var definition = Content.Economy.Buildings.GetValueOrDefault(id) ?? Content.Economy.Zones.GetValueOrDefault(id)
            ?? Content.Economy.Districts.GetValueOrDefault(id);
        return definition is null ? null : M3ConstructionEconomyConditions.Create(definition, context, Content.Conditions);
    }

    public string? EvaluateRuntimeGate(string id, ConstructionContext context, bool includeEvidence = false)
    {
        if (!Content.Economy.Buildings.ContainsKey(id) && !Content.Economy.Zones.ContainsKey(id) && !Content.Economy.Districts.ContainsKey(id))
            return "definition-execution-unresolved";
        try { _ = ResourceSilo(id, context); }
        catch (InvalidDataException exception) { return exception.Message; }
        var resolver = EconomyConditionEvaluator(id, context);
        if (resolver is null) { return (includeEvidence ? RuntimeGateEvidence : RuntimeGates).GetValueOrDefault(id); }
        return M3BaselineEconomyProfile.DefinitionGate(Content.Economy, Content.Economy.Buildings[id], includeEvidence, resolver);
    }

    internal M3ResourceSilo? ResourceSilo(string id, ConstructionContext context) => Content.Economy.Buildings.TryGetValue(id, out var definition)
        ? M3ResourceSilo.Resolve(definition, context, Content.Conditions) : null;

    public (decimal Cost, decimal Speed) PresetModifiers(ConstructionOrdinaryTemplate template, ConstructionKind kind)
    {
        if (!template.Civics.Contains("civic_functional_architecture")) { return (0, 0); }
        decimal Value(string key) => Civic.Parameters.Single(parameter => parameter.DefinitionId == "civic_functional_architecture" && parameter.FieldPath.EndsWith(":" + key, StringComparison.Ordinal)).Value;
        return (Value("planet_structures_cost_mult"), kind == ConstructionKind.Building ? Value("planet_building_build_speed_mult") : 0);
    }

    public ConstructionLiveRuntime Create(M3StartupColony startup, IReadOnlyDictionary<string, ConstructionInstalled>? resolvedStartupContributions = null)
    {
        if (startup.Plan.Gates.Count != 0 || startup.Plan.Context.Authority != Core.Content.Construction.Startup.StartupAuthority.Regular || startup.Plan.Context.IsNomadic) { throw new ArgumentException("Unsupported startup construction context."); }
        RequirePreset(startup);
        var slots = new List<ConstructionLiveSlot>();
        foreach (var zone in startup.Zones)
        {
            decimal count = 3;
            if (!FixedCityZones.Contains(zone.ZoneId))
            {
                if (!Zones.Definitions.TryGetValue(zone.ZoneId, out var source)) { continue; }
                var max = Zones.Parameters.Where(parameter => parameter.DefinitionId == zone.ZoneId && parameter.FieldPath.EndsWith(":max_buildings", StringComparison.Ordinal)).Select(parameter => parameter.Value).ToArray();
                // В zones без max source modifier задаёт число мест; неподдержанные scaling terms не теряем.
                var additions = Zones.Parameters.Where(parameter => parameter.DefinitionId == zone.ZoneId && parameter.FieldPath.Contains(":planet_modifier/", StringComparison.Ordinal) && parameter.FieldPath.EndsWith(":zone_building_slots_add", StringComparison.Ordinal)).Select(parameter => parameter.Value).ToArray();
                if (source.Fields.Any(field => field.Key == "max_buildings_planet_class")) { continue; }
                count = max.Length == 1 ? max[0] : max.Length == 0 && additions.Length == 1 ? additions[0] : -1;
            }
            if (count < 0 || count > 4096 || decimal.Truncate(count) != count) { continue; }
            for (var index = 0; index < count; index++) { slots.Add(new(zone.InstanceId + "/building-slot/" + index.ToString(CultureInfo.InvariantCulture), ConstructionKind.Building, zone.InstanceId, zone.ZoneId)); }
        }
        var installed = new List<ConstructionInstalled>();
        var startupBuildingBySlot = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var building in startup.Buildings.OrderBy(building => building.Order))
        {
            var targets = building.SlotInstanceId is not null
                ? startup.Zones.Where(zone => zone.SlotInstanceId == building.SlotInstanceId).Select(zone => zone.InstanceId).ToHashSet(StringComparer.Ordinal)
                : building.CompatibleZoneInstanceIds.ToHashSet(StringComparer.Ordinal);
            // Каноническая серверная occurrence-размещение: source order, первое совместимое свободное место.
            var target = slots.FirstOrDefault(slot => targets.Contains(slot.ZoneInstanceId!) && !installed.Any(item => item.SlotId == slot.Id));
            if (target is null) { throw new ArgumentException("Startup building placement/capacity unresolved: " + building.DefinitionId); }
            var recipe = Content.Economy.Buildings[building.DefinitionId];
            installed.Add(Contribution(building.InstanceId, target.Id, recipe, ConstructionKind.Building, PlanetEconomyTermKind.BuildingUpkeep));
            startupBuildingBySlot.Add(target.Id, building.InstanceId);
        }
        var capacity = checked(startup.Plan.FinalPlanetSize + startup.Plan.Context.DistrictCapacityModifier + startup.Plan.DistrictCaps.Where(cap => cap.ModifierId == "planet_max_districts_add").Sum(cap => cap.Value));
        var used = startup.Districts.Sum(district => district.Level);
        if (capacity < used || capacity > 4096) { throw new ArgumentException("Invalid source district capacity."); }
        for (var index = 0; index < capacity; index++) { slots.Add(new(startup.ColonyId + "/district-slot/" + index.ToString(CultureInfo.InvariantCulture), ConstructionKind.District, null, null)); }
        var position = 0;
        foreach (var district in startup.Districts)
        {
            var recipe = Content.Economy.Districts[district.DefinitionId];
            for (var level = 0; level < district.Level; level++)
            { installed.Add(Contribution(district.InstanceId, startup.ColonyId + "/district-slot/" + position++, recipe, ConstructionKind.District, PlanetEconomyTermKind.DistrictUpkeep)); }
        }
        foreach (var zone in startup.Zones)
        {
            var binding = startup.Slots.Single(slot => slot.InstanceId == zone.SlotInstanceId);
            if (binding.SlotType is not ("slot_city_01" or "slot_city_02")) continue;
            if (!M3ZoneGraph.Fits(this, zone.ZoneId, binding.SlotType)) throw new InvalidDataException("Startup urban zone set mismatch.");
            slots.Add(new(zone.SlotInstanceId, ConstructionKind.Zone, zone.InstanceId, null, zone.DistrictInstanceId, binding.SlotType));
            var level = startup.Districts.Single(district => district.InstanceId == zone.DistrictInstanceId).Level;
            var facts = EconomyFacts(startup);
            installed.Add(new(zone.SlotInstanceId, zone.ZoneId, ConstructionKind.Zone,
                M3ZoneGraph.Contributions(this, zone.ZoneId, level, PlanetEconomyTermKind.ZoneUpkeep, facts),
                M3ZoneGraph.Contributions(this, zone.ZoneId, level, PlanetEconomyTermKind.WorkforceCapacity, facts)));
        }
        return M3ZoneGraph.Materialize(this, startup, new(startup.ColonyId, startup.OwnerParticipantId, slots, installed, startupBuildingBySlot: startupBuildingBySlot));

        ConstructionInstalled Contribution(string instanceId, string slotId, PlanetEconomyDefinition recipe, ConstructionKind kind, PlanetEconomyTermKind upkeepKind)
        {
            if (resolvedStartupContributions is not null && resolvedStartupContributions.TryGetValue(instanceId, out var resolved))
            {
                if (resolved.DefinitionId != recipe.Id || resolved.Kind != kind) { throw new ArgumentException("Resolved startup contribution identity mismatch."); }
                return new(slotId, recipe.Id, kind, resolved.InfrastructureUpkeep, resolved.JobCapacity);
            }
            return new(slotId, recipe.Id, kind, Terms(recipe, upkeepKind, startup), Terms(recipe, PlanetEconomyTermKind.WorkforceCapacity, startup));
        }
    }

    public ConstructionContext Context(M3StartupColony startup, ConstructionOrdinaryTemplate template, ConstructionLiveRuntime runtime, FromBinding? fromBinding = null,
        IReadOnlySet<string>? researchedTechnologies = null, decimal? sapientPopulation = null, bool? usesConsumerGoods = null,
        IReadOnlyList<PopulationSpeciesSnapshot>? ownerSpecies = null, EntityId? ownerFounderSpeciesId = null, EntityId? ownerCountryEntityId = null,
        PlanetForeignOwnershipSnapshot? foreignOwnership = null, EntityId? planetEntityId = null)
    {
        if (fromBinding is not null && fromBinding.PolicyId != "m3-authenticated-acting-country-v1") { throw new ArgumentException("Unknown optional-from binding policy."); }
        var facts = Enum.GetValues<ConstructionOwnerPredicate>().ToDictionary(predicate => predicate, predicate => (bool?)(predicate is ConstructionOwnerPredicate.Regular or ConstructionOwnerPredicate.UsesFood));
        facts[ConstructionOwnerPredicate.UsesConsumerGoods] = usesConsumerGoods;
        var capitals = runtime.Installed.Values.Where(item => item.Kind == ConstructionKind.Building)
            .Select(item => Content.Catalogs[item.DefinitionId].Definitions[item.DefinitionId].Fields)
            .Where(fields => fields.Any(field => field.Key == "capital" && field.Operator == "=" && field.Scalar == "yes")).ToArray();
        var tiers = capitals.Length == 1 ? capitals[0].Where(field => field.Key == "capital_tier").ToArray() : [];
        int? capitalTier = tiers is [var tierField] && tierField.Operator == "=" && tierField.Children.Count == 0
            && int.TryParse(tierField.Scalar, NumberStyles.None, CultureInfo.InvariantCulture, out var tier) && tier is >= 0 and <= 4096 ? tier : null;
        var planetFacts = Enum.GetValues<ConstructionPlanetPredicate>().ToDictionary(predicate => predicate, predicate => (bool?)(predicate == ConstructionPlanetPredicate.Capital));
        planetFacts[ConstructionPlanetPredicate.UpgradedCapital] = capitalTier is { } actualTier ? actualTier >= 2 : null;
        var counts = _buildingCountIds.ToDictionary(id => id, id => (decimal)runtime.Installed.Values.Count(item => item.DefinitionId == id), StringComparer.Ordinal);
        var districtCounts = runtime.Installed.Values.Where(item => item.Kind == ConstructionKind.District)
            .GroupBy(item => item.DefinitionId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (decimal)group.Count(), StringComparer.Ordinal);
        // M3 пока не хранит полученные традиции и ascension perks; передаём известные пустые snapshots.
        // Это разрешает соответствующие условия как отсутствующие, но не выдаёт perks/традиции и не моделирует effects.
        // Частичный authority snapshot совпадает с native has_authority фактами ResearchFactsFor.
        var authorityPredicates = new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["auth_corporate"] = template.AuthorityId == "auth_corporate",
            ["auth_machine_intelligence"] = template.AuthorityId == "auth_machine_intelligence"
        };
        return new(true, facts, fromBinding?.IsNomadic, startup.Homeworld.PlanetClass, [], [], researchedTechnologies ?? template.StartingTechnologies, counts,
            new("standard", "default", template.Civics, [], [], startup.Features.Where(feature => !feature.IsHidden).Select(feature => feature.DefinitionId), planetFacts, template.Civics, [], sapientPopulation: sapientPopulation, ownerEthics: startup.Plan.Context.Ethics, ownerFederationExists: false, ownerOrigin: startup.Plan.Context.Origin, ownerAscensionPerks: M3RuntimeOwnerFacts.AscensionPerks, installedZones: M3ZoneGraph.Current(startup, runtime).Select(zone => zone.ZoneId), ownerAuthorityPredicates: authorityPredicates, ownerHeldTraditions: M3RuntimeOwnerFacts.HeldTraditions, ownerGalacticCommunityMember: M3RuntimeOwnerFacts.OwnerGalacticCommunityMember, ownerSpecies: ownerSpecies, ownerFounderSpeciesId: ownerFounderSpeciesId, ownerCountryEntityId: ownerCountryEntityId), capitalTier: capitalTier,
            foreignOwnership: foreignOwnership, expectedPlanet: planetEntityId, expectedPlanetOwner: ownerCountryEntityId,
            districtCounts: districtCounts);
    }

    public static IReadOnlyDictionary<string, bool> EconomyFacts(M3StartupColony startup)
    {
        RequirePreset(startup);
        var context = startup.Plan.Context;
        var facts = new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["planet:exists:owner"] = true,
            ["planet:is_capital"] = true,
            ["planet:planet_is_habitat_equivalent"] = false,
            ["planet:planet_is_ecu_equivalent"] = false,
            ["planet:planet_is_ring_world_equivalent"] = false,
            ["planet:has_modifier:slave_colony"] = false,
            ["planet:has_knights_zone"] = startup.Zones.Any(zone => zone.ZoneId is "zone_habitat_knights" or "zone_ark_knights"),
            ["owner:is_gestalt"] = context.Authority != Core.Content.Construction.Startup.StartupAuthority.Regular,
            ["owner:is_regular_empire"] = context.Authority == Core.Content.Construction.Startup.StartupAuthority.Regular,
            ["owner:is_hive_empire"] = context.Authority == Core.Content.Construction.Startup.StartupAuthority.Hive,
            ["owner:is_machine_empire"] = context.Authority == Core.Content.Construction.Startup.StartupAuthority.Machine,
            ["owner:is_individual_machine"] = context.IsIndividualMachine,
            ["owner:is_wilderness_empire"] = context.IsWilderness,
            ["owner:is_tankbound_empire"] = context.Civics.Contains("civic_tankbound") || context.Civics.Contains("civic_tankbound_corporate"),
            ["owner:is_worker_coop_empire"] = context.Civics.Contains("civic_worker_coop"),
            ["owner:is_cyber_imperial"] = false,
            ["owner:has_corporate_government"] = false,
            ["owner:has_tradition:tr_domination_imperious_architecture"] = false,
            ["owner:has_origin:origin_toxic_knights"] = context.Origin == "origin_toxic_knights"
        };
        return facts.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static void RequirePreset(M3StartupColony startup)
    {
        var context = startup.Plan.Context;
        if (context.Authority != Core.Content.Construction.Startup.StartupAuthority.Regular || context.IsIndividualMachine || context.IsNomadic || context.IsWilderness || context.Origin != "origin_default"
            || !new[] { "economic", "scientific", "military" }.Select(ConstructionOrdinaryTemplate.Get).Any(template => template.Ethics.SetEquals(context.Ethics) && template.Civics.SetEquals(context.Civics)))
        { throw new ArgumentException("Construction preset facts are unavailable for this startup configuration."); }
    }

    private static IReadOnlyDictionary<string, decimal> Terms(PlanetEconomyDefinition recipe, PlanetEconomyTermKind kind, M3StartupColony startup)
    {
        var facts = EconomyFacts(startup);
        if (kind == PlanetEconomyTermKind.WorkforceCapacity && recipe.Terms.Any(term => term.Kind == PlanetEconomyTermKind.WorkforceCapacityMultiplier && term.ConditionDisposition != PlanetEconomyConditionDisposition.InactiveInOrdinaryContext && ConstructionLiveConditions.Evaluate(term.Condition, facts) != false))
        { throw new ArgumentException("Installed capacity multiplier requires resolved economy graph: " + recipe.Id); }
        var terms = recipe.Terms.Where(term => term.Kind == kind && term.ConditionDisposition != PlanetEconomyConditionDisposition.InactiveInOrdinaryContext)
            .Where(term => term.ConditionDisposition != PlanetEconomyConditionDisposition.RequiresRuntimeContext || ConstructionLiveConditions.Evaluate(term.Condition, facts) != false).ToArray();
        if (terms.Any(term => term.Value is null || term.ConditionDisposition == PlanetEconomyConditionDisposition.RequiresRuntimeContext && ConstructionLiveConditions.Evaluate(term.Condition, facts) is null)) { throw new ArgumentException("Installed economy contribution unresolved: " + recipe.Id + " " + kind); }
        return terms.GroupBy(term => term.TargetId, StringComparer.Ordinal).ToFrozenDictionary(group => group.Key, group => group.Sum(term => term.Value!.Value), StringComparer.Ordinal);
    }
}

internal static class M3ConstructionContentIds
{
    public static IEnumerable<string> ContentIds(this ConstructionLiveContent content) => content.Catalogs.Keys
        .Where(id => content.Catalogs[id].Definitions[id].Kind == "building");
}

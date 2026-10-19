using System.Collections.Frozen;
using Stellaris.Core.Content.Construction;
using Stellaris.Core.Content.PlanetEconomy;

namespace Stellaris.Core.Construction;

public sealed record ConstructionLiveQuoteResult(ConstructionQuote? Quote, string Code);

/// <summary>Серверный source-каталог; контекст и числовые modifiers передаёт доверенный владелец мира.</summary>
public sealed class ConstructionLiveContent
{
    public const string PROFILE = "planet-construction-live-452-v1";
    public IReadOnlyDictionary<string, ConstructionCatalogProjection> Catalogs { get; }
    public PlanetEconomyCatalog Economy { get; }
    public ConstructionCatalogProjection Conditions { get; }

    public ConstructionLiveContent(IEnumerable<ConstructionCatalogProjection> catalogs, PlanetEconomyCatalog economy, ConstructionCatalogProjection conditions)
    {
        Economy = economy;
        Conditions = conditions;
        Catalogs = catalogs.SelectMany(catalog => catalog.Definitions.Values.Where(definition => definition.Kind is "building" or "district" or "zone").Select(definition => (definition.Id, Catalog: catalog)))
            .ToFrozenDictionary(pair => pair.Id, pair => pair.Catalog, StringComparer.Ordinal);
        var files = Catalogs.Values.Distinct().Append(conditions).SelectMany(catalog => catalog.Files).Select(file => (file.RelativePath, file.ByteLength, file.Sha256))
            .Concat(economy.Sources.Select(file => (file.RelativePath, file.ByteLength, file.Sha256)));
        if (files.GroupBy(file => file.RelativePath).Any(group => group.Distinct().Count() != 1)) { throw new ArgumentException("Source snapshot mismatch."); }
    }

    public ConstructionLiveQuoteResult Quote(string id, ConstructionContext context, decimal? costFraction, decimal? speedFraction, IReadOnlyDictionary<string, bool>? economyFacts = null, string? upgradeFrom = null,
        Func<PlanetEconomyCondition?, bool?>? economyConditionEvaluator = null, Func<ConstructionField, bool>? consumedCountryModifier = null)
    {
        if (!Catalogs.TryGetValue(id, out var catalog)) { return new(null, "definition-unavailable"); }
        var definition = catalog.Definitions[id];
        var eligibility = ConstructionCatalogAssessor.Eligibility(catalog, id, context, Conditions);
        if (eligibility.Truth != ConstructionTruth.True) return new(null, eligibility.Truth == ConstructionTruth.False ? "not-eligible" : "eligibility-unresolved");
        if (!(definition.Kind switch { "building" => Economy.Buildings, "district" => Economy.Districts, _ => Economy.Zones }).ContainsKey(id))
            return new(null, "economy-definition-unavailable");
        var assessment = ConstructionCatalogAssessor.Assess(catalog, id, context, Conditions);
        var fields = definition.Fields;
        if (upgradeFrom is not null && (definition.Kind != "building" || !Economy.Buildings.TryGetValue(upgradeFrom, out var previous)
            || !previous.SourceProperties.GetValueOrDefault("upgrades", []).Contains(id))) { return new(null, "upgrade-edge-unavailable"); }
        if (upgradeFrom is null && fields.Any(field => field.Key == "can_build" && field.Scalar == "no")) { return new(null, "source-can-build-no"); }
        if (fields.Any(field => (field.Key == "country_modifier" || field.Key is "triggered_country_modifier" or "triggered_planet_pop_group_modifier_for_species"
            && (economyFacts is null || field.Children.Where(child => child.Key == "potential").ToArray() is not [var potential]
                || ConstructionLiveConditions.EvaluateScopeModifierPotential(potential, context, economyFacts) != false))
            && (field.Key is not ("country_modifier" or "triggered_country_modifier") || consumedCountryModifier?.Invoke(field) != true)))
        { return new(null, "definition-economy-scope-unresolved"); }
        if (fields.Any(field => field.Key == "destroy_trigger") && assessment.Destroy.Truth != ConstructionTruth.False) { return new(null, assessment.Destroy.Truth == ConstructionTruth.True ? "not-eligible" : "destroy-condition-unresolved"); }
        var requirements = (definition.Kind == "zone" && !fields.Any(field => field.Key == "potential")
                ? new[] { assessment.Prerequisites } : new[] { ConstructionCatalogAssessor.Visibility(catalog, id, context, Conditions), assessment.Prerequisites })
            .Concat(fields.Any(field => field.Key == "unlock") ? new[] { assessment.Unlock } : [])
            .Concat(fields.Any(field => field.Key == "allow") ? new[] { assessment.Allow } : []);
        if (requirements.Any(condition => condition.Truth == ConstructionTruth.False)) { return new(null, "not-eligible"); }
        if (requirements.Any(condition => condition.Truth == ConstructionTruth.Unknown)) { return new(null, "eligibility-unresolved"); }
        var unsupportedEffect = assessment.Contributions.Where(contribution => contribution.Condition.Truth != ConstructionTruth.False
            && contribution.Kind is "planet_modifier" or "triggered_planet_modifier" or "district_planet_modifier" or "triggered_district_planet_modifier")
            .SelectMany(contribution => contribution.Gates)
            .FirstOrDefault(gate => gate.DependencyId.StartsWith("catalog-modifier-", StringComparison.Ordinal));
        if (unsupportedEffect is not null) { return new(null, "source-effect-unresolved:" + unsupportedEffect.DependencyId); }
        var limits = fields.Where(field => field.Key == "planet_limit").ToArray();
        int? planetLimit = limits.Length == 0 ? null : limits is [var limit] && limit.Operator == "=" && limit.Scalar == "1" && definition.Kind == "building" ? 1 : null;
        if (limits.Length != 0 && planetLimit is null || fields.Any(field => field.Key is "empire_limit" or "on_built" or "on_queued" or "on_unqueued")) { return new(null, "definition-lifecycle-unresolved"); }
        if (planetLimit is { } cap)
        {
            if (context.BuildingCounts is null || !context.BuildingCounts.TryGetValue(id, out var installed)) { return new(null, "planet-limit-context-unresolved"); }
            if (installed >= cap) { return new(null, "planet-limit-reached"); }
        }
        if (costFraction is null || speedFraction is null) { return new(null, "numerical-modifiers-unresolved"); }
        if (costFraction < -1 || speedFraction <= -1) { return new(null, "invalid-modifier-factor"); }
        var numeric = assessment.Contributions.Where(contribution => contribution.Kind is "cost" or "buildtime").ToArray();
        if (numeric.Any(contribution => contribution.Condition.Truth == ConstructionTruth.Unknown || contribution.BlockingGates.Count != 0)) { return new(null, "source-numerical-unresolved"); }
        var active = numeric.Where(contribution => contribution.Condition.Truth == ConstructionTruth.True).ToArray();
        var times = active.Where(contribution => contribution.Kind == "buildtime").SelectMany(contribution => contribution.Amounts).ToArray();
        var costs = active.Where(contribution => contribution.Kind == "cost").ToArray();
        if (times.Length != 1 || costs.Length == 0) { return new(null, "source-quote-shape-unresolved"); }
        var economyDefinitions = definition.Kind switch { "building" => Economy.Buildings, "district" => Economy.Districts, _ => Economy.Zones };
        if (!economyDefinitions.TryGetValue(id, out var recipe)) { return new(null, "economy-definition-unavailable"); }
        bool? Resolve(PlanetEconomyTerm term) => economyConditionEvaluator is not null ? economyConditionEvaluator(term.Condition)
            : term.ConditionDisposition == PlanetEconomyConditionDisposition.InactiveInOrdinaryContext ? false
            : term.ConditionDisposition != PlanetEconomyConditionDisposition.RequiresRuntimeContext ? true
            : economyFacts is null ? null : ConstructionLiveConditions.Evaluate(term.Condition, economyFacts);
        var terms = recipe.Terms.Where(term => term.Kind is PlanetEconomyTermKind.WorkforceCapacity or PlanetEconomyTermKind.WorkforceCapacityMultiplier or PlanetEconomyTermKind.BuildingUpkeep or PlanetEconomyTermKind.DistrictUpkeep or PlanetEconomyTermKind.ZoneUpkeep)
            .Where(term => !ConstructionEconomyTermPolicy.IsLeaderDependentSoldierTerm(term))
            .Where(term => Resolve(term) != false).ToArray();
        if (terms.Any(term => term.Value is null || Resolve(term) is null || term.Kind == PlanetEconomyTermKind.WorkforceCapacityMultiplier)) { return new(null, "economy-contribution-unresolved"); }
        try
        {
            var factor = checked(1 + speedFraction.Value);
            if (factor <= 0) { return new(null, "invalid-modifier-factor"); }
            // Quote публикует длительность; непредставимое отношение не должно ломать весь snapshot.
            _ = checked(times[0].Value / factor);
            var price = costs.SelectMany(cost => cost.Amounts.Select(amount => (amount.Resource,
                Value: checked(amount.Value * (cost.RawMultiplier ?? 1) * (1 + costFraction.Value)))))
                .GroupBy(amount => amount.Resource, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Sum(amount => amount.Value), StringComparer.Ordinal);
            var upkeep = Sum(terms.Where(term => term.Kind is PlanetEconomyTermKind.BuildingUpkeep or PlanetEconomyTermKind.DistrictUpkeep or PlanetEconomyTermKind.ZoneUpkeep));
            var jobs = Sum(terms.Where(term => term.Kind == PlanetEconomyTermKind.WorkforceCapacity));
            return new(new(PROFILE, upgradeFrom is null ? "accepted-daily-work-v1" : "human-building-upgrade-2026-10-13-v1", id,
                definition.Kind switch { "building" => ConstructionKind.Building, "district" => ConstructionKind.District, _ => ConstructionKind.Zone }, times[0].Value, price, upkeep, jobs, [], planetLimit), "available");
        }
        catch (OverflowException) { return new(null, "arithmetic-overflow"); }
    }

    private static IReadOnlyDictionary<string, decimal> Sum(IEnumerable<PlanetEconomyTerm> terms) => terms.GroupBy(term => term.TargetId, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Sum(term => term.Value!.Value), StringComparer.Ordinal);

    public bool IsUpgrade(string from, string target) => Catalogs.TryGetValue(from, out var source)
        && source.Definitions[from].Kind == "building"
        && source.Definitions[from].Fields.Where(field => field.Key == "upgrades" && field.Operator == "=" && field.Scalar is null)
            .SelectMany(field => field.Children).Any(field => field.Operator == "" && field.Scalar == target);

    public bool BuildingFits(string id, string zoneId, ConstructionPlacementProjection placement)
    {
        if (!Catalogs.TryGetValue(id, out var source) || !placement.Definitions.TryGetValue(zoneId, out var zone)) { return false; }
        var fields = source.Definitions[id].Fields;
        var sets = fields.Where(field => field.Key == "building_sets").SelectMany(field => field.Children).Select(field => field.Scalar).ToHashSet(StringComparer.Ordinal);
        string[] List(string key) => zone.Fields.Where(field => field.Key == key).SelectMany(field => field.Children).Select(field => field.Scalar ?? "").ToArray();
        var include = List("include");
        return (include.Contains("all") || include.Contains(id) || List("included_building_sets").Any(sets.Contains))
            && !List("exclude").Contains(id) && !List("excluded_building_sets").Any(sets.Contains);
    }
}

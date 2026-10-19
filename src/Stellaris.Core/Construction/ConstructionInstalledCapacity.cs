using System.Collections.ObjectModel;
using System.Numerics;
using Stellaris.Core.Content.Construction;
using Stellaris.Core.Economy;

namespace Stellaris.Core.Construction;

public sealed record ConstructionInstalledCapacityObject(string InstanceId, ConstructionCatalogAssessment Assessment, string? DistrictId = null, int? DistrictLevel = null);

public sealed class ConstructionInstalledCapacitySnapshot
{
    public string ColonyId { get; }
    public long AggregateRevision { get; }
    public IReadOnlyList<ConstructionInstalledCapacityObject> Objects { get; }
    public IReadOnlyList<ConstructionRawContribution> ActiveContributions { get; }
    public IReadOnlyDictionary<string, decimal> JobCapacity { get; }
    public decimal Housing { get; }
    public decimal ZoneBuildingSlotsAdd { get; }
    public IReadOnlyDictionary<string, ConstructionZoneContributionStage> ZoneStages { get; }
    internal InstalledJobEconomyEffects? JobEconomyEffects { get; }
    internal IReadOnlyList<InstalledCategoryJobModifier> CategoryJobEffects { get; }

    internal ConstructionInstalledCapacitySnapshot(string colonyId, long revision, ConstructionInstalledCapacityObject[] objects, IEnumerable<ConstructionRawContribution> contributions, Dictionary<string, decimal> jobs, decimal housing, decimal slots, Dictionary<string, ConstructionZoneContributionStage> zones, InstalledJobEconomyEffects? jobEconomyEffects, IEnumerable<InstalledCategoryJobModifier> categoryJobEffects)
    {
        ColonyId = colonyId;
        AggregateRevision = revision;
        Objects = Array.AsReadOnly(objects.ToArray());
        ActiveContributions = Array.AsReadOnly(contributions.ToArray());
        JobCapacity = new ReadOnlyDictionary<string, decimal>(jobs);
        Housing = housing;
        ZoneBuildingSlotsAdd = slots;
        ZoneStages = new ReadOnlyDictionary<string, ConstructionZoneContributionStage>(zones);
        JobEconomyEffects = jobEconomyEffects;
        CategoryJobEffects = Array.AsReadOnly(categoryJobEffects.ToArray());
    }

    internal static ConstructionInstalledCapacitySnapshot Build(string colonyId, long revision, ConstructionInstalledCapacityObject[] objects)
    {
        var active = new List<ConstructionRawContribution>();
        var scaled = new List<ConstructionScaledContribution>();
        var zones = new Dictionary<string, ConstructionZoneContributionStage>(StringComparer.Ordinal);
        var levels = new Dictionary<string, int>(StringComparer.Ordinal);
        var amountCount = 0;
        ConstructionContext? context = null;
        foreach (var instance in objects)
        {
            var assessment = instance.Assessment;
            var definition = assessment.Source.Definitions[assessment.DefinitionId];
            var zone = definition.Kind == "zone";
            var rareProducer = definition.Kind == "building" && definition.Id is "building_refinery" or "building_chemical_plant" or "building_crystal_plant";
            var supported = rareProducer || definition.Id is "district_generator" or "district_farming" && definition.Kind == "district" || definition.Id == "building_hydroponics_farm" && definition.Kind == "building" || zone && ZONE_IDS.Contains(definition.Id);
            var profile = rareProducer ? "construction-ordinary-building-" + definition.Id + "-catalog-452-v1" : zone ? "construction-zone-catalog-452-v1" : definition.Kind == "district" ? "construction-rural-catalog-452-v1" : "construction-building-building_hydroponics_farm-catalog-452-v1";
            var path = rareProducer ? "common/buildings/04_manufacturing_buildings.txt" : zone ? "common/zones/00_zones.txt" : definition.Kind == "district" ? "common/districts/02_rural_districts.txt" : "common/buildings/03_resource_buildings.txt";
            if (!supported || assessment.Source.ProfileId != profile || definition.Origin.Source != path || context is not null && !ReferenceEquals(context, assessment.Context)) { throw new InvalidOperationException("Installed source/context is unavailable."); }
            context = assessment.Context;
            var allowed = rareProducer ? RARE_BUILDING_FIELDS : zone ? ZONE_FIELDS : definition.Kind == "district" ? DISTRICT_FIELDS : BUILDING_FIELDS;
            if (definition.Fields.Any(field => !allowed.Contains(field.Key))) { throw new InvalidOperationException("Installed mandatory field is unsupported."); }
            if (zone)
            {
                if (string.IsNullOrWhiteSpace(instance.DistrictId) || instance.DistrictId.Length > 160 || instance.DistrictId.Any(char.IsControl) || instance.DistrictLevel is null or < 0 or > 4096 || levels.TryGetValue(instance.DistrictId, out var level) && level != instance.DistrictLevel) { throw new InvalidOperationException("Installed zone district binding is unresolved."); }
                levels[instance.DistrictId] = instance.DistrictLevel.Value;
                var count = active.Count;
                var amountsBeforeStage = amountCount;
                foreach (var contribution in assessment.Contributions.Where(item => item.Kind is "planet_modifier" or "triggered_planet_modifier" or "district_planet_modifier" or "triggered_district_planet_modifier").Where(item => item.Condition.Truth != ConstructionTruth.False))
                {
                    if (count >= 16384 || contribution.Amounts.Count > 256 || amountsBeforeStage > 65536 - contribution.Amounts.Count) { throw new InvalidOperationException("Installed zone contribution budget exceeded."); }
                    count++;
                    amountsBeforeStage += contribution.Amounts.Count;
                }
                var stage = ConstructionZoneContributionStager.Stage(assessment, colonyId, instance.DistrictId, revision - 1, instance.DistrictLevel);
                if (stage.Gates.Count != 0) { throw new InvalidOperationException("Installed zone contribution is unresolved."); }
                zones.Add(instance.InstanceId, stage);
                foreach (var contribution in stage.Contributions) { Append(contribution.Source, contribution.DistrictScaledAmounts); }
                continue;
            }
            if (instance.DistrictId is not null || instance.DistrictLevel is not null) { throw new InvalidOperationException("Unexpected installed district binding."); }
            foreach (var contribution in assessment.Contributions.Where(item => item.Kind is "planet_modifier" or "triggered_planet_modifier"))
            {
                if (contribution.Condition.Truth == ConstructionTruth.False) { continue; }
                if (active.Count >= 16384 || contribution.Amounts.Count > 256 || amountCount > 65536 - contribution.Amounts.Count) { throw new InvalidOperationException("Installed contribution budget exceeded."); }
                if (contribution.BlockingGates.Count != 0 || contribution.RawMultiplier is not null && (contribution.Kind != "triggered_planet_modifier" || contribution.Amounts.Any(amount => ConstructionJobEconomyMapping.Find(amount.Resource) is null))) { throw new InvalidOperationException("Installed modifier context is unresolved."); }
                Append(contribution, contribution.RawMultiplier is { } multiplier ? Array.AsReadOnly(contribution.Amounts.Select(amount => new ConstructionAmount(amount.Resource, ConstructionZoneContributionStager.ScaleExactly(amount.Value, 1, multiplier), amount.Origin)).ToArray()) : contribution.Amounts);
            }
        }
        if (!ConstructionResourceCategoryBinding.SourcesMatch(objects.SelectMany(instance => new[] { instance.Assessment.Source, instance.Assessment.ConditionSource }))) { throw new InvalidOperationException("Installed source snapshots differ."); }
        var amounts = scaled.SelectMany(contribution => contribution.DistrictScaledAmounts).ToArray();
        var housing = SumExactly(amounts.Where(amount => amount.Resource == "planet_housing_add").Select(amount => amount.Value));
        var slots = SumExactly(amounts.Where(amount => amount.Resource == "zone_building_slots_add").Select(amount => amount.Value));
        var jobs = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var group in amounts.Where(amount => amount.Resource is not ("planet_housing_add" or "zone_building_slots_add" or "planet_jobs_produces_mult" or "pop_bonus_workforce_mult") && ConstructionJobEconomyMapping.Find(amount.Resource) is null && ConstructionJobWorkforceMapping.Find(amount.Resource) is null).GroupBy(amount => amount.Resource, StringComparer.Ordinal))
        {
            if (!group.Key.StartsWith("job_", StringComparison.Ordinal) || !group.Key.EndsWith("_add", StringComparison.Ordinal) || group.Key.Length <= 8) { throw new InvalidOperationException("Installed capacity key is unsupported."); }
            jobs.Add(group.Key[4..^4], SumExactly(group.Select(amount => amount.Value)));
        }
        if (housing < 0 || slots < 0 || jobs.Values.Any(value => value < 0)) { throw new InvalidOperationException("Installed additive capacity is negative."); }
        var economic = amounts.Where(amount => amount.Resource is "planet_jobs_produces_mult" or "pop_bonus_workforce_mult" || ConstructionJobWorkforceMapping.Find(amount.Resource) is not null).ToArray();
        var jobBonuses = economic.SelectMany(amount => (ConstructionJobWorkforceMapping.Find(amount.Resource) ?? []).Select(job => (Job: job, amount.Value)))
            .GroupBy(amount => amount.Job, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => SumExactly(group.Select(amount => amount.Value)), StringComparer.Ordinal);
        var effects = economic.Length == 0 ? null : new InstalledJobEconomyEffects(SumExactly(economic.Where(amount => amount.Resource == "planet_jobs_produces_mult").Select(amount => amount.Value)), SumExactly(economic.Where(amount => amount.Resource == "pop_bonus_workforce_mult").Select(amount => amount.Value)), [], jobBonuses);
        var categoryEffects = amounts.Where(amount => ConstructionJobEconomyMapping.Find(amount.Resource) is not null).GroupBy(amount => amount.Resource, StringComparer.Ordinal).Select(group =>
        {
            var target = ConstructionJobEconomyMapping.Find(group.Key)!.Value;
            var total = SumExactly(group.Select(amount => amount.Value));
            return new InstalledCategoryJobModifier(target.Category, target.Resource, target.Operation, target.Add ? total : 0, target.Add ? 0 : total);
        }).ToArray();
        return new(colonyId, revision, objects, active, jobs, housing, slots, zones, effects, categoryEffects);

        void Append(ConstructionRawContribution source, IReadOnlyList<ConstructionAmount> values)
        {
            if (active.Count >= 16384 || values.Count > 256 || amountCount > 65536 - values.Count) { throw new InvalidOperationException("Installed contribution budget exceeded."); }
            amountCount += values.Count;
            active.Add(source);
            scaled.Add(new(source, values));
        }
    }

    private static decimal SumExactly(IEnumerable<decimal> values)
    {
        BigInteger coefficient = 0;
        const int SCALE = 28;
        foreach (var value in values)
        {
            var bits = decimal.GetBits(value);
            var part = ((BigInteger)(uint)bits[2] << 64) | ((BigInteger)(uint)bits[1] << 32) | (uint)bits[0];
            part *= BigInteger.Pow(10, SCALE - ((bits[3] >> 16) & 255));
            coefficient += bits[3] < 0 ? -part : part;
        }
        var negative = coefficient.Sign < 0;
        coefficient = BigInteger.Abs(coefficient);
        var scale = SCALE;
        while (scale > 0 && coefficient % 10 == 0) { coefficient /= 10; scale--; }
        if (coefficient > (BigInteger.One << 96) - 1) { throw new OverflowException("Exact installed sum exceeds decimal representation."); }
        return new(unchecked((int)(uint)(coefficient & uint.MaxValue)), unchecked((int)(uint)((coefficient >> 32) & uint.MaxValue)), unchecked((int)(uint)((coefficient >> 64) & uint.MaxValue)), negative, (byte)scale);
    }

    private static readonly HashSet<string> DISTRICT_FIELDS = new(StringComparer.Ordinal)
    {
        "shared_capacity_modifier", "expansion_planner", "base_buildtime", "overlay_icon", "is_uncapped", "min_for_deposits_on_planet", "max_for_deposits_on_planet", "zone_slots", "show_on_uncolonized", "potential", "allow", "destroy_trigger", "conversion_ratio", "convert_to", "resources", "planet_modifier", "triggered_planet_modifier", "triggered_name", "triggered_flavor_desc", "prerequisites", "show_tech_unlock_if", "ai_weight_coefficient"
    };
    private static readonly HashSet<string> BUILDING_FIELDS = new(StringComparer.Ordinal)
    {
        "base_buildtime", "category", "building_sets", "potential", "destroy_trigger", "triggered_planet_modifier", "planet_modifier", "resources", "prerequisites", "ai_weight_coefficient"
    };
    private static readonly HashSet<string> RARE_BUILDING_FIELDS = new(BUILDING_FIELDS.Concat(new[] { "planet_limit", "exempt_from_ai_planet_specialization", "show_tech_unlock_if", "upgrades" }), StringComparer.Ordinal);
    private static readonly HashSet<string> ZONE_IDS = new(StringComparer.Ordinal) { "zone_default", "zone_urban", "zone_research_unity", "zone_industrial", "zone_energy" };
    private static readonly HashSet<string> ZONE_FIELDS = new(StringComparer.Ordinal)
    {
        "icon", "base_buildtime", "potential", "unlock", "destroy_trigger", "resources", "max_buildings", "districts_per_building", "max_buildings_planet_class", "is_capped_by_modifier", "zone_sets", "include", "exclude", "included_building_sets", "excluded_building_sets", "planet_modifier", "triggered_planet_modifier", "district_planet_modifier", "triggered_district_planet_modifier", "triggered_desc", "desc", "convert_to", "show_in_tech", "show_tech_unlock_if", "ai_priority", "ai_weight_coefficient", "ai_resource_production"
    };
}

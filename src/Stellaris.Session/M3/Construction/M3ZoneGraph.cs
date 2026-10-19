using Stellaris.Core.Construction;
using Stellaris.Core.Content.Construction;
using Stellaris.Core.Content.PlanetEconomy;

namespace Stellaris.Session.M3.Construction;

/// <summary>Одна occurrence района на definition, как в поддержанном startup; уровни не размножают зоны.</summary>
public static class M3ZoneGraph
{
    private static readonly string[] Rural = ["district_generator", "district_mining", "district_farming"];

    // Узкая source-форма executor: три места, с необязательным явным fixed max=3.
    internal static bool HasFixedCitySlots(ConstructionLiveContent content, string id)
    {
        if (!content.Economy.Zones.TryGetValue(id, out var recipe) || !content.Catalogs.TryGetValue(id, out var catalog)) return false;
        var fields = catalog.Definitions[id].Fields;
        if (!recipe.ZoneSets.Contains("urban")) return false;
        static IEnumerable<ConstructionField> Flatten(IEnumerable<ConstructionField> values)
            => values.SelectMany(field => new[] { field }.Concat(Flatten(field.Children)));
        var all = Flatten(fields).ToArray();
        var caps = all.Where(field => field.Key == "max_buildings").ToArray();
        if (caps.Length > 1 || caps.Length == 1 && (!fields.Contains(caps[0]) || caps[0].Operator != "=" || caps[0].Scalar != "3" || caps[0].Children.Count != 0)
            || all.Any(field => field.Key is "max_buildings_planet_class" or "districts_per_building" or "max_districts" or "is_capped_by_modifier")) return false;
        var modifiers = fields.Where(field => field.Key == "planet_modifier").ToArray();
        var slots = modifiers.SelectMany(field => field.Children).Where(field => field.Key == "zone_building_slots_add").ToArray();
        if (slots is not [var slot] || slot.Operator != "=" || slot.Scalar != "3" || slot.Children.Count != 0
            || all.Count(field => field.Key == "zone_building_slots_add") != 1 || all.Any(field => field.Key == "zone_building_slots_mult")
            || modifiers.Any(field => field.Operator != "=" || field.Scalar is not null || field.Children.Any(child => child.Key is "mult" or "potential"))) return false;
        var terms = recipe.Terms.Where(term => term.TargetId == "zone_building_slots_add").ToArray();
        return terms is [var term] && term.Kind == PlanetEconomyTermKind.PlanetModifier && term.Value == 3
            && term.ConditionDisposition == PlanetEconomyConditionDisposition.Unconditional && term.Condition is null
            && term.ApplicationScope == PlanetEconomyApplicationScope.PerDefinitionInstance;
    }

    public static ConstructionLiveRuntime Materialize(M3ConstructionContent source, M3StartupColony startup, ConstructionLiveRuntime state)
    {
        var additions = new List<ConstructionLiveSlot>();
        foreach (var id in Rural.Where(id => state.Installed.Values.Any(item => item.Kind == ConstructionKind.District && item.DefinitionId == id)))
        {
            var fields = source.Content.Catalogs[id].Definitions[id].Fields.Where(field => field.Key == "zone_slots").ToArray();
            if (fields is not [var list] || list.Operator != "=" || list.Scalar is not null) throw new InvalidDataException("District zone slots unresolved.");
            for (var index = 0; index < list.Children.Count; index++)
            {
                var child = list.Children[index];
                if (child.Operator != "" || child.Scalar is not { } slotType || !source.Zones.Definitions.TryGetValue(slotType, out var definition) || definition.Kind != "zoneSlot")
                    throw new InvalidDataException("District zone slot binding unresolved.");
                var slotId = state.ColonyId + "/slot/" + id + "/" + slotType + "/" + index;
                var slot = new ConstructionLiveSlot(slotId, ConstructionKind.Zone, null, null, state.ColonyId + "/district/" + id, slotType);
                if (state.Slots.TryGetValue(slotId, out var existing))
                { if (existing != slot) throw new InvalidDataException("District zone slot identity mismatch."); }
                else additions.Add(slot);
            }
        }
        state = state.AppendSourceSlots(additions);
        additions.Clear();
        foreach (var item in state.Installed.Values.Where(item => item.Kind == ConstructionKind.Zone))
        {
            var slot = state.Slots[item.SlotId];
            if (slot.Kind != ConstructionKind.Zone || slot.DistrictInstanceId is null || slot.ZoneSlotDefinitionId is null
                || !(IsBasicZone(item.DefinitionId) || slot.ZoneSlotDefinitionId is "slot_city_01" or "slot_city_02"
                    && source.FixedCityZones.Contains(item.DefinitionId)) || !Fits(source, item.DefinitionId, slot.ZoneSlotDefinitionId))
                throw new InvalidDataException("Installed zone binding unresolved.");
            var instanceId = slot.ZoneInstanceId ?? item.SlotId + "/zone";
            for (var index = 0; index < 3; index++)
            {
                var building = new ConstructionLiveSlot(instanceId + "/building-slot/" + index, ConstructionKind.Building,
                    instanceId, item.DefinitionId);
                if (state.Slots.TryGetValue(building.Id, out var existing))
                { if (existing != building) throw new InvalidDataException("Zone building slot identity mismatch."); }
                else additions.Add(building);
            }
        }
        return state.AppendSourceSlots(additions);
    }

    public static bool IsBasicZone(string id) => id is "zone_energy" or "zone_minerals" or "zone_food";

    public static bool BuildingFits(M3ConstructionContent source, string buildingId, string zoneId)
        => BuildingCompatibility(source, buildingId, zoneId) == true;

    public static bool? BuildingCompatibility(M3ConstructionContent source, string buildingId, string zoneId)
    {
        if (!source.Content.Catalogs.TryGetValue(zoneId, out var zones) || !source.Content.Catalogs.TryGetValue(buildingId, out var buildings)) return null;
        // Состав зоны может приходить из inline script: экономическая проекция верхнего уровня неполна.
        static bool Valid(IReadOnlyList<ConstructionField> fields, string key) => fields.Where(field => field.Key == key)
            .All(field => field.Operator == "=" && field.Scalar is null && field.Children.All(child => child.Operator == "" && child.Scalar is not null && child.Children.Count == 0));
        static string[] Values(IReadOnlyList<ConstructionField> fields, string key) => fields.Where(field => field.Key == key)
            .SelectMany(field => field.Children).Where(field => field.Operator == "" && field.Scalar is not null).Select(field => field.Scalar!).ToArray();
        var zone = zones.Definitions[zoneId].Fields;
        if (!new[] { "include", "exclude", "included_building_sets", "excluded_building_sets" }.All(key => Valid(zone, key))
            || !Valid(buildings.Definitions[buildingId].Fields, "building_sets")) return null;
        var sets = Values(buildings.Definitions[buildingId].Fields, "building_sets");
        return (Values(zone, "include").Contains("all") || Values(zone, "include").Contains(buildingId) || Values(zone, "included_building_sets").Intersect(sets).Any())
            && !Values(zone, "exclude").Contains(buildingId) && !Values(zone, "excluded_building_sets").Intersect(sets).Any();
    }

    public static IReadOnlyDictionary<string, decimal> Contributions(M3ConstructionContent source, string zoneId, int level,
        PlanetEconomyTermKind kind, IReadOnlyDictionary<string, bool> facts)
    {
        var active = new List<PlanetEconomyTerm>();
        foreach (var term in source.Content.Economy.Zones[zoneId].Terms.Where(term => term.Kind == kind
            && term.ConditionDisposition != PlanetEconomyConditionDisposition.InactiveInOrdinaryContext))
        {
            var condition = term.ConditionDisposition == PlanetEconomyConditionDisposition.RequiresRuntimeContext ? ConstructionLiveConditions.Evaluate(term.Condition, facts) : true;
            if (condition == false) continue;
            if (condition is null || term.Value is null || level <= 0) throw new InvalidDataException("Zone contribution unresolved.");
            active.Add(term);
        }
        return active.GroupBy(term => term.TargetId, StringComparer.Ordinal).ToDictionary(group => group.Key,
            group => group.Sum(term => checked(term.Value!.Value * (term.ApplicationScope == PlanetEconomyApplicationScope.PerAttachedDistrictLevel ? level : 1))), StringComparer.Ordinal);
    }

    public static IReadOnlyList<M3StartupZone> Current(M3StartupColony startup, ConstructionLiveRuntime state)
    {
        var zones = startup.Zones.ToList();
        foreach (var item in state.Installed.Values.Where(item => item.Kind == ConstructionKind.Zone))
        {
            var slot = state.Slots[item.SlotId];
            if (slot.DistrictInstanceId is null) throw new InvalidDataException("Detached runtime zone.");
            var index = zones.FindIndex(zone => zone.SlotInstanceId == item.SlotId);
            if (index >= 0)
            {
                var original = zones[index];
                if (slot.ZoneInstanceId != original.InstanceId || slot.DistrictInstanceId != original.DistrictInstanceId)
                    throw new InvalidDataException("Startup runtime zone identity mismatch.");
                zones[index] = original with { ZoneId = item.DefinitionId };
            }
            else zones.Add(new(slot.ZoneInstanceId ?? item.SlotId + "/zone", slot.DistrictInstanceId, item.SlotId, item.DefinitionId, 0));
        }
        return zones.AsReadOnly();
    }

    public static int AttachedLevel(M3StartupColony startup, ConstructionLiveRuntime state, string districtInstanceId)
    {
        var districtId = state.Installed.Values.Where(item => item.Kind == ConstructionKind.District).Select(item => item.DefinitionId)
            .Distinct(StringComparer.Ordinal).SingleOrDefault(id => state.ColonyId + "/district/" + id == districtInstanceId)
            ?? throw new InvalidDataException("Zone district occurrence unresolved.");
        return state.Installed.Values.Count(item => item.Kind == ConstructionKind.District && item.DefinitionId == districtId);
    }

    public static bool Fits(M3ConstructionContent source, string zoneId, string slotDefinitionId)
    {
        if (!source.Content.Catalogs.TryGetValue(zoneId, out var catalog) || !source.Zones.Definitions.TryGetValue(slotDefinitionId, out var slot)) return false;
        static string[] Values(IReadOnlyList<ConstructionField> fields, string key) => fields.Where(field => field.Key == key)
            .SelectMany(field => field.Children).Where(field => field.Operator == "" && field.Scalar is not null).Select(field => field.Scalar!).ToArray();
        var sets = Values(catalog.Definitions[zoneId].Fields, "zone_sets");
        return (Values(slot.Fields, "include").Contains(zoneId) || Values(slot.Fields, "included_zone_sets").Intersect(sets).Any())
            && !Values(slot.Fields, "exclude").Contains(zoneId) && !Values(slot.Fields, "excluded_zone_sets").Intersect(sets).Any();
    }

    public static (string? Gate, IReadOnlyList<string> Requirements) Eligibility(M3ConstructionContent source, ConstructionLiveSlot slot, ConstructionContext context)
    {
        if (slot.DistrictInstanceId is null || slot.ZoneSlotDefinitionId is not { } id || !source.Zones.Definitions.TryGetValue(id, out var definition))
            return ("zone-slot-binding-unresolved", []);
        var assessment = ConstructionCatalogAssessor.Assess(source.Zones, id, context, source.Content.Conditions);
        var requirements = new List<string>();
        void Collect(IEnumerable<ConstructionField> fields)
        { foreach (var field in fields) { if (field.Key == "fail_text" && field.Scalar is { } key) requirements.Add(key); Collect(field.Children); } }
        Collect(definition.Fields.Where(field => field.Key is "potential" or "unlock"));
        var checks = new[] { ("potential", assessment.Potential), ("unlock", assessment.Unlock) }
            .Where(check => definition.Fields.Any(field => field.Key == check.Item1)).Select(check => check.Item2.Truth).ToArray();
        return (checks.Contains(ConstructionTruth.False) ? "zone-slot-locked" : checks.Contains(ConstructionTruth.Unknown) ? "zone-slot-condition-unresolved" : null,
            requirements.Distinct(StringComparer.Ordinal).ToArray());
    }
}

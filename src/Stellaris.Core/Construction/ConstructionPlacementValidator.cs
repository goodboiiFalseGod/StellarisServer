using System.Collections.ObjectModel;
using System.Globalization;
using Stellaris.Core.Content.Construction;

namespace Stellaris.Core.Construction;

public sealed class ConstructionPlacementState
{
    public string ZoneId { get; }
    public bool? Unlocked { get; }
    public int? EffectiveCapacity { get; }
    public string? CapacityPolicyId { get; }
    public IReadOnlyList<string> Slots { get; }
    public IReadOnlyDictionary<string, string> Installed { get; }
    public IReadOnlyDictionary<string, string> Reserved { get; }

    public ConstructionPlacementState(string zoneId, bool? unlocked, int? effectiveCapacity, string? capacityPolicyId, IEnumerable<string> slots, IReadOnlyDictionary<string, string> installed, IReadOnlyDictionary<string, string> reserved)
    {
        ZoneId = ConstructionValues.Id(zoneId);
        if (effectiveCapacity is < 0 or > 4096) { throw new ArgumentException("Invalid effective zone capacity."); }
        Unlocked = unlocked;
        EffectiveCapacity = effectiveCapacity;
        CapacityPolicyId = capacityPolicyId is null ? null : ConstructionValues.Id(capacityPolicyId);
        ArgumentNullException.ThrowIfNull(slots);
        var copy = slots.Take(4097).Select(ConstructionValues.Id).ToArray();
        if (copy.Length > 4096 || copy.Distinct(StringComparer.Ordinal).Count() != copy.Length) { throw new ArgumentException("Invalid zone slots."); }
        Slots = Array.AsReadOnly(copy);
        Installed = Copy(installed);
        Reserved = Copy(reserved);
    }

    private static IReadOnlyDictionary<string, string> Copy(IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count > 4096) { throw new ArgumentException("Zone instances exceed limit."); }
        return new ReadOnlyDictionary<string, string>(values.ToDictionary(pair => ConstructionValues.Id(pair.Key), pair => ConstructionValues.Id(pair.Value), StringComparer.Ordinal));
    }
}

public sealed record ConstructionPlacementResult(ConstructionTruth Allowed, string Code, decimal? DeclaredMaxBuildings);

public static class ConstructionPlacementValidator
{
    public static ConstructionPlacementResult Validate(ConstructionPlacementProjection content, ConstructionDefinition building, ConstructionPlacementState state, string targetSlotId)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(building);
        ArgumentNullException.ThrowIfNull(state);
        ConstructionValues.Id(targetSlotId);
        if (!content.Definitions.TryGetValue(state.ZoneId, out var zone) || zone.Kind != "zone") { return Unknown("zone-definition-unresolved"); }
        var keys = new[] { "include", "exclude", "included_building_sets", "excluded_building_sets" };
        var lists = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            var fields = zone.Fields.Where(field => field.Key == key).ToArray();
            if (fields.Length > 1 || fields.Any(field => field.Operator != "=" || field.Scalar is not null || field.Children.Any(child => child.Operator != "" || child.Scalar is null))) { return Unknown("zone-membership-shape"); }
            lists[key] = fields.Length == 0 ? [] : fields[0].Children.Select(child => child.Scalar!).ToArray();
        }
        var max = zone.Fields.Where(field => field.Key == "max_buildings").ToArray();
        if (max.Length != 1 || max[0].Operator != "=" || !decimal.TryParse(max[0].Scalar, NumberStyles.Float, CultureInfo.InvariantCulture, out var declared) || declared < 0 || decimal.Truncate(declared) != declared) { return Unknown("zone-declared-capacity-unresolved"); }
        var explicitlyIncluded = lists["include"].Contains(building.Id, StringComparer.Ordinal) || building.BuildingSets.Intersect(lists["included_building_sets"], StringComparer.Ordinal).Any();
        var excluded = lists["exclude"].Contains(building.Id, StringComparer.Ordinal) || building.BuildingSets.Intersect(lists["excluded_building_sets"], StringComparer.Ordinal).Any();
        if (explicitlyIncluded && excluded) { return new(ConstructionTruth.Unknown, "zone-membership-precedence-unresolved", declared); }
        if (excluded || !(explicitlyIncluded || lists["include"].Contains("all", StringComparer.Ordinal))) { return new(ConstructionTruth.False, "building-set-excluded", declared); }
        if (state.Unlocked == false) { return new(ConstructionTruth.False, "zone-locked", declared); }
        if (state.Unlocked is null) { return new(ConstructionTruth.Unknown, "zone-unlock-context", declared); }
        if (state.Installed.Keys.Intersect(state.Reserved.Keys, StringComparer.Ordinal).Any() || state.Installed.Keys.Concat(state.Reserved.Keys).Any(slot => !state.Slots.Contains(slot, StringComparer.Ordinal))) { return new(ConstructionTruth.Unknown, "zone-instance-state-invalid", declared); }
        if (state.EffectiveCapacity is null || state.CapacityPolicyId is null || state.Slots.Count != state.EffectiveCapacity) { return new(ConstructionTruth.Unknown, "zone-effective-capacity-unresolved", declared); }
        if (!state.Slots.Contains(targetSlotId, StringComparer.Ordinal) || state.Installed.ContainsKey(targetSlotId) || state.Reserved.ContainsKey(targetSlotId)) { return new(ConstructionTruth.False, "slot-unavailable", declared); }
        return new(ConstructionTruth.True, "placement-context-valid", declared);
    }

    private static ConstructionPlacementResult Unknown(string code) => new(ConstructionTruth.Unknown, code, null);
}

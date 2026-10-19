using System.Numerics;
using Stellaris.Core.Content.Construction;

namespace Stellaris.Core.Construction;

public sealed record ConstructionScaledContribution(ConstructionRawContribution Source, IReadOnlyList<ConstructionAmount> DistrictScaledAmounts);

public sealed class ConstructionZoneContributionStage
{
    public ConstructionCatalogAssessment Source { get; }
    public string ColonyId { get; }
    public string DistrictInstanceId { get; }
    public long WorldRevision { get; }
    public int? DistrictLevel { get; }
    public IReadOnlyList<ConstructionScaledContribution> Contributions { get; }
    public IReadOnlyList<ConstructionGate> Gates { get; }

    internal ConstructionZoneContributionStage(ConstructionCatalogAssessment source, string colonyId, string districtInstanceId, long worldRevision, int? districtLevel, IEnumerable<ConstructionScaledContribution> contributions, IEnumerable<ConstructionGate> gates)
    {
        Source = source;
        ColonyId = colonyId;
        DistrictInstanceId = districtInstanceId;
        WorldRevision = worldRevision;
        DistrictLevel = districtLevel;
        Contributions = Array.AsReadOnly(contributions.ToArray());
        Gates = Array.AsReadOnly(gates.Distinct().Take(256).ToArray());
    }
}

public static class ConstructionZoneContributionStager
{
    public static ConstructionZoneContributionStage Stage(ConstructionCatalogAssessment assessment, string colonyId, string districtInstanceId, long worldRevision, int? districtLevel)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        ConstructionValues.Id(colonyId);
        ConstructionValues.Id(districtInstanceId);
        if (worldRevision < 0 || districtLevel is < 0 or > 4096 || assessment.Source.Definitions[assessment.DefinitionId].Kind != "zone") { throw new ArgumentException("Invalid zone contribution binding."); }
        var contributions = new List<ConstructionScaledContribution>();
        var gates = new List<ConstructionGate>();
        foreach (var source in assessment.Contributions.Where(item => item.Kind is "planet_modifier" or "district_planet_modifier" or "triggered_planet_modifier" or "triggered_district_planet_modifier"))
        {
            if (source.Condition.Truth == ConstructionTruth.False) { continue; }
            var pending = source.BlockingGates.ToList();
            var district = source.Kind is "district_planet_modifier" or "triggered_district_planet_modifier";
            if (district && districtLevel is null) { pending.Add(new("dependency-unresolved", "zone-district-level")); }
            var triggered = source.Kind.StartsWith("triggered_", StringComparison.Ordinal);
            if (source.RawMultiplier is not null && !triggered) { pending.Add(new("dependency-unresolved", "zone-multiplier-composition")); }
            gates.AddRange(pending);
            if (pending.Count != 0) { continue; }
            try
            {
                var amounts = source.Amounts.Select(amount => new ConstructionAmount(amount.Resource, ScaleExactly(amount.Value, district ? districtLevel!.Value : 1, triggered ? source.RawMultiplier ?? 1m : 1m), amount.Origin)).ToArray();
                contributions.Add(new(source, Array.AsReadOnly(amounts)));
            }
            catch (OverflowException)
            {
                return new(assessment, colonyId, districtInstanceId, worldRevision, districtLevel, Array.Empty<ConstructionScaledContribution>(), new[] { new ConstructionGate("quantity-overflow", "zone-district-scaling") }.Concat(gates));
            }
        }
        return new(assessment, colonyId, districtInstanceId, worldRevision, districtLevel, contributions, gates);
    }

    internal static decimal ScaleExactly(decimal value, int level, decimal multiplier)
    {
        var bits = decimal.GetBits(value);
        var coefficient = ((BigInteger)(uint)bits[2] << 64) | ((BigInteger)(uint)bits[1] << 32) | (uint)bits[0];
        var multiplierBits = decimal.GetBits(multiplier);
        var multiplierCoefficient = ((BigInteger)(uint)multiplierBits[2] << 64) | ((BigInteger)(uint)multiplierBits[1] << 32) | (uint)multiplierBits[0];
        coefficient *= level * multiplierCoefficient;
        var scale = ((bits[3] >> 16) & 255) + ((multiplierBits[3] >> 16) & 255);
        while (scale > 0 && coefficient % 10 == 0)
        {
            coefficient /= 10;
            scale--;
        }
        if (scale > 28 || coefficient > (BigInteger.One << 96) - 1) { throw new OverflowException("Exact source scaling exceeds decimal representation."); }
        return new(unchecked((int)(uint)(coefficient & uint.MaxValue)), unchecked((int)(uint)((coefficient >> 32) & uint.MaxValue)), unchecked((int)(uint)((coefficient >> 64) & uint.MaxValue)), (bits[3] < 0) != (multiplierBits[3] < 0), (byte)scale);
    }
}

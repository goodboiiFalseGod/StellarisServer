using System.Collections.ObjectModel;
using System.Collections.Frozen;

namespace Stellaris.Core.Construction;

public enum ConstructionKind { District, Zone, Building, BlockerClearance }
public enum ConstructionOrderStatus { Pending, Completed, Cancelled }
public enum ConstructionCancellationReason { Player, ConditionLost, OwnershipChanged, Destroyed, NonPositiveSpeed, PayerMissing, CalculationFailed }
public enum ConstructionAuthorityMode { Individualist, Hive, MachineIntelligence }
public sealed record ConstructionGate(string Code, string DependencyId);
public sealed record ConstructionWork(string OrderId, decimal Amount);
public sealed record ConstructionReceipt(string Fingerprint, long AppliedRevision);
public sealed record ConstructionIdentity(string ColonyId, ConstructionAuthorityMode AuthorityMode, bool IsMobile, string? CarrierId, string? LocationSystemId);

public sealed class ConstructionQuote
{
    public string ContentProfileId { get; }
    public string PolicyId { get; }
    public string DefinitionId { get; }
    public ConstructionKind Kind { get; }
    public decimal RequiredWork { get; }
    public IReadOnlyDictionary<string, decimal> Costs { get; }
    public IReadOnlyDictionary<string, decimal> InfrastructureUpkeep { get; }
    public IReadOnlyDictionary<string, decimal> JobCapacity { get; }
    public IReadOnlyList<ConstructionGate> Gates { get; }
    public int? PlanetLimit { get; }

    public ConstructionQuote(string contentProfileId, string policyId, string definitionId, ConstructionKind kind, decimal requiredWork, IReadOnlyDictionary<string, decimal> costs, IReadOnlyDictionary<string, decimal> infrastructureUpkeep, IReadOnlyDictionary<string, decimal> jobCapacity, IEnumerable<ConstructionGate> gates, int? planetLimit = null)
    {
        ContentProfileId = ConstructionValues.Id(contentProfileId);
        PolicyId = ConstructionValues.Id(policyId);
        DefinitionId = ConstructionValues.Id(definitionId);
        if (!Enum.IsDefined(kind) || requiredWork <= 0) { throw new ArgumentException("Invalid construction kind/work."); }
        Kind = kind;
        if (planetLimit is not null && (planetLimit != 1 || kind != ConstructionKind.Building)) { throw new ArgumentException("Unsupported planet limit."); }
        PlanetLimit = planetLimit;
        RequiredWork = requiredWork;
        Costs = ConstructionValues.Amounts(costs);
        InfrastructureUpkeep = ConstructionValues.Amounts(infrastructureUpkeep);
        JobCapacity = ConstructionValues.Amounts(jobCapacity);
        ArgumentNullException.ThrowIfNull(gates);
        var gateArray = gates.Take(257).ToArray();
        if (gateArray.Length > 256 || gateArray.Any(gate => gate is null)) { throw new ArgumentException("Invalid construction dependency gates."); }
        Gates = Array.AsReadOnly(gateArray.Select(gate => new ConstructionGate(ConstructionValues.Id(gate.Code), ConstructionValues.Id(gate.DependencyId))).ToArray());
    }
}

public sealed class ConstructionRefund
{
    public const string PAID_CANCELLATION_POLICY = "actual-paid-player-cancellation";

    public string PolicyId { get; }
    public IReadOnlyDictionary<string, decimal> Amounts { get; }
    public IReadOnlyDictionary<string, decimal> StorageCaps { get; }

    public ConstructionRefund(string policyId, IReadOnlyDictionary<string, decimal> amounts, IReadOnlyDictionary<string, decimal> storageCaps)
    {
        PolicyId = ConstructionValues.Id(policyId);
        Amounts = ConstructionValues.Amounts(amounts);
        StorageCaps = ConstructionValues.Amounts(storageCaps);
    }
}

public sealed class ConstructionZoneReplacement
{
    public string SourceDefinitionId { get; }
    public string ZoneInstanceId { get; }
    public IReadOnlyDictionary<string, string> RemovedBuildings { get; }

    public ConstructionZoneReplacement(string sourceDefinitionId, string zoneInstanceId, IReadOnlyDictionary<string, string> removedBuildings)
    {
        SourceDefinitionId = ConstructionValues.Id(sourceDefinitionId);
        ZoneInstanceId = ConstructionValues.Id(zoneInstanceId);
        if (removedBuildings.Count > 4096) throw new ArgumentException("Invalid replacement buildings.");
        RemovedBuildings = removedBuildings.ToFrozenDictionary(pair => ConstructionValues.Id(pair.Key), pair => ConstructionValues.Id(pair.Value), StringComparer.Ordinal);
    }
}

public sealed record ConstructionOrder(string Id, string SlotId, int PayerId, ConstructionQuote Quote, decimal CompletedWork, ConstructionOrderStatus Status,
    string? UpgradeFromDefinitionId = null, string? UpgradeZoneInstanceId = null, ConstructionCancellationReason? CancellationReason = null,
    ConstructionZoneReplacement? ZoneReplacement = null, ConstructionBuildingReplacement? BuildingReplacement = null);
public sealed record ConstructionBuildingReplacement(string SourceDefinitionId, string ZoneInstanceId);
public sealed record ConstructionInstalled(string SlotId, string DefinitionId, ConstructionKind Kind, IReadOnlyDictionary<string, decimal> InfrastructureUpkeep, IReadOnlyDictionary<string, decimal> JobCapacity,
    bool Enabled = true);
public sealed record ConstructionTransition(bool Accepted, string Code, ConstructionLedger State, long? AppliedRevision);

internal static class ConstructionValues
{
    public static string Id(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 160 || value.Any(char.IsControl)) { throw new ArgumentException("Invalid construction identifier."); }
        return value;
    }

    public static IReadOnlyDictionary<string, decimal> Amounts(IReadOnlyDictionary<string, decimal> values, int maximum = 256)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count > maximum) { throw new ArgumentException("Construction quantity count exceeds limit."); }
        var result = new SortedDictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var pair in values)
        {
            if (pair.Value < 0) { throw new ArgumentException("Negative construction quantity."); }
            result.Add(Id(pair.Key), pair.Value);
        }
        return new ReadOnlyDictionary<string, decimal>(result);
    }
}

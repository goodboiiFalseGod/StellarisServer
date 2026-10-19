using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Stellaris.Core.Economy;

namespace Stellaris.Core.Construction;

public sealed record ConstructionStorage(bool Unlimited, decimal? Cap)
{
    public bool Unspecified => !Unlimited && Cap is null;
}
public sealed class ConstructionBankSnapshot
{
    public IReadOnlyDictionary<int, IReadOnlyDictionary<string, decimal>> Accounts { get; }
    public IReadOnlyDictionary<string, ConstructionStorage> Storage { get; }
    public IReadOnlyDictionary<int, IReadOnlyDictionary<string, ConstructionStorage>> PayerStorage { get; }
    /// <summary>Подтверждённо исчезнувшие payer; неполный accounts не является таким доказательством.</summary>
    public IReadOnlySet<int> AbsentPayerIds { get; }
    public ConstructionBankSnapshot(IReadOnlyDictionary<int, IReadOnlyDictionary<string, decimal>> accounts, IReadOnlyDictionary<string, ConstructionStorage> storage,
        IEnumerable<int>? absentPayerIds = null, IReadOnlyDictionary<int, IReadOnlyDictionary<string, ConstructionStorage>>? payerStorage = null)
    {
        if (accounts.Count > 4096 || accounts.Keys.Any(id => id <= 0)) { throw new ArgumentException("Invalid accounts."); }
        Accounts = accounts.ToFrozenDictionary(pair => pair.Key, pair => ConstructionValues.Amounts(pair.Value));
        Storage = storage.ToFrozenDictionary(pair => ConstructionValues.Id(pair.Key), pair => pair.Value, StringComparer.Ordinal);
        if (Storage.Count > 256 || Storage.Any(pair => pair.Value is null || pair.Value.Unlimited && pair.Value.Cap is not null || pair.Value.Cap < 0)) { throw new ArgumentException("Invalid storage policy."); }
        if (payerStorage is not null && (payerStorage.Count > 4096 || payerStorage.Keys.Any(id => !Accounts.ContainsKey(id)))) throw new ArgumentException("Invalid payer storage.");
        PayerStorage = (payerStorage ?? new Dictionary<int, IReadOnlyDictionary<string, ConstructionStorage>>()).ToFrozenDictionary(pair => pair.Key,
            pair => (IReadOnlyDictionary<string, ConstructionStorage>)pair.Value.ToFrozenDictionary(item => ConstructionValues.Id(item.Key), item => item.Value, StringComparer.Ordinal));
        if (PayerStorage.Values.Any(items => items.Count > 256 || items.Any(pair => !Storage.ContainsKey(pair.Key) || pair.Value is null
            || pair.Value.Unlimited && pair.Value.Cap is not null || pair.Value.Cap < 0))) throw new ArgumentException("Invalid payer storage policy.");
        var absent = (absentPayerIds ?? []).Take(4097).ToArray();
        if (absent.Length > 4096 || absent.Any(id => id <= 0 || Accounts.ContainsKey(id))) throw new ArgumentException("Invalid absent payer evidence.");
        AbsentPayerIds = absent.ToFrozenSet();
    }

    public bool TryGetStorage(int payerId, string resourceId, out ConstructionStorage storage)
    {
        if (PayerStorage.TryGetValue(payerId, out var policies) && policies.TryGetValue(resourceId, out storage!)) return true;
        return Storage.TryGetValue(resourceId, out storage!);
    }

    public IReadOnlyList<ConstructionResourceDelta> Refund(ConstructionOrder order)
        => RefundAll([order]);

    internal IReadOnlyList<ConstructionResourceDelta> RefundAll(IEnumerable<ConstructionOrder> orders)
    {
        var totals = new Dictionary<(int Payer, string Resource), decimal>();
        foreach (var order in orders)
        {
            if (AbsentPayerIds.Contains(order.PayerId)) continue;
            var account = Accounts[order.PayerId];
            foreach (var pair in order.Quote.Costs)
            {
                if (!TryGetStorage(order.PayerId, pair.Key, out var policy) || !account.TryGetValue(pair.Key, out var current)) { throw new ArgumentException("Missing resource storage policy."); }
                var key = (order.PayerId, pair.Key);
                var already = totals.GetValueOrDefault(key);
                if (policy.Unspecified && pair.Value > 0) { throw new ResourceStorageUnresolvedException(pair.Key); }
                if (pair.Value == 0) { continue; }
                var credit = policy.Unlimited ? pair.Value : Math.Min(pair.Value, Math.Max(0, policy.Cap!.Value - current - already));
                totals[key] = checked(already + credit);
            }
        }
        return Array.AsReadOnly(totals.Select(pair => new ConstructionResourceDelta(pair.Key.Payer, pair.Key.Resource, pair.Value)).ToArray());
    }
}

public sealed record ConstructionResourceDelta(int PayerId, string ResourceId, decimal Amount);
public sealed record ConstructionLiveSlot(string Id, ConstructionKind Kind, string? ZoneInstanceId, string? ZoneDefinitionId,
    string? DistrictInstanceId = null, string? ZoneSlotDefinitionId = null);
public sealed record ConstructionLiveResult(bool Accepted, string Code, ConstructionLiveRuntime State, IReadOnlyList<ConstructionResourceDelta> ResourceDeltas, long? AppliedRevision = null);

/// <summary>Неизменяемый candidate. Владелец сессии публикует его вместе с economy candidate одним lock.</summary>
public sealed class ConstructionLiveRuntime
{
    public string ColonyId { get; }
    public int OwnerId { get; }
    public long Revision { get; }
    public bool Destroyed { get; }
    public IReadOnlyDictionary<string, ConstructionLiveSlot> Slots { get; }
    /// <summary>Связь исходного startup occurrence с каноническим building slot.</summary>
    public IReadOnlyDictionary<string, string> StartupBuildingBySlot { get; }
    public IReadOnlyDictionary<string, ConstructionInstalled> Installed { get; }
    public IReadOnlyDictionary<string, ConstructionOrder> Orders { get; }
    public IReadOnlyList<string> PendingOrderIds { get; }
    private readonly IReadOnlyDictionary<string, ConstructionReceipt> _receipts;

    public ConstructionLiveRuntime(string colonyId, int ownerId, IEnumerable<ConstructionLiveSlot> slots, IEnumerable<ConstructionInstalled> installed, long revision = 0, IReadOnlyDictionary<string, string>? startupBuildingBySlot = null)
    {
        ColonyId = ConstructionValues.Id(colonyId);
        if (ownerId <= 0 || revision < 0) { throw new ArgumentException("Invalid colony identity."); }
        OwnerId = ownerId;
        Revision = revision;
        Slots = slots.Take(4097).ToFrozenDictionary(slot => ConstructionValues.Id(slot.Id), StringComparer.Ordinal);
        Installed = installed.Take(4097).Select(CopyInstalled).ToFrozenDictionary(item => ConstructionValues.Id(item.SlotId), StringComparer.Ordinal);
        if (Slots.Count > 4096 || Installed.Count > 4096 || Installed.Any(pair => !Slots.TryGetValue(pair.Key, out var slot) || slot.Kind != pair.Value.Kind) || Slots.Values.Any(slot => !Enum.IsDefined(slot.Kind))) { throw new ArgumentException("Invalid installed graph."); }
        StartupBuildingBySlot = (startupBuildingBySlot ?? new Dictionary<string, string>()).ToFrozenDictionary(pair => ConstructionValues.Id(pair.Key), pair => ConstructionValues.Id(pair.Value), StringComparer.Ordinal);
        if (StartupBuildingBySlot.Count > Installed.Count || StartupBuildingBySlot.Keys.Any(slot => !Installed.ContainsKey(slot)) || StartupBuildingBySlot.Values.Distinct(StringComparer.Ordinal).Count() != StartupBuildingBySlot.Count) { throw new ArgumentException("Invalid startup building occurrence mapping."); }
        Orders = new Dictionary<string, ConstructionOrder>().ToFrozenDictionary();
        PendingOrderIds = Array.Empty<string>();
        _receipts = new Dictionary<string, ConstructionReceipt>().ToFrozenDictionary();
    }

    private ConstructionLiveRuntime(ConstructionLiveRuntime previous, long revision, int ownerId, IEnumerable<ConstructionOrder> orders, IEnumerable<ConstructionInstalled> installed, IEnumerable<string> pending, IDictionary<string, ConstructionReceipt> receipts, bool? destroyed = null,
        IReadOnlyDictionary<string, ConstructionLiveSlot>? slots = null)
    {
        ColonyId = previous.ColonyId;
        Slots = slots ?? previous.Slots;
        StartupBuildingBySlot = previous.StartupBuildingBySlot;
        OwnerId = ownerId;
        Revision = revision;
        Destroyed = destroyed ?? previous.Destroyed;
        Orders = orders.ToFrozenDictionary(order => order.Id, StringComparer.Ordinal);
        Installed = installed.ToFrozenDictionary(item => item.SlotId, StringComparer.Ordinal);
        PendingOrderIds = Array.AsReadOnly(pending.ToArray());
        _receipts = receipts.ToFrozenDictionary(StringComparer.Ordinal);
    }

    public ConstructionLiveResult? ReplayCommand(string commandId, int actorId, long revision, string requestIdentity)
    {
        if (actorId != OwnerId) { return Failure("not-owner"); }
        if (!_receipts.TryGetValue(commandId, out var receipt)) { return null; }
        var fingerprint = Fingerprint(actorId, revision, new { Request = requestIdentity });
        return receipt.Fingerprint == fingerprint ? new(true, "duplicate", this, [], receipt.AppliedRevision) : Failure("command-id-conflict");
    }

    public ConstructionLiveRuntime SynchronizeRevision(long revision)
    {
        if (revision < Revision) { throw new ArgumentException("Revision cannot move backwards."); }
        return new(this, revision, OwnerId, Orders.Values, Installed.Values, PendingOrderIds, _receipts.ToDictionary());
    }

    /// <summary>Доверенная материализация source graph в том же candidate; не является клиентской командой.</summary>
    public ConstructionLiveRuntime AppendSourceSlots(IEnumerable<ConstructionLiveSlot> additions)
    {
        var next = Slots.ToDictionary(StringComparer.Ordinal);
        foreach (var slot in additions.Take(4097))
        {
            ConstructionValues.Id(slot.Id);
            if (slot.Kind is not (ConstructionKind.Zone or ConstructionKind.Building) || !next.TryAdd(slot.Id, slot) || next.Count > 4096)
                throw new ArgumentException("Invalid source slot extension.");
        }
        return next.Count == Slots.Count ? this : new(this, Revision, OwnerId, Orders.Values, Installed.Values, PendingOrderIds,
            _receipts.ToDictionary(), slots: next.ToFrozenDictionary(StringComparer.Ordinal));
    }

    public ConstructionLiveResult Enqueue(string commandId, int actorId, long revision, string orderId, string slotId, ConstructionQuote quote, ConstructionBankSnapshot bank, string? requestIdentity = null,
        string? upgradeFrom = null, string? upgradeZoneInstanceId = null, ConstructionZoneReplacement? zoneReplacement = null,
        ConstructionBuildingReplacement? buildingReplacement = null)
    {
        return Apply(commandId, actorId, revision, new { Operation = "enqueue", orderId, slotId, quote, upgradeFrom, upgradeZoneInstanceId, zoneReplacement, buildingReplacement }, () =>
        {
            ConstructionValues.Id(orderId);
            ConstructionValues.Id(slotId);
            if (Destroyed) { return Failure("colony-destroyed"); }
            if (Orders.Count >= 4096) { return Failure("capacity-limit"); }
            if (quote.Gates.Count != 0) { return Failure("dependency-unresolved"); }
            if (Orders.ContainsKey(orderId)) { return Failure("invalid-order"); }
            if (!Slots.TryGetValue(slotId, out var slot) || slot.Kind != quote.Kind || Orders.Values.Any(order => order.Status == ConstructionOrderStatus.Pending && order.SlotId == slotId)) { return Failure("slot-unavailable"); }
            if (slot.Kind == ConstructionKind.Building && PendingZoneReplacement(slot.ZoneInstanceId)) return Failure("zone-order-cancel-required");
            if (buildingReplacement is not null)
            {
                ConstructionValues.Id(buildingReplacement.SourceDefinitionId);
                ConstructionValues.Id(buildingReplacement.ZoneInstanceId);
                if (upgradeFrom is not null || upgradeZoneInstanceId is not null || zoneReplacement is not null || quote.Kind != ConstructionKind.Building
                    || quote.DefinitionId == buildingReplacement.SourceDefinitionId || !BuildingReplacementMatches(slotId, buildingReplacement))
                    return Failure("building-replacement-source-mismatch");
            }
            else if (zoneReplacement is not null)
            {
                if (upgradeFrom is not null || upgradeZoneInstanceId is not null || quote.Kind != ConstructionKind.Zone
                    || quote.DefinitionId == zoneReplacement.SourceDefinitionId || !ReplacementMatches(slotId, zoneReplacement, Installed))
                    return Failure("zone-replacement-source-mismatch");
                if (Orders.Values.Any(order => order.Status == ConstructionOrderStatus.Pending
                    && Slots[order.SlotId].Kind == ConstructionKind.Building && Slots[order.SlotId].ZoneInstanceId == zoneReplacement.ZoneInstanceId))
                    return Failure("zone-building-order-cancel-required");
            }
            else if (upgradeFrom is null)
            {
                if (Installed.ContainsKey(slotId) || upgradeZoneInstanceId is not null) { return Failure("slot-unavailable"); }
            }
            else if (quote.Kind != ConstructionKind.Building || upgradeFrom == quote.DefinitionId
                || !Installed.TryGetValue(slotId, out var previous) || previous.Kind != ConstructionKind.Building || previous.DefinitionId != upgradeFrom
                || slot.ZoneInstanceId is null || slot.ZoneInstanceId != upgradeZoneInstanceId)
            { return Failure("upgrade-source-mismatch"); }
            if (quote.Kind is not (ConstructionKind.Building or ConstructionKind.District or ConstructionKind.Zone)) { return Failure("operation-lifecycle-unresolved"); }
            if (quote.Kind == ConstructionKind.Zone && (slot.DistrictInstanceId is null || slot.ZoneSlotDefinitionId is null)) { return Failure("zone-slot-binding-unresolved"); }
            if (quote.PlanetLimit is { } limit && Installed.Values.Count(item => item.Kind == ConstructionKind.Building && item.DefinitionId == quote.DefinitionId)
                + Orders.Values.Count(item => item.Status == ConstructionOrderStatus.Pending && item.Quote.DefinitionId == quote.DefinitionId) >= limit) { return Failure("planet-limit-reached"); }
            if (PendingOrderIds.Any(id => Orders[id].Quote.Kind != quote.Kind)) { return Failure("queue-kind-sharing-unresolved"); }
            if (!bank.Accounts.TryGetValue(actorId, out var account) || quote.Costs.Any(pair => !account.TryGetValue(pair.Key, out var balance) || balance < pair.Value || !bank.TryGetStorage(actorId, pair.Key, out _))) { return Failure("insufficient-resources"); }
            var order = new ConstructionOrder(orderId, slotId, actorId, quote, 0, ConstructionOrderStatus.Pending, upgradeFrom, upgradeZoneInstanceId,
                ZoneReplacement: zoneReplacement, BuildingReplacement: buildingReplacement);
            return Candidate(Orders.Values.Append(order), Installed.Values, PendingOrderIds.Append(orderId), quote.Costs.Select(pair => new ConstructionResourceDelta(actorId, pair.Key, -pair.Value)));
        }, requestIdentity);
    }

    public ConstructionLiveResult Cancel(string commandId, int actorId, long revision, string orderId, ConstructionBankSnapshot bank, string? requestIdentity = null)
    {
        return Apply(commandId, actorId, revision, new { Operation = "cancel", orderId }, () =>
        {
            if (!Orders.TryGetValue(orderId, out var order) || order.Status != ConstructionOrderStatus.Pending) { return Failure("order-unavailable"); }
            return Candidate(Orders.Values.Select(item => item.Id == orderId ? item with { Status = ConstructionOrderStatus.Cancelled, CancellationReason = ConstructionCancellationReason.Player } : item), Installed.Values, PendingOrderIds.Where(id => id != orderId), bank.Refund(order));
        }, requestIdentity);
    }

    public ConstructionLiveResult SetBuildingEnabled(string commandId, int actorId, long revision, string slotId, string definitionId, bool enabled, string? requestIdentity = null)
        => Apply(commandId, actorId, revision, new { Operation = "set-building-enabled", slotId, definitionId, enabled }, () =>
        {
            if (Destroyed) return Failure("colony-destroyed");
            if (!Slots.TryGetValue(slotId, out var slot) || slot.Kind != ConstructionKind.Building
                || !Installed.TryGetValue(slotId, out var installed) || installed.DefinitionId != definitionId || installed.Kind != ConstructionKind.Building)
                return Failure("building-target-mismatch");
            if (installed.Enabled == enabled) return Failure("building-state-unchanged");
            return Candidate(Orders.Values, Installed.Values.Select(item => item.SlotId == slotId ? item with { Enabled = enabled } : item), PendingOrderIds, []);
        }, requestIdentity);

    public ConstructionLiveResult DemolishBuilding(string commandId, int actorId, long revision, string slotId, string definitionId, string? requestIdentity = null)
        => Apply(commandId, actorId, revision, new { Operation = "demolish-building", slotId, definitionId }, () =>
        {
            if (Destroyed) return Failure("colony-destroyed");
            if (!Slots.TryGetValue(slotId, out var slot) || slot.Kind != ConstructionKind.Building
                || !Installed.TryGetValue(slotId, out var installed) || installed.Kind != ConstructionKind.Building || installed.DefinitionId != definitionId)
                return Failure("building-target-mismatch");
            if (Orders.Values.Any(order => order.Status == ConstructionOrderStatus.Pending && order.SlotId == slotId))
                return Failure("building-order-cancel-required");
            if (PendingZoneReplacement(slot.ZoneInstanceId)) return Failure("zone-order-cancel-required");
            return Candidate(Orders.Values, Installed.Values.Where(item => item.SlotId != slotId), PendingOrderIds, []);
        }, requestIdentity);

    private bool PendingZoneReplacement(string? zoneInstanceId) => zoneInstanceId is not null && Orders.Values.Any(order =>
        order.Status == ConstructionOrderStatus.Pending && order.ZoneReplacement?.ZoneInstanceId == zoneInstanceId);

    private bool BuildingReplacementMatches(string slotId, ConstructionBuildingReplacement replacement)
        => Slots.TryGetValue(slotId, out var slot) && slot.Kind == ConstructionKind.Building && slot.ZoneInstanceId == replacement.ZoneInstanceId
            && Installed.TryGetValue(slotId, out var building) && building.Kind == ConstructionKind.Building && building.DefinitionId == replacement.SourceDefinitionId;

    private bool ReplacementMatches(string slotId, ConstructionZoneReplacement replacement, IReadOnlyDictionary<string, ConstructionInstalled> installed)
        => Slots.TryGetValue(slotId, out var slot) && slot.Kind == ConstructionKind.Zone && slot.ZoneInstanceId == replacement.ZoneInstanceId
            && installed.TryGetValue(slotId, out var previous) && previous.Kind == ConstructionKind.Zone && previous.DefinitionId == replacement.SourceDefinitionId
            && replacement.RemovedBuildings.All(pair => Slots.TryGetValue(pair.Key, out var building) && building.Kind == ConstructionKind.Building
                && building.ZoneInstanceId == replacement.ZoneInstanceId && installed.TryGetValue(pair.Key, out var item)
                && item.Kind == ConstructionKind.Building && item.DefinitionId == pair.Value);

    public ConstructionLiveResult Reorder(string commandId, int actorId, long revision, IReadOnlyList<string> orderedIds, string? requestIdentity = null, bool preserveActiveHead = false)
    {
        var copy = orderedIds.Take(4097).ToArray();
        return Apply(commandId, actorId, revision, new { Operation = "reorder", copy, preserveActiveHead }, () =>
        {
            if (copy.Length != PendingOrderIds.Count || !copy.Order(StringComparer.Ordinal).SequenceEqual(PendingOrderIds.Order(StringComparer.Ordinal)))
            { return Failure("invalid-queue-membership"); }
            if (preserveActiveHead && copy.Length > 0 && copy[0] != PendingOrderIds[0]) { return Failure("active-order-locked"); }
            if (copy.SequenceEqual(PendingOrderIds)) { return Failure("queue-unchanged"); }
            return Candidate(Orders.Values, Installed.Values, copy, []);
        }, requestIdentity);
    }

    /// <summary>Day identity/revision задаёт сессия; завершение возвращается до месячного settlement.</summary>
    public ConstructionLiveResult AdvanceDay(string dayId, long revision, int activeCapacity, IReadOnlyDictionary<string, decimal?> liveSpeed, IReadOnlyDictionary<string, bool?> eligibility, ConstructionBankSnapshot bank)
    {
        var speeds = liveSpeed.ToFrozenDictionary(StringComparer.Ordinal);
        var conditions = eligibility.ToFrozenDictionary(StringComparer.Ordinal);
        return Apply(dayId, OwnerId, revision, new { Operation = "day", activeCapacity, speeds, conditions },
            () => ProcessPending(activeCapacity, speeds, conditions, bank, true));
    }

    /// <summary>Доверенный пересчёт внутри уже принятого candidate: без новой команды, revision или дневной работы.</summary>
    public ConstructionLiveResult RefreshPendingConditions(IReadOnlyDictionary<string, decimal?> liveSpeed, IReadOnlyDictionary<string, bool?> eligibility, ConstructionBankSnapshot bank)
    {
        try { return ProcessPending(0, liveSpeed, eligibility, bank, false); }
        catch (ResourceStorageUnresolvedException error) { return Failure("resource-storage-unresolved:" + error.ResourceId); }
        catch (OverflowException) { return Failure("arithmetic-overflow"); }
        catch (ArgumentException) { return Failure("invalid-context"); }
        catch (KeyNotFoundException) { return Failure("bank-context-unresolved"); }
    }

    private ConstructionLiveResult ProcessPending(int activeCapacity, IReadOnlyDictionary<string, decimal?> speeds, IReadOnlyDictionary<string, bool?> conditions, ConstructionBankSnapshot bank, bool advanceDay)
    {
        if (activeCapacity is < 0 or > 4096) { return Failure("invalid-queue-capacity"); }
        var orders = Orders.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var installed = Installed.Values.ToList();
        var slots = Slots.ToDictionary(StringComparer.Ordinal);
        var cancelled = new List<ConstructionOrder>();
        var active = PendingOrderIds.Take(activeCapacity).ToHashSet(StringComparer.Ordinal);
        foreach (var id in PendingOrderIds)
        {
            var order = orders[id];
            if (bank.AbsentPayerIds.Contains(order.PayerId))
            {
                cancelled.Add(order);
                orders[id] = order with { Status = ConstructionOrderStatus.Cancelled, CancellationReason = ConstructionCancellationReason.PayerMissing };
                continue;
            }
            if (speeds.TryGetValue(id, out var knownBonus) && knownBonus is <= -1)
            {
                cancelled.Add(order);
                orders[id] = order with { Status = ConstructionOrderStatus.Cancelled, CancellationReason = ConstructionCancellationReason.NonPositiveSpeed };
                continue;
            }
            decimal? factor = null;
            if (knownBonus is { } known)
            {
                try { factor = checked(1 + known); }
                catch (OverflowException)
                {
                    cancelled.Add(order);
                    orders[id] = order with { Status = ConstructionOrderStatus.Cancelled, CancellationReason = ConstructionCancellationReason.CalculationFailed };
                    continue;
                }
            }
            if (!conditions.TryGetValue(id, out var allowed) || allowed is null) { return Failure("condition-context-unresolved"); }
            if (order.UpgradeFromDefinitionId is { } previous && (!Installed.TryGetValue(order.SlotId, out var source)
                || source.DefinitionId != previous || Slots[order.SlotId].ZoneInstanceId != order.UpgradeZoneInstanceId)) { allowed = false; }
            if (order.ZoneReplacement is { } replacement && !ReplacementMatches(order.SlotId, replacement,
                installed.ToDictionary(item => item.SlotId, StringComparer.Ordinal))) allowed = false;
            if (order.BuildingReplacement is { } buildingReplacement && !BuildingReplacementMatches(order.SlotId, buildingReplacement)) allowed = false;
            if (order.Quote.PlanetLimit is { } limit && installed.Count(item => item.Kind == ConstructionKind.Building && item.DefinitionId == order.Quote.DefinitionId)
                + orders.Values.Count(item => item.Id != id && item.Status == ConstructionOrderStatus.Pending && item.Quote.DefinitionId == order.Quote.DefinitionId) >= limit) { allowed = false; }
            if (!allowed.Value)
            {
                cancelled.Add(order);
                orders[id] = order with { Status = ConstructionOrderStatus.Cancelled, CancellationReason = ConstructionCancellationReason.ConditionLost };
                continue;
            }
            if (!active.Contains(id)) { continue; }
            if (factor is null) { return Failure("speed-context-unresolved"); }
            var work = checked(order.CompletedWork + Math.Min(factor.Value, order.Quote.RequiredWork - order.CompletedWork));
            var complete = work == order.Quote.RequiredWork;
            orders[id] = order with { CompletedWork = work, Status = complete ? ConstructionOrderStatus.Completed : ConstructionOrderStatus.Pending };
            if (complete)
            {
                var enabled = order.UpgradeFromDefinitionId is null || installed.Single(item => item.SlotId == order.SlotId).Enabled;
                if (order.ZoneReplacement is { } replacing)
                {
                    installed.RemoveAll(item => replacing.RemovedBuildings.ContainsKey(item.SlotId));
                    foreach (var child in slots.Values.Where(slot => slot.Kind == ConstructionKind.Building && slot.ZoneInstanceId == replacing.ZoneInstanceId).ToArray())
                        slots[child.Id] = child with { ZoneDefinitionId = order.Quote.DefinitionId };
                }
                if (order.UpgradeFromDefinitionId is not null || order.ZoneReplacement is not null || order.BuildingReplacement is not null) { installed.RemoveAll(item => item.SlotId == order.SlotId); }
                installed.Add(new(order.SlotId, order.Quote.DefinitionId, order.Quote.Kind, order.Quote.InfrastructureUpkeep, order.Quote.JobCapacity, enabled));
            }
        }
        var pending = PendingOrderIds.Where(id => orders[id].Status == ConstructionOrderStatus.Pending);
        var refunds = bank.RefundAll(cancelled);
        return advanceDay ? Candidate(orders.Values, installed, pending, refunds, slots)
            : new(true, "accepted", new(this, Revision, OwnerId, orders.Values, installed, pending, _receipts.ToDictionary()), refunds, Revision);
    }

    public ConstructionLiveResult ChangeOwner(string commandId, long revision, int newOwnerId, bool destroyed, ConstructionBankSnapshot bank)
    {
        return Apply(commandId, OwnerId, revision, new { Operation = "owner-change", newOwnerId, destroyed }, () =>
        {
            if (newOwnerId <= 0 || !destroyed && !bank.Accounts.ContainsKey(newOwnerId)) { return Failure("invalid-owner"); }
            var deltas = destroyed ? Array.Empty<ConstructionResourceDelta>() : bank.RefundAll(PendingOrderIds.Select(id => Orders[id]));
            var result = Candidate(Orders.Values.Select(order => order.Status == ConstructionOrderStatus.Pending ? order with { Status = ConstructionOrderStatus.Cancelled,
                CancellationReason = destroyed ? ConstructionCancellationReason.Destroyed : bank.AbsentPayerIds.Contains(order.PayerId)
                    ? ConstructionCancellationReason.PayerMissing : ConstructionCancellationReason.OwnershipChanged } : order), Installed.Values, [], deltas);
            return result with { State = new ConstructionLiveRuntime(result.State, result.State.Revision, newOwnerId, result.State.Orders.Values, result.State.Installed.Values, [], result.State._receipts.ToDictionary(), destroyed) };
        });
    }

    private ConstructionLiveResult Candidate(IEnumerable<ConstructionOrder> orders, IEnumerable<ConstructionInstalled> installed, IEnumerable<string> pending, IEnumerable<ConstructionResourceDelta> deltas,
        IReadOnlyDictionary<string, ConstructionLiveSlot>? slots = null)
        => new(true, "accepted", new(this, checked(Revision + 1), OwnerId, orders, installed, pending, _receipts.ToDictionary(), slots: slots?.ToFrozenDictionary(StringComparer.Ordinal)), Array.AsReadOnly(deltas.ToArray()), checked(Revision + 1));

    private ConstructionLiveResult Apply(string commandId, int actorId, long revision, object payload, Func<ConstructionLiveResult> operation, string? requestIdentity = null)
    {
        if (actorId != OwnerId) { return Failure("not-owner"); }
        try
        {
            ConstructionValues.Id(commandId);
            if (revision < 0) { return Failure("invalid-revision"); }
            var fingerprint = Fingerprint(actorId, revision, requestIdentity is null ? payload : new { Request = requestIdentity });
            if (_receipts.TryGetValue(commandId, out var receipt)) { return receipt.Fingerprint == fingerprint ? new(true, "duplicate", this, [], receipt.AppliedRevision) : Failure("command-id-conflict"); }
            if (revision != Revision) { return Failure("stale-revision"); }
            if (_receipts.Count >= 4096) { return Failure("capacity-limit"); }
            var result = operation();
            if (!result.Accepted) { return result; }
            var receipts = _receipts.ToDictionary();
            receipts.Add(commandId, new(fingerprint, result.State.Revision));
            return result with { State = new(result.State, result.State.Revision, result.State.OwnerId, result.State.Orders.Values, result.State.Installed.Values, result.State.PendingOrderIds, receipts) };
        }
        catch (ResourceStorageUnresolvedException error) { return Failure("resource-storage-unresolved:" + error.ResourceId); }
        catch (OverflowException) { return Failure("arithmetic-overflow"); }
        catch (ArgumentException) { return Failure("invalid-context"); }
        catch (KeyNotFoundException) { return Failure("bank-context-unresolved"); }
    }

    private ConstructionLiveResult Failure(string code) => new(false, code, this, []);
    private static ConstructionInstalled CopyInstalled(ConstructionInstalled item) => new(ConstructionValues.Id(item.SlotId), ConstructionValues.Id(item.DefinitionId), item.Kind, ConstructionValues.Amounts(item.InfrastructureUpkeep), ConstructionValues.Amounts(item.JobCapacity), item.Enabled);
    private static string Fingerprint(int actorId, long revision, object payload) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { actorId, revision, payload }))));
}

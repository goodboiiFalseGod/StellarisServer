using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text.Json;
using Stellaris.Core.Economy;

namespace Stellaris.Core.Construction;

public sealed record ConstructionColonyBinding(string ColonyId, int OwnerId, EntityId Planet);
public sealed record ConstructionColonyWork(string ColonyId, long ExpectedRevision, IReadOnlyList<ConstructionWork> Work);
public sealed record ConstructionAggregateTransition(bool Accepted, string Code, ConstructionAggregate State, long? AppliedRevision);

/// <summary>Owns isolated construction/economy state; native policies and outer-session publication stay explicit.</summary>
public sealed class ConstructionAggregate
{
    private const int LIMIT = 4096;
    private readonly EconomyWorld _economy;
    private readonly IReadOnlyDictionary<int, EntityId> _countries;
    private readonly IReadOnlyDictionary<string, ConstructionColonyBinding> _bindings;
    private readonly IReadOnlyDictionary<string, ConstructionReceipt> _receipts;
    private readonly IReadOnlyDictionary<string, ConstructionInstalledCapacitySnapshot> _capacityReceipts;

    public long Revision { get; }
    public int Month => _economy.Month;
    public IReadOnlyDictionary<string, ConstructionLedger> Ledgers { get; }
    public IReadOnlyDictionary<string, ConstructionInstalledCapacitySnapshot> InstalledCapacity { get; }

    public ConstructionAggregate(EconomyWorld economy, IReadOnlyDictionary<int, EntityId> countries, IEnumerable<ConstructionColonyBinding> bindings, IEnumerable<ConstructionLedger> ledgers)
    {
        ArgumentNullException.ThrowIfNull(economy);
        ArgumentNullException.ThrowIfNull(countries);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(ledgers);
        if (countries.Count == 0 || countries.Count > LIMIT || countries.Keys.Any(id => id <= 0) || countries.Values.Distinct().Count() != countries.Count) { throw new ArgumentException("Invalid construction country bindings."); }
        _economy = economy.Fork();
        _countries = ReadOnly(countries.ToDictionary(pair => pair.Key, pair => pair.Value));
        foreach (var country in _countries.Values) { _ = _economy.GetBalances(country); }
        var colonyArray = bindings.Take(LIMIT + 1).ToArray();
        var ledgerArray = ledgers.Take(LIMIT + 1).ToArray();
        if (colonyArray.Length > LIMIT || ledgerArray.Length > LIMIT || colonyArray.Any(binding => binding is null) || ledgerArray.Any(ledger => ledger is null) || colonyArray.Select(binding => binding.Planet).Distinct().Count() != colonyArray.Length) { throw new ArgumentException("Invalid construction colony bindings."); }
        foreach (var binding in colonyArray) { ConstructionValues.Id(binding.ColonyId); }
        _bindings = ReadOnly(colonyArray.ToDictionary(binding => binding.ColonyId, StringComparer.Ordinal));
        var indexed = ledgerArray.ToDictionary(ledger => ledger.Identity.ColonyId, StringComparer.Ordinal);
        if (!indexed.Keys.Order(StringComparer.Ordinal).SequenceEqual(_bindings.Keys.Order(StringComparer.Ordinal))) { throw new ArgumentException("Missing construction colony ledger."); }
        foreach (var binding in _bindings.Values)
        {
            var ledger = indexed[binding.ColonyId];
            if (!_countries.TryGetValue(binding.OwnerId, out var country) || ledger.OwnerId != binding.OwnerId || _economy.GetPlanetOwner(binding.Planet) != country || ledger.Accounts.Keys.Any(id => !_countries.ContainsKey(id))) { throw new ArgumentException("Construction owner/planet binding mismatch."); }
            if (_economy.GetInstalledJobCapacity(binding.Planet) is not null || _economy.GetInstalledJobEconomyEffects(binding.Planet) is not null) { throw new ArgumentException("Installed effects require their retained source snapshot; aggregate hydration is not supported."); }
        }
        Ledgers = Reconcile(_economy, indexed);
        _receipts = ReadOnly(new Dictionary<string, ConstructionReceipt>(StringComparer.Ordinal));
        _capacityReceipts = ReadOnly(new Dictionary<string, ConstructionInstalledCapacitySnapshot>(StringComparer.Ordinal));
        InstalledCapacity = ReadOnly(new Dictionary<string, ConstructionInstalledCapacitySnapshot>(StringComparer.Ordinal));
    }

    private ConstructionAggregate(ConstructionAggregate source, EconomyWorld economy, IReadOnlyDictionary<string, ConstructionLedger> ledgers, long revision, Dictionary<string, ConstructionReceipt> receipts, string commandId, ConstructionInstalledCapacitySnapshot? capacity)
    {
        _economy = economy;
        _countries = source._countries;
        _bindings = source._bindings;
        Ledgers = ledgers;
        Revision = revision;
        _receipts = ReadOnly(receipts);
        var installed = source.InstalledCapacity.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var capacityReceipts = source._capacityReceipts.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        if (capacity is not null)
        {
            installed[capacity.ColonyId] = capacity;
            capacityReceipts.Add(commandId, capacity);
        }
        InstalledCapacity = ReadOnly(installed);
        _capacityReceipts = ReadOnly(capacityReceipts);
    }

    public EconomyWorld ForkEconomy() => _economy.Fork();
    public string EconomyHash() => _economy.StateHash();
    public decimal GetBalance(int participantId, string resource) => _economy.GetBalance(_countries[participantId], resource);

    /// <summary>Trusted full installed-capacity snapshot replacement, not a player construction command.</summary>
    public ConstructionAggregateTransition ReplaceInstalledCapacity(string commandId, long expectedRevision, string colonyId, IEnumerable<ConstructionInstalledCapacityObject> objects)
    {
        ArgumentNullException.ThrowIfNull(objects);
        if (colonyId is null || !_bindings.TryGetValue(colonyId, out var binding)) { return Reject("unknown-colony"); }
        var copied = objects.Take(LIMIT + 1).ToArray();
        if (copied.Length > LIMIT) { return Reject("capacity-limit"); }
        if (copied.Any(instance => instance is null || !ValidId(instance.InstanceId) || instance.Assessment is null || instance.DistrictId is not null && !ValidId(instance.DistrictId) || instance.DistrictLevel is < 0 or > 4096) || copied.Select(instance => instance.InstanceId).Distinct(StringComparer.Ordinal).Count() != copied.Length) { return Reject("invalid-installed-snapshot"); }
        if (commandId is not null && _capacityReceipts.TryGetValue(commandId, out var previous) && (previous.ColonyId != colonyId || previous.Objects.Count != copied.Length || previous.Objects.Where((instance, index) => instance.InstanceId != copied[index].InstanceId || instance.DistrictId != copied[index].DistrictId || instance.DistrictLevel != copied[index].DistrictLevel || !ReferenceEquals(instance.Assessment, copied[index].Assessment)).Any())) { return Reject("command-id-conflict"); }
        return Apply(commandId!, expectedRevision, new { Operation = "installed-capacity", colonyId, Instances = copied.Select(instance => new { instance.InstanceId, instance.Assessment.DefinitionId, instance.DistrictId, instance.DistrictLevel }) }, (world, ledgers) =>
        {
            try
            {
                if (world.GetStructuralProductionSources(binding.Planet).Count != 0) { return new("structural-production-snapshot-unresolved", world); }
                if (world.GetInstalledCountryResourceCapacityAdditions(binding.Planet).Count != 0) { return new("country-capacity-snapshot-unresolved", world); }
                var snapshot = ConstructionInstalledCapacitySnapshot.Build(colonyId, checked(Revision + 1), copied);
                world.SetInstalledCapacity(binding.Planet, snapshot.JobCapacity, snapshot.Housing, snapshot.ZoneBuildingSlotsAdd);
                world.SetInstalledJobEconomyEffects(binding.Planet, world.ResolveInstalledJobEconomyEffects(snapshot.JobEconomyEffects, snapshot.CategoryJobEffects));
                return new(null, world, snapshot);
            }
            catch (InvalidOperationException) { return new("installed-capacity-unresolved", world); }
        });
    }

    public ConstructionAggregateTransition Enqueue(string commandId, int actorId, long expectedRevision, string colonyId, long expectedColonyRevision, string orderId, string slotId, ConstructionQuote quote)
    {
        if (!Owns(actorId, colonyId)) { return Reject("not-owner"); }
        ArgumentNullException.ThrowIfNull(quote);
        if (!ValidId(orderId) || !ValidId(slotId)) { return Reject("invalid-identifier"); }
        return Apply(commandId, expectedRevision, new { Operation = "enqueue", actorId, colonyId, expectedColonyRevision, orderId, slotId, quote }, (world, ledgers) =>
        {
            var result = ledgers[colonyId].Enqueue(LedgerCommand(commandId), actorId, expectedColonyRevision, orderId, slotId, quote);
            return PublishLedger(result, colonyId, world, ledgers);
        });
    }

    public ConstructionAggregateTransition CancelPaid(string commandId, int actorId, long expectedRevision, string colonyId, long expectedColonyRevision, string orderId)
    {
        if (!Owns(actorId, colonyId)) { return Reject("not-owner"); }
        if (!ValidId(orderId)) { return Reject("invalid-identifier"); }
        return Apply(commandId, expectedRevision, new { Operation = "cancel-paid", actorId, colonyId, expectedColonyRevision, orderId }, (world, ledgers) =>
        {
            if (!ledgers[colonyId].Orders.TryGetValue(orderId, out var order)) { return new("order-unavailable", world); }
            var caps = order.Quote.Costs.Where(pair => pair.Value != 0).ToDictionary(pair => pair.Key,
                pair => world.GetStoragePolicy(pair.Key) == ResourceStoragePolicy.SourceCapped ? world.GetEffectiveResourceCapacity(_countries[order.PayerId], pair.Key) : world.GetResourceCapacity(pair.Key), StringComparer.Ordinal);
            var result = ledgers[colonyId].CancelPaid(LedgerCommand(commandId), actorId, expectedColonyRevision, orderId, caps);
            return PublishLedger(result, colonyId, world, ledgers);
        });
    }

    public ConstructionAggregateTransition Cancel(string commandId, int actorId, long expectedRevision, string colonyId, long expectedColonyRevision, string orderId, ConstructionCancellationReason reason, ConstructionRefund? refund)
    {
        if (!Owns(actorId, colonyId)) { return Reject("not-owner"); }
        if (!ValidId(orderId)) { return Reject("invalid-identifier"); }
        return Apply(commandId, expectedRevision, new { Operation = "cancel", actorId, colonyId, expectedColonyRevision, orderId, reason, refund }, (world, ledgers) =>
        {
            var result = ledgers[colonyId].Cancel(LedgerCommand(commandId), actorId, expectedColonyRevision, orderId, reason, refund);
            return PublishLedger(result, colonyId, world, ledgers);
        });
    }

    public ConstructionAggregateTransition ConfigureQueues(string commandId, int actorId, long expectedRevision, string colonyId, long expectedColonyRevision, ConstructionQueueSnapshot? snapshot)
    {
        if (!Owns(actorId, colonyId)) { return Reject("not-owner"); }
        return Apply(commandId, expectedRevision, new { Operation = "configure-queues", actorId, colonyId, expectedColonyRevision, snapshot }, (world, ledgers) =>
        {
            var result = ledgers[colonyId].ConfigureQueues(LedgerCommand(commandId), actorId, expectedColonyRevision, snapshot);
            return PublishLedger(result, colonyId, world, ledgers);
        });
    }

    public ConstructionAggregateTransition AdvanceDay(string commandId, long expectedRevision, bool settleMonth, IEnumerable<ConstructionColonyWork> plans)
    {
        return AdvancePlanned(commandId, expectedRevision, "daily-completion-before-month-no-carry", ConstructionSettlementPhase.CompletionBeforeSettlement, settleMonth, plans, true);
    }

    public ConstructionAggregateTransition Advance(string commandId, long expectedRevision, string? policyId, ConstructionSettlementPhase phase, bool settleMonth, IEnumerable<ConstructionColonyWork> plans)
    {
        return AdvancePlanned(commandId, expectedRevision, policyId, phase, settleMonth, plans, false);
    }

    private ConstructionAggregateTransition AdvancePlanned(string commandId, long expectedRevision, string? policyId, ConstructionSettlementPhase phase, bool settleMonth, IEnumerable<ConstructionColonyWork> plans, bool daily)
    {
        if (string.IsNullOrWhiteSpace(policyId) || policyId.Length > 160 || policyId.Any(char.IsControl) || !Enum.IsDefined(phase)) { return Reject("dependency-unresolved"); }
        ArgumentNullException.ThrowIfNull(plans);
        var copied = new List<ConstructionColonyWork>();
        var count = 0;
        foreach (var plan in plans.Take(LIMIT + 1))
        {
            if (plan is null || string.IsNullOrWhiteSpace(plan.ColonyId) || plan.ColonyId.Length > 160 || plan.ColonyId.Any(char.IsControl) || plan.Work is null) { return Reject("invalid-progress"); }
            var work = plan.Work.Take(LIMIT + 1).ToArray();
            if (work.Any(step => step is null || !ValidId(step.OrderId))) { return Reject("invalid-progress"); }
            count = checked(count + work.Length);
            if (count > LIMIT || copied.Count >= LIMIT) { return Reject("capacity-limit"); }
            copied.Add(new(plan.ColonyId, plan.ExpectedRevision, Array.AsReadOnly(work)));
        }
        if (copied.Select(plan => plan.ColonyId).Distinct(StringComparer.Ordinal).Count() != copied.Count) { return Reject("invalid-progress"); }
        return Apply(commandId, expectedRevision, new { Operation = daily ? "advance-day" : "advance", policyId, phase, settleMonth, plans = copied }, (world, ledgers) =>
        {
            if (settleMonth && phase == ConstructionSettlementPhase.CompletionAfterSettlement) { world.AdvanceMonth(); }
            foreach (var plan in copied)
            {
                if (!_bindings.TryGetValue(plan.ColonyId, out var binding)) { return new("unknown-colony", world); }
                IReadOnlyList<ConstructionWork> work = plan.Work;
                if (daily)
                {
                    if (plan.ExpectedRevision != ledgers[plan.ColonyId].Revision) { return new("stale-revision", world); }
                    if (work.Count == 0 || work.Select(step => step.OrderId).Distinct(StringComparer.Ordinal).Count() != work.Count) { return new("invalid-progress", world); }
                    var active = ledgers[plan.ColonyId].Queues?.ActiveOrders();
                    var limited = new List<ConstructionWork>(work.Count);
                    foreach (var step in work)
                    {
                        if (step.Amount <= 0 || !ledgers[plan.ColonyId].Orders.TryGetValue(step.OrderId, out var order) || order.Status != ConstructionOrderStatus.Pending) { return new("invalid-progress", world); }
                        if (active is not null && !active.Contains(step.OrderId)) { continue; }
                        limited.Add(new(step.OrderId, Math.Min(step.Amount, order.Quote.RequiredWork - order.CompletedWork)));
                    }
                    work = limited;
                    if (work.Count == 0) { continue; }
                }
                var result = ConstructionEconomyCandidate.Stage(world, ledgers[plan.ColonyId], binding.Planet, LedgerCommand(commandId), binding.OwnerId, plan.ExpectedRevision, policyId, phase, false, work);
                if (!result.Accepted) { return new(result.Code, world); }
                if (result.Code == "duplicate") { return new("ledger-receipt-conflict", world); }
                world = result.Economy;
                ledgers[plan.ColonyId] = result.Ledger;
            }
            if (settleMonth && phase == ConstructionSettlementPhase.CompletionBeforeSettlement) { world.AdvanceMonth(); }
            return new(null, world);
        });
    }

    private ConstructionAggregateTransition Apply(string commandId, long expectedRevision, object payload, Func<EconomyWorld, Dictionary<string, ConstructionLedger>, Staged> stage)
    {
        if (string.IsNullOrWhiteSpace(commandId) || commandId.Length > 160 || commandId.Any(char.IsControl)) { return Reject("invalid-command"); }
        if (expectedRevision < 0) { return Reject("invalid-revision"); }
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { expectedRevision, payload })));
        if (_receipts.TryGetValue(commandId, out var receipt)) { return receipt.Fingerprint == fingerprint ? new(true, "duplicate", this, receipt.AppliedRevision) : Reject("command-id-conflict"); }
        if (expectedRevision != Revision) { return Reject("stale-revision"); }
        if (_receipts.Count >= LIMIT) { return Reject("capacity-limit"); }
        try
        {
            var world = _economy.Fork();
            var ledgers = Ledgers.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            var result = stage(world, ledgers);
            if (result.Error is not null) { return Reject(result.Error); }
            var reconciled = Reconcile(result.Economy, ledgers);
            var revision = checked(Revision + 1);
            var receipts = _receipts.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            receipts.Add(commandId, new(fingerprint, revision));
            return new(true, "applied", new(this, result.Economy, reconciled, revision, receipts, commandId, result.Capacity), revision);
        }
        catch (OverflowException) { return Reject("arithmetic-overflow"); }
        catch (ArgumentException) { return Reject("invalid-economy-context"); }
        catch (ResourceStorageUnresolvedException error) { return Reject("resource-storage-unresolved:" + error.ResourceId); }
        catch (InvalidOperationException) { return Reject("economy-settlement-rejected"); }
    }

    private Staged PublishLedger(ConstructionTransition result, string colonyId, EconomyWorld world, Dictionary<string, ConstructionLedger> ledgers)
    {
        if (!result.Accepted) { return new(result.Code, world); }
        if (result.Code == "duplicate") { return new("ledger-receipt-conflict", world); }
        foreach (var account in result.State.Accounts)
        {
            foreach (var amount in account.Value) { world.SetBalance(_countries[account.Key], amount.Key, amount.Value); }
        }
        ledgers[colonyId] = result.State;
        return new(null, world);
    }

    private IReadOnlyDictionary<string, ConstructionLedger> Reconcile(EconomyWorld world, IReadOnlyDictionary<string, ConstructionLedger> ledgers)
    {
        return ReadOnly(ledgers.ToDictionary(pair => pair.Key, pair => pair.Value.ReconcileAccounts(pair.Value.Accounts.Keys.ToDictionary(id => id, id => world.GetBalances(_countries[id]))), StringComparer.Ordinal));
    }

    private bool Owns(int actorId, string colonyId) => colonyId is not null && _bindings.TryGetValue(colonyId, out var binding) && binding.OwnerId == actorId;
    private static bool ValidId(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 160 && !value.Any(char.IsControl);
    private ConstructionAggregateTransition Reject(string code) => new(false, code, this, null);
    private static string LedgerCommand(string commandId) => "aggregate-" + Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(commandId)));
    private static IReadOnlyDictionary<TKey, TValue> ReadOnly<TKey, TValue>(Dictionary<TKey, TValue> values) where TKey : notnull => new ReadOnlyDictionary<TKey, TValue>(values);
    private sealed record Staged(string? Error, EconomyWorld Economy, ConstructionInstalledCapacitySnapshot? Capacity = null);
}

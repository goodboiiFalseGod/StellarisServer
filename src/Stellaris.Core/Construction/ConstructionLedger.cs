using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Stellaris.Core.Construction;

public sealed class ConstructionLedger
{
    private const int LIMIT = 4096;
    private readonly IReadOnlyDictionary<string, ConstructionReceipt> _receipts;

    public ConstructionIdentity Identity { get; }
    public int OwnerId { get; }
    public long Revision { get; }
    public IReadOnlyList<string> Slots { get; }
    public IReadOnlyDictionary<int, IReadOnlyDictionary<string, decimal>> Accounts { get; }
    public IReadOnlyDictionary<string, ConstructionOrder> Orders { get; }
    public IReadOnlyDictionary<string, ConstructionInstalled> Installed { get; }
    public ConstructionQueueSnapshot? Queues { get; }

    public ConstructionLedger(ConstructionIdentity identity, int ownerId, IReadOnlyDictionary<int, IReadOnlyDictionary<string, decimal>> accounts, IEnumerable<string> slots)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ConstructionValues.Id(identity.ColonyId);
        if (!Enum.IsDefined(identity.AuthorityMode) || identity.IsMobile != (identity.CarrierId is not null)) { throw new ArgumentException("Invalid colony authority/carrier identity."); }
        if (identity.CarrierId is not null) { ConstructionValues.Id(identity.CarrierId); }
        if (identity.LocationSystemId is not null) { ConstructionValues.Id(identity.LocationSystemId); }
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(slots);
        if (ownerId <= 0 || !accounts.ContainsKey(ownerId) || accounts.Count > LIMIT || accounts.Keys.Any(id => id <= 0)) { throw new ArgumentException("Invalid construction resource accounts."); }
        var slotArray = slots.Take(LIMIT + 1).Select(ConstructionValues.Id).ToArray();
        if (slotArray.Length > LIMIT || slotArray.Distinct(StringComparer.Ordinal).Count() != slotArray.Length) { throw new ArgumentException("Invalid construction slots."); }
        Identity = identity;
        OwnerId = ownerId;
        Slots = Array.AsReadOnly(slotArray);
        Accounts = ReadOnly(accounts.ToDictionary(pair => pair.Key, pair => ConstructionValues.Amounts(pair.Value)));
        Orders = ReadOnly(new Dictionary<string, ConstructionOrder>(StringComparer.Ordinal));
        Installed = ReadOnly(new Dictionary<string, ConstructionInstalled>(StringComparer.Ordinal));
        _receipts = ReadOnly(new Dictionary<string, ConstructionReceipt>(StringComparer.Ordinal));
    }

    private ConstructionLedger(ConstructionLedger previous, int ownerId, long revision, Dictionary<int, IReadOnlyDictionary<string, decimal>> accounts, Dictionary<string, ConstructionOrder> orders, Dictionary<string, ConstructionInstalled> installed, Dictionary<string, ConstructionReceipt> receipts, ConstructionQueueSnapshot? queues)
    {
        Identity = previous.Identity;
        OwnerId = ownerId;
        Revision = revision;
        Slots = previous.Slots;
        Accounts = ReadOnly(accounts);
        Orders = ReadOnly(orders);
        Installed = ReadOnly(installed);
        Queues = queues;
        _receipts = ReadOnly(receipts);
    }

    internal ConstructionLedger ReconcileAccounts(IReadOnlyDictionary<int, IReadOnlyDictionary<string, decimal>> accounts)
    {
        if (!accounts.Keys.Order().SequenceEqual(Accounts.Keys.Order())) { throw new ArgumentException("Construction account identities cannot change during reconciliation."); }
        return new(this, OwnerId, Revision, accounts.ToDictionary(pair => pair.Key, pair => ConstructionValues.Amounts(pair.Value)), Orders.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal), Installed.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal), _receipts.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal), Queues);
    }

    public ConstructionTransition ConfigureQueues(string commandId, int actorId, long expectedRevision, ConstructionQueueSnapshot? snapshot)
    {
        return Apply(commandId, actorId, expectedRevision, new { Operation = "configure-queues", snapshot }, candidate =>
        {
            if (snapshot is null) { return "dependency-unresolved"; }
            if (snapshot.Revision != expectedRevision) { return "stale-queue-revision"; }
            var ids = snapshot.Queues.SelectMany(queue => queue.OrderIds).Order(StringComparer.Ordinal);
            var pending = Orders.Values.Where(order => order.Status == ConstructionOrderStatus.Pending).Select(order => order.Id).Order(StringComparer.Ordinal);
            if (!ids.SequenceEqual(pending)) { return "invalid-queue-membership"; }
            candidate.Queues = snapshot;
            return null;
        }, []);
    }

    public ConstructionTransition Enqueue(string commandId, int actorId, long expectedRevision, string orderId, string slotId, ConstructionQuote quote)
    {
        ArgumentNullException.ThrowIfNull(quote);
        return Apply(commandId, actorId, expectedRevision, new { Operation = "enqueue", orderId, slotId, quote }, candidate =>
        {
            if (quote.Gates.Count != 0) { return "dependency-unresolved"; }
            if (quote.Kind == ConstructionKind.BlockerClearance) { return "clearance-context-unresolved"; }
            if (Orders.Count >= LIMIT) { return "capacity-limit"; }
            if (string.IsNullOrWhiteSpace(orderId) || orderId.Length > 160 || orderId.Any(char.IsControl) || Orders.ContainsKey(orderId)) { return "invalid-order"; }
            if (!Slots.Contains(slotId, StringComparer.Ordinal) || Installed.ContainsKey(slotId) || Orders.Values.Any(order => order.Status == ConstructionOrderStatus.Pending && order.SlotId == slotId)) { return "slot-unavailable"; }
            var balances = Accounts[actorId];
            if (quote.Costs.Any(cost => !balances.TryGetValue(cost.Key, out var available) || available < cost.Value)) { return "insufficient-resources"; }
            var updated = balances.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            foreach (var cost in quote.Costs) { updated[cost.Key] = checked(updated[cost.Key] - cost.Value); }
            candidate.Accounts[actorId] = ConstructionValues.Amounts(updated);
            candidate.Orders.Add(orderId, new ConstructionOrder(orderId, slotId, actorId, quote, 0, ConstructionOrderStatus.Pending));
            candidate.Queues = null;
            return null;
        }, [orderId, slotId]);
    }

    public ConstructionTransition Progress(string commandId, int actorId, long expectedRevision, string phasePolicyId, IEnumerable<ConstructionWork> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        var steps = work.Take(LIMIT + 1).ToArray();
        return Apply(commandId, actorId, expectedRevision, new { Operation = "progress", phasePolicyId, steps }, candidate =>
        {
            if (string.IsNullOrWhiteSpace(phasePolicyId)) { return "dependency-unresolved"; }
            if (steps.Length == 0 || steps.Length > LIMIT || steps.Any(step => step is null) || steps.Select(step => step.OrderId).Distinct(StringComparer.Ordinal).Count() != steps.Length) { return "invalid-progress"; }
            foreach (var step in steps)
            {
                if (step.Amount <= 0 || !Orders.TryGetValue(step.OrderId, out var order) || order.Status != ConstructionOrderStatus.Pending || order.PayerId != OwnerId) { return "invalid-progress"; }
                var completed = checked(order.CompletedWork + step.Amount);
                if (completed > order.Quote.RequiredWork) { return "work-overrun-policy-unresolved"; }
                var finished = completed == order.Quote.RequiredWork;
                candidate.Orders[order.Id] = order with { CompletedWork = completed, Status = finished ? ConstructionOrderStatus.Completed : ConstructionOrderStatus.Pending };
                if (finished)
                {
                    candidate.Installed.Add(order.SlotId, new ConstructionInstalled(order.SlotId, order.Quote.DefinitionId, order.Quote.Kind, order.Quote.InfrastructureUpkeep, order.Quote.JobCapacity));
                }
            }
            return null;
        }, steps.Select(step => step?.OrderId), phasePolicyId);
    }

    public ConstructionTransition CancelPaid(string commandId, int actorId, long expectedRevision, string orderId, IReadOnlyDictionary<string, decimal>? storageCaps)
    {
        if (actorId != OwnerId) { return Reject("not-owner"); }
        if (storageCaps is null) { return Reject("dependency-unresolved"); }
        IReadOnlyDictionary<string, decimal> caps;
        try { caps = ConstructionValues.Amounts(storageCaps); }
        catch (ArgumentException) { return Reject("invalid-refund"); }
        return Apply(commandId, actorId, expectedRevision, new { Operation = "cancel-paid", orderId, caps }, candidate =>
        {
            if (!Orders.TryGetValue(orderId, out var order) || order.Status != ConstructionOrderStatus.Pending) { return "order-unavailable"; }
            var refund = new ConstructionRefund(ConstructionRefund.PAID_CANCELLATION_POLICY, order.Quote.Costs, caps);
            var failure = Refund(candidate, order, refund);
            if (failure is not null) { return failure; }
            candidate.Orders[orderId] = order with { Status = ConstructionOrderStatus.Cancelled };
            return null;
        }, [orderId]);
    }

    public ConstructionTransition Cancel(string commandId, int actorId, long expectedRevision, string orderId, ConstructionCancellationReason reason, ConstructionRefund? refund)
    {
        return Apply(commandId, actorId, expectedRevision, new { Operation = "cancel", orderId, reason, refund }, candidate =>
        {
            if (!Enum.IsDefined(reason) || reason == ConstructionCancellationReason.OwnershipChanged) { return "invalid-cancel-reason"; }
            if (refund?.PolicyId == ConstructionRefund.PAID_CANCELLATION_POLICY && reason != ConstructionCancellationReason.Player) { return "invalid-cancel-reason"; }
            if (!Orders.TryGetValue(orderId, out var order) || order.Status != ConstructionOrderStatus.Pending) { return "order-unavailable"; }
            var failure = Refund(candidate, order, refund);
            if (failure is not null) { return failure; }
            candidate.Orders[orderId] = order with { Status = ConstructionOrderStatus.Cancelled };
            return null;
        }, [orderId]);
    }

    public ConstructionTransition ChangeOwner(string commandId, int actorId, long expectedRevision, int newOwnerId, IReadOnlyDictionary<string, ConstructionRefund> refunds)
    {
        ArgumentNullException.ThrowIfNull(refunds);
        if (refunds.Count > LIMIT) { return Reject("capacity-limit"); }
        var copy = new SortedDictionary<string, ConstructionRefund>(StringComparer.Ordinal);
        foreach (var pair in refunds) { copy.Add(pair.Key, pair.Value); }
        return Apply(commandId, actorId, expectedRevision, new { Operation = "owner-change", newOwnerId, refunds = copy }, candidate =>
        {
            if (newOwnerId == OwnerId || !Accounts.ContainsKey(newOwnerId)) { return "invalid-owner"; }
            var pending = Orders.Values.Where(order => order.Status == ConstructionOrderStatus.Pending).ToArray();
            if (!copy.Keys.Order(StringComparer.Ordinal).SequenceEqual(pending.Select(order => order.Id).Order(StringComparer.Ordinal))) { return "dependency-unresolved"; }
            foreach (var order in pending)
            {
                if (copy[order.Id]?.PolicyId == ConstructionRefund.PAID_CANCELLATION_POLICY) { return "invalid-cancel-reason"; }
                var failure = Refund(candidate, order, copy[order.Id]);
                if (failure is not null) { return failure; }
                candidate.Orders[order.Id] = order with { Status = ConstructionOrderStatus.Cancelled };
            }
            candidate.OwnerId = newOwnerId;
            candidate.Queues = null;
            return null;
        }, copy.Keys);
    }

    private static string? Refund(Candidate candidate, ConstructionOrder order, ConstructionRefund? refund)
    {
        if (refund is null) { return "dependency-unresolved"; }
        var paidPolicy = refund.PolicyId == ConstructionRefund.PAID_CANCELLATION_POLICY;
        if (paidPolicy && (refund.Amounts.Count != order.Quote.Costs.Count || order.Quote.Costs.Any(pair => !refund.Amounts.TryGetValue(pair.Key, out var value) || value != pair.Value))) { return "invalid-refund"; }
        var balances = candidate.Accounts[order.PayerId].ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        foreach (var amount in refund.Amounts)
        {
            if (!order.Quote.Costs.TryGetValue(amount.Key, out var paid) || amount.Value > paid || !balances.ContainsKey(amount.Key)) { return "invalid-refund"; }
            if (amount.Value == 0) { continue; }
            if (!refund.StorageCaps.TryGetValue(amount.Key, out var cap)) { return "dependency-unresolved"; }
            var credit = paidPolicy ? Math.Min(amount.Value, Math.Max(0, cap - balances[amount.Key])) : amount.Value;
            var resulting = checked(balances[amount.Key] + credit);
            if (!paidPolicy && resulting > cap) { return "refund-cap-policy-unresolved"; }
            balances[amount.Key] = resulting;
        }
        candidate.Accounts[order.PayerId] = ConstructionValues.Amounts(balances);
        return null;
    }

    private ConstructionTransition Apply(string commandId, int actorId, long expectedRevision, object payload, Func<Candidate, string?> evaluate, IEnumerable<string?> payloadIds, string? optionalPolicyId = null)
    {
        if (string.IsNullOrWhiteSpace(commandId) || commandId.Length > 160 || commandId.Any(char.IsControl)) { return Reject("invalid-command"); }
        if (actorId != OwnerId) { return Reject("not-owner"); }
        if (expectedRevision < 0) { return Reject("invalid-revision"); }
        if (optionalPolicyId is not null && (optionalPolicyId.Length > 160 || optionalPolicyId.Any(char.IsControl))) { return Reject("invalid-identifier"); }
        if (payloadIds.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > 160 || id.Any(char.IsControl))) { return Reject("invalid-identifier"); }
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { actorId, expectedRevision, payload }))));
        if (_receipts.TryGetValue(commandId, out var previous))
        {
            return previous.Fingerprint == fingerprint ? new ConstructionTransition(true, "duplicate", this, previous.AppliedRevision) : Reject("command-id-conflict");
        }
        if (expectedRevision != Revision) { return Reject("stale-revision"); }
        if (_receipts.Count >= LIMIT) { return Reject("capacity-limit"); }
        var candidate = new Candidate(this);
        try
        {
            var failure = evaluate(candidate);
            if (failure is not null) { return Reject(failure); }
            var revision = checked(Revision + 1);
            candidate.Receipts.Add(commandId, new ConstructionReceipt(fingerprint, revision));
            var queues = candidate.Queues?.RetainPending(revision, candidate.Orders);
            var state = new ConstructionLedger(this, candidate.OwnerId, revision, candidate.Accounts, candidate.Orders, candidate.Installed, candidate.Receipts, queues);
            return new ConstructionTransition(true, "applied", state, revision);
        }
        catch (OverflowException) { return Reject("arithmetic-overflow"); }
    }

    private ConstructionTransition Reject(string code) => new(false, code, this, null);
    private static IReadOnlyDictionary<TKey, TValue> ReadOnly<TKey, TValue>(Dictionary<TKey, TValue> values) where TKey : notnull => new ReadOnlyDictionary<TKey, TValue>(values);

    private sealed class Candidate
    {
        public int OwnerId;
        public ConstructionQueueSnapshot? Queues;
        public readonly Dictionary<int, IReadOnlyDictionary<string, decimal>> Accounts;
        public readonly Dictionary<string, ConstructionOrder> Orders;
        public readonly Dictionary<string, ConstructionInstalled> Installed;
        public readonly Dictionary<string, ConstructionReceipt> Receipts;

        public Candidate(ConstructionLedger state)
        {
            OwnerId = state.OwnerId;
            Queues = state.Queues;
            Accounts = state.Accounts.ToDictionary(pair => pair.Key, pair => pair.Value);
            Orders = state.Orders.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            Installed = state.Installed.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            Receipts = state._receipts.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        }
    }
}

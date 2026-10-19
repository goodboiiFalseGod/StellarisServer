using Stellaris.Core.Construction;
using Stellaris.Session.M3.Construction;

internal static class AbsentPayerCoreChecks
{
    public static void Run()
    {
        var (initial, bank, quote) = CreateFixture();
        var first = initial.Enqueue("absent-first", 1, initial.Revision, "absent-order-a", "slot-a", quote, bank);
        Check(first.Accepted, "First paid fixture order accepted");
        var afterFirstPayment = Apply(bank, first.ResourceDeltas);
        var second = first.State.Enqueue("absent-second", 1, first.State.Revision, "absent-order-b", "slot-b", quote, afterFirstPayment);
        Check(second.Accepted, "Second paid fixture order accepted");
        var pending = second.State;
        var paid = Apply(afterFirstPayment, second.ResourceDeltas);
        Check(paid.Accounts[1]["minerals"] == 20m && pending.PendingOrderIds.SequenceEqual(["absent-order-a", "absent-order-b"]),
            "Both orders have been paid by their original payer");

        var accountHole = BankWithoutOwner(absentPayerIds: []);
        var unresolved = pending.RefreshPendingConditions(new Dictionary<string, decimal?>(),
            new Dictionary<string, bool?> { ["absent-order-a"] = false, ["absent-order-b"] = false }, accountHole);
        Check(!unresolved.Accepted && unresolved.Code == "bank-context-unresolved" && ReferenceEquals(unresolved.State, pending)
            && unresolved.ResourceDeltas.Count == 0 && pending.PendingOrderIds.Count == 2,
            "Missing account without explicit absence proof cannot silently settle/refund cancellations");

        var absentBank = BankWithoutOwner([1], new Dictionary<int, IReadOnlyDictionary<string, decimal>>
        {
            [2] = new Dictionary<string, decimal> { ["minerals"] = 90m },
        });
        var revision = pending.Revision;
        var absentDay = pending.AdvanceDay("absent-payer-day", revision, 0,
            new Dictionary<string, decimal?>(), new Dictionary<string, bool?>(), absentBank);
        Check(absentDay.Accepted && absentDay.State.Revision == revision + 1 && absentDay.ResourceDeltas.Count == 0
            && absentDay.State.Orders.Values.All(order => order.CancellationReason == ConstructionCancellationReason.PayerMissing && order.CompletedWork == 0),
            "Дневная фаза N=0 отменяет заказы исчезнувшего payer без работы или credit");
        var quoteA = pending.Orders["absent-order-a"].Quote;
        var quoteB = pending.Orders["absent-order-b"].Quote;
        var absentRefresh = pending.RefreshPendingConditions(new Dictionary<string, decimal?>(), new Dictionary<string, bool?>(), absentBank);
        Check(absentRefresh.Accepted && absentRefresh.Code == "accepted" && absentRefresh.ResourceDeltas.Count == 0
            && absentRefresh.State.Revision == revision && absentRefresh.AppliedRevision == revision
            && absentRefresh.State.PendingOrderIds.Count == 0,
            "Confirmed missing payer cancels pending orders inside N=0 refresh without refund or revision");
        Check(absentRefresh.State.Orders.Values.All(order => order.Status == ConstructionOrderStatus.Cancelled
                && order.CancellationReason == ConstructionCancellationReason.PayerMissing && order.CompletedWork == 0m)
            && ReferenceEquals(absentRefresh.State.Orders["absent-order-a"].Quote, quoteA)
            && ReferenceEquals(absentRefresh.State.Orders["absent-order-b"].Quote, quoteB),
            "PayerMissing has priority over unknown speed/condition and retains work/quotes");
        var repeated = absentRefresh.State.RefreshPendingConditions(new Dictionary<string, decimal?>(), new Dictionary<string, bool?>(), absentBank);
        Check(repeated.Accepted && repeated.ResourceDeltas.Count == 0 && repeated.State.Revision == revision
            && repeated.State.Orders.Values.All(order => order.CancellationReason == ConstructionCancellationReason.PayerMissing),
            "Repeated refresh cannot credit or refund the already cancelled absent payer");

        var transfer = pending.ChangeOwner("transfer-absent-payer", pending.Revision, 2, destroyed: false, absentBank);
        Check(transfer.Accepted && transfer.State.OwnerId == 2 && !transfer.State.Destroyed && transfer.ResourceDeltas.Count == 0
            && transfer.State.Orders.Values.All(order => order.Status == ConstructionOrderStatus.Cancelled
                && order.CancellationReason == ConstructionCancellationReason.PayerMissing)
            && absentBank.Accounts[2]["minerals"] == 90m,
            "Ownership transfer does not redirect absent payer refund to the new owner");
        var stagedTransfer = ConstructionLiveTransaction.Stage(transfer, absentBank);
        var stagedBank = stagedTransfer.Bank ?? throw new InvalidOperationException("Staged bank is missing.");
        Check(stagedTransfer.Accepted && stagedBank.AbsentPayerIds.SetEquals([1])
            && stagedBank.Accounts[2]["minerals"] == 90m,
            "Trusted staging preserves absent-payer evidence without changing the new owner's account");

        var ownerInput = new HashSet<int> { 3 };
        var immutableEvidence = new ConstructionBankSnapshot(new Dictionary<int, IReadOnlyDictionary<string, decimal>>
        {
            [1] = new Dictionary<string, decimal> { ["minerals"] = 10m },
        }, Storage(), ownerInput);
        ownerInput.Clear();
        Check(immutableEvidence.AbsentPayerIds.SetEquals([3]), "Absent-payer input collection is copied immutably");
        Reject(() => new ConstructionBankSnapshot(new Dictionary<int, IReadOnlyDictionary<string, decimal>>
        {
            [1] = new Dictionary<string, decimal> { ["minerals"] = 10m },
        }, Storage(), [1]), "Payer cannot be both present and explicitly absent");
        Reject(() => BankWithoutOwner([0]), "Absent payer IDs must be positive");
        Reject(() => BankWithoutOwner([-2]), "Negative absent payer ID is invalid");
        Reject(() => BankWithoutOwner(Enumerable.Repeat(1, 4097)), "Объём входного evidence ограничен до дедупликации");

        var destroyedBank = new ConstructionBankSnapshot(new Dictionary<int, IReadOnlyDictionary<string, decimal>>(), Storage());
        var destroyed = pending.ChangeOwner("destroy-without-bank", pending.Revision, 2, destroyed: true, destroyedBank);
        Check(destroyed.Accepted && destroyed.State.Destroyed && destroyed.State.OwnerId == 2 && destroyed.ResourceDeltas.Count == 0
            && destroyed.State.Orders.Values.All(order => order.Status == ConstructionOrderStatus.Cancelled
                && order.CancellationReason == ConstructionCancellationReason.Destroyed),
            "Destroyed colony may close without payer/new-owner accounts and records Destroyed without refunds");

        var order = pending.Orders["absent-order-a"];
        var preview = M3ConstructionAdapter.RefundPreview(pending, order, absentBank);
        var extantRefund = absentBank.Refund(order with { PayerId = 2 });
        Check(extantRefund.Single() is { PayerId: 2, ResourceId: "minerals", Amount: 10m }, "Другой существующий payer сохраняет свой capped refund");
        Check(preview.Amounts.Count == 0 && preview.Losses is { Count: 1 } && preview.Losses["minerals"] == 40m
            && preview.PreviewId is { Length: 64 },
            "Refund preview for confirmed absent payer is empty, shows full loss, and remains fingerprinted");
        var previewRejected = false;
        try { _ = M3ConstructionAdapter.RefundPreview(pending, order, accountHole); }
        catch (KeyNotFoundException) { previewRejected = true; }
        Check(previewRejected, "Refund preview does not infer absence from an incomplete accounts dictionary");

        Console.WriteLine("PASS Core absent payer: explicit evidence versus account hole, N=0 condition priority, no redirect/refund, destroy, immutable/staged evidence and preview loss");
    }

    private static (ConstructionLiveRuntime State, ConstructionBankSnapshot Bank, ConstructionQuote Quote) CreateFixture()
    {
        var state = new ConstructionLiveRuntime("absent-payer-colony", 1,
            [new("slot-a", ConstructionKind.Building, "zone-a", "zone"), new("slot-b", ConstructionKind.Building, "zone-b", "zone")], []);
        var quote = new ConstructionQuote("fixture-profile", "fixture-policy", "building-new", ConstructionKind.Building, 5m,
            new Dictionary<string, decimal> { ["minerals"] = 40m }, Empty, Empty, []);
        var bank = new ConstructionBankSnapshot(new Dictionary<int, IReadOnlyDictionary<string, decimal>>
        {
            [1] = new Dictionary<string, decimal> { ["minerals"] = 100m },
        }, Storage());
        return (state, bank, quote);
    }

    private static IReadOnlyDictionary<string, decimal> Empty { get; } = new Dictionary<string, decimal>();

    private static Dictionary<string, ConstructionStorage> Storage()
        => new(StringComparer.Ordinal) { ["minerals"] = new(false, 100m) };

    private static ConstructionBankSnapshot BankWithoutOwner(IEnumerable<int> absentPayerIds,
        IReadOnlyDictionary<int, IReadOnlyDictionary<string, decimal>>? accounts = null)
        => new(accounts ?? new Dictionary<int, IReadOnlyDictionary<string, decimal>>(), Storage(), absentPayerIds);

    private static ConstructionBankSnapshot Apply(ConstructionBankSnapshot bank, IReadOnlyList<ConstructionResourceDelta> deltas)
    {
        var accounts = bank.Accounts.ToDictionary(pair => pair.Key,
            pair => (IReadOnlyDictionary<string, decimal>)pair.Value.ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal));
        foreach (var delta in deltas)
        {
            var account = (Dictionary<string, decimal>)accounts[delta.PayerId];
            account[delta.ResourceId] = checked(account.GetValueOrDefault(delta.ResourceId) + delta.Amount);
        }
        return new(accounts, bank.Storage, bank.AbsentPayerIds);
    }

    private static void Reject(Action action, string message)
    {
        try { action(); }
        catch (ArgumentException) { return; }
        throw new InvalidOperationException(message);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

using System.Reflection;
using Stellaris.Core.Construction;

internal static class ConditionRefreshCoreChecks
{
    public static void Run()
    {
        RefreshCancelsAtomicallyAndKeepsReceiptAndWork();
        UnknownConditionRollsBackWholeRefresh();
        Console.WriteLine("PASS Core RefreshPendingConditions: source/speed cancellation priority, atomic unknown rollback, capped/unlimited original-payer refunds, reuse and receipt stability");
    }

    private static void RefreshCancelsAtomicallyAndKeepsReceiptAndWork()
    {
        foreach (var unlimited in new[] { false, true })
        {
            var (state, bank, quote) = CreateFixture(unlimited);
            foreach (var (slot, order) in new[] { ("slot-a", "order-a"), ("slot-b", "order-b"), ("slot-c", "order-c") })
            {
                var enqueued = state.Enqueue("enqueue-" + order, 1, state.Revision, order, slot, quote, bank,
                    requestIdentity: "request-" + order);
                Check(enqueued.Accepted, "Enqueue fixture order " + order);
                state = enqueued.State;
                bank = Apply(bank, enqueued.ResourceDeltas);
            }

            var day = state.AdvanceDay("fixture-day", state.Revision, 1,
                new Dictionary<string, decimal?> { ["order-a"] = 0.5m },
                new Dictionary<string, bool?> { ["order-a"] = true, ["order-b"] = true, ["order-c"] = true }, bank);
            Check(day.Accepted && day.State.Orders["order-a"].CompletedWork == 1.5m, "Active order gets fixture work before refresh");
            state = day.State;

            var toggleRevision = state.Revision;
            var toggle = state.SetBuildingEnabled("toggle-before-refresh", 1, state.Revision, "slot-installed", "building-old", false,
                requestIdentity: "toggle-request");
            Check(toggle.Accepted && toggle.ResourceDeltas.Count == 0, "Existing toggle candidate before refresh");
            state = toggle.State;
            var revision = state.Revision;
            var receiptCount = ReceiptCount(state);
            var workA = state.Orders["order-a"].CompletedWork;
            var workB = state.Orders["order-b"].CompletedWork;
            var workC = state.Orders["order-c"].CompletedWork;
            var quoteA = state.Orders["order-a"].Quote;
            var quoteB = state.Orders["order-b"].Quote;
            var quoteC = state.Orders["order-c"].Quote;

            // Имитируем внешний приход минералов между оплатой и refresh: до cap остаётся только 5.
            // Actor 2 имеет отдельный счёт, чтобы проверить, что refund идёт исходному payer 1.
            bank = Bank(unlimited, 95m, 50m);
            var refreshed = state.RefreshPendingConditions(
                new Dictionary<string, decimal?> { ["order-b"] = -1m },
                new Dictionary<string, bool?> { ["order-a"] = false, ["order-b"] = null, ["order-c"] = true }, bank);

            Check(refreshed.Accepted && refreshed.Code == "accepted", "Refresh accepts trusted candidate");
            Check(refreshed.State.Revision == revision && refreshed.AppliedRevision == revision,
                "Refresh keeps candidate revision unchanged");
            Check(ReceiptCount(refreshed.State) == receiptCount, "Refresh adds no command receipt");
            Check(refreshed.State.Orders["order-a"].Status == ConstructionOrderStatus.Cancelled
                && refreshed.State.Orders["order-a"].CancellationReason == ConstructionCancellationReason.ConditionLost,
                "Known false cancels active order as ConditionLost");
            Check(refreshed.State.Orders["order-b"].Status == ConstructionOrderStatus.Cancelled
                && refreshed.State.Orders["order-b"].CancellationReason == ConstructionCancellationReason.NonPositiveSpeed,
                "Non-positive speed cancels even when eligibility is unknown");
            Check(refreshed.State.Orders["order-c"].Status == ConstructionOrderStatus.Pending
                && refreshed.State.PendingOrderIds.SequenceEqual(["order-c"])
                && ReferenceEquals(refreshed.State.Orders["order-c"], state.Orders["order-c"]), "True condition preserves remaining waiting order unchanged");
            Check(refreshed.State.Orders["order-a"].CompletedWork == workA && refreshed.State.Orders["order-b"].CompletedWork == workB
                && refreshed.State.Orders["order-c"].CompletedWork == workC, "Refresh never adds or removes work");
            Check(ReferenceEquals(refreshed.State.Orders["order-a"].Quote, quoteA)
                && ReferenceEquals(refreshed.State.Orders["order-b"].Quote, quoteB)
                && ReferenceEquals(refreshed.State.Orders["order-c"].Quote, quoteC), "Refresh retains immutable quotes");

            var mineralRefund = refreshed.ResourceDeltas.Where(delta => delta.ResourceId == "minerals").ToArray();
            Check(mineralRefund.Length == 1 && mineralRefund[0].PayerId == 1
                && mineralRefund[0].Amount == (unlimited ? 40m : 5m), "Cancellations aggregate capped refund to original payer (or full unlimited refund)");
            var appliedBank = Apply(bank, refreshed.ResourceDeltas);
            Check(appliedBank.Accounts[1]["minerals"] == (unlimited ? 135m : 100m)
                && appliedBank.Accounts[2]["minerals"] == 50m, "Refund credits payer account only once");

            var replay = refreshed.State.ReplayCommand("toggle-before-refresh", 1, toggleRevision, "toggle-request");
            Check(replay is { Accepted: true, Code: "duplicate" } && ReferenceEquals(replay.State, refreshed.State),
                "Preceding command receipt still replays as duplicate after refresh");
            var repeated = refreshed.State.RefreshPendingConditions(
                new Dictionary<string, decimal?> { ["order-b"] = -1m },
                new Dictionary<string, bool?> { ["order-a"] = false, ["order-b"] = null, ["order-c"] = true }, appliedBank);
            Check(repeated.Accepted && repeated.ResourceDeltas.Count == 0 && repeated.State.Revision == revision
                && ReceiptCount(repeated.State) == receiptCount, "Second refresh cannot issue the same refund again or add a receipt");

            var reused = repeated.State.Enqueue("enqueue-reused", 1, repeated.State.Revision, "order-reused", "slot-a", quote, appliedBank,
                requestIdentity: "request-reused");
            Check(reused.Accepted && reused.State.PendingOrderIds.Contains("order-reused")
                && reused.State.Orders["order-a"].Status == ConstructionOrderStatus.Cancelled,
                "Cancelled slot is reusable while a surviving order remains pending");
        }
    }

    private static void UnknownConditionRollsBackWholeRefresh()
    {
        var (state, bank, quote) = CreateFixture(unlimited: false);
        foreach (var (slot, order) in new[] { ("slot-a", "order-a"), ("slot-b", "order-b"), ("slot-c", "order-c") })
        {
            var enqueued = state.Enqueue("enqueue-" + order, 1, state.Revision, order, slot, quote, bank,
                requestIdentity: "request-" + order);
            Check(enqueued.Accepted, "Enqueue rollback fixture order " + order);
            state = enqueued.State; bank = Apply(bank, enqueued.ResourceDeltas);
        }
        var revision = state.Revision; var receipts = ReceiptCount(state); var pending = state.PendingOrderIds.ToArray();
        var result = state.RefreshPendingConditions(
            new Dictionary<string, decimal?>(),
            new Dictionary<string, bool?> { ["order-a"] = false, ["order-b"] = null, ["order-c"] = true }, bank);
        Check(!result.Accepted && result.Code == "condition-context-unresolved" && ReferenceEquals(result.State, state)
            && result.ResourceDeltas.Count == 0, "Unknown condition rolls back earlier local cancellations/refunds");
        Check(result.State.Revision == revision && ReceiptCount(result.State) == receipts
            && result.State.PendingOrderIds.SequenceEqual(pending)
            && result.State.Orders.Values.All(order => order.Status == ConstructionOrderStatus.Pending),
            "Unknown rollback preserves original state, pending orders and receipts");
    }

    private static (ConstructionLiveRuntime State, ConstructionBankSnapshot Bank, ConstructionQuote Quote) CreateFixture(bool unlimited)
    {
        var slots = new[]
        {
            new ConstructionLiveSlot("slot-a", ConstructionKind.Building, "zone-a", "zone"),
            new ConstructionLiveSlot("slot-b", ConstructionKind.Building, "zone-b", "zone"),
            new ConstructionLiveSlot("slot-c", ConstructionKind.Building, "zone-c", "zone"),
            new ConstructionLiveSlot("slot-installed", ConstructionKind.Building, "zone-d", "zone"),
        };
        var installed = new[] { new ConstructionInstalled("slot-installed", "building-old", ConstructionKind.Building,
            new Dictionary<string, decimal> { ["energy"] = 1m }, new Dictionary<string, decimal> { ["technician"] = 100m }) };
        var state = new ConstructionLiveRuntime("condition-refresh", 1, slots, installed);
        var quote = new ConstructionQuote("fixture-profile", "fixture-policy", "building-new", ConstructionKind.Building, 10m,
            new Dictionary<string, decimal> { ["minerals"] = 20m }, new Dictionary<string, decimal> { ["energy"] = 1m },
            new Dictionary<string, decimal> { ["technician"] = 100m }, []);
        return (state, Bank(unlimited, 100m, 50m), quote);
    }

    private static ConstructionBankSnapshot Bank(bool unlimited, decimal ownerMinerals, decimal otherMinerals)
        => new(new Dictionary<int, IReadOnlyDictionary<string, decimal>>
        {
            [1] = new Dictionary<string, decimal> { ["minerals"] = ownerMinerals },
            [2] = new Dictionary<string, decimal> { ["minerals"] = otherMinerals },
        }, new Dictionary<string, ConstructionStorage>
        {
            ["minerals"] = new(unlimited, unlimited ? null : 100m),
            ["energy"] = new(true, null),
        });

    private static ConstructionBankSnapshot Apply(ConstructionBankSnapshot bank, IReadOnlyList<ConstructionResourceDelta> deltas)
    {
        var accounts = bank.Accounts.ToDictionary(pair => pair.Key,
            pair => (IReadOnlyDictionary<string, decimal>)pair.Value.ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal));
        foreach (var delta in deltas)
        {
            var account = (Dictionary<string, decimal>)accounts[delta.PayerId];
            account[delta.ResourceId] = checked(account.GetValueOrDefault(delta.ResourceId) + delta.Amount);
        }
        return new(accounts, bank.Storage);
    }

    private static int ReceiptCount(ConstructionLiveRuntime state)
    {
        var field = typeof(ConstructionLiveRuntime).GetField("_receipts", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Construction receipt store is unavailable.");
        var value = field.GetValue(state) as IReadOnlyDictionary<string, ConstructionReceipt>
            ?? throw new InvalidOperationException("Construction receipt store has an unexpected type.");
        return value.Count;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

using Stellaris.Core.Construction;
using Stellaris.Core.Economy;

static class PaidCancellationChecks
{
    private static readonly IReadOnlyDictionary<string, decimal> _empty = new Dictionary<string, decimal>();

    public static Task Ledgers()
    {
        var costs = Amount(40);
        var quote = Quote(costs);
        costs["energy"] = 99;
        var queued = Ledger(100).Enqueue("order", 1, 0, "o", "slot", quote).State;
        var progressed = queued.Progress("work", 1, 1, "synthetic-work", [new("o", 5)]).State;
        var caps = Amount(100);
        var result = progressed.CancelPaid("cancel", 1, 2, "o", caps);
        Accepted(result);
        caps["energy"] = 0;
        Check(result.State.Accounts[1]["energy"] == 100 && result.State.Orders["o"].CompletedWork == 5 && result.State.Installed.Count == 0, "Stored payment/progress or installed effects changed.");
        Check(result.State.CancelPaid("cancel", 1, 2, "o", Amount(100)).Code == "duplicate", "Exact retry was not idempotent.");
        Rejected(result.State.CancelPaid("cancel", 1, 2, "o", Amount(99)), result.State, "command-id-conflict");
        Rejected(result.State.CancelPaid("cancel", 2, 2, "o", Amount(100)), result.State, "not-owner");
        Rejected(queued.CancelPaid("stale", 1, 0, "o", Amount(100)), queued, "stale-revision");
        Rejected(queued.CancelPaid("invalid", 1, 1, "", Amount(100)), queued, "invalid-identifier");
        Rejected(queued.CancelPaid("missing", 1, 1, "o", _empty), queued, "dependency-unresolved");
        Rejected(queued.CancelPaid("null", 1, 1, "o", null), queued, "dependency-unresolved");
        Rejected(queued.CancelPaid("negative", 1, 1, "o", Amount(-1)), queued, "invalid-refund");
        Rejected(result.State.CancelPaid("again", 1, 3, "o", Amount(100)), result.State, "order-unavailable");
        Accepted(result.State.Enqueue("next", 1, 3, "next", "slot", Quote(Amount(1))));
        foreach (var (cap, expected) in new[] { (100m, 100m), (80m, 80m), (60m, 60m), (50m, 60m) })
        {
            var cancellation = queued.CancelPaid("cap", 1, 1, "o", Amount(cap));
            Accepted(cancellation);
            Check(cancellation.State.Accounts[1]["energy"] == expected, "Cap-loss or existing-balance preservation failed.");
        }
        var huge = Ledger(decimal.MaxValue).Enqueue("huge", 1, 0, "o", "slot", Quote(Amount(decimal.MaxValue))).State;
        huge = huge.ReconcileAccounts(new Dictionary<int, IReadOnlyDictionary<string, decimal>> { [1] = Amount(decimal.MaxValue - 1) });
        var hugeRefund = huge.CancelPaid("huge-refund", 1, 1, "o", Amount(decimal.MaxValue));
        Accepted(hugeRefund);
        Check(hugeRefund.State.Accounts[1]["energy"] == decimal.MaxValue, "Headroom must be computed before addition.");
        var free = Ledger(100).Enqueue("free", 1, 0, "o", "slot", Quote(_empty)).State;
        Accepted(free.CancelPaid("free-cancel", 1, 1, "o", _empty));
        var completed = queued.Progress("finish", 1, 1, "synthetic-work", [new("o", 10)]).State;
        Rejected(completed.CancelPaid("cancel", 1, 2, "o", Amount(100)), completed, "order-unavailable");
        foreach (var amounts in new[] { Amount(39), Amount(41), new Dictionary<string, decimal> { ["energy"] = 40, ["other"] = 0 } })
        {
            var forged = new ConstructionRefund(ConstructionRefund.PAID_CANCELLATION_POLICY, amounts, Amount(100));
            Rejected(queued.Cancel("forged", 1, 1, "o", ConstructionCancellationReason.Player, forged), queued, "invalid-refund");
        }
        var paid = new ConstructionRefund(ConstructionRefund.PAID_CANCELLATION_POLICY, Amount(40), Amount(100));
        Rejected(queued.Cancel("destroyed", 1, 1, "o", ConstructionCancellationReason.Destroyed, paid), queued, "invalid-cancel-reason");
        var direct = queued.Cancel("direct", 1, 1, "o", ConstructionCancellationReason.Player, paid);
        Accepted(direct);
        Check(direct.State.Accounts[1]["energy"] == 100, "Fixed lower-level policy failed to refund actual payment.");
        var multi = new Dictionary<string, decimal> { ["energy"] = 40, ["z"] = 20 };
        var initial = new ConstructionLedger(new("c", ConstructionAuthorityMode.Individualist, false, null, null), 1, new Dictionary<int, IReadOnlyDictionary<string, decimal>> { [1] = new Dictionary<string, decimal> { ["energy"] = 100, ["z"] = 100 } }, ["slot"]);
        var two = initial.Enqueue("two", 1, 0, "o", "slot", Quote(multi)).State;
        Rejected(two.CancelPaid("later-missing", 1, 1, "o", Amount(100)), two, "dependency-unresolved");
        Check(two.Accounts[1]["energy"] == 60 && two.Accounts[1]["z"] == 80, "Later invalid resource published earlier refund.");
        var missingAccount = two.ReconcileAccounts(new Dictionary<int, IReadOnlyDictionary<string, decimal>> { [1] = Amount(60) });
        Rejected(missingAccount.CancelPaid("missing-resource", 1, 1, "o", new Dictionary<string, decimal> { ["energy"] = 100, ["z"] = 100 }), missingAccount, "invalid-refund");
        return Task.CompletedTask;
    }

    public static Task Aggregates()
    {
        var world = new EconomyWorld([new("energy", 100)], [], []);
        var country = world.CreateCountry();
        var first = world.CreatePlanet(country);
        var second = world.CreatePlanet(country);
        world.SetBalance(country, "energy", 100);
        var aggregate = new ConstructionAggregate(world, new Dictionary<int, EntityId> { [1] = country }, [new("a", 1, first), new("b", 1, second)], [Ledger(100, "a"), Ledger(100, "b")]);
        var queued = aggregate.Enqueue("build", 1, 0, "a", 0, "o", "slot", Quote(Amount(40))).State;
        var hash = queued.EconomyHash();
        world.SetBalance(country, "energy", 0);
        var detached = queued.ForkEconomy();
        detached.SetBalance(country, "energy", 1);
        var refund = queued.CancelPaid("cancel", 1, 1, "a", 1, "o");
        Check(refund.Accepted && refund.State.GetBalance(1, "energy") == 100 && refund.State.Ledgers["b"].Accounts[1]["energy"] == 100, "Actual world refund or shared reconciliation failed.");
        Check(refund.State.Month == 0 && refund.State.Ledgers["a"].Installed.Count == 0 && refund.State.ForkEconomy().GetBuildingUpkeep(first).Count == 0 && queued.EconomyHash() == hash, "Cancellation changed time/effects or mutated previous candidate.");
        var replay = refund.State.CancelPaid("cancel", 1, 1, "a", 1, "o");
        Check(replay.Accepted && replay.Code == "duplicate" && ReferenceEquals(replay.State, refund.State), "Aggregate retry mutated refund.");
        foreach (var actor in new[] { 0, 2 })
        {
            Check(queued.CancelPaid("foreign", actor, 1, "a", 1, "o").Code == "not-owner", "Foreign/observer cancelled.");
        }
        Check(refund.State.CancelPaid("cancel", 1, 1, "b", 1, "o").Code == "command-id-conflict", "Changed colony replay accepted.");
        Check(queued.CancelPaid("stale", 1, 0, "a", 1, "o").Code == "stale-revision", "Stale aggregate accepted.");
        Check(queued.CancelPaid("stale-ledger", 1, 1, "a", 0, "o").Code == "stale-revision" && queued.EconomyHash() == hash, "Stale ledger changed economy.");
        foreach (var balance in new[] { 99m, 100m })
        {
            world.SetBalance(country, "energy", balance);
            var paidLedger = Ledger(decimal.MaxValue, "a").Enqueue("huge", 1, 0, "o", "slot", Quote(Amount(decimal.MaxValue))).State;
            var nearCap = new ConstructionAggregate(world, new Dictionary<int, EntityId> { [1] = country }, [new("a", 1, first), new("b", 1, second)], [paidLedger, Ledger(balance, "b")]);
            var capped = nearCap.CancelPaid("cap", 1, 0, "a", 1, "o");
            Check(capped.Accepted && capped.State.GetBalance(1, "energy") == 100 && capped.State.Ledgers["b"].Accounts[1]["energy"] == 100, "Aggregate cap loss overflowed or used non-world caps.");
        }
        return Task.CompletedTask;
    }

    private static Dictionary<string, decimal> Amount(decimal value) => new() { ["energy"] = value };
    private static ConstructionQuote Quote(IReadOnlyDictionary<string, decimal> costs) => new("synthetic", "fixed-price", "building", ConstructionKind.Building, 10, costs, _empty, _empty, []);
    private static ConstructionLedger Ledger(decimal balance, string colony = "c") => new(new(colony, ConstructionAuthorityMode.Individualist, false, null, null), 1, new Dictionary<int, IReadOnlyDictionary<string, decimal>> { [1] = Amount(balance) }, ["slot"]);
    private static void Accepted(ConstructionTransition result) => Check(result.Accepted, result.Code);
    private static void Rejected(ConstructionTransition result, ConstructionLedger original, string code) => Check(!result.Accepted && result.Code == code && ReferenceEquals(result.State, original), "Expected atomic " + code + ", got " + result.Code);
    private static void Check(bool value, string message)
    {
        if (!value) { throw new InvalidOperationException(message); }
    }
}
